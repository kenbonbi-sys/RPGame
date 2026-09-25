"""
Creatures of Thảo Nguyên Gió: Linh Cẩu Gió (wind hyena, hunts in packs), Chim Ưng Đá (stone eagle,
swoops from high up), Bò Rừng (steppe bison, charges head down), Bò Rừng Sắt (the bison clad in
the bandits' iron) and Bù Nhìn Sống (a scarecrow that stands still among the straw ones, then
wakes with sickles in its sleeves). Procedural shaded shapes like gen_cave_creatures, facing
right. Every build_* returns {animation: [Canvas frames]}; scarecrow_still() is also the straw
scarecrow prop, pixel for pixel.
"""
import math
import random

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
    # scarecrow: grey weathered wood, burlap, a faded red shirt, straw
    "post": ramp("#2a2018", "#40322a", "#584638", "#6e5a48"),
    "sack": ramp("#5a4830", "#7c6644", "#9c845a", "#bca476", "#d6c294"),
    "shirt": ramp("#3a1a18", "#5a2a24", "#7a3c30", "#98543e", "#b06e50"),
    "patch": ramp("#2a3a4a", "#3e5468", "#587088"),
    "hat": ramp("#5a4420", "#7e6230", "#a08040", "#c2a058", "#dcbe74"),
    "straw": ramp("#8a6e2c", "#b08e3c", "#d2b050", "#ecd072"),
    "sickle": ramp("#3a3e4c", "#5c6272", "#8a92a4", "#c4cad8", "#eef0f6"),
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
    comp(cv, hp, OUT)
    if iron:
        # riveted iron plates over the hump and shoulders, rust at their edges
        pl = layer(FW, FH)
        iron_r = ramp("#2e3240", "#4a5064", "#6e7690", "#9aa2ba", "#c8cedc")
        for (dx0, dy0, w, h) in ((-6, -7, 6, 4), (0, -8, 6, 4), (5, -5, 5, 5), (-3, -3, 7, 3)):
            x0, y0 = hump_x + dx0, hump_y + dy0
            shaded_poly(pl, [(x0, y0 + 1), (x0 + 1, y0), (x0 + w, y0), (x0 + w, y0 + h - 1), (x0 + w - 1, y0 + h), (x0, y0 + h)],
                        iron_r, grad_dir=(0.3, 1.0), dither=0.15)
            for (rx, ry) in ((x0 + 1, y0 + 1), (x0 + w - 1, y0 + 1)):
                pl.px(int(rx), int(ry), iron_r[4])
            pl.px(int(x0 + w // 2), int(y0 + h), hx("#7a3a1a"))
        comp(cv, pl, OUT)
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
    if iron:
        # an iron mask over the brow, a spike on it
        mask = ramp("#3a4050", "#5c6478", "#8a92a8", "#b8c0d2")
        shaded_poly(hd, [(hx0 - 2, hy0 - 3), (hx0 + 3, hy0 - 3.5), (hx0 + 4.5, hy0 - 0.5), (hx0 + 1, hy0 + 0.5), (hx0 - 2, hy0 - 0.5)],
                    mask, grad_dir=(0.4, 1.0), dither=0.1)
        hd.px(int(hx0 + 1), int(hy0 - 2), mask[3])
        hd.px(int(hx0 + 4), int(hy0 - 4), mask[3])
        hd.px(int(hx0 + 4), int(hy0 - 3), mask[2])
    comp(cv, hd, OUT)
    # horns: from the top of the head, out and up
    hn = layer(FW, FH)
    hr = C["horn"] if not iron else ramp("#3a3e4c", "#5c6272", "#8a92a4", "#c4cad8")
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


# ============================================================================
# Bù Nhìn Sống
# ============================================================================

def scarecrow(tilt=0.0, arms=0.0, lift=0.0, spin=0.0, sickles=0.0, eyes="stitch", slump=0.0, dead=0.0, hop=0):
    """A scarecrow on its post: a burlap head under a straw hat, a faded shirt on the crossbar,
    straw bristling from the sleeves. tilt: the head's lean (−1..1); arms: the crossbar raised (−)
    or dropped (+); spin: the arms swung round (0..1: one side toward the viewer); sickles: blades
    out of the sleeves (0..1); eyes: "stitch" (sewn, a straw one), "glow" (alive), "x" (fallen);
    slump: sagging on its post; dead: a heap of straw; hop: 0..3, the post bouncing."""
    FW, FH = 32, 38
    cv = Canvas(FW, FH)
    cx = 15
    base = FH - 2
    bob = [0, -1, -2, -1][hop % 4] if hop else 0
    if dead > 0.5:
        # the heap: the shirt and hat on a pile of straw, the post snapped
        pile = layer(FW, FH)
        shaded_ellipse(pile, cx, base - 3, 10, 3.5, C["straw"], dither=0.4, bias=0.05)
        for k in range(12):
            x = cx - 9 + k * 1.6
            pile.line(x, base - 4, x + (1 if k % 2 else -1), base - 7 - (k % 3), C["straw"][2 + k % 2])
        comp(cv, pile, OUT)
        sh = layer(FW, FH)
        shaded_poly(sh, [(cx - 7, base - 3), (cx - 1, base - 7), (cx + 5, base - 5), (cx + 2, base - 1), (cx - 6, base - 1)], C["shirt"][1:], dither=0.3)
        comp(cv, sh, OUT)
        pt = layer(FW, FH)
        pt.line(cx - 12, base - 2, cx - 2, base - 5, C["post"][2], 2)
        comp(cv, pt, OUT)
        hd = layer(FW, FH)
        shaded_ellipse(hd, cx + 8, base - 5, 3.5, 3, C["sack"][1:], dither=0.2)
        comp(cv, hd, OUT)
        hat = layer(FW, FH)
        shaded_ellipse(hat, cx + 9, base - 8, 5, 1.6, C["hat"][1:], dither=0.2)
        comp(cv, hat, OUT)
        if dead < 1.0:
            for k in range(6):
                cv.px(cx - 6 + k * 3, base - 9 - (k % 2) * 2, C["straw"][3])
        return cv
    # the post: from the ground up behind the shirt
    pt = layer(FW, FH)
    top = 9 + bob + slump * 2
    for y in range(int(top), base + 1):
        pt.px(cx, y, C["post"][2])
        pt.px(cx + 1, y, C["post"][1])
    # the crossbar (the arms): level, raised or dropped, or swung round in the spin
    ay = 15 + bob + arms * 3 + slump * 2
    reach = 11 * (1 - 0.55 * math.sin(spin * math.pi)) if spin else 11
    lean = (arms * 2.0) if not spin else math.cos(spin * math.pi * 2) * 3
    pt.line(cx - reach, ay + lean, cx + reach, ay - lean, C["post"][3], 2)
    comp(cv, pt, OUT)
    # the shirt: body and sleeves on the crossbar, tattered hem, a blue patch
    shirt = layer(FW, FH)
    body_top, body_bot = ay - 1, ay + 10 - slump
    shaded_poly(shirt, [(cx - 5, body_top), (cx + 6, body_top), (cx + 5, body_bot), (cx + 2, body_bot + 1), (cx - 1, body_bot - 1),
                        (cx - 3, body_bot + 1), (cx - 5, body_bot)], C["shirt"], grad_dir=(0.6, 1.0), dither=0.3)
    for side in (-1, 1):
        x_end = cx + side * reach
        y_end = ay - side * lean
        shaded_poly(shirt, [(cx + side * 3, ay - 2), (x_end - side * 1, y_end - 2), (x_end - side * 1, y_end + 2), (cx + side * 4, ay + 3)],
                    C["shirt"][1:], grad_dir=(0.3 * side, 1.0), dither=0.3)
    for (x, y) in ((cx - 3, ay + 4), (cx - 2, ay + 4), (cx - 3, ay + 5), (cx - 2, ay + 5)):
        x, y = int(x), int(y)
        if shirt.opaque(x, y):
            shirt.px(x, y, C["patch"][1 + (x + y) % 2])
    # a rope belt
    shirt.hline(cx - 4, cx + 5, int(ay + 6), C["sack"][1])
    comp(cv, shirt, OUT)
    # straw from the sleeves, the collar and under the hem
    st = layer(FW, FH)
    for side in (-1, 1):
        x_end = cx + side * reach
        y_end = ay - side * lean
        # a fan of bristles out of the cuff
        for k in range(3):
            st.line(x_end, y_end - 1 + k, x_end + side * 3, y_end - 3 + k * 2.6, C["straw"][1 + k])
    for k in range(5):
        x = cx - 3 + k * 1.6
        st.line(x, body_bot, x + (k % 2) - 0.5, body_bot + 2 + (k % 2), C["straw"][2])
    comp(cv, st, None)
    # sickles sliding out of the sleeves
    if sickles > 0:
        sk = layer(FW, FH)
        for side in (-1, 1):
            x_end = cx + side * reach
            y_end = ay - side * lean
            length = 5 * sickles
            pts = []
            for i in range(8):
                t = i / 7
                a = -math.pi / 2 + t * math.pi * 0.9
                pts.append((x_end + side * (1 + math.cos(a) * length * 0.8 + length * 0.2), y_end + math.sin(a) * length))
            for i in range(len(pts) - 1):
                sk.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], C["sickle"][3 if i < 4 else 2])
            sk.px(int(pts[0][0]), int(pts[0][1]), C["sickle"][4])
        comp(cv, sk, OUT)
    # the head: a stuffed sack, tied at the neck, leaning
    hd = layer(FW, FH)
    hx0 = cx + 0.5 + tilt * 1.6
    hy0 = ay - 5 + abs(tilt) * 0.6 + slump
    shaded_ellipse(hd, hx0, hy0, 4.2, 4.0, C["sack"], dither=0.25, bias=0.05)
    hd.hline(int(hx0 - 2), int(hx0 + 2), int(hy0 + 4), C["sack"][0])
    comp(cv, hd, OUT)
    # the face: sewn eyes and a stitched grin, or eyes glowing when it wakes
    ex, ey = int(hx0 + 0.5), int(hy0 - 0.5)
    if eyes == "glow":
        for (x, y) in ((ex - 2, ey), (ex + 1, ey)):
            cv.px(x, y, hx("#ffb030"))
            cv.px(x + 1, y, hx("#fff0a0"))
    elif eyes == "x":
        for x in (ex - 2, ex + 1):
            cv.px(x, ey, OUTLINE)
            cv.px(x + 1, ey + 1, OUTLINE)
    else:
        for x in (ex - 2, ex + 1):
            cv.px(x, ey, OUT)
            cv.px(x + 1, ey, OUT)
    for k in range(5):
        cv.px(ex - 2 + k, ey + 2 + (1 if k in (0, 4) else 0), OUT if k % 2 == 0 else C["sack"][1])
    # the straw hat, wide brim
    hat = layer(FW, FH)
    hat_y = hy0 - 3.5
    shaded_ellipse(hat, hx0 + tilt * 0.8, hat_y + 0.6, 7, 1.6, C["hat"][1:], dither=0.2)
    shaded_ellipse(hat, hx0 + tilt * 1.1, hat_y - 1.2, 3.4, 2.2, C["hat"], dither=0.2, bias=0.1)
    hat.hline(int(hx0 - 3 + tilt), int(hx0 + 3 + tilt), int(hat_y - 0.2), C["shirt"][2])
    comp(cv, hat, OUT)
    return cv


