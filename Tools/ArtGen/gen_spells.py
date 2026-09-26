"""
Sách Chiêu (T63): skill icons (24x24) of the Băng, Lôi and Ám schools' new spells, drawn from
glyphs on the class icons' bevelled squares (gen_class_icons), and the Bí Kíp books that teach
them (16x16 items): a leather-bound tome in its school's colour with the spell's glyph shrunk on
its cover, gold-cornered for a Tuyệt kỹ.
"""
import math

from pixelkit import Canvas, P, hx, mix, shaded_ellipse, ramp
from gen_icons import done
from gen_class_icons import P6, icon, poly, g_arrow, g_shield, g_boot, g_dagger, g_orb, g_cloud, g_skull, g_vortex

WHITE = (255, 255, 255, 255)
ICE = P6["ice"]
VOLT = ramp("#3a2a8a", "#5a4ac8", "#8a7aff", "#c0b4ff", "#e8e4ff", "#ffffff")
DARK = ramp("#1a0a24", "#34163e", "#5a2a6a", "#8a4aa0", "#c088d8", "#f0d0ff")


# ---------------------------------------------------------------- glyphs (24x24, transparent)
def g_snowflake(c=ICE, r=9, cx=12, cy=12):
    fg = Canvas(24, 24)
    for k in range(6):
        a = math.radians(k * 60 + 90)
        x1, y1 = cx + math.cos(a) * r, cy + math.sin(a) * r
        fg.line(cx, cy, round(x1), round(y1), c[4])
        # the little branches two thirds of the way out
        bx, by = cx + math.cos(a) * r * 0.6, cy + math.sin(a) * r * 0.6
        for s in (-1, 1):
            b = a + s * math.radians(45)
            fg.line(round(bx), round(by), round(bx + math.cos(b) * r * 0.3), round(by + math.sin(b) * r * 0.3), c[3])
        fg.px(round(x1), round(y1), c[5])
    fg.ellipse(cx, cy, 1, 1, WHITE)
    return fg


def g_crystal(c=ICE):
    """Three ice pillars, the middle one tallest (Ngục Băng)."""
    fg = Canvas(24, 24)
    for (x, h, w) in ((6, 10, 3), (12, 16, 4), (18, 11, 3)):
        top = 21 - h
        poly(fg, [(x - w, 21), (x - w, top + w), (x, top), (x + w, top + w), (x + w, 21)], c[4], c[2], x)
        fg.line(x, top + 1, x, 20, c[5])
    fg.hline(2, 21, 21, c[1])
    return fg


def g_bolt(c=VOLT, x0=12, y0=2, scale=1.0):
    """A zigzag thunderbolt."""
    fg = Canvas(24, 24)
    s = scale
    pts = [(x0 + 3 * s, y0), (x0 - 4 * s, y0 + 11 * s), (x0, y0 + 11 * s), (x0 - 3 * s, y0 + 20 * s),
           (x0 + 5 * s, y0 + 8 * s), (x0 + 1 * s, y0 + 8 * s), (x0 + 5 * s, y0)]
    poly(fg, pts, c[4], c[3], x0)
    fg.line(round(x0 + 2 * s), round(y0 + 1), round(x0 - 2 * s), round(y0 + 9 * s), c[5])
    return fg


def g_chain(c=VOLT):
    """Lightning leaping between three sparks (Xích Lôi)."""
    fg = Canvas(24, 24)
    nodes = [(4, 18), (11, 7), (15, 15), (20, 5)]
    for (a, b) in zip(nodes, nodes[1:]):
        mx, my = (a[0] + b[0]) / 2 + 2, (a[1] + b[1]) / 2 - 1
        for (w, col) in ((2, c[3]), (1, c[5])):
            fg.line(a[0], a[1], round(mx), round(my), col, w)
            fg.line(round(mx), round(my), b[0], b[1], col, w)
    for (x, y) in nodes:
        fg.ellipse(x, y, 1, 1, c[5])
        fg.px(x, y, WHITE)
    return fg


