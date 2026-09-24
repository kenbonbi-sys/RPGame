"""
Player character sprite sheet (paper-doll assembled from ASCII parts).

Frame size 32x32, feet at y=29. Directions: down, up, side(right; left = flipX).
Animations:
  idle(2) walk(4) attack(3) cast(2) dash(1) hurt(1) per direction + dead(1)
"""
import math

from pixelkit import Canvas, P, OUTLINE, hx, from_ascii

FW, FH = 32, 32
FEET_Y = 29
CX = 16

C = {
    "1": P["hair_red"][0], "2": P["hair_red"][1], "3": P["hair_red"][2], "4": P["hair_red"][3], "5": P["hair_red"][4],
    "s": P["skin"][1], "S": P["skin"][2], "L": P["skin"][3],
    "e": hx("#241826"), "w": hx("#ffffff"), "b": hx("#f08a7a"),
    "c": P["cloth"][1], "C": P["cloth"][2], "W": P["cloth"][3],
    "r": P["red"][1], "R": P["red"][2], "q": P["red"][3],
    "t": P["leather"][1], "T": P["gold"][3],
    "p": P["pants"][1], "P": P["pants"][2],
    "n": P["leather"][0], "N": P["leather"][2],
    "m": P["metal"][1], "M": P["metal"][3], "X": P["metal"][5],
    "g": P["gold"][2], "G": P["gold"][4],
    "k": P["leather"][2],
}

# ---------------------------------------------------------------- heads (12x11)
HEAD_DOWN = [
    "...344443...",
    ".3345544333.",
    "334455433332",
    "233444333322",
    "233233332332",
    "23SS3SS3SS32",
    "23SeSSSSeS32",
    "21SeSSSSeS12",
    "21bSSssSSb12",
    "11sSSSSSSs11",
    "..1sSSSSs1..",
]
HEAD_DOWN_BLINK = HEAD_DOWN[:6] + ["23SSSSSSSS32", "21seSSSSes12"] + HEAD_DOWN[8:]
HEAD_DOWN_HURT = HEAD_DOWN[:6] + ["23SeSSSSeS32", "21eSSSSSSe12", "21bSSeeSSb12"] + HEAD_DOWN[9:]

HEAD_UP = [
    "...344443...",
    ".3345544333.",
    "334455433332",
    "233444333322",
    "233333333322",
    "223333333322",
    "222333333222",
    "212233332212",
    "211222222112",
    "11122qR22111",
    "..11222211..",
]

HEAD_SIDE = [
    "...344443...",
    ".3345544333.",
    "334455433333",
    "233444333333",
    "233333332333",
    "2333332S3SS3",
    "233332SSSSeS",
    "22332sLSSSeS",
    "21221sSSSbSS",
    "11211sSSSSs.",
    ".111..ssss..",
]
HEAD_SIDE_HURT = HEAD_SIDE[:6] + ["233332SSSSSS", "22332sLSSeeS"] + HEAD_SIDE[8:]

# ---------------------------------------------------------------- torsos
TORSO_DOWN = [
    "rRqRRqRr",
    "CrRRRRrc",
    "CWWrRCCc",
    "CWWWCCcc",
    "tttTTttt",
    "CCCCCccc",
    ".cCCCcc.",
]
TORSO_UP = [
    "rRRRRRRr",
    "CrRRRRrc",
    "CWWWCCCc",
    "CWWCCCcc",
    "tttttttt",
    "CCCCCccc",
    ".cCCCcc.",
]
TORSO_SIDE = [
    ".rRqRr",
    "rRRRCc",
    "WWCCCc",
    "WWCCcc",
    "ttTttt",
    "CCCCcc",
    ".CCcc.",
]

ARM_L = ["WC", "WC", "Cc", "Cc", "SS", "ss"]   # viewer's left arm (lit)
ARM_R = ["Cc", "Cc", "cc", "cc", "SS", "ss"]   # viewer's right arm (shadow side)
ARM_SIDE = ["WC", "WC", "CC", "Cc", "SS", "sS"]
HAND = ["SS", "ss"]

LEG = ["PP", "Pp", "NN", "nn"]
LEG_BACK = ["pp", "pp", "nn", "nn"]


def part(rows):
    return from_ascii(rows, C)


