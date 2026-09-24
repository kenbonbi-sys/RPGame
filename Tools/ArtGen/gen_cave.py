"""
Hang Pha Lê (the crystal cave north of the swamp): terrain tiles, props and item icons.

Tiles (16x16), layered like the swamp (gen_swamp):
  Ground : cave_i            dark blue-grey rock floor (random variants)
  Walls  : cavewall_i / cavewallfull_i   marching squares: the rock mass seen from above, a lit lip
           along its top edge and a cliff face below its bottom edge (its own tilemap, solid)
  Details: shards_i (crystal splinters), pebbles_c, crack_i, glowcap_0, cavepuddle_0
Props return (Canvas, pivot) like gen_props: crystal clusters in three colours (they carry their
own light in Unity), stalagmites, cave rocks, mine rails and carts, timber supports, cobwebs and
the crystal pillars of the spider queen's hall.
"""
import math
import random

import numpy as np

from pixelkit import Canvas, P, OUTLINE, TileNoise, hx, ramp, mix, shaded_ellipse, shade_index, shaded_poly, point_in_poly
from gen_terrain import ms_mask, edge_dist

T = 16

C = {
    "floor": ramp("#14151d", "#1a1c26", "#212430", "#292d3b", "#333848", "#3f4557"),
    "rock": ramp("#07080d", "#0c0e15", "#12141e", "#191c28", "#222636", "#2e3346", "#3c4259"),
    "face": ramp("#141621", "#1e2130", "#282c3e", "#33384d", "#3f455c", "#4c536c"),
    "cyan": ramp("#0a2a38", "#0e4658", "#136a7a", "#1c94a0", "#36c2c4", "#88ecea", "#dcfffc"),
    "pink": ramp("#2a0a34", "#461054", "#6a1a7a", "#9428a2", "#c048c8", "#e888ea", "#ffdcff"),
    "amber": ramp("#361e08", "#58320c", "#824c12", "#aa6e1c", "#d29632", "#f0c464", "#fff0c0"),
    "timber": ramp("#1c120c", "#2c1c12", "#40291a", "#563823", "#6e4a2e", "#8a6040"),
    "iron": ramp("#16171c", "#24262e", "#363944", "#4c505e", "#666c7c", "#8a92a2"),
    "web": ramp("#8890a0", "#b0b8c8", "#d8e0ee", "#f4f8ff"),
    "bone": ramp("#4e4638", "#7a705e", "#a89c86", "#d0c6ae", "#eee6d2"),
    "cap": ramp("#12304a", "#1a4f6c", "#2a7aa0", "#4cb0d0", "#9ae4f4"),
}
CAVE_OUT = hx("#07080c")

n_lo = TileNoise(T, 2, seed=171)
n_hi = TileNoise(T, 4, seed=173)
n_hi2 = TileNoise(T, 8, seed=179)


# ============================================================================
# Terrain
# ============================================================================

