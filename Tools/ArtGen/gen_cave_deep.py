"""
The deeper creatures of Hang Pha Lê: Bọ Giáp Đá (stone-shelled beetle), Slime Pha Lê (crystal
slime), Mắt Hang (the eye in the wall), Mimic Tham Lam (the chest that bites), and the cave's two
bosses: Golem Pha Lê Cổ (the old crystal golem, its core in its back) and Nhện Chúa Pha Lê (the
spider queen). Drawn like gen_cave_creatures: procedural shaded shapes, facing right.
Every build_* returns {animation: [Canvas frames]}.
"""
import math
import random

from pixelkit import Canvas, P, OUTLINE, hx, ramp, mix, shaded_ellipse, shaded_poly, point_in_poly
from gen_enemies import shaded_capsule, layer, comp, build_slime
from gen_cave_creatures import C, OUT, EYE_RED, EYE_CYAN
import gen_props

D = {
    "chitin": ramp("#0e0c10", "#1a161e", "#28222e", "#383040"),
    "belly": ramp("#2a1420", "#44202e", "#62303e", "#84464e", "#a8645e"),
    "amber": ramp("#361e08", "#58320c", "#824c12", "#aa6e1c", "#d29632", "#f0c464", "#fff0c0"),
    "pink": ramp("#2a0a34", "#461054", "#6a1a7a", "#9428a2", "#c048c8", "#e888ea", "#ffdcff"),
    "eyeball": ramp("#5a5f70", "#8a90a0", "#c4cad6", "#eef2f8"),
    "tongue": ramp("#3a0a14", "#6a1424", "#9a2436", "#c84a58"),
    "gums": ramp("#2a0610", "#4a0c1a", "#6e1626"),
    "tooth": ramp("#8a8270", "#c8c0aa", "#f0ead8"),
    "queen": ramp("#0a0810", "#141020", "#201a30", "#2c2440", "#3a3054", "#4a3e68"),
}
# the slime's own palette, crystal blue
P["crystalslime"] = ramp("#0c2a44", "#12466a", "#1c6a96", "#3494c2", "#72c6e6", "#d0f4ff")


def crystal_spike(cv, x, y, h, w, col, lean=0.0):
    """A small upright crystal spike with its tip at (x + lean, y - h)."""
    pts = [(x - w, y), (x + lean, y - h), (x + w, y)]
    for yy in range(int(y - h) - 1, int(y) + 1):
        for xx in range(int(x - w) - 1, int(x + w) + 2):
            if point_in_poly(xx + 0.5, yy + 0.5, pts):
                u = (xx + 0.5 - (x + lean * (y - yy) / max(h, 1))) / max(w, 0.5)
                cv.px(xx, yy, col[5] if u < -0.2 else col[4] if u < 0.25 else col[2])
    cv.px(int(x + lean), int(y - h), col[6] if len(col) > 6 else col[5])


# ============================================================================
# Bọ Giáp Đá
# ============================================================================