def draw_sword(cv: Canvas, hx_, hy_, angle_deg, length=9):
    """Draws a sword whose grip starts at (hx_, hy_) pointing along angle (0 = right, 90 = down)."""
    a = math.radians(angle_deg)
    dx, dy = math.cos(a), math.sin(a)
    # grip (2px) behind the hand
    for k in range(-2, 0):
        cv.px(round(hx_ + dx * k), round(hy_ + dy * k), C["k"])
    # guard: perpendicular 3px
    px_, py_ = -dy, dx
    gx, gy = hx_ + dx * 1, hy_ + dy * 1
    for k in (-1, 0, 1):
        cv.px(round(gx + px_ * k * 1.2), round(gy + py_ * k * 1.2), C["G"] if k != 1 else C["g"])
    # blade
    for k in range(2, length + 2):
        x, y = hx_ + dx * k, hy_ + dy * k
        cv.px(round(x), round(y), C["M"])
        # edge highlight on one side
        cv.px(round(x + px_ * 0.8), round(y + py_ * 0.8), C["X"] if k < length else C["M"])
    cv.px(round(hx_ + dx * (length + 2)), round(hy_ + dy * (length + 2)), C["X"])


def compose(direction, legs=(0, 0), bob=0, arms=((0, 0), (0, 0)), head="normal", lean=0,
            sword=None, arm_pose=None, blink=False):
    """legs: vertical lift for (left, right) legs, or forward offsets for side view.
    arms: (dx, dy) offsets for left/right arm. sword: (hand_index, angle) or None."""
    cv = Canvas(FW, FH)
    top_y = FEET_Y - 21 + bob  # top of head
    body_y = top_y + 11
    leg_y = FEET_Y - 3

    if direction == "down":
        # legs
        l = part(LEG)
        r = part(LEG)
        cv.blit(l, CX - 3 + lean, leg_y - legs[0])
        cv.blit(r, CX + 1 + lean, leg_y - legs[1])
        # arms (behind torso edges)
        al = part(ARM_L)
        ar = part(ARM_R)
        if arm_pose != "cast":
            cv.blit(al, CX - 6 + arms[0][0] + lean, body_y + 1 + arms[0][1])
            cv.blit(ar, CX + 4 + arms[1][0] + lean, body_y + 1 + arms[1][1])
        cv.blit(part(TORSO_DOWN), CX - 4 + lean, body_y)
        hd = HEAD_DOWN_HURT if head == "hurt" else (HEAD_DOWN_BLINK if blink else HEAD_DOWN)
        cv.blit(part(hd), CX - 6 + lean, top_y)
        if arm_pose == "cast":
            # both hands raised forward, in front of the torso
            cv.blit(part(["WC", "Cc", "SS"]), CX - 5, body_y + 1)
            cv.blit(part(["Cc", "cc", "SS"]), CX + 3, body_y + 1)
        hand_pos = [(CX - 5 + arms[0][0] + lean, body_y + 6 + arms[0][1]),
                    (CX + 5 + arms[1][0] + lean, body_y + 6 + arms[1][1])]
    elif direction == "up":
        cv.blit(part(LEG_BACK), CX - 3 + lean, leg_y - legs[0])
        cv.blit(part(LEG_BACK), CX + 1 + lean, leg_y - legs[1])
        hand_pos = [(CX - 5 + arms[0][0], body_y + 6 + arms[0][1]),
                    (CX + 5 + arms[1][0], body_y + 6 + arms[1][1])]
        # sword behind the body when facing up
        if sword is not None:
            draw_sword(cv, *hand_pos[sword[0]], sword[1])
            sword = None
        cv.blit(part(ARM_R), CX - 6 + arms[0][0], body_y + 1 + arms[0][1])
        cv.blit(part(ARM_L), CX + 4 + arms[1][0], body_y + 1 + arms[1][1])
        cv.blit(part(TORSO_UP), CX - 4, body_y)
        cv.blit(part(HEAD_UP), CX - 6, top_y)
        if arm_pose == "cast":
            cv.blit(part(["SS"]), CX - 5, body_y)
            cv.blit(part(["SS"]), CX + 3, body_y)
    else:  # side (facing right)
        # back leg, back arm, torso, front leg, head, front arm
        back = part(LEG_BACK)
        front = part(LEG)
        cv.blit(back, CX - 2 - legs[1] + lean, leg_y - (1 if legs[1] else 0))
        cv.blit(part(["cc", "cc", "cc", "cc", "ss", "ss"]), CX - 3 - arms[1][0] + lean, body_y + 1 + arms[1][1])
        cv.blit(part(TORSO_SIDE), CX - 3 + lean, body_y)
        cv.blit(front, CX - 1 + legs[0] + lean, leg_y - (1 if legs[0] < 0 else 0))
        hd = HEAD_SIDE_HURT if head == "hurt" else HEAD_SIDE
        cv.blit(part(hd), CX - 6 + lean, top_y)
        if arm_pose == "cast":
            cv.blit(part(["WCCSS", "CccSs"]), CX, body_y + 2)
            hand_pos = [(CX + 4, body_y + 2), (CX + 4, body_y + 2)]
        else:
            cv.blit(part(ARM_SIDE), CX - 1 + arms[0][0] + lean, body_y + 1 + arms[0][1])
            hand_pos = [(CX + arms[0][0] + lean, body_y + 6 + arms[0][1])] * 2
    if sword is not None:
        draw_sword(cv, *hand_pos[sword[0]], sword[1])
    cv.outline(OUTLINE)
    return cv


