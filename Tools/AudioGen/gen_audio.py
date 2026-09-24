"""
gen_audio.py - procedural audio for the RPG prototype (Unity 6).

Everything is synthesized from scratch with numpy: band-limited oscillators,
white/pink/brown noise, envelopes, time-varying biquad filters (vectorised
blocked-scan IIR, no scipy), FFT convolution reverb and echoes.

Writes 16-bit PCM mono WAVs:
    Assets/Audio/SFX/sfx_*.wav      44100 Hz, peak -1 dBFS, faded ends
    Assets/Audio/Music/*.wav        32000 Hz, sample-exact seamless loops

Run:   python gen_audio.py                 build everything (+ verification table)
       python gen_audio.py hit boss        only sounds whose name contains a word
Output is deterministic: every sound is seeded from its own name.
"""
import os
import sys
import time
import wave
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SFX_DIR = os.path.join(ROOT, "Assets", "Audio", "SFX")
MUSIC_DIR = os.path.join(ROOT, "Assets", "Audio", "Music")

SR = 44100      # SFX sample rate
MSR = 32000     # music / ambience sample rate
TAU = 2.0 * np.pi


# ============================================================================
# basic helpers
# ============================================================================
def seed_of(name):
    return zlib.crc32(name.encode("utf-8"))


def ns(dur, sr=SR):
    return max(1, int(round(dur * sr)))


def tvec(n, sr=SR):
    return np.arange(n) / sr


def arr(v, n):
    """Broadcast a scalar / array to length n (short arrays hold their last value)."""
    a = np.asarray(v, dtype=float)
    if a.ndim == 0:
        return np.full(n, float(a))
    if len(a) >= n:
        return a[:n]
    return np.concatenate([a, np.full(n - len(a), a[-1])])


def norm(x, peak=1.0):
    m = np.max(np.abs(x)) if len(x) else 0.0
    return x * (peak / m) if m > 1e-12 else x


def rmsn(x, target=0.3):
    r = np.sqrt(np.mean(x * x)) if len(x) else 0.0
    return x * (target / r) if r > 1e-12 else x


def drive(x, amt=2.0):
    """tanh saturation of the signal normalised to +-1."""
    return np.tanh(amt * norm(x)) / np.tanh(amt)


def place(buf, sig, start, taper=64):
    """Add sig into buf at sample offset start (clipped to the buffer).
    A layer whose first/last sample is not ~0 gets a short raised-cosine taper
    so that no layer can start or stop with a click."""
    s = int(round(start))
    if s >= len(buf) or s + len(sig) <= 0:
        return buf
    if taper and len(sig) > 4 * taper:
        pk = np.max(np.abs(sig))
        head, tail = abs(sig[0]) > 1e-3 * pk, abs(sig[-1]) > 1e-3 * pk
        if head or tail:
            sig = sig.copy()
            w = 0.5 - 0.5 * np.cos(np.pi * np.arange(taper) / taper)
            if head:
                sig[:taper] *= w
            if tail:
                sig[-taper:] *= w[::-1]
    a = max(0, -s)
    e = min(len(buf), s + len(sig))
    buf[s + a:e] += sig[a:e - s]
    return buf


def xsweep(f0, f1, n, power=1.0):
    """Exponential frequency sweep f0 -> f1 over n samples."""
    u = np.linspace(0.0, 1.0, n) ** power
    return f0 * (f1 / f0) ** u


def seg_sweep(points, n):
    """Frequency curve through (u in 0..1, Hz) breakpoints, interpolated in log-frequency."""
    us, fs = zip(*points)
    return np.exp(np.interp(np.linspace(0.0, 1.0, n), us, np.log(fs)))


# ============================================================================
# envelopes
# ============================================================================
def ramp_in(n, att, sr=SR):
    e = np.ones(n)
    a = int(att * sr)
    if a > 0:
        m = min(a, n)
        e[:m] = 0.5 - 0.5 * np.cos(np.pi * np.arange(m) / a)
    return e


def ramp_out(n, dur, sr=SR):
    return ramp_in(n, dur, sr)[::-1]


def env_exp(n, tau, sr=SR):
    return np.exp(-np.arange(n) / (max(tau, 1e-5) * sr))


def env_perc(n, att, tau, sr=SR):
    return ramp_in(n, att, sr) * env_exp(n, tau, sr)


def env_hump(n, peak=0.3, rise=2.0, fall=2.0):
    """0 -> 1 at `peak` (fraction of length) -> 0 at the end."""
    x = (np.arange(n) + 0.5) / n
    return np.where(x < peak, (x / peak) ** rise, ((1.0 - x) / (1.0 - peak)) ** fall)


def env_pts(points, n, sr=SR):
    """Piecewise-linear envelope from (seconds, value) breakpoints."""
    ts, vs = zip(*points)
    return np.interp(tvec(n, sr), ts, vs)


def env_note(n, dur, att=0.005, tau=None, rel=0.05, sr=SR, sus=1.0):
    """Attack, optional exp decay toward `sus`, exp release after `dur` s, zero at the end."""
    t = tvec(n, sr)
    e = ramp_in(n, att, sr)
    if tau is not None:
        e = e * (sus + (1.0 - sus) * np.exp(-t / tau))
    e = e * np.where(t > dur, np.exp(-(t - dur) / max(rel, 1e-4)), 1.0)
    return e * ramp_out(n, min(0.004, n / sr / 4.0), sr)


# ============================================================================
# oscillators & noise
# ============================================================================
def phase(freq, n, sr=SR, ph0=0.0):
    """Phase accumulation (in cycles) for a scalar or per-sample frequency."""
    f = arr(freq, n)
    ph = np.empty(n)
    ph[0] = 0.0
    np.cumsum(f[:-1] / sr, out=ph[1:])
    return ph + ph0, f


def _blep(t, dt):
    y = np.zeros_like(t)
    m = t < dt
    x = t[m] / dt[m]
    y[m] = x + x - x * x - 1.0
    m = t > 1.0 - dt
    x = (t[m] - 1.0) / dt[m]
    y[m] = x * x + x + x + 1.0
    return y


def osc(kind, freq, n, sr=SR, duty=0.5, ph0=0.0):
    """sine / saw / square (PWM via duty) / tri. Saw & square are polyBLEP band-limited."""
    ph, f = phase(freq, n, sr, ph0)
    if kind == "sine":
        return np.sin(TAU * ph)
    fr = ph % 1.0
    dt = np.clip(np.abs(f) / sr, 1e-7, 0.5)
    if kind == "saw":
        return 2.0 * fr - 1.0 - _blep(fr, dt)
    if kind == "square":
        d = np.clip(arr(duty, n), 0.02, 0.98)
        y = np.where(fr < d, 1.0, -1.0) + _blep(fr, dt) - _blep((fr - d) % 1.0, dt)
        return y - (2.0 * d - 1.0)
    if kind == "tri":
        return 1.0 - 4.0 * np.abs(((fr + 0.25) % 1.0) - 0.5)
    raise ValueError(kind)


def white(n, rng):
    return rng.standard_normal(n) * 0.35


def colored(n, rng, power=1.0, fmin=30.0, sr=SR):
    """1/f^power noise shaped in the frequency domain (periodic over n samples)."""
    X = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / sr)
    X = X / np.maximum(f, fmin) ** (power / 2.0)
    X[0] = 0.0
    return rmsn(np.fft.irfft(X, n), 0.35)


def pink(n, rng, sr=SR):
    return colored(n, rng, 1.0, 30.0, sr)


def brown(n, rng, sr=SR):
    return colored(n, rng, 2.0, 25.0, sr)


def smooth_rand(n, rate, rng, sr=SR):
    """Smooth random control signal in [-1, 1] (cosine-interpolated random points)."""
    k = int(np.ceil(n / sr * rate)) + 2
    pts = rng.uniform(-1.0, 1.0, k)
    pos = np.arange(n) / sr * rate
    i = pos.astype(int)
    w = 0.5 - 0.5 * np.cos(np.pi * (pos - i))
    return pts[i] * (1.0 - w) + pts[i + 1] * w


# ============================================================================
# filters - vectorised first-order recurrences
# ============================================================================
def _scan(p, x, L=64):
    """y[n] = p[n] * y[n-1] + x[n]   (real or complex, |p| < 1).

    Blocked parallel scan: inside each block of L samples the recurrence is
    solved in closed form with cumulative products (log domain); only the block
    boundary states are propagated with a tiny Python loop.
    """
    n = len(x)
    cplx = np.iscomplexobj(p) or np.iscomplexobj(x)
    dt = np.complex128 if cplx else np.float64
    p = np.asarray(p, dtype=dt)
    p = np.full(n, p, dtype=dt) if p.ndim == 0 else p
    x = np.asarray(x, dtype=dt)
    m = -(-n // L)
    pad = m * L - n
    if pad:
        p = np.concatenate([p, np.full(pad, 0.5, dtype=dt)])
        x = np.concatenate([x, np.zeros(pad, dtype=dt)])
    G = np.cumsum(np.log(p.reshape(m, L)), axis=1)
    E = np.exp(G)
    Z = E * np.cumsum(x.reshape(m, L) * np.exp(-G), axis=1)
    ze = Z[:, -1].tolist()
    ee = E[:, -1].tolist()
    s0 = [0.0] * m
    s = 0.0
    for c in range(m):
        s0[c] = s
        s = ze[c] + ee[c] * s
    Y = Z + E * np.asarray(s0, dtype=dt)[:, None]
    return Y.reshape(-1)[:n]


def lp1(x, fc, sr=SR):
    """One-pole lowpass (6 dB/oct); fc scalar or per-sample."""
    fc = np.clip(np.asarray(fc, dtype=float), 0.5, 0.45 * sr)
    p = np.exp(-TAU * fc / sr)
    return np.real(_scan(p, (1.0 - p) * x))


def hp1(x, fc, sr=SR):
    return x - lp1(x, fc, sr)


def biquad(x, kind, fc, q=0.707, sr=SR):
    """RBJ lowpass / highpass / bandpass with scalar or per-sample fc and q.

    The biquad is run in modal (partial-fraction) form: one complex one-pole
    recurrence (pole p) whose real part, plus a direct term, equals the 2-pole
    filter exactly for static coefficients and stays stable when swept.
    """
    fc = np.clip(np.asarray(fc, dtype=float), 8.0, 0.46 * sr)
    q = np.maximum(np.asarray(q, dtype=float), 0.55)
    w0 = TAU * fc / sr
    cw, sw = np.cos(w0), np.sin(w0)
    al = sw / (2.0 * q)
    a0 = 1.0 + al
    if kind == "lp":
        b0 = (1.0 - cw) / 2.0
        b1 = 1.0 - cw
        b2 = b0
    elif kind == "hp":
        b0 = (1.0 + cw) / 2.0
        b1 = -(1.0 + cw)
        b2 = b0
    elif kind == "bp":
        b0 = al
        b1 = 0.0 * al
        b2 = -al
    else:
        raise ValueError(kind)
    b0, b1, b2 = b0 / a0, b1 / a0, b2 / a0
    a1 = -2.0 * cw / a0
    a2 = (1.0 - al) / a0
    im = np.sqrt(np.maximum(sw * sw - al * al, 1e-18)) / a0
    p = cw / a0 + 1j * im
    d = b2 / a2
    A = ((b0 - d) * p + (b1 - d * a1)) / (2j * im)
    u = _scan(p, x)
    return d * x + 2.0 * np.real(A * u)


def lp(x, fc, q=0.707, sr=SR):
    return biquad(x, "lp", fc, q, sr)


def hp(x, fc, q=0.707, sr=SR):
    return biquad(x, "hp", fc, q, sr)


def bp(x, fc, q=1.0, sr=SR):
    return biquad(x, "bp", fc, q, sr)


def formant(x, specs, sr=SR):
    """Parallel band-passes: specs = [(freq (scalar/array), q, gain), ...]."""
    y = np.zeros(len(x))
    for f, q, g in specs:
        y += g * bp(x, f, q, sr)
    return y


# ============================================================================
# convolution reverb / echo
# ============================================================================
def fftconv(x, h, n_out=None):
    n = len(x) + len(h) - 1
    N = 1 << (n - 1).bit_length()
    y = np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(h, N), N)
    return y[:(n_out or n)]


def cconv(x, h):
    """Circular convolution over len(x) (reverb tails wrap -> seamless loops)."""
    N = len(x)
    hh = np.zeros(N)
    for i in range(0, len(h), N):
        seg = h[i:i + N]
        hh[:len(seg)] += seg
    return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(hh), N)


def make_ir(t60=1.0, sr=SR, predelay=0.012, damp=3500.0, seed=1, length=None):
    """Synthetic room: early reflections + exponentially decaying noise whose
    highs die faster than its lows. Energy-normalised."""
    rng = np.random.default_rng(seed)
    n = ns(length or t60 * 1.15, sr)
    t = tvec(n, sr)
    nz = rng.standard_normal(n)
    lo = lp1(nz, damp, sr)
    hi = nz - lo
    tau = t60 / 6.91
    ir = lo * np.exp(-t / tau) + 0.5 * hi * np.exp(-t / (0.45 * tau))
    ir *= 1.0 - np.exp(-t / 0.005)
    for _ in range(10):
        k = int(rng.uniform(0.003, 0.05) * sr)
        if k < n:
            ir[k] += rng.choice([-1.0, 1.0]) * rng.uniform(2.0, 5.0) * np.exp(-t[k] / tau)
    ir = np.concatenate([np.zeros(int(predelay * sr)), ir])
    return ir / np.sqrt(np.sum(ir * ir))


def reverb(x, wet=0.25, t60=1.0, sr=SR, seed=3, damp=3500.0, predelay=0.012):
    ir = make_ir(t60, sr, predelay, damp, seed)
    return x + wet * fftconv(x, ir, len(x))


def echo(x, delay, fb=0.35, taps=4, sr=SR, damp=None):
    y = x.copy()
    d = int(delay * sr)
    cur = x
    g = 1.0
    for i in range(1, taps + 1):
        g *= fb
        if damp:
            cur = lp1(cur, damp, sr)
        k = d * i
        if k >= len(x):
            break
        y[k:] += g * cur[:len(x) - k]
    return y


