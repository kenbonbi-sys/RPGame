"""
Thảo Nguyên Gió (the windy steppe north-west of the forest, reached through the crystal cave):
terrain tiles, props and item icons.

Tiles (16x16):
  Ground : steppe_i            golden-green grass, dry straw blades (random variants)
  Chasm  : chasm_i / chasmfull_i   marching squares: the ravine (Khe Vực) seen from above — a lit
           lip where the grass ends, the far cliff face falling into the dark, a void below
           (its own tilemap: solid for walkers, open for shots)
  Details: straw_i, stones_s, flowers_s_i, skull_s
Props return (Canvas, pivot) like gen_props: tall grass tufts (they sway with the wind in Unity),
an acacia, sandstone rocks, a cairn with prayer flags, nomad yurts, a wind column's stone ring,
and the Hắc Phong's banner; for T62 the straw scarecrow of the abandoned fields (the same pixels
as a Bù Nhìn Sống standing still), a haystack, the bandits' black tents and stake fence, and the
windmill on their chief's hill (the tower, and its sails a quarter turn apart in four frames).
"""
import math
import random

import numpy as np

from pixelkit import Canvas, P, OUTLINE, TileNoise, hx, ramp, mix, shaded_ellipse, shade_index, shaded_poly, point_in_poly
from gen_terrain import ms_mask, edge_dist
import gen_props

T = 16

C = {
    "grass": ramp("#4c5424", "#606a2a", "#788236", "#909a44", "#a8ae56", "#c2c26c"),
    "straw": ramp("#7a6630", "#9a8440", "#b89c50", "#d2b664", "#ead084"),
    "void": ramp("#05060a", "#090b12", "#0e111c", "#141828"),
    "cliff": ramp("#241a14", "#34261c", "#463424", "#5a4430", "#6e563e", "#86704e"),
    "sand": ramp("#3a2c20", "#58422e", "#76593c", "#94744c", "#b0905e", "#caac78", "#e2c894"),
    "felt": ramp("#6e6456", "#968a78", "#bcb29c", "#dcd4c0", "#f2ecdc"),
    "red": P["red"],
    "blue": P["blue"],
    "wood": P["wood"],
    "acacia": ramp("#1e2c14", "#2c3e1a", "#3e5422", "#546c2c", "#6c8638", "#86a046"),
    "bark": ramp("#2a1c14", "#3e2a1c", "#543a26", "#6c4c32"),
    "wind": ramp("#6a8a9a", "#9ab8c4", "#c8e0e8", "#f0fbff"),
}
STEPPE_OUT = hx("#1a140c")

n_lo = TileNoise(T, 2, seed=271)
n_hi = TileNoise(T, 4, seed=273)
n_hi2 = TileNoise(T, 8, seed=279)


# ============================================================================
# Terrain
# ============================================================================

def steppe_ground(seed):
    """Short sun-bleached grass: a warm green, patches of straw, little vertical blades."""
    cv = Canvas(T, T)
    g = C["grass"]
    s = C["straw"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            n = n_lo(x + seed * 5, y + seed) * 0.55 + n_hi(x + seed, y + seed * 2) * 0.45
            c = g[3] if n > 0.5 else g[2]
            if n > 0.72:
                c = s[2]
            elif n < 0.28:
                c = g[1]
            cv.px(x, y, c)
    # blades: a light tip over a dark root
    for _ in range(rnd.randint(7, 11)):
        x, y = rnd.randrange(T), rnd.randrange(1, T)
        straw = rnd.random() < 0.4
        cv.px(x, y, (s if straw else g)[4])
        cv.px(x, y - 1, (s if straw else g)[3])
        if y + 1 < T:
            cv.px(x, y + 1, g[1])
    return cv


FACE_H = 7


def chasm_tile(idx, seed=0):
    """The ravine (marching squares): inside the mask the void; along its top edge (where the
    grass is north of it) the far cliff face falls away in strata, lit at the rim; along its
    bottom edge a dark overhang line where the near rim hides the drop."""
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.3, seed_shift=57)
    v = C["void"]
    fc = C["cliff"]
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            # how far below the top rim this pixel is (the cliff face shows there); a rim in the tile
            # above is too far up for its face to reach here: the void
            depth = 0
            for k in range(1, FACE_H + 1):
                if y - k < 0:
                    depth = -1
                    break
                if not m[y - k, x]:
                    depth = k
                    break
            if depth <= 0:
                n = n_hi2(x + seed, y) * 0.5 + n_lo(x, y + seed) * 0.5
                cv.px(x, y, v[1] if n > 0.55 else v[0])
                if (x * 7 + y * 3 + idx) % 29 == 0:
                    cv.px(x, y, v[2])
                continue
            level = 5 - (depth * 5) // FACE_H
            crack = (x * 5 + idx * 3) % 7 == 0
            if crack and depth > 1:
                level -= 2
            elif (x * 3 + depth * 5 + idx) % 11 == 0:
                level -= 1
            cv.px(x, y, fc[max(0, min(len(fc) - 1, level))])
            if depth == 1:
                cv.px(x, y, C["grass"][4] if (x + idx) % 3 else C["straw"][3])   # the grass lip
    # the near rim: a dark line where the ground stops at the bottom of the void
    for x in range(T):
        for y in range(T - 1, -1, -1):
            if m[y, x] and y + 1 < T and not m[y + 1, x]:
                cv.px(x, y, STEPPE_OUT)
                cv.px(x, y + 1, C["grass"][0])
                break
    return cv


