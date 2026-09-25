"""
Creatures of Thảo Nguyên Gió: Linh Cẩu Gió (wind hyena, hunts in packs), Chim Ưng Đá (stone eagle,
swoops from high up) and Bò Rừng (steppe bison, charges head down). Procedural shaded shapes like
gen_cave_creatures, facing right. Every build_* returns {animation: [Canvas frames]}.
"""
import math

from pixelkit import Canvas, P, OUTLINE, hx, ramp, mix, shaded_ellipse, shade_index, shaded_poly, point_in_poly
from gen_enemies import shaded_capsule, layer, comp

OUT = hx("#120c08")

C = {
    # hyena: sandy tan with dark spots, a dark bristly mane
    "hyena": ramp("#3a2614", "#5a3c20", "#7c5830", "#9c7644", "#b8925a", "#d2ae74"),
    "hyena_dark": ramp("#1c120a", "#2c1c10", "#402a18", "#563a22"),
    "spot": hx("#4a3018"),
    # stone eagle: slate plumage mottled like rock, a pale head, an amber beak
    "eagle": ramp("#1a1c22", "#2a2e36", "#3c424c", "#525a66", "#6c7482", "#8a92a0"),
    "wing": ramp("#14161c", "#22252e", "#323742", "#454b58", "#5c6472"),
    "head": ramp("#6a6258", "#9a9084", "#c4bcae", "#e6e0d4"),
    "beak": ramp("#6a3c0c", "#a8661a", "#d89a30", "#f4c860"),
    # bison: a dark shaggy hump, a lighter brown rear, bone horns
    "bison": ramp("#1e120a", "#2e1c10", "#422a18", "#583a22", "#704c2e", "#8a6040"),
    "shag": ramp("#140c06", "#22160c", "#342214", "#46301c"),
    "horn": ramp("#5a5244", "#8e8470", "#c2b89e", "#e6dec8"),
    "hoof": ramp("#0e0a08", "#1c1612", "#2c241c"),
}
EYE_AMBER = (hx("#ffb030"), hx("#fff0a0"))
EYE_RED = (hx("#ff5a3a"), hx("#ffb070"))


def _eye(cv, x, y, eyes, col=EYE_AMBER):
    x, y = int(x), int(y)
    if eyes == "x":
        cv.px(x, y, OUTLINE)
        cv.px(x + 1, y + 1, OUTLINE)
    else:
        cv.px(x, y, col[0])
        if eyes == "wide":
            cv.px(x + 1, y, col[1])


# ============================================================================
# Linh Cẩu Gió
# ============================================================================

