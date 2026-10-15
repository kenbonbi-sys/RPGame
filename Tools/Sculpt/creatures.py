"""The forest creatures, sculpted: `python Tools/Sculpt/creatures.py [slime] [shroom] [bear]`
writes Godot/assets/creatures/<name>.glb (one node per part). Faces look along -Z, feet on y = 0.

Slime Rêu is built at radius 1 (the game scales it); Nấm Độc in metres at size 1; Gấu Ma in bear
units (the game multiplies by GauMa.S), each part in its own frame, matching the colliders in
gau_ma.gd so heroes hold on to the fur they see.
"""
import math
import os
import sys
import time

import numpy as np

from sdf import (Box, Displace, hit, Ellipsoid, Intersect, MirrorX, Paint, Plane, RoundCone, Sphere, Sub, Torus, Tube, Union, Xf,
                 axis_angle, basis_y, bezier, build, clumps, euler, fbm, mix, normalize, smoothstep, taper, vec, vnoise, write_glb)

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Godot", "assets", "creatures")


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def blend(stops):
    """A colour ramp: stops [(t, colour)], t sorted; returns f(t array) -> colours."""
    ts = np.array([s[0] for s in stops])
    cs = np.stack([np.asarray(s[1], dtype=float) for s in stops])

    def f(t):
        t = np.clip(np.asarray(t, dtype=float), ts[0], ts[-1])
        return np.stack([np.interp(t, ts, cs[:, i]) for i in range(3)], axis=1)
    return f


def mottle(base, amount, scale, seed=0, octaves=3):
    """A colour with painterly blotches."""
    base = np.asarray(base, dtype=float)

    def f(p):
        n = fbm(p * scale, octaves, seed)
        return base[None, :] * (1.0 - amount + 2.0 * amount * n)[:, None]
    return f


