"""
Creatures of Đầm Lầy Sương Mù: Cóc Độc (poison toad), Đỉa Bùn (mud leech), Người Bùn
(mud golem, splits into Bùn Con), the mini-boss Cóc Tía (purple toad king) and the boss
Xà Mẫu Đầm Lầy (the snake mother). Procedural shaded shapes, drawn like gen_enemies.
Every build_* returns {animation: [Canvas frames]}.
"""
import math
import random

import numpy as np

from pixelkit import Canvas, P, OUTLINE, hx, ramp, shaded_ellipse, shade_index, LIGHT
from gen_enemies import shaded_capsule, layer, comp, slime

OUT = hx("#0c1210")

C = {
    "toad": ramp("#15240f", "#223a16", "#34561e", "#4a7428", "#669436", "#8eb850"),
    "toad_belly": ramp("#5a5230", "#8a7e48", "#b8aa66", "#dcd08a"),
    "wart": ramp("#2a1238", "#4a1e62", "#72328e", "#9a52b4"),
    "ktoad": ramp("#170a22", "#26103a", "#3a1a56", "#542878", "#723c9c", "#9458c0", "#b882de"),
    "ktoad_belly": ramp("#4a3a4a", "#7a6478", "#a8909e", "#d2bcc4"),
    "kwart": ramp("#3a2a08", "#6a4a10", "#a0761c", "#d4a42e", "#ffd452"),
    "leech": ramp("#0e0a0a", "#1a1311", "#281d18", "#382921", "#4a372b", "#5e4636"),
    "leech_belly": ramp("#3a1012", "#5e1a1a", "#862a26", "#ac4034"),
    "mud": ramp("#1a1210", "#281b15", "#382619", "#4a3320", "#5e432b", "#765737"),
    "snake": ramp("#08140f", "#0e2219", "#153325", "#1e4632", "#295c40", "#377550", "#4d9064", "#6aac7a"),
    "snake_belly": ramp("#4a3e16", "#766224", "#a88c36", "#d4b44c", "#f0d470"),
    "hood": ramp("#1a0e1e", "#2c1630", "#44203e", "#62304e", "#86465e"),
}
EYE_GLOW = (hx("#ffe36a"), hx("#ff8a2a"))
VENOM = (hx("#6adf3a"), hx("#b6ff6a"))


# ============================================================================
# Cóc Độc
# ============================================================================