def hyena(step=0, crouch=0.0, bite=0.0, lunge=0.0, eyes="open", dead=0.0, run=False):
    """A lean hyena, shoulders higher than its hips. step: gait phase 0..3; crouch: body low,
    head down (the lunge's windup); bite: jaws open; lunge: stretched forward; dead: on its side."""
    FW, FH = 32, 24
    cv = Canvas(FW, FH)
    base = 22
    hy = C["hyena"]
    dk = C["hyena_dark"]
    low = crouch * 2.5 + dead * 5
    fx = lunge * 3
    sh_x, sh_y = 19 + fx, 12.5 + low          # shoulders
    hp_x, hp_y = 9 + fx * 0.5, 14.5 + low      # hips
    gait = [0, 1, 2, 1][step % 4] if run else [0, 1, 0, -1][step % 4]

    def leg(layer_cv, x0, y0, phase, col, front, near):
        swing = [2.0, 0.5, -2.0, 0.5][phase % 4] if run else [1.2, 0.3, -1.2, 0.3][phase % 4]
        if lunge > 0.3:
            swing = 3.0 if front else -3.0
        lift = [0, 1.5, 0, 0][phase % 4] if run else [0, 0.7, 0, 0][phase % 4]
        if dead > 0.5:
            layer_cv.line(x0, y0, x0 + (3 if front else -3), y0 - 2, col)
            return
        knee_x, knee_y = x0 + swing * 0.4, (y0 + base) / 2
        foot_x, foot_y = x0 + swing, base - lift
        layer_cv.line(x0, y0, knee_x, knee_y, col)
        layer_cv.line(knee_x, knee_y, foot_x, foot_y, col)
        layer_cv.px(int(foot_x) + 1, int(foot_y), dk[0])

    # far legs (darker)
    far = layer(FW, FH)
    leg(far, sh_x - 1, sh_y + 2, step + 2, dk[2], True, False)
    leg(far, hp_x + 1, hp_y + 2, step, dk[2], False, False)
    comp(cv, far, OUT)
    # tail: short, bushy, dark tip
    tl = layer(FW, FH)
    shaded_capsule(tl, hp_x - 3, hp_y - 0.5, hp_x - 5.5, hp_y + 3 - gait * 0.3, 1.3, hy[1:5])
    tl.px(int(hp_x - 6), int(hp_y + 3.5), dk[0])
    tl.px(int(hp_x - 5), int(hp_y + 4), dk[1])
    comp(cv, tl, OUT)
    # body: from the hips up to the shoulders
    bd = layer(FW, FH)
    shaded_capsule(bd, hp_x, hp_y, sh_x, sh_y, 3.9, hy, bias=0.05)
    # the dark mane along the neck and the top of the back
    for k in range(9):
        x = int(sh_x + 1 - k * 1.1)
        y = int(sh_y - 3.6 + k * 0.28)
        if bd.opaque(x, y):
            bd.px(x, y, dk[1] if k % 2 else dk[2])
            if k < 5:
                bd.px(x, y - 1, dk[0])
    # spots
    for (dx, dy) in ((-2, 0), (-5, 1), (-8, 0.5), (-4, 2.5), (-7, -1), (1, 1.5), (-1, -1.5)):
        x, y = int(sh_x + dx), int(sh_y + 1 + dy + (hp_y - sh_y) * (-dx / 10.0))
        if bd.opaque(x, y) and bd.opaque(x + 1, y):
            bd.px(x, y, C["spot"])
    comp(cv, bd, OUT)
    # neck and head (low and forward when crouching or lunging)
    hd = layer(FW, FH)
    hx0 = sh_x + 5 + fx * 0.3
    hy0 = sh_y - 1.5 + crouch * 2.5 + (1 if lunge > 0.3 else 0)
    if dead > 0.5:
        hy0 = sh_y + 1
    shaded_capsule(hd, sh_x + 1, sh_y - 1, hx0 - 1, hy0, 2.6, hy[1:])
    shaded_ellipse(hd, hx0, hy0, 3.0, 2.5, hy[1:], dither=0.25, bias=0.08)
    # the muzzle: dark, blunt; the jaw drops open to bite
    mz = hx0 + 3.2
    open_ = bite * 2.2
    shaded_capsule(hd, hx0 + 1, hy0 + 0.5, mz, hy0 + 0.8 - open_ * 0.3, 1.4, dk)
    if open_ > 0.5:
        shaded_capsule(hd, hx0 + 1, hy0 + 1.8, mz - 0.5, hy0 + 1.6 + open_, 1.0, dk)
        hd.px(int(mz - 0.5), int(hy0 + 1.2), hx("#f0e6d0"))
        hd.px(int(mz - 1.5), int(hy0 + 1.3 + open_ * 0.6), hx("#f0e6d0"))
    # a round ear
    shaded_ellipse(hd, hx0 - 1.2, hy0 - 2.8, 1.3, 1.6, dk[1:], dither=0.1)
    comp(cv, hd, OUT)
    _eye(cv, hx0 + 0.6, hy0 - 0.8, eyes)
    # near legs (lighter)
    near = layer(FW, FH)
    leg(near, sh_x + 0.5, sh_y + 2.5, step, hy[3], True, True)
    leg(near, hp_x + 2, hp_y + 2.5, step + 2, hy[3], False, True)
    comp(cv, near, OUT)
    return cv