# ============================================================================
# music helpers
# ============================================================================
_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def midi(name):
    pc = _PC[name[0].upper()]
    i = 1
    while i < len(name) and name[i] in "#b":
        pc += 1 if name[i] == "#" else -1
        i += 1
    return 12 * (int(name[i:]) + 1) + pc


def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def nf(name):
    return mtof(midi(name))


def parse_seq(s):
    """'D5:3 F5:1 -:4' -> [(start_16th, len_16ths, midi)], total length ('-' = rest)."""
    out, pos = [], 0.0
    for tok in s.split():
        nm, d = tok.split(":")
        d = float(d)
        if nm != "-":
            out.append((pos, d, midi(nm)))
        pos += d
    return out, pos


# ============================================================================
# shared SFX layers
# ============================================================================
def partials(n, freqs, amps, taus, sr=SR, att=0.0008, rng=None):
    """Sum of exponentially decaying sines (bells, rings, metal)."""
    t = tvec(n, sr)
    y = np.zeros(n)
    for f, a, tau in zip(freqs, amps, taus):
        if f < 0.45 * sr:
            ph = rng.uniform(0, TAU) if rng is not None else 0.0
            y += a * np.sin(TAU * f * t + ph) * np.exp(-t / tau)
    return y * ramp_in(n, att, sr)


def chime(f, n, tau=0.3, sr=SR, bright=0.35, att=0.002):
    """Soft bell: fundamental + a few slightly stretched harmonics that decay faster."""
    return partials(n, [f, 2.0 * f, 3.01 * f, 4.2 * f],
                    [1.0, bright, bright * 0.35, bright * 0.2],
                    [tau, tau * 0.55, tau * 0.35, tau * 0.2], sr, att)


def fm_bell(f, n, ratio=3.5, index=1.5, tau=0.3, itau=0.1, sr=SR):
    t = tvec(n, sr)
    I = index * np.exp(-t / itau)
    y = np.sin(TAU * f * t + I * np.sin(TAU * f * ratio * t))
    return y * np.exp(-t / tau) * ramp_in(n, 0.001, sr)


def whoosh(n, rng, points, q=1.2, peak=0.35, rise=2.0, fall=2.0, color="white", sr=SR):
    """Band-passed noise swept through `points` under a hump envelope."""
    src = white(n, rng) if color == "white" else pink(n, rng, sr)
    return rmsn(bp(src, seg_sweep(points, n), q, sr)) * env_hump(n, peak, rise, fall)


def thump(n, f_hi, f_lo, tau, sweep=0.03, att=0.0005, sr=SR):
    """Sine kick / body thump with exponential pitch drop."""
    t = tvec(n, sr)
    f = f_lo + (f_hi - f_lo) * np.exp(-t / sweep)
    return osc("sine", f, n, sr) * env_perc(n, att, tau, sr)


def grains(n, rng, times, freqs, taus, amps, q=4.0, sr=SR, ring=0.3):
    """Short band-passed noise ticks (debris, gravel, shards)."""
    y = np.zeros(n)
    for t0, f, tau, a in zip(times, freqs, taus, amps):
        s = int(t0 * sr)
        if s >= n:
            continue
        m = min(int(tau * sr * 7) + 32, n - s)
        tt = tvec(m, sr)
        e = np.exp(-tt / tau) * (1.0 - np.exp(-tt / 0.0003))
        g = norm(bp(rng.standard_normal(m), f, q, sr)) * e
        if ring:
            g = g + ring * np.sin(TAU * f * tt + rng.uniform(0, TAU)) * e
        y[s:s + m] += a * g
    return y


def crackle(n, rng, rate, sr=SR, hpf=1800.0, grain=0.0012):
    """Poisson crackle; rate = events per second (scalar or per-sample)."""
    rate = arr(rate, n)
    k = np.nonzero(rng.random(n) < rate / sr)[0]
    imp = np.zeros(n)
    imp[k] = rng.uniform(0.15, 1.0, len(k)) ** 2 * rng.choice([-1.0, 1.0], len(k))
    g = int(grain * sr) + 8
    gr = rng.standard_normal(g) * np.exp(-np.arange(g) / (grain * sr / 3.0))
    return hp(fftconv(imp, gr, n), hpf, 0.707, sr)


def sparkles(n, rng, count, t_lo, t_hi, f_lo=3000.0, f_hi=7000.0, tau=0.02, shape=1.0, sr=SR):
    """Random high sine pings between t_lo and t_hi seconds."""
    y = np.zeros(n)
    for t0 in t_lo + (t_hi - t_lo) * rng.random(count) ** shape:
        m = ns(tau * 6 + 0.005, sr)
        f = rng.uniform(f_lo, f_hi)
        place(y, rng.uniform(0.4, 1.0) * np.sin(TAU * f * tvec(m, sr)) * env_perc(m, 0.0008, tau, sr), t0 * sr)
    return y


# ============================================================================
# SFX  (each function gets a seeded rng and returns a float array at SR)
# ============================================================================
SFX = {}


def sfx(name):
    def deco(fn):
        SFX[name] = fn
        return fn
    return deco


# ---------------------------------------------------------------- melee
@sfx("sfx_swing")
def _swing(rng):
    n = ns(0.26)
    body = whoosh(n, rng, [(0, 400), (0.4, 2800), (1, 1000)], q=1.3, peak=0.4, rise=1.6, fall=2.4)
    fc = seg_sweep([(0, 600), (0.4, 4200), (1, 1600)], n)
    whistle = rmsn(bp(white(n, rng), fc, 9.0)) * env_hump(n, 0.42, 2.5, 3.0)
    air = rmsn(hp(white(n, rng), 5000)) * env_hump(n, 0.35, 2.0, 3.0)
    return body + 0.3 * whistle + 0.2 * air


@sfx("sfx_hit")
def _hit(rng):
    n = ns(0.22)
    t = tvec(n)
    thmp = thump(n, 260, 85, 0.05, sweep=0.02)
    smack = rmsn(lp(white(n, rng), 3200 + 6000 * np.exp(-t / 0.008), 0.8)) * env_perc(n, 0.0003, 0.03)
    meat = rmsn(bp(white(n, rng), 400, 1.5)) * env_perc(n, 0.001, 0.05)
    crunch = rmsn(bp(white(n, rng), 1100, 1.2)) * env_perc(n, 0.001, 0.045)
    click = rmsn(hp(white(n, rng), 2000)) * env_perc(n, 0.0001, 0.003)
    sq = osc("square", 80 + 200 * np.exp(-t / 0.04), n, duty=0.35) * env_perc(n, 0.0005, 0.025)
    return drive(0.8 * thmp + 1.0 * smack + 0.5 * meat + 0.7 * crunch + 0.4 * click + 0.2 * sq, 2.2)


@sfx("sfx_hit_heavy")
def _hit_heavy(rng):
    n = ns(0.5)
    t = tvec(n)
    thmp = thump(n, 200, 55, 0.12, sweep=0.035)
    sub = osc("sine", 45, n) * env_perc(n, 0.004, 0.2)
    smack = rmsn(lp(white(n, rng), 2500 + 5000 * np.exp(-t / 0.012), 0.8)) * env_perc(n, 0.0004, 0.06)
    meat = rmsn(bp(white(n, rng), 300, 1.2)) * env_perc(n, 0.001, 0.09)
    crunch = rmsn(bp(white(n, rng), 750, 1.1)) * env_perc(n, 0.001, 0.08)
    click = rmsn(hp(white(n, rng), 1500)) * env_perc(n, 0.0001, 0.004)
    y = drive(0.8 * thmp + 0.2 * sub + 1.0 * smack + 0.6 * meat + 0.8 * crunch + 0.35 * click, 2.8)
    return reverb(y, 0.18, 0.45, seed=11, damp=2500)


@sfx("sfx_crit")
def _crit(rng):
    n = ns(0.62)
    imp = 0.6 * thump(n, 300, 90, 0.04, 0.015) + 0.8 * rmsn(lp(white(n, rng), 5000)) * env_perc(n, 0.0003, 0.02)
    f0 = 1480.0
    ratios = [1.0, 1.003, 1.51, 2.14, 2.145, 2.93, 3.58, 4.21, 5.05]
    amps = [1.0, 0.7, 0.55, 0.5, 0.35, 0.3, 0.22, 0.15, 0.1]
    taus = [0.32, 0.3, 0.2, 0.16, 0.15, 0.1, 0.08, 0.06, 0.04]
    ring = partials(n, [f0 * r for r in ratios], amps, taus, att=0.0005, rng=rng)
    shing = rmsn(hp(white(n, rng), 6000)) * env_perc(n, 0.0005, 0.05)
    y = 0.9 * drive(imp, 1.8) + 0.8 * norm(ring) + 0.45 * shing
    return reverb(y, 0.22, 0.8, seed=12)


@sfx("sfx_bladestorm")
def _bladestorm(rng):
    n = ns(0.85)
    y = np.zeros(n)
    amps = [0.7, 0.9, 1.0, 1.0, 0.95, 0.85, 0.75]
    for i in range(7):
        m = ns(0.14)
        pts = [(0, 600), (0.45, 3400), (1, 1100)] if i % 2 == 0 else [(0, 900), (0.4, 2800), (1, 1500)]
        s = whoosh(m, rng, pts, q=1.4, peak=0.42, rise=1.5, fall=2.2)
        s += 0.3 * rmsn(bp(white(m, rng), seg_sweep(pts, m) * 1.6, 8.0)) * env_hump(m, 0.45, 2.0, 3.0)
        place(y, amps[i] * s, i * 0.105 * SR)
    t = tvec(n)
    whirl = rmsn(bp(pink(n, rng), 380, 1.5)) * (0.5 + 0.5 * np.sin(TAU * 9.5 * t)) * env_hump(n, 0.3, 1.2, 1.5)
    return reverb(y + 0.35 * whirl, 0.12, 0.4, seed=20)


@sfx("sfx_dash")
def _dash(rng):
    n = ns(0.2)
    t = tvec(n)
    body = whoosh(n, rng, [(0, 1200), (0.35, 4200), (1, 2500)], q=0.9, peak=0.3, rise=1.4, fall=2.2)
    air = rmsn(hp(white(n, rng), 5000)) * env_hump(n, 0.25, 1.5, 2.5)
    fwip = osc("sine", 180 + 360 * np.exp(-t / 0.03), n) * env_perc(n, 0.002, 0.04)
    return body + 0.3 * air + 0.25 * fwip


# ---------------------------------------------------------------- magic
@sfx("sfx_fireball_cast")
def _fireball_cast(rng):
    n = ns(0.62)
    u = np.linspace(0.0, 1.0, n)
    env = env_hump(n, 0.8, 1.4, 1.6)
    wh = rmsn(bp(pink(n, rng), seg_sweep([(0, 250), (0.85, 2400), (1, 1800)], n), 0.9)) * env
    roar = rmsn(lp(brown(n, rng), 300 + 1400 * u ** 1.5, 0.9)) * env
    crk = norm(crackle(n, rng, 30 + 450 * u ** 1.5, hpf=2200)) * env ** 0.7
    tone = rmsn(lp(osc("saw", 110 * 4.5 ** u, n), 900, 0.9)) * env
    y = 0.9 * wh + 0.7 * roar + 0.45 * crk + 0.2 * tone
    return reverb(y, 0.15, 0.5, seed=13)


@sfx("sfx_fireball_explode")
def _fireball_explode(rng):
    n = ns(1.0)
    t = tvec(n)
    boom = thump(n, 140, 45, 0.22, sweep=0.06)
    body = rmsn(lp(brown(n, rng), 180 + 1500 * np.exp(-t / 0.12), 0.8)) * env_perc(n, 0.002, 0.3)
    blast = rmsn(lp(white(n, rng), 500 + 7000 * np.exp(-t / 0.06), 0.9)) * env_perc(n, 0.0005, 0.12)
    whomp = rmsn(bp(pink(n, rng), 700, 0.8)) * env_perc(n, 0.004, 0.2)
    rate = 700 * np.exp(-t / 0.22) + 60.0 * (t < 0.75)
    crk = norm(crackle(n, rng, rate, hpf=2500)) * np.exp(-t / 0.4)
    y = drive(0.8 * boom + 0.9 * body + 1.1 * blast + 0.6 * whomp, 2.5) + 0.5 * crk
    return reverb(y, 0.25, 1.0, seed=14, damp=2500)


@sfx("sfx_ice_cast")
def _ice_cast(rng):
    n = ns(0.75)
    u = np.linspace(0.0, 1.0, n)
    y = np.zeros(n)
    for i, f in enumerate([1318.5, 1568.0, 1975.5, 2349.3, 2637.0]):
        s = ns(0.02 + i * 0.07)
        m = n - s
        tt = tvec(m)
        glide = f * (1.0 + 0.5 * np.clip(tt / 0.6, 0, 1) ** 1.3)
        trem = 0.75 + 0.25 * np.sin(TAU * rng.uniform(16, 26) * tt)
        ph, _ = phase(glide, m)
        I = 1.2 * np.exp(-tt / 0.12)
        v = np.sin(TAU * ph + I * np.sin(TAU * 3.5 * ph))       # glassy FM
        y[s:] += 0.5 * v * trem * env_perc(m, 0.004, 0.22)
    sp = sparkles(n, rng, 14, 0.05, 0.7, 3000, 7000, 0.02, shape=0.5)
    frost = rmsn(bp(white(n, rng), 5500 * 1.7 ** u, 1.5)) * env_hump(n, 0.7, 1.5, 2.0)
    y = norm(y) + 0.3 * sp + 0.25 * frost
    y = echo(y, 0.09, 0.35, 3, damp=6000)
    return reverb(y, 0.3, 0.9, seed=15)