def beetle(step=0, lower=0.0, snap=0.0, eyes="open", dead=0.0, lunge=0.0):
    """A beetle under a dome of cave rock studded with crystal; its soft rear shows at the back
    (left). lower: head down, pawing (the charge's windup); snap: mandibles closing; lunge:
    thrown forward; dead: on its side, legs curled."""
    FW, FH = 34, 26
    cv = Canvas(FW, FH)
    base = 24
    st = C["stone"]
    ch = D["chitin"]
    cx = 15 + lunge * 2
    sy = base - 8 + dead * 2
    # far legs
    far = layer(FW, FH)
    for i, lx in enumerate((10, 16, 22)):
        ph = (step + i) % 4
        lift = (1.5 if ph == 1 else 0.5 if ph == 2 else 0) * (1 - dead)
        fx = lx + lunge * 2 + (1 if i == 2 else -1) + (1 if ph == 3 else 0)
        if dead > 0.5:
            far.line(lx - 1, sy + 1, lx + 1, sy - 3, ch[1])
        else:
            far.line(lx - 1, base - 4, fx - 1, base - lift, ch[1])
    comp(cv, far, OUT)
    # the soft rear, peeking out behind the shell
    bl = layer(FW, FH)
    shaded_ellipse(bl, cx - 9.5, sy + 2.5, 4.2, 3.6, D["belly"], dither=0.3, bias=0.05)
    comp(cv, bl, OUT)
    # the head, low in front
    hd = layer(FW, FH)
    hx0, hy0 = cx + 10.5 + lower * 1.0, sy + 3 + lower * 2.5
    shaded_ellipse(hd, hx0, hy0, 3.8, 3.2, ch, dither=0.25, bias=0.08)
    comp(cv, hd, OUT)
    # mandibles: two hooks, open unless snapping
    op = 1.0 - snap
    for side in (-1, 1):
        mx, my = hx0 + 3, hy0 + side * 1.0
        tx, ty = mx + 3, my + side * (1.8 * op + 0.3)
        cv.line(mx, my, tx, ty, D["tooth"][1])
        cv.px(int(tx), int(ty) - side, D["tooth"][2])
    # the shell: a dome of rock
    sh = layer(FW, FH)
    pts = [(cx - 11, sy + 4), (cx - 9, sy - 3), (cx - 3, sy - 7), (cx + 4, sy - 7), (cx + 9, sy - 3), (cx + 11, sy + 4)]
    shaded_poly(sh, pts, st[1:], grad_dir=(0.35, 1.0), dither=0.3)
    # ridges and cracks across it
    for k in (-5, 0, 5):
        for t in range(6):
            x = int(cx + k + t * 0.2)
            y = int(sy - 6 + t * 1.6)
            if sh.opaque(x, y):
                sh.px(x, y, st[1])
    comp(cv, sh, OUT)
    # crystal studs along its crest
    for (dx, h) in ((-6, 3), (-1, 4), (4, 3)):
        crystal_spike(cv, cx + dx, sy - 5.5 - (1 if dx == -1 else 0), h, 1.3, C["crys"], lean=0.4)
    # near legs
    near = layer(FW, FH)
    for i, lx in enumerate((9, 15, 21)):
        ph = (step + i + 2) % 4
        lift = (1.5 if ph == 1 else 0.5 if ph == 2 else 0) * (1 - dead)
        fx = lx + lunge * 2 + (1 if i == 2 else -1) + (1 if ph == 3 else 0)
        if dead > 0.5:
            near.line(lx, sy + 3, lx + 2, sy - 1, ch[3])
        else:
            near.line(lx, base - 3.5, fx, base - lift, ch[3])
            near.px(int(fx), int(base - lift), ch[2])
    comp(cv, near, OUT)
    # eye
    ex, ey = int(hx0 + 1), int(hy0 - 1)
    if eyes == "open":
        cv.px(ex, ey, EYE_RED[0])
        cv.px(ex + 1, ey, EYE_RED[1])
    else:
        cv.px(ex, ey, OUTLINE)
        cv.px(ex + 1, ey + 1, OUTLINE)
    return cv


def build_beetle():
    A = {}
    A["idle"] = [beetle(step=0), beetle(step=0, snap=0.3)]
    A["move"] = [beetle(step=k) for k in range(4)]
    A["windup"] = [beetle(lower=0.6, step=1), beetle(lower=1.0, step=3)]
    A["attack"] = [beetle(snap=1.0, lunge=0.8), beetle(snap=0.5, lunge=0.4)]
    A["hurt"] = [beetle(eyes="x", lower=0.3)]
    A["dead"] = [beetle(eyes="x", dead=0.6), beetle(eyes="x", dead=1.0)]
    return A


# ============================================================================
# Slime Pha Lê
# ============================================================================