def build():
    """Returns {anim_name: [Canvas...]} ; names like idle_down, walk_side ..."""
    A = {}
    for d in ("down", "up", "side"):
        A[f"idle_{d}"] = [compose(d), compose(d, bob=1, blink=(d == "down"))]
        if d == "side":
            A[f"walk_{d}"] = [
                compose(d, legs=(2, 2), arms=((1, 0), (1, 0))),
                compose(d, legs=(0, 0), bob=-1),
                compose(d, legs=(-2, -2), arms=((-1, 0), (-1, 0))),
                compose(d, legs=(0, 0), bob=-1),
            ]
        else:
            A[f"walk_{d}"] = [
                compose(d, legs=(1, 0), arms=((0, 1), (0, -1))),
                compose(d, legs=(0, 0), bob=-1),
                compose(d, legs=(0, 1), arms=((0, -1), (0, 1))),
                compose(d, legs=(0, 0), bob=-1),
            ]
    # attacks: windup, strike, follow-through
    A["attack_down"] = [
        compose("down", arms=((0, 0), (1, -3)), sword=(1, -60), lean=0),
        compose("down", arms=((0, 0), (-1, 0)), sword=(1, 110), lean=0, bob=1),
        compose("down", arms=((0, 0), (-3, 1)), sword=(1, 160), lean=0, bob=1),
    ]
    A["attack_up"] = [
        compose("up", arms=((0, 0), (0, -2)), sword=(1, -30)),
        compose("up", arms=((0, 0), (-2, -3)), sword=(1, -100)),
        compose("up", arms=((0, 0), (-4, -2)), sword=(1, -150)),
    ]
    A["attack_side"] = [
        compose("side", arms=((-2, -3), (0, 0)), sword=(0, -120)),
        compose("side", arms=((2, -1), (0, 0)), sword=(0, -10), lean=1),
        compose("side", arms=((2, 1), (0, 0)), sword=(0, 50), lean=1),
    ]
    for d in ("down", "up", "side"):
        A[f"cast_{d}"] = [compose(d, arm_pose="cast"), compose(d, arm_pose="cast", bob=1)]
        A[f"hurt_{d}"] = [compose(d, head="hurt", bob=1)]
    A["dash_down"] = [compose("down", legs=(2, 0), bob=-1)]
    A["dash_up"] = [compose("up", legs=(0, 2), bob=-1)]
    A["dash_side"] = [compose("side", legs=(3, 3), lean=2, bob=1, arms=((-2, 0), (2, 0)))]
    # dead: lying down (rotate idle)
    import numpy as np
    base = compose("down", head="hurt")
    rot = Canvas(FW, FH)
    rot.a = np.rot90(base.a, k=1).copy()
    A["dead"] = [rot.shifted(0, 6)]
    return A


ORDER = ["idle_down", "idle_up", "idle_side", "walk_down", "walk_up", "walk_side",
         "attack_down", "attack_up", "attack_side", "cast_down", "cast_up", "cast_side",
         "hurt_down", "hurt_up", "hurt_side", "dash_down", "dash_up", "dash_side", "dead"]
