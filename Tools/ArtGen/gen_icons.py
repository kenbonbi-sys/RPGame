"""
Item icons (16x16), skill icons (24x24) and small status icons (10x10).
Style: chunky dark outline, warm palette (inspired by classic RPG icon sets).
"""
import math
import random

from pixelkit import (Canvas, P, OUTLINE, hx, mix, shaded_ellipse, shade_index, from_ascii,
                      point_in_poly, LIGHT, bayer)

ICON_OUT = hx("#1e1216")


def done(cv, out=ICON_OUT):
    cv.outline(out)
    return cv


# ============================================================================
# Items 16x16
# ============================================================================

def potion(liquid):
    cv = Canvas(16, 16)
    gl = P["white"]
    # cork
    cv.rect(6, 1, 4, 2, P["wood"][4])
    cv.hline(6, 9, 2, P["wood"][2])
    # neck
    cv.rect(6, 3, 4, 3, hx("#c8d8e8", 255))
    cv.px(6, 3, gl[3]); cv.px(6, 4, gl[3])
    cv.px(9, 3, gl[0]); cv.px(9, 4, gl[0])
    # bulb glass
    shaded_ellipse(cv, 8, 10.5, 6, 5, [hx("#8898b0"), hx("#b0c0d4"), hx("#d8e4f0")], dither=0.3)
    # liquid (lower 70%)
    for y in range(8, 16):
        for x in range(2, 14):
            dx = (x + 0.5 - 8) / 5.2
            dy = (y + 0.5 - 10.5) / 4.3
            if dx * dx + dy * dy <= 1 and y >= 8:
                inten = -dx * 0.6 - dy * 0.5 + 0.15
                cv.px(x, y, liquid[shade_index(inten, len(liquid), x, y, 0.3)])
    # surface line
    cv.hline(4, 11, 8, liquid[-1])
    # glass highlight
    cv.px(4, 9, (255, 255, 255, 255))
    cv.px(4, 10, (255, 255, 255, 255))
    cv.px(5, 8, (255, 255, 255, 220))
    return done(cv)


def coin_stack():
    cv = Canvas(16, 16)
    g = P["gold"]
    for i, (cx, cy) in enumerate([(6, 12), (10, 12), (8, 9), (7, 6)]):
        for y in range(cy - 2, cy + 2):
            for x in range(cx - 3, cx + 4):
                dx = (x + 0.5 - cx - 0.5) / 3.5
                dy = (y + 0.5 - cy) / 1.8
                if dx * dx + dy * dy <= 1:
                    cv.px(x, y, g[4] if y < cy else g[2])
        cv.px(cx - 1, cy - 1, g[5])
        cv.hline(cx - 2, cx + 3, cy + 1, g[1])
    return done(cv)


def coin():
    cv = Canvas(16, 16)
    g = P["gold"]
    shaded_ellipse(cv, 8, 8, 6, 6, g[1:], dither=0.3, bias=0.05)
    cv.ellipse(8, 8, 3.6, 3.6, g[2])
    for y in range(5, 11):
        cv.px(8, y, g[4])
    cv.px(7, 6, g[5]); cv.px(9, 10, g[1])
    cv.px(5, 5, g[5])
    return done(cv)


def gel():
    cv = Canvas(16, 16)
    s = P["slime"]
    for y in range(4, 15):
        for x in range(2, 14):
            dx = (x + 0.5 - 8) / 6
            dy = (y + 0.5 - 10) / 5
            if dy > 0:
                dy *= 0.7
            if dx * dx + dy * dy <= 1:
                inten = -dx * 0.6 - dy * 0.6 + math.sqrt(max(0, 1 - dx * dx - dy * dy)) * 0.5
                cv.px(x, y, s[shade_index(inten, len(s), x, y, 0.3)])
    cv.px(5, 7, s[5]); cv.px(6, 7, s[5]); cv.px(5, 8, s[5])
    cv.px(8, 4, s[3]); cv.px(8, 3, s[4])
    return done(cv)