# ================================================================== Slime Rêu
def slime():
    jelly_base = Union(
        Xf(Ellipsoid((1.0, 0.8, 1.0)), (0, 0.78, 0)),
        Xf(Ellipsoid((0.62, 0.5, 0.62)), (0.02, 1.22, 0.04)),
        Xf(Ellipsoid((1.16, 0.3, 1.16)), (0, 0.22, 0)),
        k=0.32)
    drips = []
    for i in range(7):
        a = i / 7 * math.tau + 0.4
        r = 1.06 + 0.08 * math.sin(i * 2.3)
        drips.append(Xf(Ellipsoid((0.26, 0.17, 0.26)), (math.cos(a) * r, 0.12, math.sin(a) * r)))
    jelly = Union(jelly_base, *drips, k=0.22)
    jelly = Intersect(jelly, Plane((0, -1, 0), 0.0), k=0.08)
    jelly = Displace(jelly, lambda p: (fbm(p * 2.2, 2, 3) - 0.5) * 0.07, 0.1)

    # angry eyes: dark glossy almonds, the inner top corner cut away
    eye = Xf(Ellipsoid((0.19, 0.25, 0.13), rgb("101a14")), (0.31, 0.98, -0.86), euler(0.12, -0.36, 0.0))
    lid = Plane(normalize((-0.55, 1.0, -0.15)), 0.0)
    eye = Intersect(eye, Xf(lid, (0.31, 1.07, -0.86)), k=0.02)
    eyes = MirrorX(eye)
    # a wide grumpy mouth, flat at the bottom, with a dark green inside
    mouth = Intersect(Xf(Ellipsoid((0.27, 0.14, 0.09), rgb("0d2414")), (0, 0.66, -0.93), euler(0.18, 0, 0)),
                      Plane((0, -1, 0), -0.62), k=0.02)
    tongue = Xf(Ellipsoid((0.11, 0.05, 0.06), rgb("b8485a")), (0.05, 0.645, -0.95))

    # a thick cap of moss: a shell over the crown with a ragged edge, clumps, strands hanging down
    def moss_col(p):
        n = fbm(p * 3.0, 3, 21)
        light = smoothstep(1.3, 1.85, p[:, 1])
        c = blend([(0, rgb("3d6b22")), (0.5, rgb("5f9a2c")), (1, rgb("a7cf4a"))])(n * 0.6 + light * 0.5)
        return c
    crown = Sub(Xf(Ellipsoid((1.07, 0.88, 1.07)), (0, 0.78, 0)), Xf(Ellipsoid((0.98, 0.79, 0.98)), (0, 0.76, 0)), k=0.02)
    cut = Displace(Plane((0, -1, 0), -1.2), lambda p: (fbm(p * 3.5, 2, 5) - 0.5) * 0.28, 0.4)
    turf = Intersect(crown, cut, k=0.06)
    lumps = []
    rng = np.random.default_rng(5)
    for i in range(9):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0.0, 0.55)
        y = 0.78 + 0.88 * math.sqrt(max(0.0, 1 - (rr / 1.07) ** 2)) - 0.04
        s = rng.uniform(0.14, 0.24)
        lumps.append(Xf(Ellipsoid((s, s * 0.7, s)), (math.cos(a) * rr, y, math.sin(a) * rr)))
    strands = []
    for i, a in enumerate([0.5, 1.6, 2.5, 3.4, 4.6, 5.6]):
        a += 0.15 * math.sin(i * 7.1)
        top = np.array([math.cos(a) * 0.72, 1.32, math.sin(a) * 0.72])
        if top[2] < -0.5:      # keep the face clear
            continue
        length = 0.3 + 0.12 * (i % 3)
        bot = top + np.array([math.cos(a) * 0.17, -length, math.sin(a) * 0.17])
        strands.append(RoundCone(top, bot, 0.09, 0.035))
    moss = Displace(Union(turf, *lumps, *strands, k=0.07), lambda p: -(fbm(p * 9.0, 2, 8) - 0.35) * 0.05, 0.08,
                    paint=lambda p, c: moss_col(p))
    moss = Paint(moss, lambda p, c: moss_col(p))

    # sprouts, a curled fern and a little red toadstool on top
    green = rgb("7cc243")
    bits = []
    for x, z, h, lean in [(-0.18, 0.05, 0.36, -0.25), (0.08, -0.1, 0.3, 0.2), (-0.02, 0.22, 0.24, 0.05)]:
        base = np.array([x, 1.55, z])
        tip = base + np.array([lean * 0.4, h, 0.03])
        bits.append(RoundCone(base, tip, 0.028, 0.02, green))
        for s in (-1, 1):
            leaf_at = tip + np.array([s * 0.09, 0.02, 0.0])
            bits.append(Xf(Ellipsoid((0.11, 0.025, 0.06), rgb("8fd14e")), leaf_at, euler(0.0, 0.0, s * 0.45)))
    shroom_at = np.array([0.42, 1.4, 0.3])
    bits.append(RoundCone(shroom_at, shroom_at + np.array([0.02, 0.2, 0.0]), 0.05, 0.04, rgb("f2ead8")))

    def toad(p):
        q = p - (shroom_at + np.array([0.02, 0.24, 0]))
        spot = vnoise(q * 22.0, 4) > 0.72
        return np.where(spot[:, None], rgb("fff4e0")[None, :], rgb("d8352a")[None, :])
    bits.append(Intersect(Xf(Ellipsoid((0.15, 0.1, 0.15), toad), shroom_at + np.array([0.02, 0.22, 0.0])),
                          Plane((0, -1, 0), -(shroom_at[1] + 0.19)), k=0.01))
    # pebbles and a twig caught inside the jelly
    stone = mottle(rgb("8a8577"), 0.25, 6.0, 30)
    inside = [Xf(Ellipsoid((0.17, 0.12, 0.15), stone), (0.5, 0.3, 0.35), euler(0.3, 0.8, 0.2)),
              Xf(Ellipsoid((0.13, 0.1, 0.12), stone), (-0.55, 0.42, 0.3), euler(-0.2, 0.3, 0.5)),
              Xf(Ellipsoid((0.1, 0.08, 0.1), stone), (0.05, 0.25, 0.6), euler(0.5, 0.1, 0.0)),
              Tube(bezier((-0.5, 0.36, 0.55), (-0.1, 0.55, 0.65), (0.35, 0.82, 0.45), 6), taper(0.045, 0.03, 6), rgb("6b4a2e"))]
    body = Union(eyes, mouth, tongue, moss, *bits, *inside)
    glints = MirrorX(Union(Xf(Sphere(0.045, (1, 1, 1)), (0.35, 1.08, -0.97)), Xf(Sphere(0.022, (1, 1, 1)), (0.27, 0.92, -0.98))))

    lo, hi = (-1.45, -0.02, -1.45), (1.45, 2.05, 1.45)
    return [
        build("Jelly", jelly, lo, (1.45, 1.8, 1.45), 0.04, ao=0.0, cavity=0.0),
        build("Body", body, lo, hi, 0.026, ao=0.55, ao_dist=0.18, cavity=0.35),
        build("Glints", glints, (0.2, 0.85, -1.05), (0.42, 1.15, -0.9), 0.008, ao=0.0, cavity=0.0),
    ]


