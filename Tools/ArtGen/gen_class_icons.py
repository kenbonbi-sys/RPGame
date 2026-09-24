"""
Skill icons of the twelve classes and their emblems (24x24), drawn from a set of glyphs
(an axe, a hammer, a note, a flame, an eye, a vortex...) on a coloured bevelled square,
in the style of gen_icons.py's skill icons.
"""
import math

from pixelkit import Canvas, P, hx, mix, shaded_ellipse, point_in_poly
from gen_icons import skill_bg, overlay

WHITE = (255, 255, 255, 255)

# every ramp padded to six shades, so any glyph takes any colour
P6 = {k: list(v) + [v[-1]] * max(0, 6 - len(v)) for k, v in P.items()}


def bg_of(base):
    c = hx(base)
    return skill_bg(mix(c, (0, 0, 0, 255), 0.72), mix(c, (0, 0, 0, 255), 0.4), mix(c, WHITE, 0.15))


def poly(cv, pts, col_left, col_right=None, split=12):
    for y in range(24):
        for x in range(24):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                cv.px(x, y, col_left if (col_right is None or x < split) else col_right)


def thick_line(cv, x0, y0, x1, y1, c, w=1):
    cv.line(x0, y0, x1, y1, c, w)


# ---------------------------------------------------------------- glyphs (24x24, transparent)
def g_axe(tint="metal"):
    fg = Canvas(24, 24)
    wood = P6["wood"]
    thick_line(fg, 5, 20, 17, 5, wood[3], 2)
    m = P6[tint]
    poly(fg, [(12, 3), (20, 2), (22, 9), (17, 12), (14, 8)], m[4], m[3], 17)
    fg.line(20, 3, 22, 9, m[5])
    return fg


def g_hammer(tint="metal"):
    fg = Canvas(24, 24)
    thick_line(fg, 5, 20, 14, 9, P6["wood"][3], 2)
    m = P6[tint]
    poly(fg, [(10, 5), (16, 2), (21, 8), (15, 12)], m[4], m[2], 15)
    fg.line(16, 2, 21, 8, m[5])
    return fg


def g_dagger(tint="metal"):
    fg = Canvas(24, 24)
    m = P6[tint]
    thick_line(fg, 4, 20, 7, 17, P6["leather"][3], 2)
    fg.line(5, 14, 10, 19, P6["gold"][4])
    poly(fg, [(8, 15), (19, 4), (21, 3), (20, 5), (9, 16)], m[4], m[3], 14)
    fg.line(9, 15, 20, 4, m[5])
    return fg


def g_spear(tint="metal"):
    fg = Canvas(24, 24)
    thick_line(fg, 3, 21, 16, 8, P6["wood"][3], 1)
    m = P6[tint]
    poly(fg, [(15, 7), (21, 2), (18, 10)], m[4], m[3], 18)
    fg.px(20, 3, m[5])
    return fg


def g_fist(tint="skin"):
    fg = Canvas(24, 24)
    s = P6[tint] if tint in P else P6["skin"]
    for i in range(4):
        shaded_ellipse(fg, 8 + i * 3, 9, 2, 3, s[1:], dither=0.2)
    poly(fg, [(5, 10), (19, 10), (18, 19), (7, 19)], s[2], s[1], 13)
    poly(fg, [(3, 12), (7, 11), (8, 16), (4, 16)], s[2])
    for x in (4, 20):
        fg.px(x, 6, P6["gold"][4])
    return fg


def g_arrow(tint="metal", n=1):
    fg = Canvas(24, 24)
    for k in range(n):
        ox = (k - (n - 1) / 2) * 5
        x0, y0, x1, y1 = 4 + ox, 20, 18 + ox, 6
        fg.line(x0, y0, x1, y1, P6["wood"][4])
        m = P6[tint]
        poly(fg, [(x1 - 1, y1 + 2), (x1 + 3, y1 - 3), (x1 + 2, y1 + 1)], m[4])
        fg.px(x1 + 2, y1 - 2, m[5])
        fg.line(x0 - 2, y0, x0, y0 - 2, P6["white"][2])
        fg.line(x0, y0 + 2, x0 + 2, y0, P6["red"][3])
    return fg


def g_bow():
    fg = Canvas(24, 24)
    w = P6["wood"]
    for k in range(-9, 10):
        x = 8 + (81 - k * k) / 81 * 6
        fg.px(round(x), 12 + k, w[3 if k < 0 else 2])
    fg.line(8, 3, 8, 21, P6["white"][2])
    fg.line(7, 12, 21, 12, P6["wood"][4])
    poly(fg, [(19, 10), (23, 12), (19, 14)], P6["metal"][4])
    return fg


