"""
gen_godot_sfx.py - hit sounds for the Godot 3D version, made with the same synth as gen_audio.py.

Writes Godot/assets/sfx/*.wav (16-bit mono, 44.1 kHz, peak -1 dBFS):
    whack_1..4      a sharp whack on the impact frame: a click, a snapped noise crack with a
                    falling bright edge, a short body thump (four takes so repeats differ)
    whack_heavy     the same with more weight and a little room (Trảm, crits)
    whack_hero      a dull thud for blows the hero takes
    crunch          bark and bone breaking (a colossus part breaks)
    interrupt       a big impact with a ringing clang (a charge interrupted)

Run:  python Tools/AudioGen/gen_godot_sfx.py
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_audio as g  # noqa: E402

OUT = os.path.join(g.ROOT, "Godot", "assets", "sfx")
SR = g.SR


def whack(rng, weight=0.0):
    """weight 0 = a quick blade whack, 1 = a heavy one."""
    n = g.ns(0.16 + 0.22 * weight)
    t = g.tvec(n)
    click = g.rmsn(g.hp(g.white(n, rng), 2500)) * g.env_perc(n, 0.0001, 0.0025)
    crack_fc = 2600 + 7000 * np.exp(-t / (0.006 + 0.004 * weight))
    crack = g.rmsn(g.lp(g.white(n, rng), crack_fc, 0.9)) * g.env_perc(n, 0.0002, 0.018 + 0.03 * weight)
    snap = g.rmsn(g.bp(g.white(n, rng), 1500 - 400 * weight, 1.6)) * g.env_perc(n, 0.0005, 0.022 + 0.03 * weight)
    body = g.thump(n, 240 - 50 * weight, 80 - 25 * weight, 0.035 + 0.07 * weight, sweep=0.014 + 0.012 * weight)
    meat = g.rmsn(g.bp(g.white(n, rng), 420, 1.4)) * g.env_perc(n, 0.001, 0.03 + 0.04 * weight)
    y = 0.55 * click + 1.0 * crack + 0.6 * snap + (0.7 + 0.4 * weight) * body + 0.4 * meat
    y = g.drive(y, 2.4 + 0.6 * weight)
    if weight > 0.5:
        y = g.reverb(y, 0.12, 0.35, seed=7, damp=3000)
    return y


def whack_hero(rng):
    n = g.ns(0.22)
    t = g.tvec(n)
    body = g.thump(n, 160, 60, 0.07, sweep=0.02)
    muff = g.rmsn(g.lp(g.white(n, rng), 900 + 2500 * np.exp(-t / 0.01), 0.8)) * g.env_perc(n, 0.0004, 0.04)
    return g.drive(1.0 * body + 0.8 * muff, 2.0)


def crunch(rng):
    n = g.ns(0.6)
    base = whack(rng, 0.8)
    y = np.zeros(n)
    g.place(y, base, 0)
    times = np.sort(rng.uniform(0.0, 0.32, 26))
    grains = g.grains(n, rng, times, rng.uniform(700, 3200, 26), rng.uniform(0.006, 0.02, 26), rng.uniform(0.3, 1.0, 26),
                      q=3.0)
    crack = g.crackle(n, rng, 420 * np.exp(-g.tvec(n) / 0.12), hpf=1500)
    return y + 0.9 * g.rmsn(grains) * g.env_perc(n, 0.001, 0.25) + 0.5 * g.rmsn(crack)


def interrupt(rng):
    n = g.ns(1.1)
    y = np.zeros(n)
    g.place(y, whack(rng, 1.0), 0)
    ring = g.fm_bell(620, n, ratio=2.76, index=1.2, tau=0.45, itau=0.08) * 0.35
    sub = g.osc("sine", 52, n) * g.env_perc(n, 0.003, 0.25) * 0.5
    return g.reverb(y + ring + sub, 0.2, 0.8, seed=5, damp=3500)


SOUNDS = {
    "whack_1": lambda r: whack(r, 0.0),
    "whack_2": lambda r: whack(r, 0.1),
    "whack_3": lambda r: whack(r, 0.05),
    "whack_4": lambda r: whack(r, 0.15),
    "whack_heavy": lambda r: whack(r, 1.0),
    "whack_hero": whack_hero,
    "crunch": crunch,
    "interrupt": interrupt,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in SOUNDS.items():
        rng = np.random.default_rng(g.seed_of("godot_" + name))
        x = g.finalize_sfx(fn(rng))
        g.write_wav(os.path.join(OUT, name + ".wav"), x, SR)
        print("  %-12s %.3f s" % (name, len(x) / SR))


if __name__ == "__main__":
    main()