def toad(FW=24, FH=24, base=21, scale=1.0, squash=0.0, lift=0, mouth=0, eyes="open", puff=0.0,
         tone="toad", belly="toad_belly", wart="wart", crown=False, tongue=0):
    cv = Canvas(FW, FH)
    body = C[tone]
    bl = C[belly]
    wt = C[wart]
    s = scale
    cx = FW / 2
    b = base - lift
    rx = 8.2 * s * (1 + squash * 0.25)
    ry = 5.6 * s * (1 - squash * 0.3)
    cy = b - ry + 0.5
    # back legs (bent, to the sides)
    lg = layer(FW, FH)
    for sgn in (-1, 1):
        lx = cx + sgn * rx * 0.85
        shaded_ellipse(lg, lx, b - 2 * s, 3.2 * s, 2.4 * s, body[:5], dither=0.3)
        for k in range(3):
            lg.px(int(lx + sgn * (1 + k) * s), int(b), body[1])
    comp(cv, lg, OUT)
    bd = layer(FW, FH)
    shaded_ellipse(bd, cx, cy, rx, ry, body, dither=0.35, bias=0.02)
    # pale belly and throat
    for y in range(int(cy), int(b + 1)):
        for x in range(int(cx - rx * 0.6), int(cx + rx * 0.6) + 1):
            dx = (x + 0.5 - cx) / (rx * 0.6)
            dy = (y + 0.5 - (cy + ry * 0.55)) / (ry * 0.55)
            if dx * dx + dy * dy <= 1 and bd.opaque(x, y):
                bd.px(x, y, bl[shade_index(-dx * 0.5 - dy * 0.3 + 0.3, len(bl), x, y, 0.3)])
    if puff > 0:
        shaded_ellipse(bd, cx, cy + ry * 0.55, rx * 0.45 * (1 + puff * 0.6), ry * 0.4 * (1 + puff * 0.7), bl, dither=0.2, bias=0.15)
    # warts
    rnd = random.Random(3)
    for _ in range(int(7 * s)):
        wx = cx + rnd.uniform(-rx * 0.75, rx * 0.75)
        wy = cy - ry * rnd.uniform(0.1, 0.8)
        if bd.opaque(int(wx), int(wy)):
            bd.px(int(wx), int(wy), wt[2])
            bd.px(int(wx) + 1, int(wy), wt[1])
            if s > 1.4:
                bd.px(int(wx), int(wy) - 1, wt[3])
    if crown:
        # a ring of golden warts: the king
        for k in range(5):
            wx = cx - 6 * s + k * 3 * s
            wy = cy - ry * 0.95 + abs(k - 2) * 0.8
            for dy in range(int(2 + s)):
                bd.px(int(wx), int(wy) - dy, wt[3] if dy else wt[2])
                bd.px(int(wx) + 1, int(wy) - dy, wt[4] if dy == int(1 + s) else wt[3])
    # front legs
    for sgn in (-1, 1):
        fx = cx + sgn * rx * 0.45
        for k in range(int(3 * s)):
            bd.px(int(fx), int(b - k), body[2])
            bd.px(int(fx + sgn), int(b - k), body[3])
        bd.px(int(fx - sgn), int(b), body[1])
        bd.px(int(fx + sgn * 2), int(b), body[1])
    comp(cv, bd, OUT)
    # eyes on top of the head
    hd = layer(FW, FH)
    ey = int(cy - ry * 0.75)
    for sgn in (-1, 1):
        ex = cx + sgn * rx * 0.5
        shaded_ellipse(hd, ex, ey, 2.3 * s, 2.1 * s, body[2:], dither=0.2, bias=0.2)
        ix, iy = int(ex), int(ey)
        if eyes == "open":
            hd.px(ix, iy, hx("#f4d84a"))
            hd.px(ix - 1, iy, hx("#f4d84a"))
            hd.px(ix, iy, OUTLINE)
            if s > 1.4:
                hd.px(ix + 1, iy, hx("#f4d84a"))
                hd.px(ix, iy - 1, hx("#f4d84a"))
        elif eyes == "hurt":
            hd.px(ix - 1, iy, OUTLINE); hd.px(ix, iy, OUTLINE); hd.px(ix + 1, iy, OUTLINE)
        else:
            hd.px(ix - 1, iy - 1, OUTLINE); hd.px(ix + 1, iy + 1, OUTLINE)
            hd.px(ix + 1, iy - 1, OUTLINE); hd.px(ix - 1, iy + 1, OUTLINE)
    comp(cv, hd, OUT)
    # mouth: a wide line, open for spitting (and a tongue)
    my = int(cy + ry * 0.05)
    mw = int(rx * 0.55)
    if mouth == 0:
        for x in range(int(cx - mw), int(cx + mw) + 1):
            cv.px(x, my, body[0])
    else:
        for y in range(my, my + mouth + 1):
            for x in range(int(cx - mw + (y - my)), int(cx + mw - (y - my)) + 1):
                cv.px(x, y, hx("#5a1030") if y > my else hx("#2a0818"))
        if tongue > 0:
            for k in range(tongue):
                cv.px(int(cx), my + mouth + k, hx("#e0607a") if k < tongue - 1 else hx("#ff90a8"))
                cv.px(int(cx) + 1, my + mouth + k, hx("#b04058"))
    return cv


def toad_flat(FW=24, FH=24, base=21, scale=1.0, tone="toad"):
    cv = Canvas(FW, FH)
    bd = layer(FW, FH)
    shaded_ellipse(bd, FW / 2, base - 2 * scale, 9 * scale, 3 * scale, C[tone][1:], dither=0.3)
    for sgn in (-1, 1):
        bd.px(int(FW / 2 + sgn * 3 * scale), int(base - 3 * scale), OUTLINE)
    comp(cv, bd, OUT)
    return cv


def build_toad():
    A = {}
    A["idle"] = [toad(), toad(puff=0.6)]
    A["move"] = [toad(squash=0.6), toad(squash=-0.3, lift=3), toad(squash=-0.2, lift=5), toad(squash=0.4, lift=1)]
    A["attack"] = [toad(squash=0.5, puff=1.0), toad(mouth=2, squash=-0.2), toad(mouth=1)]
    A["hurt"] = [toad(eyes="hurt", squash=0.3)]
    A["dead"] = [toad(eyes="x", squash=0.8), toad_flat()]
    return A