@sfx("sfx_ice_shatter")
def _ice_shatter(rng):
    n = ns(0.8)
    crack = rmsn(hp(white(n, rng), 1800)) * env_perc(n, 0.0008, 0.018) \
        + 0.6 * rmsn(bp(white(n, rng), 3500, 2.0)) * env_perc(n, 0.0008, 0.03)
    body = 0.25 * thump(n, 420, 150, 0.04, 0.02)
    shards = np.zeros(n)
    for t0 in 0.004 + 0.55 * rng.random(30) ** 1.8:
        f = rng.uniform(2200, 7500)
        m = ns(0.25)
        tau = rng.uniform(0.03, 0.11)
        v = partials(m, [f, 2.76 * f, 5.40 * f], [1.0, 0.5, 0.25], [tau, tau * 0.6, tau * 0.35], rng=rng)
        place(shards, v * rng.uniform(0.3, 1.0) * np.exp(-t0 / 0.3), t0 * SR)
    y = 0.55 * crack + body + 0.7 * norm(shards)
    return reverb(y, 0.25, 0.7, seed=16)


@sfx("sfx_thunder")
def _thunder(rng):
    n = ns(1.6)
    t = tvec(n)
    crack = np.zeros(n)
    for t0, tau, a in [(0.0, 0.018, 1.0), (0.013, 0.012, 0.7), (0.031, 0.02, 0.85),
                       (0.058, 0.015, 0.5), (0.09, 0.035, 0.4)]:
        m = ns(0.2)
        place(crack, a * rmsn(hp(white(m, rng), 500)) * env_perc(m, 0.0002, tau), t0 * SR)
    zf = 95 * (1 + 0.35 * smooth_rand(n, 40, rng))
    zap = bp(osc("square", zf, n, duty=0.3), 2600, 1.0) * (0.5 + 0.5 * np.abs(smooth_rand(n, 90, rng)))
    zap = norm(zap) * env_perc(n, 0.0005, 0.07)
    boom = thump(n, 110, 40, 0.3, 0.08)
    roll = 0.55 + 0.45 * smooth_rand(n, 5.5, rng)
    rum_env = env_pts([(0, 0), (0.03, 0.2), (0.12, 1.0), (1.6, 0.0)], n) ** 1.6
    rumble = rmsn(lp(brown(n, rng), 700 * (160 / 700) ** (t / 1.6), 0.8)) * roll * rum_env
    grumble = rmsn(bp(pink(n, rng), 250, 0.8)) * roll * rum_env
    y = drive(1.2 * crack + 0.7 * zap + 0.35 * boom + 1.0 * rumble + 0.5 * grumble, 1.8)
    return reverb(y, 0.3, 1.3, seed=17, damp=2000)


@sfx("sfx_lightning_charge")
def _lightning_charge(rng):
    n = ns(0.6)
    u = np.linspace(0.0, 1.0, n)
    f = 70 * 4.5 ** u
    src = osc("saw", f, n) + 0.7 * osc("square", f * 1.01, n, duty=0.3)
    buzz = bp(src, 700 * 6 ** u, 1.2) + 0.4 * lp(src, 600)
    buzz = rmsn(buzz) * (0.55 + 0.45 * np.abs(smooth_rand(n, 70, rng)))
    siz = norm(crackle(n, rng, 50 + 900 * u ** 1.5, hpf=3000))
    whine = osc("sine", 900 * 3.5 ** u, n)
    return (1.0 * buzz + 0.35 * siz + 0.12 * whine) * (0.1 + 0.9 * u ** 1.3) * ramp_out(n, 0.04)


@sfx("sfx_heal")
def _heal(rng):
    n = ns(0.95)
    t = tvec(n)
    y = np.zeros(n)
    for i, name in enumerate(["G5", "B5", "D6", "G6"]):
        m = ns(0.7)
        place(y, (0.8 + 0.07 * i) * chime(nf(name), m, tau=0.32, bright=0.3), (0.01 + 0.075 * i) * SR)
    pad = np.zeros(n)
    for name in ["G4", "B4", "D5"]:
        f = nf(name) * (1 + 0.003 * np.sin(TAU * 5.0 * t + rng.uniform(0, TAU)))
        pad += osc("sine", f, n) + 0.3 * osc("tri", 2 * f, n)
    pad *= env_hump(n, 0.35, 1.5, 2.0)
    sp = sparkles(n, rng, 10, 0.25, 0.75, 3500, 6500, 0.03)
    y = norm(y) + 0.18 * norm(pad) + 0.15 * sp
    y = echo(y, 0.11, 0.3, 3, damp=5000)
    return reverb(y, 0.35, 1.1, seed=18)


@sfx("sfx_shield")
def _shield(rng):
    n = ns(0.8)
    t = tvec(n)
    vib = 1 + 0.004 * np.sin(TAU * 5.5 * t)
    hum = np.zeros(n)
    for f in [110.0, 165.0, 220.0]:
        for c in (-7, 7):
            hum += osc("saw", f * vib * 2 ** (c / 1200), n, ph0=rng.random())
    swell = env_hump(n, 0.45, 1.5, 1.8)
    hum = rmsn(lp(hum, 250 + 1400 * swell, 1.6)) * swell
    ring = (osc("sine", 880 * vib, n) + 0.6 * osc("sine", 1320 * vib, n) + 0.4 * osc("sine", 1760 * vib, n))
    ring *= (0.7 + 0.3 * np.sin(TAU * 7 * t)) * env_hump(n, 0.5, 2.0, 1.6)
    sh = sparkles(n, rng, 18, 0.15, 0.7, 4000, 8000, 0.025, shape=0.7)
    wh = whoosh(n, rng, [(0, 400), (0.6, 2600), (1, 1500)], q=1.0, peak=0.5, color="pink")
    y = 1.0 * hum + 0.3 * norm(ring) + 0.2 * sh + 0.25 * wh
    return reverb(y, 0.35, 1.0, seed=19)


@sfx("sfx_potion")
def _potion(rng):
    n = ns(0.75)
    y = np.zeros(n)
    for t0, f0 in [(0.0, 260), (0.13, 310), (0.27, 240)]:
        m = ns(0.12)
        tt = tvec(m)
        bub = osc("sine", f0 * np.exp(np.minimum(tt, 0.06) / 0.045), m) * env_perc(m, 0.003, 0.045)
        gulp = rmsn(bp(white(m, rng), 480, 2.0)) * env_perc(m, 0.002, 0.035)
        thk = osc("sine", 110 + 60 * tt / tt[-1], m) * env_perc(m, 0.003, 0.03)
        place(y, bub + 0.4 * gulp + 0.4 * thk, t0 * SR)
    sp = np.zeros(n)
    for i, name in enumerate(["C6", "E6", "G6", "C7"]):
        m = ns(0.3)
        place(sp, chime(nf(name), m, tau=0.12, bright=0.25), (0.38 + 0.045 * i) * SR)
    sp += 0.4 * sparkles(n, rng, 8, 0.4, 0.7, 4000, 7000, 0.02)
    y = norm(y) + 0.45 * norm(sp)
    return reverb(y, 0.22, 0.7, seed=21)


# ---------------------------------------------------------------- pickups
@sfx("sfx_pickup")
def _pickup(rng):
    n = ns(0.3)
    t = tvec(n)
    t1 = 0.055
    f = np.where(t < t1, nf("B5"), nf("F#6"))                  # phase-continuous 2-note blip
    env = ramp_in(n, 0.001) * np.where(t < t1, 1.0, np.exp(-(t - t1) / 0.085))
    y = lp((osc("square", f, n, duty=0.25) + 0.3 * osc("sine", 2 * f, n)) * env, 9000)
    return echo(y, 0.06, 0.25, 2)


@sfx("sfx_pickup_item")
def _pickup_item(rng):
    n = ns(0.45)
    m = ns(0.06)
    y = np.zeros(n)
    pop = osc("sine", 280 * (950 / 280) ** np.minimum(tvec(m) / 0.025, 1.0), m) * env_perc(m, 0.001, 0.02)
    place(y, pop, 0)
    place(y, 0.3 * rmsn(bp(white(m, rng), 2000, 1.5)) * env_perc(m, 0.0003, 0.004), 0)
    place(y, 0.6 * fm_bell(nf("E6"), n - ns(0.03), ratio=2.0, index=1.2, tau=0.2, itau=0.06), 0.03 * SR)
    place(y, 0.45 * fm_bell(nf("A6"), n - ns(0.09), ratio=2.0, index=0.9, tau=0.18, itau=0.05), 0.09 * SR)
    return reverb(y, 0.2, 0.6, seed=22)


# ---------------------------------------------------------------- small enemies
@sfx("sfx_slime_hop")
def _slime_hop(rng):
    n = ns(0.32)
    t = tvec(n)
    arc = np.sin(np.pi * np.clip(t / 0.2, 0, 1))
    f = (170 + 260 * arc - 40 * np.clip((t - 0.2) / 0.12, 0, 1)) * (1 + 0.07 * np.sin(TAU * 22 * t) * np.exp(-t / 0.15))
    boing = (osc("tri", f, n) + 0.3 * osc("sine", 2 * f, n)) * env_perc(n, 0.004, 0.09)
    squish = rmsn(bp(white(n, rng), 250 + 1300 * np.exp(-t / 0.05), 3.0)) \
        * (0.6 + 0.4 * np.sin(TAU * 38 * t)) * env_perc(n, 0.002, 0.04)
    low = thump(n, 95, 60, 0.05, 0.03)
    return lp(0.9 * norm(boing) + 0.5 * squish + 0.35 * low, 3800, 0.8)


@sfx("sfx_slime_die")
def _slime_die(rng):
    n = ns(0.55)
    t = tvec(n)
    splat = rmsn(lp(white(n, rng), 350 + 6000 * np.exp(-t / 0.03), 1.5)) * env_perc(n, 0.0025, 0.07)
    body = rmsn(bp(pink(n, rng), 600, 2.0)) * (0.6 + 0.4 * np.sin(TAU * 28 * t)) * env_perc(n, 0.002, 0.12)
    drops = np.zeros(n)
    for t0 in 0.05 + 0.35 * rng.random(7):
        m = ns(0.08)
        tt = tvec(m)
        f0 = rng.uniform(450, 1300)
        d = osc("sine", f0 * np.exp(np.minimum(tt, 0.04) / 0.03), m) * env_perc(m, 0.002, 0.022)
        place(drops, rng.uniform(0.5, 1.0) * d, t0 * SR)
    low = thump(n, 180, 70, 0.08, 0.04)
    return 0.8 * splat + 0.8 * body + 0.6 * drops + 0.35 * low


@sfx("sfx_shroom_puff")
def _shroom_puff(rng):
    n = ns(0.35)
    t = tvec(n)
    puff = rmsn(lp(pink(n, rng), 500 + 1800 * np.exp(-t / 0.08), 0.8)) * env_perc(n, 0.012, 0.09)
    mid = rmsn(bp(pink(n, rng), 900, 1.0)) * env_perc(n, 0.01, 0.06)
    low = thump(n, 220, 110, 0.05, 0.03, att=0.004)
    return lp1(puff + 0.3 * mid + 0.15 * low, 4500)


@sfx("sfx_spore_shot")
def _spore_shot(rng):
    n = ns(0.35)
    t = tvec(n)
    f = 380 + 1100 * np.exp(-t / 0.035)
    pew = (lp(osc("square", f, n, duty=0.3), 3000, 0.8) + 0.5 * osc("sine", f, n)) * env_perc(n, 0.001, 0.05)
    tail = rmsn(bp(white(n, rng), 2600, 1.0)) * env_hump(n, 0.25, 1.5, 2.0)
    return norm(pew) + 0.35 * tail


# ---------------------------------------------------------------- swamp (Đầm Lầy Sương Mù)
@sfx("sfx_croak")
def _croak(rng):
    """A toad's rough two-part croak: a ratcheting low buzz through nasal formants."""
    n = ns(0.55)
    y = np.zeros(n)
    for t0, dur, f0 in ((0.0, 0.2, rng.uniform(150, 175)), (0.25, 0.24, rng.uniform(122, 142))):
        m = ns(dur)
        tt = tvec(m)
        f = f0 * (1 + 0.14 * np.sin(np.pi * np.clip(tt / dur, 0, 1)))
        buzz = osc("saw", f, m) + 0.5 * osc("square", f * 0.5, m, duty=0.4)
        ratchet = 0.5 + 0.5 * (np.sin(TAU * rng.uniform(32, 40) * tt) > -0.2)
        voice = formant(buzz * ratchet, [(430, 4.0, 1.0), (1150, 5.0, 0.55), (2400, 6.0, 0.2)])
        place(y, rmsn(voice) * env_hump(m, 0.3, 1.2, 1.6), t0 * SR)
    return lp(y, 3200, 0.8)


@sfx("sfx_splash")
def _splash(rng):
    """Something heavy breaking the water: a wet burst and falling drops."""
    n = ns(0.75)
    t = tvec(n)
    burst = rmsn(bp(white(n, rng), 700 + 2600 * np.exp(-t / 0.06), 0.8)) * env_perc(n, 0.003, 0.12)
    body = rmsn(lp(pink(n, rng), 600, 0.9)) * env_perc(n, 0.002, 0.08)
    drops = np.zeros(n)
    for t0 in 0.08 + 0.5 * rng.random(9):
        m = ns(0.06)
        tt = tvec(m)
        d = osc("sine", rng.uniform(700, 1600) * np.exp(np.minimum(tt, 0.03) / 0.025), m) * env_perc(m, 0.001, 0.018)
        place(drops, rng.uniform(0.4, 1.0) * d, t0 * SR)
    low = thump(n, 140, 60, 0.08, 0.03)
    return 0.9 * burst + 0.5 * body + 0.5 * drops + 0.4 * low


@sfx("sfx_wade")
def _wade(rng):
    """A step through shallow water."""
    n = ns(0.28)
    t = tvec(n)
    slosh = rmsn(bp(white(n, rng), 500 + 1400 * np.exp(-t / 0.05), 1.0)) * env_hump(n, 0.25, 1.5, 2.2)
    drops = np.zeros(n)
    for t0 in 0.05 + 0.15 * rng.random(3):
        m = ns(0.05)
        tt = tvec(m)
        place(drops, osc("sine", rng.uniform(900, 1700) * np.exp(tt / 0.03), m) * env_perc(m, 0.001, 0.015), t0 * SR)
    return lp1(slosh + 0.35 * drops, 5000)