def scarecrow_still():
    """The straw scarecrow as it stands (the prop, and the living one pretending)."""
    return scarecrow(tilt=0.35)


def build_scarecrow():
    A = {}
    A["still"] = [scarecrow_still()]
    A["wake"] = [scarecrow(tilt=-0.4, eyes="glow", slump=0.5), scarecrow(tilt=0.6, eyes="glow", arms=-0.5, sickles=0.5),
                 scarecrow(tilt=0.0, eyes="glow", arms=-0.8, sickles=1.0)]
    A["idle"] = [scarecrow(tilt=0.2, eyes="glow", sickles=1.0, hop=1), scarecrow(tilt=0.0, eyes="glow", sickles=1.0, hop=2, arms=0.3)]
    A["move"] = [scarecrow(tilt=0.3, eyes="glow", sickles=1.0, hop=k, arms=0.4 if k % 2 else -0.2) for k in range(4)]
    A["windup"] = [scarecrow(tilt=-0.5, eyes="glow", sickles=1.0, arms=-1.0), scarecrow(tilt=-0.6, eyes="glow", sickles=1.0, arms=-1.2)]
    A["attack"] = [scarecrow(tilt=0.2, eyes="glow", sickles=1.0, spin=k / 4.0) for k in range(4)]
    A["hurt"] = [scarecrow(tilt=0.9, eyes="x", sickles=0.6, slump=1.0)]
    A["dead"] = [scarecrow(dead=0.7), scarecrow(dead=1.0)]
    return A