# ============================================================================
# Cóc Tía (mini-boss)
# ============================================================================

KW, KH, KB = 48, 48, 45


def king(**k):
    return toad(KW, KH, KB, 2.1, tone="ktoad", belly="ktoad_belly", wart="kwart", crown=True, **k)


def build_toad_king():
    A = {}
    A["idle"] = [king(), king(puff=0.7)]
    A["walk"] = [king(squash=0.5), king(squash=-0.3, lift=5), king(squash=-0.2, lift=8), king(squash=0.4, lift=2)]
    A["windup"] = [king(squash=0.7, puff=0.4), king(squash=0.9, puff=0.8)]
    A["slam"] = [king(squash=1.0, mouth=2), king(squash=0.6, mouth=1)]
    A["air"] = [king(squash=-0.4, lift=12)]
    A["tongue"] = [king(mouth=4, tongue=6, squash=-0.1), king(mouth=4, tongue=12, squash=-0.2)]
    A["spit"] = [king(puff=1.2, squash=0.3), king(mouth=4, squash=-0.2)]
    A["roar"] = [king(mouth=4, puff=1.0, squash=-0.3), king(mouth=5, puff=1.3, squash=-0.4)]
    A["hurt"] = [king(eyes="hurt", squash=0.3)]
    A["dead"] = [king(eyes="x", squash=0.9), toad_flat(KW, KH, KB, 2.1, tone="ktoad")]
    return A


# ============================================================================
# Đỉa Bùn
# ============================================================================

def leech(phase=0.0, rise=0.0, stretch=0.0, submerged=0.0, eyes="open", curl=False):
    """A segmented leech. rise: the front rears up (0..1); stretch: lunging forward;
    submerged: sunk in the mud, only a hump and the eyes show."""
    FW, FH, base = 24, 24, 21
    cv = Canvas(FW, FH)
    lc = C["leech"]
    bl = C["leech_belly"]
    n = 6
    segs = []
    length = 13 + stretch * 6
    for i in range(n):
        t = i / (n - 1)
        x = FW / 2 - length / 2 + t * length
        wave = math.sin(t * math.pi * 2 + phase) * (1.6 - stretch)
        y = base - 3 + wave * 0.6
        if curl:
            ang = t * math.pi * 1.4
            x = FW / 2 + math.cos(ang) * 5
            y = base - 4 - math.sin(ang) * 3
        if rise > 0 and i >= n - 2:
            y -= rise * (4 + (i - (n - 2)) * 3)
        r = 3.4 - abs(t - 0.55) * 1.6
        segs.append((x, y, r))
    bd = layer(FW, FH)
    for (x, y, r) in segs:
        sub = submerged * 2.5
        shaded_ellipse(bd, x, y + sub * 0.6, r, r * 0.8 * (1 - submerged * 0.5), lc, dither=0.3, bias=0.05)
    # the red underside along the bottom edge
    for x in range(FW):
        for y in range(FH - 1, -1, -1):
            if bd.opaque(x, y):
                if submerged < 0.5:
                    bd.px(x, y, bl[1])
                    if bd.opaque(x, y - 1):
                        bd.px(x, y - 1, bl[2])
                break
    # ring marks between segments
    for (x, y, r) in segs[1:-1]:
        for k in range(-1, 2):
            if bd.opaque(int(x), int(y + k)):
                bd.px(int(x), int(y + k), lc[1])
    comp(cv, bd, OUT)
    # head: the last segment, with a round mouth and two tiny eyes
    hx0, hy0, hr = segs[-1]
    hy0 += submerged * 1.5
    if eyes == "open":
        cv.px(int(hx0 - 1), int(hy0 - hr * 0.5), hx("#ff6a3a"))
        cv.px(int(hx0 + 1), int(hy0 - hr * 0.5), hx("#ff6a3a"))
    elif eyes == "x":
        cv.px(int(hx0), int(hy0 - hr * 0.5), OUTLINE)
    if stretch > 0.3 or rise > 0.5:
        # the sucker, open
        for a in range(8):
            ang = a / 8 * math.tau
            cv.px(int(round(hx0 + 1.6 + math.cos(ang) * 1.3)), int(round(hy0 + math.sin(ang) * 1.3)), bl[3])
        cv.px(int(hx0 + 1.6), int(hy0), hx("#2a0810"))
    if submerged > 0:
        # mud around it
        md = C["mud"]
        for x in range(3, FW - 3):
            if cv.opaque(x, base - 2) or cv.opaque(x, base - 1):
                cv.px(x, base - 1, md[3] if x % 3 else md[4])
                cv.px(x, base, md[2])
    return cv


