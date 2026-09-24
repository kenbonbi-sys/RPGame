"""
Creatures of Hang Pha Lê: Dơi Pha Lê (crystal bat), Nhện Hang (cave spider) and Golem Đá Nhỏ
(small stone golem). Procedural shaded shapes, drawn like gen_swamp_creatures, facing right.
Every build_* returns {animation: [Canvas frames]}.
"""
import math
import random

from pixelkit import Canvas, P, OUTLINE, hx, ramp, mix, shaded_ellipse, shade_index, shaded_poly, point_in_poly
from gen_enemies import shaded_capsule, layer, comp

OUT = hx("#0a0a12")

C = {
    "bat": ramp("#120a1e", "#1e1030", "#2c1846", "#3c225e", "#50307a", "#664296"),
    "membrane": ramp("#1a0c26", "#2a143c", "#3a1e52", "#4c2a68"),
    "crys": ramp("#0e4658", "#136a7a", "#1c94a0", "#36c2c4", "#88ecea", "#dcfffc"),
    "spider": ramp("#0c0a0e", "#16121a", "#221c28", "#2e2638", "#3c3248", "#4c405a"),
    "leg": ramp("#0e0c12", "#1a161e", "#28222e", "#383040"),
    "mark": ramp("#1c94a0", "#36c2c4", "#88ecea"),
    "stone": ramp("#15171e", "#1f222c", "#2a2e3a", "#373c4b", "#474d5f", "#5a6176", "#707890"),
    "moss": ramp("#1a2e22", "#24422c", "#325a36"),
}
EYE_RED = (hx("#ff5a3a"), hx("#ffb070"))
EYE_CYAN = (hx("#6af2f0"), hx("#e0fffe"))


# ============================================================================
# Dơi Pha Lê
# ============================================================================

def _poly(cv, pts, rmp, grad=(0.3, 1.0), dither=0.25):
    shaded_poly(cv, pts, rmp, grad_dir=grad, dither=dither)


def bat(flap=0.0, bob=0, dive=0.0, mouth=False, eyes="open", dead=0.0):
    """A small bat with a crystal-studded wing edge. flap -1 (wings down) .. 1 (wings up);
    dive: wings swept back, body tilted down to strike; dead: falling, wings limp."""
    FW, FH = 26, 22
    cv = Canvas(FW, FH)
    cx, cy = 13.0, 11.0 + bob + dead * 5
    body = C["bat"]
    mem = C["membrane"]
    cr = C["crys"]
    # wings: from the shoulder out to three finger tips, the membrane between them
    for side in (-1, 1):
        wl = layer(FW, FH)
        sx, sy = cx + side * 1.5, cy - 1
        if dead > 0.5:
            tips = [(sx + side * 6, sy + 5), (sx + side * 5, sy + 7), (sx + side * 3, sy + 7)]
        elif dive > 0.5:
            tips = [(sx - 8, sy - 1 + side * 2), (sx - 9, sy + 1 + side), (sx - 6, sy + 3)]
        else:
            ang = flap * 0.9
            reach = 10.5
            tips = [(sx + side * reach * math.cos(ang * 0.6), sy - reach * math.sin(ang) * 0.9),
                    (sx + side * (reach - 1.5) * math.cos(ang * 0.3 - 0.35), sy - (reach - 3) * math.sin(ang - 0.35) * 0.9 + 2),
                    (sx + side * 5.5, sy + 4 - flap * 1.5)]
        pts = [(sx, sy - 1.5)] + tips + [(sx, sy + 2.5)]
        _poly(wl, pts, mem, grad=(side * 0.5, 1.0))
        # finger bones
        for (tx, ty) in tips[:2]:
            wl.line(sx, sy - 1, tx, ty, body[3])
        comp(cv, wl, OUT)
        # crystal studs along the wing's leading edge
        for k, (tx, ty) in enumerate(tips[:2]):
            cv.px(int(tx), int(ty), cr[4])
            cv.px(int(tx), int(ty) - 1, cr[5] if k == 0 else cr[3])
    # the furry body and head
    bl = layer(FW, FH)
    shaded_ellipse(bl, cx, cy + 1.5, 3.4, 4.0, body, dither=0.3, bias=0.05)
    hx0, hy0 = cx + (1.5 if dive > 0.5 else 0.5), cy - 3.2 + (1.5 if dive > 0.5 else 0)
    shaded_ellipse(bl, hx0, hy0, 3.0, 2.6, body, dither=0.25, bias=0.08)
    # ears
    for side in (-1, 1):
        ex = hx0 + side * 1.8
        bl.px(int(ex), int(hy0 - 3), body[3])
        bl.px(int(ex), int(hy0 - 4), body[4])
        bl.px(int(ex + side), int(hy0 - 2), body[2])
    comp(cv, bl, OUT)
    # glowing eyes, a fanged mouth
    ey = int(hy0 - 0.5)
    for side in (-1, 1):
        ex = int(hx0 + side * 1.1 + (0 if side < 0 else 0.5))
        if eyes == "open":
            cv.px(ex, ey, EYE_CYAN[0])
        elif eyes == "x":
            cv.px(ex, ey, OUTLINE)
    if mouth:
        cv.px(int(hx0), ey + 2, hx("#5a1020"))
        cv.px(int(hx0) - 1, ey + 2, hx("#f0e6d2"))
        cv.px(int(hx0) + 1, ey + 2, hx("#f0e6d2"))
    # a little crystal growing on its back
    cv.px(int(cx - 1), int(cy + 3), cr[3])
    cv.px(int(cx - 1), int(cy + 2), cr[4])
    return cv