@sfx("sfx_hiss")
def _hiss(rng):
    """A big snake's hiss: bright breath swelling and fading, with a tongue flutter."""
    n = ns(0.9)
    t = tvec(n)
    air = rmsn(hp(white(n, rng), 3200)) * env_hump(n, 0.35, 1.4, 2.2)
    sib = rmsn(bp(white(n, rng), 6500 + 900 * np.sin(TAU * 2.2 * t), 2.0)) * env_hump(n, 0.4, 1.6, 2.0)
    flutter = 0.75 + 0.25 * np.sin(TAU * 17 * t)
    rasp = rmsn(bp(pink(n, rng), 1400, 1.5)) * env_hump(n, 0.3, 1.5, 2.5)
    return (0.8 * air + 0.6 * sib) * flutter + 0.25 * rasp


@sfx("sfx_spit")
def _spit(rng):
    """A wet spit: a plosive puff and a gurgling squelch."""
    n = ns(0.35)
    t = tvec(n)
    puff = rmsn(lp(white(n, rng), 1800 + 3000 * np.exp(-t / 0.02), 0.8)) * env_perc(n, 0.002, 0.04)
    f = 320 + 900 * np.exp(-t / 0.05)
    gurgle = rmsn(bp(white(n, rng), f, 5.0)) * (0.6 + 0.4 * np.sin(TAU * 45 * t)) * env_perc(n, 0.004, 0.09)
    return puff + 0.7 * gurgle


@sfx("sfx_splat")
def _splat(rng):
    """Poison or mud hitting the ground: a soft thump and a squelch."""
    n = ns(0.4)
    t = tvec(n)
    squelch = rmsn(bp(white(n, rng), 400 + 1800 * np.exp(-t / 0.03), 1.6)) * env_perc(n, 0.002, 0.07)
    bubbles = rmsn(bp(pink(n, rng), 900, 3.0)) * (0.5 + 0.5 * np.sin(TAU * 26 * t)) * env_perc(n, 0.004, 0.12)
    return squelch + 0.5 * bubbles + 0.5 * thump(n, 160, 70, 0.06, 0.03)


@sfx("sfx_mud_slam")
def _mud_slam(rng):
    """A mud man's fists into the mud: a heavy thud, sucking mud and clods."""
    n = ns(0.8)
    t = tvec(n)
    thud = thump(n, 110, 42, 0.16, 0.04)
    muck = rmsn(lp(brown(n, rng), 300 + 1600 * np.exp(-t / 0.05), 1.2)) * env_perc(n, 0.004, 0.18)
    suck = rmsn(bp(white(n, rng), 500 + 500 * np.clip((t - 0.15) / 0.3, 0, 1), 4.0)) * env_hump(n, 0.55, 2.0, 2.0) * 0.5
    clods = np.zeros(n)
    for t0 in 0.1 + 0.45 * rng.random(6):
        m = ns(0.05)
        place(clods, rmsn(bp(white(m, rng), rng.uniform(400, 900), 3.0)) * env_perc(m, 0.001, 0.02), t0 * SR)
    return 1.0 * thud + 0.8 * muck + suck + 0.3 * clods


@sfx("sfx_tongue")
def _tongue(rng):
    """Cóc Tía's tongue: a wet whip out and a sticky smack."""
    n = ns(0.4)
    t = tvec(n)
    whip = whoosh(n, rng, [(0, 500), (0.5, 3500), (1, 1200)], q=2.0, peak=0.35, rise=1.4, fall=3.0)
    m = ns(0.12)
    smack = rmsn(bp(white(m, rng), 1300, 1.5)) * env_perc(m, 0.001, 0.03) + 0.6 * thump(m, 300, 120, 0.03, 0.01)
    y = 0.8 * whip
    place(y, smack, 0.17 * SR)
    return y


@sfx("sfx_waystone")
def _waystone(rng):
    """A standing stone waking: a soft bell chord with a rising shimmer."""
    n = ns(1.5)
    t = tvec(n)
    y = np.zeros(n)
    for i, (name, amp) in enumerate((("A4", 0.8), ("E5", 0.6), ("A5", 0.5), ("C#6", 0.35))):
        m = ns(1.4)
        place(y, amp * fm_bell(nf(name), m, ratio=2.0, index=0.8, tau=0.7, itau=0.2), i * 0.06 * SR)
    shimmer = sparkles(n, rng, 26, 0.05, 1.1, 3500, 8000, tau=0.03)
    swell = rmsn(bp(pink(n, rng), 1800 + 2500 * np.clip(t / 1.2, 0, 1), 2.0)) * env_hump(n, 0.5, 2.0, 2.0) * 0.25
    return reverb(y + 0.3 * shimmer + swell, 0.35, 1.6, seed=61)


# ---------------------------------------------------------------- boss
def _beast_voice(n, rng, f, detunes, formants_v, formants_n, growl_hz, growl_depth, sub=0.8):
    """Detuned saws + sub square through formants, gritty AM growl, breathy formant noise."""
    saws = np.zeros(n)
    for c in detunes:
        saws += osc("saw", f * 2 ** (c / 1200), n, ph0=rng.random())
    saws += sub * osc("square", f * 0.5, n)
    gph, _ = phase(growl_hz + 0.2 * growl_hz * smooth_rand(n, 3, rng), n)
    growl = 1 - growl_depth * (0.5 + 0.5 * np.sin(TAU * gph))
    voice = formant(saws, formants_v) + 0.35 * lp(saws, 500, 0.8)
    rasp = formant(pink(n, rng), formants_n)
    return rmsn(voice) * growl + 0.6 * rmsn(rasp) * (0.6 + 0.4 * growl)


@sfx("sfx_boss_roar")
def _boss_roar(rng):
    n = ns(1.55)
    t = tvec(n)
    T = t[-1]
    u = t / T
    f = np.where(t < 0.18, 85 * (118 / 85) ** (t / 0.18), 118 * (62 / 118) ** ((t - 0.18) / (T - 0.18)))
    f = f * (1 + 0.025 * smooth_rand(n, 9, rng) + 0.01 * smooth_rand(n, 23, rng))
    F1, F2, F3 = 720 * (480 / 720) ** u, 1150 * (820 / 1150) ** u, 2500 * (2300 / 2500) ** u
    v = _beast_voice(n, rng, f, (-14, -6, 0, 7, 15),
                     [(F1, 5, 1.0), (F2, 7, 0.6), (F3, 9, 0.3)],
                     [(F1, 3, 1.0), (F2, 3, 0.6), (F3, 4, 0.4)], 32, 0.45)
    env = env_pts([(0, 0), (0.12, 1.0), (0.85, 0.9), (T, 0.0)], n)
    return reverb(drive(v * env, 2.5), 0.2, 1.2, seed=23, damp=2500)


@sfx("sfx_enrage")
def _enrage(rng):
    n = ns(1.25)
    t = tvec(n)
    T = t[-1]
    u = t / T
    f = np.where(t < 0.5, 58 * (72 / 58) ** (t / 0.5), 72 * (52 / 72) ** ((t - 0.5) / (T - 0.5)))
    f = f * (1 + 0.03 * smooth_rand(n, 8, rng))
    F1, F2 = 550 * (400 / 550) ** u, 900 * (700 / 900) ** u
    v = _beast_voice(n, rng, f, (-18, -7, 0, 9, 17),
                     [(F1, 5, 1.0), (F2, 6, 0.6), (2200, 8, 0.25)],
                     [(F1, 3, 1.0), (F2, 3, 0.6)], 26, 0.55)
    clu = sum(osc("saw", fr, n, ph0=rng.random()) for fr in (110.0, 116.5, 155.6))   # m2 + tritone
    clu = rmsn(lp(clu, 300 + 1300 * u, 1.2))
    rum = rmsn(lp(brown(n, rng), 200, 0.8))
    swell = env_pts([(0, 0), (0.7, 1.0), (1.0, 0.85), (T, 0.0)], n)
    y = drive((v + 0.3 * clu + 0.45 * rum) * swell, 3.0)
    return reverb(y, 0.25, 1.2, seed=31, damp=2200)


@sfx("sfx_boss_stomp")
def _boss_stomp(rng):
    n = ns(1.05)
    t = tvec(n)
    boom = thump(n, 160, 55, 0.24, 0.04)
    click = rmsn(lp(white(n, rng), 4000)) * env_perc(n, 0.0002, 0.006)
    thud = rmsn(lp(brown(n, rng), 250 + 900 * np.exp(-t / 0.08), 0.8)) * env_perc(n, 0.001, 0.12)
    body = rmsn(bp(white(n, rng), 180, 1.5)) * env_perc(n, 0.001, 0.12)
    crunch = rmsn(lp(white(n, rng), 1500, 0.7)) * env_perc(n, 0.0005, 0.08)
    k = 45
    times = 0.04 + 0.7 * rng.random(k) ** 1.6
    deb = grains(n, rng, times, rng.uniform(900, 4500, k), rng.uniform(0.003, 0.012, k),
                 rng.uniform(0.3, 1.0, k) * np.exp(-times / 0.3), q=4.0)
    y = drive(0.8 * boom + 0.5 * click + 0.6 * thud + 0.8 * body + 1.0 * crunch, 3.0) + 0.8 * norm(deb)
    return reverb(y, 0.2, 0.9, seed=24, damp=2500)


@sfx("sfx_boss_leap")
def _boss_leap(rng):
    n = ns(0.6)
    t = tvec(n)
    f = (95 + 40 * np.clip(t / 0.15, 0, 1)) * (1 + 0.02 * smooth_rand(n, 15, rng))
    src = osc("saw", f, n) + osc("saw", f * 1.008, n, ph0=0.3)
    grunt = formant(src, [(520, 5, 1.0), (1050, 6, 0.5), (2400, 8, 0.2)]) + 0.3 * lp(src, 400)
    grunt = drive(rmsn(grunt) * env_pts([(0, 0), (0.015, 1), (0.12, 0.8), (0.22, 0)], n), 2.0)
    wh = whoosh(n, rng, [(0, 150), (1, 1600)], q=0.9, peak=0.55, rise=1.5, fall=2.0, color="pink")
    return 0.8 * grunt + 1.0 * wh


@sfx("sfx_boss_swipe")
def _boss_swipe(rng):
    n = ns(0.45)
    t = tvec(n)
    sw = whoosh(n, rng, [(0, 300), (0.35, 2600), (1, 700)], q=1.1, peak=0.35, color="pink")
    claws = np.zeros(n)
    for t0 in (0.06, 0.075, 0.092):
        m = ns(0.12)
        c = rmsn(bp(white(m, rng), xsweep(2800, 4200, m), 6.0)) * env_hump(m, 0.3, 1.5, 2.0)
        place(claws, c, t0 * SR)
    wo = osc("sine", 120 - 40 * t / t[-1], n) * env_hump(n, 0.35)
    return 1.0 * sw + 0.35 * claws + 0.2 * wo


@sfx("sfx_rock_throw")
def _rock_throw(rng):
    n = ns(0.45)
    t = tvec(n)
    pts = [(0, 180), (0.45, 700), (1, 260)]
    body = whoosh(n, rng, pts, q=1.0, peak=0.45, color="pink")
    grit = rmsn(bp(white(n, rng), seg_sweep(pts, n) * 2.2, 1.5)) * env_hump(n, 0.45)
    vw = osc("sine", 100 - 20 * t / t[-1], n) * env_hump(n, 0.45)
    return body + 0.35 * grit + 0.2 * vw


@sfx("sfx_rock_impact")
def _rock_impact(rng):
    n = ns(0.8)
    t = tvec(n)
    th = thump(n, 160, 60, 0.08, 0.03)
    crash = rmsn(lp(white(n, rng), 900 + 5000 * np.exp(-t / 0.02), 0.8)) * env_perc(n, 0.0005, 0.06)
    crack = rmsn(bp(white(n, rng), 1600, 1.5)) * env_perc(n, 0.0003, 0.025)
    k = 60
    times = 0.02 + 0.65 * rng.random(k) ** 1.5
    crum = grains(n, rng, times, rng.uniform(500, 3000, k), rng.uniform(0.002, 0.008, k),
                  rng.uniform(0.3, 1.0, k) * np.exp(-times / 0.25), q=3.0)
    rum = rmsn(lp(brown(n, rng), 300)) * env_perc(n, 0.005, 0.2)
    y = drive(0.7 * th + 1.0 * crash + 0.7 * crack, 2.2) + 0.55 * norm(crum) + 0.3 * rum
    return reverb(y, 0.15, 0.6, seed=25)


@sfx("sfx_boulder_break")
def _boulder_break(rng):
    n = ns(0.75)
    crack = rmsn(bp(white(n, rng), 2200, 1.2)) * env_perc(n, 0.0003, 0.02) \
        + 0.6 * rmsn(hp(white(n, rng), 1000)) * env_perc(n, 0.0002, 0.008)
    th = 0.6 * thump(n, 150, 70, 0.06, 0.025)
    k = 90
    times = 0.01 + 0.6 * rng.random(k) ** 2
    crum = grains(n, rng, times, rng.uniform(400, 3500, k), rng.uniform(0.002, 0.01, k),
                  rng.uniform(0.2, 1.0, k) * np.exp(-times / 0.2), q=3.0, ring=0.2)
    bed = rmsn(bp(pink(n, rng), 1200, 0.8)) * (0.5 + 0.5 * smooth_rand(n, 30, rng)) * env_perc(n, 0.005, 0.2)
    y = drive(1.0 * crack + 0.4 * th, 1.8) + 0.7 * norm(crum) + 0.35 * bed
    return reverb(y, 0.12, 0.5, seed=32)


@sfx("sfx_telegraph")
def _telegraph(rng):
    n = ns(0.38)
    t = tvec(n)
    tick = rmsn(bp(white(n, rng), 3500, 3.0)) * env_perc(n, 0.0002, 0.004) \
        + osc("sine", 2800, n) * env_perc(n, 0.0003, 0.008)
    dip = 1 - 0.03 * t / t[-1]
    beep = lp(osc("square", 311.1 * dip, n) + osc("square", 329.6 * dip, n, ph0=0.25), 1800, 0.9)
    e = env_pts([(0, 0), (0.004, 1), (0.13, 0.85), (0.3, 0.0)], n)
    y = 0.5 * tick + 0.5 * norm(beep) * e + 0.15 * osc("sine", 55, n) * e
    return reverb(y, 0.15, 0.5, seed=29)