def build_leech():
    A = {}
    A["idle"] = [leech(submerged=0.8), leech(phase=1.2, submerged=0.7)]
    A["move"] = [leech(phase=0), leech(phase=2.1), leech(phase=4.2)]
    A["attack"] = [leech(rise=0.6), leech(rise=1.0), leech(stretch=1.0)]
    A["hurt"] = [leech(eyes="x", phase=1)]
    A["dead"] = [leech(eyes="x", curl=True), leech(eyes="x", curl=True, submerged=0.9)]
    return A


# ============================================================================
# Người Bùn and Bùn Con
# ============================================================================

def mudman(bob=0, arms=20, slam=0.0, lean=0, legs=(0, 0), eyes="glow", melt=0.0):
    FW, FH, base = 32, 32, 30
    cv = Canvas(FW, FH)
    md = C["mud"]
    cx = FW / 2 + lean
    rx, ry = 8.5 * (1 + melt * 0.5), 9.5 * (1 - melt * 0.55)
    cy = base - 4 - ry + bob + melt * 4
    lg = layer(FW, FH)
    for i, sgn in enumerate((-1, 1)):
        lx = cx + sgn * 4
        if melt < 0.5:
            shaded_capsule(lg, lx, cy + ry - 2, lx, base - 1 - legs[i], 3.2, md, bias=-0.05)
    comp(cv, lg, OUT)
    bd = layer(FW, FH)
    shaded_ellipse(bd, cx, cy, rx, ry, md, dither=0.4, bias=0.02)
    # drips
    rnd = random.Random(7)
    for _ in range(6 if melt < 0.5 else 0):
        x = int(cx + rnd.uniform(-rx * 0.8, rx * 0.8))
        top = None
        for y in range(FH):
            if bd.opaque(x, y):
                top = y
                break
        if top is None:
            continue
        for y in range(FH - 1, 0, -1):
            if bd.opaque(x, y):
                for k in range(rnd.randint(1, 3)):
                    bd.px(x, y + k, md[1])
                break
    # moss and a sprouting reed on the head
    mo = P["moss"]
    for dx in range(-4, 5):
        y = int(cy - ry + 1 + abs(dx) * 0.4)
        if bd.opaque(int(cx + dx), y):
            bd.px(int(cx + dx), y, mo[2] if dx % 2 else mo[3])
    for k in range(4):
        bd.px(int(cx + 2), int(cy - ry - k), hx("#5a7a2a") if k < 3 else hx("#a08644"))
    comp(cv, bd, OUT)
    # arms: thick blobs hanging from the shoulders, raised for the slam; gone once it melts
    for sgn in (-1, 1):
        if melt >= 0.6:
            break
        a = math.radians(arms + slam * 130)
        sx, sy = cx + sgn * (rx - 1.0), cy + ry * 0.05
        ex = sx + sgn * math.sin(a) * 6.5
        ey = sy + math.cos(a) * 6.5
        part = layer(FW, FH)
        shaded_capsule(part, sx, sy, ex, ey, 2.6, md, bias=0.0)
        shaded_ellipse(part, ex, ey, 3.2, 2.8, md, dither=0.3, bias=0.06)
        comp(cv, part, OUT)
    # glowing eyes and a gaping mouth
    ey = int(cy - ry * 0.25)
    for sgn in (-1, 1):
        ex = int(cx + sgn * 3 - (1 if sgn > 0 else 0))
        if eyes == "glow":
            cv.px(ex, ey, EYE_GLOW[0])
            cv.px(ex + 1, ey, EYE_GLOW[1])
        elif eyes == "x":
            cv.px(ex, ey, OUTLINE)
            cv.px(ex + 1, ey, OUTLINE)
    if melt < 0.7:
        for x in range(int(cx - 2), int(cx + 2)):
            cv.px(x, ey + 3, hx("#140c08"))
        cv.px(int(cx - 1), ey + 4, hx("#140c08"))
    return cv


