"""
Enemies: Slime Rêu (moss slime), Nấm Độc (poison mushroom) and the boss
Gấu Ma Rừng Già (old-forest ghost bear). Procedural shaded shapes.
"""
import math
import random

import numpy as np

from pixelkit import (Canvas, P, OUTLINE, hx, mix, shaded_ellipse, shade_index, LIGHT, bayer)

ENEMY_OUT = hx("#10141a")


def shaded_capsule(cv: Canvas, ax, ay, bx, by, r, rmp, dither=0.35, bias=0.0):
    n = len(rmp)
    x0, x1 = int(min(ax, bx) - r - 1), int(max(ax, bx) + r + 2)
    y0, y1 = int(min(ay, by) - r - 1), int(max(ay, by) + r + 2)
    vx, vy = bx - ax, by - ay
    L2 = vx * vx + vy * vy + 1e-6
    for y in range(y0, y1):
        for x in range(x0, x1):
            px, py = x + 0.5, y + 0.5
            t = max(0.0, min(1.0, ((px - ax) * vx + (py - ay) * vy) / L2))
            cx, cy = ax + vx * t, ay + vy * t
            dx, dy = (px - cx) / r, (py - cy) / r
            d2 = dx * dx + dy * dy
            if d2 > 1.0:
                continue
            nz = math.sqrt(1 - d2)
            inten = dx * LIGHT[0] + dy * LIGHT[1] + nz * LIGHT[2]
            cv.px(x, y, rmp[shade_index(inten, n, x, y, dither, bias)])


def layer(w, h):
    return Canvas(w, h)


def comp(dst: Canvas, part: Canvas, outline=ENEMY_OUT):
    if outline is not None:
        part.outline(outline)
    dst.blit(part, 0, 0)


# ============================================================================
# Slime
# ============================================================================

def slime(w=14, h=10, lift=0, eyes="open", seed=0, tone="slime"):
    FW, FH = 24, 24
    base = 21 - lift
    cv = Canvas(FW, FH)
    body = layer(FW, FH)
    rmp = P[tone]
    cx = 12
    cy = base - h / 2 + 0.5
    # jelly dome with a flat-ish bottom
    for y in range(int(cy - h / 2 - 1), int(base + 1)):
        for x in range(int(cx - w / 2 - 1), int(cx + w / 2 + 2)):
            dx = (x + 0.5 - cx) / (w / 2)
            dy = (y + 0.5 - cy) / (h / 2)
            if dy > 0:
                dy *= 0.75  # flatter bottom
            d2 = dx * dx + dy * dy
            if d2 > 1:
                continue
            nz = math.sqrt(max(0, 1 - d2))
            inten = dx * LIGHT[0] + dy * LIGHT[1] + nz * LIGHT[2]
            body.px(x, y, rmp[shade_index(inten, len(rmp), x, y, 0.3, 0.02)])
    # darker bottom band (jelly depth)
    for x in range(FW):
        for y in range(FH - 1, -1, -1):
            if body.opaque(x, y):
                body.px(x, y, rmp[1])
                if body.opaque(x, y - 1) and abs(x + 0.5 - cx) < w / 2 - 2:
                    body.px(x, y - 1, rmp[2])
                break
    # specular highlight
    hx0, hy0 = int(cx - w * 0.22), int(cy - h * 0.22)
    body.px(hx0, hy0, rmp[5])
    body.px(hx0 + 1, hy0, rmp[5])
    body.px(hx0, hy0 + 1, rmp[5])
    body.px(hx0 + 2, hy0 - 1 if h > 8 else hy0, rmp[4])
    # moss + sprout on top
    top_y = int(cy - h / 2 + 0.5)
    mo = P["moss"]
    for dx in (-2, -1, 0, 1, 2):
        if body.opaque(cx + dx, top_y + 1):
            body.px(cx + dx, top_y + 1 if abs(dx) < 2 else top_y + 2, mo[2] if dx % 2 else mo[1])
    body.px(cx, top_y, mo[3])
    body.px(cx, top_y - 1, P["leaf"][4])
    body.px(cx + 1, top_y - 2, P["leaf"][5])
    body.px(cx - 1, top_y - 2, P["leaf"][4])
    # face
    ey = int(cy + h * 0.05)
    ex = [int(cx - w * 0.26), int(cx + w * 0.1)]
    if eyes == "open":
        for x in ex:
            for dx in (0, 1):
                body.px(x + dx, ey, OUTLINE)
                body.px(x + dx, ey + 1, OUTLINE)
            body.px(x, ey, (255, 255, 255, 255))
    elif eyes == "hurt":
        for x in ex:
            body.px(x - 1, ey - 1, OUTLINE)
            body.px(x, ey, OUTLINE)
            body.px(x + 1, ey + 1, OUTLINE)
            body.px(x + 1, ey - 1, OUTLINE)
            body.px(x - 1, ey + 1, OUTLINE)
    elif eyes == "angry":
        for i, x in enumerate(ex):
            for dx in (0, 1):
                body.px(x + dx, ey, OUTLINE)
                body.px(x + dx, ey + 1, OUTLINE)
            body.px(x + (1 if i == 0 else 0), ey, hx("#ff5a3a"))
            body.px(x + (-1 if i == 0 else 2), ey - 1, OUTLINE)
    # mouth
    body.px(int(cx - 1), ey + 3, rmp[0])
    body.px(int(cx), ey + 3, rmp[0])
    comp(cv, body)
    return cv