def g_note(tint="white"):
    fg = Canvas(24, 24)
    c = P6[tint]
    fg.rect(13, 4, 2, 13, c[3])
    fg.rect(15, 4, 4, 2, c[3])
    fg.rect(17, 6, 2, 3, c[2])
    shaded_ellipse(fg, 10, 17, 4, 3, c[1:], dither=0.2)
    fg.rect(6, 7, 2, 9, c[2])
    shaded_ellipse(fg, 4, 16, 2, 2, c[1:], dither=0.2)
    fg.hline(6, 13, 7, c[3])
    return fg


def g_flame(tint="fire", small=False):
    fg = Canvas(24, 24)
    f = P6[tint]
    s = 0.7 if small else 1.0
    pts = [(12, 2 + (4 if small else 0)), (18, 11), (18, 16), (12, 21), (6, 16), (6, 11), (9, 8), (10, 12)]
    pts = [(12 + (x - 12) * s, 12 + (y - 12) * s + (3 if small else 0)) for x, y in pts]
    poly(fg, pts, f[3], f[2], 12)
    inner = [(12 + (x - 12) * 0.55, 15 + (y - 15) * 0.55) for x, y in pts]
    poly(fg, inner, f[5])
    return fg


def g_sun(tint="gold"):
    fg = Canvas(24, 24)
    g = P6[tint]
    for a in range(0, 360, 45):
        r = math.radians(a)
        fg.line(12 + math.cos(r) * 6, 12 + math.sin(r) * 6, 12 + math.cos(r) * 10, 12 + math.sin(r) * 10, g[4])
    shaded_ellipse(fg, 12, 12, 5, 5, g[2:], dither=0.2)
    fg.px(10, 10, WHITE)
    return fg


def g_cross(tint="gold"):
    fg = Canvas(24, 24)
    g = P6[tint]
    fg.rect(10, 3, 4, 18, g[4])
    fg.rect(5, 8, 14, 4, g[4])
    fg.rect(11, 4, 2, 16, g[5])
    fg.rect(6, 9, 12, 2, g[5])
    return fg


def g_leaf(tint="green"):
    fg = Canvas(24, 24)
    g = P6[tint]
    poly(fg, [(4, 20), (7, 9), (14, 4), (21, 3), (19, 11), (13, 17)], g[3], g[2], 12)
    fg.line(5, 19, 18, 6, g[5])
    for k in range(3):
        fg.line(9 + k * 3, 15 - k * 3, 9 + k * 3, 11 - k * 3, g[4])
    return fg


def g_thorns(tint="green"):
    fg = Canvas(24, 24)
    g = P6[tint]
    for i, (x, h) in enumerate([(5, 12), (10, 17), (15, 14), (19, 10)]):
        poly(fg, [(x - 2, 21), (x + 2, 21), (x, 21 - h)], g[3], g[2], x)
        fg.px(x - 1, 21 - h + 4, P6["bone"][3])
    fg.hline(2, 21, 21, P6["wood"][2])
    return fg


def g_wave(tint="blue"):
    fg = Canvas(24, 24)
    c = P6[tint]
    for i, r in enumerate((4, 8, 12)):
        for a in range(-55, 56, 4):
            rad = math.radians(a)
            fg.px(round(4 + math.cos(rad) * r), round(12 + math.sin(rad) * r), c[5 - i] if i < 3 else c[2])
    fg.ellipse(3, 12, 2, 2, c[4])
    return fg


def g_skull(tint="bone"):
    fg = Canvas(24, 24)
    b = P6[tint] if tint in P else P6["bone"]
    shaded_ellipse(fg, 12, 10, 7, 6, b[1:], dither=0.2)
    fg.rect(8, 14, 8, 5, b[2])
    fg.ellipse(9, 10, 2, 2, (30, 20, 30, 255))
    fg.ellipse(15, 10, 2, 2, (30, 20, 30, 255))
    for x in (9, 11, 13, 15):
        fg.px(x, 18, (30, 20, 30, 255))
    return fg


