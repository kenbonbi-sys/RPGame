import sys
from PIL import Image
from pixelkit import Canvas, preview
OUT = sys.argv[1]
mod = __import__(sys.argv[2])
A = mod.build()
order = getattr(mod, "ORDER", list(A.keys()))
fw = max(f.w for v in A.values() for f in v)
fh = max(f.h for v in A.values() for f in v)
cols = max(len(A[k]) for k in order)
sheet = Canvas(fw * cols, fh * len(order))
for r, k in enumerate(order):
    for c, f in enumerate(A[k]):
        sheet.blit(f, c * fw, r * fh)
preview(sheet.to_image(), int(sys.argv[3]) if len(sys.argv) > 3 else 4, f"{OUT}/{sys.argv[2]}.png", bg=(74, 110, 60, 255))
print("rows:", order)
