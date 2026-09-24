"""
UI sprites (pixel art, meant to be scaled up with point filtering).
Sliced sprites report their 9-slice borders (L, B, R, T) in pixels.
"""
import math

import numpy as np

from pixelkit import Canvas, P, OUTLINE, hx, mix, shaded_ellipse, shade_index, point_in_poly, from_ascii

UI_OUT = hx("#0c080e")
PINK = P["pink_ui"]
DARK = P["ui_dark"]
GOLD = P["gold"]


def frame_boss():
    """Ornate boss bar frame: diamond end caps with gems. 9-slice L/R=16, T/B=7."""
    W, H = 56, 22
    cv = Canvas(W, H)
    # main band
    for y in range(3, 19):
        for x in range(8, W - 8):
            c = PINK[2]
            if y in (3, 18):
                c = PINK[4] if y == 3 else PINK[1]
            elif y in (4, 17):
                c = PINK[3] if y == 4 else PINK[1]
            elif 6 <= y <= 15:
                c = DARK[1]  # inner track background
            elif y == 5:
                c = PINK[1]
            elif y == 16:
                c = PINK[3]
            cv.px(x, y, c)
    # end caps: diamonds
    for side in (0, 1):
        cx = 8 if side == 0 else W - 9
        for y in range(H):
            for x in range(W):
                dx = abs(x + 0.5 - (cx + 0.5))
                dy = abs(y + 0.5 - 11)
                if dx + dy <= 10.5 and ((side == 0 and x <= cx + 2) or (side == 1 and x >= cx - 2)):
                    d = dx + dy
                    c = PINK[4] if d > 9.5 else (PINK[3] if d > 8 else PINK[2])
                    if d < 5:
                        c = hx("#8a1a2a") if d > 3.5 else (hx("#d83a4a") if d > 1.5 else hx("#ff9a9a"))
                    cv.px(x, y, c)
        # small gold studs
        cv.px(cx, 1, GOLD[4])
        cv.px(cx, 20, GOLD[2])
    cv.outline(UI_OUT)
    return cv, (16, 7, 16, 7)


def frame_panel():
    """Dark translucent panel with a thin mauve rim and gold corner studs. Border 6."""
    W = H = 18
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0:
                continue
            if edge == 1:
                c = PINK[3] if (y < H / 2) else PINK[2]
            elif edge == 2:
                c = DARK[0]
            else:
                c = hx("#140f18", 225)
            cv.px(x, y, c)
    # round the corners
    for (x, y) in [(1, 1), (W - 2, 1), (1, H - 2), (W - 2, H - 2)]:
        cv.px(x, y, GOLD[3])
    cv.outline(UI_OUT)
    return cv, (6, 6, 6, 6)


def frame_wood():
    """Warm wooden frame with gold trim (dialogue/minimap). Border 7."""
    W = H = 22
    cv = Canvas(W, H)
    wd = P["wood"]
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0:
                continue
            if edge == 1:
                c = GOLD[3] if y < H / 2 else GOLD[2]
            elif edge in (2, 3):
                c = wd[3] if (x + y) % 5 else wd[2]
                if edge == 3:
                    c = wd[1]
            elif edge == 4:
                c = GOLD[1]
            else:
                c = hx("#16101a", 235)
            cv.px(x, y, c)
    for (x, y) in [(2, 2), (W - 3, 2), (2, H - 3), (W - 3, H - 3)]:
        cv.px(x, y, GOLD[4])
    cv.outline(UI_OUT)
    return cv, (7, 7, 7, 7)


def slot():
    """Skill/potion slot. Border 5."""
    W = H = 16
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0:
                c = P["metal"][2] if (x + y) < W else P["metal"][1]
            elif edge == 1:
                c = P["metal"][0]
            elif edge == 2:
                c = hx("#06050a") if (x < W / 2 or y < H / 2) else hx("#2a2230")
            else:
                c = hx("#120e16", 240)
            cv.px(x, y, c)
    cv.outline(UI_OUT)
    return cv, (4, 4, 4, 4)


