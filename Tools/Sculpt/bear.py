"""Gấu Ma Rừng Già, sculpted part by part in bear units (gau_ma.gd multiplies by GauMa.S).

Each part is built in the frame of its AnimatableBody3D and hugs its collider: the fur surface
starts a little inside the capsule and the locks rise a little past it, so heroes hold on to the
fur they see. Curved bark plates cover the boxes that cannot be held. The old forest grows on it:
moss on its back and plates, glowing mushrooms, branch antlers with leaves, a bone mask with teal
marks over its face.

`python creatures.py gau_ma --only Head,Torso` rebuilds some parts and takes the rest from the
cache (Tools/Sculpt/.cache).
"""
import math
import os

import numpy as np

from sdf import (Box, Displace, Ellipsoid, Field, Intersect, MirrorX, Offset, Paint, Plane, RoundCone, Sphere, Sub, Tube,
                 Union, Xf, axis_angle, basis_y, bezier, build, clumps, euler, fbm, grad, hit, mix, normalize, shell,
                 smoothstep, taper, vec)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


FUR_ROOT = rgb("23150d")
FUR_MID = rgb("4f321f")
FUR_TIP = rgb("8e623c")
FUR_SILVER = rgb("b39a78")
MOSS_DARK = rgb("2f4d1a")
MOSS = rgb("5b8a28")
MOSS_LIGHT = rgb("a2c94c")
BARK_DARK = rgb("21150d")
BARK = rgb("4a3222")
BARK_LIGHT = rgb("7d5c3c")
LICHEN = rgb("98a678")
BONE = rgb("ece2c8")
BONE_DARK = rgb("a59479")
TEAL = rgb("27b7a2")
SOCKET = rgb("120d0a")


def ramp(stops, t):
    ts = np.array([s[0] for s in stops])
    cs = np.stack([np.asarray(s[1], dtype=float) for s in stops])
    t = np.clip(np.asarray(t, dtype=float), ts[0], ts[-1])
    return np.stack([np.interp(t, ts, cs[:, i]) for i in range(3)], axis=1)


def _normal(node, p, eps=0.05):
    g = grad(node, p, eps)
    return g / np.maximum(np.linalg.norm(g, axis=1), 1e-9)[:, None]


# ------------------------------------------------------------------ fur and moss
def furry(base, flow, size, amp, seed, flow2=None, inset=0.12, moss=0.0, moss_up=0.45, belly=None, silver=0.0, socks=None,
          stretch=3.0):
    """`base` covered in layered fur locks along `flow`; where the surface faces along `flow`
    (an end cap) the locks follow `flow2` instead, so they always lie along the skin. Where the
    base faces up (more than `moss_up`) and `moss` > 0, soft moss pads replace the fur.
    `belly(p)` 0..1 darkens, `socks(p)` 0..1 darkens toward the paws, `silver` lightens tips on top."""
    f1 = normalize(flow)

    def locks(p, n):
        h = clumps(p, size, f1, stretch, seed)
        if flow2 is None:
            return h
        w = smoothstep(0.55, 0.85, np.abs(n @ f1))
        m = w > 0
        if m.any():
            h = h.copy()
            h[m] = mix(h[m], clumps(p[m], size, flow2, stretch, seed + 50), w[m])
        return h

    def moss_mask(p, n):
        if moss <= 0:
            return np.zeros(len(p))
        g = fbm(p * 0.45, 3, seed + 7)
        return smoothstep(moss_up, moss_up + 0.25, n[:, 1] + (g - 0.5) * 0.7) * moss

    def push(p):
        n = _normal(base, p)
        h = locks(p, n)
        m = moss_mask(p, n)
        pads = fbm(p * 1.6, 3, seed + 11)
        return inset - mix(amp * h, amp * 0.5 * (0.4 + pads), m)

    def paint(p, c):
        n = _normal(base, p)
        h = locks(p, n)
        g = fbm(p * 0.35, 3, seed + 3)
        fur = ramp([(0.0, FUR_ROOT), (0.5, FUR_MID), (1.0, FUR_TIP)], 0.3 + 0.7 * h * (0.85 + 0.3 * g))
        if silver > 0:
            fur = mix(fur, FUR_SILVER * (0.75 + 0.35 * h)[:, None], (smoothstep(0.2, 0.9, n[:, 1]) * silver * h)[:, None])
        if belly is not None:
            fur = fur * (1.0 - 0.4 * belly(p))[:, None]
        if socks is not None:
            fur = mix(fur, FUR_ROOT * (0.9 + 0.5 * h)[:, None], (socks(p) * 0.7)[:, None])
        m = moss_mask(p, n)
        pads = fbm(p * 1.6, 3, seed + 11)
        mc = ramp([(0.0, MOSS_DARK), (0.5, MOSS), (1.0, MOSS_LIGHT)], pads * 0.8 + smoothstep(0.5, 1.0, n[:, 1]) * 0.35)
        return mix(fur, mc, smoothstep(0.15, 0.6, m)[:, None])

    return Displace(base, push, inset + amp + 0.1, paint=paint)


