import sys
from PIL import Image
import gen_props, gen_terrain
from pixelkit import pack_shelf, preview
OUT = sys.argv[1]
items = gen_props.build()
sheet, rects = pack_shelf(items, 256)
preview(sheet.to_image(), 3, f"{OUT}/props_sheet.png")
# scene: props on grass
ter, trects = gen_terrain.build()
timg = ter.to_image()
g = timg.crop((0, 0, 16, 16))
W, H = 320, 180
bg = Image.new("RGBA", (W, H))
for y in range(0, H, 16):
    for x in range(0, W, 16):
        bg.alpha_composite(g, (x, y))
simg = sheet.to_image()
lay = [("pine_0", 30, 70), ("pine_1", 60, 80), ("oak_0", 110, 75), ("bush_big_0", 150, 95), ("boulder", 190, 100),
       ("rock_big_0", 225, 95), ("log", 270, 90), ("hut", 60, 170), ("tent", 125, 160), ("campfire", 165, 150), ("flame_0", 165, 146),
       ("well", 205, 165), ("stump", 240, 150), ("mushrooms", 265, 150), ("fence_h", 280, 170), ("fence_h", 296, 170),
       ("signpost", 300, 140), ("crate", 20, 150), ("barrel", 35, 160), ("pillar_0", 250, 60), ("pillar_1", 280, 55),
       ("rock_small_0", 95, 110), ("bush_small_0", 180, 60), ("pine_2", 210, 60), ("oak_1", 150, 45), ("lantern", 100, 170)]
lay.sort(key=lambda t: t[2])
rd = {r[0]: r for r in rects}
for name, x, y in lay:
    n, rx, ry, w, h, px, py = rd[name]
    spr = simg.crop((rx, ry, rx + w, ry + h))
    bg.alpha_composite(spr, (x - px, y - py))
preview(bg, 4, f"{OUT}/props_scene.png")
print(len(rects), "props; sheet", sheet.w, sheet.h)