def slot_highlight():
    W = H = 16
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0:
                cv.px(x, y, GOLD[4])
            elif edge == 1:
                cv.px(x, y, hx("#ffe07a", 110))
    return cv, (4, 4, 4, 4)


def bar_track():
    W, H = 8, 8
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            cv.px(x, y, hx("#0a080c") if edge == 0 else hx("#221a26", 230))
    return cv, (2, 2, 2, 2)


def bar_fill():
    """White fill with a lit top edge; tinted in Unity."""
    W, H = 8, 8
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            v = 255 if y <= 1 else (225 if y < 5 else 185)
            if y == 0:
                v = 255
            cv.px(x, y, (v, v, v, 255))
    return cv, (1, 1, 1, 1)


def key_badge():
    W, H = 10, 10
    cv = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0 and (x in (0, W - 1)) and (y in (0, H - 1)):
                continue
            c = hx("#d8d0e0") if edge == 0 else (hx("#1a141e", 235))
            cv.px(x, y, c)
    return cv, (3, 3, 3, 3)


def orb_frame():
    """80x80 bronze ring with gold studs, a skull crest on top and bone horns."""
    S = 80
    cv = Canvas(S, S)
    c = S / 2
    bone = P["bone"]
    br = [hx("#1e1418"), hx("#3a2626"), hx("#5a3c30"), hx("#7a5a3e"), hx("#9e7c52"), hx("#c8a470")]
    for y in range(S):
        for x in range(S):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            if 30.5 <= d <= 37.5:
                t = (d - 30.5) / 7.0            # 0 inner .. 1 outer
                ang = math.atan2(y + 0.5 - c, x + 0.5 - c)
                light = -math.cos(ang + 2.35)   # +1 at top-left
                # rounded profile: brightest in the middle of the band
                prof = 1.0 - abs(t - 0.45) * 2.2
                inten = light * 0.55 + prof * 0.7 - 0.25
                idx = shade_index(inten, len(br), x, y, dither=0.25)
                col = br[idx]
                seg = (ang + math.pi) / math.tau * 32
                if abs(seg - round(seg)) < 0.09 and 0.15 < t < 0.85:
                    col = br[max(0, idx - 2)]
                if t < 0.12 or t > 0.9:
                    col = br[0]
                cv.px(x, y, col)
            elif 29.5 <= d < 30.5:
                cv.px(x, y, hx("#050308"))
    # studs
    for k in range(8):
        a = k / 8 * math.tau + math.pi / 8
        sx, sy = c + math.cos(a) * 34, c + math.sin(a) * 34
        shaded_ellipse(cv, sx, sy, 2.2, 2.2, GOLD[1:], dither=0.1)
    # skull crest on the top
    sk = from_ascii([
        "..bbbbbb..",
        ".bBBBBBBb.",
        "bBBBBBBBBb",
        "bBooBBooBb",
        "bBooBBooBb",
        "bBBBBoBBBb",
        ".bBBBBBBb.",
        "..bBbBbB..",
        "..b.b.b...",
    ], {"b": bone[1], "B": bone[3], "o": hx("#1a0c10")})
    cv.blit(sk, int(c - 5), 0)
    # side horns
    for sgn in (-1, 1):
        for k in range(8):
            x = c + sgn * (38 + k * 0.4)
            y = c - 4 - k * 1.2
            cv.px(int(x), int(y), bone[3] if k < 6 else bone[2])
            cv.px(int(x), int(y) + 1, bone[1])
    cv.outline(UI_OUT)
    return cv