def shroom_cap():
    cv = Canvas(16, 16)
    r = P["shroom_red"]
    c = P["cream"]
    cv.rect(7, 9, 3, 5, c[3])
    cv.vline(9, 9, 13, c[1])
    shaded_ellipse(cv, 8, 9, 7, 6, r[1:], dither=0.3, clip=lambda x, y: y <= 9)
    for (x, y) in [(5, 5), (9, 4), (11, 7), (7, 7)]:
        cv.px(x, y, c[4])
    cv.hline(2, 13, 9, c[1])
    return done(cv)


def claw():
    cv = Canvas(16, 16)
    b = P["bone"]
    for i, ox in enumerate((0, 4, 8)):
        pts = [(3 + ox, 13), (5 + ox, 13), (7 + ox, 5 + i % 2), (6 + ox, 2 + i % 2)]
        for y in range(16):
            for x in range(16):
                if point_in_poly(x + 0.5, y + 0.5, pts):
                    t = (y - 2) / 11
                    cv.px(x, y, b[3] if t < 0.3 else (b[2] if t < 0.7 else b[1]))
    cv.hline(2, 13, 13, P["fur"][3])
    cv.hline(2, 13, 14, P["fur"][2])
    return done(cv)


def pelt():
    cv = Canvas(16, 16)
    f = P["fur"]
    pts = [(2, 4), (5, 2), (11, 2), (14, 4), (13, 8), (14, 13), (10, 12), (6, 12), (2, 13), (3, 8)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                n = (math.sin(x * 1.3) + math.cos(y * 1.7)) * 0.2
                inten = -(x - 8) / 8 * 0.5 - (y - 7) / 7 * 0.4 + n
                cv.px(x, y, f[1:][shade_index(inten, 5, x, y, 0.4)])
    for (x, y) in [(6, 5), (9, 7), (7, 9)]:
        cv.px(x, y, f[5])
    return done(cv)


def meat():
    cv = Canvas(16, 16)
    r = P["red"]
    b = P["bone"]
    shaded_ellipse(cv, 7, 8, 5.5, 4.5, [r[1], r[2], r[3], hx("#f07a70")], dither=0.3)
    cv.ellipse(6, 7, 2, 1.4, hx("#ffc8b8"))
    # bone end
    cv.line(11, 11, 14, 14, b[3], 2)
    cv.px(14, 15, b[2]); cv.px(15, 14, b[2]); cv.px(15, 15, b[1])
    return done(cv)


def apple():
    cv = Canvas(16, 16)
    r = P["red"]
    shaded_ellipse(cv, 8, 9.5, 5.5, 5, r[1:], dither=0.3, bias=0.05)
    cv.px(8, 4, P["wood"][2]); cv.px(8, 3, P["wood"][3])
    cv.px(9, 3, P["leaf"][4]); cv.px(10, 3, P["leaf"][5]); cv.px(10, 2, P["leaf"][4])
    cv.px(5, 7, (255, 230, 220, 255)); cv.px(5, 8, r[4])
    return done(cv)


def sword_icon():
    cv = Canvas(16, 16)
    m = P["metal"]
    for k in range(9):
        x, y = 4 + k, 11 - k
        cv.px(x, y, m[3])
        cv.px(x + 1, y, m[4] if k < 8 else m[5])
        cv.px(x, y - 1 if k > 0 else y, m[2]) if k % 3 == 0 else None
    cv.px(13, 2, m[5])
    # guard
    cv.line(2, 10, 6, 14, P["gold"][3])
    cv.px(2, 10, P["gold"][4])
    # grip
    cv.px(3, 13, P["leather"][2]); cv.px(2, 14, P["leather"][3]); cv.px(1, 15, P["gold"][3])
    return done(cv)


def shield_icon():
    cv = Canvas(16, 16)
    w = P["wood"]
    pts = [(2, 2), (14, 2), (14, 8), (8, 15), (2, 8)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                inten = -(x - 8) / 7 * 0.6 - (y - 8) / 8 * 0.3
                cv.px(x, y, w[1:][shade_index(inten, 5, x, y, 0.3)])
    # metal rim + boss
    for x in range(2, 15):
        cv.px(x, 2, P["metal"][3])
    cv.ellipse(8, 7, 2, 2, P["metal"][3])
    cv.px(7, 6, P["metal"][5])
    cv.vline(8, 3, 13, w[1])
    return done(cv)


def ring():
    cv = Canvas(16, 16)
    g = P["gold"]
    for a in range(0, 360, 6):
        r = math.radians(a)
        x, y = 8 + math.cos(r) * 4.5, 10 + math.sin(r) * 3.5
        cv.px(round(x), round(y), g[4] if math.sin(r) < 0 else g[2])
    shaded_ellipse(cv, 8, 5, 2.8, 2.6, P["ice"][1:], dither=0.2)
    cv.px(7, 4, (255, 255, 255, 255))
    return done(cv)


def gem(rmp):
    cv = Canvas(16, 16)
    pts = [(8, 2), (13, 6), (8, 14), (3, 6)]
    for y in range(16):
        for x in range(16):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                # facets
                if y < 6:
                    c = rmp[4] if x < 8 else rmp[3]
                else:
                    c = rmp[2] if x < 8 else rmp[1]
                cv.px(x, y, c)
    cv.hline(4, 12, 6, rmp[5] if len(rmp) > 5 else rmp[4])
    cv.px(6, 4, (255, 255, 255, 255))
    return done(cv)


def scroll():
    cv = Canvas(16, 16)
    c = P["cream"]
    cv.rect(3, 4, 10, 8, c[3])
    for y in (6, 8, 10):
        cv.hline(5, 11, y, c[1])
    cv.rect(2, 3, 12, 2, c[2])
    cv.rect(2, 11, 12, 2, c[2])
    cv.px(2, 3, c[4]); cv.px(2, 11, c[4])
    cv.rect(7, 12, 2, 3, P["red"][2])
    return done(cv)


def key():
    cv = Canvas(16, 16)
    g = P["gold"]
    for a in range(0, 360, 8):
        r = math.radians(a)
        cv.px(round(5 + math.cos(r) * 2.6), round(5 + math.sin(r) * 2.6), g[3])
    cv.line(7, 7, 13, 13, g[3], 2)
    cv.px(11, 13, g[2]); cv.px(10, 14, g[2]); cv.px(13, 11, g[2]); cv.px(14, 10, g[2])
    cv.px(4, 3, g[5])
    return done(cv)


def herb():
    cv = Canvas(16, 16)
    l = P["green"]
    cv.line(8, 14, 8, 6, P["leaf"][3])
    for (x, y, d) in [(5, 8, -1), (11, 7, 1), (6, 4, -1), (10, 11, 1), (8, 3, 0)]:
        shaded_ellipse(cv, x, y, 2.6, 1.8, l[1:], dither=0.2)
    return done(cv)


def bread():
    cv = Canvas(16, 16)
    b = [hx("#6a3c1c"), hx("#9a5a28"), hx("#c8823c"), hx("#e8aa5a"), hx("#ffd08a")]
    shaded_ellipse(cv, 8, 9, 6.5, 4.5, b, dither=0.3, bias=0.05)
    for x in (5, 8, 11):
        cv.line(x - 1, 7, x + 1, 9, b[1])
    return done(cv)


def bone():
    cv = Canvas(16, 16)
    b = P["bone"]
    cv.line(4, 12, 12, 4, b[2], 2)
    for (x, y) in [(3, 11), (4, 13), (11, 3), (13, 4)]:
        shaded_ellipse(cv, x, y, 1.6, 1.6, b[1:], dither=0.1)
    return done(cv)


# ============================================================================
# Skill icons 24x24
# ============================================================================

def skill_bg(c_dark, c_mid, c_light):
    cv = Canvas(24, 24)
    for y in range(24):
        for x in range(24):
            t = (x + y) / 46
            c = mix(c_light, c_dark, t)
            if (x + y) % 4 == 0 and t > 0.5:
                c = mix(c, c_dark, 0.35)
            cv.px(x, y, c)
    # inner bevel
    cv.hline(0, 23, 0, mix(c_light, (255, 255, 255, 255), 0.4))
    cv.vline(0, 0, 23, mix(c_light, (255, 255, 255, 255), 0.25))
    cv.hline(0, 23, 23, mix(c_dark, (0, 0, 0, 255), 0.4))
    cv.vline(23, 0, 23, mix(c_dark, (0, 0, 0, 255), 0.3))
    return cv


def overlay(bg: Canvas, fg: Canvas, out=ICON_OUT):
    fg.outline(out)
    bg.blit(fg, 0, 0)
    return bg


def sk_slash():
    bg = skill_bg(hx("#0e3a44"), hx("#1e6a74"), hx("#4ab4b0"))
    fg = Canvas(24, 24)
    for arc, (r, w, c) in enumerate([(9, 2.2, hx("#ffffff")), (6, 1.6, hx("#c8fff4"))]):
        for a in range(-60, 110, 2):
            rad = math.radians(a)
            x = 12 + math.cos(rad) * r - 2 + arc * 3
            y = 12 + math.sin(rad) * r - 1
            th = w * (1 - abs(a - 25) / 85)
            for k in range(int(th) + 1):
                fg.px(round(x - math.cos(rad) * k), round(y - math.sin(rad) * k), c if k < th - 0.5 else hx("#6ae0d8"))
    return overlay(bg, fg)


def sk_fireball():
    bg = skill_bg(hx("#3a0a06"), hx("#8a1e0a"), hx("#e8641a"))
    fg = Canvas(24, 24)
    f = P["fire"]
    # trail
    for k in range(10):
        x, y = 5 + k * 0.9, 19 - k * 0.9
        r = 1 + k * 0.35
        fg.ellipse(x, y, r, r, f[2 + min(3, k // 3)])
    shaded_ellipse(fg, 15, 9, 5.5, 5.5, f[3:], dither=0.3, bias=0.1)
    fg.px(13, 7, f[6]); fg.px(14, 7, f[6]); fg.px(13, 8, f[6])
    return overlay(bg, fg)


def sk_ice():
    bg = skill_bg(hx("#0a1e3a"), hx("#1e4a8a"), hx("#5ab0e8"))
    fg = Canvas(24, 24)
    ic = P["ice"]
    for (bx, h, w) in [(6, 10, 3), (12, 16, 4), (18, 11, 3)]:
        pts = [(bx - w, 21), (bx + w, 21), (bx, 21 - h)]
        for y in range(24):
            for x in range(24):
                if point_in_poly(x + 0.5, y + 0.5, pts):
                    fg.px(x, y, ic[4] if x < bx else ic[2])
        fg.px(bx - 1, 21 - h + 3, ic[5])
    fg.hline(2, 21, 21, ic[3])
    return overlay(bg, fg)


def sk_lightning():
    bg = skill_bg(hx("#1a0e3a"), hx("#3c1f7a"), hx("#8a5ae0"))
    fg = Canvas(24, 24)
    pts = [(14, 1), (6, 13), (11, 13), (8, 23), (18, 9), (13, 9), (16, 1)]
    for y in range(24):
        for x in range(24):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                fg.px(x, y, hx("#fff4a0") if x < 12 else hx("#ffd84a"))
    fg.px(13, 3, (255, 255, 255, 255)); fg.px(12, 5, (255, 255, 255, 255))
    return overlay(bg, fg)


def sk_heal():
    bg = skill_bg(hx("#0a2e14"), hx("#1e6a2a"), hx("#5ac85a"))
    fg = Canvas(24, 24)
    g = P["green"]
    fg.rect(9, 4, 6, 16, g[4])
    fg.rect(4, 9, 16, 6, g[4])
    fg.rect(10, 5, 4, 14, g[5])
    fg.rect(5, 10, 14, 4, g[5])
    fg.px(10, 5, (255, 255, 255, 255))
    for (x, y) in [(4, 4), (19, 5), (18, 19)]:
        fg.px(x, y, g[5]); fg.px(x - 1, y, g[3]); fg.px(x + 1, y, g[3]); fg.px(x, y - 1, g[3]); fg.px(x, y + 1, g[3])
    return overlay(bg, fg)


def sk_shield():
    bg = skill_bg(hx("#2a2008"), hx("#6a5214"), hx("#e8c050"))
    fg = Canvas(24, 24)
    g = P["gold"]
    pts = [(4, 4), (20, 4), (20, 12), (12, 21), (4, 12)]
    for y in range(24):
        for x in range(24):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                inner = point_in_poly(x + 0.5, y + 0.5, [(7, 6), (17, 6), (17, 12), (12, 18), (7, 12)])
                fg.px(x, y, (g[5] if x < 12 else g[4]) if inner else (g[3] if x < 12 else g[2]))
    fg.vline(12, 7, 16, P["blue"][3])
    fg.hline(9, 15, 10, P["blue"][3])
    return overlay(bg, fg)


def sk_bladestorm():
    bg = skill_bg(hx("#2e0a14"), hx("#6a1428"), hx("#d04a5a"))
    fg = Canvas(24, 24)
    m = P["metal"]
    for i in range(3):
        a = math.radians(i * 120 - 90)
        for k in range(3, 10):
            x = 12 + math.cos(a) * k
            y = 12 + math.sin(a) * k
            fg.px(round(x), round(y), m[4])
            ax, ay = math.cos(a + 1.57), math.sin(a + 1.57)
            fg.px(round(x + ax), round(y + ay), m[3])
        # swirl
        for s in range(8):
            b = a + s * 0.12
            fg.px(round(12 + math.cos(b) * 10), round(12 + math.sin(b) * 10), hx("#ffb0b8"))
    fg.ellipse(12, 12, 2, 2, P["gold"][3])
    return overlay(bg, fg)


def sk_dash():
    bg = skill_bg(hx("#141a2e"), hx("#2e3a5a"), hx("#7a8ab8"))
    fg = Canvas(24, 24)
    w = P["white"]
    for (y, x0, x1) in [(7, 3, 12), (12, 1, 14), (17, 4, 11)]:
        fg.hline(x0, x1, y, w[2])
        fg.px(x0, y, w[1])
    pts = [(13, 5), (21, 12), (13, 19), (15, 12)]
    for y in range(24):
        for x in range(24):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                fg.px(x, y, w[3] if y < 12 else w[2])
    return overlay(bg, fg)


def sk_stomp():
    bg = skill_bg(hx("#2a1a10"), hx("#5a3a22"), hx("#a8784a"))
    fg = Canvas(24, 24)
    s = P["stone"]
    fg.hline(2, 21, 17, s[3])
    for (x0, y0, x1, y1) in [(12, 17, 7, 22), (12, 17, 17, 22), (12, 17, 4, 18), (12, 17, 20, 19)]:
        fg.line(x0, y0, x1, y1, OUTLINE)
    shaded_ellipse(fg, 12, 11, 6, 4, P["fur"][1:], dither=0.2)
    for x in (8, 11, 14):
        fg.px(x, 15, P["bone"][3])
    return overlay(bg, fg)


def sk_rock():
    bg = skill_bg(hx("#1a1a24"), hx("#3a3a4a"), hx("#7a7a92"))
    fg = Canvas(24, 24)
    shaded_ellipse(fg, 13, 10, 7, 6, P["stone"][1:], dither=0.25)
    for k in range(5):
        fg.px(4 + k, 19 - k, P["white"][1])
    fg.px(11, 8, P["moss"][3]); fg.px(12, 7, P["moss"][3]); fg.px(13, 7, P["moss"][2])
    return overlay(bg, fg)


def sk_pounce():
    bg = skill_bg(hx("#2a0a0a"), hx("#6a1a14"), hx("#c8503a"))
    fg = Canvas(24, 24)
    for i in range(3):
        fg.line(6 + i * 5, 4, 3 + i * 5, 20, P["bone"][3], 2)
        fg.px(6 + i * 5, 3, (255, 255, 255, 255))
    return overlay(bg, fg)


def sk_roar():
    bg = skill_bg(hx("#1a0a2a"), hx("#3a1a5a"), hx("#9060c8"))
    fg = Canvas(24, 24)
    for r in (4, 7, 10):
        for a in range(-50, 51, 5):
            rad = math.radians(a)
            fg.px(round(5 + math.cos(rad) * r), round(12 + math.sin(rad) * r), P["purple"][4])
    fg.ellipse(4, 12, 2, 3, P["red"][2])
    return overlay(bg, fg)


# ============================================================================
# Status icons 10x10
# ============================================================================

def st_stun():
    cv = Canvas(10, 10)
    for (x, y) in [(2, 3), (7, 2), (5, 7)]:
        cv.px(x, y, P["gold"][5]); cv.px(x - 1, y, P["gold"][3]); cv.px(x + 1, y, P["gold"][3])
        cv.px(x, y - 1, P["gold"][3]); cv.px(x, y + 1, P["gold"][3])
    return done(cv)


def st_slow():
    cv = Canvas(10, 10)
    ic = P["ice"]
    for i in range(4):
        cv.line(5, 5, 5 + round(math.cos(i * 0.785 * 2) * 4), 5 + round(math.sin(i * 0.785 * 2) * 4), ic[4])
        cv.line(5, 5, 5 - round(math.cos(i * 0.785 * 2) * 4), 5 - round(math.sin(i * 0.785 * 2) * 4), ic[4])
    cv.px(5, 5, ic[5])
    return done(cv)


def st_burn():
    cv = Canvas(10, 10)
    f = P["fire"]
    for y in range(2, 9):
        w = (y - 2) * 0.55
        for x in range(int(5 - w), int(5 + w) + 1):
            cv.px(x, y, f[5] if abs(x - 5) < w * 0.4 and y > 5 else f[3])
    return done(cv)


def st_shield():
    cv = Canvas(10, 10)
    g = P["gold"]
    pts = [(1, 1), (9, 1), (9, 5), (5, 9), (1, 5)]
    for y in range(10):
        for x in range(10):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                cv.px(x, y, g[4] if x < 5 else g[3])
    return done(cv)


def st_regen():
    cv = Canvas(10, 10)
    g = P["green"]
    cv.rect(4, 1, 2, 8, g[4])
    cv.rect(1, 4, 8, 2, g[4])
    return done(cv)


def build_items():
    return [
        ("potion_red", potion(P["red"][1:])),
        ("potion_blue", potion(P["blue"][1:])),
        ("potion_green", potion(P["green"][1:])),
        ("coin", coin()),
        ("gold", coin_stack()),
        ("gel", gel()),
        ("shroom_cap", shroom_cap()),
        ("claw", claw()),
        ("pelt", pelt()),
        ("meat", meat()),
        ("apple", apple()),
        ("sword", sword_icon()),
        ("shield", shield_icon()),
        ("ring", ring()),
        ("gem_red", gem(P["red"])),
        ("gem_blue", gem(P["blue"])),
        ("scroll", scroll()),
        ("key", key()),
        ("herb", herb()),
        ("bread", bread()),
        ("bone", bone()),
    ]


def build_skills():
    return [
        ("sk_slash", sk_slash()), ("sk_fireball", sk_fireball()), ("sk_ice", sk_ice()),
        ("sk_lightning", sk_lightning()), ("sk_heal", sk_heal()), ("sk_shield", sk_shield()),
        ("sk_bladestorm", sk_bladestorm()), ("sk_dash", sk_dash()),
        ("sk_stomp", sk_stomp()), ("sk_rock", sk_rock()), ("sk_pounce", sk_pounce()), ("sk_roar", sk_roar()),
    ]


def build_status():
    return [("st_stun", st_stun()), ("st_slow", st_slow()), ("st_burn", st_burn()),
            ("st_shield", st_shield()), ("st_regen", st_regen())]