def build_mudman():
    A = {}
    A["idle"] = [mudman(), mudman(bob=1, arms=25)]
    A["move"] = [mudman(legs=(2, 0), lean=-1), mudman(bob=1), mudman(legs=(0, 2), lean=1), mudman(bob=1)]
    A["attack"] = [mudman(slam=0.6, bob=-1), mudman(slam=1.0, bob=-2), mudman(arms=-10, bob=2), mudman(arms=0, bob=1)]
    A["hurt"] = [mudman(eyes="x", bob=1, arms=40)]
    A["dead"] = [mudman(eyes="x", melt=0.4), mudman(eyes="none", melt=0.8), mudman(eyes="none", melt=1.0)]
    return A


def build_mudling():
    P.setdefault("mudslime", C["mud"])
    A = {}
    A["idle"] = [slime(12, 9, tone="mudslime"), slime(13, 8, tone="mudslime")]
    A["move"] = [slime(14, 7, tone="mudslime"), slime(10, 11, 2, tone="mudslime"), slime(11, 10, 4, tone="mudslime"), slime(15, 6, tone="mudslime")]
    A["attack"] = [slime(15, 6, eyes="angry", tone="mudslime"), slime(9, 12, 3, eyes="angry", tone="mudslime"), slime(11, 9, 5, eyes="angry", tone="mudslime")]
    A["hurt"] = [slime(14, 7, eyes="hurt", tone="mudslime")]
    A["dead"] = [slime(16, 5, eyes="hurt", tone="mudslime"), slime(18, 3, eyes="hurt", tone="mudslime")]
    return A


# ============================================================================
# Xà Mẫu Đầm Lầy (boss)
# ============================================================================

SW, SH, SB = 72, 64, 60