def g_eye(tint="purple"):
    fg = Canvas(24, 24)
    c = P6[tint]
    poly(fg, [(2, 12), (8, 6), (16, 6), (22, 12), (16, 18), (8, 18)], c[4], c[3], 12)
    shaded_ellipse(fg, 12, 12, 4, 4, P6["red"][1:], dither=0.2)
    fg.ellipse(12, 12, 1, 2, (20, 10, 20, 255))
    fg.px(11, 10, WHITE)
    return fg


def g_tentacle(tint="purple"):
    fg = Canvas(24, 24)
    c = P6[tint]
    for k, x0 in enumerate((5, 12, 19)):
        for y in range(4, 22):
            x = x0 + math.sin((y + k * 3) * 0.5) * 2.2
            w = 1 if y < 10 else 2
            for d in range(w):
                fg.px(round(x) + d, y, c[3] if d == 0 else c[2])
    return fg


def g_vortex(tint="purple"):
    fg = Canvas(24, 24)
    c = P6[tint]
    for arm in range(3):
        for t in range(0, 60):
            a = arm * 2.094 + t * 0.12
            r = 1 + t * 0.17
            fg.px(round(12 + math.cos(a) * r), round(12 + math.sin(a) * r), c[5 - min(4, t // 14)])
    fg.ellipse(12, 12, 2, 2, (20, 10, 30, 255))
    return fg


def g_shield(tint="gold", mark=None):
    fg = Canvas(24, 24)
    g = P6[tint]
    poly(fg, [(4, 4), (20, 4), (20, 12), (12, 21), (4, 12)], g[3], g[2])
    poly(fg, [(7, 6), (17, 6), (17, 12), (12, 18), (7, 12)], g[5], g[4])
    if mark is not None:
        fg.vline(12, 7, 16, mark)
        fg.hline(9, 15, 10, mark)
    return fg


def g_heart(tint="red"):
    fg = Canvas(24, 24)
    c = P6[tint]
    shaded_ellipse(fg, 8, 9, 5, 5, c[2:], dither=0.2)
    shaded_ellipse(fg, 16, 9, 5, 5, c[2:], dither=0.2)
    poly(fg, [(3, 10), (21, 10), (12, 21)], c[3], c[2])
    fg.px(7, 7, WHITE)
    return fg


def g_hand(tint="skin"):
    fg = Canvas(24, 24)
    s = P6["skin"] if tint == "skin" else P6[tint]
    for i, h in enumerate((7, 10, 11, 9)):
        fg.rect(7 + i * 3, 13 - h, 2, h, s[2])
        fg.px(7 + i * 3, 13 - h, s[3])
    poly(fg, [(6, 12), (19, 12), (18, 21), (8, 21)], s[2], s[1], 13)
    poly(fg, [(3, 14), (7, 12), (8, 16), (5, 18)], s[2])
    for (x, y) in [(4, 4), (20, 3), (21, 16)]:
        fg.px(x, y, P6["gold"][5])
    return fg


def g_boot(tint="leather"):
    fg = Canvas(24, 24)
    l = P6[tint]
    poly(fg, [(8, 4), (14, 4), (14, 14), (21, 17), (21, 21), (8, 21)], l[3], l[2], 14)
    fg.hline(8, 14, 6, l[4])
    for (y, x0, x1) in [(9, 1, 6), (14, 2, 6), (19, 0, 6)]:
        fg.hline(x0, x1, y, P6["white"][2])
    return fg


def g_cloud(tint="white"):
    fg = Canvas(24, 24)
    c = P6[tint]
    for (x, y, r) in [(8, 13, 5), (14, 10, 6), (17, 15, 4), (11, 16, 4)]:
        shaded_ellipse(fg, x, y, r, r - 1, c[0:], dither=0.3)
    return fg


def g_meteor():
    fg = Canvas(24, 24)
    f = P6["fire"]
    for k in range(9):
        fg.ellipse(4 + k * 1.1, 4 + k * 1.1, 1 + k * 0.25, 1 + k * 0.25, f[2 + min(3, k // 3)])
    shaded_ellipse(fg, 16, 16, 5, 5, P6["stone"][1:], dither=0.3)
    fg.px(14, 14, f[5])
    return fg


def g_star(tint="purple"):
    fg = Canvas(24, 24)
    c = P6[tint]
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = 10 if i % 2 == 0 else 4
        pts.append((12 + math.cos(a) * r, 12 + math.sin(a) * r))
    poly(fg, pts, c[4], c[3])
    fg.px(11, 9, WHITE)
    return fg


def g_book(tint="blue"):
    fg = Canvas(24, 24)
    c = P6[tint]
    poly(fg, [(4, 5), (19, 3), (20, 19), (5, 21)], c[3], c[2])
    fg.line(6, 6, 6, 20, P6["white"][3])
    fg.line(19, 4, 20, 18, P6["white"][3])
    g_star_on = g_star("gold")
    for y in range(24):
        for x in range(24):
            px = g_star_on.get(x, y)
            if px[3] and 7 <= x <= 18:
                fg.px(round(7 + (x - 7) * 0.55 + 3), round(y * 0.55 + 5), px)
    return fg


def g_orb(tint="purple"):
    fg = Canvas(24, 24)
    c = P6[tint]
    shaded_ellipse(fg, 12, 12, 8, 8, c[1:], dither=0.3)
    fg.ellipse(9, 9, 2, 2, c[5])
    fg.px(8, 8, WHITE)
    return fg


def g_swords(tint="metal"):
    fg = Canvas(24, 24)
    m = P6[tint]
    for flip in (False, True):
        x0, x1 = (4, 20) if not flip else (20, 4)
        fg.line(x0, 20, x1, 4, m[4], 1)
        fg.line(x0 + (1 if not flip else -1), 20, x1 + (1 if not flip else -1), 4, m[3], 1)
        gx = 7 if not flip else 17
        fg.line(gx - 2, 15, gx + 2, 19, P6["gold"][4])
    return fg


def g_lute():
    fg = Canvas(24, 24)
    w = P6["wood"]
    shaded_ellipse(fg, 9, 15, 6, 6, w[1:], dither=0.2)
    fg.ellipse(9, 15, 1, 1, w[0])
    fg.line(12, 12, 20, 4, w[2], 2)
    fg.rect(19, 2, 3, 3, w[1])
    fg.line(5, 17, 20, 4, P6["white"][2])
    return fg


# ---------------------------------------------------------------- icons
def icon(glyph, base):
    return overlay(bg_of(base), glyph)


def build():
    """(name, Canvas) of every class skill icon and every class emblem (cls_*)."""
    items = [
        # weapons' basic attacks and cantrips
        ("sk_cleave", icon(g_axe(), "#9a3a24")),
        ("sk_smash", icon(g_hammer(), "#6a6a7a")),
        ("sk_thrust", icon(g_spear(), "#2c5a8a")),
        ("sk_stab", icon(g_dagger(), "#1e6a6a")),
        ("sk_flurry", icon(g_fist(), "#c86a1e")),
        ("sk_arrow", icon(g_arrow(), "#3a6a2a")),
        ("sk_notes", icon(g_note(), "#b03a88")),
        ("sk_sacredflame", icon(g_flame("gold"), "#b8821e")),
        ("sk_thornwhip", icon(g_thorns(), "#2e6a2a")),
        ("sk_firebolt", icon(g_flame("fire", True), "#b83a1a")),
        ("sk_eldritch", icon(g_orb("purple"), "#5a1e6a")),
        ("sk_missile", icon(g_star("ice"), "#4a3aa8")),
        # Cuồng Chiến Binh
        ("sk_rage", icon(g_skull("red"), "#8a1a1a")),
        ("sk_axethrow", icon(g_axe(), "#b85a2a")),
        ("sk_endure", icon(g_shield("red"), "#6a1a1a")),
        # Thi Sĩ
        ("sk_soundwave", icon(g_wave("pink_ui"), "#9a2a78")),
        ("sk_mockery", icon(g_skull("purple"), "#a03a90")),
        ("sk_anthem", icon(g_note("gold"), "#a87a1e")),
        ("sk_song", icon(g_heart("red"), "#c04a8a")),
        ("sk_misty", icon(g_cloud("ice"), "#2a6a9a")),
        ("sk_hypnotic", icon(g_vortex("pink_ui"), "#8a1e6a")),
        # Tu Sĩ
        ("sk_guidingbolt", icon(g_star("gold"), "#a8821e")),
        ("sk_spiritguard", icon(g_sun(), "#8a6a14")),
        ("sk_lightpillar", icon(g_cross(), "#c8a02a")),
        ("sk_radiance", icon(g_sun("white"), "#d8b040")),
        # Tế Sư
        ("sk_entangle", icon(g_thorns("leather"), "#5a3a1e")),
        ("sk_thunderwave", icon(g_wave("blue"), "#1e4a8a")),
        ("sk_barkskin", icon(g_leaf("leather"), "#4a3a1e")),
        ("sk_thornstorm", icon(g_thorns("green"), "#1e5a2a")),
        # Chiến Binh
        ("sk_charge", icon(g_boot("red"), "#8a2a1e")),
        ("sk_whirl", icon(g_swords(), "#b85a1e")),
        ("sk_secondwind", icon(g_heart("red"), "#8a2a2a")),
        ("sk_parry", icon(g_shield("metal", P6["red"][3]), "#4a4a5a")),
        ("sk_surge", icon(g_fist("gold"), "#b88a1e")),
        # Võ Tăng
        ("sk_flyingkick", icon(g_boot("leather"), "#1e8a8a")),
        ("sk_stunstrike", icon(g_hand(), "#1e6a7a")),
        ("sk_flurryblows", icon(g_fist(), "#1e7a6a")),
        ("sk_patience", icon(g_heart("green"), "#1e6a5a")),
        ("sk_windstep", icon(g_boot("ice"), "#2a8aa8")),
        ("sk_palm", icon(g_hand("gold"), "#1e5a6a")),
        # Hiệp Sĩ Thánh
        ("sk_smite", icon(g_hammer("gold"), "#c8a02a")),
        ("sk_layonhands", icon(g_hand("gold"), "#a8821e")),
        ("sk_auraprotect", icon(g_shield("gold", P6["white"][3]), "#8a6a1e")),
        ("sk_holycharge", icon(g_boot("gold"), "#b8901e")),
        ("sk_heavenfall", icon(g_cross("white"), "#5a7ac8")),
        # Du Hiệp
        ("sk_volley", icon(g_arrow("metal", 3), "#3a6a2a")),
        ("sk_piercing", icon(g_arrow("ice"), "#2a6a6a")),
        ("sk_trap", icon(g_thorns("stone"), "#5a5a3a")),
        ("sk_huntermark", icon(g_eye("green"), "#3a5a1e")),
        ("sk_disengage", icon(g_boot("green"), "#2a5a2a")),
        ("sk_multishot", icon(g_bow(), "#4a7a2a")),
        # Đạo Tặc
        ("sk_throwknife", icon(g_dagger(), "#2a2a4a")),
        ("sk_deathstrike", icon(g_skull("red"), "#4a1a2a")),
        ("sk_smokebomb", icon(g_cloud("white"), "#3a3a4a")),
        ("sk_evasion", icon(g_boot("fur"), "#2a2a3a")),
        ("sk_shadowstep", icon(g_vortex("blue"), "#1a1a3a")),
        ("sk_bladefan", icon(g_swords("purple"), "#3a1a4a")),
        # Thuật Sĩ
        ("sk_chaosbolt", icon(g_orb("pink_ui"), "#8a2a6a")),
        ("sk_arcaneshield", icon(g_shield("purple"), "#4a2a8a")),
        ("sk_meteor", icon(g_meteor(), "#8a3a1a")),
        # Khế Ước Sư
        ("sk_hex", icon(g_eye("purple"), "#4a1a5a")),
        ("sk_tentacles", icon(g_tentacle(), "#3a1a4a")),
        ("sk_drain", icon(g_heart("purple"), "#3a1a3a")),
        ("sk_darkarmor", icon(g_shield("fur"), "#2a1a3a")),
        ("sk_voidstorm", icon(g_vortex("purple"), "#2a0a3a")),
        # Pháp Sư
        ("sk_blackhole", icon(g_vortex("fur"), "#1a0a2a")),
        # emblems of the classes (the creator's cards)
        ("cls_barbarian", icon(g_axe(), "#e07a2a")),
        ("cls_bard", icon(g_lute(), "#d0409a")),
        ("cls_cleric", icon(g_sun(), "#d0a030")),
        ("cls_druid", icon(g_leaf(), "#50a040")),
        ("cls_fighter", icon(g_swords(), "#b04a30")),
        ("cls_monk", icon(g_fist(), "#20a0a0")),
        ("cls_paladin", icon(g_shield("metal", P6["gold"][4]), "#8a9ab8")),
        ("cls_ranger", icon(g_bow(), "#6a8a3a")),
        ("cls_rogue", icon(g_dagger(), "#3a4a8a")),
        ("cls_sorcerer", icon(g_flame(), "#e05030")),
        ("cls_warlock", icon(g_eye("purple"), "#9a2a4a")),
        ("cls_wizard", icon(g_book(), "#7a4ad0")),
    ]
    return items