def cave_floor(seed):
    """Worn rock: fine grain, a few darker cracks and light pebbles."""
    cv = Canvas(T, T)
    f = C["floor"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            n = n_hi2(x + seed * 3, y + seed) * 0.6 + n_lo(x + seed, y) * 0.4
            c = f[2] if n > 0.42 else f[1]
            if n > 0.74:
                c = f[3]
            cv.px(x, y, c)
    # a hairline crack now and then
    if rnd.random() < 0.45:
        x, y = rnd.randrange(2, T - 2), rnd.randrange(2, T - 2)
        for _ in range(rnd.randint(3, 6)):
            cv.px(x, y, f[0])
            x += rnd.choice((-1, 0, 1))
            y += 1
            if not (0 <= x < T and 0 <= y < T):
                break
    for _ in range(rnd.randint(2, 4)):
        x, y = rnd.randrange(T), rnd.randrange(1, T)
        cv.px(x, y, f[4])
        cv.px(x, y - 1, f[3])
    return cv


def rock_texture(cv, seed, mask, veins=True):
    """The top of the rock mass: dark, lumpy, with the odd vein of crystal."""
    r = C["rock"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if not mask[y, x]:
                continue
            n = n_hi(x + seed * 7, y + seed * 3) * 0.55 + n_hi2(x, y + seed) * 0.45
            c = r[2] if n > 0.45 else r[1]
            if n > 0.75:
                c = r[3]
            cv.px(x, y, c)
    for _ in range(rnd.randint(2, 4)):
        x, y = rnd.randrange(T), rnd.randrange(T)
        if mask[y, x]:
            cv.px(x, y, r[0])
    if veins and rnd.random() < 0.12:
        col = C[rnd.choice(("cyan", "cyan", "pink", "amber"))]
        x, y = rnd.randrange(2, T - 3), rnd.randrange(2, T - 3)
        for k in range(rnd.randint(2, 4)):
            if 0 <= x < T and 0 <= y < T and mask[y, x]:
                cv.px(x, y, col[3 if k % 2 else 4])
            x += 1
            y += rnd.choice((-1, 0, 1))


FACE_H = 6


def wall_tile(idx, seed=0):
    """The rock mass (marching squares): its top as the rock texture, a lit lip where it meets
    the floor above it, and below its bottom edge a cliff face with strata, then a dark foot."""
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.28, seed_shift=31)
    rock_texture(cv, seed + idx * 7, m)
    r = C["rock"]
    fc = C["face"]
    ed = edge_dist(m, idx)
    # the lip along the top edge (the floor is above: the cliff top, lit), a dark line on the sides
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            if y - 1 >= 0 and not m[y - 1, x]:
                cv.px(x, y, r[5])
                if y + 1 < T and m[y + 1, x]:
                    cv.px(x, y + 1, r[4])
            elif ed[y, x] == 1 and (x - 1 >= 0 and not m[y, x - 1] or x + 1 < T and not m[y, x + 1]):
                cv.px(x, y, r[0])
    # the cliff face: floor pixels under the rock, down to FACE_H below it
    for x in range(T):
        for y in range(T):
            if m[y, x]:
                continue
            above = 0
            for k in range(1, FACE_H + 1):
                if y - k >= 0 and m[y - k, x]:
                    above = k
                    break
            if above == 0:
                continue
            # rows of the face from the top (1) to the foot (FACE_H): lit under the lip, darker below
            if above == FACE_H:
                cv.px(x, y, CAVE_OUT)
                continue
            level = 5 - (above * 4) // FACE_H
            crack = (x * 5 + idx * 3) % 7 == 0 or (x * 3 + idx) % 11 == 0
            if crack and above < FACE_H - 1 - (x + idx) % 3:
                level -= 2          # a crack running down the face, not always to the foot
            elif (x * 7 + above * 3 + idx) % 13 == 0:
                level -= 1
            cv.px(x, y, fc[max(0, min(len(fc) - 1, level))])
    return cv


def wall_full(seed):
    m = np.ones((T, T), bool)
    cv = Canvas(T, T)
    rock_texture(cv, seed, m, veins=False)
    return cv


def detail_shards(seed):
    """Crystal splinters on the floor (they catch the light, no light of their own)."""
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    col = C[("cyan", "pink", "amber")[seed % 3]]
    for _ in range(rnd.randint(2, 3)):
        x, y = rnd.randrange(3, T - 3), rnd.randrange(6, T - 2)
        h = rnd.randint(2, 4)
        for k in range(h):
            cv.px(x, y - k, col[3 + min(3, k)])
        cv.px(x + 1, y, col[2])
        cv.px(x - 1, y, col[1])
    return cv


def detail_pebbles(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    f = C["floor"]
    for _ in range(rnd.randint(3, 5)):
        x, y = rnd.randrange(2, T - 2), rnd.randrange(2, T - 2)
        cv.px(x, y, f[5])
        cv.px(x + 1, y, f[4])
        cv.px(x, y + 1, f[0])
    return cv


def detail_crack(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    f = C["floor"]
    x, y = rnd.randrange(3, T - 3), 1
    while y < T - 1:
        cv.px(x, y, f[0])
        if rnd.random() < 0.4:
            cv.px(x + 1, y, f[0])
        x = max(1, min(T - 2, x + rnd.choice((-1, 0, 1))))
        y += 1
    return cv


def detail_glowcap(seed):
    """Tiny blue cave mushrooms (they glow a little in Unity's dark: the sprite is bright)."""
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    c = C["cap"]
    for _ in range(3):
        x, y = rnd.randrange(3, T - 3), rnd.randrange(7, T - 2)
        cv.px(x, y, hx("#6c7a86"))
        cv.px(x, y - 1, hx("#8a98a4"))
        cv.px(x - 1, y - 2, c[2]); cv.px(x, y - 2, c[4]); cv.px(x + 1, y - 2, c[3])
        cv.px(x, y - 3, c[3])
    return cv


def detail_puddle(seed):
    cv = Canvas(T, T)
    shaded_ellipse(cv, 8, 9, 5.2, 2.4, ramp("#0a1420", "#101e2c", "#18283a", "#2a4058"), dither=0.3, bias=-0.1)
    cv.px(6, 8, hx("#5a7a98"))
    cv.px(7, 8, hx("#7aa0c0"))
    return cv


def tiles():
    """Rows of (name, Canvas) for the terrain sheet."""
    rows = []
    rows.append([(f"cave_{i}", cave_floor(1100 + i)) for i in range(8)])
    rows.append([(f"cavewall_{i}", wall_tile(i)) for i in range(16)])
    rows.append([(f"cavewallfull_{i}", wall_full(1200 + i)) for i in range(4)] +
                [(f"shards_{i}", detail_shards(1210 + i)) for i in range(3)] +
                [("pebbles_c", detail_pebbles(1220)), ("crack_0", detail_crack(1221)), ("crack_1", detail_crack(1222)),
                 ("glowcap_0", detail_glowcap(1223)), ("cavepuddle_0", detail_puddle(1224))])
    return rows


# ============================================================================
# Props (Canvas, pivot) — the pivot is the foot of the prop in pixels
# ============================================================================

def crystal_cluster(color, big=True, seed=0):
    """A cluster of hexagonal crystal spires growing from a rock base, lit from the upper left."""
    rnd = random.Random(seed)
    col = C[color]
    W_, H_ = (32, 44) if big else (18, 24)
    cv = Canvas(W_, H_)
    base_y = H_ - 3
    # the rock it grows from
    rk = Canvas(W_, H_)
    shaded_ellipse(rk, W_ / 2, base_y - 1, W_ * 0.36, 3.2 if big else 2.2, C["rock"][2:], dither=0.3, bias=0.05)
    rk.outline(CAVE_OUT)
    cv.blit(rk, 0, 0)
    spires = []
    n = 5 if big else 3
    for i in range(n):
        t = (i - (n - 1) / 2) / max(1, (n - 1) / 2)
        h = (H_ - 8) * (1 - abs(t) * 0.45) * rnd.uniform(0.8, 1.0)
        w = (4.2 if big else 2.6) * rnd.uniform(0.85, 1.15)
        x0 = W_ / 2 + t * W_ * 0.26 + rnd.uniform(-1, 1)
        lean = t * (5 if big else 2.5)
        spires.append((abs(t), x0, h, w, lean))
    # the tallest ones last (in front)
    for _, x0, h, w, lean in sorted(spires, key=lambda s: -s[0]):
        sp = Canvas(W_, H_)
        bx, by = x0, base_y - 1
        tx, ty = x0 + lean, base_y - 1 - h
        pts = [(bx - w, by), (bx - w * 0.8 + lean * 0.8, ty + w * 1.2), (tx, ty), (bx + w * 0.8 + lean * 0.8, ty + w * 1.2), (bx + w, by)]
        for y in range(H_):
            for x in range(W_):
                if not point_in_poly(x + 0.5, y + 0.5, pts):
                    continue
                # facets: the left face lit, the right in shade, a bright ridge in the middle
                u = (x + 0.5 - (bx + (tx - bx) * ((by - y) / max(1.0, by - ty)))) / w
                v = (by - y) / max(1.0, h)
                if abs(u) < 0.14:
                    c = col[6] if v > 0.55 else col[5]
                elif u < 0:
                    c = col[5] if u > -0.5 else col[4]
                else:
                    c = col[3] if u < 0.55 else col[2]
                if v < 0.15:
                    c = col[max(1, col.index(c) - 2)]
                sp.px(x, y, c)
        sp.outline(mix(col[0], CAVE_OUT, 0.5))
        cv.blit(sp, 0, 0)
    # a glint
    cv.px(int(W_ / 2 - 2), int(base_y - (H_ - 8) * 0.7), col[6])
    return cv, (W_ // 2, H_ - 2)


def stalagmite(seed=0, tall=True):
    rnd = random.Random(seed)
    W_, H_ = (14, 30) if tall else (12, 18)
    cv = Canvas(W_, H_)
    r = C["rock"]
    base = H_ - 2
    h = (H_ - 4) * rnd.uniform(0.85, 1.0)
    pts = [(1.5, base), (W_ / 2 - 1.2, base - h), (W_ / 2 + 0.6, base - h - 1), (W_ - 1.5, base)]
    shaded_poly(cv, pts, r[1:], grad_dir=(0.8, 0.2), dither=0.3)
    for k in range(3):
        y = int(base - h * (0.25 + k * 0.22))
        for x in range(W_):
            if cv.opaque(x, y) and (x + k) % 3:
                cv.px(x, y, r[2])
    cv.outline(CAVE_OUT)
    return cv, (W_ // 2, H_ - 2)


def cave_rock(seed=0, big=True):
    rnd = random.Random(seed)
    W_, H_ = (24, 18) if big else (14, 11)
    cv = Canvas(W_, H_)
    r = C["rock"]
    shaded_ellipse(cv, W_ / 2, H_ / 2 + 1, W_ * 0.44, H_ * 0.4, r[1:], dither=0.35, bias=0.08)
    for _ in range(3 if big else 1):
        x, y = rnd.randrange(3, W_ - 3), rnd.randrange(3, H_ - 3)
        cv.px(x, y, r[1]); cv.px(x + 1, y + 1, r[1])
    if big and rnd.random() < 0.6:
        col = C[rnd.choice(("cyan", "pink"))]
        x = rnd.randrange(5, W_ - 6)
        for k in range(3):
            cv.px(x + k, int(H_ * 0.35) - (k % 2), col[4 + k % 2])
    cv.outline(CAVE_OUT)
    return cv, (W_ // 2, H_ - 2)


def mine_cart(seed=0, ore=True):
    """A rusty ore cart on its wheels, some crystal ore heaped inside."""
    W_, H_ = 24, 20
    cv = Canvas(W_, H_)
    ir = C["iron"]
    tb = C["timber"]
    # the wheels
    for wx in (6, 17):
        shaded_ellipse(cv, wx, H_ - 4, 2.6, 2.6, ir[:4], dither=0.2)
        cv.px(wx, H_ - 4, ir[5])
    # the body: a trapezoid, planks with iron bands
    pts = [(2, 6), (W_ - 2, 6), (W_ - 4, H_ - 5), (4, H_ - 5)]
    shaded_poly(cv, pts, tb[1:], grad_dir=(0.6, 0.8), dither=0.3)
    for y in (8, 12):
        for x in range(3, W_ - 3):
            if cv.opaque(x, y):
                cv.px(x, y, ir[2] if x % 4 else ir[4])
    for x in range(2, W_ - 1):
        cv.px(x, 6, ir[4])
    if ore:
        col = C["cyan"]
        for i in range(6):
            x = 5 + i * 2.5
            shaded_ellipse(cv, x, 5, 1.8, 1.6, col[2:], dither=0.2, bias=0.1)
        cv.px(9, 3, col[6]); cv.px(14, 4, col[6])
    cv.outline(CAVE_OUT)
    return cv, (W_ // 2, H_ - 2)


def rails(horizontal=True):
    """A short piece of mine track (a prop so it can lie anywhere)."""
    W_, H_ = (32, 12) if horizontal else (12, 32)
    cv = Canvas(W_, H_)
    tb = C["timber"]
    ir = C["iron"]
    if horizontal:
        for x in range(1, W_, 5):
            for y in range(2, H_ - 2):
                cv.px(x, y, tb[2]); cv.px(x + 1, y, tb[3])
        for x in range(W_):
            cv.px(x, 3, ir[4]); cv.px(x, 4, ir[2])
            cv.px(x, H_ - 5, ir[4]); cv.px(x, H_ - 4, ir[2])
    else:
        for y in range(1, H_, 5):
            for x in range(2, W_ - 2):
                cv.px(x, y, tb[2]); cv.px(x, y + 1, tb[3])
        for y in range(H_):
            cv.px(3, y, ir[4]); cv.px(4, y, ir[2])
            cv.px(W_ - 5, y, ir[4]); cv.px(W_ - 4, y, ir[2])
    return cv, (W_ // 2, H_ // 2)


def timber_support():
    """Two posts and a lintel propping up the rock over a mine tunnel."""
    W_, H_ = 34, 34
    cv = Canvas(W_, H_)
    tb = C["timber"]
    for x0 in (3, W_ - 8):
        shaded_poly(cv, [(x0, 6), (x0 + 5, 6), (x0 + 5, H_ - 2), (x0, H_ - 2)], tb[1:], grad_dir=(1, 0), dither=0.2)
    shaded_poly(cv, [(1, 2), (W_ - 1, 2), (W_ - 1, 8), (1, 8)], tb[1:], grad_dir=(0, 1), dither=0.2)
    for x in range(2, W_ - 1, 6):
        cv.px(x, 5, C["iron"][3])
    cv.outline(CAVE_OUT)
    return cv, (W_ // 2, H_ - 2)


def cobweb(seed=0):
    """A cobweb strung between two rocks: pale strands, half see-through."""
    rnd = random.Random(seed)
    W_, H_ = 32, 28
    cv = Canvas(W_, H_)
    wc = C["web"]
    cx, cy = W_ / 2 + rnd.uniform(-2, 2), H_ / 2
    spokes = 9
    for i in range(spokes):
        a = i / spokes * math.tau + rnd.uniform(-0.1, 0.1)
        r = rnd.uniform(11, 14)
        cv.line(cx, cy, cx + math.cos(a) * r, cy + math.sin(a) * r * 0.85, (*wc[2][:3], 150))
    for ring in (3, 6, 9, 12):
        prev = None
        for i in range(spokes + 1):
            a = i / spokes * math.tau
            p = (cx + math.cos(a) * ring, cy + math.sin(a) * ring * 0.85)
            if prev is not None:
                cv.line(prev[0], prev[1], p[0], p[1], (*wc[1][:3], 120))
            prev = p
    cv.px(int(cx), int(cy), wc[3])
    return cv, (W_ // 2, H_ - 4)


def crystal_pillar(seed=0):
    """A tall cyan crystal column of the spider queen's hall (it throws her beam back)."""
    rnd = random.Random(seed)
    W_, H_ = 24, 60
    cv = Canvas(W_, H_)
    col = C["cyan"]
    rk = Canvas(W_, H_)
    shaded_ellipse(rk, W_ / 2, H_ - 4, 10, 3.2, C["rock"][2:], dither=0.3)
    rk.outline(CAVE_OUT)
    cv.blit(rk, 0, 0)
    pts = [(4, H_ - 4), (5, 10), (W_ / 2, 2), (W_ - 5, 10), (W_ - 4, H_ - 4)]
    for y in range(H_):
        for x in range(W_):
            if not point_in_poly(x + 0.5, y + 0.5, pts):
                continue
            u = (x + 0.5 - W_ / 2) / 8
            c = col[6] if abs(u) < 0.12 else col[5] if u < -0.45 else col[4] if u < 0 else col[3] if u < 0.5 else col[2]
            if (y + int(x * 0.5)) % 9 == 0:
                c = col[max(2, col.index(c) - 1)]
            cv.px(x, y, c)
    cv.outline(mix(col[0], CAVE_OUT, 0.5))
    for k in range(4):
        cv.px(8 + k % 2, 14 + k * 9, col[6])
    return cv, (W_ // 2, H_ - 3)


def ore_vein():
    """A rock with a fat seam of amber crystal (the miners' prize)."""
    cv, piv = cave_rock(seed=77, big=True)
    col = C["amber"]
    for i in range(5):
        x, y = 7 + i * 2, 7 - (i % 2)
        cv.px(x, y, col[5]); cv.px(x + 1, y, col[4]); cv.px(x, y + 1, col[3])
    return cv, piv


def props():
    items = []
    for i, color in enumerate(("cyan", "pink", "amber")):
        items.append((f"crystal_big_{color}", crystal_cluster(color, True, 300 + i)))
        items.append((f"crystal_small_{color}", crystal_cluster(color, False, 310 + i)))
    items += [("stalagmite_0", stalagmite(1, True)), ("stalagmite_1", stalagmite(2, False)),
              ("caverock_big", cave_rock(3, True)), ("caverock_small", cave_rock(4, False)),
              ("minecart", mine_cart(5, True)), ("minecart_empty", mine_cart(6, False)),
              ("rails_h", rails(True)), ("rails_v", rails(False)), ("timber", timber_support()),
              ("cobweb_0", cobweb(7)), ("cobweb_1", cobweb(8)), ("crystal_pillar", crystal_pillar(9)), ("orevein", ore_vein())]
    # (name, Canvas, pivot) like the other prop generators
    return [(name, cv, piv) for name, (cv, piv) in items]


# ============================================================================
# Item icons (16x16)
# ============================================================================

ICON_OUT = hx("#0a0a12")


def _icon():
    return Canvas(16, 16)


def _done(cv):
    cv.outline(ICON_OUT)
    return cv


def icon_crystal_shard():
    """Mảnh Pha Lê: a single clear cyan splinter."""
    cv = _icon()
    col = C["cyan"]
    pts = [(5, 14), (4, 7), (8, 1), (12, 6), (11, 14)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                u = (x + 0.5 - 8) / 4
                cv.px(x, y, col[6] if abs(u) < 0.15 else col[5] if u < 0 else col[3])
    cv.px(7, 4, col[6])
    return _done(cv)


def icon_bat_wing():
    """Cánh Dơi Pha Lê: a dark wing, crystal studs along its edge."""
    cv = _icon()
    m = ramp("#1a0c26", "#2a143c", "#3a1e52", "#4c2a68", "#664296")
    pts = [(2, 12), (6, 3), (14, 2), (12, 7), (14, 10), (9, 11), (8, 14)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                cv.px(x, y, m[1 + (x + y) % 3 // 2 + (1 if y < 6 else 0)])
    cv.line(2, 12, 14, 2, m[4])
    cv.line(6, 8, 14, 10, m[3])
    for (x, y) in ((14, 2), (13, 7), (14, 10)):
        cv.px(x, y, C["cyan"][5])
    return _done(cv)


def icon_spider_silk():
    """Tơ Nhện Hang: a wound skein of pale thread."""
    cv = _icon()
    w = C["web"]
    shaded_ellipse(cv, 8, 9, 5.2, 4.6, [hx("#6a7080"), w[0], w[1], w[2], w[3]], dither=0.2, bias=0.1)
    for k in range(-4, 5, 2):
        cv.line(8 + k - 2, 5, 8 + k + 2, 13, w[0])
    cv.px(3, 4, w[2]); cv.px(2, 3, w[1]); cv.px(1, 3, w[1])
    return _done(cv)


def icon_golem_core():
    """Lõi Golem: a rough stone heart with a glowing cyan eye."""
    cv = _icon()
    st = ramp("#1f222c", "#2a2e3a", "#373c4b", "#474d5f", "#5a6176")
    shaded_ellipse(cv, 8, 8.5, 5.8, 5.6, st, dither=0.35, bias=0.05)
    for (x, y) in ((6, 8), (7, 8), (8, 8), (9, 8)):
        cv.px(x, y, C["cyan"][4] if 7 <= x <= 8 else C["cyan"][2])
    cv.px(7, 7, C["cyan"][6])
    return _done(cv)


def icons():
    return [("crystal_shard", icon_crystal_shard()), ("bat_wing", icon_bat_wing()), ("spider_silk", icon_spider_silk()),
            ("golem_core", icon_golem_core())]


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    rows = tiles()
    sheet = Canvas(16 * 16, len(rows) * 16)
    for r, row in enumerate(rows):
        for c, (name, tile) in enumerate(row):
            sheet.blit(tile, c * 16, r * 16)
    preview(sheet.to_image(), 4, os.path.join(out, "cave_tiles.png"))
    frames = [cv for _, cv, _ in props()]
    preview(pack_grid(frames, 7).to_image(), 3, os.path.join(out, "cave_props.png"))
    print("ok")