def bark(plate, axis, seed, moss=0.6, lichen=0.25, depth=0.09):
    """Bark armour: a network of long cracks running along `axis`, moss on top, lichen spots."""
    b = basis_y(normalize(axis))

    def ridges(p):
        q = p @ b
        q[:, 1] *= 0.3
        n = fbm(q * 1.5 + fbm(p * 0.5, 2, seed + 1)[:, None] * 1.5, 3, seed)
        return smoothstep(0.0, 0.1, np.abs(n - 0.5))

    def push(p):
        lumps = fbm(p * 1.1, 2, seed + 4)
        return (1.0 - ridges(p)) * depth - (lumps - 0.5) * 0.1

    def paint(p, c):
        f = ridges(p)
        n = fbm(p * 0.9, 3, seed + 5)
        col = ramp([(0.0, BARK_DARK), (0.5, BARK), (1.0, BARK_LIGHT)], f * (0.6 + 0.6 * n))
        spots = smoothstep(0.64, 0.7, fbm(p * 2.4, 2, seed + 9)) * lichen
        col = mix(col, LICHEN, spots[:, None])
        up = _normal(plate, p, 0.04)[:, 1]
        m = smoothstep(0.45, 0.8, up + (fbm(p * 0.8, 2, seed + 13) - 0.5) * 0.6) * moss
        mc = ramp([(0.0, MOSS_DARK), (0.6, MOSS), (1.0, MOSS_LIGHT)], fbm(p * 2.0, 2, seed + 21))
        return mix(col, mc, m[:, None])

    return Displace(plate, push, depth + 0.14, paint=paint)


def oval(a, b, ca, cb, ra, rb):
    """Signed distance-ish to an ellipse in a plane: a, b are the point's two coordinates."""
    return (np.sqrt(((a - ca) / ra) ** 2 + ((b - cb) / rb) ** 2) - 1.0) * min(ra, rb)


def ragged(region, amount, scale, seed):
    """A cut-out region with a broken, wavy edge."""
    return Displace(region, lambda p: (fbm(p * scale, 2, seed) - 0.5) * amount, amount + 0.2)