def slime_puddle():
    cv = Canvas(24, 24)
    body = layer(24, 24)
    rmp = P["slime"]
    shaded_ellipse(body, 12, 20, 9, 2.6, rmp, dither=0.3)
    body.px(8, 19, rmp[5])
    body.px(15, 20, rmp[4])
    comp(cv, body)
    return cv


def build_slime(tone="slime"):
    A = {}
    A["idle"] = [slime(14, 10, tone=tone), slime(15, 9, tone=tone)]
    A["move"] = [slime(16, 8, tone=tone), slime(11, 13, 2, tone=tone), slime(12, 12, 5, tone=tone), slime(17, 7, tone=tone)]
    A["attack"] = [slime(17, 7, eyes="angry", tone=tone), slime(10, 14, 3, eyes="angry", tone=tone), slime(12, 11, 6, eyes="angry", tone=tone)]
    A["hurt"] = [slime(16, 8, eyes="hurt", tone=tone)]
    A["dead"] = [slime(18, 6, eyes="hurt", tone=tone), slime_puddle()]
    return A


# ============================================================================
# Mushroom
# ============================================================================

def mushroom(cap_w=17, cap_h=8, bob=0, lean=0, feet=(0, 0), eyes="angry", puff=0.0):
    FW, FH = 24, 26
    base = 23
    cv = Canvas(FW, FH)
    cx = 12 + lean
    cr = P["shroom_red"]
    cm = P["cream"]
    # feet
    ft = layer(FW, FH)
    for i, dx in enumerate((-3, 2)):
        lift = feet[i]
        ft.px(cx + dx, base - lift, cm[1])
        ft.px(cx + dx + 1, base - lift, cm[1])
        ft.px(cx + dx, base - 1 - lift, cm[2])
        ft.px(cx + dx + 1, base - 1 - lift, cm[2])
    comp(cv, ft)
    # stem/body
    st = layer(FW, FH)
    sy0 = base - 9 + bob
    for y in range(sy0, base - 1):
        for x in range(cx - 4, cx + 4):
            dx = (x + 0.5 - cx) / 4
            idx = shade_index(-dx * 0.9, len(cm) - 1, x, y, dither=0.3, bias=0.1)
            st.px(x, y, cm[idx + 1])
    # face on the stem
    ey = sy0 + 3
    if eyes == "angry":
        st.px(cx - 2, ey, OUTLINE); st.px(cx - 2, ey + 1, OUTLINE); st.px(cx - 3, ey - 1, OUTLINE)
        st.px(cx + 1, ey, OUTLINE); st.px(cx + 1, ey + 1, OUTLINE); st.px(cx + 2, ey - 1, OUTLINE)
        st.px(cx - 1, ey + 3, P["red"][1]); st.px(cx, ey + 3, P["red"][1])
    else:
        for x in (cx - 2, cx + 1):
            st.px(x - 1, ey - 1, OUTLINE); st.px(x, ey, OUTLINE); st.px(x + 1, ey + 1, OUTLINE)
            st.px(x + 1, ey - 1, OUTLINE); st.px(x - 1, ey + 1, OUTLINE)
    comp(cv, st)
    # cap
    cp = layer(FW, FH)
    ccy = sy0 + 1
    w = cap_w * (1 + puff * 0.18)
    h = cap_h * (1 - puff * 0.25)
    for y in range(int(ccy - h - 1), int(ccy + 2)):
        for x in range(int(cx - w / 2 - 1), int(cx + w / 2 + 2)):
            dx = (x + 0.5 - cx) / (w / 2)
            dy = (y + 0.5 - ccy) / h
            if dy > 0.25:
                continue
            d2 = dx * dx + dy * dy
            if d2 > 1:
                continue
            nz = math.sqrt(max(0, 1 - d2))
            inten = dx * LIGHT[0] + dy * LIGHT[1] + nz * LIGHT[2]
            cp.px(x, y, cr[shade_index(inten, len(cr), x, y, 0.3, 0.05)])
    # underside rim
    for x in range(int(cx - w / 2 + 1), int(cx + w / 2)):
        if cp.opaque(x, int(ccy)):
            cp.px(x, int(ccy), cm[1])
    # spots
    spots = [(-4, -4), (2, -5), (5, -2), (-1, -2), (-6, -1)]
    for (sx, sy) in spots:
        x, y = int(cx + sx * w / 17), int(ccy + sy * h / 8)
        if cp.opaque(x, y) and cp.opaque(x + 1, y):
            cp.px(x, y, cm[4])
            cp.px(x + 1, y, cm[3])
            if cp.opaque(x, y - 1):
                cp.px(x, y - 1, cm[3])
    comp(cv, cp)
    return cv