def orb_disc(tone=None):
    """White shaded disc (liquid), tintable. 60x60"""
    S = 60
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / (S / 2)
            if d > 1:
                continue
            nx, ny = (x + 0.5 - c) / c, (y + 0.5 - c) / c
            nz = math.sqrt(max(0, 1 - nx * nx - ny * ny))
            inten = 0.35 + nz * 0.55 - ny * 0.2
            v = int(max(0.25, min(1.0, inten)) * 255)
            # swirl pattern
            ang = math.atan2(ny, nx)
            swirl = math.sin(ang * 3 + d * 9)
            if swirl > 0.6 and d < 0.9:
                v = min(255, v + 25)
            # quantize for a pixel look
            v = int(v / 32) * 32 + 16
            cv.px(x, y, (v, v, v, 255))
    return cv


def orb_back():
    S = 60
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / (S / 2)
            if d <= 1:
                cv.px(x, y, hx("#0c0a12") if d > 0.9 else hx("#16121e"))
    return cv


def orb_glass():
    S = 60
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / (S / 2)
            if d > 1:
                continue
            # crescent highlight at top-left
            d2 = math.hypot(x + 0.5 - (c - 7), y + 0.5 - (c - 9)) / (S / 2)
            if d < 0.92 and d2 > 0.78 and y < c:
                cv.px(x, y, (255, 255, 255, 70))
            # rim shadow
            if d > 0.9:
                cv.px(x, y, (0, 0, 0, 90))
    # specular dots
    for (x, y, a) in [(16, 14, 220), (17, 14, 200), (16, 15, 200), (20, 11, 150)]:
        cv.px(x, y, (255, 255, 255, a))
    return cv


def surface_line():
    W, H = 60, 3
    cv = Canvas(W, H)
    for x in range(W):
        cv.px(x, 0, (255, 255, 255, 120))
        cv.px(x, 1, (255, 255, 255, 200 if x % 7 < 4 else 150))
        cv.px(x, 2, (255, 255, 255, 60))
    return cv


def white():
    cv = Canvas(4, 4)
    cv.rect(0, 0, 4, 4, (255, 255, 255, 255))
    return cv


def skull_icon():
    return from_ascii([
        "..bbbbbb..",
        ".bBBBBBBb.",
        "bBBBBBBBBb",
        "bBooBBooBb",
        "bBooBBooBb",
        "bBBBBoBBBb",
        ".bBBBBBBb.",
        "..BbBbBb..",
    ], {"b": P["bone"][1], "B": P["bone"][3], "o": hx("#2a0c14")}).outline(UI_OUT)


def sun_icon():
    cv = Canvas(12, 12)
    g = GOLD
    cv.ellipse(6, 6, 3.2, 3.2, g[4])
    cv.px(5, 5, g[5])
    for (x, y) in [(6, 0), (6, 11), (0, 6), (11, 6), (2, 2), (9, 2), (2, 9), (9, 9)]:
        cv.px(x, y, g[3])
    return cv.outline(UI_OUT)


def moon_icon():
    cv = Canvas(12, 12)
    for y in range(12):
        for x in range(12):
            if math.hypot(x + 0.5 - 6, y + 0.5 - 6) < 5.0 and math.hypot(x + 0.5 - 8.2, y + 0.5 - 4.6) > 3.9:
                cv.px(x, y, hx("#e8ecff") if x < 5 else hx("#b8c0e8"))
    return cv.outline(UI_OUT)


def star_icon():
    return from_ascii([
        "...y...",
        "...y...",
        "yyyYyyy",
        ".yYYYy.",
        "..yYy..",
        ".yy.yy.",
        ".y...y.",
    ], {"y": GOLD[3], "Y": GOLD[5]}).outline(UI_OUT)


def quest_mark(ch="!"):
    if ch == "!":
        rows = [".yy.", "yYYy", "yYYy", "yYYy", ".yy.", ".yy.", "....", ".yy.", ".yy."]
    else:
        rows = [".yyy.", "yY.Yy", "...Yy", "..Yy.", "..yy.", ".....", "..yy.", "..yy."]
    return from_ascii(rows, {"y": GOLD[3], "Y": GOLD[5]}).outline(UI_OUT)