# ------------------------------------------------------------------ parts
def torso():
    base = Union(
        RoundCone((0, 0.5, -6.3), (0, 0.6, 2.3), 3.6, 3.55),
        Xf(Sphere(3.3), (0, 2.25, -5.5)),                                # shoulder hump
        Xf(Ellipsoid((3.3, 2.6, 5.6)), (0, -1.05, -2.6)),                # belly
        MirrorX(Xf(Ellipsoid((1.9, 2.5, 2.3)), (2.45, -0.7, -6.9))),     # shoulders
        MirrorX(Xf(Ellipsoid((2.0, 2.5, 2.5)), (2.4, -0.35, 3.5))),      # haunches
        Xf(Ellipsoid((1.0, 0.85, 1.0)), (0, 1.7, 6.0)),                  # tail
        k=1.1)
    fur = furry(base, (0, -0.35, 1.0), 0.75, 0.32, 3, flow2=(0, -1.0, 0.15), moss=1.0, moss_up=0.55,
                belly=lambda p: smoothstep(-0.6, -2.8, p[:, 1]), silver=0.6)

    # bark scales that follow the flanks: two rows of three, the front ones over the back ones
    plates = []
    rows = [(0.55, [(-6.4, 0.62), (-3.3, 0.56), (-0.2, 0.5)], 1.35, 1.95), (-1.35, [(-4.9, 0.5), (-1.8, 0.45), (1.2, 0.4)], 1.15, 1.85)]
    for side in (-1.0, 1.0):
        for yc, scales, ry, rz in rows:
            for i, (zc, out) in enumerate(scales):
                def region(p, yc=yc, zc=zc, ry=ry, rz=rz, side=side):
                    return np.maximum(oval(p[:, 1], p[:, 2], yc, zc, ry, rz), side * -p[:, 0] + 1.6)
                plates.append(Intersect(shell(base, out, -0.15), ragged(Field(region), 0.45, 1.1, int(40 + i + 10 * yc + side)), k=0.16))
    flanks = bark(Union(*plates, k=0.03), (0, 0.15, 1), 31, moss=0.8)

    chest_plates = []
    for k, (yc, rx, out) in enumerate([(0.95, 2.4, 0.62), (-0.45, 2.6, 0.52), (-1.85, 2.2, 0.42)]):
        def region(p, yc=yc, rx=rx):
            return np.maximum(oval(p[:, 0], p[:, 1], 0.0, yc, rx, 0.95), p[:, 2] + 8.4)
        chest_plates.append(Intersect(shell(base, out, -0.15), ragged(Field(region), 0.4, 1.1, 50 + k), k=0.15))
    chest = bark(Union(*chest_plates, k=0.03), (1, 0, 0), 37, moss=0.5)

    # glowing mushrooms in clumps on the flanks and shoulders, off the climbing line
    stems, caps = [], []
    rng = np.random.default_rng(908)
    for phi_deg, z in [(-50, -6.6), (48, -6.0), (-48, -1.5), (50, 4.4), (-46, 4.8), (44, 0.9)]:
        phi = math.radians(phi_deg)
        nrm = np.array([math.sin(phi), math.cos(phi), 0.0])
        root = hit(base, np.array([0, 0.5, z]) + nrm * 9.0, -nrm)
        for i in range(rng.integers(3, 6)):
            off = np.array([rng.uniform(-0.6, 0.6), rng.uniform(-0.3, 0.3), rng.uniform(-0.8, 0.8)])
            p = hit(base, root + off + nrm * 3.0, -nrm) - nrm * 0.1
            d = normalize(nrm + np.array([rng.uniform(-0.3, 0.3), 0.5, rng.uniform(-0.3, 0.3)]))
            h = rng.uniform(0.6, 1.05)
            r = rng.uniform(0.3, 0.55)
            stems.append(RoundCone(p, p + d * h, 0.12, 0.08, rgb("e8e2cf")))
            cap = Intersect(Ellipsoid((r, r * 0.55, r), rgb("7ff0d8")), Plane((0, -1, 0), r * 0.05), k=0.05)
            caps.append(Xf(cap, p + d * h, basis_y(d)))
    body = Union(fur, flanks, chest, Union(*stems), k=0.0)
    return [
        build("Torso", body, (-5.0, -4.6, -11.6), (5.0, 6.2, 7.5), 0.13, ao=0.45, ao_dist=0.7, cavity=0.35, cavity_r=0.2),
        build("TorsoGlow", Union(*caps), (-5.0, -3.0, -9.0), (5.0, 5.5, 7.0), 0.08, ao=0.0, cavity=0.0),
    ]


def neck():
    nd = normalize((0, 1.0, -3.4))
    c = np.array([0, 0.5, -1.7])
    base = Union(RoundCone(c - nd * 0.4, c + nd * 0.4, 2.3, 2.3),
                 Xf(Ellipsoid((2.2, 2.3, 2.6)), (0, 0.4, -0.2)), k=0.8)
    fur = furry(base, (0, -0.25, 1.0), 0.6, 0.3, 5, flow2=(0, -1.0, 0.0), moss=0.7, moss_up=0.6, silver=0.7)
    return [build("Neck", fur, (-3.2, -2.6, -5.0), (3.2, 3.6, 2.4), 0.11, ao=0.45, ao_dist=0.6, cavity=0.35, cavity_r=0.18)]


EYE = np.array([0.95, 0.42, -2.42])