def build_mushroom():
    A = {}
    A["idle"] = [mushroom(), mushroom(bob=1, cap_h=7.5)]
    A["move"] = [mushroom(lean=-1, feet=(1, 0)), mushroom(bob=1), mushroom(lean=1, feet=(0, 1)), mushroom(bob=1)]
    A["attack"] = [mushroom(puff=0.6, bob=1), mushroom(puff=1.0, bob=2), mushroom(puff=-0.3, cap_h=9)]
    A["hurt"] = [mushroom(eyes="hurt", bob=1, lean=-1)]
    A["dead"] = [mushroom(eyes="hurt", bob=3, cap_h=6), mushroom(eyes="hurt", bob=5, cap_h=4, cap_w=19)]
    return A


# ============================================================================
# Boss bear
# ============================================================================

BW, BH = 64, 64
BEAR_OUT = hx("#08080e")


def bear(arm_l=15, arm_r=15, squash=0.0, bob=0, mouth=0, legs=(0, 0), eyes="glow", paw_drop=0,
         head_dy=0, rock=False, lying=False):
    """arm angles in degrees: 0 = hanging down, 90 = straight out, 160 = raised up."""
    cv = Canvas(BW, BH)
    fur = P["fur"]
    base = 60
    cx = 32
    body_cx = cx
    body_cy = 41 + bob + squash * 3
    rx = 17 + squash * 2.5
    ry = 15 - squash * 3

    # ---- legs (behind body)
    lg = layer(BW, BH)
    for i, sgn in enumerate((-1, 1)):
        lift = legs[i]
        lx = cx + sgn * 9
        shaded_capsule(lg, lx, body_cy + 8, lx + sgn * 1, base - 3 - lift, 5.2, fur[:5], bias=-0.02)
        # foot pad + claws
        for k in range(-2, 3):
            lg.px(lx + k + sgn, base - 1 - lift, fur[1])
        for k in (-2, 0, 2):
            lg.px(lx + k + sgn, base - lift, P["bone"][3])
    comp(cv, lg, BEAR_OUT)

    # ---- body
    bd = layer(BW, BH)
    shaded_ellipse(bd, body_cx, body_cy, rx, ry, fur[:5], dither=0.4, bias=0.02)
    # belly (lighter, slightly brown)
    bl = [hx('#2a2230'), hx('#3a3040'), hx('#4c4050'), hx('#5e5262'), hx('#726676')]
    for y in range(int(body_cy - 1), int(body_cy + ry)):
        for x in range(int(body_cx - 7), int(body_cx + 8)):
            dx = (x + 0.5 - body_cx) / 7.0
            dy = (y + 0.5 - (body_cy + 5)) / (ry - 5)
            if dx * dx + dy * dy <= 1 and bd.opaque(x, y):
                inten = -dx * 0.5 - dy * 0.3 + 0.2
                bd.px(x, y, bl[shade_index(inten, len(bl), x, y, 0.35)])
    # fur tufts on the belly edge
    for x in range(int(body_cx - 6), int(body_cx + 7), 3):
        y = int(body_cy - 1 + abs(x - body_cx) * 0.25)
        bd.px(x, y, fur[3])
        bd.px(x + 1, y + 1, fur[3])
    # moss on the shoulders
    mo = P["moss"]
    rnd = random.Random(4)
    for sgn in (-1, 1):
        for k in range(14):
            x = int(body_cx + sgn * (5 + k * 0.85))
            y = int(body_cy - ry + 2 + k * 0.55 + rnd.randint(0, 1))
            if bd.opaque(x, y):
                bd.px(x, y, mo[3] if k % 3 else mo[2])
                if bd.opaque(x, y + 1):
                    bd.px(x, y + 1, mo[1])
    # leather harness: strap from left shoulder to right hip
    lt = P["leather"]
    for t in np.linspace(0, 1, 40):
        x = body_cx - 13 + t * 24
        y = body_cy - 11 + t * 19
        for w in (0, 1, 2):
            px_, py_ = int(x), int(y + w)
            if bd.opaque(px_, py_):
                bd.px(px_, py_, lt[3] if w == 0 else (lt[2] if w == 1 else lt[1]))
    # buckle
    bx, by = int(body_cx - 1), int(body_cy - 2)
    for dx in range(3):
        for dy in range(3):
            bd.px(bx + dx, by + dy, P["gold"][3] if (dx, dy) != (1, 1) else lt[1])
    bd.px(bx, by, P["gold"][4])
    # pouch on the hip
    for dx in range(5):
        for dy in range(4):
            bd.px(int(body_cx + 9 + dx), int(body_cy + 6 + dy), lt[3] if dy == 0 else lt[2])
    comp(cv, bd, BEAR_OUT)

    # ---- arms
    def arm(sgn, ang):
        a = math.radians(ang)
        sx, sy = body_cx + sgn * (rx - 4), body_cy - ry + 7
        length = 15
        ex = sx + sgn * math.sin(a) * length
        ey = sy + math.cos(a) * length + (paw_drop if ang < 60 else 0)
        part = layer(BW, BH)
        shaded_capsule(part, sx, sy, ex, ey, 5.0, fur[:5], bias=0.04)
        # paw
        shaded_ellipse(part, ex, ey + 1, 5.2, 4.6, fur[:5], dither=0.3, bias=0.08)
        # claws pointing away from the shoulder
        dxv, dyv = ex - sx, ey - sy
        L = math.hypot(dxv, dyv) + 1e-6
        ux, uy = dxv / L, dyv / L
        px_, py_ = -uy, ux
        for k in (-1.6, 0, 1.6):
            cxp = ex + ux * 4.8 + px_ * k * 1.4
            cyp = ey + 1 + uy * 4.8 + py_ * k * 1.4
            part.px(int(cxp), int(cyp), P["bone"][3])
            part.px(int(cxp + ux), int(cyp + uy), P["bone"][2])
        comp(cv, part, BEAR_OUT)
        return ex, ey

    # arms raised above the head are drawn after the head so they read clearly
    raised = [arm_l > 100, arm_r > 100]
    paw_pos = {}
    if not raised[0]:
        paw_pos["l"] = arm(-1, arm_l)
    if not raised[1]:
        paw_pos["r"] = arm(1, arm_r)

    # ---- head
    hd = layer(BW, BH)
    hcx, hcy = cx, body_cy - ry - 3 + head_dy
    # ears
    for sgn in (-1, 1):
        shaded_ellipse(hd, hcx + sgn * 8.5, hcy - 7, 3.6, 3.4, fur[1:], dither=0.3)
        hd.px(int(hcx + sgn * 8.5), int(hcy - 7), P["fur_brown"][1])
        hd.px(int(hcx + sgn * 8.5 - (1 if sgn > 0 else 0)), int(hcy - 6), P["fur_brown"][2])
    shaded_ellipse(hd, hcx, hcy, 11.5, 9.5, fur[:5], dither=0.4, bias=0.05)
    # muzzle
    mz = P["fur_brown"]
    shaded_ellipse(hd, hcx, hcy + 4, 5.8, 4.0, mz[1:], dither=0.3, bias=0.1)
    # nose
    for dx in (-1, 0, 1):
        hd.px(hcx + dx, int(hcy + 2), OUTLINE)
    hd.px(hcx, int(hcy + 3), OUTLINE)
    hd.px(hcx - 1, int(hcy + 1), fur[4])
    # mouth
    my = int(hcy + 5)
    if mouth > 0:
        for y in range(my, my + 2 + mouth):
            for x in range(hcx - 3, hcx + 4):
                if abs(x - hcx) <= 3 - (1 if y == my + 1 + mouth else 0):
                    hd.px(x, y, hx("#5a1020") if y > my else hx("#2a0810"))
        # fangs
        hd.px(hcx - 3, my, P["bone"][3])
        hd.px(hcx + 3, my, P["bone"][3])
        hd.px(hcx - 3, my + 1, P["bone"][2])
        hd.px(hcx + 3, my + 1, P["bone"][2])
    else:
        hd.px(hcx - 1, my, OUTLINE)
        hd.px(hcx + 1, my, OUTLINE)
        hd.px(hcx, my - 1, OUTLINE)
    # eyes: ghostly glow
    ey = int(hcy - 1)
    for sgn in (-1, 1):
        ex = hcx + sgn * 5 - (1 if sgn > 0 else 0)
        if eyes == "glow":
            hd.px(ex, ey, hx("#ffe36a"))
            hd.px(ex + 1, ey, hx("#ff8a2a"))
            hd.px(ex + (1 if sgn < 0 else 0), ey - 1, hx("#ff5a1a"))
            # angry brow
            hd.px(ex - sgn, ey - 2, OUTLINE)
            hd.px(ex, ey - 2, OUTLINE)
            hd.px(ex + sgn, ey - 1 - (1 if sgn < 0 else 0), OUTLINE)
        elif eyes == "closed":
            hd.px(ex, ey, OUTLINE)
            hd.px(ex + 1, ey, OUTLINE)
        elif eyes == "x":
            hd.px(ex - 1, ey - 1, OUTLINE); hd.px(ex + 1, ey + 1, OUTLINE)
            hd.px(ex + 1, ey - 1, OUTLINE); hd.px(ex - 1, ey + 1, OUTLINE)
            hd.px(ex, ey, OUTLINE)
    # scar over the left eye
    hd.px(hcx - 6, int(hcy - 4), hx("#8a7a88"))
    hd.px(hcx - 5, int(hcy - 3), hx("#8a7a88"))
    hd.px(hcx - 4, int(hcy - 2), hx("#8a7a88"))
    comp(cv, hd, BEAR_OUT)

    if raised[0]:
        paw_pos["l"] = arm(-1, arm_l)
    if raised[1]:
        paw_pos["r"] = arm(1, arm_r)
    if rock and "r" in paw_pos:
        # a boulder held over the head
        rk = layer(BW, BH)
        px_, py_ = paw_pos["r"]
        shaded_ellipse(rk, px_ - 4, py_ - 6, 7, 6, P["stone"][1:], dither=0.25)
        comp(cv, rk, BEAR_OUT)
    return cv