def build_bat():
    A = {}
    A["idle"] = [bat(flap=1.0), bat(flap=0.2, bob=1), bat(flap=-0.8, bob=1), bat(flap=0.2)]
    A["move"] = [bat(flap=1.0), bat(flap=0.0, bob=1), bat(flap=-1.0, bob=1), bat(flap=0.0)]
    A["windup"] = [bat(flap=1.2, mouth=True), bat(flap=1.0, mouth=True, bob=-1)]
    A["attack"] = [bat(dive=1.0, mouth=True), bat(dive=1.0, mouth=True, bob=1)]
    A["hurt"] = [bat(flap=-0.4, eyes="x")]
    A["dead"] = [bat(dead=0.6, eyes="x"), bat(dead=1.0, eyes="x")]
    return A


# ============================================================================
# Nhện Hang
# ============================================================================

def spider(step=0, rear=0.0, spit=False, eyes="open", curl=0.0, lunge=0.0):
    """A cave spider seen from the side: a round abdomen with a glowing crystal mark, a smaller
    head in front (right) with a cluster of red eyes, and eight long legs arching high over the
    body to feet spread in front and behind. step: the walk cycle 0..3; rear: the front raised to
    spit web; lunge: thrown forward; curl: dead, legs folded under."""
    FW, FH = 34, 24
    cv = Canvas(FW, FH)
    base = 21
    sp = C["spider"]
    lg = C["leg"]
    ax, ay = 12.0 - lunge * 1.5, base - 6.0 - rear * 1.5          # abdomen
    hx0, hy0 = 19.5 + lunge * 2, base - 6.5 - rear * 3.5           # head
    root_x, root_y = hx0 - 2.5, hy0 + 0.5
    feet = [3.5, 10.0, 22.0, 30.5]   # from the back to the front

    def leg(canvas, i, near):
        phase = (step + i * 2 + (0 if near else 1)) % 4
        lift = (2.0 if phase == 1 else 1.0 if phase == 2 else 0.0) * (1 - curl)
        fx = feet[i] + (0.8 if near else -0.8) + lunge * (1.5 if i >= 2 else 0)
        fy = base - lift
        if rear > 0.4 and i >= 2:
            fx, fy = root_x + 5 + (i - 2) * 3, root_y - 7 - (i - 2) * 1.5   # front legs raised
        if curl > 0:
            fx = root_x + (fx - root_x) * (1 - curl * 0.7)
            fy = fy - curl * 3
        kx = root_x + (fx - root_x) * 0.45
        ky = min(root_y, fy) - 6.5 + curl * 4 - (0.5 if near else 1.5)
        col = lg[3] if near else lg[1]
        canvas.line(root_x, root_y, kx, ky, col)
        canvas.line(kx, ky, fx, fy, col)
        if near:
            canvas.px(int(round(kx)), int(round(ky)), lg[2])

    far = layer(FW, FH)
    for i in range(4):
        leg(far, i, False)
    comp(cv, far, OUT)
    bd = layer(FW, FH)
    shaded_ellipse(bd, ax, ay, 6.4, 5.0 - curl, sp, dither=0.3, bias=0.03)
    shaded_ellipse(bd, hx0, hy0, 3.4, 2.9, sp, dither=0.25, bias=0.06)
    comp(cv, bd, OUT)
    near = layer(FW, FH)
    for i in range(4):
        leg(near, i, True)
    comp(cv, near, OUT)
    # the crystal mark on its back, like the cave's own light
    mk = C["mark"]
    for (dx, dy, k) in ((-1, -2, 1), (0, -2, 2), (1, -2, 1), (0, -3, 1), (0, -1, 0), (-2, -1, 0), (2, -1, 0)):
        cv.px(int(ax + dx), int(ay + dy), mk[k])
    # eyes and fangs
    ey = int(hy0 - 1)
    if eyes == "open":
        for (dx, dy, k) in ((1, 0, 0), (2, 0, 1), (1, -1, 0), (2, 1, 0)):
            cv.px(int(hx0 + dx), ey + dy, EYE_RED[k])
    else:
        cv.px(int(hx0 + 1), ey, OUTLINE)
        cv.px(int(hx0 + 2), ey + 1, OUTLINE)
    fx = int(hx0 + 3)
    cv.px(fx, int(hy0 + 1), hx("#d8d0c0"))
    cv.px(fx, int(hy0 + 2), hx("#a89c86"))
    if spit:
        for k in range(5):
            cv.px(fx + 1 + k, int(hy0 + 1) - k // 2, (230, 236, 248, 230 - k * 38))
    return cv


def build_spider():
    A = {}
    A["idle"] = [spider(step=0), spider(step=1)]
    A["move"] = [spider(step=k) for k in range(4)]
    A["windup"] = [spider(rear=0.6), spider(rear=1.0)]
    A["attack"] = [spider(rear=1.0, spit=True), spider(rear=0.5, lunge=0.5)]
    A["hurt"] = [spider(step=2, eyes="x", rear=0.3)]
    A["dead"] = [spider(curl=0.6, eyes="x"), spider(curl=1.0, eyes="x")]
    return A


# ============================================================================
# Golem Đá Nhỏ
# ============================================================================

def golem(bob=0, arms=0.0, slam=0.0, step=0, eyes="open", crumble=0.0):
    """A squat golem of cave rock with crystal shards grown through it and one glowing eye.
    arms: fists raised (0..1); slam: brought down in front; crumble: falling apart (dead)."""
    FW, FH = 34, 34
    cv = Canvas(FW, FH)
    base = 31
    st = C["stone"]
    cr = C["crys"]
    rnd = random.Random(5)
    if crumble >= 0.8:
        # a heap of rocks and shards
        for i in range(7):
            x = 8 + i * 3 + rnd.uniform(-1, 1)
            y = base - 2 - (i % 3)
            part = layer(FW, FH)
            shaded_ellipse(part, x, y, 3.2, 2.4, st[1:], dither=0.3, bias=0.05)
            comp(cv, part, OUT)
        cv.px(15, base - 5, cr[3]); cv.px(16, base - 6, cr[4]); cv.px(21, base - 4, cr[2])
        return cv
    cx = FW / 2
    cy = base - 12 + bob + crumble * 4
    # legs
    lg = layer(FW, FH)
    for i, side in enumerate((-1, 1)):
        lift = 2 if (step % 2 == i and step > 0) else 0
        shaded_capsule(lg, cx + side * 4, cy + 7, cx + side * 4.5, base - 2 - lift, 2.8, st, bias=-0.05)
    comp(cv, lg, OUT)
    # the body: a rough block
    bd = layer(FW, FH)
    pts = [(cx - 8.5, cy - 4), (cx - 6, cy - 9), (cx + 6, cy - 9.5), (cx + 8.5, cy - 3), (cx + 7.5, cy + 7), (cx - 7.5, cy + 7.5)]
    shaded_poly(bd, pts, st[1:], grad_dir=(0.45, 0.9), dither=0.3)
    for (x, y) in ((cx - 4, cy - 2), (cx + 3, cy + 3), (cx - 1, cy + 5)):
        bd.px(int(x), int(y), st[0]); bd.px(int(x) + 1, int(y), st[1])
    # moss on its shoulders
    for x in range(int(cx - 6), int(cx + 6)):
        if bd.opaque(x, int(cy - 9) + (1 if x % 3 else 0)):
            bd.px(x, int(cy - 9) + (1 if x % 3 else 0), C["moss"][1 + x % 2])
    comp(cv, bd, OUT)
    # crystals growing out of its back
    for (dx, h) in ((-5, 5), (-2, 7), (1, 4)):
        for k in range(h):
            x = int(cx + dx + (k // 3))
            y = int(cy - 9 - k)
            cv.px(x, y, cr[2 + min(3, k // 2)])
            cv.px(x + 1, y, cr[1 + min(3, k // 2)])
        cv.px(int(cx + dx + (h // 3)), int(cy - 9 - h), cr[5])
    # the eye
    ex, ey = int(cx + 2), int(cy - 3)
    if eyes == "open":
        cv.px(ex, ey, EYE_CYAN[0]); cv.px(ex + 1, ey, EYE_CYAN[1]); cv.px(ex + 2, ey, EYE_CYAN[0])
    else:
        cv.px(ex, ey, OUTLINE); cv.px(ex + 1, ey, OUTLINE)
    # arms: big fists hanging, raised for the slam, brought down in front
    for side in (-1, 1):
        sx, sy = cx + side * 8, cy - 4
        if slam > 0:
            ex_, ey_ = sx + side * 1 + 3, cy + 6 + slam * 2
        else:
            a = math.radians(15 + arms * 150)
            ex_, ey_ = sx + side * math.sin(a) * 6, sy + math.cos(a) * 7
        arm = layer(FW, FH)
        shaded_capsule(arm, sx, sy, ex_, ey_, 2.4, st, bias=0.02)
        shaded_ellipse(arm, ex_, ey_, 3.6, 3.2, st[1:], dither=0.3, bias=0.08)
        comp(cv, arm, OUT)
    return cv


def build_golem():
    A = {}
    A["idle"] = [golem(), golem(bob=1)]
    A["move"] = [golem(step=1), golem(step=0, bob=1), golem(step=2), golem(step=0, bob=1)]
    A["windup"] = [golem(arms=0.6), golem(arms=1.0, bob=-1)]
    A["attack"] = [golem(slam=1.0, bob=1), golem(slam=0.6)]
    A["hurt"] = [golem(eyes="x", bob=1)]
    A["dead"] = [golem(eyes="x", crumble=0.4), golem(crumble=1.0)]
    return A


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in (("bat", build_bat), ("spider", build_spider), ("golem", build_golem)):
        A = fn()
        frames = [f for k in A for f in A[k]]
        preview(pack_grid(frames, 8).to_image(), 4, os.path.join(out, f"cave_{name}.png"))
    print("ok")
