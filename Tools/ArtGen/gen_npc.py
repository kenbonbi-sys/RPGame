"""
NPCs: village chief (Trưởng Làng) and a villager girl. Same paper-doll approach
as the player. Frame 32x32, feet at y=29.
"""
from pixelkit import Canvas, P, OUTLINE, hx, from_ascii

FW, FH = 32, 32
FEET_Y = 29
CX = 16

C = {
    # chief: white hair & beard, green robe, wooden staff
    "1": P["white"][0], "2": P["white"][1], "3": P["white"][2], "4": P["white"][3],
    "s": P["skin"][1], "S": P["skin"][2], "L": P["skin"][3],
    "e": hx("#241826"), "b": hx("#e08a78"),
    "g": P["green"][0], "G": P["green"][1], "H": P["green"][2], "J": P["green"][3],
    "t": P["leather"][1], "T": P["gold"][3], "y": P["gold"][4],
    "w": P["wood"][2], "W": P["wood"][4], "x": P["wood"][1],
    "o": hx("#6ae0c0"), "O": hx("#c8fff0"),
    "n": P["leather"][0], "N": P["leather"][2],
    # villager girl
    "h": P["leather"][2], "i": P["leather"][3], "j": P["leather"][4],
    "c": P["cloth"][1], "C": P["cloth"][2], "D": P["cloth"][3],
    "r": P["blue"][1], "R": P["blue"][2], "q": P["blue"][3],
    "a": P["red"][2], "A": P["red"][3],
}

CHIEF_HEAD = [
    "...3444443..",
    "..344444443.",
    ".23SSSSSS32.",
    ".2SeSSSSeS2.",
    ".2SSSLSSSS2.",
    "234433334432",
    "234444444432",
    ".2344444432.",
    "..23444432..",
    "...234432...",
    "....2332....",
]
CHIEF_HEAD_TALK = CHIEF_HEAD[:5] + [
    "234433334432",
    "2344b33b4432",
    ".2344444432.",
    "..23444432..",
    "...234432...",
    "....2332....",
]
CHIEF_BODY = [
    "HJJJJJGH",
    "HJJTTJGG",
    "HJJJJJGG",
    "GHJJJGGg",
    "GHHJJGGg",
    "GHHHJGGg",
    "gGGGGGgg",
    "gGGGGGgg",
    ".gGGGgg.",
]
CHIEF_ARM = ["JH", "HG", "HG", "GG", "SS"]
FEET = ["nN", "nn"]


def staff(cv: Canvas, x, top, bottom, glow=0):
    for y in range(top, bottom + 1):
        cv.px(x, y, C["W"] if y % 3 else C["w"])
        cv.px(x + 1, y, C["x"])
    # crystal on top
    cv.px(x, top - 1, C["o"])
    cv.px(x + 1, top - 1, C["o"])
    cv.px(x, top - 2, C["O"] if glow else C["o"])
    cv.px(x + 1, top - 2, C["o"])
    cv.px(x, top - 3, C["O"])
    # curl
    cv.px(x - 1, top, C["W"])
    cv.px(x + 2, top, C["x"])


def chief(bob=0, talk=False, glow=0):
    cv = Canvas(FW, FH)
    top = FEET_Y - 21 + bob
    body_y = top + 10
    # feet
    cv.blit(from_ascii(FEET, C), CX - 3, FEET_Y - 1)
    cv.blit(from_ascii(FEET, C), CX + 1, FEET_Y - 1)
    # staff held on the right side
    staff(cv, CX + 7, top + 4, FEET_Y, glow)
    cv.blit(from_ascii(CHIEF_BODY, C), CX - 4, body_y)
    cv.blit(from_ascii(CHIEF_ARM, C), CX - 6, body_y + 1)
    cv.blit(from_ascii(CHIEF_ARM, C), CX + 4, body_y + 1)
    # hand on the staff
    cv.px(CX + 6, body_y + 5, C["S"])
    cv.px(CX + 7, body_y + 5, C["S"])
    cv.blit(from_ascii(CHIEF_HEAD_TALK if talk else CHIEF_HEAD, C), CX - 6, top)
    # bald head shine
    cv.px(CX - 1, top, C["L"])
    cv.outline(OUTLINE)
    return cv


GIRL_HEAD = [
    "...hiiiih...",
    ".hhijjiiihh.",
    "hhijjiiiihhh",
    "hiihhiiihhih",
    "hhSSiSSiSShh",
    "hhSeSSSSeShh",
    "hhSeSSSSeShh",
    "hhbSSssSSbhh",
    ".hhSSSSSShh.",
    ".hh.SSSS.hh.",
    ".h........h.",
]
GIRL_BODY = [
    "aAAAAAAa",
    "RqqCCqqR",
    "RqDDDDqR",
    "rRRRRRRr",
    "rRRqRRRr",
    "rRRRRRrr",
    ".rRRRRr.",
]
GIRL_ARM = ["CD", "Cc", "Cc", "SS"]


def girl(bob=0, blink=False):
    cv = Canvas(FW, FH)
    top = FEET_Y - 20 + bob
    body_y = top + 10
    cv.blit(from_ascii(["Sn", "nn"], C), CX - 3, FEET_Y - 1)
    cv.blit(from_ascii(["Sn", "nn"], C), CX + 1, FEET_Y - 1)
    cv.blit(from_ascii(GIRL_BODY, C), CX - 4, body_y)
    cv.blit(from_ascii(GIRL_ARM, C), CX - 6, body_y + 1)
    cv.blit(from_ascii(GIRL_ARM, C), CX + 4, body_y + 1)
    head = GIRL_HEAD
    if blink:
        head = GIRL_HEAD[:5] + ["hhSSSSSSSShh", "hhseSSSSeshh"] + GIRL_HEAD[7:]
    cv.blit(from_ascii(head, C), CX - 6, top)
    # flower pin
    cv.px(CX + 4, top + 2, C["A"])
    cv.px(CX + 5, top + 1, C["A"])
    cv.px(CX + 5, top + 2, hx("#ffe07a"))
    cv.outline(OUTLINE)
    return cv


def build():
    return {
        "chief_idle": [chief(0), chief(1), chief(0, glow=1), chief(1, glow=1)],
        "chief_talk": [chief(0, talk=True), chief(0), chief(1, talk=True), chief(1)],
        "girl_idle": [girl(0), girl(1), girl(0, blink=True), girl(1)],
    }


ORDER = ["chief_idle", "chief_talk", "girl_idle"]