def build_hyena():
    A = {}
    A["idle"] = [hyena(step=0), hyena(step=0, crouch=0.2)]
    A["move"] = [hyena(step=k, run=True) for k in range(4)]
    A["windup"] = [hyena(crouch=0.8, bite=0.4, eyes="wide"), hyena(crouch=1.0, bite=0.6, eyes="wide")]
    A["attack"] = [hyena(lunge=1.0, bite=1.0, eyes="wide"), hyena(lunge=0.6, bite=0.3)]
    A["hurt"] = [hyena(eyes="x", crouch=0.4)]
    A["dead"] = [hyena(eyes="x", dead=0.6), hyena(eyes="x", dead=1.0)]
    return A


# ============================================================================
# Chim Ưng Đá
# ============================================================================

def eagle(flap=0.0, bob=0, dive=0.0, glide=False, eyes="open", dead=0.0, talons=False):
    """A big eagle seen from the front and above, wings spread wide like the bat's (it reads at
    any facing). flap -1 (wings down) .. 1 (up); glide: wings flat, tips curled up; dive: wings
    swept back against the body, talons out (the swoop); dead: falling, wings limp."""
    FW, FH = 40, 28
    cv = Canvas(FW, FH)
    cx, cy = 20.0, 12.0 + bob + dead * 6
    pl = C["eagle"]
    wg = C["wing"]
    for side in (-1, 1):
        wl = layer(FW, FH)
        sx, sy = cx + side * 2.2, cy - 1.5
        if dead > 0.5:
            lead = (sx + side * 8, sy + 6)
            tip = (sx + side * 9, sy + 9)
            trail = (sx + side * 3, sy + 6)
        elif dive > 0.5:
            lead = (sx + side * 5, sy - 3)
            tip = (sx + side * 6, sy + 7)
            trail = (sx + side * 2, sy + 6)
        else:
            up = 0.25 if glide else flap
            reach = 17.0
            lead = (sx + side * reach * 0.55, sy - 4 * up - 1.5)
            tip = (sx + side * reach * (0.95 - 0.12 * abs(up)), sy - 9 * up + (1.5 if glide else 0))
            trail = (sx + side * reach * 0.5, sy + 4 - 3 * up)
        pts = [(sx, sy - 2), lead, tip, trail, (sx, sy + 3)]
        shaded_poly(wl, pts, wg[1:] if side > 0 else wg[:4], grad_dir=(side * 0.3, 1.0), dither=0.25)
        # the primaries: dark finger feathers spread at the tip
        tx, ty = tip
        for k in range(3):
            fx_, fy_ = tx - side * k * 1.4, ty + k * 1.3
            wl.px(int(fx_), int(fy_), wg[0])
            wl.px(int(fx_ + side), int(fy_ + 0.5), wg[0])
        # the leading edge catches the light
        wl.line(sx, sy - 2, lead[0], lead[1], wg[4] if len(wg) > 4 else wg[3])
        comp(cv, wl, OUT)
    # tail fan below the body
    tf = layer(FW, FH)
    shaded_poly(tf, [(cx - 2, cy + 4), (cx - 4, cy + 9), (cx + 4, cy + 9), (cx + 2, cy + 4)], pl[1:5], grad_dir=(0.0, 1.0), dither=0.2)
    tf.hline(int(cx - 3), int(cx + 3), int(cy + 9), pl[1])
    comp(cv, tf, OUT)
    # the body: stone-grey, mottled
    bd = layer(FW, FH)
    shaded_ellipse(bd, cx, cy + 1.5, 3.4, 4.6, pl, dither=0.3, bias=0.05)
    for (dx, dy) in ((-1, 0), (1, 2), (0, 3), (-2, 2)):
        x, y = int(cx + dx), int(cy + 1 + dy)
        if bd.opaque(x, y):
            bd.px(x, y, pl[1])
    comp(cv, bd, OUT)
    if talons or dive > 0.5:
        tl = layer(FW, FH)
        for side in (-1, 1):
            tl.line(cx + side * 1.2, cy + 5, cx + side * 1.8, cy + 8, C["beak"][1])
            tl.px(int(cx + side * 1.8), int(cy + 8), OUTLINE)
        comp(cv, tl, OUT)
    # the pale head and the hooked amber beak, turned a little toward the facing side (right)
    hd = layer(FW, FH)
    hx0, hy0 = cx + 0.8, cy - 3.2 + (2 if dive > 0.5 else 0) + (3 if dead > 0.5 else 0)
    shaded_ellipse(hd, hx0, hy0, 2.8, 2.5, C["head"], dither=0.2, bias=0.1)
    comp(cv, hd, OUT)
    bx, by = int(hx0 + 1.5), int(hy0 + 1)
    cv.px(bx, by, C["beak"][3])
    cv.px(bx + 1, by, C["beak"][2])
    cv.px(bx + 1, by + 1, C["beak"][1])
    cv.px(bx, by + 1, C["beak"][2])
    _eye(cv, hx0 - 0.8, hy0 - 0.8, eyes)
    _eye(cv, hx0 + 1.2, hy0 - 0.8, eyes if eyes != "wide" else "open")
    return cv