def cursor():
    return from_ascii([
        "o.........",
        "oWo.......",
        "oWWo......",
        "oWWWo.....",
        "oWWWWo....",
        "oWWWWWo...",
        "oWWWWWWo..",
        "oWWWWooo..",
        "oWoWWo....",
        "oo.oWWo...",
        "....oWo...",
        ".....o....",
    ], {"o": UI_OUT, "W": hx("#f4ecd8")})


def cursor_attack():
    cv = Canvas(12, 12)
    m = P["metal"]
    for k in range(8):
        cv.px(k + 1, k + 1, m[4])
        cv.px(k + 2, k + 1, m[3])
    cv.px(0, 0, m[5])
    cv.line(6, 9, 9, 6, GOLD[3])
    cv.px(9, 9, P["leather"][3]); cv.px(10, 10, P["leather"][3]); cv.px(11, 11, GOLD[3])
    return cv.outline(UI_OUT)


def divider():
    W, H = 64, 5
    cv = Canvas(W, H)
    for x in range(W):
        t = abs(x + 0.5 - W / 2) / (W / 2)
        if t < 0.95:
            cv.px(x, 2, mix(GOLD[4], GOLD[1], t))
    for y in range(H):
        for x in range(W):
            if abs(x + 0.5 - W / 2) + abs(y + 0.5 - 2.5) <= 2.6:
                cv.px(x, y, GOLD[5])
    return cv


def arrow_icon():
    return from_ascii([
        "...y...",
        "..yYy..",
        ".yYYYy.",
        "yyYYYyy",
        "..yYy..",
        "..yYy..",
    ], {"y": GOLD[3], "Y": GOLD[5]}).outline(UI_OUT)


def portrait_frame():
    """40x40 portrait frame for dialogue."""
    S = 40
    cv = Canvas(S, S)
    for y in range(S):
        for x in range(S):
            edge = min(x, y, S - 1 - x, S - 1 - y)
            if edge == 0:
                cv.px(x, y, GOLD[2] if y > S / 2 else GOLD[4])
            elif edge == 1:
                cv.px(x, y, P["wood"][3])
            elif edge == 2:
                cv.px(x, y, P["wood"][1])
            else:
                cv.px(x, y, mix(hx("#3a2a44"), hx("#1a1220"), y / S))
    return cv.outline(UI_OUT), (4, 4, 4, 4)


def build():
    """Returns list of (name, Canvas, border or None)."""
    out = []
    c, b = frame_boss(); out.append(("frame_boss", c, b))
    c, b = frame_panel(); out.append(("frame_panel", c, b))
    c, b = frame_wood(); out.append(("frame_wood", c, b))
    c, b = slot(); out.append(("slot", c, b))
    c, b = slot_highlight(); out.append(("slot_highlight", c, b))
    c, b = bar_track(); out.append(("bar_track", c, b))
    c, b = bar_fill(); out.append(("bar_fill", c, b))
    c, b = key_badge(); out.append(("key_badge", c, b))
    c, b = portrait_frame(); out.append(("portrait_frame", c, b))
    out.append(("orb_frame", orb_frame(), None))
    out.append(("orb_liquid", orb_disc(), None))
    out.append(("orb_back", orb_back(), None))
    out.append(("orb_glass", orb_glass(), None))
    out.append(("orb_surface", surface_line(), None))
    out.append(("white", white(), (1, 1, 1, 1)))
    out.append(("icon_skull", skull_icon(), None))
    out.append(("icon_sun", sun_icon(), None))
    out.append(("icon_moon", moon_icon(), None))
    out.append(("icon_star", star_icon(), None))
    out.append(("quest_excl", quest_mark("!"), None))
    out.append(("quest_ques", quest_mark("?"), None))
    out.append(("cursor", cursor(), None))
    out.append(("cursor_attack", cursor_attack(), None))
    out.append(("divider", divider(), None))
    out.append(("icon_arrow", arrow_icon(), None))
    return out