# ============================================================================
# Lốc Xoáy (Thủ Lĩnh Hắc Phong's whirlwind)
# ============================================================================

def tornado(frame=0):
    """A whirlwind of sand, seen from the side: a funnel wide at the top and thin at its foot,
    bands of dust spiralling round it (shifted a quarter turn each frame), grit flying off."""
    FW, FH = 34, 46
    cv = Canvas(FW, FH)
    cx = FW / 2
    sand = ramp("#6a5030", "#8e6e42", "#b08e5a", "#ceae78", "#e8d09c", "#f8ecc8")
    top, foot = 3, FH - 3
    for y in range(top, foot + 1):
        t = (y - top) / (foot - top)            # 0 at the top, 1 at the foot
        half = 14 * (1 - t) ** 1.25 + 1.6
        sway = math.sin(t * 5.0 + frame * 0.6) * 2.2 * (1 - t) + math.sin(t * 9 + frame) * 0.6
        for x in range(FW):
            d = (x + 0.5 - (cx + sway)) / half
            if abs(d) > 1:
                continue
            # the spiral bands: phase runs down the funnel and round it
            band = math.sin((t * 13.0 - d * 3.2 + frame * math.pi / 2))
            inten = 0.5 - d * 0.4 + (0.38 if band > 0.35 else -0.2 if band < -0.5 else 0.05)
            i = max(0, min(len(sand) - 1, int((inten + 0.2) * (len(sand) - 1))))
            if abs(d) > 0.86 and (x + y + frame) % 3 == 0:
                continue   # a ragged edge
            if y < top + 3 and (x * 7 + y * 3 + frame) % 4 == 0:
                continue   # the cloud it rises into, torn
            cv.px(x, y, sand[i])
    # grit flung off the sides
    rnd = random.Random(frame * 7 + 3)
    for k in range(10):
        t = rnd.uniform(0.05, 0.8)
        y = int(top + t * (foot - top))
        side = 1 if k % 2 else -1
        x = int(cx + side * (14 * (1 - t) ** 1.25 + 3 + rnd.uniform(0, 3)))
        cv.px(x, y, sand[4 + (k % 2)])
    cv.outline(OUT)
    # a dust cloud at the foot
    for k in range(7):
        x = cx - 6 + k * 2 + (frame % 2)
        cv.px(int(x), foot, sand[2])
        cv.px(int(x) + 1, foot - 1, sand[3])
    return cv


def build_tornado():
    return {"spin": [tornado(f) for f in range(4)]}


def build_iron_bison():
    """Bò Rừng Sắt: the bison clad in iron, with the clips a boss needs (walk, roar)."""
    A = build_bison(iron=True)
    A["walk"] = A.pop("move")
    A["roar"] = [bison(lower=0.0, eyes="wide", iron=True), bison(lower=-0.4, eyes="wide", iron=True, paw=True, step=1)]
    order = ["idle", "walk", "windup", "attack", "roar", "hurt", "dead"]
    return {k: A[k] for k in order}


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in (("hyena", build_hyena), ("eagle", build_eagle), ("bison", build_bison), ("scarecrow", build_scarecrow),
                     ("ironbison", build_iron_bison), ("tornado", build_tornado)):
        A = fn()
        frames = [f for k in A for f in A[k]]
        preview(pack_grid(frames, 8).to_image(), 5, os.path.join(out, f"steppe_{name}.png"))
    print("ok")