# ================================================================== Nấm Độc
def shroom():
    cream = rgb("d2b383")

    def stem_col(p):
        n = fbm(p * 5.0, 3, 41)
        y = p[:, 1]
        c = blend([(0.0, rgb("5e4630")), (0.16, rgb("ad8d62")), (0.5, cream), (0.82, rgb("c9a2b8")), (1.05, rgb("8a5aa0"))])(y + (n - 0.5) * 0.15)
        return c * (0.9 + 0.2 * n)[:, None]

    stem = Union(
        RoundCone((0, 0.16, 0.0), (0, 0.98, 0.04), 0.4, 0.25),
        Xf(Ellipsoid((0.46, 0.4, 0.43)), (0, 0.42, -0.03)),
        k=0.22)
    # stubby arms ending in mitts
    arm = Union(RoundCone((0.3, 0.74, -0.04), (0.5, 0.5, -0.2), 0.1, 0.075),
                Xf(Ellipsoid((0.1, 0.11, 0.1)), (0.53, 0.45, -0.22)), k=0.05)
    # root feet with tendrils spreading on the ground
    foot = [Xf(Ellipsoid((0.2, 0.12, 0.27)), (0.21, 0.08, -0.08))]
    for a, l in [(-0.45, 0.24), (0.1, 0.27), (0.65, 0.22)]:
        d = np.array([math.sin(a), 0, -math.cos(a)])
        start = np.array([0.21, 0.07, -0.08])
        foot.append(RoundCone(start, start + d * l + np.array([0, -0.02, 0]), 0.085, 0.055))
    body = Union(stem, MirrorX(arm), MirrorX(Union(*foot, k=0.05)), k=0.07)

    def on_skin(x, y, inset):
        """A point on the front of the stem, `inset` under the skin."""
        return hit(stem, (x, y, -2.0), (0, 0, 1)) + vec(0, 0, inset)
    body = Displace(body, lambda p: (fbm(p * 6.0, 2, 44) - 0.5) * 0.03, 0.05)
    # a ring of skin under the cap
    ring = Xf(Union(Torus(0.27, 0.05), Xf(Torus(0.31, 0.035), (0, -0.05, 0)), k=0.04), (0, 0.9, 0.03))
    # a jagged grin with two fangs
    mouth = Xf(Ellipsoid((0.2, 0.075, 0.2)), on_skin(0, 0.53, 0.02), euler(-0.15, 0, 0))
    body = Sub(Union(body, ring, k=0.03), mouth, k=0.04, inner=rgb("2b0b2e"))
    body = Paint(body, lambda p, c: np.where((np.abs(c - rgb("2b0b2e")).sum(axis=1) < 0.25)[:, None], c, stem_col(p)))
    fang = on_skin(0.1, 0.565, 0.02)
    fangs = MirrorX(RoundCone(fang, fang + vec(-0.012, -0.09, -0.025), 0.03, 0.006, rgb("fff6df")))
    brows = MirrorX(Xf(Ellipsoid((0.14, 0.04, 0.06), rgb("8f6a9a")), on_skin(0.135, 0.8, 0.02), euler(0.0, -0.3, -0.42)))
    body = Union(body, fangs, brows, k=0.0)

    eye_at = on_skin(0.135, 0.71, 0.03)
    eyes = MirrorX(Intersect(Xf(Ellipsoid((0.1, 0.072, 0.055), rgb("e8ff6a")), eye_at, euler(0.0, -0.3, 0.0)),
                             Xf(Plane(normalize((-0.6, 1.0, 0.0))), eye_at + vec(0, 0.01, 0)), k=0.01))

    # the cap, around its own origin (the game scales it to puff): a droopy dome tipped forward,
    # wavy rim, warts, gills underneath
    def cap_col(p):
        n = fbm(p * 3.2, 3, 51)
        h = np.clip(p[:, 1] / 0.62, 0, 1)
        c = blend([(0, rgb("3a1240")), (0.35, rgb("6a2a78")), (0.75, rgb("9a46a8")), (1, rgb("b968bf"))])(h + (n - 0.5) * 0.35)
        return c

    dome = Intersect(Union(Ellipsoid((0.88, 0.58, 0.88)), Xf(Ellipsoid((0.4, 0.3, 0.4)), (0.0, 0.36, 0.04)), k=0.3),
                     Plane((0, -1, 0), 0.06), k=0.1)
    under = Xf(Ellipsoid((0.7, 0.16, 0.7)), (0, -0.1, 0))
    cap = Sub(dome, under, k=0.08)

    def rim_wave(p):
        ang = np.arctan2(p[:, 2], p[:, 0])
        rad = np.hypot(p[:, 0], p[:, 2])
        return -np.sin(ang * 6.0 + 0.6) * 0.03 * smoothstep(0.6, 0.88, rad)
    cap = Displace(cap, rim_wave, 0.05)

    def gills(p):
        ang = np.arctan2(p[:, 2], p[:, 0])
        rad = np.hypot(p[:, 0], p[:, 2])
        inside_dome = smoothstep(-0.03, -0.08, dome.d(p)) * smoothstep(0.25, 0.35, rad) * smoothstep(-0.0, -0.04, p[:, 1])
        return (np.abs(np.sin(ang * 34.0)) - 0.5) * 0.022 * inside_dome

    def gill_col(p, c):
        under_side = smoothstep(-0.02, -0.06, dome.d(p)) * smoothstep(0.02, -0.03, p[:, 1])
        rad = np.hypot(p[:, 0], p[:, 2])
        g = blend([(0.2, rgb("4a2442")), (0.6, rgb("9a6a8c")), (0.9, rgb("c493b4"))])(rad)
        return mix(c, g, under_side[:, None])
    cap = Displace(cap, gills, 0.05)
    warts = []
    rng = np.random.default_rng(12)
    for i in range(16):
        a = rng.uniform(0, math.tau)
        u = rng.uniform(0.3, 0.97)
        dvec = np.array([math.cos(a) * math.sqrt(1 - u * u), u, math.sin(a) * math.sqrt(1 - u * u)])
        at = hit(dome, dvec * 2.0, -dvec)
        s = rng.uniform(0.06, 0.12) * (1.15 - 0.35 * u)
        warts.append(Xf(Ellipsoid((s, s * 0.32, s)), at, basis_y(at / np.array([0.88, 0.58, 0.88]) ** 2)))

    def wart_col(p):
        return rgb("cfe85a") * (0.9 + 0.2 * fbm(p * 12, 2, 3))[:, None]
    cap = Union(Paint(cap, lambda p, c: cap_col(p)), Paint(Union(*warts), lambda p, c: wart_col(p)), k=0.02)
    cap = Paint(cap, gill_col)
    cap = Xf(cap, (0, 0, 0), euler(-0.08, 0, 0))

    return [
        build("Body", body, (-0.8, -0.05, -0.8), (0.8, 1.3, 0.8), 0.018, ao=0.55, ao_dist=0.12, cavity=0.3),
        build("Eyes", eyes, (-0.3, 0.6, -0.5), (0.3, 0.85, -0.25), 0.008, ao=0.0, cavity=0.0),
        build("Cap", cap, (-1.0, -0.25, -1.0), (1.0, 0.9, 1.0), 0.02, ao=0.5, ao_dist=0.14, cavity=0.35),
    ]


def bear(only=None):
    from bear import bear as sculpt_bear
    return sculpt_bear(only)


SPECIES = {"slime": slime, "shroom": shroom, "gau_ma": bear}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    args = sys.argv[1:]
    only = None
    if "--only" in args:
        i = args.index("--only")
        only = args[i + 1].split(",")
        del args[i:i + 2]
    for name in args or list(SPECIES):
        t = time.time()
        print(name)
        meshes = SPECIES[name](only) if name == "gau_ma" else SPECIES[name]()
        write_glb(os.path.join(OUT, name + ".glb"), meshes)
        print("  %.1f s" % (time.time() - t))
