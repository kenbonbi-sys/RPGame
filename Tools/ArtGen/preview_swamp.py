"""Renders a composed test map of the swamp tiles (to check seams and the look)."""
import math
import random
import sys

from PIL import Image

import gen_swamp as gs
from pixelkit import Canvas, preview

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
T = 16

tiles = {}
for row in gs.tiles():
    for name, cv in row:
        tiles[name] = cv.to_image()

W, H = 30, 17
rnd = random.Random(5)
water = [[False] * (W + 1) for _ in range(H + 1)]
mud = [[False] * (W + 1) for _ in range(H + 1)]
for vy in range(H + 1):
    for vx in range(W + 1):
        if (vx - 8) ** 2 * 0.7 + (vy - 5) ** 2 < 14 or (vx - 22) ** 2 * 0.5 + (vy - 11) ** 2 < 18:
            water[vy][vx] = True
        py = 12 + 2.5 * math.sin(vx * 0.3)
        if abs(vy - py) < 1.4 and not water[vy][vx]:
            mud[vy][vx] = True


def idx(g, x, y):
    return (g[y][x] << 3) | (g[y][x + 1] << 2) | (g[y + 1][x + 1] << 1) | g[y + 1][x]


m = Image.new("RGBA", (W * T, H * T))
for y in range(H):
    for x in range(W):
        # the grids count rows from the top here; Unity's tilemap from the bottom: flip y for the index
        m.alpha_composite(tiles[f"swamp_{rnd.randrange(8)}"], (x * T, y * T))
        yy = H - 1 - y
        di = (mud[yy + 1][x] << 3) | (mud[yy + 1][x + 1] << 2) | (mud[yy][x + 1] << 1) | mud[yy][x]
        if di == 15:
            m.alpha_composite(tiles[f"mudfull_{rnd.randrange(4)}"], (x * T, y * T))
        elif di:
            m.alpha_composite(tiles[f"mud_{di}"], (x * T, y * T))
        wi = (water[yy + 1][x] << 3) | (water[yy + 1][x + 1] << 2) | (water[yy][x + 1] << 1) | water[yy][x]
        if wi == 15:
            m.alpha_composite(tiles[f"waterfull_{rnd.randrange(4)}"], (x * T, y * T))
            if rnd.random() < 0.15:
                m.alpha_composite(tiles[f"lily_{rnd.randrange(3)}"], (x * T, y * T))
        elif wi:
            m.alpha_composite(tiles[f"water_{wi}"], (x * T, y * T))
        elif di == 0 and rnd.random() < 0.1:
            m.alpha_composite(tiles[rnd.choice(["reeds_0", "reeds_1", "puddle_0", "rotleaf_0", "lotus_0"])], (x * T, y * T))
preview(m, 3, f"{OUT}/swamp_map.png")
print("ok")
