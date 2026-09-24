"""
World props: trees, bushes, rocks, logs, village buildings...
Each function returns (Canvas, pivot_px) where pivot is (x, y-from-top) of
the ground contact point, used for Y-sorting in Unity.
"""
import math
import random

from pixelkit import (Canvas, P, OUTLINE, hx, mix, bayer, shaded_ellipse, shade_index,
                      point_in_poly, LIGHT)

LEAF_OUT = hx("#0c1a12")
WOOD_OUT = hx("#1e120e")
STONE_OUT = hx("#15141c")


# ----------------------------------------------------------------------------
# Trees
# ----------------------------------------------------------------------------


def pine_tree(seed=0, scale=1.0):
    rnd = random.Random(seed)
    W, H = 34, 52
    cv = Canvas(W, H)
    cx = W // 2
    pr = P["pine"]
    # trunk
    tw = 4
    for y in range(40, 49):
        for x in range(cx - tw // 2, cx + tw // 2):
            c = P["wood"][2] if x < cx else P["wood"][1]
            if x == cx - tw // 2:
                c = P["wood"][3]
            cv.px(x, y, c)
    # roots
    cv.px(cx - 3, 48, P["wood"][1])
    cv.px(cx + 2, 48, P["wood"][1])
    tiers = [(3, 16, 7), (10, 26, 11), (18, 36, 14), (27, 44, 16)]
    for ti, (top, bot, hw) in enumerate(tiers):
        hw = int(hw * scale)
        for y in range(top, bot + 1):
            t = (y - top) / max(1, (bot - top))
            w = max(1.0, hw * (t ** 0.85))
            for x in range(int(cx - w - 1), int(cx + w + 2)):
                dx = (x + 0.5 - cx) / max(w, 1)
                if abs(dx) > 1.0:
                    continue
                # jagged bottom: drooping spikes
                if y >= bot - 2:
                    phase = (x + ti * 2) % 4
                    if y == bot and phase != 0:
                        continue
                    if y == bot - 1 and phase == 2:
                        continue
                # shading: left & top brighter
                inten = -dx * 0.75 + (0.35 - t) * 0.9
                # underside darkness near the bottom of each tier
                if t > 0.82:
                    inten -= 0.35
                idx = shade_index(inten, len(pr), x, y, dither=0.45, bias=0.08)
                cv.px(x, y, pr[idx])
        # a few needle highlights
        for _ in range(4 + ti):
            y = rnd.randint(top + 2, bot - 3)
            t = (y - top) / max(1, (bot - top))
            w = hw * (t ** 0.85)
            x = int(cx - w * rnd.uniform(0.2, 0.8))
            if cv.opaque(x, y):
                cv.px(x, y, pr[5])
                if cv.opaque(x + 1, y + 1):
                    cv.px(x + 1, y + 1, pr[4])
    # tip highlight
    cv.px(cx - 1, 3, pr[5])
    cv.outline(LEAF_OUT)
    return cv, (cx, 48)


def oak_tree(seed=0):
    rnd = random.Random(seed)
    W, H = 44, 48
    cv = Canvas(W, H)
    cx = W // 2
    lf = P["leaf"]
    wd = P["wood"]
    # trunk + roots
    for y in range(26, 44):
        half = 3 if y < 40 else 3 + (y - 39)
        for x in range(cx - half, cx + half):
            dx = (x + 0.5 - cx) / half
            idx = shade_index(-dx * 0.9, len(wd), x, y, dither=0.3, bias=-0.1)
            cv.px(x, y, wd[idx])
    # bark lines
    for y in range(28, 42, 3):
        cv.px(cx - 1 + (y % 2), y, wd[0])
    # canopy clumps (drawn back to front)
    clumps = [(-11, 21, 9), (11, 21, 9), (0, 23, 10), (-7, 13, 10), (7, 13, 10), (0, 9, 10),
              (-13, 15, 7), (13, 15, 7), (0, 17, 9)]
    for (ox, oy, r) in clumps:
        r2 = r + rnd.uniform(-0.6, 0.6)
        shaded_ellipse(cv, cx + ox, oy, r2, r2 * 0.92, lf, dither=0.4, bias=-0.04)
    # leaf texture: little dark notches + light dots
    for _ in range(70):
        x = rnd.randint(cx - 20, cx + 20)
        y = rnd.randint(2, 30)
        if cv.opaque(x, y) and cv.get(x, y)[:3] != wd[2][:3]:
            c = cv.get(x, y)
            # find index in ramp
            for i, rc in enumerate(lf):
                if rc[:3] == c[:3]:
                    if rnd.random() < 0.5 and i > 0:
                        cv.px(x, y, lf[i - 1])
                    elif i < len(lf) - 1:
                        cv.px(x, y, lf[i + 1])
                    break
    cv.outline(LEAF_OUT)
    return cv, (cx, 43)


def bush(seed=0, big=True):
    rnd = random.Random(seed)
    W, H = (26, 20) if big else (18, 14)
    cv = Canvas(W, H)
    cx = W / 2
    lf = P["leaf"]
    if big:
        clumps = [(-6, 12, 6), (6, 12, 6), (0, 9, 7), (-2, 13, 6), (3, 13, 6)]
    else:
        clumps = [(-3, 8, 4.5), (3, 8, 4.5), (0, 6, 5)]
    for (ox, oy, r) in clumps:
        shaded_ellipse(cv, cx + ox, oy, r, r * 0.9, lf[1:], dither=0.4, bias=0.12)
    # berries on some bushes
    if seed % 3 == 0:
        for _ in range(4 if big else 2):
            x, y = int(cx + rnd.randint(-8, 8)), rnd.randint(5, H - 5)
            if cv.opaque(x, y):
                cv.px(x, y, P["red"][3])
                cv.px(x + 1, y, P["red"][4])
    cv.outline(LEAF_OUT)
    return cv, (int(cx), H - 2)


# ----------------------------------------------------------------------------
# Rocks
# ----------------------------------------------------------------------------


def faceted_rock(W, H, seed, moss=False, cracks=2):
    rnd = random.Random(seed)
    cv = Canvas(W, H)
    st = P["stone"]
    cx, cy = W / 2, H * 0.55
    rx, ry = W * 0.46, H * 0.42
    # base silhouette with a flat bottom
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            wob = 0.06 * math.sin(x * 1.3 + seed) + 0.05 * math.cos(y * 1.7 + seed)
            if dx * dx + dy * dy > 1.0 + wob:
                continue
            if y > H - 3:
                continue
            # facets: top plane, left plane, right plane
            nz = math.sqrt(max(0, 1 - dx * dx - dy * dy))
            # quantize the normal to get flat facets
            qx = round(dx * 2.2) / 2.2
            qy = round(dy * 2.2) / 2.2
            inten = qx * LIGHT[0] + qy * LIGHT[1] + nz * LIGHT[2]
            idx = shade_index(inten, len(st) - 1, x, y, dither=0.2, bias=0.05)
            cv.px(x, y, st[idx + 1 if idx < len(st) - 2 else idx])
    # cracks
    for _ in range(cracks):
        x = rnd.randint(int(cx - rx * 0.5), int(cx + rx * 0.5))
        y = rnd.randint(int(cy - ry * 0.5), int(cy + ry * 0.3))
        for k in range(rnd.randint(2, 4)):
            if cv.opaque(x, y):
                cv.px(x, y, st[1])
                if cv.opaque(x + 1, y):
                    cv.px(x + 1, y, st[4])
            x += rnd.choice([-1, 0, 1])
            y += 1
    # top highlight rim
    for x in range(W):
        for y in range(H):
            if cv.opaque(x, y):
                if not cv.opaque(x, y - 1) and x < cx + rx * 0.3:
                    cv.px(x, y, st[5])
                break
    if moss:
        mo = P["moss"]
        for x in range(W):
            for y in range(H):
                if cv.opaque(x, y):
                    depth = int(2 + 2.2 * math.sin(x * 0.6 + seed) + rnd.random() * 1.2)
                    if abs(x + 0.5 - cx) < rx * 0.85:
                        for k in range(max(0, depth)):
                            if cv.opaque(x, y + k):
                                cv.px(x, y + k, mo[3] if k == 0 else (mo[2] if k < depth - 1 else mo[1]))
                    break
    cv.outline(STONE_OUT)
    return cv, (W // 2, H - 3)


def boulder(seed=0):
    return faceted_rock(28, 24, seed, moss=True, cracks=3)


def rock_big(seed=0):
    return faceted_rock(22, 18, seed, moss=seed % 2 == 0, cracks=2)


def rock_small(seed=0):
    return faceted_rock(12, 10, seed, moss=False, cracks=0)


# ----------------------------------------------------------------------------
# Wood things
# ----------------------------------------------------------------------------


def log(seed=0):
    W, H = 32, 16
    cv = Canvas(W, H)
    wd = P["wood"]
    y0, y1 = 3, 13
    for y in range(y0, y1):
        t = (y - y0) / (y1 - y0)
        for x in range(4, W - 1):
            inten = 0.6 - t * 1.6
            idx = shade_index(inten, len(wd) - 1, x, y, dither=0.3)
            cv.px(x, y, wd[idx])
    # bark grooves
    rnd = random.Random(seed)
    for _ in range(9):
        x = rnd.randint(7, W - 4)
        y = rnd.randint(y0 + 1, y1 - 2)
        cv.hline(x, x + rnd.randint(2, 4), y, wd[0])
    # end cap with rings
    for y in range(y0, y1):
        for x in range(0, 9):
            dx = (x + 0.5 - 5) / 4.2
            dy = (y + 0.5 - 8) / 5.0
            d = dx * dx + dy * dy
            if d <= 1:
                ring = int(math.sqrt(d) * 3.2)
                cv.px(x, y, [P["cream"][2], wd[5], wd[4], wd[3]][min(ring, 3)])
    cv.px(5, 8, wd[2])
    # moss on top
    for x in range(10, 24):
        if (x * 7) % 5 < 3:
            cv.px(x, y0, P["moss"][3])
            if x % 3 == 0:
                cv.px(x, y0 + 1, P["moss"][2])
    cv.outline(WOOD_OUT)
    return cv, (W // 2, y1)


def stump(seed=0):
    W, H = 18, 16
    cv = Canvas(W, H)
    wd = P["wood"]
    cx = W / 2
    for y in range(5, 14):
        for x in range(2, W - 2):
            dx = (x + 0.5 - cx) / (cx - 2)
            idx = shade_index(-dx * 0.9 - 0.1, len(wd) - 1, x, y, dither=0.3)
            cv.px(x, y, wd[idx])
    # roots
    for (x, y) in [(1, 13), (2, 12), (W - 2, 13), (W - 3, 12), (cx - 1, 14), (cx, 14)]:
        cv.px(int(x), int(y), wd[2])
    # top
    for y in range(2, 9):
        for x in range(2, W - 2):
            dx = (x + 0.5 - cx) / (cx - 2)
            dy = (y + 0.5 - 5) / 3.2
            d = dx * dx + dy * dy
            if d <= 1:
                ring = int(math.sqrt(d) * 3.5)
                cv.px(x, y, [wd[4], P["cream"][2], wd[5], wd[4]][min(ring, 3)])
    cv.px(int(cx), 5, wd[3])
    cv.outline(WOOD_OUT)
    return cv, (int(cx), 13)


def mushroom_cluster(seed=0):
    W, H = 16, 14
    cv = Canvas(W, H)
    r = P["shroom_red"]
    c = P["cream"]
    for (x, y, s) in [(5, 7, 3), (10, 9, 2), (3, 10, 2)]:
        # stem
        for k in range(s + 1):
            cv.px(x, y + k, c[3] if k < s else c[2])
            cv.px(x + 1, y + k, c[2])
        # cap
        shaded_ellipse(cv, x + 1, y, s + 1.5, s * 0.8 + 0.5, r[1:], dither=0.3,
                       clip=lambda px, py, yy=y: py <= yy)
        cv.px(x, y - s // 2 - 1, (255, 245, 235, 255))
        cv.px(x + 2, y - 1, (255, 245, 235, 255))
    cv.outline(hx("#2a1216"))
    return cv, (W // 2, H - 2)


def campfire_base():
    W, H = 20, 14
    cv = Canvas(W, H)
    st = P["stone"]
    wd = P["wood"]
    # crossed logs
    cv.line(4, 10, 15, 6, wd[3], 2)
    cv.line(4, 6, 15, 10, wd[2], 2)
    cv.px(4, 10, wd[5])
    cv.px(15, 6, wd[5])
    # stones ring
    for i in range(8):
        a = i / 8 * math.tau
        x = 10 + math.cos(a) * 8
        y = 8 + math.sin(a) * 4
        shaded_ellipse(cv, x, y, 2.2, 1.8, st[1:], dither=0.2)
    # embers
    cv.px(9, 8, P["fire"][4])
    cv.px(11, 8, P["fire"][3])
    cv.outline(STONE_OUT)
    return cv, (10, 10)


def campfire_flame(frame):
    W, H = 16, 20
    cv = Canvas(W, H)
    f = P["fire"]
    rnd = random.Random(frame * 13 + 1)
    cx = W / 2
    tongues = [(-3, 9), (0, 14), (3, 10)]
    for (ox, hgt) in tongues:
        hgt += rnd.randint(-2, 2)
        sway = [0, 1, 0, -1][(frame + ox) % 4]
        for k in range(hgt):
            t = k / hgt
            w = (1 - t) ** 0.7 * 3.2
            yy = H - 2 - k
            for x in range(int(cx + ox - w - 1), int(cx + ox + w + 2)):
                sx = x - sway * t * 1.5
                dx = abs(sx + 0.5 - (cx + ox)) / max(w, 0.5)
                if dx > 1:
                    continue
                heat = (1 - dx) * (1 - t * 0.8)
                idx = min(len(f) - 1, int(heat * 5.2) + 1)
                cv.px(x, yy, f[idx])
    # sparks
    for _ in range(2):
        cv.px(int(cx + rnd.randint(-4, 4)), rnd.randint(1, 6), f[5])
    return cv, (8, 18)


def tent():
    W, H = 36, 30
    cv = Canvas(W, H)
    cr = P["cream"]
    rd = P["red"]
    apex = (18, 3)
    left, right = (2, 26), (34, 26)
    for y in range(H):
        for x in range(W):
            if point_in_poly(x + 0.5, y + 0.5, [apex, right, left]):
                # stripes and shading
                t = (x - 2) / 32
                inten = 0.5 - t * 1.2
                ramp = cr if ((x - 18) // 3) % 3 else rd[1:]
                idx = shade_index(inten, len(ramp), x, y, dither=0.3)
                cv.px(x, y, ramp[idx])
    # entrance
    for y in range(12, 27):
        w = (y - 12) * 0.38
        for x in range(int(18 - w), int(18 + w) + 1):
            cv.px(x, y, hx("#20141a"))
    # flap highlight
    cv.line(18, 12, 12, 26, cr[3])
    # poles/ropes
    cv.px(18, 2, P["wood"][3])
    cv.px(18, 1, P["wood"][4])
    cv.line(2, 26, 0, 28, P["wood"][2])
    cv.line(34, 26, 35, 28, P["wood"][2])
    cv.outline(WOOD_OUT)
    return cv, (18, 26)


def hut():
    W, H = 52, 50
    cv = Canvas(W, H)
    wd = P["wood"]
    gd = P["gold"]
    # walls (planks)
    for y in range(24, 46):
        for x in range(6, 46):
            row = (y - 24) // 3
            c = wd[3] if row % 2 == 0 else wd[2]
            if (y - 24) % 3 == 2:
                c = wd[1]
            if x in (6, 7) or x in (44, 45):
                c = wd[1] if x in (7, 44) else wd[0]
            cv.px(x, y, c)
    # posts
    for x0 in (6, 44):
        for y in range(22, 46):
            cv.px(x0, y, wd[4])
            cv.px(x0 + 1, y, wd[2])
    # door
    for y in range(32, 46):
        for x in range(21, 31):
            c = wd[1] if (x - 21) % 3 else wd[0]
            cv.px(x, y, c)
    cv.px(28, 39, gd[3])
    cv.hline(21, 30, 31, wd[4])
    # windows (warm light)
    for wx in (11, 35):
        for y in range(29, 36):
            for x in range(wx, wx + 6):
                cv.px(x, y, gd[4] if (y < 32) else gd[3])
        cv.hline(wx - 1, wx + 6, 28, wd[4])
        cv.hline(wx - 1, wx + 6, 36, wd[1])
        cv.vline(wx + 2, 29, 35, wd[1])
        cv.hline(wx, wx + 5, 32, wd[1])
    # thatched roof
    straw = [hx("#4a2e14"), hx("#6e4a1e"), hx("#957028"), hx("#b89238"), hx("#d8b458"), hx("#f0d27e")]
    for y in range(4, 28):
        t = (y - 4) / 24
        half = 10 + t * 16
        for x in range(int(26 - half), int(26 + half) + 1):
            dx = (x + 0.5 - 26) / half
            inten = -dx * 0.5 + 0.2 - (0.6 if y > 24 else 0)
            # straw strands
            if (x * 3 + y * 5) % 7 == 0:
                inten -= 0.4
            idx = shade_index(inten, len(straw), x, y, dither=0.35)
            cv.px(x, y, straw[idx])
    # roof bottom fringe
    for x in range(0, W):
        if cv.opaque(x, 27) and x % 2 == 0:
            cv.px(x, 28, straw[1])
    # ridge
    cv.hline(16, 36, 4, straw[5])
    cv.hline(17, 35, 3, straw[3])
    # chimney
    for y in range(0, 10):
        for x in range(34, 39):
            cv.px(x, y, P["stone"][3] if x < 36 else P["stone"][2])
    cv.hline(33, 39, 0, P["stone"][4])
    cv.outline(WOOD_OUT)
    return cv, (26, 45)


def fence_h():
    W, H = 16, 14
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in (4, 8):
        cv.hline(0, 15, y, wd[4])
        cv.hline(0, 15, y + 1, wd[2])
    for y in range(1, 13):
        cv.px(1, y, wd[4])
        cv.px(2, y, wd[3])
        cv.px(3, y, wd[1])
    cv.px(2, 0, wd[5])
    cv.outline(WOOD_OUT)
    return cv, (8, 12)


def fence_post():
    W, H = 6, 14
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in range(1, 13):
        cv.px(1, y, wd[4])
        cv.px(2, y, wd[3])
        cv.px(3, y, wd[1])
    cv.px(2, 0, wd[5])
    cv.outline(WOOD_OUT)
    return cv, (2, 12)


def signpost():
    W, H = 18, 22
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in range(8, 21):
        cv.px(8, y, wd[3])
        cv.px(9, y, wd[1])
    for y in range(2, 10):
        for x in range(1, 17):
            cv.px(x, y, wd[4] if y < 4 else (wd[3] if y < 8 else wd[2]))
    for y in (4, 6):
        cv.hline(4, 13, y, wd[1])
    # arrow tip
    cv.px(17, 5, wd[3])
    cv.px(17, 6, wd[3])
    cv.outline(WOOD_OUT)
    return cv, (9, 20)


def crate():
    W, H = 16, 16
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in range(2, 15):
        for x in range(1, 15):
            c = wd[3] if y > 4 else wd[4]
            if x in (1, 14) or y in (5, 14):
                c = wd[1]
            cv.px(x, y, c)
    cv.line(2, 6, 13, 13, wd[2])
    cv.line(2, 13, 13, 6, wd[2])
    cv.outline(WOOD_OUT)
    return cv, (8, 14)


def barrel():
    W, H = 14, 18
    cv = Canvas(W, H)
    wd = P["wood"]
    mt = P["metal"]
    for y in range(3, 17):
        bulge = 1 if 6 <= y <= 13 else 0
        for x in range(2 - bulge, 12 + bulge):
            dx = (x + 0.5 - 7) / 5.5
            idx = shade_index(-dx * 0.9, len(wd) - 1, x, y, dither=0.3)
            cv.px(x, y, wd[idx])
    for y in (5, 13):
        for x in range(1, 13):
            if cv.opaque(x, y):
                cv.px(x, y, mt[2] if x < 7 else mt[1])
    for x in range(3, 11):
        cv.px(x, 3, wd[5] if x < 7 else wd[4])
        cv.px(x, 2, wd[3])
    cv.outline(WOOD_OUT)
    return cv, (7, 16)


def treasure_chest(opened=False):
    """A boss's treasure chest: dark wood, gold bands and a lock; opened, the lid stands back and gold shines inside."""
    W, H = 24, 24
    cv = Canvas(W, H)
    wd = P["wood"]
    gd = P["gold"]
    x0, x1 = 2, 21
    # the box
    top = 13
    for y in range(top, 22):
        for x in range(x0, x1 + 1):
            dx = (x + 0.5 - 12) / 10
            c = wd[shade_index(-dx * 0.7 + (0.25 if y < 15 else 0), len(wd) - 2, x, y, dither=0.25) + 1]
            if y == 21:
                c = wd[1]
            cv.px(x, y, c)
    # planks
    for y in (16, 19):
        cv.hline(x0 + 1, x1 - 1, y, wd[2])
    # gold bands down the front and the corners
    for x in (x0, x0 + 1, x1 - 1, x1, 7, 16):
        for y in range(top, 22):
            cv.px(x, y, gd[3] if x < 12 else gd[2])
    if opened:
        # inside: the back of the box, a heap of coins catching the light
        for y in range(top - 3, top + 1):
            for x in range(x0 + 1, x1):
                cv.px(x, y, wd[0])
        for x in range(x0 + 2, x1 - 1):
            h = 2 + (1 if (x * 7) % 5 < 2 else 0)
            for y in range(top - h + 1, top + 1):
                cv.px(x, y, gd[5] if (x + y) % 3 == 0 else gd[4] if (x + y) % 3 == 1 else gd[3])
        # the lid leaning back
        for y in range(3, top - 3):
            for x in range(x0 + 1, x1):
                t = (y - 3) / max(1, top - 7)
                cv.px(x, y, wd[2 + int(t * 2)])
        cv.hline(x0 + 1, x1 - 1, 3, gd[3])
        for x in (x0 + 1, 7, 16, x1 - 1):
            for y in range(3, top - 3):
                cv.px(x, y, gd[2])
        # sparkles
        for (sx, sy) in ((6, 7), (15, 5), (11, 9)):
            cv.px(sx, sy, gd[5])
    else:
        # the domed lid
        for y in range(6, top):
            bulge = 0 if y >= 8 else (1 if y == 7 else 2)
            for x in range(x0 + bulge, x1 + 1 - bulge):
                dx = (x + 0.5 - 12) / 10
                dy = (y - 6) / 7
                c = wd[shade_index(-dx * 0.6 - (1 - dy) * 0.5, len(wd) - 2, x, y, dither=0.25) + 1]
                cv.px(x, y, c)
        cv.hline(x0, x1, top - 1, gd[2])
        for x in (x0 + 1, 7, 16, x1 - 1):
            for y in range(6 if x in (7, 16) else 8, top):
                cv.px(x, y, gd[4] if x < 12 else gd[3])
    # the lock
    for y in range(top - 1, top + 4):
        for x in range(10, 14):
            cv.px(x, y, gd[4] if x < 12 else gd[3])
    cv.px(11, top + 1, wd[0])
    cv.px(11, top + 2, wd[0])
    cv.outline(WOOD_OUT)
    return cv, (12, 21)


def ruin_pillar(seed=0):
    W, H = 18, 36
    cv = Canvas(W, H)
    st = P["stone"]
    top = 6 + seed * 3
    for y in range(top, 32):
        for x in range(3, 15):
            dx = (x + 0.5 - 9) / 6
            idx = shade_index(-dx * 0.8 + 0.1, len(st) - 1, x, y, dither=0.3)
            c = st[idx + 1] if idx < len(st) - 2 else st[idx]
            if (y - top) % 7 == 6:
                c = st[1]
            cv.px(x, y, c)
    # broken top: a jagged diagonal break
    rnd = random.Random(seed * 7 + 3)
    for x in range(3, 15):
        k = int((x - 3) * (0.45 if seed == 0 else -0.45) + (0 if seed == 0 else 6)) + rnd.randint(0, 2)
        for y in range(top, top + max(0, k)):
            cv.clear(x, y)
    # base
    for y in range(30, 34):
        for x in range(1, 17):
            cv.px(x, y, st[3] if y == 30 else st[2])
    # runes glowing faintly
    for (x, y) in [(8, top + 10), (9, top + 11), (8, top + 12), (10, top + 12)]:
        cv.px(x, y, hx("#7fe0c8"))
    # moss
    for x in range(3, 15):
        if (x * 5 + seed) % 4 == 0:
            cv.px(x, 29, P["moss"][3])
            cv.px(x, 28, P["moss"][2])
    cv.outline(STONE_OUT)
    return cv, (9, 33)


def well():
    W, H = 26, 30
    cv = Canvas(W, H)
    st = P["stone"]
    wd = P["wood"]
    # stone ring
    for y in range(16, 28):
        for x in range(2, 24):
            dx = (x + 0.5 - 13) / 11
            idx = shade_index(-dx * 0.8, len(st) - 1, x, y, dither=0.3)
            c = st[idx + 1] if idx < len(st) - 2 else st[idx]
            if (x + (y // 3) * 2) % 5 == 0:
                c = st[1]
            cv.px(x, y, c)
    for x in range(3, 23):
        for y in range(14, 18):
            dx = (x + 0.5 - 13) / 10
            dy = (y + 0.5 - 16) / 2.2
            if dx * dx + dy * dy <= 1:
                cv.px(x, y, hx("#122038") if dx * dx + dy * dy < 0.6 else st[4])
    # roof posts and roof
    for y in range(4, 17):
        cv.px(3, y, wd[3])
        cv.px(22, y, wd[2])
    for y in range(1, 6):
        for x in range(0, 26):
            if abs(x + 0.5 - 13) <= 8 + y * 1.1:
                cv.px(x, y, P["red"][2] if y < 3 else P["red"][1])
    cv.hline(5, 20, 1, P["red"][3])
    cv.outline(WOOD_OUT)
    return cv, (13, 27)


def lantern_post():
    W, H = 10, 26
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in range(8, 25):
        cv.px(4, y, wd[3])
        cv.px(5, y, wd[1])
    cv.hline(4, 8, 6, wd[3])
    cv.px(8, 7, wd[2])
    for y in range(8, 13):
        for x in range(6, 10):
            cv.px(x, y, P["gold"][4] if 1 <= x - 6 <= 2 and 9 <= y <= 11 else P["metal"][1])
    cv.outline(WOOD_OUT)
    return cv, (4, 24)


def build():
    """Returns list of (name, Canvas, pivot)."""
    items = []
    for i in range(3):
        c, p = pine_tree(seed=i, scale=1.0 - i * 0.08)
        items.append((f"pine_{i}", c, p))
    for i in range(2):
        c, p = oak_tree(seed=i + 5)
        items.append((f"oak_{i}", c, p))
    for i in range(2):
        c, p = bush(seed=i, big=True)
        items.append((f"bush_big_{i}", c, p))
        c, p = bush(seed=i + 1, big=False)
        items.append((f"bush_small_{i}", c, p))
    c, p = boulder(1); items.append(("boulder", c, p))
    for i in range(2):
        c, p = rock_big(i + 2); items.append((f"rock_big_{i}", c, p))
        c, p = rock_small(i + 4); items.append((f"rock_small_{i}", c, p))
    c, p = log(); items.append(("log", c, p))
    c, p = stump(); items.append(("stump", c, p))
    c, p = mushroom_cluster(); items.append(("mushrooms", c, p))
    c, p = campfire_base(); items.append(("campfire", c, p))
    for f in range(4):
        c, p = campfire_flame(f); items.append((f"flame_{f}", c, p))
    c, p = tent(); items.append(("tent", c, p))
    c, p = hut(); items.append(("hut", c, p))
    c, p = fence_h(); items.append(("fence_h", c, p))
    c, p = fence_post(); items.append(("fence_post", c, p))
    c, p = signpost(); items.append(("signpost", c, p))
    c, p = crate(); items.append(("crate", c, p))
    c, p = barrel(); items.append(("barrel", c, p))
    c, p = treasure_chest(False); items.append(("chest_closed", c, p))
    c, p = treasure_chest(True); items.append(("chest_open", c, p))
    for i in range(2):
        c, p = ruin_pillar(i); items.append((f"pillar_{i}", c, p))
    c, p = well(); items.append(("well", c, p))
    c, p = lantern_post(); items.append(("lantern", c, p))
    return items