def build_crystal_slime():
    """The moss slime in crystal blue, the moss and sprout on its crown turned to crystal."""
    A = build_slime(tone="crystalslime")
    crys = C["crys"]
    swaps = [(P["moss"][i], crys[2 + min(i, 2)]) for i in range(len(P["moss"]))]
    swaps += [(P["leaf"][i], crys[3 + min(i // 2, 2)]) for i in range(len(P["leaf"]))]
    swaps += [(P["slime"][i], P["crystalslime"][i]) for i in range(len(P["slime"]))]   # the puddle it leaves
    for frames in A.values():
        for cv in frames:
            for src, dst in swaps:
                cv.recolor(src, dst)
            # two more spikes on its crown
            top = None
            for y in range(cv.h):
                if any(cv.opaque(x, y) for x in range(9, 15)):
                    top = y
                    break
            if top is None or top > 18:
                continue
            crystal_spike(cv, 9.5, top + 3, 3, 1.1, crys, lean=-0.6)
            crystal_spike(cv, 15, top + 3.5, 2.5, 1.0, crys, lean=0.6)
    return A


# ============================================================================
# Mắt Hang
# ============================================================================

def cave_eye(open_=0.0, glow=0.0, look=0.0, eyes="open", crack=0.0):
    """A great eye in a socket of rock with crystal lashes. open_: the stone lids part (0 shut
    .. 1 wide); glow: the iris burns; look: the pupil turned left/right (-1..1); crack: dead."""
    FW, FH = 28, 28
    cv = Canvas(FW, FH)
    cx, cy = 14.0, 14.5
    st = C["stone"]
    cr = C["crys"]
    # the socket: a lump of rock
    so = layer(FW, FH)
    shaded_ellipse(so, cx, cy + 1, 12, 10.5, st[1:], dither=0.3, bias=0.03)
    comp(cv, so, OUT)
    # crystal lashes around it
    for k in range(7):
        a = math.radians(200 + k * 23)
        x = cx + math.cos(a) * 11
        y = cy + 1 + math.sin(a) * 9.5
        crystal_spike(cv, x, y + 1, 3 + (k % 2), 1.0, cr, lean=math.cos(a) * 1.5)
    # the hollow
    ho = Canvas(FW, FH)
    shaded_ellipse(ho, cx, cy + 1, 8, 6.2, [hx("#05060a"), hx("#0a0c14"), hx("#10131e")], dither=0.2)
    cv.blit(ho, 0, 0)
    # the eyeball, seen between the lids
    gap = 5.8 * max(0.0, min(1.0, open_))
    if gap > 0.4:
        ball = Canvas(FW, FH)
        shaded_ellipse(ball, cx, cy + 1, 7, 5.6, D["eyeball"], dither=0.25, bias=0.1,
                       clip=lambda x, y: abs(y + 0.5 - (cy + 1)) <= gap)
        # the iris and pupil
        ix = cx + look * 2.5
        iris = cr[0:5] if glow < 0.5 else cr[1:6]
        shaded_ellipse(ball, ix, cy + 1, 3.4, 3.4, iris, dither=0.2, bias=-0.05 + glow * 0.2,
                       clip=lambda x, y: abs(y + 0.5 - (cy + 1)) <= gap)
        if eyes == "open":
            for dy in (0, 1):
                if abs(dy + 0.5) <= gap:
                    ball.px(int(ix), int(cy + dy), hx("#04060c") if glow < 0.7 else hx("#ffffff"))
            ball.px(int(ix - 1), int(cy - 1), hx("#ffffff"))
        else:
            ball.line(ix - 2, cy - 1, ix + 2, cy + 3, OUTLINE)
            ball.line(ix - 2, cy + 3, ix + 2, cy - 1, OUTLINE)
        cv.blit(ball, 0, 0)
    # the lids of stone closing over it
    lid = layer(FW, FH)
    for y in range(int(cy + 1 - 7), int(cy + 1 + 7)):
        for x in range(int(cx - 9), int(cx + 10)):
            dx = (x + 0.5 - cx) / 8.4
            dy = (y + 0.5 - (cy + 1)) / 6.6
            if dx * dx + dy * dy > 1:
                continue
            if abs(y + 0.5 - (cy + 1)) <= gap:
                continue
            lid.px(x, y, st[3] if y < cy + 1 else st[2])
    # the lids' lips
    for x in range(int(cx - 8), int(cx + 9)):
        for sgn in (-1, 1):
            y = int(round(cy + 1 + sgn * (gap + 0.5)))
            if lid.opaque(x, y):
                lid.px(x, y, st[5] if sgn < 0 else st[1])
    comp(cv, lid, None)
    if glow > 0.3 and gap > 0.4:
        # the light spilling from it
        for (dx, dy) in ((-9, 0), (9, 0), (-7, -3), (7, -3), (0, -6)):
            cv.px(int(cx + dx), int(cy + 1 + dy), cr[5] if glow > 0.7 else cr[4])
    if crack > 0:
        cv.line(cx - 6, cy - 5, cx - 1, cy + 1, st[0])
        cv.line(cx - 1, cy + 1, cx + 5, cy - 2, st[0])
        cv.line(cx + 2, cy + 4, cx + 7, cy + 7, st[0])
    return cv


def build_cave_eye():
    A = {}
    A["idle"] = [cave_eye(0.0), cave_eye(0.12)]
    A["move"] = [cave_eye(0.0), cave_eye(0.12)]
    A["windup"] = [cave_eye(0.5, 0.3), cave_eye(0.85, 0.6, look=0.3), cave_eye(1.0, 0.9, look=0.3)]
    A["attack"] = [cave_eye(1.0, 1.0), cave_eye(0.9, 0.8)]
    A["hurt"] = [cave_eye(0.6, 0.0, eyes="x")]
    A["dead"] = [cave_eye(0.3, 0.0, eyes="x", crack=0.5), cave_eye(0.0, 0.0, crack=1.0)]
    return A


# ============================================================================
# Mimic Tham Lam
# ============================================================================

def mimic(open_=0.0, bob=0, tongue=0.0, eyes="open", sink=0.0, chomp=0.0):
    """The boss chest (gen_props.treasure_chest) that is not a chest: open_ lifts its lid over a
    maw of teeth with a tongue and two eyes; sink: dug halfway into the ground; chomp: jaws
    slammed shut with a lurch forward."""
    FW, FH = 30, 30
    cv = Canvas(FW, FH)
    chest, _ = gen_props.treasure_chest(False)
    ox, oy = 3, 5 + bob
    if open_ <= 0.01 and sink <= 0.01:
        cv.blit(chest, ox, oy)
        return cv
    top = 13   # the lid line in the chest's own drawing
    lift = int(round(open_ * 7))
    lean = int(round(open_ * 2)) - int(round(chomp * 1))
    fwd = int(round(chomp * 2))
    # the maw between lid and box
    maw = Canvas(FW, FH)
    if lift > 0:
        for y in range(oy + top - lift - 1, oy + top + 1):
            for x in range(ox + 3, ox + 20):
                maw.px(x + fwd, y, D["gums"][1] if y > oy + top - lift + 1 else D["gums"][0])
        # the tongue lolling out of the front
        if tongue > 0:
            for i in range(int(3 + tongue * 5)):
                x = ox + 12 + i + fwd
                y = oy + top - 1 + int(math.sin(i * 0.9) * 1.2) + i // 3
                maw.px(x, y, D["tongue"][3 if i % 2 else 2])
                maw.px(x, y + 1, D["tongue"][1])
        # eyes glowing in the dark of the maw, under the lid
        if eyes == "open":
            for ex in (ox + 8, ox + 14):
                maw.px(ex + fwd, oy + top - lift + 1, hx("#ffd24a"))
                maw.px(ex + 1 + fwd, oy + top - lift + 1, hx("#fff2a0"))
        elif eyes == "x":
            for ex in (ox + 8, ox + 14):
                maw.px(ex + fwd, oy + top - lift + 1, OUTLINE)
    cv.blit(maw, 0, 0)
    # the box (lower part of the chest)
    box = Canvas(24, 24)
    for y in range(top, 24):
        for x in range(24):
            if chest.opaque(x, y):
                box.px(x, y, chest.get(x, y))
    # the lid (upper part), lifted and leaning back
    lid = Canvas(24, 24)
    for y in range(0, top):
        for x in range(24):
            if chest.opaque(x, y):
                lid.px(x, y, chest.get(x, y))
    cut = int(round(sink * 12))
    sunk = Canvas(FW, FH)
    sunk.blit(box, ox + fwd, oy + cut)
    sunk.blit(lid, ox - lean + fwd, oy - lift + cut)
    # teeth along the box's rim and under the lid
    if lift > 1:
        for x in range(ox + 3, ox + 20, 2):
            sunk.px(x + fwd, oy + top - 1 + cut, D["tooth"][2])
            sunk.px(x + 1 + fwd, oy + top - 1 + cut, D["tooth"][1])
            sunk.px(x - lean + fwd, oy + top - lift + cut, D["tooth"][2])
            sunk.px(x - lean + fwd, oy + top - lift + 1 + cut, D["tooth"][1])
    # dug into the ground: nothing shows below the floor line
    floor = oy + 22
    for y in range(floor, FH):
        for x in range(FW):
            if sunk.opaque(x, y):
                sunk.clear(x, y)
    cv.blit(sunk, 0, 0)
    if sink > 0:
        # a ring of broken earth around it
        for x in range(ox + 1, ox + 23):
            cv.px(x, floor - (1 if x % 3 == 0 else 0), C["stone"][2 + x % 2])
            cv.px(x, floor, C["stone"][1])
    return cv


def build_mimic():
    A = {}
    A["hidden"] = [mimic()]
    A["spring"] = [mimic(0.4, bob=-1), mimic(1.0, bob=-2, tongue=0.5), mimic(0.8, tongue=1.0)]
    A["idle"] = [mimic(0.7, tongue=0.8), mimic(0.8, bob=-1, tongue=1.0)]
    A["move"] = [mimic(0.5, bob=0), mimic(0.9, bob=-3, tongue=0.6), mimic(0.6, bob=-1)]
    A["windup"] = [mimic(1.0, bob=-1, tongue=0.3), mimic(1.1, bob=-2)]
    A["attack"] = [mimic(0.1, chomp=1.0), mimic(0.4, chomp=0.5, tongue=0.4)]
    A["burrow"] = [mimic(0.3, sink=0.3), mimic(0.1, sink=0.65), mimic(0.0, sink=1.0)]
    A["emerge"] = [mimic(0.0, sink=0.9), mimic(0.3, sink=0.5), mimic(0.6, sink=0.1)]
    A["hurt"] = [mimic(0.6, eyes="x")]
    A["dead"] = [mimic(1.0, eyes="x", tongue=1.0), mimic(1.2, bob=1, eyes="x", tongue=1.0)]
    return A


# ============================================================================
# Golem Pha Lê Cổ
# ============================================================================

def old_golem(bob=0, arms=0.0, slam=0.0, step=0, eyes="open", crumble=0.0, spin=-1, core=1.0, cast=0.0):
    """The old crystal golem, twice a small golem's size: rock grown over with great crystals,
    one amber eye, and its core — a burning amber heart — set in its back (left). arms: fists
    raised; slam: brought down in front; spin: 0..2, arms flung out around it; cast: the eye and
    chest blaze; crumble: falling apart, the core going dark."""
    FW, FH = 56, 56
    cv = Canvas(FW, FH)
    base = 53
    st = C["stone"]
    cr = C["crys"]
    am = D["amber"]
    rnd = random.Random(11)
    if crumble >= 0.8:
        for i in range(10):
            x = 10 + i * 3.8 + rnd.uniform(-1.5, 1.5)
            y = base - 3 - (i % 3) * 2
            part = layer(FW, FH)
            shaded_ellipse(part, x, y, 4.6, 3.4, st[1:], dither=0.3, bias=0.05)
            comp(cv, part, OUT)
        for (x, y, h) in ((17, base - 6, 6), (29, base - 8, 8), (38, base - 5, 5)):
            crystal_spike(cv, x, y, h, 1.8, cr)
        cv.px(24, base - 4, am[2]); cv.px(25, base - 4, am[1])
        return cv
    cx = FW / 2
    cy = base - 20 + bob + crumble * 6
    # legs
    lg = layer(FW, FH)
    for i, side in enumerate((-1, 1)):
        lift = 3 if (step % 2 == i and step > 0) else 0
        shaded_capsule(lg, cx + side * 6.5, cy + 12, cx + side * 7, base - 3 - lift, 4.4, st, bias=-0.05)
    comp(cv, lg, OUT)
    # the far arm (behind the body) when flung out in a spin
    def arm(canvas, side, far=False):
        sx, sy = cx + side * 13, cy - 7
        if spin >= 0:
            a = math.radians((spin * 60 + (0 if side > 0 else 180)) % 360)
            ex_, ey_ = sx + math.cos(a) * 11 * side, sy + math.sin(a) * 4 + 2
        elif slam > 0:
            ex_, ey_ = sx + side * 2 + 5, cy + 10 + slam * 3
        else:
            a = math.radians(15 + arms * 150)
            ex_, ey_ = sx + side * math.sin(a) * 9, sy + math.cos(a) * 11
        rmp = st if not far else st[:-2]
        shaded_capsule(canvas, sx, sy, ex_, ey_, 3.8, rmp, bias=0.02)
        shaded_ellipse(canvas, ex_, ey_, 5.6, 5, rmp[1:], dither=0.3, bias=0.08)
        return ex_, ey_
    # the back arm (its left, away from the viewer) behind the body
    back = layer(FW, FH)
    bfx, bfy = arm(back, -1, far=True)
    comp(cv, back, OUT)
    crystal_spike(cv, bfx - 1.5, bfy - 3, 3, 1.2, cr, lean=-0.8)
    # the body
    bd = layer(FW, FH)
    pts = [(cx - 14, cy - 6), (cx - 10, cy - 15), (cx + 9, cy - 16), (cx + 14, cy - 5), (cx + 12, cy + 12), (cx - 12, cy + 12.5)]
    shaded_poly(bd, pts, st[1:], grad_dir=(0.45, 0.9), dither=0.3)
    for (x, y) in ((cx - 6, cy - 3), (cx + 5, cy + 5), (cx - 2, cy + 8), (cx + 8, cy - 8)):
        bd.px(int(x), int(y), st[0]); bd.px(int(x) + 1, int(y), st[1]); bd.px(int(x) + 1, int(y) + 1, st[0])
    for x in range(int(cx - 10), int(cx + 10)):
        yy = int(cy - 15) + (1 if x % 3 else 0)
        if bd.opaque(x, yy):
            bd.px(x, yy, C["moss"][1 + x % 2])
    comp(cv, bd, OUT)
    # the core in its back: a burning amber heart in a ring of stone
    kx, ky = cx - 12, cy - 2
    ring = layer(FW, FH)
    shaded_ellipse(ring, kx, ky, 6, 6.5, st[2:], dither=0.2)
    comp(cv, ring, OUT)
    lit = max(0.0, min(1.0, core * (1 - crumble)))
    heart = am if lit > 0.5 else [mix(c, hx("#20140a"), 0.6) for c in am]
    shaded_ellipse(cv, kx, ky, 4.2, 4.6, heart[2:7], dither=0.2, bias=0.1 + cast * 0.15)
    if lit > 0.5:
        cv.px(int(kx - 1), int(ky - 2), am[6]); cv.px(int(kx), int(ky - 2), am[6])
        for (dx, dy) in ((-6, 0), (0, -6), (-4, -4), (-4, 4)):
            cv.px(int(kx + dx), int(ky + dy), am[5])
    # great crystals on its shoulders and crown
    for (dx, dy, h, w, lean) in ((-7, -14, 11, 2.4, -1.5), (-2, -16, 14, 2.8, 0.5), (4, -15, 9, 2.2, 1.5), (10, -9, 7, 2.0, 2.5)):
        crystal_spike(cv, cx + dx, cy + dy + 1, h, w, cr, lean=lean)
    # the eye
    ex, ey = int(cx + 6), int(cy - 7)
    if eyes == "open":
        glow = am[5] if cast < 0.5 else am[6]
        for dx in range(3):
            cv.px(ex + dx, ey, glow if dx == 1 else am[4])
        if cast > 0.5:
            cv.px(ex + 1, ey - 1, am[6]); cv.px(ex + 3, ey, am[5])
    else:
        cv.px(ex, ey, OUTLINE); cv.px(ex + 1, ey + 1, OUTLINE); cv.px(ex + 2, ey, OUTLINE)
    # the arms and fists (crystal-studded knuckles)
    al = layer(FW, FH)
    fx, fy = arm(al, 1)
    comp(cv, al, OUT)
    crystal_spike(cv, fx + 1.5, fy - 3, 3, 1.2, cr, lean=0.8)
    return cv


def build_old_golem():
    A = {}
    A["idle"] = [old_golem(), old_golem(bob=1)]
    A["walk"] = [old_golem(step=1), old_golem(step=0, bob=1), old_golem(step=2), old_golem(step=0, bob=1)]
    A["windup"] = [old_golem(arms=0.6), old_golem(arms=1.0, bob=-1)]
    A["slam"] = [old_golem(slam=1.0, bob=2), old_golem(slam=0.6, bob=1)]
    A["spin"] = [old_golem(spin=k) for k in range(6)]
    A["cast"] = [old_golem(cast=0.6, arms=0.3), old_golem(cast=1.0, arms=0.4, bob=-1)]
    A["roar"] = [old_golem(arms=0.8, cast=0.7), old_golem(arms=1.0, cast=1.0, bob=-1)]
    A["hurt"] = [old_golem(eyes="x", bob=1, core=0.4)]
    A["dead"] = [old_golem(eyes="x", crumble=0.4, core=0.3), old_golem(crumble=1.0)]
    return A


# ============================================================================
# Nhện Chúa Pha Lê
# ============================================================================

def queen(step=0, rear=0.0, bite=0.0, eyes="open", curl=0.0, glow=0.0, climb=0.0, splay=0.0):
    """The spider queen from the side, drawn like the cave spider at twice its size: a great
    abdomen crusted with crystal and a glowing crown on her head, eight long legs. rear: the front
    raised (her beam, her webs); bite: fangs thrust forward; glow: her crystals blaze; climb:
    legs raised to the ceiling; splay: landing, legs flat out; curl: dead."""
    FW, FH = 80, 64
    cv = Canvas(FW, FH)
    base = 61
    k = 2.2
    sp = D["queen"]
    lg = C["leg"]
    cr = C["crys"]
    ax, ay = 28.0 - bite * 2, base - 15.0 - rear * 3 + climb * 4 - splay * 3   # abdomen
    hx0, hy0 = 46.0 + bite * 4, base - 15.5 - rear * 8 + climb * 2 - splay * 2   # head
    root_x, root_y = hx0 - 6, hy0 + 1
    feet = [6.0, 20.0, 50.0, 70.0]

    def leg(canvas, i, near):
        phase = (step + i * 2 + (0 if near else 1)) % 4
        lift = (4.0 if phase == 1 else 2.0 if phase == 2 else 0.0) * (1 - curl)
        fx = feet[i] + (1.5 if near else -1.5) + bite * (3 if i >= 2 else 0)
        fy = base - lift
        if rear > 0.4 and i >= 2:
            fx, fy = root_x + 10 + (i - 2) * 7, root_y - 10 - (i - 2) * 2
        if climb > 0:
            fx, fy = root_x + (fx - root_x) * 0.7, fy - climb * (20 + i * 1.5)
        if splay > 0:
            fx = root_x + (fx - root_x) * 1.15
            fy = base - 1
        if curl > 0:
            fx = root_x + (fx - root_x) * (1 - curl * 0.7)
            fy = fy - curl * 7
        kx = root_x + (fx - root_x) * 0.45
        ky = max(4, min(root_y, fy) - 15 + curl * 9 - (1 if near else 3) + splay * 8)
        col = lg[3] if near else lg[1]
        canvas.line(root_x, root_y, kx, ky, col, width=2)
        canvas.line(kx, ky, fx, fy, col, width=2 if near else 1)
        if near:
            canvas.px(int(round(kx)), int(round(ky)), lg[2])
            # crystal spurs on the knees
            canvas.px(int(round(kx)), int(round(ky)) - 1, cr[4])

    far = layer(FW, FH)
    for i in range(4):
        leg(far, i, False)
    comp(cv, far, OUT)
    bd = layer(FW, FH)
    shaded_ellipse(bd, ax, ay, 15, 11.5 - curl * 2, sp, dither=0.3, bias=0.03)
    shaded_ellipse(bd, hx0, hy0, 7.5, 6.3, sp, dither=0.25, bias=0.06)
    comp(cv, bd, OUT)
    near = layer(FW, FH)
    for i in range(4):
        leg(near, i, True)
    comp(cv, near, OUT)
    # the crystal crust on her abdomen
    bright = [mix(c, hx("#ffffff"), 0.3 * glow) for c in cr]
    for (dx, dy, h, w, lean) in ((-9, -6, 7, 2.2, -1.5), (-3, -9, 10, 2.6, -0.3), (4, -8, 8, 2.2, 1.0), (9, -4, 5, 1.8, 2.0)):
        crystal_spike(cv, ax + dx, ay + dy + 2, h * (1 - curl * 0.4), w, bright, lean=lean)
    # the glowing mark on its back
    mk = C["mark"]
    for (dx, dy, kk) in ((-2, 1, 1), (-1, 1, 2), (0, 1, 2), (1, 1, 1), (-1, 2, 1), (0, 0, 1), (-1, 0, 2), (0, 2, 0)):
        cv.px(int(ax + dx), int(ay + dy), mk[kk] if glow < 0.5 else cr[5])
    # a crown of crystal on her head
    for (dx, h, lean) in ((-3, 5, -1.0), (0, 7, 0.0), (3, 5, 1.0)):
        crystal_spike(cv, hx0 + dx, hy0 - 4.5, h, 1.4, bright, lean=lean)
    # eyes: a cluster, blazing when she casts
    ey = int(hy0 - 1.5)
    if eyes == "open":
        c0, c1 = (EYE_RED[0], EYE_RED[1]) if glow < 0.5 else (hx("#9af4ff"), hx("#ffffff"))
        for (dx, dy, kk) in ((2, 0, 0), (4, 0, 1), (3, -1, 0), (5, 1, 0), (2, 2, 1), (4, 2, 0)):
            cv.px(int(hx0 + dx), ey + dy, c0 if kk == 0 else c1)
    else:
        cv.line(hx0 + 2, ey - 1, hx0 + 4, ey + 1, OUTLINE)
        cv.line(hx0 + 2, ey + 1, hx0 + 4, ey - 1, OUTLINE)
    # fangs
    fx = int(hx0 + 6.5)
    for kk in range(3 + int(bite * 2)):
        cv.px(fx + (kk if bite > 0.5 else 0), int(hy0 + 2 + kk * (0.5 if bite > 0.5 else 1)), D["tooth"][2 if kk < 2 else 1])
    return cv


def build_queen():
    A = {}
    A["idle"] = [queen(step=0), queen(step=1)]
    A["walk"] = [queen(step=s) for s in range(4)]
    A["windup"] = [queen(rear=0.5), queen(rear=0.9)]
    A["bite"] = [queen(bite=1.0), queen(bite=0.5)]
    A["cast"] = [queen(rear=1.0, glow=0.7), queen(rear=1.0, glow=1.0)]
    A["climb"] = [queen(climb=0.5), queen(climb=1.0)]
    A["air"] = [queen(climb=1.0, curl=0.3)]
    A["land"] = [queen(splay=1.0)]
    A["roar"] = [queen(rear=0.8, glow=0.6), queen(rear=1.0, glow=1.0)]
    A["hurt"] = [queen(eyes="x", rear=0.3)]
    A["dead"] = [queen(curl=0.6, eyes="x"), queen(curl=1.0, eyes="x")]
    return A


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_grid
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in (("beetle", build_beetle), ("crystalslime", build_crystal_slime), ("caveeye", build_cave_eye),
                     ("mimic", build_mimic), ("oldgolem", build_old_golem), ("queen", build_queen)):
        A = fn()
        frames = [f for key in A for f in A[key]]
        preview(pack_grid(frames, 8).to_image(), 4, os.path.join(out, f"deep_{name}.png"))
    print("ok")
