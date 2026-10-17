"""Dresses Hang Gấu's forest from the environment packs in the project (added from Fab with Add to
Project, e.g. Paragon: Agora and Monolith Environment), editor Python:

    Tools\\Unreal\\ue.cmd forest

Every static mesh outside the game's own folders is sorted by its name into trees, bushes, rocks
and grass (WORDS), kept only if its size suits the kind (SIZES), and named in Config/
DefaultGame.ini for ARttForest, with a scale per kind that brings the kind's middle size to the
size Dauntless draws it at (TARGET). A hero's or monster's pack (one with Characters/Heroes)
only stands in until an environment pack brings its own: so far, Rampage's rocks. If the pack has ground textures (grass, earth, rock: a colour map and a normal
map each), MI_Ground paints the terrain with them. Everything found is listed in the log.
"""
import io
import os
import re

import unreal

SECTION = "[/Script/RungThiTham.RttForest]"
OURS = ("/Game/Maps", "/Game/Materials", "/Game/Creatures", "/Game/Environment", "/Game/Data", "/Game/Developers",
        "/Game/Collections", "/Game/Textures")
ENVIRONMENT = "/Game/Environment/HangGau"
# kind -> (words in the name, words that rule it out)
WORDS = {
    "Tree": (("tree", "pine", "oak", "birch", "beech", "willow", "poplar", "fir", "spruce", "maple", "conifer", "cedar",
              "redwood", "cypress", "trunk"),
             ("branch", "leaf", "leaves", "log", "stump", "root", "billboard", "impostor", "card", "street", "treeline")),
    "Bush": (("bush", "shrub", "fern", "hedge", "plant", "ivy", "sapling"), ("billboard", "card_")),
    "Rock": (("rock", "boulder", "stone", "pebble"), ("shard", "chunk", "wall", "path", "brick", "tile", "column", "statue", "crater")),
    "Grass": (("grass", "clover", "weed", "flower", "reed", "tuft", "daisy", "dandelion"), ("billboard",)),
}
# kind -> (smallest, largest) height in cm, as the pack made it, worth planting
SIZES = {"Tree": (350.0, 6000.0), "Bush": (40.0, 600.0), "Rock": (40.0, 2500.0), "Grass": (5.0, 250.0)}
# kind -> the height (cm) the kind's middle mesh is drawn at: Dauntless's trees tower over a
# slayer, its rocks are chest-high to house-high, its grass reaches the knee
TARGET = {"Tree": 1800.0, "Bush": 180.0, "Rock": 220.0, "Grass": 55.0}



