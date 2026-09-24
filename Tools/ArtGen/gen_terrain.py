"""
Terrain tiles (16x16) for the ground tilemaps.

Layers used in Unity:
  Ground     : full grass tiles (random variants)
  TallGrass  : marching-squares overlay (dark, dense grass with a bordered edge)
  Dirt       : marching-squares overlay (paths and clearings)
  Details    : small decorations (tufts, flowers, leaves, pebbles)

Marching-squares index = TL*8 + TR*4 + BR*2 + BL*1 (corner terrain flags).
"""
import math
import random

import numpy as np

from pixelkit import Canvas, P, TileNoise, OUTLINE, hx, mix, bayer

T = 16

noise_lo = TileNoise(T, 2, seed=11)
noise_hi = TileNoise(T, 4, seed=23)
noise_hi2 = TileNoise(T, 8, seed=37)


def field(px, py):
    return (noise_lo(px + 0.5, py + 0.5) - 0.5) * 0.9 + (noise_hi(px + 0.5, py + 0.5) - 0.5) * 0.45


def ms_mask(idx: int, amp: float, thresh: float = 0.5, seed_shift=0.0) -> np.ndarray:
    TL = (idx >> 3) & 1
    TR = (idx >> 2) & 1
    BR = (idx >> 1) & 1
    BL = idx & 1
    m = np.zeros((T, T), bool)
    for py in range(T):
        for px in range(T):
            u = (px + 0.5) / T
            v = (py + 0.5) / T
            f = TL * (1 - u) * (1 - v) + TR * u * (1 - v) + BL * (1 - u) * v + BR * u * v
            if f >= 0.999:
                m[py, px] = True
                continue
            if f <= 0.001:
                continue
            m[py, px] = f + field(px + seed_shift, py) * amp > thresh
    # remove single-pixel noise (isolated pixels / holes)
    m2 = m.copy()
    for py in range(T):
        for px in range(T):
            nb = 0
            for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                x, y = px + dx, py + dy
                if 0 <= x < T and 0 <= y < T:
                    nb += m[y, x]
                else:
                    nb += m[py, px]  # treat outside as same (keeps edges seamless)
            if m[py, px] and nb <= 1:
                m2[py, px] = False
            if not m[py, px] and nb >= 4:
                m2[py, px] = True
    return m2


def edge_dist(m: np.ndarray, idx: int):
    """Distance (0,1,2..) from the terrain border, 99 when far.
    Outside the tile we assume the neighbour follows the corner flags,
    approximated by the same mask continuing (seamless enough)."""
    d = np.full(m.shape, 99, np.int32)
    h, w = m.shape
    for py in range(h):
        for px in range(w):
            if not m[py, px]:
                continue
            best = 99
            for r in range(1, 4):
                hit = False
                for dx in range(-r, r + 1):
                    for dy in range(-r, r + 1):
                        if max(abs(dx), abs(dy)) != r:
                            continue
                        x, y = px + dx, py + dy
                        if 0 <= x < w and 0 <= y < h:
                            if not m[y, x]:
                                hit = True
                        # outside tile -> unknown; ignore
                if hit:
                    best = r
                    break
            d[py, px] = best
    return d


# ----------------------------------------------------------------------------
# Textures
# ----------------------------------------------------------------------------