# ---------------------------------------------------------------- player
@sfx("sfx_player_hurt")
def _player_hurt(rng):
    n = ns(0.3)
    t = tvec(n)
    u = t / t[-1]
    f = 190 + 300 * np.exp(-t / 0.06)
    src = osc("saw", f, n) + 0.5 * osc("square", f, n, duty=0.4)
    oof = formant(src, [(500 - 100 * u, 4, 1.0), (900 - 150 * u, 5, 0.5), (2400, 7, 0.15)]) + 0.2 * src
    oof = rmsn(oof) * env_pts([(0, 0), (0.008, 1), (0.08, 0.7), (0.17, 0)], n)
    hit = thump(n, 160, 70, 0.04, 0.02) + 0.7 * rmsn(lp(white(n, rng), 3000)) * env_perc(n, 0.0003, 0.02)
    return 0.9 * norm(oof) + 0.6 * hit


@sfx("sfx_player_die")
def _player_die(rng):
    n = ns(1.25)
    y = np.zeros(n)
    for t0, d, name in [(0.0, 0.17, "E5"), (0.17, 0.17, "C5"), (0.34, 0.17, "A4")]:
        m = ns(d + 0.25)
        f = nf(name)
        v = osc("tri", f, m) + 0.3 * osc("square", f, m)
        place(y, lp(v, 2500) * env_note(m, d, 0.006, 0.18, 0.06, sus=0.6), t0 * SR)
    m = ns(0.74)
    tt = tvec(m)
    f = nf("F4") * (nf("E4") / nf("F4")) ** np.clip((tt - 0.15) / 0.35, 0, 1)   # sad semitone sag
    f = f * (1 + 0.012 * np.sin(TAU * 6 * tt) * np.clip(tt / 0.2, 0, 1))
    v = osc("tri", f, m) + 0.3 * osc("square", f, m)
    place(y, lp(v, 2200) * env_perc(m, 0.008, 0.3) * ramp_out(m, 0.05), 0.51 * SR)
    return reverb(y, 0.3, 1.2, seed=26)


@sfx("sfx_step")
def _step(rng):
    n = ns(0.06)
    t = tvec(n)
    grain = np.abs(smooth_rand(n, 900, rng)) ** 2
    crunch = rmsn(bp(white(n, rng), 3000, 0.8)) * (0.25 + 0.75 * grain) * env_perc(n, 0.004, 0.014)
    thud = osc("sine", 90 + 40 * np.exp(-t / 0.01), n) * env_perc(n, 0.002, 0.01)
    return lp1(crunch + 0.15 * thud, 7000)


@sfx("sfx_stun")
def _stun(rng):
    n = ns(0.8)
    t = tvec(n)
    y = np.zeros(n)
    cyc = ["C7", "G6", "E7", "C7", "G6", "D7"]
    for i, t0 in enumerate(np.arange(0.0, 0.72, 0.055)):
        m = ns(0.12)
        tt = tvec(m)
        f = nf(cyc[i % len(cyc)]) * (1 + 0.02 * np.sin(TAU * 6 * (tt + t0)))
        v = (osc("sine", f, m) + 0.3 * osc("tri", f, m)) * env_perc(m, 0.002, 0.045)
        place(y, (0.6 + 0.4 * np.cos(TAU * 1.6 * t0)) * v, t0 * SR)
    wob = osc("sine", 950 * (1 + 0.12 * np.sin(TAU * 7 * t)), n) * env_hump(n, 0.3, 1.2, 1.5)
    return echo(norm(y) + 0.3 * wob, 0.07, 0.3, 3, damp=6000)


# ---------------------------------------------------------------- UI / jingles
@sfx("sfx_ui_click")
def _ui_click(rng):
    n = ns(0.05)
    tk = osc("sine", 2400, n) * env_perc(n, 0.0003, 0.006)
    body = osc("sine", 1200, n) * env_perc(n, 0.0005, 0.01)
    nz = rmsn(hp(white(n, rng), 3000)) * env_perc(n, 0.0001, 0.0015)
    return tk + 0.3 * body + 0.4 * nz


@sfx("sfx_ui_open")
def _ui_open(rng):
    n = ns(0.25)
    wh = whoosh(n, rng, [(0, 600), (1, 3200)], q=1.1, peak=0.6, rise=1.5, fall=2.5, color="pink")
    crin = rmsn(bp(white(n, rng), 4500, 2.0)) * np.abs(smooth_rand(n, 120, rng)) ** 2 * env_hump(n, 0.5)
    m = ns(0.12)
    pip = osc("sine", xsweep(700, 1050, m), m) * env_perc(m, 0.002, 0.035)
    y = wh + 0.25 * crin
    return place(y, 0.3 * pip, 0.12 * SR)


@sfx("sfx_ui_close")
def _ui_close(rng):
    n = ns(0.22)
    wh = whoosh(n, rng, [(0, 3200), (1, 550)], q=1.1, peak=0.3, rise=1.5, fall=2.0, color="pink")
    crin = rmsn(bp(white(n, rng), 4000, 2.0)) * np.abs(smooth_rand(n, 120, rng)) ** 2 * env_hump(n, 0.35)
    m = ns(0.1)
    pip = osc("sine", xsweep(1000, 650, m), m) * env_perc(m, 0.002, 0.035)
    y = wh + 0.2 * crin
    return place(y, 0.3 * pip, 0)


@sfx("sfx_dialogue_blip")
def _dialogue_blip(rng):
    n = ns(0.045)
    v = osc("tri", 620, n) + 0.4 * osc("sine", 1240, n) + 0.15 * lp(osc("square", 620, n), 2000, 0.7)
    return v * env_perc(n, 0.002, 0.012)


@sfx("sfx_denied")
def _denied(rng):
    n = ns(0.28)
    y = np.zeros(n)
    for t0, d in ((0.0, 0.1), (0.13, 0.12)):
        m = ns(d + 0.02)
        v = osc("square", 116.5, m) + osc("square", 123.5, m, duty=0.35) + 0.5 * osc("saw", 58.3, m)
        place(y, lp(v, 1600, 1.0) * env_note(m, d, 0.003, 0.05, 0.008, sus=0.45), t0 * SR)
    return drive(y, 1.5)


@sfx("sfx_quest")
def _quest(rng):
    n = ns(0.85)
    y = np.zeros(n)
    seq = [(0.0, 0.09, "G5"), (0.09, 0.09, "C6"), (0.18, 0.09, "E6"), (0.27, 0.5, "G6")]
    for i, (t0, d, name) in enumerate(seq):
        last = i == len(seq) - 1
        m = ns(d + (0.3 if last else 0.06))
        tt = tvec(m)
        f = nf(name) * (1 + 0.006 * np.sin(TAU * 6 * tt) * np.clip((tt - 0.1) / 0.1, 0, 1) * last)
        v = osc("square", f, m, duty=0.25) + 0.5 * osc("tri", f, m)
        e = env_note(m, d, 0.003, 0.25 if last else None, 0.08 if last else 0.02, sus=0.3)
        place(y, v * e, t0 * SR)
        if last:
            for h in ("C5", "E5", "G5"):
                place(y, 0.3 * osc("tri", nf(h), m) * e, t0 * SR)
    y = echo(lp(y, 7000), 0.1, 0.25, 2, damp=4000)
    return reverb(y, 0.2, 0.8, seed=27)


@sfx("sfx_levelup")
def _levelup(rng):
    n = ns(1.05)
    y = np.zeros(n)
    for i, name in enumerate(["C5", "E5", "G5", "C6", "E6", "G6", "C7"]):
        m = ns(0.3)
        f = nf(name)
        v = 0.5 * osc("square", f, m, duty=0.25) + 0.5 * osc("tri", f, m)
        place(y, v * env_perc(m, 0.002, 0.12), i * 0.055 * SR)
    m = ns(0.66)
    tt = tvec(m)
    ch = np.zeros(m)
    for name in ["C6", "E6", "G6", "C7"]:
        ch += osc("sine", nf(name) * (1 + 0.003 * np.sin(TAU * 5 * tt + rng.uniform(0, TAU))), m)
    ch *= (0.75 + 0.25 * np.sin(TAU * 12 * tt)) * env_perc(m, 0.01, 0.35)
    place(y, 0.35 * ch, 0.39 * SR)
    y += 0.25 * sparkles(n, rng, 20, 0.2, 1.0, 3000, 8000, 0.025)
    y += 0.2 * whoosh(n, rng, [(0, 1000), (0.4, 6000), (1, 6000)], q=1.2, peak=0.35)
    y = echo(y, 0.08, 0.3, 3, damp=6000)
    return reverb(y, 0.3, 1.0, seed=30)


@sfx("sfx_victory")
def _victory(rng):
    n = ns(2.5)
    y = np.zeros(n)
    lead = [(0.00, 0.1, "G4"), (0.10, 0.1, "C5"), (0.20, 0.1, "E5"), (0.30, 0.42, "G5"),
            (0.75, 0.22, "A5"), (1.00, 0.22, "B5"), (1.25, 0.8, "C6")]
    for t0, d, name in lead:
        long = d > 0.5
        m = ns(d + 0.4)
        tt = tvec(m)
        f = nf(name) * (1 + 0.007 * np.sin(TAU * 5.5 * tt) * np.clip((tt - 0.15) / 0.2, 0, 1))
        v = osc("square", f, m, duty=0.25) + 0.4 * osc("saw", f * 1.003, m)
        e = env_note(m, d, 0.004, 0.5 if long else None, 0.12 if long else 0.03, sus=0.4 if long else 1.0)
        place(y, 0.5 * lp(v, 5000) * e, t0 * SR)
    chords = [(0.30, 0.42, ["C4", "E4", "G4"]), (0.75, 0.22, ["F4", "A4", "C5"]),
              (1.00, 0.22, ["G4", "B4", "D5"]), (1.25, 0.85, ["C4", "E4", "G4", "C5"])]
    for t0, d, names in chords:
        m = ns(d + 0.4)
        tt = tvec(m)
        v = np.zeros(m)
        for nm in names:
            for c in (-8, 8):
                v += osc("saw", nf(nm) * 2 ** (c / 1200), m, ph0=rng.random())
        v = lp(v, 900 + 2600 * np.exp(-tt / 0.12), 0.9)
        place(y, 0.14 * v * env_note(m, d, 0.01, 0.6, 0.15, sus=0.5), t0 * SR)
    for t0, d, name in [(0.30, 0.42, "C3"), (0.75, 0.22, "F2"), (1.00, 0.22, "G2"), (1.25, 0.85, "C3")]:
        m = ns(d + 0.35)
        place(y, 0.35 * osc("tri", nf(name), m) * env_note(m, d, 0.005, 0.5, 0.12, sus=0.5), t0 * SR)
    for t0, name in [(0.30, "C2"), (1.00, "G2"), (1.25, "C2")]:
        m = ns(0.6)
        tt = tvec(m)
        tim = osc("sine", nf(name) * (1 + 0.15 * np.exp(-tt / 0.03)), m) * env_perc(m, 0.001, 0.3)
        tim += 0.3 * rmsn(lp(white(m, rng), 900)) * env_perc(m, 0.001, 0.03)
        place(y, 0.45 * tim, t0 * SR)
    m = ns(1.2)
    place(y, 0.12 * rmsn(hp(white(m, rng), 5000)) * env_perc(m, 0.001, 0.45), 1.25 * SR)
    y = reverb(y, 0.3, 1.3, seed=28)
    return y * ramp_out(n, 0.35)


# ============================================================================
# Music - rendered circularly: notes / tails / reverb that run past the end
# wrap to the start, so every loop is sample-exact and seamless.
# ============================================================================
class Loop:
    """Circular mix bus."""

    def __init__(self, n):
        self.n = n
        self.buf = np.zeros(n)

    def add(self, sig, start, gain=1.0):
        s = int(round(start)) % self.n
        i, L = 0, len(sig)
        while i < L:
            k = min(L - i, self.n - s)
            self.buf[s:s + k] += gain * sig[i:i + k]
            i += k
            s = 0


