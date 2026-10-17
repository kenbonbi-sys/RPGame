"""Finds a Paragon monster in the project (added from Fab with Add to Project) and names it in
Config/DefaultGame.ini as the colossus body ARttBehemoth reads (editor Python):

    Tools\\Unreal\\ue.cmd beast [Pack]        (default ParagonRampage)

From the pack it takes the mesh named after the hero, its animation blueprint, the capsule of its
own character blueprint, a size that makes it about 9 m tall, and an animation for each move:
it prefers the names in MOVES (the first that exists), else any whose name holds one of the words.
Everything it found is listed in the log, to check and to tune MOVES by.
"""
import io
import os
import sys

import unreal

SECTION = "[/Script/RungThiTham.RttBehemoth]"
HEIGHT_CM = 900.0
# move -> (preferred animation names, fallback words); the strike's place in the animation (0..1)
MOVES = {
    "Swipe": (["Attack_PrimaryA", "Attack_A_Slow", "Attack_A_Med", "Attack_A_Fast"], ["primary", "attack"], 0.4),
    "Slam": (["Ability_E", "Ability_Slam", "Ability_GroundSlam", "Attack_PrimaryC"], ["slam", "ground", "pound"], 0.45),
    "Throw": (["Ability_Q", "Ability_Throw", "Ability_RipAndToss"], ["throw", "toss", "rip"], 0.5),
    "Charge": (["Ability_R", "Ability_Charge", "Ability_Ultimate"], ["charge", "ultimate"], 0.6),
    "Roar": (["Emote_Roar", "Ability_Enrage", "LevelStart", "Emote_Taunt"], ["roar", "enrage", "taunt", "emote"], 0.5),
    "Stun": (["Stun_Idle", "Stun_Loop", "Stun_Start"], ["stun"], 0.5),
    "Flinch": (["HitReact_Front", "HitReact_Back"], ["hitreact"], 0.5),
    "Death": (["Death", "Death_A"], ["death"], 0.5),
}

pack_name = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else "ParagonRampage"
pack = "/Game/" + pack_name
hero = pack_name[len("Paragon"):] if pack_name.startswith("Paragon") else pack_name

registry = unreal.AssetRegistryHelpers.get_asset_registry()
registry.scan_paths_synchronous([pack], True)
items = [(str(a.asset_class_path.asset_name), str(a.package_name), str(a.asset_name)) for a in registry.get_assets_by_path(pack, recursive=True)]
if not items:
    raise RuntimeError("nothing in " + pack + ": add the Paragon pack from the launcher's Fab Library (Add to Project)")


def path(p, n):
    return p + "." + n


meshes = [(p, n) for c, p, n in items if c == "SkeletalMesh"]
anim_bps = [(p, n) for c, p, n in items if c == "AnimBlueprint"]
anims = {n: (p, n, c) for c, p, n in items if c in ("AnimSequence", "AnimMontage")}
blueprints = [(p, n) for c, p, n in items if c == "Blueprint"]
rocks = [(p, n) for c, p, n in items if c == "StaticMesh" and any(w in n.lower() for w in ("rock", "boulder", "stone"))]

mesh = sorted(meshes, key=lambda pn: (0 if pn[1].lower() == hero.lower() else 1, len(pn[1])))[0]
anim_bp = sorted(anim_bps, key=lambda pn: (0 if "animblueprint" in pn[1].lower() else 1, len(pn[1])))[0] if anim_bps else None

# its own character blueprint: capsule size
radius, half = 55.0, 105.0
for p, n in blueprints:
    if "character" not in n.lower():
        continue
    cls = unreal.EditorAssetLibrary.load_blueprint_class(p)
    if not cls:
        continue
    cdo = unreal.get_default_object(cls)
    try:
        cap = cdo.get_editor_property("capsule_component")
        radius, half = cap.get_unscaled_capsule_radius(), cap.get_unscaled_capsule_half_height()
        unreal.log("RTT_BEAST capsule from %s: %.0f x %.0f" % (n, radius, half))
        break
    except Exception as e:
        unreal.log("RTT_BEAST no capsule in %s: %s" % (n, e))

loaded = unreal.EditorAssetLibrary.load_asset(path(*mesh))
height = loaded.get_bounds().box_extent.z * 2.0 if loaded else 200.0
scale = round(HEIGHT_CM / max(height, 1.0), 2)

slots = {}
for name, (p, n, c) in anims.items():
    if c == "AnimMontage":
        m = unreal.EditorAssetLibrary.load_asset(path(p, n))
        for t in m.get_editor_property("slot_anim_tracks"):
            slot = str(t.get_editor_property("slot_name"))
            slots[slot] = slots.get(slot, 0) + 1
slot = max(slots, key=slots.get) if slots else "DefaultSlot"


def pick(move):
    names, words, strike = MOVES[move]
    for n in names:
        if n in anims:
            return anims[n], strike
    for n in sorted(anims):
        if any(w in n.lower() for w in words) and anims[n][2] == "AnimSequence" and not n.endswith("_MSA"):
            return anims[n], strike
    return None, strike


lines = [SECTION,
         "; the colossus body, found by Tools/Unreal/setup_beast.py (%s, %.0f cm tall, x%.2f)" % (pack_name, height, scale),
         "BodyMesh=" + path(*mesh),
         "BodyAnimClass=" + (path(*anim_bp) + "_C" if anim_bp else ""),
         "BodyScale=%.2f" % scale,
         "CapsuleRadius=%.1f" % radius,
         "CapsuleHalfHeight=%.1f" % half,
         "MoveSlot=" + slot,
         "RockMesh=" + (path(*rocks[0]) if rocks else "")]
for move in MOVES:
    found, strike = pick(move)
    lines.append("%sAnimation=%s" % (move, path(found[0], found[1]) if found else ""))
    unreal.log("RTT_BEAST %s -> %s" % (move, found[1] if found else "(none)"))
lines += ["SwipeStrike=%.2f" % MOVES["Swipe"][2], "SlamStrike=%.2f" % MOVES["Slam"][2], "ThrowRelease=%.2f" % MOVES["Throw"][2]]

unreal.log("RTT_BEAST meshes: " + ", ".join(n for _, n in meshes))
unreal.log("RTT_BEAST animation blueprints: " + ", ".join(n for _, n in anim_bps))
unreal.log("RTT_BEAST blueprints: " + ", ".join(n for _, n in blueprints))
unreal.log("RTT_BEAST rocks: " + ", ".join(n for _, n in rocks))
unreal.log("RTT_BEAST slots: %s" % slots)
for n in sorted(anims):
    p, _, c = anims[n]
    a = unreal.EditorAssetLibrary.load_asset(path(p, n))
    notes = [str(x) for x in unreal.AnimationLibrary.get_animation_notify_event_names(a)]
    unreal.log("RTT_BEAST anim %s %s %.2fs %s" % (c, n, a.get_play_length(), notes))

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
    unreal.log("RTT_BEAST " + line)
print("RTT_BEAST_OK " + pack_name)
