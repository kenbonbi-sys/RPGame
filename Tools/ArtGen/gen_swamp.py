"""
Đầm Lầy Sương Mù (the swamp east of the forest): terrain tiles and props.

Tiles (16x16), same layering as the forest (gen_terrain):
  Ground : swamp_i        dark, wet, olive grass (random variants)
  Mud    : mud_i / mudfull_i   marching squares (wet paths and clearings)
  Water  : water_i / waterfull_i marching squares (deep water that slows: its own tilemap)
  Details: lily_i (on water), reeds_i, puddle_0, lotus_0, rotleaf_0
Props return (Canvas, pivot) like gen_props: dead trees, weeping willows, cattails,
mossy rocks and logs, bones, a stilt hut and the Đá Truyền Tống (waystone, used everywhere).
"""
import math
import random

import numpy as np

from pixelkit import Canvas, P, OUTLINE, TileNoise, hx, ramp, mix, shaded_ellipse, shade_index, LIGHT
from gen_terrain import ms_mask, edge_dist

T = 16

S = {
    "swamp": ramp("#141f19", "#1b2a20", "#223526", "#2c4430", "#3a5536", "#4c6a3e"),
    "water": ramp("#0a161c", "#0f2026", "#152c30", "#1c3b3c", "#265048", "#346a5a", "#5a9282", "#8cc0ac"),
    "mud": ramp("#1c1512", "#281d17", "#36271e", "#473426", "#5a4331", "#71573f"),
    "lily": ramp("#16301a", "#214a22", "#2e652c", "#428238", "#62a24c", "#8cc26a"),
    "reed": ramp("#2a2414", "#453a1c", "#625226", "#826c32", "#a48a44", "#c8ac5e"),
    "cattail": ramp("#2a160e", "#452414", "#63361c", "#824a26"),
    "deadwood": ramp("#161315", "#241f20", "#352d2b", "#4a3f3a", "#62544c", "#7e6c60", "#9a8676"),
    "willow": ramp("#0e1c16", "#15291d", "#1d3824", "#27482c", "#335a34", "#44703e", "#5c8a4c"),
    "rock": ramp("#1a1c1e", "#2a2e30", "#3c4244", "#51585a", "#6a7272", "#8a9290"),
    "moss": ramp("#1c3018", "#2a4620", "#3a5e28", "#527a34", "#6e9644"),
    "bone": ramp("#4e4638", "#7a705e", "#a89c86", "#d0c6ae", "#eee6d2"),
    "rune": ramp("#0e2a36", "#16485a", "#23707e", "#3aa0a8", "#6ad2cc", "#b4fff0"),
    "plank": ramp("#221610", "#36241a", "#4c3424", "#644630", "#7e5a3e", "#9a7250"),
    "thatch": ramp("#2c2414", "#44381e", "#5e4e28", "#7a6634", "#988244"),
}
SWAMP_OUT = hx("#0a120e")
WOOD_OUT = hx("#140e0c")
STONE_OUT = hx("#0f1214")

n_lo = TileNoise(T, 2, seed=71)
n_hi = TileNoise(T, 4, seed=73)
n_hi2 = TileNoise(T, 8, seed=79)


# ============================================================================
# Terrain
# ============================================================================

