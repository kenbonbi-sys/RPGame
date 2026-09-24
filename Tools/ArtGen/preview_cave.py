"""Renders a composed test map of the cave tiles and props (to check the seams, the cliff faces and the look)."""
import math
import random
import sys

from PIL import Image

import gen_cave as gc
from pixelkit import preview

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
T = 16

tiles = {}
for row in gc.tiles():
    for name, cv in row:
        tiles[name] = cv.to_image()
props = {name: (cv.to_image(), piv) for name, cv, piv in gc.props()}

W, H = 36, 22
rnd = random.Random(5)
# corner grid, rows counted from the bottom like Unity's tilemap: True = rock
wall = [[True] * (W + 1) for _ in range(H + 1)]
chambers = [(10, 12, 7, 5), (26, 9, 6, 4.5), (24, 17, 5, 3)]
for vy in range(H + 1):
    for vx in range(W + 1):
        for (cx, cy, rx, ry) in chambers:
            wob = 0.18 * math.sin(vx * 1.3 + vy * 0.7)
            if ((vx - cx) / rx) ** 2 + ((vy - cy) / ry) ** 2 < 1 + wob:
                wall[vy][vx] = False
        # a tunnel between them
        if 14 <= vx <= 24 and abs(vy - (11 + 0.2 * (vx - 14))) < 1.8:
            wall[vy][vx] = False


def idx(x, y):
    return (wall[y + 1][x] << 3) | (wall[y + 1][x + 1] << 2) | (wall[y][x + 1] << 1) | wall[y][x]


m = Image.new("RGBA", (W * T, H * T))
for y in range(H):
    for x in range(W):
        py = (H - 1 - y) * T
        m.alpha_composite(tiles[f"cave_{rnd.randrange(8)}"], (x * T, py))
        i = idx(x, y)
        if i == 15:
            m.alpha_composite(tiles[f"cavewallfull_{rnd.randrange(4)}"], (x * T, py))
        elif i:
            m.alpha_composite(tiles[f"cavewall_{i}"], (x * T, py))
        elif rnd.random() < 0.12:
            m.alpha_composite(tiles[rnd.choice(["shards_0", "shards_1", "shards_2", "pebbles_c", "crack_0", "glowcap_0", "cavepuddle_0"])], (x * T, py))
for name, (x, y) in (("crystal_big_cyan", (8, 13)), ("crystal_small_pink", (12, 10)), ("stalagmite_0", (6, 9)), ("minecart", (26, 8)),
                     ("rails_h", (23, 8)), ("cobweb_0", (24, 18)), ("crystal_big_amber", (29, 10)), ("caverock_big", (14, 13))):
    img, (px, py) = props[name]
    sx = int(x * T - px)
    sy = int((H - y) * T - py)
    m.alpha_composite(img, (sx, sy))
preview(m, 3, f"{OUT}/cave_map.png")
print("ok")
