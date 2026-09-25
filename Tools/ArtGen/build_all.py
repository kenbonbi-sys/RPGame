"""
Generates every piece of art into the Unity project and writes
Assets/Art/art_manifest.json which the Unity editor importer uses to
slice sprites, set pivots / 9-slice borders and build animation sets.

Run:  python build_all.py            (from Tools/ArtGen)
"""
import json
import os
import sys

from PIL import Image

import gen_terrain
import gen_props
import gen_player
import gen_npc
import gen_enemies
import gen_icons
import gen_ui
import gen_vfx
import gen_swamp
import gen_swamp_creatures
import gen_cave
import gen_cave_creatures
import gen_cave_deep
import gen_class_icons
import gen_gear
from pixelkit import Canvas, pack_shelf

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Art")

manifest = {"textures": [], "anims": []}


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def save(img: Image.Image, path: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def add_texture(path, img_h, sprites, filter_="point", ppu=16, kind="sprite"):
    """sprites: list of dicts {name, x, y(top), w, h, pivot:(px, py_from_top) or None, border:(L,B,R,T)}"""
    out = []
    for s in sprites:
        x, y, w, h = s["x"], s["y"], s["w"], s["h"]
        uy = img_h - y - h
        if s.get("pivot") is not None:
            px, py = s["pivot"]
            pivot = [round(px / w, 5), round((h - py) / h, 5)]
        else:
            pivot = [0.5, 0.5]
        out.append({"name": s["name"], "x": x, "y": uy, "w": w, "h": h, "pivot": pivot,
                    "border": list(s.get("border") or (0, 0, 0, 0))})
    manifest["textures"].append({"path": rel(path), "filter": filter_, "ppu": ppu, "kind": kind, "sprites": out})


def grid_sheet(prefix, anims: dict, order, fw, fh, pivot, path, fps_map, ppu=16, filter_="point"):
    rows = [k for k in order if k in anims]
    cols = max(len(anims[k]) for k in rows)
    sheet = Canvas(fw * cols, fh * len(rows))
    sprites = []
    for r, k in enumerate(rows):
        names = []
        for c, f in enumerate(anims[k]):
            sheet.blit(f, c * fw + (fw - f.w) // 2, r * fh + (fh - f.h))
            nm = f"{prefix}_{k}_{c}"
            sprites.append({"name": nm, "x": c * fw, "y": r * fh, "w": fw, "h": fh, "pivot": pivot})
            names.append(nm)
        fps, loop = fps_map(k)
        manifest["anims"].append({"set": prefix, "name": k, "frames": names, "fps": fps, "loop": loop})
    img = sheet.to_image()
    save(img, path)
    add_texture(path, img.height, sprites, filter_, ppu)
    return img


def char_fps(k):
    if k.startswith("idle") or k.startswith("chief_idle") or k.startswith("girl_idle"):
        return 3, True
    if k.startswith("walk") or k == "move":
        return 10, True
    if k.startswith("attack"):
        return 16, False
    if k.startswith("cast"):
        return 8, True
    if k.startswith("chief_talk"):
        return 6, True
    return 8, False


def build_terrain():
    forest, rects = gen_terrain.build()
    # the swamp's rows under the forest's (Đầm Lầy Sương Mù, east of the forest)
    T = gen_terrain.T
    sheet = Canvas(forest.w, 16 * T)
    sheet.blit(forest, 0, 0)
    y0 = forest.h
    # then the cave's (Hang Pha Lê, north of the swamp)
    for r, row in enumerate(gen_swamp.tiles() + gen_cave.tiles()):
        for c, (name, tile) in enumerate(row):
            sheet.blit(tile, c * T, y0 + r * T)
            rects.append((name, c * T, y0 + r * T, T, T))
    img = sheet.to_image()
    path = os.path.join(ART, "Tiles", "terrain.png")
    save(img, path)
    add_texture(path, img.height, [{"name": n, "x": x, "y": y, "w": w, "h": h, "pivot": (w / 2, h / 2)}
                                   for (n, x, y, w, h) in rects])


def build_props():
    items = gen_props.build() + gen_swamp.props() + gen_cave.props()
    sheet, rects = pack_shelf(items, 256)
    img = sheet.to_image()
    path = os.path.join(ART, "Props", "props.png")
    save(img, path)
    add_texture(path, img.height, [{"name": n, "x": x, "y": y, "w": w, "h": h, "pivot": (px + 0.5, py + 1)}
                                   for (n, x, y, w, h, px, py) in rects])
    manifest["anims"].append({"set": "props", "name": "flame", "frames": [f"flame_{i}" for i in range(4)], "fps": 8, "loop": True})


def build_chars():
    # player
    A = gen_player.build()
    grid_sheet("player", A, gen_player.ORDER, 32, 32, (16, 30), os.path.join(ART, "Characters", "player.png"), char_fps)
    # npcs
    N = gen_npc.build()
    grid_sheet("npc", N, gen_npc.ORDER, 32, 32, (16, 30), os.path.join(ART, "Characters", "npc.png"), char_fps)
    # slime
    S = gen_enemies.build_slime()
    sfps = lambda k: {"idle": (4, True), "move": (8, True), "attack": (10, False), "hurt": (1, False), "dead": (6, False)}[k]
    grid_sheet("slime", S, ["idle", "move", "attack", "hurt", "dead"], 24, 24, (12, 22), os.path.join(ART, "Characters", "slime.png"), sfps)
    M = gen_enemies.build_mushroom()
    mfps = lambda k: {"idle": (3, True), "move": (7, True), "attack": (9, False), "hurt": (1, False), "dead": (6, False)}[k]
    grid_sheet("shroom", M, ["idle", "move", "attack", "hurt", "dead"], 24, 26, (12, 24), os.path.join(ART, "Characters", "shroom.png"), mfps)
    B = gen_enemies.build_bear()
    bfps = lambda k: {"idle": (3, True), "walk": (6, True), "windup": (6, False), "slam": (10, False), "throw": (8, False),
                      "crouch": (1, False), "air": (1, False), "roar": (6, True), "hurt": (1, False), "dead": (3, False)}[k]
    grid_sheet("bear", B, ["idle", "walk", "windup", "slam", "throw", "crouch", "air", "roar", "hurt", "dead"],
               64, 64, (32, 61), os.path.join(ART, "Characters", "bear.png"), bfps)
    # Đầm Lầy Sương Mù
    small = lambda k: {"idle": (4, True), "move": (8, True), "attack": (10, False), "hurt": (1, False), "dead": (6, False)}[k]
    order = ["idle", "move", "attack", "hurt", "dead"]
    grid_sheet("toad", gen_swamp_creatures.build_toad(), order, 24, 24, (12, 22), os.path.join(ART, "Characters", "toad.png"), small)
    grid_sheet("leech", gen_swamp_creatures.build_leech(), order, 24, 24, (12, 22), os.path.join(ART, "Characters", "leech.png"), small)
    grid_sheet("mudling", gen_swamp_creatures.build_mudling(), order, 24, 24, (12, 22), os.path.join(ART, "Characters", "mudling.png"), small)
    mfps2 = lambda k: {"idle": (3, True), "move": (6, True), "attack": (8, False), "hurt": (1, False), "dead": (5, False)}[k]
    grid_sheet("mudman", gen_swamp_creatures.build_mudman(), order, 32, 32, (16, 30), os.path.join(ART, "Characters", "mudman.png"), mfps2)
    kfps = lambda k: {"idle": (3, True), "walk": (8, True), "windup": (5, False), "slam": (10, False), "air": (1, False),
                      "tongue": (10, False), "spit": (8, False), "roar": (6, True), "hurt": (1, False), "dead": (3, False)}[k]
    grid_sheet("toadking", gen_swamp_creatures.build_toad_king(),
               ["idle", "walk", "windup", "slam", "air", "tongue", "spit", "roar", "hurt", "dead"],
               48, 48, (24, 45), os.path.join(ART, "Characters", "toadking.png"), kfps)
    sfps = lambda k: {"idle": (3, True), "walk": (6, True), "windup": (6, False), "bite": (12, False), "tail": (8, False),
                      "spit": (8, False), "submerge": (6, False), "emerge": (6, False), "roar": (5, True), "hurt": (1, False),
                      "dead": (3, False)}[k]
    grid_sheet("snake", gen_swamp_creatures.build_snake(),
               ["idle", "walk", "windup", "bite", "tail", "spit", "submerge", "emerge", "roar", "hurt", "dead"],
               72, 64, (36, 60), os.path.join(ART, "Characters", "snake.png"), sfps)
    wfps = lambda k: {"idle": (4, True), "move": (8, True), "windup": (8, True), "attack": (10, False), "hidden": (3, True),
                      "hurt": (1, False), "dead": (5, False)}[k]
    grid_sheet("watersnake", gen_swamp_creatures.build_water_snake(), ["idle", "move", "windup", "attack", "hidden", "hurt", "dead"],
               32, 24, (16, 22), os.path.join(ART, "Characters", "watersnake.png"), wfps)
    dfps = lambda k: {"idle": (18, True), "move": (18, True), "windup": (10, True), "attack": (12, False), "hurt": (1, False),
                      "dead": (5, False)}[k]
    grid_sheet("dragonfly", gen_swamp_creatures.build_dragonfly(), ["idle", "move", "windup", "attack", "hurt", "dead"],
               24, 24, (12, 22), os.path.join(ART, "Characters", "dragonfly.png"), dfps)
    ofps = lambda k: {"idle": (7, True), "move": (7, True), "attack": (6, False), "hurt": (1, False), "dead": (9, False)}[k]
    grid_sheet("wisp", gen_swamp_creatures.build_wisp(), ["idle", "move", "attack", "hurt", "dead"],
               24, 32, (12, 30), os.path.join(ART, "Characters", "wisp.png"), ofps)
    # Hang Pha Lê
    cave_order = ["idle", "move", "windup", "attack", "hurt", "dead"]
    bfps = lambda k: {"idle": (12, True), "move": (14, True), "windup": (8, True), "attack": (10, True), "hurt": (1, False), "dead": (5, False)}[k]
    grid_sheet("bat", gen_cave_creatures.build_bat(), cave_order, 26, 22, (13, 20), os.path.join(ART, "Characters", "bat.png"), bfps)
    pfps = lambda k: {"idle": (3, True), "move": (10, True), "windup": (6, True), "attack": (8, False), "hurt": (1, False), "dead": (5, False)}[k]
    grid_sheet("spider", gen_cave_creatures.build_spider(), cave_order, 34, 24, (17, 22), os.path.join(ART, "Characters", "spider.png"), pfps)
    gfps = lambda k: {"idle": (2, True), "move": (6, True), "windup": (4, False), "attack": (8, False), "hurt": (1, False), "dead": (4, False)}[k]
    grid_sheet("golem", gen_cave_creatures.build_golem(), cave_order, 34, 34, (17, 32), os.path.join(ART, "Characters", "golem.png"), gfps)
    # the deeper cave
    efps = lambda k: {"idle": (3, True), "move": (10, True), "windup": (8, True), "attack": (10, False), "hurt": (1, False), "dead": (5, False)}[k]
    grid_sheet("beetle", gen_cave_deep.build_beetle(), cave_order, 34, 26, (17, 24), os.path.join(ART, "Characters", "beetle.png"), efps)
    slfps = lambda k: {"idle": (4, True), "move": (8, True), "attack": (10, False), "hurt": (1, False), "dead": (6, False)}[k]
    grid_sheet("crystalslime", gen_cave_deep.build_crystal_slime(), ["idle", "move", "attack", "hurt", "dead"], 24, 24, (12, 22),
               os.path.join(ART, "Characters", "crystalslime.png"), slfps)
    yfps = lambda k: {"idle": (1, True), "move": (1, True), "windup": (6, False), "attack": (10, True), "hurt": (1, False), "dead": (4, False)}[k]
    grid_sheet("caveeye", gen_cave_deep.build_cave_eye(), cave_order, 28, 28, (14, 24), os.path.join(ART, "Characters", "caveeye.png"), yfps)
    mmfps = lambda k: {"hidden": (1, True), "spring": (10, False), "idle": (4, True), "move": (9, True), "windup": (6, True),
                       "attack": (12, False), "burrow": (5, False), "emerge": (5, False), "hurt": (1, False), "dead": (4, False)}[k]
    grid_sheet("mimic", gen_cave_deep.build_mimic(), ["hidden", "spring", "idle", "move", "windup", "attack", "burrow", "emerge", "hurt", "dead"],
               30, 30, (15, 27), os.path.join(ART, "Characters", "mimic.png"), mmfps)
    ogfps = lambda k: {"idle": (2, True), "walk": (5, True), "windup": (4, False), "slam": (8, False), "spin": (16, True),
                       "cast": (4, True), "roar": (4, True), "hurt": (1, False), "dead": (3, False)}[k]
    grid_sheet("oldgolem", gen_cave_deep.build_old_golem(), ["idle", "walk", "windup", "slam", "spin", "cast", "roar", "hurt", "dead"],
               56, 56, (28, 53), os.path.join(ART, "Characters", "oldgolem.png"), ogfps)
    qfps = lambda k: {"idle": (3, True), "walk": (8, True), "windup": (6, False), "bite": (12, False), "cast": (6, True),
                      "climb": (6, False), "air": (1, False), "land": (1, False), "roar": (5, True), "hurt": (1, False), "dead": (3, False)}[k]
    grid_sheet("queen", gen_cave_deep.build_queen(), ["idle", "walk", "windup", "bite", "cast", "climb", "air", "land", "roar", "hurt", "dead"],
               80, 64, (40, 61), os.path.join(ART, "Characters", "queen.png"), qfps)


def simple_grid(items, fw, fh, path, ppu=16, filter_="point"):
    cols = min(8, len(items))
    rows = (len(items) + cols - 1) // cols
    sheet = Canvas(fw * cols, fh * rows)
    sprites = []
    for i, (n, cv) in enumerate(items):
        x, y = (i % cols) * fw, (i // cols) * fh
        sheet.blit(cv, x + (fw - cv.w) // 2, y + (fh - cv.h) // 2)
        sprites.append({"name": n, "x": x, "y": y, "w": fw, "h": fh, "pivot": (fw / 2, fh / 2)})
    img = sheet.to_image()
    save(img, path)
    add_texture(path, img.height, sprites, filter_, ppu)


def build_icons():
    simple_grid(gen_icons.build_items() + gen_swamp.icons() + gen_cave.icons() + gen_gear.icons(), 16, 16, os.path.join(ART, "Icons", "items.png"), ppu=16)
    simple_grid(gen_icons.build_skills() + gen_class_icons.build(), 24, 24, os.path.join(ART, "Icons", "skills.png"), ppu=16)
    simple_grid(gen_icons.build_status(), 10, 10, os.path.join(ART, "Icons", "status.png"), ppu=16)


def build_ui():
    items = gen_ui.build()
    borders = {n: b for (n, c, b) in items}
    packed = [(n, c, (c.w / 2, c.h / 2)) for (n, c, b) in items]
    sheet, rects = pack_shelf(packed, 256, pad=2)
    img = sheet.to_image()
    path = os.path.join(ART, "UI", "ui.png")
    save(img, path)
    add_texture(path, img.height, [{"name": n, "x": x, "y": y, "w": w, "h": h, "pivot": (px, py), "border": borders[n]}
                                   for (n, x, y, w, h, px, py) in rects], "point", 100 / 3, kind="ui")
    # hardware cursors (3x, on a 32x32 canvas); configured by the Unity importer as Cursor textures
    for name, cv in (("cursor", gen_ui.cursor()), ("cursor_attack", gen_ui.cursor_attack())):
        big = cv.to_image().resize((cv.w * 3, cv.h * 3), Image.NEAREST)
        canvas = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        canvas.alpha_composite(big, (0, 0))
        save(canvas, os.path.join(ART, "UI", f"{name}.png"))


def build_vfx():
    smooth, singles, books = gen_vfx.build()
    for n, im in smooth.items():
        path = os.path.join(ART, "VFX", "Smooth", f"{n}.png")
        save(im, path)
        pv = (im.width / 2, im.height / 2)
        if n == "tele_cone":
            pv = (im.width / 2, im.height)  # apex
        add_texture(path, im.height, [{"name": n, "x": 0, "y": 0, "w": im.width, "h": im.height, "pivot": pv}],
                    "bilinear", 64 if n not in ("shadow",) else 16)
    # pixel singles: one texture each (particles need a whole-texture UV)
    for n, cv in singles:
        path = os.path.join(ART, "VFX", "Pixel", "Singles", f"{n}.png")
        img = cv.to_image()
        save(img, path)
        add_texture(path, img.height, [{"name": n, "x": 0, "y": 0, "w": cv.w, "h": cv.h, "pivot": (cv.w / 2, cv.h / 2)}],
                    "point", 16)
    # flipbooks: one sheet per effect (single row)
    fps = {"slash": 24, "claw": 18, "fire": 14, "smoke_px": 10, "dust": 12, "fireball": 12,
           "ice_spike": 14, "bolt_hit": 18, "hit": 24, "explosion": 16, "poison": 8}
    for n, frames in books.items():
        fw = max(f.w for f in frames)
        fh = max(f.h for f in frames)
        sheet = Canvas(fw * len(frames), fh)
        sprites = []
        pivot = (fw / 2, fh / 2)
        if n == "ice_spike":
            pivot = (fw / 2, fh - 3)
        for i, f in enumerate(frames):
            sheet.blit(f, i * fw, 0)
            sprites.append({"name": f"{n}_{i}", "x": i * fw, "y": 0, "w": fw, "h": fh, "pivot": pivot})
        img = sheet.to_image()
        path = os.path.join(ART, "VFX", "Pixel", f"fx_{n}.png")
        save(img, path)
        add_texture(path, img.height, sprites, "point", 16)
        manifest["anims"].append({"set": "vfx", "name": n, "frames": [s["name"] for s in sprites],
                                  "fps": fps.get(n, 12), "loop": n in ("fireball",)})


def main():
    build_terrain()
    build_props()
    build_chars()
    build_icons()
    build_ui()
    build_vfx()
    os.makedirs(ART, exist_ok=True)
    with open(os.path.join(ART, "art_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    n = sum(len(t["sprites"]) for t in manifest["textures"])
    print(f"textures: {len(manifest['textures'])}, sprites: {n}, anims: {len(manifest['anims'])}")


if __name__ == "__main__":
    main()
