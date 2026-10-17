"""Finds the Paragon hero in the project (added from Fab with the launcher's Add to Project) and
names it in Config/DefaultGame.ini, where ARttHero reads its body from (editor Python):

    Tools\\Unreal\\ue.cmd hero [Name]

Takes the hero given, else the first found of PREFERRED (Greystone: the big blade, the nearest to a
Dauntless slayer), else any Paragon hero. Of its pack it takes the mesh named after the hero, its
animation blueprint and a forward dodge or roll for Lướt, and lists the rest in the log.
"""
import io
import os
import sys

import unreal

PREFERRED = ["Greystone", "Kwang", "FengMao", "Shinbi", "Aurora", "Countess", "Kallari", "Crunch", "Twinblast",
             "Sparrow", "Revenant", "Murdock", "Serath", "Terra", "Phase", "Iggy", "Grux", "Rampage"]
SECTION = "[/Script/RungThiTham.RttHero]"

registry = unreal.AssetRegistryHelpers.get_asset_registry()
registry.scan_paths_synchronous(["/Game"], True)


def assets_under(path):
    out = []
    for a in registry.get_assets_by_path(path, recursive=True):
        out.append((str(a.asset_class_path.asset_name), str(a.package_name), str(a.asset_name)))
    return out


def object_path(package, name):
    return package + "." + name


packs = sorted({p.split("/")[2] for _, p, _ in assets_under("/Game") if p.startswith("/Game/Paragon")})
heroes = {pack[len("Paragon"):]: pack for pack in packs if pack.startswith("Paragon") and len(pack) > len("Paragon")}
unreal.log("RTT_HERO packs: " + ", ".join(packs))
if not heroes:
    raise RuntimeError("no Paragon hero in Content: add one from the launcher's Fab Library (Add to Project)")

wanted = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else ""
order = ([wanted] if wanted else []) + PREFERRED + sorted(heroes)
hero = next(h for h in order if h in heroes)
pack = "/Game/" + heroes[hero]
items = assets_under(pack)

meshes = [(p, n) for c, p, n in items if c == "SkeletalMesh"]
anims = [(p, n) for c, p, n in items if c == "AnimBlueprint"]
sequences = [(p, n) for c, p, n in items if c in ("AnimSequence", "AnimMontage")]


def pick(found, score):
    ranked = sorted(found, key=lambda pn: (score(pn[1]), len(pn[1]), pn[1]))
    return ranked[0] if ranked else None


mesh = pick(meshes, lambda n: 0 if n.lower() == hero.lower() else (1 if hero.lower() in n.lower() else 2))
anim = pick(anims, lambda n: 0 if "animblueprint" in n.lower() or "animbp" in n.lower() else 1)
dodge = pick([s for s in sequences if any(w in s[1].lower() for w in ("dodge", "roll", "evade"))],
             lambda n: 0 if any(w in n.lower() for w in ("fwd", "forward", "_f")) else 1)
if mesh is None:
    raise RuntimeError("no skeletal mesh in " + pack)

height = 0.0
loaded = unreal.EditorAssetLibrary.load_asset(object_path(*mesh))
if loaded:
    bounds = loaded.get_bounds()
    height = bounds.box_extent.z * 2.0

unreal.log("RTT_HERO hero: %s (%s), %d meshes, %d animation blueprints, %d animations" % (hero, pack, len(meshes), len(anims), len(sequences)))
unreal.log("RTT_HERO meshes: " + ", ".join(n for _, n in meshes))
unreal.log("RTT_HERO animation blueprints: " + ", ".join(n for _, n in anims))
unreal.log("RTT_HERO animations: " + ", ".join(n for _, n in sorted(sequences, key=lambda s: s[1])))
unreal.log("RTT_HERO height: %.0f cm" % height)

lines = [SECTION,
         "; the Paragon hero, found by Tools/Unreal/setup_hero.py (%s, %.0f cm tall)" % (hero, height),
         "HeroMesh=" + object_path(*mesh),
         "HeroAnimClass=" + (object_path(*anim) + "_C" if anim else ""),
         "DodgeAnimation=" + (object_path(*dodge) if dodge else ""),
         "HeroScale=1.0"]

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
for line in lines[2:]:
    unreal.log("RTT_HERO " + line)
print("RTT_HERO_OK " + hero)
