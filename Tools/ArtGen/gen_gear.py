"""
Gear icons (16x16) for the slots around the hero and the forge's Chế Tạo tab:
caps and helms, tunics and mail, boots, a shield, a crown and rings, each in the
materials of the region whose monsters they are made from.

Shapes are drawn as ASCII (a half is mirrored for the symmetric ones), shades
1 (dark) .. 5 (light) come from the piece's main ramp, a / b from its accent.
"""
from pixelkit import P, hx, mix, shaded_ellipse, shaded_poly, point_in_poly, from_ascii, Canvas
from gen_icons import done
import math


def mirror(half_rows):
    """16-wide rows from their left 8 characters."""
    rows = []
    for r in half_rows:
        assert len(r) == 8, r
        rows.append(r + r[::-1])
    return rows


def paint(rows, main, accent=None, extra=None):
    """ASCII rows to a canvas: 1..5 the main ramp (dark to light), a/b/c the accent's, w white."""
    for r in rows:
        assert len(r) == 16, (len(r), r)
    cmap = {}
    n = len(main)
    for i in range(5):
        cmap[str(i + 1)] = main[min(n - 1, round(i * (n - 1) / 4))]
    if accent:
        m = len(accent)
        cmap["a"] = accent[max(0, m // 2 - 1)]
        cmap["b"] = accent[min(m - 1, m // 2 + 1)]
        cmap["c"] = accent[m - 1]
    cmap["w"] = (255, 255, 255, 255)
    if extra:
        cmap.update(extra)
    return from_ascii(rows, cmap, 16)


# ---------------------------------------------------------------- shapes
CAP = [
    "................",
    "................",
    ".....333333.....",
    "...3344444433...",
    "..334455554433..",
    "..344555555443..",
    ".33445555554433.",
    ".34444444444443.",
    ".aaaaaaaaaaaaaa.",
    ".abbabbabbabbab.",
    ".33322....22333.",
    ".3332......2333.",
    "..332......233..",
    "..22........22..",
    "................",
    "................",
]

HELM = mirror([
    "........",
    "......cc",
    ".....cbb",
    "....2abb",
    "...23345",
    "..233455",
    "..234555",
    ".2234455",
    ".2344444",
    ".2aaaaaa",
    ".2342..2",
    ".2342..2",
    ".2342...",
    "..222...",
    "........",
    "........",
])

TUNIC = mirror([
    "........",
    "....22..",
    "..22332.",
    ".2334433",
    "23444544",
    "23445554",
    "23445554",
    ".2344554",
    ".2344454",
    ".2344444",
    ".2aaaaaa",
    ".23aabbc",
    ".2344444",
    "..233333",
    "...22222",
    "........",
])

MAIL = mirror([
    "........",
    "....22..",
    "..22332.",
    ".2343434",
    "23434343",
    "24343434",
    "23434343",
    ".2434343",
    ".2343434",
    ".2434343",
    ".2aaaaaa",
    ".2abbbcb",
    ".2343434",
    "..243434",
    "...22222",
    "........",
])

BOOT = [
    "................",
    "...22222222.....",
    "...2aaaaaa2.....",
    "...23444432.....",
    "...23445432.....",
    "...23445432.....",
    "...23444432.....",
    "...234444432....",
    "...2344444432...",
    "...23444444432..",
    "...234445544432.",
    "...233333333332.",
    "...bbbbbbbbbbbb.",
    "................",
    "................",
    "................",
]

CROWN = mirror([
    "........",
    "........",
    "........",
    "...c...c",
    "..cbc.cb",
    "..4b4.44",
    ".245445c",
    ".2445445",
    ".2444444",
    ".2aa4aa4",
    ".2abaab4",
    ".2444444",
    "..222222",
    "........",
    "........",
    "........",
])


# ---------------------------------------------------------------- pieces
def helm_leather():
    return done(paint(CAP, P["leather"], P["leather"][:2] + [P["cream"][3], P["cream"][4]]))


def boots_leather():
    return done(paint(BOOT, P["leather"], [P["leather"][0], P["leather"][1], P["cream"][3], P["leather"][0]]))


def armor_bear():
    cv = paint(TUNIC, P["fur"], P["leather"])
    # a ruff of the bear's fur over the shoulders
    for x in list(range(2, 6)) + list(range(10, 14)):
        cv.px(x, 3, P["fur"][4] if x % 2 else P["fur"][3])
    return done(cv)


def boots_toad():
    toad = [hx("#2a3014"), hx("#4a5a1c"), hx("#6e7e28"), hx("#94a23a"), hx("#bcc85a")]
    cv = paint(BOOT, toad, [hx("#3a1c40"), hx("#6a2e70"), hx("#9a4aa0"), hx("#c07ac4")])
    # warts
    for (x, y) in [(5, 4), (7, 7), (9, 9), (12, 10), (6, 10)]:
        cv.px(x, y, toad[4])
    return done(cv)


def armor_scale():
    scale = [hx("#10301e"), hx("#1c4a2c"), hx("#2c6a3a"), hx("#4a8e50"), hx("#7ab86a")]
    return done(paint(MAIL, scale, P["gold"]))


def crown_toad():
    cv = paint(CROWN, P["gold"], P["purple"])
    cv.px(7, 7, P["purple"][4]); cv.px(8, 7, P["purple"][3])
    cv.px(7, 8, P["purple"][3]); cv.px(8, 8, P["purple"][2])
    return done(cv)


def helm_crystal():
    return done(paint(HELM, P["metal"], P["ice"]))


def shield_beetle():
    cv = Canvas(16, 16)
    shell = [hx("#23222e"), hx("#383848"), hx("#50526a"), hx("#6c7088"), hx("#8e94ab")]
    pts = [(2, 2), (14, 2), (14, 8), (8, 15), (2, 8)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                inten = -(x - 8) / 7 * 0.6 - (y - 8) / 8 * 0.4
                i = max(0, min(4, int((inten + 1) * 2.5)))
                cv.px(x, y, shell[i])
    # the ridge down the middle and crystal specks
    for y in range(3, 14):
        cv.px(8, y, shell[4] if y < 8 else shell[3])
        cv.px(7, y, shell[1])
    for (x, y) in [(4, 4), (11, 5), (5, 8), (10, 9), (12, 3)]:
        cv.px(x, y, P["ice"][4])
    cv.px(4, 3, P["ice"][5])
    return done(cv)


def armor_silk():
    silk = [hx("#4a5a78"), hx("#7a8cb0"), hx("#aabcdc"), hx("#d4e2f6"), hx("#f4f8ff")]
    cv = paint(TUNIC, silk, P["ice"])
    # the crystal dust glints
    for (x, y) in [(4, 5), (11, 6), (6, 9), (9, 12), (12, 11)]:
        cv.px(x, y, P["ice"][5])
    return done(cv)


def ring_with(band, gem, tooth=False):
    cv = Canvas(16, 16)
    for a in range(0, 360, 6):
        r = math.radians(a)
        x, y = 8 + math.cos(r) * 4.5, 10 + math.sin(r) * 3.5
        cv.px(round(x), round(y), band[4] if math.sin(r) < 0 else band[2])
    if tooth:
        # a curved fang set on the band
        bone = P["bone"]
        for (x, y, c) in [(6, 1, 3), (7, 1, 3), (8, 1, 2), (9, 1, 1),
                          (6, 2, 3), (7, 2, 3), (8, 2, 2), (9, 2, 1),
                          (7, 3, 3), (8, 3, 2), (9, 3, 1),
                          (7, 4, 2), (8, 4, 1),
                          (8, 5, 1)]:
            cv.px(x, y, bone[c])
        for x in range(5, 11):
            cv.px(x, 6, band[3] if x % 2 else band[2])
    else:
        shaded_ellipse(cv, 8, 5, 2.8, 2.6, gem, dither=0.2)
        cv.px(8, 5, hx("#101018"))
        cv.px(7, 4, (255, 255, 255, 255))
    return done(cv)


def ring_eye():
    return ring_with(P["metal"], [hx("#10304e"), hx("#1e5a8a"), hx("#3a8ec4"), hx("#6ec4ec"), hx("#b0ecff")])


def ring_mimic():
    return ring_with(P["gold"], None, tooth=True)


def helm_eagle():
    cv = paint(HELM, P["metal"], [hx("#3c424c"), hx("#6c7482"), hx("#c4bcae"), hx("#f0ece4")])
    # two eagle feathers standing up from the crest
    for (x, lean) in ((6, -1), (9, 1)):
        for k in range(5):
            cv.px(x + (lean if k > 2 else 0), 5 - k, hx("#e6e0d4") if k < 3 else hx("#525a66"))
    return done(cv)


def armor_bison():
    hide = [hx("#1e120a"), hx("#342214"), hx("#4a3220"), hx("#62442c"), hx("#7c5a3a")]
    cv = paint(TUNIC, hide, [hx("#5a5244"), hx("#8e8470"), hx("#c2b89e"), hx("#e6dec8")])
    # horns on the shoulders
    for (x, d) in ((2, -1), (13, 1)):
        cv.px(x, 3, hx("#c2b89e"))
        cv.px(x + d, 2, hx("#e6dec8"))
    # the shaggy collar
    for x in range(4, 12):
        cv.px(x, 3, hide[0] if x % 2 else hide[1])
    return done(cv)


def boots_wind():
    sky = [hx("#1e3a4c"), hx("#2e5a70"), hx("#4a86a0"), hx("#7ab8cc"), hx("#b8e4ee")]
    cv = paint(BOOT, sky, [hx("#e6f4f8"), hx("#c8e0e8"), hx("#f0fbff"), hx("#9ab8c4")])
    # a swirl of wind on the shaft
    for (x, y) in ((5, 5), (6, 4), (7, 5), (6, 6), (8, 7)):
        cv.px(x, y, hx("#f0fbff"))
    return done(cv)


def shield_iron():
    """Khiên Sừng Sắt: a round iron shield with the iron bison's horn across it."""
    cv = Canvas(16, 16)
    iron = [hx("#2e3240"), hx("#4a5064"), hx("#6e7690"), hx("#9aa2ba"), hx("#c8cedc")]
    shaded_ellipse(cv, 8, 8, 6.5, 6.5, iron, dither=0.15, bias=0.1)
    for (x, y) in ((8, 2), (2, 8), (14, 8), (8, 14), (4, 4), (12, 4), (4, 12), (12, 12)):
        if cv.opaque(x, y):
            cv.px(x, y, iron[4])
    horn = [hx("#5a5244"), hx("#8e8470"), hx("#c2b89e"), hx("#e6dec8")]
    for i in range(9):
        t = i / 8
        cv.px(int(4 + t * 8), int(10 - math.sin(t * math.pi) * 4), horn[1 + i % 3])
    return done(cv)


def boots_howl():
    """Ủng Gió Hú: black leather boots with grey fur cuffs, wind-lines streaming off the heel."""
    dark = [hx("#0e0e14"), hx("#1c1c26"), hx("#2c2c3a"), hx("#40404e"), hx("#5a5a6a")]
    cv = paint(BOOT, dark, [hx("#8a8a96"), hx("#b4b4c0"), hx("#dcdce6"), hx("#f4f4fa")])
    for (x, y) in ((1, 8), (2, 8), (0, 10), (1, 10), (2, 11)):
        cv.px(x, y, hx("#c8e0e8"))
    return done(cv)


def scarf_blackwind():
    """Khăn Hắc Phong: the chief's black hood and red scarf."""
    hood = [hx("#0e0e14"), hx("#1c1c26"), hx("#2c2c3a"), hx("#40404e"), hx("#56566a")]
    cv = paint(CAP, hood, [hx("#4a0e1a"), hx("#7c1a26"), hx("#b52a34"), hx("#e04a44")])
    # the scarf's tails
    for (x, y) in ((12, 11), (13, 12), (13, 13), (12, 13)):
        cv.px(x, y, hx("#b52a34"))
    return done(cv)


def icons():
    return [
        ("helm_leather", helm_leather()),
        ("boots_leather", boots_leather()),
        ("armor_bear", armor_bear()),
        ("boots_toad", boots_toad()),
        ("armor_scale", armor_scale()),
        ("crown_toad", crown_toad()),
        ("helm_crystal", helm_crystal()),
        ("shield_beetle", shield_beetle()),
        ("armor_silk", armor_silk()),
        ("ring_eye", ring_eye()),
        ("ring_mimic", ring_mimic()),
        # Thảo Nguyên Gió
        ("helm_eagle", helm_eagle()),
        ("armor_bison", armor_bison()),
        ("boots_wind", boots_wind()),
        # Hắc Phong
        ("shield_iron", shield_iron()),
        ("boots_howl", boots_howl()),
        ("scarf_blackwind", scarf_blackwind()),
    ]


if __name__ == "__main__":
    import os
    from pixelkit import pack_row, preview
    out = os.path.join(os.path.dirname(__file__), "..", "..", "..", "gear_preview.png")
    img = pack_row([c for _, c in icons()]).to_image()
    preview(img, 8, os.environ.get("GEAR_PREVIEW", out))