def chasm_full(seed):
    cv = Canvas(T, T)
    v = C["void"]
    for y in range(T):
        for x in range(T):
            n = n_hi2(x + seed, y) * 0.5 + n_lo(x, y + seed) * 0.5
            cv.px(x, y, v[1] if n > 0.55 else v[0])
            if (x * 7 + y * 3 + seed) % 31 == 0:
                cv.px(x, y, v[2])
    return cv


def detail_straw(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    s = C["straw"]
    for _ in range(4):
        x, y = rnd.randrange(3, T - 3), rnd.randrange(6, T - 2)
        for k in range(rnd.randint(3, 5)):
            cv.px(x + (k % 2) * rnd.choice((-1, 1)), y - k, s[4 - min(3, k)])
    return cv


def detail_stones(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    sd = C["sand"]
    for _ in range(rnd.randint(3, 5)):
        x, y = rnd.randrange(2, T - 2), rnd.randrange(2, T - 2)
        cv.px(x, y, sd[4])
        cv.px(x + 1, y, sd[3])
        cv.px(x, y + 1, sd[1])
    return cv


def detail_flowers(seed, col):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    for _ in range(rnd.randint(3, 5)):
        x, y = rnd.randrange(2, T - 2), rnd.randrange(3, T - 1)
        cv.px(x, y, col[4])
        cv.px(x, y + 1, C["grass"][1])
    return cv


def detail_skull(seed):
    cv = Canvas(T, T)
    b = P["bone"]
    cx, cy = 8, 9
    shaded_ellipse(cv, cx, cy, 3.2, 2.4, b[1:], dither=0.1)
    cv.px(cx - 1, cy, OUTLINE)
    cv.px(cx + 1, cy, OUTLINE)
    # horns of a long-dead bison
    cv.line(cx - 3, cy - 1, cx - 6, cy - 3, b[2])
    cv.line(cx + 3, cy - 1, cx + 6, cy - 3, b[2])
    return cv


def tiles():
    """Rows of (name, Canvas) for the terrain sheet."""
    rows = []
    rows.append([(f"steppe_{i}", steppe_ground(1500 + i)) for i in range(8)])
    rows.append([(f"chasm_{i}", chasm_tile(i, 1550)) for i in range(16)])
    rows.append([(f"chasmfull_{i}", chasm_full(1600 + i)) for i in range(4)] +
                [(f"straw_{i}", detail_straw(1610 + i)) for i in range(3)] +
                [("stones_s", detail_stones(1620)), ("flowers_s_0", detail_flowers(1621, P["gold"])),
                 ("flowers_s_1", detail_flowers(1622, P["purple"])), ("skull_s", detail_skull(1623))])
    return rows


# ============================================================================
# Props (Canvas, pivot) — the pivot is the foot of the prop in pixels
# ============================================================================

def grass_tuft(seed=0, tall=True):
    """A clump of tall golden grass; its tips sway with the wind in the game (a shader)."""
    W, H = (16, 22) if tall else (12, 14)
    cv = Canvas(W, H)
    rnd = random.Random(seed)
    s = C["straw"]
    g = C["grass"]
    blades = rnd.randint(9, 13) if tall else rnd.randint(6, 8)
    for i in range(blades):
        x0 = W / 2 + rnd.uniform(-W * 0.28, W * 0.28)
        h = rnd.uniform(H * 0.55, H - 2)
        lean = rnd.uniform(-2.5, 2.5)
        ramp_ = s if rnd.random() < 0.65 else g
        steps = int(h)
        for k in range(steps):
            t = k / max(1, steps - 1)
            x = x0 + lean * t * t
            y = H - 1 - k
            idx = 1 + int(t * (len(ramp_) - 2))
            cv.px(int(x), int(y), ramp_[min(len(ramp_) - 1, idx)])
        # a seed head on some
        if tall and rnd.random() < 0.5:
            x = int(x0 + lean)
            cv.px(x, int(H - steps), s[4])
            cv.px(x, int(H - steps + 1), s[3])
    cv.outline(STEPPE_OUT)
    return cv, (W // 2, H - 1)


def acacia(seed=0):
    """A lone flat-topped steppe tree: a thin crooked trunk, a wide umbrella crown."""
    W, H = 52, 46
    cv = Canvas(W, H)
    rnd = random.Random(seed)
    b = C["bark"]
    cx = W // 2
    # trunk forking in two
    tr = Canvas(W, H)
    tr.line(cx, H - 2, cx - 1, H - 14, b[2], 3)
    tr.line(cx - 1, H - 14, cx - 7, H - 24, b[2], 2)
    tr.line(cx - 1, H - 14, cx + 6, H - 25, b[1], 2)
    tr.line(cx - 7, H - 24, cx - 13, H - 28, b[1], 1)
    tr.line(cx + 6, H - 25, cx + 12, H - 29, b[1], 1)
    for y in range(H - 14, H - 2):
        tr.px(cx - 1, y, b[3])
    tr.outline(STEPPE_OUT)
    cv.blit(tr, 0, 0)
    # the crown: overlapping flat ellipses, lit from above
    cr = Canvas(W, H)
    for (dx, dy, rx, ry) in ((-10, -31, 12, 5), (9, -32, 13, 5), (0, -34, 15, 5.5), (-4, -30, 10, 4), (5, -30, 10, 4)):
        shaded_ellipse(cr, cx + dx, H + dy, rx, ry, C["acacia"], dither=0.35, bias=0.05)
    # leaf clusters on top
    for _ in range(40):
        x, y = rnd.randrange(cx - 22, cx + 22), rnd.randrange(H - 40, H - 30)
        if cr.opaque(x, y):
            cr.px(x, y, C["acacia"][5] if rnd.random() < 0.5 else C["acacia"][4])
    cr.outline(STEPPE_OUT)
    cv.blit(cr, 0, 0)
    return cv, (cx, H - 2)


def sand_rock(seed=0, big=True):
    """A weathered sandstone boulder (a charging bison stuns itself on one)."""
    W, H = (30, 24) if big else (16, 12)
    cv, piv = gen_props.faceted_rock(W, H, seed, moss=False, cracks=2 if big else 0)
    st = P["stone"]
    sd = C["sand"]
    for i in range(len(st)):
        cv.recolor(tuple(st[i]), tuple(sd[min(len(sd) - 1, i + 1)]))
    return cv, piv


def cairn(seed=0):
    """An ovoo: a heap of stones with a pole, strings of coloured prayer flags stretched from it."""
    W, H = 34, 40
    cv = Canvas(W, H)
    rnd = random.Random(seed)
    cx = W // 2
    st = Canvas(W, H)
    for (dx, dy, r) in ((-6, -4, 4.5), (6, -4, 4.5), (0, -5, 5.5), (-3, -10, 4), (3, -10, 4), (0, -14, 3.5)):
        shaded_ellipse(st, cx + dx, H + dy, r, r * 0.8, C["sand"][1:], dither=0.3)
    st.outline(STEPPE_OUT)
    cv.blit(st, 0, 0)
    # the pole
    for y in range(H - 34, H - 15):
        cv.px(cx, y, C["wood"][3])
        cv.px(cx + 1, y, C["wood"][1])
    cv.px(cx, H - 35, P["gold"][4])
    # flags on two strings, in the colours of the sky, clouds, fire, water and earth
    colours = [C["blue"][3], P["white"][2], C["red"][3], P["green"][3], P["gold"][4]]
    for side in (-1, 1):
        for k in range(5):
            t = (k + 1) / 6
            x = cx + side * t * 15
            y = H - 33 + t * 16 + math.sin(t * 3) * 1.5
            col = colours[(k + (side > 0)) % 5]
            cv.px(int(x), int(y), col)
            cv.px(int(x), int(y) + 1, col)
            cv.px(int(x) + side, int(y), col)
            # the string
            if k < 4:
                cv.px(int(x + side * 1.5), int(y + 1.4), P["cream"][1])
    return cv, (cx, H - 2)


def yurt(seed=0):
    """A nomads' ger: white felt walls, a low domed roof with a smoke ring, a painted red door."""
    W, H = 46, 38
    cv = Canvas(W, H)
    f = C["felt"]
    cx = W // 2
    # the wall: a short cylinder
    for y in range(H - 16, H - 2):
        for x in range(4, W - 4):
            t = (x - 4) / (W - 8)
            inten = 0.55 - t * 1.3
            cv.px(x, y, f[shade_index(inten, len(f), x, y, 0.25)])
    # lattice lines on the wall
    for x in range(6, W - 5, 4):
        for y in range(H - 15, H - 3):
            if (y + x) % 4 == 0 and cv.opaque(x, y):
                cv.px(x, y, f[1])
    # the roof: a flattened dome
    shaded_ellipse(cv, cx, H - 17, W / 2 - 3, 9, f[1:], dither=0.3, bias=0.08)
    # the crown ring and a wisp of smoke
    shaded_ellipse(cv, cx, H - 25, 4, 2, C["wood"][1:], dither=0.1)
    # bands around the roof
    for x in range(6, W - 6):
        y = int(H - 16 + 0.0)
        if cv.opaque(x, y):
            cv.px(x, y, C["red"][2])
    # the door
    for y in range(H - 12, H - 2):
        for x in range(cx - 3, cx + 4):
            cv.px(x, y, C["red"][2] if (x + y) % 5 else C["red"][3])
    for y in range(H - 12, H - 2):
        cv.px(cx - 4, y, C["wood"][2])
        cv.px(cx + 4, y, C["wood"][2])
    cv.hline(cx - 4, cx + 4, H - 13, P["gold"][3])
    cv.outline(STEPPE_OUT)
    return cv, (cx, H - 2)


def wind_ring(seed=0):
    """Where a wind column rises: a ring of standing stones carved with spirals, dust at its feet."""
    W, H = 38, 24
    cv = Canvas(W, H)
    cx, cy = W / 2, H / 2 + 3
    sd = C["sand"]
    # the swept floor
    shaded_ellipse(cv, cx, cy, 16, 7, [C["grass"][1], C["straw"][1], C["straw"][2]], dither=0.4, bias=-0.1)
    # standing stones around it, the near ones lower in the picture
    for k in range(8):
        a = k * math.tau / 8 + 0.2
        x, y = cx + math.cos(a) * 15, cy + math.sin(a) * 6.5
        h = 6 if math.sin(a) > 0 else 5
        st = Canvas(W, H)
        shaded_poly(st, [(x - 1.5, y), (x - 1.2, y - h), (x + 1.2, y - h - 0.5), (x + 1.5, y)], sd[2:], grad_dir=(0.6, 1.0), dither=0.2)
        st.outline(STEPPE_OUT)
        cv.blit(st, 0, 0)
        cv.px(int(x), int(y - h + 2), C["wind"][2])   # a carved spiral catches the light
    # spiral grooves in the floor
    for i in range(24):
        t = i / 24
        a = t * math.tau * 1.5
        r = 2 + t * 10
        cv.px(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r * 0.42), C["wind"][1])
    return cv, (int(cx), int(cy + 7))


def banner(seed=0):
    """The Hắc Phong's black banner on a spear: a white whirlwind painted on it."""
    W, H = 20, 40
    cv = Canvas(W, H)
    x0 = 4
    for y in range(3, H - 1):
        cv.px(x0, y, C["wood"][2])
        cv.px(x0 + 1, y, C["wood"][1])
    cv.px(x0, 2, P["metal"][4])
    cv.px(x0, 1, P["metal"][5])
    bk = ramp("#0a0a0e", "#16161e", "#22222c", "#30303c")
    for y in range(5, 22):
        for x in range(x0 + 2, x0 + 14):
            wave = int(math.sin(y * 0.5) * 1.2)
            if x < x0 + 14 + wave:
                cv.px(x, y, bk[1 + (x + y) % 2])
    # a whirlwind
    for i in range(14):
        t = i / 14
        a = t * math.tau * 1.4
        r = 1 + t * 4
        cv.px(int(x0 + 8 + math.cos(a) * r), int(13 + math.sin(a) * r), P["white"][3])
    cv.outline(STEPPE_OUT)
    return cv, (x0, H - 1)


def scarecrow_prop(seed=0):
    """The straw scarecrow: pixel for pixel the Bù Nhìn Sống standing still (they must look alike)."""
    import gen_steppe_creatures
    cv = gen_steppe_creatures.scarecrow_still()
    return cv, (15, cv.h - 2)


def haystack(seed=0):
    """A haystack left in the field: a dome of straw, a few loose wisps."""
    W, H = 26, 18
    cv = Canvas(W, H)
    rnd = random.Random(seed)
    shaded_ellipse(cv, W / 2, H - 7, 11, 7, C["straw"], dither=0.35, bias=0.05)
    for k in range(18):
        x = rnd.uniform(3, W - 3)
        y = rnd.uniform(4, H - 4)
        if cv.opaque(int(x), int(y)):
            cv.line(x, y, x + rnd.choice((-1, 1)), y + 2, C["straw"][rnd.randint(0, 1)])
    cv.hline(4, W - 5, H - 2, C["straw"][1])
    cv.outline(STEPPE_OUT)
    return cv, (W // 2, H - 2)


def black_tent(seed=0):
    """A Hắc Phong tent: black hides on poles, a peak with a whirlwind painted on the flap, guy ropes."""
    W, H = 40, 36
    cv = Canvas(W, H)
    hide = ramp("#0e0e12", "#18181e", "#24242c", "#32323c", "#44444e")
    cx = W // 2
    # the ropes and pegs
    for side in (-1, 1):
        cv.line(cx + side * 4, 6, cx + side * 18, H - 3, C["wood"][2])
        cv.px(cx + side * 18, H - 2, C["wood"][1])
    # the tent: a wide cone of hides
    pts = [(cx, 4), (cx + 15, H - 4), (cx - 15, H - 4)]
    shaded_poly(cv, pts, hide, grad_dir=(0.8, 0.4), dither=0.35)
    # seams of the hides
    for k in (-8, -3, 3, 8):
        cv.line(cx, 6, cx + k * 1.6, H - 5, hide[0])
    # the open flap, dark inside, a firelight glow at its foot
    flap = [(cx, 16), (cx + 5, H - 4), (cx - 3, H - 4)]
    for y in range(16, H - 4):
        for x in range(cx - 4, cx + 6):
            if point_in_poly(x + 0.5, y + 0.5, flap):
                cv.px(x, y, hx("#060608") if y < H - 7 else hx("#5a2a10"))
    # a white whirlwind on the side
    for i in range(12):
        t = i / 12
        a = t * math.tau * 1.3
        r = 1 + t * 3.2
        cv.px(int(cx - 7 + math.cos(a) * r), int(H - 12 + math.sin(a) * r), P["white"][2])
    # the pole's peak and a scrap of red cloth
    cv.vline(cx, 1, 5, C["wood"][3])
    cv.px(cx + 1, 2, P["red"][2])
    cv.px(cx + 2, 2, P["red"][3])
    cv.px(cx + 2, 3, P["red"][2])
    cv.hline(cx - 15, cx + 15, H - 4, hide[0])
    cv.outline(STEPPE_OUT)
    return cv, (cx, H - 3)


def stake_fence(seed=0):
    """A stretch of the bandits' stake fence: sharpened posts lashed together."""
    W, H = 34, 22
    cv = Canvas(W, H)
    rnd = random.Random(seed)
    w = C["wood"]
    for i in range(6):
        x = 2 + i * 5.4
        top = rnd.randint(1, 4)
        for y in range(top + 2, H - 1):
            cv.px(int(x), y, w[2])
            cv.px(int(x) + 1, y, w[3])
            cv.px(int(x) + 2, y, w[1])
        cv.px(int(x) + 1, top, w[4])
        cv.px(int(x), top + 1, w[3])
        cv.px(int(x) + 2, top + 1, w[2])
    # the lashing, two rows of rope
    for y in (9, 15):
        cv.hline(1, W - 2, y, C["straw"][1])
    cv.outline(STEPPE_OUT)
    return cv, (W // 2, H - 2)


def windmill(seed=0):
    """The windmill on the Hắc Phong's hill: a tapering tower of stone and planks, a thatched cap,
    a door and a small window, the hub of the sails at the top (the sails are their own sprite)."""
    W, H = 36, 60
    cv = Canvas(W, H)
    cx = W // 2
    st = C["sand"]
    w = C["wood"]
    base_w, top_w = 12, 7
    top_y, base_y = 16, H - 2
    for y in range(top_y, base_y + 1):
        t = (y - top_y) / (base_y - top_y)
        half = top_w + (base_w - top_w) * t
        for x in range(int(cx - half), int(cx + half) + 1):
            u = (x - (cx - half)) / (2 * half)
            inten = 0.5 - u * 1.2
            if y > base_y - 16:
                # stone footing: courses of blocks
                c = st[shade_index(inten, len(st) - 1, x, y, 0.2) + 1]
                if y % 4 == 0 or (x + (y // 4) * 3) % 6 == 0:
                    c = st[1]
            else:
                # plank walls above
                c = w[shade_index(inten, len(w) - 1, x, y, 0.15) + 1]
                if (x - int(cx - half)) % 3 == 0:
                    c = w[1]
            cv.px(x, y, c)
    # the door and a window
    for y in range(base_y - 9, base_y + 1):
        for x in range(cx - 2, cx + 3):
            cv.px(x, y, hx("#140c08") if y > base_y - 8 else w[1])
    for y in range(28, 32):
        for x in range(cx + 1, cx + 4):
            cv.px(x, y, hx("#ffcc70") if (x + y) % 3 else hx("#d08a30"))
    # the thatched cap
    cap = [(cx - 10, top_y + 2), (cx, top_y - 10), (cx + 10, top_y + 2)]
    shaded_poly(cv, cap, C["straw"], grad_dir=(0.7, 0.6), dither=0.35)
    for k in range(-8, 9, 3):
        cv.line(cx, top_y - 9, cx + k, top_y + 1, C["straw"][0])
    # the hub
    shaded_ellipse(cv, cx, top_y + 2, 2.2, 2.2, w[2:], dither=0.1)
    # a black banner on the cap
    cv.vline(cx, top_y - 16, top_y - 10, w[3])
    for y in range(top_y - 16, top_y - 12):
        for x in range(cx + 1, cx + 6):
            cv.px(x, y, hx("#16161e") if (x + y) % 2 else hx("#22222c"))
    cv.outline(STEPPE_OUT)
    return cv, (cx, H - 2)


def windmill_sails(frame=0):
    """The four lattice sails, turned frame × 22.5° (they repeat every quarter turn)."""
    S = 57
    cv = Canvas(S, S)
    c = S / 2
    w = C["wood"]
    cloth = C["felt"]
    for k in range(4):
        a = math.radians(frame * 22.5 + k * 90)
        dx, dy = math.cos(a), math.sin(a)
        nx, ny = -dy, dx
        # the stock
        cv.line(c, c, c + dx * 27, c + dy * 27, w[2], 2)
        # the lattice sail beside it
        for t in range(7, 27):
            for s_ in range(1, 7):
                x, y = c + dx * t + nx * s_, c + dy * t + ny * s_
                lat = t % 4 == 0 or s_ == 6
                cv.px(int(round(x)), int(round(y)), w[3] if lat else cloth[1 + (t + s_) % 3])
    shaded_ellipse(cv, c, c, 3, 3, w[2:], dither=0.1)
    cv.outline(STEPPE_OUT)
    return cv, (int(c), int(c))


def props():
    items = []
    for i in range(3):
        items.append((f"steppegrass_{i}", grass_tuft(40 + i, True)))
    items.append(("steppegrass_s", grass_tuft(50, False)))
    items += [("acacia_0", acacia(1)), ("acacia_1", acacia(2)),
              ("sandrock_big_0", sand_rock(3, True)), ("sandrock_big_1", sand_rock(4, True)),
              ("sandrock_small", sand_rock(5, False)), ("cairn", cairn(6)), ("yurt", yurt(7)),
              ("windring", wind_ring(8)), ("hp_banner", banner(9)),
              ("scarecrow", scarecrow_prop(10)), ("haystack", haystack(11)), ("blacktent", black_tent(12)),
              ("stakefence", stake_fence(13)), ("windmill", windmill(14))]
    items += [(f"windmill_sails_{i}", windmill_sails(i)) for i in range(4)]
    return [(name, cv, piv) for name, (cv, piv) in items]


# ============================================================================
# Item icons (16x16)
# ============================================================================

ICON_OUT = hx("#140e08")


def _done(cv):
    cv.outline(ICON_OUT)
    return cv


def icon_hyena_fang():
    cv = Canvas(16, 16)
    b = P["bone"]
    pts = [(5, 3), (10, 3), (8, 13)]
    shaded_poly(cv, pts, b, grad_dir=(0.5, 1.0), dither=0.1)
    cv.hline(5, 10, 3, hx("#7a3a2a"))
    cv.hline(5, 10, 4, hx("#a0503a"))
    cv.px(7, 6, b[3])
    return _done(cv)


def icon_eagle_feather():
    cv = Canvas(16, 16)
    g = ramp("#2a2e36", "#3c424c", "#525a66", "#6c7482", "#8a92a0")
    # the vane: a long leaf shape along the diagonal, lighter on its upper side
    pts = [(3, 13), (6, 8), (10, 4), (13, 2), (12, 6), (8, 11), (4, 14)]
    shaded_poly(cv, pts, g, grad_dir=(-0.6, -0.8), dither=0.0)
    # notches in the vane and the white shaft
    cv.px(7, 9, g[0])
    cv.px(10, 7, g[0])
    cv.line(2, 14, 12, 3, P["cream"][2])
    cv.px(13, 2, C["wind"][3])
    return _done(cv)


def icon_bison_horn():
    cv = Canvas(16, 16)
    h = ramp("#5a5244", "#8e8470", "#c2b89e", "#e6dec8")
    for i in range(12):
        t = i / 11
        x = 3 + t * 9 + math.sin(t * 2.5) * 1.5
        y = 12 - t * 10 + (1 - t) * 1
        r = 2.4 * (1 - t) + 0.6
        shaded_ellipse(cv, x, y, r, r, h, dither=0.1)
    cv.px(12, 2, h[3])
    return _done(cv)


def icon_bison_hide():
    cv = Canvas(16, 16)
    br = ramp("#2e1c10", "#422a18", "#583a22", "#704c2e", "#8a6040")
    pts = [(3, 3), (13, 3), (14, 8), (12, 13), (4, 13), (2, 8)]
    shaded_poly(cv, pts, br, grad_dir=(0.4, 1.0), dither=0.3)
    for (x, y) in ((5, 5), (9, 6), (7, 9), (11, 10), (4, 10)):
        cv.px(x, y, hx("#1c120a"))
        cv.px(x + 1, y, hx("#1c120a"))
    return _done(cv)


def icon_straw():
    """Rơm Bù Nhìn: a bundle of straw tied with twine, a glint of an ember in it."""
    cv = Canvas(16, 16)
    st = C["straw"]
    for k in range(9):
        x0 = 4 + k
        cv.line(x0, 13, x0 + (k - 4) * 0.5 + 1, 2, st[1 + k % 4])
    cv.hline(4, 12, 8, hx("#5a3a1c"))
    cv.hline(4, 12, 9, hx("#7a5228"))
    cv.px(9, 4, hx("#ffb030"))
    return _done(cv)


def icon_hp_badge():
    """Huy Hiệu Hắc Phong: a black iron disc with the band's white whirlwind."""
    cv = Canvas(16, 16)
    iron = ramp("#0e0e14", "#1c1c26", "#2c2c3a", "#40404e", "#5a5a6a")
    shaded_ellipse(cv, 8, 8, 6, 6, iron, dither=0.1, bias=0.1)
    for i in range(14):
        t = i / 14
        a = t * math.tau * 1.4
        r = 0.8 + t * 4
        cv.px(int(8 + math.cos(a) * r), int(8 + math.sin(a) * r), P["white"][3])
    cv.px(5, 4, iron[4])
    return _done(cv)


def icon_iron_horn():
    """Sừng Sắt: the iron-capped horn of Bò Rừng Sắt."""
    cv = Canvas(16, 16)
    h = ramp("#3a3e4c", "#5c6272", "#8a92a4", "#c4cad8", "#eef0f6")
    for i in range(12):
        t = i / 11
        x = 3 + t * 9 + math.sin(t * 2.5) * 1.5
        y = 12 - t * 10 + (1 - t) * 1
        r = 2.4 * (1 - t) + 0.6
        shaded_ellipse(cv, x, y, r, r, h, dither=0.1)
    for (x, y) in ((4, 11), (6, 9)):
        cv.px(x, y, h[4])
    cv.px(3, 12, hx("#7a3a1a"))
    return _done(cv)


def icon_iron_plate():
    """Giáp Sắt Vụn: a riveted plate torn off the bison, rust at the edges."""
    cv = Canvas(16, 16)
    iron = ramp("#2e3240", "#4a5064", "#6e7690", "#9aa2ba", "#c8cedc")
    pts = [(3, 4), (12, 3), (13, 11), (8, 13), (3, 11)]
    shaded_poly(cv, pts, iron, grad_dir=(0.4, 1.0), dither=0.2)
    for (x, y) in ((4, 5), (11, 4), (12, 10), (4, 10)):
        cv.px(x, y, iron[4])
    for (x, y) in ((8, 12), (9, 12), (3, 8)):
        cv.px(x, y, hx("#7a3a1a"))
    return _done(cv)


def icon_blackwind_blade():
    """Mảnh Song Đao: the broken half of the chief's curved blade, a red cord on its hilt."""
    cv = Canvas(16, 16)
    m = P["metal"]
    for i in range(10):
        t = i / 9
        x = 4 + t * 8
        y = 12 - t * 9 + math.sin(t * 3) * 1.6
        cv.px(int(x), int(y), m[4])
        cv.px(int(x), int(y) + 1, m[2])
    cv.line(2, 14, 4, 12, hx("#1c1c26"))
    cv.px(3, 12, P["red"][3])
    cv.px(2, 12, P["red"][2])
    cv.px(12, 3, m[5])
    return _done(cv)


def icons():
    return [("scarecrow_straw", icon_straw()), ("hp_badge", icon_hp_badge()), ("iron_horn", icon_iron_horn()),
            ("iron_plate", icon_iron_plate()), ("blackwind_blade", icon_blackwind_blade()),
            ("hyena_fang", icon_hyena_fang()), ("eagle_feather", icon_eagle_feather()),
            ("bison_horn", icon_bison_horn()), ("bison_hide", icon_bison_hide())]


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_row, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    tl = [t for row in tiles() for _, t in row]
    preview(pack_grid(tl, 16).to_image(), 4, os.path.join(out, "steppe_tiles.png"))
    pr = [c for _, c, _ in props()]
    widest = max(c.w for c in pr)
    tallest = max(c.h for c in pr)
    sheet = Canvas(widest * len(pr), tallest)
    for i, c in enumerate(pr):
        sheet.blit(c, i * widest + (widest - c.w) // 2, tallest - c.h)
    preview(sheet.to_image(), 3, os.path.join(out, "steppe_props.png"))
    preview(pack_row([c for _, c in icons()]).to_image(), 6, os.path.join(out, "steppe_icons.png"))
    print("ok")