def head():
    cranium = Xf(Ellipsoid((2.55, 2.35, 2.8)), (0, 0.2, 0.2))
    cheeks = MirrorX(Xf(Ellipsoid((1.5, 1.55, 1.8)), (1.5, -0.65, -0.8)))
    muzzle = RoundCone((0, -0.75, -1.8), (0, -0.95, -3.95), 1.45, 1.08)
    jaw = RoundCone((0, -1.95, -1.6), (0, -2.1, -3.6), 1.0, 0.72)
    furbase = Union(cranium, cheeks, muzzle, jaw, k=0.6)
    mouth = Xf(Ellipsoid((1.05, 0.22, 1.6)), (0, -1.6, -3.3), euler(-0.08, 0, 0))
    furbase = Sub(furbase, mouth, k=0.12)
    fur = furry(furbase, (0, 0.1, 1.0), 0.45, 0.2, 7, flow2=(0, -1.0, 0.2), moss=0.5, moss_up=0.72, silver=0.4)
    inner_mouth = Xf(Ellipsoid((0.95, 0.18, 1.4), rgb("3a1414")), (0, -1.6, -3.15))

    # the bone mask: a plate over brow, eyes and the bridge of the muzzle, floating on the fur
    maskbase = Union(cranium, RoundCone((0, -0.5, -1.8), (0, -0.66, -3.95), 1.0, 0.76), k=0.6)

    def face(p):
        n = fbm(p * 1.3, 2, 61)
        front = p[:, 2] + 1.15 + (n - 0.5) * 0.7 - 0.25 * np.clip(p[:, 1] - 1.2, 0, None)
        low = -(p[:, 1] + 0.62 - 0.12 * np.clip(-p[:, 2] - 3.0, 0, None)) + (n - 0.5) * 0.25
        side = np.abs(p[:, 0]) - 2.1 - 0.4 * np.clip(-p[:, 1], 0, None)
        return np.maximum.reduce([front, low, side])
    mask = Intersect(shell(maskbase, 0.45, 0.12), Field(face), k=0.14)
    brows = MirrorX(Xf(Ellipsoid((1.0, 0.36, 0.6)), (0.95, 1.12, -2.55), euler(0.15, -0.25, 0.3)))
    nubs = MirrorX(RoundCone((0.62, 1.85, -2.05), (0.75, 2.3, -2.45), 0.3, 0.1))
    mask = Union(mask, brows, nubs, k=0.22)
    sockets = MirrorX(Xf(Ellipsoid((0.6, 0.48, 1.0)), EYE + vec(0, 0, -0.35), euler(0, -0.3, 0.25)))
    mask = Sub(mask, sockets, k=0.1, inner=SOCKET)

    def cracks(p):
        return (1.0 - smoothstep(0.0, 0.025, np.abs(fbm(p * 1.1, 3, 71) - 0.5))) * 0.05

    def mask_paint(p, c):
        n = fbm(p * 1.5, 3, 73)
        col = mix(BONE_DARK, BONE, np.clip(0.55 + 0.6 * n, 0, 1)[:, None])
        crack = 1.0 - smoothstep(0.0, 0.03, np.abs(fbm(p * 1.1, 3, 71) - 0.5))
        col = col * (1.0 - 0.55 * crack)[:, None]
        stripe = smoothstep(0.24, 0.12, np.abs(p[:, 0])) * smoothstep(-0.9, -1.5, p[:, 2]) * smoothstep(1.7, 1.2, p[:, 1])
        streak = smoothstep(0.2, 0.08, np.abs(np.abs(p[:, 0]) - 0.95 + p[:, 1] * 0.15)) * smoothstep(0.0, -0.2, p[:, 1]) \
            * smoothstep(-0.9, -0.6, p[:, 1])
        col = mix(col, TEAL * 0.85, np.clip(stripe + streak, 0, 1)[:, None])
        dark = np.abs(c - SOCKET).sum(axis=1) < 0.2
        return np.where(dark[:, None], c, col)
    mask = Displace(mask, cracks, 0.08, paint=mask_paint)

    nose = Xf(Ellipsoid((0.7, 0.48, 0.46), rgb("15110f")), (0, -0.76, -4.78))
    nose = Sub(nose, MirrorX(Xf(Ellipsoid((0.17, 0.1, 0.2)), (0.28, -0.84, -5.2))), k=0.05, inner=rgb("050404"))

    teeth = []
    for i in range(5):
        z = -4.0 + i * 0.3
        x = 0.42 + i * 0.1
        for s in (-1, 1):
            teeth.append(RoundCone((s * x, -1.4, z), (s * x * 0.98, -1.78, z - 0.04), 0.13, 0.02, rgb("efe6d0")))
            teeth.append(RoundCone((s * x * 0.95, -1.82, z + 0.1), (s * x * 0.95, -1.5, z + 0.06), 0.11, 0.02, rgb("efe6d0")))
    tusk = Tube(bezier((0.88, -1.9, -3.25), (1.4, -1.6, -4.15), (1.12, -0.5, -4.5), 8), taper(0.27, 0.04, 8),
                lambda t: ramp([(0, BONE_DARK), (0.4, BONE), (1, rgb("fffaf0"))], t))

    ear = Sub(Xf(Ellipsoid((0.8, 0.92, 0.5)), (1.85, 2.0, 0.65), euler(0.1, 0.3, -0.45)),
              Xf(Ellipsoid((0.5, 0.62, 0.3)), (1.9, 2.05, 0.3), euler(0.1, 0.3, -0.45)), k=0.1, inner=rgb("3a2620"))
    ear = furry(ear, (0, 1, 0.3), 0.32, 0.1, 9)

    def wood_col(t):
        return ramp([(0, rgb("3e3226")), (0.5, rgb("6e5a46")), (1, rgb("9a8670"))], t)
    wood, leaves = [], []
    main = bezier((1.0, 2.1, 0.7), (2.5, 3.8, 0.5), (3.2, 5.5, 2.2), 10)
    wood.append(Tube(main, taper(0.5, 0.16, 10), wood_col))
    for t0, d, ln in [(0.35, (0.1, 1.0, -0.4), 1.9), (0.6, (0.4, 1.0, -0.1), 1.6), (0.82, (0.7, 0.7, 0.4), 1.2), (0.5, (1.0, 0.1, 0.6), 1.1)]:
        start = main[int(t0 * 9)]
        dv = normalize(d)
        tip = start + dv * ln
        wood.append(Tube(bezier(start, start + dv * ln * 0.5 + np.array([0.15, 0.1, 0.25]), tip, 6), taper(0.28, 0.05, 6), wood_col))
        leaves.append(Xf(Ellipsoid((0.34, 0.24, 0.34)), tip + dv * 0.08))
    leaves.append(Xf(Ellipsoid((0.42, 0.3, 0.42)), main[-1] + np.array([0.1, 0.15, 0.1])))
    antler = Displace(Union(*wood, k=0.12), lambda p: (fbm(p * 2.5, 2, 81) - 0.5) * 0.08, 0.12)
    foliage = Paint(Displace(Union(*leaves, k=0.15), lambda p: -(fbm(p * 3.0, 2, 85) - 0.4) * 0.2, 0.3),
                    lambda p, c: ramp([(0, MOSS_DARK), (0.5, rgb("5f9a30")), (1, rgb("a6d050"))], fbm(p * 2.2, 2, 83)))
    moss_root = Paint(Displace(Xf(Ellipsoid((0.72, 0.34, 0.72)), (1.0, 2.1, 0.7)), lambda p: -(fbm(p * 3, 2, 87) - 0.5) * 0.15, 0.2),
                      lambda p, c: ramp([(0, MOSS_DARK), (1, MOSS_LIGHT)], fbm(p * 2.5, 2, 89)))

    body = Union(fur, inner_mouth, mask, nose, Union(*teeth), MirrorX(tusk), MirrorX(ear), k=0.0)
    # the antlers are their own mesh: they snap off when the head part breaks
    antlers = MirrorX(Union(antler, foliage, moss_root))
    eyes = MirrorX(Xf(Ellipsoid((0.3, 0.24, 0.2), rgb("b8fff0")), EYE, euler(0, -0.3, 0.25)))
    return [
        build("Head", body, (-5.0, -3.4, -6.0), (5.0, 7.0, 3.8), 0.085, ao=0.45, ao_dist=0.5, cavity=0.4, cavity_r=0.14),
        build("HeadGlow", eyes, (-1.5, -0.2, -3.2), (1.5, 1.0, -1.8), 0.04, ao=0.0, cavity=0.0),
        build("Antlers", antlers, (-5.0, 1.2, -1.0), (5.0, 7.2, 4.0), 0.07, ao=0.4, ao_dist=0.4, cavity=0.4, cavity_r=0.12),
    ]