def bear_lying():
    cv = Canvas(BW, BH)
    fur = P["fur"]
    bd = layer(BW, BH)
    shaded_ellipse(bd, 32, 52, 22, 8, fur[1:], dither=0.4)
    comp(cv, bd, BEAR_OUT)
    hd = layer(BW, BH)
    shaded_ellipse(hd, 14, 50, 9, 7, fur[1:], dither=0.4)
    shaded_ellipse(hd, 9, 53, 4.5, 3, P["fur_brown"][1:], dither=0.3)
    shaded_ellipse(hd, 17, 44, 3, 3, fur[1:], dither=0.3)
    for (x, y) in [(13, 48), (15, 50), (15, 48), (13, 50), (14, 49)]:
        hd.px(x, y, OUTLINE)
    hd.px(7, 51, OUTLINE)
    comp(cv, hd, BEAR_OUT)
    for x in (44, 50):
        cv.px(x, 60, P["bone"][3])
        cv.px(x + 1, 60, P["bone"][3])
    return cv


def build_bear():
    A = {}
    A["idle"] = [bear(), bear(bob=1, squash=0.15, arm_l=17, arm_r=17)]
    A["walk"] = [
        bear(legs=(2, 0), arm_l=25, arm_r=8, bob=0),
        bear(legs=(0, 0), bob=1, arm_l=15, arm_r=15),
        bear(legs=(0, 2), arm_l=8, arm_r=25, bob=0),
        bear(legs=(0, 0), bob=1, arm_l=15, arm_r=15),
    ]
    A["windup"] = [bear(arm_l=120, arm_r=120, squash=-0.25, mouth=1, head_dy=-1),
                   bear(arm_l=155, arm_r=155, squash=-0.4, mouth=2, head_dy=-2, bob=-1)]
    A["slam"] = [bear(arm_l=25, arm_r=25, squash=0.6, mouth=2, paw_drop=4, bob=2),
                 bear(arm_l=30, arm_r=30, squash=0.35, mouth=1, paw_drop=3, bob=1)]
    A["throw"] = [bear(arm_l=30, arm_r=165, rock=True, squash=-0.2, head_dy=-1),
                  bear(arm_l=20, arm_r=60, squash=0.2, mouth=1)]
    A["crouch"] = [bear(arm_l=35, arm_r=35, squash=0.8, bob=3, legs=(-1, -1))]
    A["air"] = [bear(arm_l=110, arm_r=110, squash=-0.5, mouth=2, legs=(3, 3), bob=-3)]
    A["roar"] = [bear(arm_l=70, arm_r=70, mouth=3, head_dy=-2, squash=-0.2),
                 bear(arm_l=80, arm_r=80, mouth=3, head_dy=-3, squash=-0.3)]
    A["hurt"] = [bear(eyes="closed", squash=0.25, mouth=1, arm_l=30, arm_r=30)]
    A["dead"] = [bear(eyes="x", squash=0.9, bob=4, arm_l=40, arm_r=40, mouth=1), bear_lying()]
    return A


def build():
    out = {}
    for k, v in build_slime().items():
        out[f"slime_{k}"] = v
    for k, v in build_mushroom().items():
        out[f"shroom_{k}"] = v
    return out


ORDER = ["slime_idle", "slime_move", "slime_attack", "slime_hurt", "slime_dead",
         "shroom_idle", "shroom_move", "shroom_attack", "shroom_hurt", "shroom_dead"]