def snake(sway=0.0, head_dy=0.0, head_dx=0.0, lunge=0.0, mouth=0, hood=1.0, eyes="glow",
          tail=0.0, sink=0.0, coil_shift=0.0, venom=False):
    """Front view of a giant coiled serpent: three coils, a raised neck, a cobra hood.
    sink (0..1) lowers everything below the water line (submerging)."""
    cv = Canvas(SW, SH)
    sk = C["snake"]
    bl = C["snake_belly"]
    hd_col = C["hood"]
    cx = SW / 2
    water_y = SB - 2
    drop = sink * 30

    def clip_water(x, y):
        return y <= water_y

    # coils, back to front, each outlined so they read as separate loops
    coils = [(-14 + coil_shift, SB - 14, 15, 7), (13 - coil_shift, SB - 12, 15, 7), (0, SB - 6, 20, 7.5)]
    rnd = random.Random(11)
    for (ox, oy, rx, ry) in coils:
        coil = layer(SW, SH)
        shaded_ellipse(coil, cx + ox, oy + drop, rx, ry, sk[:6], dither=0.4, bias=-0.08, clip=clip_water)
        # banded scales: dark diamonds with a light edge, in rows along the loop
        for k in range(14):
            ang = k / 14 * math.pi + 0.15
            x = int(cx + ox + math.cos(ang) * rx * 0.72)
            y = int(oy + drop - math.sin(ang) * ry * 0.45)
            if coil.opaque(x, y) and y <= water_y:
                coil.px(x, y, sk[0])
                coil.px(x + 1, y, sk[1])
                if coil.opaque(x, y - 1):
                    coil.px(x, y - 1, sk[5])
        comp(cv, coil, OUT)
    body = layer(SW, SH)
    # the tail tip, raised for a sweep
    if tail > 0 and sink < 0.5:
        tx0, ty0 = cx + 18, SB - 10
        for k in range(14):
            t = k / 13
            x = tx0 + t * 12
            y = ty0 - math.sin(t * math.pi * 0.9) * 14 * tail
            shaded_ellipse(body, x, y, 3.2 - t * 2, 3.2 - t * 2, sk, dither=0.3)
    comp(cv, body, OUT)
    if sink >= 0.95:
        # fully under: ripples and two eyes glowing through the murk
        rp = layer(SW, SH)
        for r in (6, 11, 16):
            for a in range(36):
                ang = a / 36 * math.tau
                x = int(round(cx + math.cos(ang) * r))
                y = int(round(water_y - 3 + math.sin(ang) * r * 0.35))
                if a % 3:
                    rp.px(x, y, hx("#5a9282") if r < 16 else hx("#346a5a"))
        cv.blit(rp, 0, 0)
        for sgn in (-1, 1):
            cv.px(int(cx + sgn * 3), int(water_y - 4), EYE_GLOW[1])
        return cv
    # neck: a thick curve rising from the front coil to the head
    nk = layer(SW, SH)
    hx0 = cx + head_dx + math.sin(sway) * 3 + lunge * 2
    hy0 = SB - 40 + head_dy + lunge * 12 + drop
    pts = 14
    for i in range(pts):
        t = i / (pts - 1)
        x = cx + (hx0 - cx) * t + math.sin(t * math.pi + sway) * 3 * (1 - t)
        y = (SB - 10 + drop) + (hy0 + 8 - (SB - 10 + drop)) * t
        r = 5.8 - t * 1.4
        shaded_ellipse(nk, x, y, r, r * 0.9, sk, dither=0.3, clip=clip_water)
        # the pale belly scales on the front of the neck
        for dy in range(-1, 2):
            if nk.opaque(int(x), int(y + dy)) and y + dy <= water_y:
                nk.px(int(x), int(y + dy), bl[2] if (i + dy) % 2 else bl[3])
                nk.px(int(x) - 1, int(y + dy), bl[1])
    comp(cv, nk, OUT)
    # hood: a cobra's flared neck behind the head, widest in the middle, dark with a pale band
    hdl = layer(SW, SH)
    hw = 8 + hood * 5
    top, bot = hy0 - 7, hy0 + 15
    for y in range(int(top), int(bot)):
        t = (y + 0.5 - top) / (bot - top)
        half = hw * math.sin(math.pi * min(1.0, t * 1.05)) ** 0.75
        if half < 1 or y > water_y:
            continue
        for x in range(int(hx0 - half), int(hx0 + half) + 1):
            dx = (x + 0.5 - hx0) / half
            inten = -dx * 0.7 - (t - 0.3) * 0.6
            hdl.px(x, y, sk[shade_index(inten, 5, x, y, 0.35, -0.05)])
            if abs(dx) < 0.28 and y > hy0 + 7:
                hdl.px(x, y, bl[1] if abs(dx) > 0.14 else bl[2])
    # the markings: a dark spectacle mark over the pale band
    for sgn in (-1, 1):
        shaded_ellipse(hdl, hx0 + sgn * hw * 0.5, hy0 + 5, 1.8, 2.4, hd_col[:4], dither=0.2)
        hdl.px(int(hx0 + sgn * hw * 0.5), int(hy0 + 5), bl[4])
    comp(cv, hdl, OUT)
    # head: broad at the back, a narrow snout toward the viewer, heavy brows over slit eyes
    hdp = layer(SW, SH)
    for y in range(int(hy0 - 5), int(hy0 + 6)):
        t = (y + 0.5 - (hy0 - 5)) / 11
        half = 6.4 - t * 3.0
        for x in range(int(hx0 - half), int(hx0 + half) + 1):
            dx = (x + 0.5 - hx0) / half
            inten = -dx * 0.6 + (0.5 - t) * 0.7
            hdp.px(x, y, sk[shade_index(inten, len(sk) - 1, x, y, 0.3, 0.02) + 1])
    ey = int(hy0 - 1)
    for sgn in (-1, 1):
        ex = int(hx0 + sgn * 3 - (1 if sgn > 0 else 0))
        hdp.px(ex - sgn, ey - 2, sk[0]); hdp.px(ex, ey - 2, sk[0]); hdp.px(ex + sgn, ey - 1, sk[0])
        if eyes == "glow":
            hdp.px(ex, ey, hx("#ff3a2a"))
            hdp.px(ex + 1, ey, hx("#ffb040"))
            hdp.px(ex, ey + 1, hx("#a01a1a"))
        elif eyes == "closed":
            hdp.px(ex, ey, OUTLINE)
            hdp.px(ex + 1, ey, OUTLINE)
        else:
            hdp.px(ex - 1, ey - 1, OUTLINE); hdp.px(ex + 1, ey + 1, OUTLINE)
            hdp.px(ex + 1, ey - 1, OUTLINE); hdp.px(ex - 1, ey + 1, OUTLINE)
    hdp.px(int(hx0 - 1), int(hy0 + 3), sk[0]); hdp.px(int(hx0 + 1), int(hy0 + 3), sk[0])   # nostrils
    comp(cv, hdp, OUT)
    # mouth, fangs, tongue, venom
    my = int(hy0 + 5)
    if mouth > 0:
        for y in range(my, my + mouth + 1):
            for x in range(int(hx0 - 3), int(hx0 + 4)):
                if abs(x + 0.5 - hx0) <= 3.5 - (y - my) * 0.4:
                    cv.px(x, y, hx("#5a1020") if y > my else hx("#1e0610"))
        for sgn in (-1, 1):
            cv.px(int(hx0 + sgn * 2), my + 1, P["bone"][3])
            cv.px(int(hx0 + sgn * 2), my + 2, P["bone"][2])
        if venom:
            for k in range(5):
                cv.px(int(hx0) + (k % 2), my + mouth + k, VENOM[k % 2])
    else:
        for x in range(int(hx0 - 2), int(hx0 + 3)):
            cv.px(x, my, sk[0])
        # the forked tongue flicks now and then
        if sway > 2:
            cv.px(int(hx0), my + 1, hx("#e0506a"))
            cv.px(int(hx0) - 1, my + 2, hx("#e0506a"))
            cv.px(int(hx0) + 1, my + 2, hx("#e0506a"))
    if sink > 0:
        # the water line cutting across it
        for x in range(4, SW - 4):
            if cv.opaque(x, water_y) or cv.opaque(x, water_y - 1):
                cv.px(x, water_y, hx("#5a9282") if x % 4 else hx("#8cc0ac"))
    return cv