def _greave(base, r2, l2, seed):
    """Bark shin plates curving round the front (-z) of a lower leg, two overlapping."""
    plates = []
    for k, (y0, out) in enumerate([(l2 * 0.12, 0.42), (-l2 * 0.2, 0.34)]):
        def region(p, y0=y0):
            return np.maximum(oval(p[:, 0], p[:, 1], 0.0, y0, r2 * 1.05, l2 * 0.24), p[:, 2] + r2 * 0.15)
        plates.append(Intersect(shell(base, out, -0.1), ragged(Field(region), 0.3, 1.3, seed + k), k=0.12))
    return bark(Union(*plates, k=0.03), (0, 1, 0), seed, moss=0.5, depth=0.05)


def leg(name, l1, l2, r1, r2, front):
    upper_base = Union(RoundCone((0, l1 * 0.42, 0.05), (0, -l1 * 0.45, 0), r1 * 1.0, r1 * 0.82),
                       Xf(Ellipsoid((r1 * 0.88, l1 * 0.42, r1 * 0.9)), (0, l1 * 0.12, r1 * 0.3)), k=0.6)
    upper = furry(upper_base, (0, -1.0, 0.2), 0.55, 0.28, 11 + front, flow2=(0, 0, 1.0), moss=0.4, moss_up=0.7)
    lower_base = Union(RoundCone((0, l2 * 0.33, 0), (0, -l2 * 0.4, -0.05), r2 * 0.97, r2 * 0.86),
                       Xf(Ellipsoid((r2 * 0.8, l2 * 0.3, r2 * 0.85)), (0, l2 * 0.12, r2 * 0.25)), k=0.5)
    lower = furry(lower_base, (0, -1.0, 0.1), 0.46, 0.24, 13 + front, flow2=(0, 0, 1.0),
                  socks=lambda p: smoothstep(0.0, -l2 * 0.5, p[:, 1]))
    # the bark shin is its own mesh: it falls off when the leg part breaks
    greave = _greave(lower_base, r2, l2, 17 + front)
    h1 = (l1 + r1) * 0.5 + 0.6
    h2 = (l2 + r2 * 0.6) * 0.5 + 0.6
    return [
        build("Upper" + name, upper, (-r1 - 0.8, -h1, -r1 - 0.8), (r1 + 0.8, h1, r1 + 1.2), 0.1, ao=0.45, ao_dist=0.5, cavity=0.35,
              cavity_r=0.16),
        build("Lower" + name, lower, (-r2 - 0.8, -h2, -r2 - 0.9), (r2 + 0.8, h2, r2 + 0.9), 0.09, ao=0.45, ao_dist=0.45, cavity=0.35,
              cavity_r=0.14),
        build("Greave" + name, greave, (-r2 - 0.8, -h2, -r2 - 0.9), (r2 + 0.8, h2, 0.2), 0.07, ao=0.4, ao_dist=0.3, cavity=0.35,
              cavity_r=0.12),
    ]