def swamp_ground(seed):
    """Wet, dark grass with moss patches and a few reeds of light."""
    cv = Canvas(T, T)
    g = S["swamp"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            n = n_hi2(x + seed * 3, y) * 0.55 + n_lo(x, y + seed) * 0.45
            c = g[2] if n > 0.4 else g[1]
            if n > 0.72:
                c = g[3]
            cv.px(x, y, c)
    for _ in range(7):
        x, y = rnd.randrange(T), rnd.randrange(1, T)
        cv.px(x, y, g[4])
        cv.px(x, y - 1, g[3])
    for _ in range(3):
        x, y = rnd.randrange(T), rnd.randrange(T)
        cv.px(x, y, g[0])
    if rnd.random() < 0.3:
        x, y = rnd.randrange(2, T - 2), rnd.randrange(2, T - 2)
        cv.px(x, y, S["moss"][3])
        cv.px(x + 1, y, S["moss"][2])
    return cv


def mud_texture(cv, seed, mask):
    d = S["mud"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if not mask[y, x]:
                continue
            # fine grain only: coarse blotches make the tiling show
            n = n_hi2(x + seed * 5, y + seed) * 0.7 + rnd.random() * 0.3
            c = d[3]
            if n < 0.3:
                c = d[2]
            elif n > 0.78:
                c = d[4]
            cv.px(x, y, c)
    # wet glints and little puddles
    for _ in range(rnd.randint(0, 1)):
        x, y = rnd.randrange(1, T - 3), rnd.randrange(1, T - 2)
        if mask[y, x] and mask[y, x + 1] and mask[y, x + 2]:
            cv.px(x, y, S["water"][3])
            cv.px(x + 1, y, S["water"][4])
            cv.px(x + 2, y, S["water"][3])
            cv.px(x + 1, y - 1 if y > 0 and mask[y - 1, x + 1] else y, S["water"][5])
    for _ in range(rnd.randint(2, 4)):
        x, y = rnd.randrange(T), rnd.randrange(T)
        if mask[y, x]:
            cv.px(x, y, d[1])
    # a footprint of a toad now and then
    if rnd.random() < 0.25:
        x, y = rnd.randrange(3, T - 3), rnd.randrange(3, T - 3)
        if mask[y, x]:
            for dx, dy in ((0, 0), (-1, -1), (1, -1), (0, -2)):
                cv.px(x + dx, y + dy, d[1])


def mud_tile(idx, seed=0):
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.42, seed_shift=11)
    mud_texture(cv, seed + idx * 3, m)
    d = S["mud"]
    ed = edge_dist(m, idx)
    rnd = random.Random(idx * 37 + seed)
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            if ed[y, x] == 1:
                up_open = y - 1 >= 0 and not m[y - 1, x]
                cv.px(x, y, d[0] if up_open else d[1])
            elif ed[y, x] == 2 and y - 2 >= 0 and not m[y - 2, x]:
                cv.px(x, y, d[2])
    for y in range(T):
        for x in range(T):
            if m[y, x] and ed[y, x] == 1 and rnd.random() < 0.15:
                cv.px(x, y, S["swamp"][3])
    return cv


def mud_full(seed):
    m = np.ones((T, T), bool)
    cv = Canvas(T, T)
    mud_texture(cv, seed, m)
    return cv


def water_texture(cv, seed, mask, ed=None):
    w = S["water"]
    rnd = random.Random(seed)
    for y in range(T):
        for x in range(T):
            if not mask[y, x]:
                continue
            # still, dark water: a faint grain, no blotches (they would repeat tile after tile)
            n = n_hi2(x + seed * 2, y + seed * 3) * 0.6 + rnd.random() * 0.4
            c = w[1]
            if n > 0.72:
                c = w[2]
            cv.px(x, y, c)
    # ripples: short light dashes, a lighter pixel in the middle
    for _ in range(rnd.randint(1, 3)):
        x, y = rnd.randrange(1, T - 4), rnd.randrange(2, T - 1)
        if all(mask[y, x + k] for k in range(4)) and (ed is None or ed[y, x] > 2):
            cv.px(x, y, w[4])
            cv.px(x + 1, y, w[5])
            cv.px(x + 2, y, w[5])
            cv.px(x + 3, y, w[4])
    if rnd.random() < 0.35:
        x, y = rnd.randrange(2, T - 2), rnd.randrange(2, T - 2)
        if mask[y, x] and (ed is None or ed[y, x] > 2):
            cv.px(x, y, w[6])


def water_tile(idx, seed=0):
    """Deep water: a muddy bank with a dark lip, a bright line where the light hits the edge."""
    cv = Canvas(T, T)
    if idx == 0:
        return cv
    m = ms_mask(idx, amp=0.5, seed_shift=23)
    ed = edge_dist(m, idx)
    water_texture(cv, seed + idx * 5, m, ed)
    w = S["water"]
    d = S["mud"]
    for y in range(T):
        for x in range(T):
            if not m[y, x]:
                continue
            up_open = y - 1 >= 0 and not m[y - 1, x]
            if ed[y, x] == 1:
                # the bank: mud above the water, a dark shadow line on the far side
                cv.px(x, y, d[2] if up_open else OUTLINE)
            elif ed[y, x] == 2:
                cv.px(x, y, w[0] if up_open else w[4])
    return cv


def water_full(seed):
    m = np.ones((T, T), bool)
    cv = Canvas(T, T)
    water_texture(cv, seed, m)
    return cv


def detail_lily(seed):
    """Lily pads (drawn on water tiles)."""
    cv = Canvas(T, T)
    l = S["lily"]
    rnd = random.Random(seed)
    for _ in range(rnd.randint(1, 3)):
        cx, cy = rnd.uniform(4, 12), rnd.uniform(4, 12)
        r = rnd.uniform(2.2, 3.4)
        notch = rnd.uniform(0, math.tau)
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = x + 0.5 - cx, (y + 0.5 - cy) * 1.25
                d = math.hypot(dx, dy)
                if d > r:
                    continue
                a = math.atan2(dy, dx)
                if abs((a - notch + math.pi) % math.tau - math.pi) < 0.35 and d > r * 0.25:
                    continue
                c = l[3] if dx < 0 and dy < 0 else l[2]
                if d > r - 0.9:
                    c = l[1]
                cv.px(x, y, c)
        cv.px(int(cx - r * 0.4), int(cy - r * 0.3), l[5])
    if rnd.random() < 0.4:
        # a pink flower on one of them
        x, y = rnd.randrange(5, 11), rnd.randrange(5, 11)
        for dx, dy in ((0, 0), (-1, 0), (1, 0), (0, -1)):
            cv.px(x + dx, y + dy, hx("#f0a0c8"))
        cv.px(x, y, hx("#ffe07a"))
    cv.outline(hx("#0a1a14"))
    return cv


def detail_reeds(seed):
    """A tuft of sedge: thin green blades, a few pale tips."""
    cv = Canvas(T, T)
    g = S["lily"]
    r = S["reed"]
    rnd = random.Random(seed)
    cx = rnd.randint(6, 9)
    by = rnd.randint(12, 14)
    for dx in (-4, -2, -1, 0, 1, 3):
        h = rnd.randint(4, 9)
        lean = rnd.choice([-1, 0, 0, 1])
        for k in range(h):
            x = cx + dx + (lean if k > h // 2 else 0)
            cv.px(x, by - k, g[1] if k < 2 else (g[2] if k < h - 1 else (r[4] if rnd.random() < 0.4 else g[3])))
    cv.outline(hx("#0a140c"))
    return cv


def detail_puddle(seed):
    cv = Canvas(T, T)
    w = S["water"]
    rnd = random.Random(seed)
    cx, cy = rnd.uniform(6, 10), rnd.uniform(6, 10)
    shaded_ellipse(cv, cx, cy, 4.5, 2.2, [w[2], w[3], w[4], w[5]], dither=0.3)
    cv.px(int(cx - 2), int(cy - 1), w[6])
    cv.outline(S["mud"][1])
    return cv


def detail_lotus(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    cx, cy = 8, 9
    pet = [hx("#7a3a6a"), hx("#b85a94"), hx("#e88ac0"), hx("#ffc4e4")]
    for a in range(6):
        ang = a / 6 * math.tau + rnd.uniform(-0.2, 0.2)
        for k in range(1, 4):
            x = int(round(cx + math.cos(ang) * k))
            y = int(round(cy + math.sin(ang) * k * 0.7))
            cv.px(x, y, pet[min(3, 3 - k + 1)])
    cv.px(cx, cy, hx("#ffe07a"))
    cv.px(cx, cy - 1, hx("#fff4c4"))
    cv.outline(hx("#2a1024"))
    return cv


def detail_rotleaf(seed):
    cv = Canvas(T, T)
    rnd = random.Random(seed)
    cols = [(hx("#3a2c18"), hx("#5a4422")), (hx("#2a3018"), hx("#44502a")), (hx("#4a2a18"), hx("#6a4426"))]
    for _ in range(rnd.randint(3, 5)):
        x, y = rnd.randrange(2, T - 3), rnd.randrange(2, T - 3)
        c1, c2 = rnd.choice(cols)
        cv.px(x, y, c2)
        cv.px(x + 1, y, c1)
        cv.px(x + 1, y + 1, c2)
    return cv


def tiles():
    """Rows of (name, Canvas) for the terrain sheet."""
    rows = []
    rows.append([(f"swamp_{i}", swamp_ground(700 + i)) for i in range(8)])
    rows.append([(f"water_{i}", water_tile(i)) for i in range(16)])
    rows.append([(f"waterfull_{i}", water_full(800 + i)) for i in range(4)] +
                [(f"lily_{i}", detail_lily(810 + i)) for i in range(3)] +
                [(f"reeds_{i}", detail_reeds(820 + i)) for i in range(2)] +
                [("puddle_0", detail_puddle(830)), ("lotus_0", detail_lotus(831)), ("rotleaf_0", detail_rotleaf(832))])
    rows.append([(f"mud_{i}", mud_tile(i)) for i in range(16)])
    rows.append([(f"mudfull_{i}", mud_full(900 + i)) for i in range(4)])
    return rows


# ============================================================================
# Props
# ============================================================================

def dead_tree(seed=0):
    """A bare, twisted grey tree: the swamp's silhouette."""
    rnd = random.Random(seed)
    W, H = 40, 54
    cv = Canvas(W, H)
    wd = S["deadwood"]
    cx = W // 2 + rnd.randint(-2, 2)

    def branch(x, y, ang, length, width, depth):
        for k in range(int(length)):
            t = k / max(1, length)
            wdt = max(1, int(round(width * (1 - t * 0.6))))
            px = x + math.cos(ang) * k
            py = y - math.sin(ang) * k
            for o in range(-wdt // 2, wdt - wdt // 2):
                ox = int(round(px + o * math.sin(ang)))
                oy = int(round(py + o * math.cos(ang)))
                c = wd[4] if o < 0 else (wd[2] if o > 0 else wd[3])
                cv.px(ox, oy, c)
            ang += rnd.uniform(-0.12, 0.12)
        ex, ey = x + math.cos(ang) * length, y - math.sin(ang) * length
        if depth > 0:
            for s in (-1, 1):
                if rnd.random() < 0.85:
                    branch(ex, ey, ang + s * rnd.uniform(0.35, 0.8), length * rnd.uniform(0.5, 0.7), max(1, width - 1), depth - 1)

    # trunk
    for y in range(26, 50):
        half = 2.5 if y < 44 else 2.5 + (y - 43) * 0.7
        for x in range(int(cx - half), int(cx + half + 1)):
            dx = (x + 0.5 - cx) / half
            idx = shade_index(-dx * 0.9, len(wd), x, y, dither=0.3, bias=-0.1)
            cv.px(x, y, wd[idx])
    for y in range(28, 46, 3):
        cv.px(cx + (y % 3) - 1, y, wd[1])
    branch(cx, 27, math.pi / 2 + rnd.uniform(-0.25, 0.25), 13, 3, 2)
    branch(cx - 1, 34, math.pi * 0.82, 9, 2, 1)
    branch(cx + 1, 31, math.pi * 0.18, 10, 2, 1)
    # hanging moss strands
    for _ in range(5):
        x = rnd.randint(cx - 12, cx + 12)
        for y in range(4, 30):
            if cv.opaque(x, y):
                for k in range(rnd.randint(2, 6)):
                    if not cv.opaque(x, y + k + 1):
                        cv.px(x, y + k + 1, S["moss"][2] if k % 2 else S["moss"][1])
                break
    cv.outline(WOOD_OUT)
    return cv, (cx, 49)


def willow(seed=0):
    """A weeping swamp willow: a dark dome with long drooping strands."""
    rnd = random.Random(seed)
    W, H = 46, 50
    cv = Canvas(W, H)
    cx = W // 2
    lf = S["willow"]
    wd = S["deadwood"]
    for y in range(28, 46):
        half = 3 if y < 42 else 3 + (y - 41)
        for x in range(cx - half, cx + half):
            dx = (x + 0.5 - cx) / half
            cv.px(x, y, wd[shade_index(-dx * 0.9, len(wd) - 1, x, y, 0.3, -0.1) + 1])
    for (ox, oy, r) in [(-9, 16, 9), (9, 16, 9), (0, 12, 11), (-5, 9, 8), (5, 9, 8)]:
        shaded_ellipse(cv, cx + ox, oy, r, r * 0.8, lf[1:], dither=0.4, bias=-0.05)
    # drooping strands over the dome's lower half
    for i in range(18):
        x = cx - 19 + i * 38 / 17 + rnd.uniform(-0.6, 0.6)
        top = 12 + int(6 * (1 - abs(x - cx) / 20))
        length = rnd.randint(14, 26)
        for k in range(length):
            xx = int(round(x + math.sin(k * 0.25 + i) * 0.8))
            c = lf[2] if k % 3 else lf[3]
            if k > length - 3:
                c = lf[4]
            cv.px(xx, top + k, c)
    cv.outline(hx("#07100a"))
    return cv, (cx, 45)


def cattails(seed=0):
    rnd = random.Random(seed)
    W, H = 20, 28
    cv = Canvas(W, H)
    r = S["reed"]
    ct = S["cattail"]
    base = H - 2
    for i in range(7):
        x0 = 3 + i * 2 + rnd.randint(-1, 1)
        h = rnd.randint(14, 24)
        lean = rnd.uniform(-0.12, 0.12)
        for k in range(h):
            x = int(round(x0 + lean * k))
            cv.px(x, base - k, r[1] if k < 3 else r[3] if k < h - 4 else r[4])
        if rnd.random() < 0.7:
            x = int(round(x0 + lean * h))
            for k in range(4):
                cv.px(x, base - h + k, ct[2] if k else ct[3])
                cv.px(x + 1, base - h + k, ct[1])
    # a few leaves
    for _ in range(4):
        x = rnd.randint(4, W - 5)
        for k in range(rnd.randint(5, 9)):
            cv.px(x + (k // 3) * rnd.choice([-1, 1]), base - k, r[2])
    cv.outline(hx("#120e06"))
    return cv, (W // 2, base)


def mossy_rock(seed=0, big=True):
    rnd = random.Random(seed)
    W, H = (24, 18) if big else (14, 10)
    cv = Canvas(W, H)
    st = S["rock"]
    cx, cy = W / 2, H * 0.58
    rx, ry = W * 0.46, H * 0.42
    for y in range(H - 2):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            wob = 0.07 * math.sin(x * 1.1 + seed) + 0.05 * math.cos(y * 1.9 + seed)
            if dx * dx + dy * dy > 1.0 + wob:
                continue
            nz = math.sqrt(max(0, 1 - dx * dx - dy * dy))
            qx, qy = round(dx * 2) / 2, round(dy * 2) / 2
            inten = qx * LIGHT[0] + qy * LIGHT[1] + nz * LIGHT[2]
            cv.px(x, y, st[shade_index(inten, len(st), x, y, 0.2, 0.0)])
    mo = S["moss"]
    for x in range(W):
        for y in range(H):
            if cv.opaque(x, y):
                depth = int(2 + 2.5 * math.sin(x * 0.5 + seed) + rnd.random() * 1.5)
                for k in range(max(0, depth)):
                    if cv.opaque(x, y + k):
                        cv.px(x, y + k, mo[3] if k == 0 else mo[2] if k < depth - 1 else mo[1])
                break
    # the waterline stain at the bottom
    for x in range(W):
        for y in range(H - 1, -1, -1):
            if cv.opaque(x, y):
                cv.px(x, y, st[0])
                break
    cv.outline(STONE_OUT)
    return cv, (W // 2, H - 3)


def mossy_log(seed=0):
    rnd = random.Random(seed)
    W, H = 34, 14
    cv = Canvas(W, H)
    wd = S["deadwood"]
    y0, y1 = 3, 11
    for y in range(y0, y1):
        for x in range(2, W - 3):
            t = (y - y0) / (y1 - y0)
            c = wd[4] if t < 0.3 else wd[3] if t < 0.7 else wd[1]
            cv.px(x, y, c)
    # the cut end
    shaded_ellipse(cv, W - 4, (y0 + y1) / 2, 2.6, 4, [wd[2], wd[4], wd[5], wd[6]], dither=0.2)
    cv.px(W - 4, (y0 + y1) // 2, wd[2])
    mo = S["moss"]
    for x in range(3, W - 5):
        if rnd.random() < 0.8:
            cv.px(x, y0, mo[3])
            if rnd.random() < 0.5:
                cv.px(x, y0 + 1, mo[2])
    for _ in range(3):
        x = rnd.randint(6, W - 8)
        cv.px(x, y0 - 1, hx("#c8a04a"))   # little yellow fungi
        cv.px(x + 1, y0 - 1, hx("#e8c86a"))
    cv.outline(WOOD_OUT)
    return cv, (W // 2, y1)


def bones(seed=0):
    rnd = random.Random(seed)
    W, H = 20, 12
    cv = Canvas(W, H)
    b = S["bone"]
    # a skull and two long bones
    shaded_ellipse(cv, 7, 6, 3.6, 3.0, b[1:], dither=0.2)
    cv.px(6, 6, OUTLINE); cv.px(8, 6, OUTLINE); cv.px(7, 8, b[0])
    for x in range(10, 18):
        cv.px(x, 8 + (x - 10) // 4, b[3])
        cv.px(x, 9 + (x - 10) // 4, b[1])
    cv.px(10, 7, b[4]); cv.px(17, 10, b[4])
    for x in range(12, 18):
        cv.px(x, 4 + (17 - x) // 3, b[2])
    cv.outline(hx("#1a1612"))
    return cv, (W // 2, H - 2)


def waystone(seed=0, lit=False):
    """Đá Truyền Tống: a standing stone with a glowing rune (lit once attuned)."""
    W, H = 18, 34
    cv = Canvas(W, H)
    st = S["rock"]
    cx = W / 2
    for y in range(3, H - 3):
        t = (y - 3) / (H - 6)
        half = 5.5 - 1.8 * (1 - t) ** 2
        for x in range(W):
            dx = (x + 0.5 - cx) / half
            if abs(dx) > 1:
                continue
            q = round(dx * 2) / 2
            inten = -q * 0.8 + (0.4 - t) * 0.5
            cv.px(x, y, st[shade_index(inten, len(st), x, y, 0.25, 0.08)])
    # a pointed top
    for k in range(3):
        for x in range(int(cx - 3 + k), int(cx + 3 - k)):
            cv.px(x, 3 - k, st[4 - k // 2])
    # base stones
    for x in range(1, W - 1):
        cv.px(x, H - 3, st[1])
        cv.px(x, H - 4, st[2] if x % 3 else st[3])
    # the rune: a circle with a vertical stroke
    rn = S["rune"]
    col = rn[5] if lit else rn[2]
    mid = rn[4] if lit else rn[1]
    ry = 15
    for a in range(16):
        ang = a / 16 * math.tau
        cv.px(int(round(cx - 0.5 + math.cos(ang) * 3)), int(round(ry + math.sin(ang) * 3)), mid)
    for y in range(ry - 5, ry + 6):
        cv.px(int(cx - 0.5), y, col)
    cv.px(int(cx - 2), ry, col); cv.px(int(cx + 1), ry, col)
    # moss at its feet
    for x in range(2, W - 2, 2):
        cv.px(x, H - 5, S["moss"][2])
    cv.outline(STONE_OUT)
    return cv, (W // 2, H - 3)


def stilt_hut():
    """A fisher's hut on stilts, for the swamp's camp."""
    W, H = 48, 50
    cv = Canvas(W, H)
    pl = S["plank"]
    th = S["thatch"]
    # stilts
    for x in (8, 18, 30, 40):
        for y in range(34, 47):
            cv.px(x, y, pl[1])
            cv.px(x + 1, y, pl[2])
    # floor
    for y in range(31, 35):
        for x in range(4, W - 4):
            cv.px(x, y, pl[3] if (x // 4) % 2 else pl[2])
    for x in range(4, W - 4):
        cv.px(x, 35, pl[0])
    # walls
    for y in range(18, 31):
        for x in range(8, W - 8):
            c = pl[4] if (x // 3) % 2 else pl[3]
            if y == 30:
                c = pl[1]
            cv.px(x, y, c)
    # door and window
    for y in range(22, 31):
        for x in range(21, 27):
            cv.px(x, y, pl[0])
    for y in range(21, 25):
        for x in range(32, 37):
            cv.px(x, y, hx("#2a1a10"))
    cv.px(34, 22, hx("#ffc860"))
    # thatch roof
    for y in range(4, 20):
        t = (y - 4) / 16
        half = 6 + t * 18
        for x in range(int(W / 2 - half), int(W / 2 + half)):
            n = (x + y * 2) % 5
            cv.px(x, y, th[3] if n == 0 else th[2] if y > 16 else th[4] if t < 0.3 else th[3] if n % 2 else th[2])
    for x in range(int(W / 2 - 24), int(W / 2 + 24)):
        cv.px(x, 20, th[0])
    cv.outline(WOOD_OUT)
    return cv, (W // 2, 46)


def props():
    """(name, Canvas, pivot) for the props sheet."""
    items = []
    for i in range(2):
        c, p = dead_tree(seed=i + 1); items.append((f"deadtree_{i}", c, p))
        c, p = willow(seed=i + 3); items.append((f"willow_{i}", c, p))
        c, p = cattails(seed=i + 5); items.append((f"cattails_{i}", c, p))
        c, p = mossy_rock(seed=i + 7, big=True); items.append((f"swamprock_big_{i}", c, p))
        c, p = mossy_rock(seed=i + 9, big=False); items.append((f"swamprock_small_{i}", c, p))
    c, p = mossy_log(11); items.append(("mossylog", c, p))
    c, p = bones(12); items.append(("bones", c, p))
    c, p = waystone(lit=False); items.append(("waystone", c, p))
    c, p = waystone(lit=True); items.append(("waystone_lit", c, p))
    c, p = stilt_hut(); items.append(("stilthut", c, p))
    return items


if __name__ == "__main__":
    import os, sys
    from pixelkit import preview, pack_shelf
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    rows = tiles()
    sheet = Canvas(16 * T, len(rows) * T)
    for r, row in enumerate(rows):
        for c, (n, t) in enumerate(row):
            sheet.blit(t, c * T, r * T)
    preview(sheet.to_image(), 4, os.path.join(out, "swamp_tiles.png"))
    s, _ = pack_shelf(props(), 256)
    preview(s.to_image(), 3, os.path.join(out, "swamp_props.png"))
    print("ok")


# ============================================================================
# Item icons (16x16), in the style of gen_icons
# ============================================================================

ICON_OUT = hx("#1e1216")


def _icon():
    return Canvas(16, 16)


def _done(cv):
    cv.outline(ICON_OUT)
    return cv


def icon_toad_skin():
    cv = _icon()
    g = ramp("#223a16", "#34561e", "#4a7428", "#669436", "#8eb850")
    pts = [(2, 5), (6, 2), (12, 3), (14, 7), (12, 13), (6, 14), (2, 11)]
    from pixelkit import point_in_poly
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                inten = -(x - 8) / 8 * 0.5 - (y - 8) / 8 * 0.5 + math.sin(x * 1.7 + y) * 0.15
                cv.px(x, y, g[shade_index(inten, len(g), x, y, 0.35)])
    for (x, y) in ((5, 6), (9, 5), (11, 9), (7, 10), (4, 9)):
        cv.px(x, y, hx("#72328e"))
        cv.px(x + 1, y, hx("#4a1e62"))
    return _done(cv)


def icon_poison_gland():
    cv = _icon()
    p = ramp("#2a1238", "#4a1e62", "#72328e", "#9a52b4", "#c48ae0")
    shaded_ellipse(cv, 8, 8, 5.5, 5, p, dither=0.3)
    cv.px(6, 6, p[4]); cv.px(7, 6, p[4])
    for k in range(3):
        cv.px(8, 13 + k, hx("#6adf3a") if k < 2 else hx("#b6ff6a"))
    cv.px(10, 9, hx("#6adf3a"))
    return _done(cv)


def icon_leech_tooth():
    cv = _icon()
    b = S["bone"]
    for a in range(10):
        ang = a / 10 * math.tau
        x = int(round(8 + math.cos(ang) * 4.5))
        y = int(round(8 + math.sin(ang) * 4.5))
        cv.px(x, y, b[3])
        ix = int(round(8 + math.cos(ang) * 3.2))
        iy = int(round(8 + math.sin(ang) * 3.2))
        cv.px(ix, iy, b[1])
    shaded_ellipse(cv, 8, 8, 2.2, 2.2, ramp("#3a1012", "#5e1a1a", "#862a26"), dither=0.2)
    return _done(cv)


def icon_mud_core():
    cv = _icon()
    m = S["mud"]
    shaded_ellipse(cv, 8, 8.5, 5.8, 5.4, m, dither=0.35)
    for (x, y) in ((6, 7), (7, 8), (8, 8), (9, 9), (10, 10), (8, 7)):
        cv.px(x, y, hx("#ff8a2a") if (x + y) % 2 else hx("#ffe36a"))
    cv.px(5, 5, m[5])
    return _done(cv)


def icon_toad_crown():
    cv = _icon()
    g = P["gold"]
    for x in range(3, 13):
        cv.px(x, 11, g[2]); cv.px(x, 12, g[1]); cv.px(x, 10, g[3])
    for x0 in (3, 7, 11):
        for k in range(5):
            cv.px(x0, 10 - k, g[3] if k < 4 else g[5])
            cv.px(x0 + 1, 10 - k, g[2] if k < 4 else g[4])
    for x0, c in ((4, hx("#9a52b4")), (8, hx("#6adf3a")), (12, hx("#9a52b4"))):
        cv.px(x0, 5 if x0 != 8 else 4, c)
    return _done(cv)


def icon_snake_scale():
    cv = _icon()
    s = ramp("#0e2219", "#153325", "#1e4632", "#295c40", "#377550", "#4d9064", "#82c89a")
    from pixelkit import point_in_poly
    pts = [(8, 1), (14, 8), (8, 15), (2, 8)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                inten = -(x - 8) / 7 * 0.6 - (y - 8) / 7 * 0.6
                cv.px(x, y, s[shade_index(inten, len(s), x, y, 0.3)])
    for k in range(4):
        cv.px(6 + k // 2, 4 + k, s[6])
    return _done(cv)


def icon_snake_fang():
    cv = _icon()
    b = S["bone"]
    from pixelkit import point_in_poly
    pts = [(9, 1), (12, 3), (8, 14), (7, 14), (6, 6)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                t = y / 15
                cv.px(x, y, b[4] if x < 9 and t < 0.5 else b[3] if t < 0.7 else b[2])
    cv.px(7, 13, hx("#6adf3a")); cv.px(8, 13, hx("#b6ff6a")); cv.px(7, 14, hx("#6adf3a"))
    return _done(cv)


def icon_venom_sac():
    cv = _icon()
    g = ramp("#10301a", "#1e5a28", "#2e8a36", "#50b848", "#8ae06a", "#c8ff9a")
    shaded_ellipse(cv, 8, 9, 5, 5.2, g, dither=0.3)
    cv.px(6, 7, g[5]); cv.px(7, 6, g[5])
    for x in range(7, 10):
        cv.px(x, 3, hx("#5a3a24")); cv.px(x, 4, hx("#7a5234"))
    return _done(cv)


def icon_lotus():
    cv = _icon()
    pet = [hx("#7a3a6a"), hx("#b85a94"), hx("#e88ac0"), hx("#ffc4e4")]
    for a in range(7):
        ang = a / 7 * math.tau - math.pi / 2
        for k in range(1, 6):
            x = int(round(8 + math.cos(ang) * k))
            y = int(round(8 + math.sin(ang) * k * 0.8))
            cv.px(x, y, pet[max(0, 3 - k // 2)])
    shaded_ellipse(cv, 8, 8, 1.8, 1.6, [hx("#c8a030"), hx("#ffe07a"), hx("#fff4c4")], dither=0.2)
    for x in range(3, 13):
        cv.px(x, 13, S["lily"][2] if x % 3 else S["lily"][3])
    return _done(cv)


def icons():
    return [
        ("toad_skin", icon_toad_skin()), ("poison_gland", icon_poison_gland()), ("leech_tooth", icon_leech_tooth()),
        ("mud_core", icon_mud_core()), ("toad_crown", icon_toad_crown()), ("snake_scale", icon_snake_scale()),
        ("snake_fang", icon_snake_fang()), ("venom_sac", icon_venom_sac()), ("lotus", icon_lotus()),
    ]