def snake_dead():
    cv = Canvas(SW, SH)
    sk = C["snake"]
    body = layer(SW, SH)
    for (ox, oy, rx, ry) in [(-16, SB - 5, 16, 5), (14, SB - 4, 17, 5), (0, SB - 3, 12, 4)]:
        shaded_ellipse(body, SW / 2 + ox, oy, rx, ry, sk[1:], dither=0.4)
    comp(cv, body, OUT)
    hd = layer(SW, SH)
    shaded_ellipse(hd, SW / 2 - 26, SB - 6, 6, 4, sk[1:], dither=0.3)
    comp(cv, hd, OUT)
    x0, y0 = int(SW / 2 - 27), SB - 7
    cv.px(x0 - 1, y0 - 1, OUTLINE); cv.px(x0 + 1, y0 + 1, OUTLINE)
    cv.px(x0 + 1, y0 - 1, OUTLINE); cv.px(x0 - 1, y0 + 1, OUTLINE)
    return cv


def build_snake():
    A = {}
    A["idle"] = [snake(sway=0.0), snake(sway=1.4, head_dy=1), snake(sway=2.8)]
    A["walk"] = [snake(sway=0.5, coil_shift=-2), snake(sway=1.5, coil_shift=0, head_dy=1), snake(sway=2.5, coil_shift=2), snake(sway=3.5, coil_shift=0, head_dy=1)]
    A["windup"] = [snake(head_dy=-4, hood=1.3, mouth=1), snake(head_dy=-7, hood=1.5, mouth=2)]
    A["bite"] = [snake(lunge=1.0, mouth=4, hood=0.8), snake(lunge=0.6, mouth=2, hood=1.0)]
    A["tail"] = [snake(tail=0.6, head_dx=-4, head_dy=-2), snake(tail=1.0, head_dx=-6, head_dy=-3)]
    A["spit"] = [snake(mouth=3, hood=1.4, head_dy=-3, venom=True), snake(mouth=2, hood=1.2, venom=True)]
    A["submerge"] = [snake(sink=0.35, hood=0.6), snake(sink=0.7, hood=0.3), snake(sink=1.0)]
    A["emerge"] = [snake(sink=0.6, hood=0.5, mouth=2), snake(sink=0.2, hood=1.2, mouth=3)]
    A["roar"] = [snake(mouth=4, hood=1.6, head_dy=-5), snake(mouth=5, hood=1.8, head_dy=-6)]
    A["hurt"] = [snake(eyes="closed", head_dy=3, hood=0.7)]
    A["dead"] = [snake(eyes="x", head_dy=10, hood=0.3, sink=0.3), snake_dead()]
    return A


if __name__ == "__main__":
    import os, sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in (("toad", build_toad), ("leech", build_leech), ("mudman", build_mudman), ("mudling", build_mudling),
                     ("toadking", build_toad_king), ("snake", build_snake)):
        A = fn()
        frames = [f for k in A for f in A[k]]
        sheet = pack_grid(frames, 8)
        preview(sheet.to_image(), 4 if sheet.w < 300 else 2, os.path.join(out, f"creature_{name}.png"))
    print("ok")