def paw():
    pad = Union(Xf(Ellipsoid((1.4, 0.7, 1.75)), (0, -0.05, -0.4)),
                Xf(Ellipsoid((1.1, 0.95, 1.15)), (0, 0.45, 0.15)), k=0.5)
    toes = [Xf(Ellipsoid((0.42, 0.38, 0.5)), (-0.9 + i * 0.6, -0.22, -1.7 - 0.12 * (1 - abs(i - 1.5) / 1.5))) for i in range(4)]
    foot = Intersect(Union(pad, *toes, k=0.2), Plane((0, -1, 0), 0.6), k=0.12)
    fur = furry(foot, (0, -0.5, -1.0), 0.38, 0.17, 19, flow2=(0, -1.0, 0.0), socks=lambda p: np.full(len(p), 0.6))
    claws = []
    for i in range(4):
        x = -0.9 + i * 0.6
        z0 = -2.05 - 0.12 * (1 - abs(i - 1.5) / 1.5)
        pts = bezier((x, -0.12, z0 + 0.15), (x, -0.05, z0 - 0.6), (x, -0.62, z0 - 0.95), 7)
        claws.append(Tube(pts, taper(0.24, 0.03, 7), lambda t: ramp([(0, rgb("3b302a")), (0.45, BONE_DARK), (1, rgb("fbf5e6"))], t)))
    body = Union(fur, Union(*claws), k=0.0)
    return [build("Paw", body, (-1.9, -0.8, -3.4), (1.9, 1.7, 1.9), 0.075, ao=0.45, ao_dist=0.4, cavity=0.35, cavity_r=0.12)]


PARTS = {
    "torso": torso, "neck": neck, "head": head, "front": lambda: leg("Front", 4.1, 4.2, 1.5, 1.2, 0),
    "hind": lambda: leg("Hind", 4.4, 4.0, 1.75, 1.25, 1), "paw": paw,
}
CACHE = os.path.join(os.path.dirname(__file__), ".cache")


def bear(only=None):
    os.makedirs(CACHE, exist_ok=True)
    out = []
    for key, fn in PARTS.items():
        path = os.path.join(CACHE, "gau_ma_%s.npz" % key)
        if only and key not in only and os.path.exists(path):
            data = np.load(path, allow_pickle=True)
            out += list(data["meshes"])
            print("  %-14s (cached)" % key)
            continue
        meshes = fn()
        np.savez(path, meshes=np.array(meshes, dtype=object), allow_pickle=True)
        out += meshes
    return out