def words_of(name):
    """SM_Plains_TreeLarge_01 -> sm, plains, tree, large, 01 (so Greystone is not a stone)."""
    return [w.lower() for w in re.findall(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+", name)]


def effect(package):
    """The Paragon packs' ability effects (auras, shockwaves); their rock debris is real rock."""
    return "/FX/" in package and "/Debris/" not in package


registry = unreal.AssetRegistryHelpers.get_asset_registry()
registry.scan_paths_synchronous(["/Game"], True)
found = {k: [] for k in WORDS}
textures = []
hero_packs = set()
for a in registry.get_assets_by_path("/Game", recursive=True):
    package = str(a.package_name)
    if package.startswith(OURS):
        continue
    if "/Characters/Heroes/" in package:
        hero_packs.add(package.split("/")[2])
    cls = str(a.asset_class_path.asset_name)
    name = str(a.asset_name)
    low = name.lower()
    if cls == "Texture2D":
        textures.append((package, name))
        continue
    if cls != "StaticMesh" or effect(package):
        continue
    parts = words_of(name)
    for kind, (words, nots) in WORDS.items():
        if any(p.startswith(w) for p in parts for w in words) and not any(p.startswith(w) for p in parts for w in nots):
            found[kind].append((package, name))
            break

mel = unreal.MaterialEditingLibrary


def unfit_material(mesh):
    """Why the mesh would draw the engine's checker, if it would: a slot with no material or only
    the engine's own placeholder, or a texture parameter left empty (Rampage's M_Rock_To_Throw)."""
    for slot in mesh.get_editor_property("static_materials"):
        mat = slot.get_editor_property("material_interface")
        if mat is None:
            return "none"
        base = mat.get_base_material()
        if base.get_path_name().startswith("/Engine/"):
            return base.get_name()
        for p in mel.get_texture_parameter_names(base):
            if isinstance(mat, unreal.MaterialInstanceConstant):
                value = mel.get_material_instance_texture_parameter_value(mat, p)
            else:
                value = mel.get_material_default_texture_parameter_value(base, p)
            if value is None:
                return "%s.%s with no texture" % (base.get_name(), p)
    return None


def allow_instancing(mesh):
    """ARttForest draws by instances: a material not flagged for them draws the engine's checker.
    Only the packs' own materials are flagged and saved, never the engine's."""
    for slot in mesh.get_editor_property("static_materials"):
        mat = slot.get_editor_property("material_interface")
        base = mat.get_base_material() if mat else None
        if base and base.get_path_name().startswith("/Game/") and not base.get_editor_property("used_with_instanced_static_meshes"):
            base.set_editor_property("used_with_instanced_static_meshes", True)
            mel.recompile_material(base)
            unreal.EditorAssetLibrary.save_loaded_asset(base)
            unreal.log("RTT_FOREST %s may now be drawn by instances" % base.get_path_name())


kinds = {}
for kind, items in found.items():
    lo, hi = SIZES[kind]
    keep = []
    seen = set()
    for package, name in sorted(items):
        if name in seen:
            continue
        mesh = unreal.load_asset(package + "." + name)
        if mesh is None:
            continue
        broken = unfit_material(mesh)
        if broken:
            unreal.log("RTT_FOREST %s %s left out: its material is %s" % (kind, package, broken))
            continue
        height = mesh.get_bounds().box_extent.z * 2.0
        if lo <= height <= hi:
            keep.append((package, name, height))
            seen.add(name)
        unreal.log("RTT_FOREST %s %s %.0f cm %s" % (kind, package, height, "kept" if lo <= height <= hi else "too big or small"))
    # an environment pack's own beat the stand-ins from a hero's or monster's pack
    own = [k for k in keep if k[0].split("/")[2] not in hero_packs]
    kinds[kind] = own if own else keep
    for package, name, _ in kinds[kind]:
        allow_instancing(unreal.load_asset(package + "." + name))

lines = [SECTION, "; the forest's meshes, found by Tools/Unreal/setup_forest.py"]
for kind in ("Tree", "Bush", "Rock", "Grass"):
    items = kinds[kind]
    for package, name, _ in items:
        lines.append("+%sMeshes=%s.%s" % (kind, package, name))
    if items:
        heights = sorted(h for _, _, h in items)
        middle = heights[len(heights) // 2]
        scale = round(max(0.25, min(4.0, TARGET[kind] / max(middle, 1.0))), 2)
    else:
        scale = 1.0
    lines.append("%sScale=%.2f" % (kind, scale))
    unreal.log("RTT_FOREST %d %s kinds, drawn x%.2f: %s" % (len(items), kind.lower(), scale, ", ".join(n for _, n, _ in items)))

ini = os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_config_dir()), "DefaultGame.ini")
with io.open(ini, encoding="utf-8") as fh:
    text = fh.read()
if SECTION in text:
    start = text.index(SECTION)
    end = text.find("\n[", start + 1)
    text = text[:start] + text[end + 1:] if end >= 0 else text[:start]
text = text.rstrip("\n") + "\n\n" + "\n".join(lines) + "\n"
with io.open(ini, "w", encoding="utf-8", newline="\n") as fh:
    fh.write(text)


# the ground's textures: a colour and a normal map for each layer, by name
LAYERS = {"Grass": ("grass", "meadow", "moss"), "Earth": ("dirt", "soil", "mud", "earth", "ground", "path"),
          "Rock": ("rock", "cliff", "stone")}
COLOUR = ("_d", "_diffuse", "_basecolor", "_base_color", "_albedo", "_col", "_color")
NORMAL = ("_n", "_normal", "_nrm", "_norm")


def ground_texture(words, ends):
    for package, name in sorted(textures):
        low = name.lower()
        if any(w in low for w in words) and low.endswith(ends) and package.split("/")[2] not in hero_packs:
            return package + "." + name
    return None


picked = {}
for layer, words in LAYERS.items():
    picked[layer + "Colour"] = ground_texture(words, COLOUR)
    picked[layer + "Normal"] = ground_texture(words, NORMAL)
    unreal.log("RTT_FOREST ground %s: %s, %s" % (layer.lower(), picked[layer + "Colour"], picked[layer + "Normal"]))

if all(picked[layer + "Colour"] for layer in LAYERS):
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    mi_path = ENVIRONMENT + "/MI_Ground"
    if unreal.EditorAssetLibrary.does_asset_exist(mi_path):
        unreal.EditorAssetLibrary.delete_asset(mi_path)
    mi = tools.create_asset("MI_Ground", ENVIRONMENT, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    mel = unreal.MaterialEditingLibrary
    mel.set_material_instance_parent(mi, unreal.load_asset("/Game/Materials/M_Ground"))
    for param, tex in picked.items():
        if tex:
            mel.set_material_instance_texture_parameter_value(mi, param, unreal.load_asset(tex))
    mel.set_material_instance_scalar_parameter_value(mi, "UseTextures", 1.0)
    # the pack's own colours, kept a little warm and mottled
    for param, colour in (("GrassTint", (1.0, 1.0, 0.92)), ("GrassShade", (0.62, 0.68, 0.58)), ("EarthTint", (1.0, 0.96, 0.9)),
                          ("RockTint", (1.0, 1.0, 1.0))):
        mel.set_material_instance_vector_parameter_value(mi, param, unreal.LinearColor(colour[0], colour[1], colour[2], 1.0))
    unreal.EditorAssetLibrary.save_loaded_asset(mi)
    for a in registry.get_assets_by_path(ENVIRONMENT, recursive=True):
        if str(a.asset_name) == "HangGau_Ground" and str(a.asset_class_path.asset_name) == "StaticMesh":
            ground = a.get_asset()
            ground.set_material(0, mi)
            unreal.EditorAssetLibrary.save_loaded_asset(ground)
            unreal.log("RTT_FOREST the ground is painted with MI_Ground")
else:
    unreal.log("RTT_FOREST no ground textures in the packs: the ground keeps M_Ground's own tints")

for line in lines[2:]:
    unreal.log("RTT_FOREST " + line)
print("RTT_FOREST_OK")