def g_knives(c=DARK):
    """Three throwing knives fanning out (Ám Tiễn)."""
    fg = Canvas(24, 24)
    for ang in (-28, 0, 28):
        a = math.radians(-90 + ang)
        ox, oy = 12 + math.cos(a) * 3, 21 + math.sin(a) * 3
        tx, ty = 12 + math.cos(a) * 18, 21 + math.sin(a) * 18
        mx, my = 12 + math.cos(a) * 9, 21 + math.sin(a) * 9
        nx, ny = -math.sin(a), math.cos(a)
        poly(fg, [(mx + nx * 1.6, my + ny * 1.6), (tx, ty), (mx - nx * 1.6, my - ny * 1.6)], c[4], c[3], round(mx))
        fg.line(round(mx), round(my), round(tx), round(ty), c[5])
        fg.line(round(ox), round(oy), round(mx), round(my), P6["leather"][3])
        fg.line(round(mx + nx * 2), round(my + ny * 2), round(mx - nx * 2), round(my - ny * 2), P6["gold"][4])
    return fg


def g_sparkring(c=VOLT):
    """A ring of crackling sparks (Điện Trường)."""
    fg = Canvas(24, 24)
    for a in range(0, 360, 6):
        rad = math.radians(a)
        r = 8.5 + (1.2 if (a // 30) % 2 else 0)
        fg.px(round(12 + math.cos(rad) * r), round(12 + math.sin(rad) * r * 0.8), c[4] if (a // 6) % 3 else c[5])
    for k in range(4):
        a = math.radians(k * 90 + 45)
        x, y = 12 + math.cos(a) * 9, 12 + math.sin(a) * 7
        fg.line(round(x), round(y), round(x + math.cos(a + 1.5) * 3), round(y + math.sin(a + 1.5) * 3), c[5])
    fg.ellipse(12, 12, 2, 2, c[3])
    fg.px(12, 12, WHITE)
    return fg


def g_sigil(c=DARK):
    """A cursed ring with a five-point star (Lời Nguyền)."""
    fg = Canvas(24, 24)
    for a in range(0, 360, 4):
        rad = math.radians(a)
        fg.px(round(12 + math.cos(rad) * 9), round(12 + math.sin(rad) * 9), c[4])
    pts = [(12 + math.cos(math.radians(-90 + i * 144)) * 8, 12 + math.sin(math.radians(-90 + i * 144)) * 8) for i in range(6)]
    for (a, b) in zip(pts, pts[1:]):
        fg.line(round(a[0]), round(a[1]), round(b[0]), round(b[1]), c[5])
    fg.ellipse(12, 12, 1, 1, P6["red"][4])
    return fg


def g_figure(c, x, y):
    """A hooded figure (Phân Thân's two of them)."""
    fg = Canvas(24, 24)
    shaded_ellipse(fg, x, y, 3, 3, c[1:], dither=0.2)
    poly(fg, [(x - 4, y + 12), (x - 3, y + 2), (x + 3, y + 2), (x + 4, y + 12)], c[3], c[2], x)
    fg.px(x - 1, y, P6["red"][4])
    fg.px(x + 1, y, P6["red"][4])
    return fg


def g_eclipse():
    """A black sun in a violet corona."""
    fg = Canvas(24, 24)
    for a in range(0, 360, 15):
        rad = math.radians(a)
        r0, r1 = 8, 11 if (a // 15) % 2 else 10
        fg.line(round(12 + math.cos(rad) * r0), round(12 + math.sin(rad) * r0),
                round(12 + math.cos(rad) * r1), round(12 + math.sin(rad) * r1), DARK[4])
    fg.ellipse(12, 12, 7, 7, DARK[5])
    fg.ellipse(12, 12, 6, 6, (12, 4, 18, 255))
    fg.px(9, 9, DARK[3])
    return fg


def stack(*glyphs):
    """Layers glyphs (later ones on top) into one."""
    out = Canvas(24, 24)
    for g in glyphs:
        out.blit(g, 0, 0)
    return out


def shifted(g, dx, dy):
    return g.shifted(dx, dy)


# ---------------------------------------------------------------- the spells
def glyphs():
    """(spell id, glyph, background colour) of every new spell of the three schools."""
    return [
        # Băng
        ("frostarrows", g_arrow("ice", 3), "#1e4a7a"),
        ("frostarmor", g_shield("ice", ICE[5]), "#2a5a8a"),
        ("iceprison", g_crystal(), "#1a3a6a"),
        ("blizzard", stack(shifted(g_cloud("white"), 0, -5), g_snowflake(ICE, 5, 12, 16)), "#2a5a8a"),
        ("iceage", g_snowflake(), "#0e2a5a"),
        # Lôi
        ("chainlightning", g_chain(), "#2a1e6a"),
        ("thunderstep", stack(shifted(g_boot("ice"), -3, 0), g_bolt(VOLT, 17, 3, 0.7)), "#3a2a8a"),
        ("lightningbrand", stack(g_dagger("metal"), g_bolt(VOLT, 7, 2, 0.6)), "#34287a"),
        ("balllightning", stack(g_orb("ice"), g_bolt(VOLT, 13, 5, 0.6)), "#2a206a"),
        ("stormfield", g_sparkring(), "#221a5a"),
        ("thunderstorm", stack(shifted(g_cloud("metal"), 0, -6), g_bolt(VOLT, 12, 10, 0.65)), "#1a1450"),
        # Ám
        ("shadowknives", g_knives(), "#2a0e3a"),
        ("curse", g_sigil(), "#3a0e3a"),
        ("shadowclone", stack(g_figure(DARK, 8, 7), g_figure(ramp("#0a0410", "#1a0a24", "#2a1238", "#3a1a4a", "#4a2258"), 16, 8)), "#241030"),
        ("soulsiphon", stack(shifted(g_skull("purple"), 0, 1), shifted(g_vortex("purple"), 5, -5)), "#2a0a30"),
        ("eclipse", g_eclipse(), "#1a0624"),
    ]


def build():
    """(name, Canvas) of the new spells' skill icons (sk_<id>)."""
    return [("sk_" + sid, icon(g, bg)) for (sid, g, bg) in glyphs()]


# ---------------------------------------------------------------- Bí Kíp (16x16 items)
SCHOOL = {  # cover ramp of a school's books
    "ice": ramp("#16305a", "#23508a", "#3a7ab8", "#6aaee0", "#b4e0ff"),
    "lightning": ramp("#1e1650", "#2e2480", "#4a3cb0", "#7c6ce0", "#c0b4ff"),
    "dark": ramp("#140818", "#2a1034", "#44205a", "#6a3a88", "#a870c8"),
}
# which school each spell's book is bound for (Spellbook.All)
BOOKS = {
    "ice": ["ice", "frostarrows", "frostarmor", "iceprison", "blizzard", "iceage"],
    "lightning": ["chainlightning", "thunderstep", "lightningbrand", "balllightning", "stormfield", "thunderstorm"],
    "dark": ["shadowknives", "curse", "shadowstep", "shadowclone", "soulsiphon", "eclipse"],
}
ULTIMATES = {"iceage", "thunderstorm", "eclipse"}


def tome(school, glyph, ultimate):
    cv = Canvas(16, 16)
    c = SCHOOL[school]
    page = P["cream"]
    # the pages' edge on the right and bottom, then the cover
    cv.rect(4, 3, 10, 12, page[3])
    for y in range(4, 15, 2):
        cv.hline(12, 13, y, page[1])
    cv.rect(2, 2, 10, 12, c[2])
    cv.rect(2, 2, 2, 12, c[1])            # the spine
    cv.hline(2, 11, 2, c[3])
    cv.vline(11, 2, 13, c[1])
    # the spell's glyph on the cover, shrunk to 8x8
    for y in range(8):
        for x in range(8):
            px = glyph.get(x * 3 + 1, y * 3 + 1)
            if px[3] == 0:
                # a thin glyph: look at the neighbours too so it does not vanish
                for (dx, dy) in ((1, 0), (0, 1), (2, 1), (1, 2)):
                    q = glyph.get(x * 3 + dx, y * 3 + dy)
                    if q[3]:
                        px = q
                        break
            if px[3]:
                cv.px(4 + x, 4 + y, mix(px, c[4], 0.25))
    corner = P["gold"][4] if ultimate else c[4]
    for (x, y) in ((2, 2), (11, 2), (2, 13), (11, 13)):
        cv.px(x, y, corner)
    if ultimate:
        cv.hline(5, 9, 13, P["gold"][3])
    cv.rect(7, 14, 2, 2, P["red"][2])     # the ribbon
    return done(cv)


def tomes():
    """(tome_<id>, Canvas) of every Bí Kíp."""
    art = {sid: g for (sid, g, _bg) in glyphs()}
    # the older spells of the schools keep glyphs close to their prototype icons
    art["ice"] = g_crystal()
    art["shadowstep"] = g_vortex("blue")
    out = []
    for school, ids in BOOKS.items():
        for sid in ids:
            out.append(("tome_" + sid, tome(school, art[sid], sid in ULTIMATES)))
    return out


if __name__ == "__main__":
    import os
    import sys
    from pixelkit import preview, pack_row
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    preview(pack_row([c for _, c in build()]).to_image(), 5, os.path.join(out, "spell_icons.png"))
    preview(pack_row([c for _, c in tomes()]).to_image(), 6, os.path.join(out, "spell_tomes.png"))
    print("ok")