def circ_shape(x, sr, lo=None, hi=None, order=2):
    """Zero-phase circular high/low-pass via FFT magnitude (keeps loops periodic, kills DC)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1.0 / sr)
    g = np.ones(len(f))
    if lo:
        g *= 1.0 / np.sqrt(1.0 + (lo / np.maximum(f, 1e-3)) ** (2 * order))
    if hi:
        g *= 1.0 / np.sqrt(1.0 + (f / hi) ** (2 * order))
    g[0] = 0.0
    return np.fft.irfft(X * g, len(x))


def circ_echo(x, d, fb, taps, sr, damp=4000.0):
    y = x.copy()
    cur = x
    for i in range(1, taps + 1):
        cur = fb * circ_shape(cur, sr, hi=damp, order=1)
        y += np.roll(cur, d * i)
    return y


def humanize(rng, ms, sr):
    return rng.normal(0.0, ms * 1e-3 * sr)


def put_melody(loop, bars, first_bar, s16, inst, rng, gain=1.0, gate=0.92, jitter_ms=0.0, sr=MSR):
    """Render one 16-sixteenth string per bar (see parse_seq) into a Loop."""
    for i, s in enumerate(bars):
        notes, tot = parse_seq(s)
        assert tot == 16, (s, tot)
        for st, ln, m in notes:
            sig = inst(mtof(m), ln * s16 / sr * gate)
            pos = ((first_bar + i) * 16 + st) * s16 + (humanize(rng, jitter_ms, sr) if jitter_ms else 0)
            loop.add(sig, pos, gain * rng.uniform(0.88, 1.0))


# ---------------------------------------------------------------- instruments
def inst_pluck(f, dur, sr, rng, bright=0.55, decay=0.9, tail=0.35):
    """Additive plucked string: harmonics with pick-position comb, highs decay faster."""
    n = int((dur + tail) * sr)
    t = tvec(n, sr)
    y = np.zeros(n)
    for k in range(1, 16):
        fk = k * f * (1 + 0.0003 * k * k)
        if fk > 0.42 * sr:
            break
        a = abs(np.sin(np.pi * k * 0.18)) / k ** 1.3 * bright ** (0.35 * (k - 1))
        y += a * np.sin(TAU * fk * t) * np.exp(-t * (1 + 0.45 * (k - 1) ** 1.2) / decay)
    rel = np.where(t > dur, np.exp(-(t - dur) / 0.09), 1.0)
    return y * ramp_in(n, 0.0015, sr) * rel * ramp_out(n, 0.01, sr)


def inst_flute(f, dur, sr, rng, tail=0.25):
    n = int((dur + tail) * sr)
    t = tvec(n, sr)
    ff = f * (1 + 0.005 * np.sin(TAU * 5.0 * t) * np.clip((t - 0.15) / 0.25, 0, 1))
    y = osc("tri", ff, n, sr) + 0.18 * osc("sine", 2 * ff, n, sr)
    y += rmsn(bp(rng.standard_normal(n), 2 * f, 2.0, sr), 0.04)          # breath
    return lp1(y, 3200, sr) * env_note(n, dur, 0.06, None, 0.12, sr)


def inst_pad(midis, dur, sr, rng, cutoff=900.0, att=0.5, rel=0.9, detune=8.0):
    n = int((dur + rel * 3.0) * sr)
    y = np.zeros(n)
    for m in midis:
        for c in (-detune, 0.0, detune):
            y += osc("saw", mtof(m) * 2 ** (c / 1200), n, sr, ph0=rng.random())
    return lp(y, cutoff, 0.7, sr) * env_note(n, dur, att, None, rel, sr) / (3 * len(midis))


def inst_softbass(f, dur, sr):
    n = int((dur + 0.3) * sr)
    y = osc("sine", f, n, sr) + 0.35 * osc("tri", f, n, sr) + 0.2 * osc("sine", 2 * f, n, sr)
    return lp1(y, 1200, sr) * env_note(n, dur, 0.008, 0.5, 0.08, sr, sus=0.55)


def inst_bass_saw(f, dur, sr, rng, vel=1.0):
    n = int((dur + 0.03) * sr)
    t = tvec(n, sr)
    y = osc("saw", f, n, sr, ph0=rng.random()) + osc("saw", f * 1.004, n, sr, ph0=rng.random()) \
        + 0.6 * osc("square", f * 0.5, n, sr)
    y = lp(y, 280 + 2000 * vel * np.exp(-t / 0.045), 1.3, sr)
    return np.tanh(0.8 * y) * env_note(n, dur, 0.002, None, 0.012, sr) * vel


def inst_lead(f, dur, sr, rng):
    n = int((dur + 0.15) * sr)
    t = tvec(n, sr)
    ff = f * (1 + 0.006 * np.sin(TAU * 5.8 * t) * np.clip((t - 0.12) / 0.15, 0, 1))
    y = osc("square", ff, n, sr, duty=0.3) + 0.5 * osc("saw", ff * 1.005, n, sr, ph0=rng.random())
    return lp(y, 3800, 0.9, sr) * env_note(n, dur, 0.004, 0.4, 0.06, sr, sus=0.75)


def inst_arp(f, dur, sr):
    n = int((dur + 0.05) * sr)
    return lp(osc("square", f, n, sr, duty=0.125), 2600, 0.8, sr) * env_note(n, dur * 0.7, 0.002, 0.06, 0.02, sr, sus=0.3)


def inst_stab(midis, dur, sr, rng):
    n = int((dur + 0.2) * sr)
    t = tvec(n, sr)
    y = np.zeros(n)
    for m in midis:
        for c in (-10, 10):
            y += osc("saw", mtof(m) * 2 ** (c / 1200), n, sr, ph0=rng.random())
    y = lp(y, 500 + 3000 * np.exp(-t / 0.07), 1.1, sr)
    return y * env_note(n, dur, 0.003, 0.15, 0.08, sr, sus=0.35) / (2 * len(midis))


def inst_brass(f, dur, sr, rng):
    n = int((dur + 0.2) * sr)
    t = tvec(n, sr)
    ff = f * (1 + 0.004 * np.sin(TAU * 5 * t) * np.clip((t - 0.2) / 0.2, 0, 1))
    y = np.zeros(n)
    for c in (-8, 0, 8):
        y += osc("saw", ff * 2 ** (c / 1200), n, sr, ph0=rng.random())
    fc = 500 + 1800 * (1 - np.exp(-t / 0.06)) * np.exp(-t / 0.5)
    return lp(y, fc, 0.9, sr) * env_note(n, dur, 0.03, None, 0.1, sr) / 3


def drum_softkick(sr, rng):
    n = int(0.35 * sr)
    t = tvec(n, sr)
    y = osc("sine", 46 + 75 * np.exp(-t / 0.022), n, sr) * env_perc(n, 0.001, 0.13, sr)
    y += 0.08 * rmsn(lp(rng.standard_normal(n), 3000, 0.7, sr)) * env_perc(n, 0.0005, 0.003, sr)
    return y * ramp_out(n, 0.02, sr)


def drum_shaker(sr, rng):
    n = int(0.09 * sr)
    return rmsn(bp(rng.standard_normal(n), 7500, 0.9, sr)) * env_perc(n, 0.006, 0.022, sr) * ramp_out(n, 0.01, sr)


def drum_rim(sr, rng):
    n = int(0.12 * sr)
    y = osc("sine", 1150, n, sr) * env_perc(n, 0.0005, 0.018, sr) \
        + 0.4 * osc("sine", 1800, n, sr) * env_perc(n, 0.0005, 0.01, sr)
    y += 0.3 * rmsn(bp(rng.standard_normal(n), 2200, 1.5, sr)) * env_perc(n, 0.0003, 0.005, sr)
    return y * ramp_out(n, 0.01, sr)


def drum_kick(sr, rng):
    n = int(0.4 * sr)
    t = tvec(n, sr)
    y = osc("sine", 55 + 130 * np.exp(-t / 0.025), n, sr) * env_perc(n, 0.0008, 0.12, sr)
    y += 0.45 * rmsn(lp(rng.standard_normal(n), 5000, 0.7, sr)) * env_perc(n, 0.0003, 0.004, sr)
    return drive(y, 2.0) * ramp_out(n, 0.03, sr)


def drum_snare(sr, rng):
    n = int(0.35 * sr)
    t = tvec(n, sr)
    tone = osc("sine", 185 * (1 + 0.3 * np.exp(-t / 0.01)), n, sr) * env_perc(n, 0.0005, 0.06, sr)
    nz = rng.standard_normal(n)
    noise = (rmsn(bp(nz, 3500, 0.7, sr)) + 0.4 * rmsn(hp(nz, 5000, 0.707, sr))) * env_perc(n, 0.0005, 0.12, sr)
    return drive(0.6 * tone + noise, 1.5) * ramp_out(n, 0.03, sr)


def drum_hat(sr, rng, tau):
    n = int((tau * 6 + 0.02) * sr)
    nz = rng.standard_normal(n)
    y = rmsn(hp(nz, 7000, 0.707, sr)) + 0.5 * rmsn(bp(nz, 10000, 1.0, sr))
    return y * env_perc(n, 0.0003, tau, sr) * ramp_out(n, 0.01, sr)


def drum_crash(sr, rng):
    n = int(2.2 * sr)
    y = rmsn(hp(rng.standard_normal(n), 4500, 0.707, sr))
    y += 0.5 * norm(partials(n, list(rng.uniform(400, 8000, 24)), [1.0] * 24, [0.8] * 24, sr, rng=rng))
    return y * env_perc(n, 0.001, 0.75, sr) * ramp_out(n, 0.2, sr)


def drum_tom(f, sr, rng):
    n = int(0.45 * sr)
    t = tvec(n, sr)
    y = osc("sine", f * (1 + 0.5 * np.exp(-t / 0.02)), n, sr) * env_perc(n, 0.0008, 0.18, sr)
    y += 0.3 * rmsn(bp(rng.standard_normal(n), f * 3, 1.2, sr)) * env_perc(n, 0.0005, 0.03, sr)
    return drive(y, 1.5) * ramp_out(n, 0.03, sr)


# ---------------------------------------------------------------- forest theme
def build_forest():
    """D dorian, 96 BPM, 24 bars (A: pluck theme / B: flute / A': theme + counter-line).
    Bar 24 is an Asus4 -> A turnaround with a run that resolves into bar 1 (Dm)."""
    sr = MSR
    S16 = 5000                       # samples per 16th -> exactly 96 BPM at 32 kHz
    NB = 24
    N = NB * 16 * S16                # 1,920,000 samples = 60.0 s
    rng = np.random.default_rng(seed_of("music_forest"))
    PAD = {"Dm": "D3 F3 A3 C4", "C": "C3 E3 G3 D4", "G": "B2 D3 G3 A3", "Am": "A2 E3 G3 C4",
           "F": "F3 A3 C4 E4", "Em": "E3 G3 B3 D4", "A": "A2 E3 A3 C#4", "Asus": "A2 E3 A3 D4"}
    ROOT = {"Dm": "D2", "C": "C2", "G": "G2", "Am": "A2", "F": "F2", "Em": "E2", "A": "A2"}
    prog = ["Dm", "C", "G", "Dm", "Dm", "F", "C", "Am",
            "F", "C", "G", "Dm", "F", "C", "Em", "A",
            "Dm", "C", "G", "Dm", "F", "C", "G", "A*"]
    MEL_A = ["D5:3 F5:1 A5:4 G5:2 F5:2 E5:2 F5:2", "E5:3 C5:1 G4:4 C5:2 E5:2 G5:4",
             "B4:3 D5:1 G5:4 A5:2 B5:2 A5:2 G5:2", "A5:6 F5:2 D5:8",
             "D5:3 F5:1 A5:4 C6:2 A5:2 G5:2 A5:2", "F5:3 G5:1 A5:4 C6:4 A5:4",
             "G5:3 E5:1 C5:4 E5:2 G5:2 E5:2 D5:2", "E5:6 C5:2 A4:8"]
    MEL_B = ["C6:6 A5:2 F5:4 G5:4", "E5:6 G5:2 C6:8", "B5:6 A5:2 G5:4 D5:4", "F5:6 E5:2 D5:8",
             "A5:4 C6:4 F6:4 E6:2 C6:2", "D6:6 C6:2 G5:8", "B5:4 G5:4 E5:4 G5:4", "A5:4 C#6:4 E6:8"]
    MEL_A2 = MEL_A[:4] + ["F5:3 G5:1 A5:4 C6:2 D6:2 C6:2 A5:2", "G5:3 E5:1 C5:4 E5:2 G5:2 C6:4",
                          "B5:3 A5:1 G5:4 D5:2 G5:2 B5:4", "A5:4 G5:2 F5:2 E5:4 C#5:2 E5:2"]
    COUNTER = ["F4:8 A4:8", "G4:8 E4:8", "D4:8 G4:8", "A4:16",
               "A4:8 C5:8", "G4:8 E4:8", "B4:8 D5:8", "C#5:8 E5:8"]

    # pads: merge repeated chords so they don't re-attack
    events = []
    for b, ch in enumerate(prog):
        events += [(b * 16, 8, "Asus"), (b * 16 + 8, 8, "A")] if ch == "A*" else [(b * 16, 16, ch)]
    merged = []
    for s, l, c in events:
        if merged and merged[-1][2] == c:
            merged[-1] = (merged[-1][0], merged[-1][1] + l, c)
        else:
            merged.append((s, l, c))
    pad = Loop(N)
    for s, l, c in merged:
        sig = inst_pad([midi(x) for x in PAD[c].split()], l * S16 / sr, sr, rng, cutoff=1000, att=0.45, rel=0.9)
        pad.add(sig, s * S16 - int(0.05 * sr))

    bass = Loop(N)
    for b, ch in enumerate(prog):
        r = midi(ROOT["A" if ch == "A*" else ch])
        if b == NB - 1:
            pat = [(0, 6, r), (6, 2, r), (8, 4, r - 5), (12, 4, midi("C#2"))]     # A A E C# -> D
        elif b < 8:
            pat = [(0, 7, r), (8, 6, r + 7)]
        else:
            pat = [(0, 6, r), (6, 2, r), (8, 4, r + 7), (12, 4, r + 12 if b % 2 else r)]
        for s, l, m in pat:
            bass.add(inst_softbass(mtof(m), l * S16 / sr * 0.92, sr), (b * 16 + s) * S16)

    lead, flute, arp = Loop(N), Loop(N), Loop(N)
    pl = lambda f, d: inst_pluck(f, d, sr, rng, bright=0.7)
    fl = lambda f, d: inst_flute(f, d, sr, rng)
    put_melody(lead, MEL_A, 0, S16, pl, rng, jitter_ms=4)
    put_melody(flute, MEL_B, 8, S16, fl, rng, gate=0.97)
    put_melody(lead, MEL_A2, 16, S16, pl, rng, jitter_ms=4)
    put_melody(flute, COUNTER, 16, S16, fl, rng, gain=0.55, gate=0.97)
    for b in range(8, 16):                      # B section: plucked 8th-note arpeggios
        tones = sorted(midi(x) + 12 for x in PAD[prog[b]].split())
        for i, k in enumerate([0, 1, 2, 3, 2, 1, 2, 3]):
            sig = inst_pluck(mtof(tones[k]), 2 * S16 / sr, sr, rng, bright=0.4, decay=0.6, tail=0.3)
            arp.add(sig, (b * 16 + 2 * i) * S16 + humanize(rng, 3, sr), 0.8 if i % 2 else 1.0)

    drums = Loop(N)
    kick = drum_softkick(sr, rng)
    shakers = [drum_shaker(sr, rng) for _ in range(4)]
    rim = drum_rim(sr, rng)
    for b in range(NB):
        base = b * 16
        for i in range(16):
            v = (0.5, 0.22, 0.75, 0.3)[i % 4] * rng.uniform(0.8, 1.1)
            drums.add(shakers[int(rng.integers(4))], (base + i) * S16 + humanize(rng, 3, sr), 0.65 * v)
        kpos = [(0, 0.9), (8, 0.65)] if b < 8 else [(0, 0.95), (8, 0.7), (10, 0.45)]
        if b == NB - 1:
            kpos = [(0, 0.95), (8, 0.7), (12, 0.5), (14, 0.6)]
        for p, v in kpos:
            drums.add(kick, (base + p) * S16, 0.55 * v)
        if b >= 8:
            rpos = [(4, 0.5), (12, 0.5)]
            if b == NB - 1:
                rpos = [(4, 0.5), (12, 0.35), (13, 0.42), (14, 0.5), (15, 0.6)]
            for p, v in rpos:
                drums.add(rim, (base + p) * S16 + humanize(rng, 3, sr), 0.4 * v)

    G = {"lead": 0.75, "flute": 0.5, "pad": 0.7, "bass": 0.3, "arp": 0.45, "drums": 1.0}
    lead_fx = circ_echo(lead.buf, 3 * S16, 0.28, 3, sr, damp=3500)
    buses = {"lead": G["lead"] * lead_fx, "flute": G["flute"] * flute.buf, "pad": G["pad"] * pad.buf,
             "bass": G["bass"] * bass.buf, "arp": G["arp"] * arp.buf, "drums": G["drums"] * drums.buf}
    dry = sum(buses.values())
    send = 0.6 * buses["lead"] + buses["flute"] + 0.8 * buses["pad"] + 0.8 * buses["arp"] + 0.15 * buses["drums"]
    buses["wet"] = 0.5 * cconv(send, make_ir(2.4, MSR, predelay=0.025, damp=3000, seed=41))
    mix = circ_shape(dry + buses["wet"], sr, lo=30.0)
    return mix - mix.mean(), {"bars": NB, "bpm": 60.0 * sr / (4 * S16), "buses": buses}


# ---------------------------------------------------------------- boss theme
def build_boss():
    """E minor (harmonic), ~140 BPM, 24 bars: A riff+stabs / B lead / C climax.
    Bar 24 is a B-major tom/snare fill that crashes back into bar 1 (Em)."""
    sr = MSR
    S16 = 3428                       # samples per 16th -> 140.02 BPM at 32 kHz (integer grid)
    NB = 24
    N = NB * 16 * S16                # 1,316,352 samples = 41.136 s
    d16 = S16 / sr
    rng = np.random.default_rng(seed_of("music_boss"))
    prog = ["Em", "Em", "C", "D", "Em", "Em", "C", "B",
            "Am", "Em", "C", "B", "Am", "Em", "F", "B",
            "Em", "C", "Am", "B", "Em", "C", "F", "B"]
    ROOT = {"Em": 40, "C": 36, "D": 38, "B": 35, "Am": 45, "F": 41}
    TRI = {"Em": [52, 55, 59], "C": [48, 52, 55], "D": [50, 54, 57], "B": [47, 51, 54],
           "Am": [45, 48, 52], "F": [53, 57, 60]}
    LEAD_B = ["A5:4 C6:4 B5:2 A5:2 E5:4", "G5:6 F#5:2 E5:4 B4:4", "C5:2 E5:2 G5:4 C6:4 B5:2 G5:2",
              "D#6:8 B5:4 F#5:4", "A5:4 C6:4 E6:4 D6:2 C6:2", "B5:6 G5:2 E5:8",
              "F5:4 A5:4 C6:4 A5:2 F5:2", "D#5:4 F#5:4 A5:4 B5:4"]
    LEAD_C = ["E6:6 D#6:2 E6:4 B5:4", "C6:6 B5:2 G5:4 E5:4", "A5:4 B5:4 C6:4 E6:4", "D#6:6 C6:2 B5:8",
              "G6:4 F#6:2 E6:2 B5:4 G5:4", "E6:4 D6:2 C6:2 G5:4 E5:4", "F6:4 E6:2 C6:2 A5:4 F5:4",
              "F#5:4 A5:4 B5:4 D#6:4"]
    BRASS = ["E4:6 F#4:2 G4:8", "F#4:6 E4:2 D#4:8", "C4:6 D4:2 E4:8", "D#4:8 F#4:4 B4:4"]
    CHUG = "RRRORRRORRROFFOR"
    ACC = [1, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0]

    kick, snare = drum_kick(sr, rng), drum_snare(sr, rng)
    hatc = [drum_hat(sr, rng, 0.022) for _ in range(3)]
    hato = drum_hat(sr, rng, 0.16)
    crash = drum_crash(sr, rng)
    toms = {k: drum_tom(f, sr, rng) for k, f in (("h", 210.0), ("m", 155.0), ("l", 110.0))}
    drums, bass, arp, stab, pad, lead, brass, rv = (Loop(N) for _ in range(8))
    kicks = []
    for b, ch in enumerate(prog):
        sec = b // 8
        last = b == NB - 1
        at = lambda p, b=b: (b * 16 + p) * S16
        # ---- drums
        kpos = [0, 8, 10] + ([3, 11] if b % 2 else []) if sec == 0 else ([0, 3, 8, 10] if sec == 1 else list(range(0, 16, 2)))
        spos = [4, 12, 13, 14, 15] if b in (7, 15) else [4, 12]
        hpos = range(0, 16, 2) if sec == 0 else range(16)
        if last:
            kpos, spos, hpos = [0, 4], [4], range(0, 8)
        for p in kpos:
            drums.add(kick, at(p), 0.6)
            kicks.append(at(p))
        for p in spos:
            v = 0.75 if p in (4, 12) else 0.45 + 0.07 * (p - 12)
            drums.add(snare, at(p) + humanize(rng, 2, sr), v)
            rv.add(snare, at(p), 0.3 * v)
        for p in hpos:
            if b % 4 == 3 and p == 14 and not last:
                drums.add(hato, at(p), 0.22)
            else:
                v = (0.28 if p % 4 == 2 else 0.2) if sec == 0 else (0.26 if p % 2 == 0 else 0.14)
                drums.add(hatc[int(rng.integers(3))], at(p) + humanize(rng, 2, sr), v)
        if b in (0, 8, 16, 20):
            drums.add(crash, at(0), 0.35)
        if last:
            for p, k, v in [(8, "h", 0.6), (9, "h", 0.55), (10, "m", 0.65), (11, "m", 0.6),
                            (12, "l", 0.7), (13, "l", 0.7)]:
                drums.add(toms[k], at(p), v)
                rv.add(toms[k], at(p), 0.2 * v)
            for p, v in [(14, 0.75), (15, 0.85)]:
                drums.add(snare, at(p), v)
                rv.add(snare, at(p), 0.3 * v)
        # ---- bass
        r = ROOT[ch]
        if sec == 1:
            for i, m in enumerate([r, r, r + 12, r, r, r, r + 12, r + 7]):
                bass.add(inst_bass_saw(mtof(m), 2 * d16 * 0.85, sr, rng, 0.9), at(2 * i))
        else:
            for i in range(16):
                m = {"R": r, "O": r + 12, "F": r + 7}[CHUG[i]]
                bass.add(inst_bass_saw(mtof(m), d16 * 0.8, sr, rng, 1.0 if ACC[i] else 0.72), at(i))
        # ---- harmony
        tri = TRI[ch]
        up = [m + 12 for m in tri]
        if sec != 1:
            tones = [up[0], up[1], up[2], tri[0] + 24]
            for i, k in enumerate([0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0, 1, 2, 1]):
                arp.add(inst_arp(mtof(tones[k]), d16, sr), at(i), 1.0 if i % 4 == 0 else 0.75)
        if sec == 0 and b < 4:
            hits = ((0, 5), (6, 5), (12, 3))
        elif sec == 1:
            hits = ()
        else:
            hits = ((0, 6),)
        for p, l in hits:
            stab.add(inst_stab(up, l * d16 * 0.6, sr, rng), at(p))
        if sec >= 1:
            pad.add(inst_pad(tri + [tri[0] + 12], 16 * d16, sr, rng, cutoff=700, att=0.25, rel=0.4), at(0))
    put_melody(brass, BRASS, 4, S16, lambda f, d: inst_brass(f, d, sr, rng), rng)
    put_melody(lead, LEAD_B, 8, S16, lambda f, d: inst_lead(f, d, sr, rng), rng)
    put_melody(lead, LEAD_C, 16, S16, lambda f, d: inst_lead(f, d, sr, rng), rng)

    # sidechain-style ducking of the tonal beds on every kick
    dl = Loop(N)
    m = int(0.25 * sr)
    bump = 0.4 * np.exp(-tvec(m, sr) / 0.05) * ramp_in(m, 0.003, sr)
    for k in kicks:
        dl.add(bump, k)
    duck = 1.0 - np.minimum(dl.buf, 0.55)

    G = {"bass": 0.55, "arp": 0.35, "stab": 1.2, "pad": 0.6, "brass": 0.9, "lead": 0.4}
    lead_fx = circ_echo(lead.buf, 3 * S16, 0.3, 3, sr, damp=3000)
    buses = {"drums": drums.buf, "lead": G["lead"] * lead_fx}
    for k, bus in (("bass", bass), ("arp", arp), ("stab", stab), ("pad", pad), ("brass", brass)):
        buses[k] = G[k] * bus.buf * duck
    send = rv.buf + 0.9 * buses["lead"] + 0.3 * buses["stab"] + 0.6 * buses["pad"] + 0.4 * buses["brass"]
    buses["wet"] = 0.4 * cconv(send, make_ir(1.4, MSR, predelay=0.015, damp=3500, seed=42))
    mix = norm(sum(buses.values()))
    mix = np.tanh(1.3 * mix) / np.tanh(1.3)                  # gentle bus saturation
    mix = circ_shape(mix, sr, lo=30.0)
    return mix - mix.mean(), {"bars": NB, "bpm": 60.0 * sr / (4 * S16), "buses": buses}


# ---------------------------------------------------------------- forest ambience
# ---------------------------------------------------------------- swamp theme
def build_swamp():
    """A aeolian with a flat second, 72 BPM, 16 bars: a slow pad and a plodding bass under a sparse,
    wandering flute; water-drop plucks and a soft heartbeat kick. Misty rather than scary."""
    sr = MSR
    S16 = 6667                       # samples per 16th -> 72.0 BPM at 32 kHz
    NB = 16
    N = NB * 16 * S16                # 1,707,392 samples = 53.4 s
    rng = np.random.default_rng(seed_of("music_swamp"))
    PAD = {"Am": "A2 E3 A3 C4", "Bb": "Bb2 F3 Bb3 D4", "F": "F2 C3 A3 C4", "Dm": "D3 F3 A3 E4",
           "Em": "E2 B2 G3 D4", "E": "E2 B2 G#3 D4"}
    ROOT = {"Am": "A1", "Bb": "Bb1", "F": "F1", "Dm": "D2", "Em": "E2", "E": "E2"}
    prog = ["Am", "Am", "Bb", "Am", "F", "Dm", "Em", "Am",
            "Am", "Bb", "F", "Am", "Dm", "Bb", "Em", "E"]
    MEL = ["-:4 E5:4 C5:2 B4:2 A4:4", "-:8 A4:2 C5:2 E5:4", "F5:6 E5:2 D5:4 -:4", "E5:8 -:8",
           "-:4 C5:4 A4:4 C5:4", "D5:6 F5:2 E5:4 D5:4", "B4:6 G4:2 E4:8", "-:16",
           "-:4 A5:4 G5:2 E5:2 C5:4", "D5:6 Bb4:2 A4:8", "-:4 C5:2 D5:2 F5:4 E5:4", "E5:12 -:4",
           "F5:4 E5:2 D5:2 A4:8", "Bb4:6 D5:2 F5:4 E5:4", "G5:6 E5:2 B4:8", "G#4:4 B4:4 E5:8"]

    pad = Loop(N)
    for b, ch in enumerate(prog):
        sig = inst_pad([midi(x) for x in PAD[ch].split()], 16 * S16 / sr, sr, rng, cutoff=620, att=0.9, rel=1.2, detune=10)
        pad.add(sig, b * 16 * S16 - int(0.2 * sr))

    bass = Loop(N)
    for b, ch in enumerate(prog):
        r = midi(ROOT[ch])
        for s, l in ((0, 6), (8, 6)):
            bass.add(inst_softbass(mtof(r if s == 0 else r + 7), l * S16 / sr * 0.9, sr), (b * 16 + s) * S16)

    flute = Loop(N)
    put_melody(flute, MEL, 0, S16, lambda f, d: inst_flute(f, d, sr, rng, tail=0.4), rng, gain=0.9, gate=0.96, jitter_ms=6)

    drops = Loop(N)
    for b in range(NB):
        tones = sorted(midi(x) + 24 for x in PAD[prog[b]].split())
        for i in range(3):
            st = int(rng.integers(0, 16))
            sig = inst_pluck(mtof(tones[int(rng.integers(len(tones)))]), 1.5 * S16 / sr, sr, rng, bright=0.3, decay=0.45, tail=0.5)
            drops.add(sig, (b * 16 + st) * S16 + humanize(rng, 8, sr), rng.uniform(0.35, 0.7))

    drums = Loop(N)
    kick = drum_softkick(sr, rng)
    shakers = [drum_shaker(sr, rng) for _ in range(3)]
    for b in range(NB):
        base = b * 16
        for p, v in ((0, 0.8), (3, 0.45)):                  # a slow heartbeat
            drums.add(kick, (base + p) * S16, 0.5 * v)
        for i in range(0, 16, 2):
            drums.add(shakers[int(rng.integers(3))], (base + i) * S16 + humanize(rng, 4, sr), 0.25 * (0.9 if i % 4 == 2 else 0.5))

    G = {"flute": 0.55, "pad": 0.75, "bass": 0.32, "drops": 0.4, "drums": 0.9}
    fl = circ_echo(flute.buf, 6 * S16, 0.32, 3, sr, damp=2800)
    buses = {"flute": G["flute"] * fl, "pad": G["pad"] * pad.buf, "bass": G["bass"] * bass.buf,
             "drops": G["drops"] * circ_echo(drops.buf, 3 * S16, 0.4, 4, sr, damp=3000), "drums": G["drums"] * drums.buf}
    dry = sum(buses.values())
    send = buses["flute"] + 0.9 * buses["pad"] + buses["drops"] + 0.1 * buses["drums"]
    buses["wet"] = 0.65 * cconv(send, make_ir(3.2, MSR, predelay=0.035, damp=2400, seed=71))
    mix = circ_shape(dry + buses["wet"], sr, lo=32.0, hi=9000.0)
    return mix - mix.mean(), {"bars": NB, "bpm": 60.0 * sr / (4 * S16), "buses": buses}


def bird_phrase(kind, rng, sr):
    def chirp(f0, f1, dur, harm=0.12, shape=1.0):
        n = int(dur * sr)
        u = np.linspace(0.0, 1.0, n)
        f = f0 * (f1 / f0) ** (u ** shape)
        return (osc("sine", f, n, sr) + harm * osc("sine", 2 * f, n, sr)) * np.sin(np.pi * u) ** 2

    parts = []
    if kind == "whistle":                       # "tee-oo"
        b = rng.uniform(2600, 3400)
        parts = [(0.0, chirp(b, b * 1.15, 0.12)), (0.17, chirp(b * 1.1, b * 0.75, 0.22))]
    elif kind == "whistle3":                    # three falling notes
        b = rng.uniform(3000, 3800)
        parts = [(i * 0.16, chirp(b * r, b * r * 0.93, 0.11)) for i, r in enumerate((1.0, 0.89, 0.8))]
    elif kind == "chirps":                      # quick upward chirps
        off = 0.0
        for _ in range(int(rng.integers(3, 6))):
            parts.append((off, chirp(rng.uniform(1900, 2400), rng.uniform(4200, 5200), 0.045, shape=0.7)))
            off += rng.uniform(0.085, 0.11)
    else:                                       # trill
        dur = rng.uniform(0.5, 0.8)
        n = int(dur * sr)
        t = tvec(n, sr)
        rate = rng.uniform(22, 30)
        f = rng.uniform(3800, 4600) * (1 + 0.06 * np.sin(TAU * rate * t)) * (1 - 0.08 * t / dur)
        am = (0.5 + 0.5 * np.sin(TAU * rate * t - 1.0)) ** 2
        parts = [(0.0, osc("sine", f, n, sr) * am * env_hump(n, 0.3, 1.5, 1.2))]
    total = max(o + len(s) / sr for o, s in parts)
    y = np.zeros(int(total * sr) + 2)
    for o, s in parts:
        place(y, s, o * sr)
    return y


def build_ambience():
    """20 s wind bed (slowly moving filters, gusts, leaf rustle) + sparse synthesized birds.
    Rendered 2 s long and the overhang is equal-power cross-faded into the start."""
    sr = MSR
    N = 20 * sr
    X = 2 * sr
    M = N + X
    rng = np.random.default_rng(seed_of("amb_forest"))
    t = tvec(M, sr)
    fc = 520 + 260 * np.sin(TAU * t / 9.3) + 160 * smooth_rand(M, 0.3, rng, sr)
    wind = rmsn(lp(pink(M, rng, sr), fc, 0.8, sr))
    howl = rmsn(bp(pink(M, rng, sr), 850 + 350 * np.sin(TAU * t / 6.1 + 1.3), 2.5, sr))
    gust = 0.55 + 0.45 * (0.5 + 0.5 * smooth_rand(M, 0.2, rng, sr))
    wind = (wind + 0.35 * howl) * gust
    rustle = rmsn(hp(white(M, rng), 2500, 0.707, sr)) * np.maximum(0.0, smooth_rand(M, 0.8, rng, sr)) ** 2
    birds = np.zeros(M)
    kinds = ["whistle", "chirps", "trill", "whistle3", "chirps", "whistle", "trill", "chirps"]
    for t0, kd in zip([0.9, 3.6, 5.9, 8.4, 10.8, 12.6, 15.1, 17.4], kinds):
        place(birds, bird_phrase(kd, rng, sr) * rng.uniform(0.5, 1.0), (t0 + rng.uniform(-0.3, 0.3)) * sr)
    birds = lp1(birds, 7000, sr)
    birds = birds + 0.35 * fftconv(birds, make_ir(1.6, sr, 0.03, 4000, seed=51), M)
    y = 0.8 * norm(wind) + 0.12 * norm(rustle) + 0.45 * norm(birds)
    w = np.linspace(0.0, 1.0, X, endpoint=False)
    out = y[:N].copy()
    out[:X] = y[:X] * np.sin(0.5 * np.pi * w) + y[N:N + X] * np.cos(0.5 * np.pi * w)
    out = circ_shape(out, sr, lo=30.0)
    return out - out.mean(), {"crossfade_s": X / sr}


def build_swamp_ambience():
    """20 s swamp bed: still air, a far frog chorus, near croaks, insects and water drips.
    Rendered 2 s long and the overhang is equal-power cross-faded into the start."""
    sr = MSR
    N = 20 * sr
    X = 2 * sr
    M = N + X
    rng = np.random.default_rng(seed_of("amb_swamp"))
    t = tvec(M, sr)
    air = rmsn(lp(brown(M, rng, sr), 260 + 80 * np.sin(TAU * t / 11.0), 0.7, sr))
    # the chorus: many small frogs far away, a pulsing band of buzz
    chorus = np.zeros(M)
    for k in range(6):
        f = rng.uniform(900, 1700)
        rate = rng.uniform(5.5, 9.0)
        gate = (0.5 + 0.5 * np.sin(TAU * rate * t + rng.random() * TAU)) ** 6
        swell = 0.4 + 0.6 * (0.5 + 0.5 * smooth_rand(M, 0.15, rng, sr))
        chorus += rmsn(bp(white(M, rng), f, 8.0, sr)) * gate * swell
    chorus = lp1(chorus, 3000, sr)
    # a few near toads
    near = np.zeros(M)
    for t0 in (1.3, 4.8, 7.1, 10.9, 13.6, 17.2):
        f0 = rng.uniform(110, 160)
        for j in range(int(rng.integers(2, 4))):
            m = int(0.18 * sr)
            tt = tvec(m, sr)
            buzz = osc("saw", f0 * (1 + 0.1 * np.sin(np.pi * tt / 0.18)), m, sr)
            ratchet = 0.5 + 0.5 * (np.sin(TAU * 36 * tt) > -0.2)
            v = formant(buzz * ratchet, [(420, 4.0, 1.0), (1100, 5.0, 0.5)], sr)
            place(near, rmsn(v) * env_hump(m, 0.3, 1.2, 1.6), (t0 + rng.uniform(-0.3, 0.3) + j * 0.26) * sr)
    # insects: thin high trills
    insects = np.zeros(M)
    for t0 in rng.uniform(0, 19, 9):
        m = int(rng.uniform(0.8, 1.6) * sr)
        tt = tvec(m, sr)
        am = (0.5 + 0.5 * np.sin(TAU * rng.uniform(40, 60) * tt)) ** 2
        insects_part = osc("sine", rng.uniform(5200, 6800) * (1 + 0.01 * np.sin(TAU * 3 * tt)), m, sr) * am * env_hump(m, 0.5, 2.0, 2.0)
        place(insects, insects_part, t0 * sr)
    # water drips
    drips = np.zeros(M)
    for t0 in rng.uniform(0, 19.5, 16):
        m = int(0.06 * sr)
        tt = tvec(m, sr)
        d = osc("sine", rng.uniform(800, 1500) * np.exp(np.minimum(tt, 0.03) / 0.025), m, sr) * env_perc(m, 0.001, 0.02, sr)
        place(drips, d * rng.uniform(0.4, 1.0), t0 * sr)
    drips = drips + 0.5 * fftconv(drips, make_ir(1.2, sr, 0.02, 3500, seed=73), M)
    y = 0.55 * norm(air) + 0.28 * norm(chorus) + 0.3 * norm(near) + 0.08 * norm(insects) + 0.3 * norm(drips)
    w = np.linspace(0.0, 1.0, X, endpoint=False)
    out = y[:N].copy()
    out[:X] = y[:X] * np.sin(0.5 * np.pi * w) + y[N:N + X] * np.cos(0.5 * np.pi * w)
    out = circ_shape(out, sr, lo=30.0)
    return out - out.mean(), {"crossfade_s": X / sr}


# ============================================================================
# output / verification
# ============================================================================
SFX_NAMES = [
    "sfx_swing", "sfx_hit", "sfx_hit_heavy", "sfx_crit", "sfx_fireball_cast", "sfx_fireball_explode",
    "sfx_ice_cast", "sfx_ice_shatter", "sfx_thunder", "sfx_heal", "sfx_shield", "sfx_bladestorm",
    "sfx_dash", "sfx_potion", "sfx_pickup", "sfx_pickup_item", "sfx_slime_hop", "sfx_slime_die",
    "sfx_shroom_puff", "sfx_spore_shot", "sfx_boss_roar", "sfx_boss_stomp", "sfx_rock_throw",
    "sfx_rock_impact", "sfx_boss_leap", "sfx_boss_swipe", "sfx_player_hurt", "sfx_player_die",
    "sfx_ui_click", "sfx_ui_open", "sfx_ui_close", "sfx_quest", "sfx_victory", "sfx_dialogue_blip",
    "sfx_telegraph", "sfx_stun", "sfx_step", "sfx_denied", "sfx_levelup", "sfx_enrage",
    "sfx_boulder_break", "sfx_lightning_charge",
    "sfx_croak", "sfx_splash", "sfx_wade", "sfx_hiss", "sfx_spit", "sfx_splat", "sfx_mud_slam", "sfx_tongue", "sfx_waystone",
]
# name, builder, allowed duration range (s), target peak dBFS
MUSIC = [
    ("music_forest", build_forest, (48.0, 64.0), -3.0),
    ("music_boss", build_boss, (32.0, 48.0), -2.0),
    ("amb_forest", build_ambience, (19.5, 20.5), -12.0),
    ("music_swamp", build_swamp, (40.0, 64.0), -3.0),
    ("amb_swamp", build_swamp_ambience, (19.5, 20.5), -12.0),
]


def finalize_sfx(x, sr=SR, peak_db=-1.0, fin=0.001, fout=0.012):
    """DC/subsonic removal, click-free raised-cosine fades, peak normalisation."""
    x = np.nan_to_num(np.asarray(x, dtype=float))
    x = hp1(x, 20.0, sr)
    x = x - np.mean(x)
    n = len(x)
    a = min(n // 4, max(1, int(fin * sr)))
    b = min(n // 4, max(1, int(fout * sr)))
    x[:a] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(a) / a)
    x[n - b:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(b) / b)
    return norm(x, 10.0 ** (peak_db / 20.0))


def write_wav(path, x, sr):
    pcm = np.round(np.clip(x, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())


def read_wav(path):
    with wave.open(path, "rb") as w:
        sr, ch, sw, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        x = np.frombuffer(w.readframes(n), dtype="<i2").astype(float) / 32768.0
    return sr, ch, sw, x


def verify(meta):
    specs = [(nm, SFX_DIR, SR, (0.03, 2.6 if nm == "sfx_victory" else 1.7)) for nm in SFX_NAMES]
    specs += [(nm, MUSIC_DIR, MSR, lim) for nm, _, lim, _ in MUSIC]
    problems, total = [], 0
    print("\n%-24s %6s %8s %8s %8s  %s" % ("file", "rate", "dur(s)", "peak dB", "dc", "notes"))
    print("-" * 96)
    for name, folder, sr_exp, (lo, hi) in specs:
        path = os.path.join(folder, name + ".wav")
        if not os.path.isfile(path):
            problems.append(name + ": missing")
            print("%-24s MISSING" % name)
            continue
        size = os.path.getsize(path)
        total += size
        sr, ch, sw, x = read_wav(path)
        dur = len(x) / sr
        pk = np.max(np.abs(x)) if len(x) else 0.0
        pk_db = 20 * np.log10(max(pk, 1e-9))
        dc = float(np.mean(x)) if len(x) else 0.0
        if name.startswith(("music", "amb")):
            seam = abs(x[0] - x[-1])
            p999 = np.percentile(np.abs(np.diff(x)), 99.9)
            note = "loop seam step %.4f (p99.9 step %.4f)" % (seam, p999)
            if seam > p999:
                problems.append(name + ": loop seam discontinuity")
            m = meta.get(name, {})
            if "bars" in m:
                note += "  %d bars @ %.2f BPM" % (m["bars"], m["bpm"])
        else:
            note = "ends %.4f / %.4f" % (abs(x[0]), abs(x[-1]))
            if max(abs(x[0]), abs(x[-1])) > 0.01:
                problems.append(name + ": non-zero end sample")
        if sr != sr_exp or ch != 1 or sw != 2:
            problems.append("%s: format %d Hz / %d ch / %d bytes" % (name, sr, ch, sw))
        if size <= 44 or not (lo <= dur <= hi):
            problems.append("%s: duration %.3f s outside %.2f-%.2f" % (name, dur, lo, hi))
        if abs(dc) > 0.005 or not np.isfinite(x).all():
            problems.append("%s: DC offset %.4f" % (name, dc))
        print("%-24s %6d %8.3f %8.2f %8.5f  %s" % (name, sr, dur, pk_db, dc, note))
    print("-" * 96)
    print("%d files, total %.2f MB" % (len(specs) - sum(p.endswith("missing") for p in problems), total / 1e6))
    if problems:
        print("PROBLEMS:\n  " + "\n  ".join(problems))
    else:
        print("all checks passed")
    return not problems


def main(argv):
    t_all = time.time()
    os.makedirs(SFX_DIR, exist_ok=True)
    os.makedirs(MUSIC_DIR, exist_ok=True)
    words = [a.lower() for a in argv[1:]]
    want = lambda name: not words or any(w in name for w in words)
    missing = [nm for nm in SFX_NAMES if nm not in SFX]
    if missing:
        print("WARNING: no generator for", missing)
    for name in SFX_NAMES:
        if name in SFX and want(name):
            t0 = time.time()
            x = finalize_sfx(SFX[name](np.random.default_rng(seed_of(name))))
            write_wav(os.path.join(SFX_DIR, name + ".wav"), x, SR)
            print("  %-24s %5.2f s audio   %5.2f s cpu" % (name, len(x) / SR, time.time() - t0))
    meta = {}
    for name, fn, _, peak_db in MUSIC:
        if want(name):
            t0 = time.time()
            x, info = fn()
            x = norm(x, 10.0 ** (peak_db / 20.0))
            write_wav(os.path.join(MUSIC_DIR, name + ".wav"), x, MSR)
            info.pop("buses", None)
            meta[name] = info
            print("  %-24s %5.2f s audio   %5.2f s cpu" % (name, len(x) / MSR, time.time() - t0))
    ok = verify(meta)
    print("done in %.1f s" % (time.time() - t_all))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
