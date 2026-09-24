"""Renders the terrain sheet and a composed test map (to check seams)."""
import math
import random
import sys

from PIL import Image

import gen_terrain as gt
from pixelkit import preview

OUT = sys.argv[1] if len(sys.argv) > 1 else "."

sheet, rects = gt.build()
img = sheet.to_image()
preview(img, 4, f"{OUT}/terrain_sheet.png")

tiles = {name: img.crop((x, y, x + w, y + h)) for (name, x, y, w, h) in rects}

W, H = 30, 17
rnd = random.Random(3)
# vertex grids
dirt = [[False] * (W + 1) for _ in range(H + 1)]
tall = [[False] * (W + 1) for _ in range(H + 1)]
for vy in range(H + 1):
    for vx in range(W + 1):
        # winding path
        py = 8 + 2.5 * math.sin(vx * 0.35)
        if abs(vy - py) < 1.6:
            dirt[vy][vx] = True
        # a clearing
        if (vx - 20) ** 2 + (vy - 9) ** 2 < 16:
            dirt[vy][vx] = True
        if (vx - 6) ** 2 * 0.6 + (vy - 2.5) ** 2 < 7 or (vx - 25) ** 2 + (vy - 15) ** 2 < 10:
            tall[vy][vx] = True


def idx(g, x, y):
    return (g[y][x] << 3) | (g[y][x + 1] << 2) | (g[y + 1][x + 1] << 1) | g[y + 1][x]


m = Image.new("RGBA", (W * 16, H * 16))
for y in range(H):
    for x in range(W):
        m.alpha_composite(tiles[f"grass_{rnd.randrange(8)}"], (x * 16, y * 16))
        ti = idx(tall, x, y)
        if ti == 15:
            m.alpha_composite(tiles[f"tallfull_{rnd.randrange(4)}"], (x * 16, y * 16))
        elif ti:
            m.alpha_composite(tiles[f"tall_{ti}"], (x * 16, y * 16))
        di = idx(dirt, x, y)
        if di == 15:
            m.alpha_composite(tiles[f"dirtfull_{rnd.randrange(4)}"], (x * 16, y * 16))
        elif di:
            m.alpha_composite(tiles[f"dirt_{di}"], (x * 16, y * 16))
        if di == 0 and ti == 0 and rnd.random() < 0.12:
            d = rnd.choice(["tuft_0", "tuft_1", "tuft_2", "flowers_0", "flowers_1", "leaves_0", "leaves_1", "pebbles_0", "clover_0", "mushrooms_0"])
            m.alpha_composite(tiles[d], (x * 16, y * 16))
preview(m, 3, f"{OUT}/terrain_map.png")
print("ok")
