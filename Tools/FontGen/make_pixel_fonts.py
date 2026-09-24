#!/usr/bin/env python3
"""
Vietnamese pixel fonts (plan T19, Docs/FontPixelTiengViet.md).

Downloads Galmuri7 (8 px) and Galmuri11 (12 px) by Lee Minseo (SIL OFL 1.1, v2.404), keeps
Latin, Vietnamese and the UI symbols (71 KB and 101 KB instead of 3.7-5.4 MB) and writes them
to Assets/Fonts, next to Galmuri-LICENSE.txt. Checks that all 134 Vietnamese letters survive.
With --preview it also renders Docs/font-pixel-tieng-viet.png: game text in Galmuri7/9/11 and
VT323 at their pixel sizes, x3, without anti-aliasing (like a point-filtered TMP raster atlas).

    pip install fonttools pillow
    python Tools/FontGen/make_pixel_fonts.py [--preview]

Commit the .ttf files (Git LFS) and the .meta files Unity creates for them.
"""
import argparse
import io
import os
import sys
import unicodedata
import urllib.request

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GALMURI = "https://raw.githubusercontent.com/quiple/galmuri/71e1cacf1437a11220307120e63e30bc275312d4/dist/{}.ttf"   # v2.404
VT323 = "https://raw.githubusercontent.com/google/fonts/main/ofl/vt323/VT323-Regular.ttf"
FONTS = ["Galmuri7", "Galmuri11"]
UNICODES = ("U+0020-007E,U+00A0-00FF,U+0100-017F,U+01A0-01B0,U+0300-0303,U+0309,U+0323,U+1EA0-1EF9,"
            "U+2010-2027,U+2030-203A,U+20AB,U+2190-2193,U+2212,U+2248,U+25A0-25FF,U+2605,U+2606,"
            "U+2620,U+2665,U+2713,U+2717")
LINES = [
    "Bách Khoa Trùm: ghi lại Gấu Ma Rừng Già",
    "Kỹ năng: Dậm Đất · Chụp Quăng · Cuồng Nộ!",
    "Nhiệm vụ: Tìm Mèo Mướp (+1 · Tab)",
    "Trưởng Làng: Rừng đang thì thầm, cháu nghe thấy không?",
    "RỪNG THÌ THẦM · ẤN ẨN ẪN ẬN · ỄỂỆ ỖỔỘ ỞỠỢ ỪỬỮỰ",
    "Hồi chiêu 3.5s · +15% năng lượng · Cấp 12 · 1 280 XP",
]


def vietnamese_letters():
    """The 134 letters beyond ASCII: every vowel with every tone, plus Đ/đ."""
    tones = ["", "̀", "́", "̉", "̃", "̣"]
    letters = {"đ", "Đ"}
    for v in "aăâeêioôơuưy":
        for t in tones:
            for c in (v, v.upper()):
                s = unicodedata.normalize("NFC", c + t)
                if len(s) == 1 and ord(s) > 127:
                    letters.add(s)
    assert len(letters) == 134
    return letters


def download(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return r.read()


def parse_unicodes(spec):
    codes = []
    for part in spec.split(","):
        a, _, b = part.replace("U+", "").partition("-")
        codes.extend(range(int(a, 16), int(b or a, 16) + 1))
    return codes


def make_subset(data, dst):
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.glyph_names = True
    font = subset.load_font(io.BytesIO(data), opts)
    s = subset.Subsetter(opts)
    s.populate(unicodes=parse_unicodes(UNICODES))
    s.subset(font)
    subset.save_font(font, dst, opts)
    have = {chr(c) for c in TTFont(dst).getBestCmap()}
    missing = vietnamese_letters() - have
    print(f"  {os.path.relpath(dst, ROOT)}: {os.path.getsize(dst) // 1024} KB, Vietnamese missing: {len(missing)}")
    return not missing


def preview(sources, dst):
    from PIL import Image, ImageDraw, ImageFont
    from fontTools.pens.boundsPen import BoundsPen
    scale, bg, fg, dim = 3, (27, 24, 34), (242, 232, 208), (150, 140, 170)
    blocks = []
    for title, data, px in sources:
        ft = TTFont(io.BytesIO(data), lazy=True)
        upm, hh = ft["head"].unitsPerEm, ft["hhea"]
        gs, cmap = ft.getGlyphSet(), ft.getBestCmap()
        top, bottom = 0, 0
        for ch in "ẪỄỖẨỞỮỰẶẬỆỘỊỴẸỌỤ":
            if ord(ch) in cmap:
                pen = BoundsPen(gs)
                gs[cmap[ord(ch)]].draw(pen)
                if pen.bounds:
                    top, bottom = max(top, pen.bounds[3]), min(bottom, pen.bounds[1])
        k = px / upm
        line = (hh.ascent - hh.descent + hh.lineGap) * k
        font = ImageFont.truetype(io.BytesIO(data), px)
        step = int(round(max(line, (top - bottom) * k))) + 2
        img = Image.new("RGB", (max(int(font.getlength(l)) for l in LINES) + 8, step * len(LINES) + 8), bg)
        d = ImageDraw.Draw(img)
        d.fontmode = "1"   # no anti-aliasing
        for i, l in enumerate(LINES):
            d.text((4, 4 + i * step), l, font=font, fill=fg)
        note = (f"{title}: dòng {line:.0f} px · dấu cao nhất {top * k:.0f} px trên chân chữ "
                f"(ascent {hh.ascent * k:.0f}) · dấu nặng sâu {-bottom * k:.0f} px")
        blocks.append((note, img.resize((img.width * scale, img.height * scale), Image.NEAREST)))
    label = ImageFont.truetype(io.BytesIO(sources[-2][1]), 12)
    sheet = Image.new("RGB", (max(b.width for _, b in blocks) + 40, sum(b.height + 44 for _, b in blocks) + 16), (18, 16, 24))
    d = ImageDraw.Draw(sheet)
    d.fontmode = "1"
    y = 14
    for note, big in blocks:
        d.text((20, y), note, font=label, fill=dim)
        sheet.paste(big, (20, y + 20))
        y += big.height + 44
    sheet.save(dst)
    print(f"  {os.path.relpath(dst, ROOT)}")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--preview", action="store_true", help="also render Docs/font-pixel-tieng-viet.png (needs Pillow)")
    args = ap.parse_args()
    print("[FontGen] Galmuri v2.404 -> Assets/Fonts")
    full = {}
    ok = True
    for name in FONTS + (["Galmuri9"] if args.preview else []):
        full[name] = download(GALMURI.format(name))
    for name in FONTS:
        ok &= make_subset(full[name], os.path.join(ROOT, "Assets", "Fonts", name + ".ttf"))
    if args.preview:
        preview([("Galmuri7 · 8 px", full["Galmuri7"], 8), ("Galmuri9 · 10 px", full["Galmuri9"], 10),
                 ("Galmuri11 · 12 px", full["Galmuri11"], 12), ("VT323 · 16 px", download(VT323), 16)],
                os.path.join(ROOT, "Docs", "font-pixel-tieng-viet.png"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