def grass_texture(cv: Canvas, seed: int, mask=None, flowers=True):
    g = P["grass"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if mask is not None and not mask[y, x]:
                continue
            n = noise_hi2(x + seed * 3, y) * 0.6 + noise_lo(x, y + seed) * 0.4
            c = g[3] if n > 0.42 else g[2]
            if n > 0.78:
                c = g[4]
            cv.px(x, y, c)
    # blades: little 'v' and '|' marks
    for _ in range(9):
        x, y = rnd.randrange(T), rnd.randrange(1, T)
        if mask is not None and not mask[y, x]:
            continue
        cv.px(x, y, g[1])
        if rnd.random() < 0.7 and x + 1 < T and (mask is None or mask[y, x]):
            cv.px(x, y - 1, g[4])
    for _ in range(5):
        x, y = rnd.randrange(T - 2), rnd.randrange(1, T)
        if mask is not None and not (mask[y, x] and mask[y, x + 2]):
            continue
        # V blade
        cv.px(x, y - 1, g[4])
        cv.px(x + 1, y, g[1])
        cv.px(x + 2, y - 1, g[4])
    if flowers and rnd.random() < 0.12:
        x, y = rnd.randrange(1, T - 1), rnd.randrange(1, T - 1)
        if mask is None or mask[y, x]:
            fc = rnd.choice([hx("#e8e0f0"), hx("#c89af0"), hx("#ffe07a")])
            cv.px(x, y, fc)
            cv.px(x, y + 1, g[1])


def tallgrass_texture(cv: Canvas, seed: int, mask):
    d = P["dgrass"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if not mask[y, x]:
                continue
            n = noise_hi2(x + 5, y + seed) * 0.7 + noise_hi(x, y) * 0.3
            c = d[2] if n > 0.45 else d[1]
            cv.px(x, y, c)
    # dense blade tips: pattern of light 'ʌ' shapes on a staggered grid
    for gy in range(0, T, 4):
        for gx in range(0, T, 4):
            ox = gx + ((gy // 4) % 2) * 2 + rnd.choice([0, 0, 1])
            oy = gy + rnd.choice([1, 2])
            pts = [(ox, oy + 1), (ox + 1, oy), (ox + 2, oy + 1)]
            for (x, y) in pts:
                if 0 <= x < T and 0 <= y < T and mask[y, x]:
                    cv.px(x, y, d[4] if (x + y + seed) % 3 else d[3])
            x, y = ox + 1, oy + 1
            if 0 <= x < T and 0 <= y < T and mask[y, x]:
                cv.px(x, y, d[0])


def dirt_texture(cv: Canvas, seed: int, mask):
    d = P["dirt"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if not mask[y, x]:
                continue
            n = noise_lo(x + seed * 5, y + seed) * 0.7 + noise_hi(x + 3, y + seed * 2) * 0.3
            c = d[3]
            if n < 0.36:
                c = d[2]
            elif n > 0.74:
                c = d[4]
            r = rnd.random()
            if r < 0.06:
                c = d[2]
            elif r < 0.09:
                c = d[4]
            cv.px(x, y, c)
    # pebbles: light top, dark shadow below
    for _ in range(rnd.randint(1, 3)):
        x, y = rnd.randrange(1, T - 2), rnd.randrange(1, T - 2)
        if mask[y, x] and mask[y, x + 1] and mask[y + 1, x] and mask[y + 1, x + 1]:
            w = rnd.choice([1, 2])
            for dx in range(w):
                cv.px(x + dx, y, d[5])
                cv.px(x + dx, y + 1, d[1])
    # tiny dark specks
    for _ in range(rnd.randint(2, 4)):
        x, y = rnd.randrange(T), rnd.randrange(T)
        if mask[y, x]:
            cv.px(x, y, d[1])

# ----------------------------------------------------------------------------
# Tile builders
# ----------------------------------------------------------------------------


def grass_full(seed):
    cv = Canvas(T, T)
    grass_texture(cv, seed)
    return cv


def tallgrass_tile(idx, seed=0):
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.55)
    tallgrass_texture(cv, seed + idx, m)
    d = P["dgrass"]
    ed = edge_dist(m, idx)
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            # the edge: dark rim, with light blade tips just inside
            if ed[y, x] == 1:
                # border against lower pixels is darker (fake height)
                below_open = y + 1 < T and not m[y + 1, x]
                cv.px(x, y, OUTLINE if below_open else d[0])
            elif ed[y, x] == 2 and (x + y) % 2 == 0:
                cv.px(x, y, d[4])
    return cv


def dirt_tile(idx, seed=0):
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.38, seed_shift=7)
    dirt_texture(cv, seed + idx * 3, m)
    d = P["dirt"]
    g = P["grass"]
    ed = edge_dist(m, idx)
    rnd = random.Random(idx * 91 + seed)
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            if ed[y, x] == 1:
                up_open = y - 1 >= 0 and not m[y - 1, x]
                # top edges get a shadow (grass lip casts a shadow on the path)
                cv.px(x, y, d[0] if up_open else d[1])
            elif ed[y, x] == 2:
                up_open2 = y - 2 >= 0 and not m[y - 2, x]
                if up_open2:
                    cv.px(x, y, d[2])
    # a few grass blades poking over the dirt edge
    for y in range(T):
        for x in range(T):
            if m[y, x] and ed[y, x] == 1 and rnd.random() < 0.18:
                cv.px(x, y, g[2])
    return cv


def dirt_full(seed):
    m = np.ones((T, T), bool)
    cv = Canvas(T, T)
    dirt_texture(cv, seed, m)
    return cv


def tallgrass_full(seed):
    m = np.ones((T, T), bool)
    cv = Canvas(T, T)
    tallgrass_texture(cv, seed, m)
    return cv


# ----------------------------------------------------------------------------
# Details
# ----------------------------------------------------------------------------


def detail_tuft(seed):
    cv = Canvas(T, T)
    g = P["grass"]
    rnd = random.Random(seed)
    cx = rnd.randint(5, 10)
    by = rnd.randint(10, 13)
    blades = [(-3, 3), (-2, 5), (-1, 6), (0, 7), (1, 5), (2, 6), (3, 3)]
    for i, (dx, hgt) in enumerate(blades):
        hgt = max(2, hgt - rnd.randint(0, 2))
        lean = rnd.choice([-1, 0, 0, 1]) if abs(dx) > 1 else 0
        for k in range(hgt):
            x = cx + dx + (lean if k > hgt // 2 else 0)
            y = by - k
            c = g[1] if k < 2 else (g[3] if k < hgt - 1 else g[5])
            cv.px(x, y, c)
    cv.outline(hx("#1b3320"))
    return cv


def detail_flowers(seed, petal, center):
    cv = Canvas(T, T)
    g = P["grass"]
    rnd = random.Random(seed)
    spots = [(4, 6), (9, 4), (11, 10), (5, 11), (8, 9)]
    rnd.shuffle(spots)
    for (x, y) in spots[: rnd.randint(3, 5)]:
        cv.px(x, y + 2, g[1])
        cv.px(x, y + 1, g[2])
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            cv.px(x + dx, y + dy, petal)
        cv.px(x, y, center)
    return cv


def detail_leaves(seed):
    cv = Canvas(T, T)
    a = P["autumn"]
    rnd = random.Random(seed)
    for _ in range(rnd.randint(3, 5)):
        x, y = rnd.randrange(2, T - 3), rnd.randrange(2, T - 3)
        c1, c2 = rnd.choice([(a[2], a[3]), (a[1], a[2]), (a[3], a[4])])
        cv.px(x, y, c2)
        cv.px(x + 1, y, c1)
        cv.px(x + 1, y + 1, c2)
        cv.px(x + 2, y + 1, c1)
    return cv


def detail_pebbles(seed):
    cv = Canvas(T, T)
    s = P["stone"]
    rnd = random.Random(seed)
    for _ in range(rnd.randint(2, 4)):
        x, y = rnd.randrange(2, T - 3), rnd.randrange(2, T - 3)
        w = rnd.choice([1, 2])
        for dx in range(w + 1):
            cv.px(x + dx, y, s[4])
            cv.px(x + dx, y + 1, s[2])
        cv.px(x, y, s[5])
    cv.outline(hx("#1f2a22"))
    return cv


def detail_clover(seed):
    cv = Canvas(T, T)
    g = P["grass"]
    rnd = random.Random(seed)
    for _ in range(3):
        x, y = rnd.randrange(3, T - 3), rnd.randrange(3, T - 3)
        for dx, dy in [(0, -1), (-1, 0), (1, 0)]:
            cv.px(x + dx, y + dy, g[5])
        cv.px(x, y, g[4])
        cv.px(x, y + 1, g[1])
    return cv


def detail_mushrooms(seed):
    cv = Canvas(T, T)
    r = P["shroom_red"]
    c = P["cream"]
    for (x, y) in [(5, 9), (9, 11)]:
        cv.px(x, y + 1, c[2])
        cv.px(x, y + 2, c[1])
        for dx in (-1, 0, 1):
            cv.px(x + dx, y, r[3])
        cv.px(x, y - 1, r[3])
        cv.px(x - 1, y, r[2])
        cv.px(x + 1, y - 1 + 1, r[2])
        cv.px(x, y - 1, r[4])
        cv.px(x + 1, y, (255, 240, 230, 255))
    cv.outline(hx("#2a1418"))
    return cv


# ----------------------------------------------------------------------------
# Sheet
# ----------------------------------------------------------------------------


def build():
    """Returns (sheet Canvas, list of (name, x, y, w, h))."""
    cols = 16
    rows_def = []
    # row 0: base grass variants
    rows_def.append([(f"grass_{i}", grass_full(100 + i)) for i in range(8)])
    # row 1: tall grass marching squares
    rows_def.append([(f"tall_{i}", tallgrass_tile(i)) for i in range(16)])
    # row 2: tall grass full variants
    rows_def.append([(f"tallfull_{i}", tallgrass_full(200 + i)) for i in range(4)])
    # row 3: dirt marching squares
    rows_def.append([(f"dirt_{i}", dirt_tile(i)) for i in range(16)])
    # row 4: dirt full variants
    rows_def.append([(f"dirtfull_{i}", dirt_full(300 + i)) for i in range(4)])
    # row 5: details
    det = []
    det += [(f"tuft_{i}", detail_tuft(400 + i)) for i in range(3)]
    det += [("flowers_0", detail_flowers(1, hx("#c89af0"), hx("#ffe07a"))),
            ("flowers_1", detail_flowers(2, hx("#f4f0ff"), hx("#ffcc40"))),
            ("flowers_2", detail_flowers(3, hx("#ffd24a"), hx("#c86a20")))]
    det += [(f"leaves_{i}", detail_leaves(500 + i)) for i in range(2)]
    det += [(f"pebbles_{i}", detail_pebbles(600 + i)) for i in range(2)]
    det += [("clover_0", detail_clover(7)), ("mushrooms_0", detail_mushrooms(8))]
    rows_def.append(det)

    sheet = Canvas(cols * T, 8 * T)
    rects = []
    for r, row in enumerate(rows_def):
        for c, (name, tile) in enumerate(row):
            sheet.blit(tile, c * T, r * T)
            rects.append((name, c * T, r * T, T, T))
    return sheet, rects


if __name__ == "__main__":
    s, rects = build()
    s.save("terrain_test.png")
