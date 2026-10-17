"""Writes Unreal/Source/RungThiTham/RttText.h: the Vietnamese words the game shows, as TEXT()
literals with \\u escapes so the compiler never guesses the source file's encoding.

    python Tools/Unreal/make_text.py

Edit the table below, not the header.
"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Unreal", "Source", "RungThiTham", "RttText.h")

TEXT = [
    ("BearName", "Gấu Ma Rừng Già"),
    ("BeastName", "Khỉ Đá Rừng Già"),
    ("Punch", "Đấm Quét"),
    ("Slam", "Nện Đất"),
    ("Throw", "Ném Đá Lớn"),
    ("Roar", "Gầm"),
    ("Swipe", "Vồ"),
    ("Stomp", "Dậm Đất"),
    ("Charge", "Húc"),
    ("Step", "Giẫm"),
    ("Rage", "Cuồng Nộ"),
    ("Stunned", "Choáng!"),
    ("Interrupted", "Gián Đoạn!"),
    ("Paralysed", "Tê Liệt!"),
    ("Falls", "Ngã Xuống"),
    ("BrokenPrefix", "Vỡ "),
    ("Perfect", "Hoàn Hảo!"),
    ("Slash", "Chém Gió"),
    ("YouFell", "Bạn đã gục ngã"),
    ("RiseIn", "Đứng dậy sau %d giây"),
    ("BearDown", "Gấu Ma Rừng Già đã ngã!"),
    ("BearBack", "Gấu Ma quay lại sau %d giây"),
    ("StaggerBar", "Choáng"),
    ("Life", "Máu"),
    ("Controls", "Chuột trái: Chém Gió  ·  Giữ chuột phải: Trảm  ·  Shift: Lướt (giữ: chạy nhanh)  ·  Space: Nhảy"),
]


def escape(s):
    out = []
    for ch in s:
        o = ord(ch)
        if o < 128:
            out.append("\\\\" if ch == "\\" else ('\\"' if ch == '"' else ch))
        else:
            out.append("\\u%04x" % o)
    return "".join(out)


lines = [
    "// The Vietnamese words the game shows. Written by Tools/Unreal/make_text.py: edit the table there.",
    "#pragma once",
    "",
    "#include \"CoreMinimal.h\"",
    "",
    "namespace RttText",
    "{",
]
for key, value in TEXT:
    lines.append("\t// %s" % value)
    lines.append("\tinline const TCHAR* %s = TEXT(\"%s\");" % (key, escape(value)))
lines.append("}")
with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(lines) + "\n")
print("wrote", OUT)