def build_eagle():
    A = {}
    A["idle"] = [eagle(glide=True), eagle(glide=True, bob=1), eagle(flap=0.6), eagle(flap=-0.4, bob=1)]
    A["move"] = [eagle(flap=1.0), eagle(flap=0.2, bob=1), eagle(flap=-0.8, bob=1), eagle(flap=0.2)]
    A["windup"] = [eagle(flap=1.2, eyes="wide"), eagle(flap=1.0, eyes="wide", bob=-1, talons=True)]
    A["attack"] = [eagle(dive=1.0, eyes="wide"), eagle(dive=1.0, eyes="wide", bob=1)]
    A["hurt"] = [eagle(flap=-0.4, eyes="x")]
    A["dead"] = [eagle(dead=0.6, eyes="x"), eagle(dead=1.0, eyes="x")]
    return A


# ============================================================================
# Bò Rừng
# ============================================================================

def bison(step=0, lower=0.0, paw=False, charge=0.0, eyes="open", dead=0.0, iron=False):
    """A steppe bison: a great dark shaggy hump over the forelegs, a lighter rear, the head low with
    curved horns. lower: head down (the charge's windup); paw: a foreleg scraping; charge: stretched
    out at a run; dead: on its side. iron: Bò Rừng Sắt's grey, iron-plated look."""
    FW, FH = 44, 34
    cv = Canvas(FW, FH)
    base = 32
    br = C["bison"] if not iron else ramp("#16181e", "#22252e", "#30343e", "#40454f", "#555b66", "#6e7580")
    sg = C["shag"] if not iron else ramp("#0e1014", "#181a20", "#24272e", "#32363e")
    fx = charge * 2.5
    low = dead * 7
    hump_x, hump_y = 24 + fx, 16 + low
    rear_x, rear_y = 12 + fx * 0.6, 19 + low

    def leg(lc, x0, y0, phase, col, front):
        run = charge > 0.3
        swing = ([2.5, 0.5, -2.5, 0.5] if run else [1.0, 0.2, -1.0, 0.2])[phase % 4]
        lift = ([0, 2, 0, 0] if run else [0, 1, 0, 0])[phase % 4]
        if paw and front and phase % 2 == 0:
            swing, lift = 2.0, 3.0
        if dead > 0.5:
            lc.line(x0, y0, x0 + (4 if front else -4), y0 - 3, col)
            return
        fx_, fy = x0 + swing, base - lift
        for w in (0, 1):
            lc.line(x0 + w, y0, fx_ + w, fy - 1, col)
        lc.px(int(fx_), int(fy), C["hoof"][1])
        lc.px(int(fx_) + 1, int(fy), C["hoof"][0])

    far = layer(FW, FH)
    leg(far, hump_x - 2, hump_y + 5, step + 2, sg[2], True)
    leg(far, rear_x + 1, rear_y + 3, step, sg[2], False)
    comp(cv, far, OUT)
    # tail: a thin rope with a tuft
    tl = layer(FW, FH)
    tl.line(rear_x - 6, rear_y - 2, rear_x - 8, rear_y + 3, br[2])
    tl.px(int(rear_x - 8), int(rear_y + 4), sg[0])
    tl.px(int(rear_x - 9), int(rear_y + 4), sg[1])
    comp(cv, tl, OUT)
    # the rear: shorter hair, lighter
    rr = layer(FW, FH)
    shaded_ellipse(rr, rear_x, rear_y, 7.2, 5.4, br[1:], dither=0.3, bias=0.05)
    comp(cv, rr, OUT)
    # the hump: a mass of dark shaggy hair
    hp = layer(FW, FH)
    shaded_ellipse(hp, hump_x, hump_y, 8.6, 7.4, sg + [br[3]], dither=0.35, bias=0.02)
    # shag: ragged darker streaks hanging down its front and belly
    for k in range(7):
        x = int(hump_x - 4 + k * 1.6)
        y0 = int(hump_y + 4 + (k % 2))
        for d in range(3):
            if hp.opaque(x, y0 + d):
                hp.px(x, y0 + d, sg[0] if d == 2 else sg[1])
    if iron:
        # riveted plates over the hump
        for (dx, dy) in ((-3, -4), (1, -5), (4, -3), (-1, -1), (3, 0)):
            x, y = int(hump_x + dx), int(hump_y + dy)
            if hp.opaque(x, y):
                hp.px(x, y, br[5])
                hp.px(x + 1, y + 1, br[1])
    comp(cv, hp, OUT)
    # the head: low, broad, bearded, with horns curving up
    hd = layer(FW, FH)
    hx0 = hump_x + 8.5 + lower * 1.5 + fx * 0.3
    hy0 = hump_y + 3.5 + lower * 3.5 + (1.5 if charge > 0.3 else 0)
    if dead > 0.5:
        hx0, hy0 = hump_x + 7, hump_y + 4
    shaded_ellipse(hd, hx0, hy0, 4.4, 3.8, sg + [br[2]], dither=0.25, bias=0.05)
    # the beard
    for d in range(3):
        hd.px(int(hx0 - 1), int(hy0 + 3.5 + d), sg[0])
        hd.px(int(hx0), int(hy0 + 3.5 + d * 0.7), sg[1])
    # muzzle
    shaded_ellipse(hd, hx0 + 3, hy0 + 1.2, 1.8, 1.5, br[1:4], dither=0.1)
    comp(cv, hd, OUT)
    # horns: from the top of the head, out and up
    hn = layer(FW, FH)
    hr = C["horn"]
    for side, dx in ((1, 0.8), (-1, -1.2)):
        x0, y0 = hx0 + dx, hy0 - 3
        pts = [(x0, y0), (x0 + 1.5 * side + 0.8, y0 - 1.5), (x0 + 1.8 * side + 1.5, y0 - 3.2)]
        for i in range(len(pts) - 1):
            hn.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], hr[2 if i == 0 else 3] if side > 0 else hr[1])
    comp(cv, hn, OUT)
    _eye(cv, hx0 + 1, hy0 - 0.8, eyes, EYE_RED if iron else EYE_AMBER)
    near = layer(FW, FH)
    leg(near, hump_x + 1, hump_y + 5.5, step, sg[3], True)
    leg(near, rear_x + 3, rear_y + 3.5, step + 2, br[3], False)
    comp(cv, near, OUT)
    return cv


def build_bison(iron=False):
    A = {}
    A["idle"] = [bison(step=0, iron=iron), bison(step=0, lower=0.3, iron=iron)]
    A["move"] = [bison(step=k, iron=iron) for k in range(4)]
    A["windup"] = [bison(lower=0.8, paw=True, step=0, iron=iron, eyes="wide"), bison(lower=1.0, paw=True, step=1, iron=iron, eyes="wide")]
    A["attack"] = [bison(lower=1.0, charge=1.0, step=k, iron=iron, eyes="wide") for k in (0, 2)]
    A["hurt"] = [bison(eyes="x", lower=0.4, iron=iron)]
    A["dead"] = [bison(eyes="x", dead=0.6, iron=iron), bison(eyes="x", dead=1.0, iron=iron)]
    return A


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in (("hyena", build_hyena), ("eagle", build_eagle), ("bison", build_bison)):
        A = fn()
        frames = [f for k in A for f in A[k]]
        preview(pack_grid(frames, 8).to_image(), 5, os.path.join(out, f"steppe_{name}.png"))
    print("ok")
