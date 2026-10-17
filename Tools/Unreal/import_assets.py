"""Brings the game's own art into the Unreal project (editor Python):

    Tools\\Unreal\\ue.cmd assets

- the materials the code sets up at runtime: M_VertexColour (the sculpts' painted vertex colours,
  with a Hurt flash), M_Glow (eyes and teal marks: Color x Strength) and M_Telegraph (the ground
  warnings before a blow: unlit, see-through, Color and Opacity);
- Gấu Ma Rừng Già, the Godot port's sculpted bear (Godot/assets/creatures/gau_ma.glb, from
  Tools/Sculpt/bear.py): one static mesh per part, in bear units, in /Game/Creatures/GauMa.

It prints each part's bounds (to check the glTF axes against the Godot ones) and the Paragon hero's
attack montages (their length and slot).
"""
import os

import unreal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GLB = os.path.join(ROOT, "Godot", "assets", "creatures", "gau_ma.glb")
MATERIALS = "/Game/Materials"
BEAR = "/Game/Creatures/GauMa"

assets = unreal.EditorAssetLibrary
edit = unreal.MaterialEditingLibrary
tools = unreal.AssetToolsHelpers.get_asset_tools()


def node(material, cls, x, y, **props):
    e = edit.create_material_expression(material, cls, x, y)
    for k, v in props.items():
        e.set_editor_property(k, v)
    return e


def material(name):
    # ue.cmd deletes the old file first: rebuilding a material in place trips an editor assert
    return tools.create_asset(name, MATERIALS, unreal.Material, unreal.MaterialFactoryNew())


def finish(m):
    edit.recompile_material(m)
    assets.save_loaded_asset(m)
    return m


def vertex_colour():
    m = material("M_VertexColour")
    vc = node(m, unreal.MaterialExpressionVertexColor, -800, 0)
    # the glTF's colours arrive gamma-encoded in the 8-bit vertex colours, which shaders read as
    # linear: undo the gamma, or the fur comes out pale
    gamma = node(m, unreal.MaterialExpressionPower, -550, 0, const_exponent=2.2)
    edit.connect_material_expressions(vc, "", gamma, "Base")
    edit.connect_material_property(gamma, "", unreal.MaterialProperty.MP_BASE_COLOR)
    hurt = node(m, unreal.MaterialExpressionScalarParameter, -600, 200, parameter_name="Hurt", default_value=0.0)
    white = node(m, unreal.MaterialExpressionConstant3Vector, -600, 320, constant=unreal.LinearColor(1.6, 1.5, 1.4, 1))
    glow = node(m, unreal.MaterialExpressionMultiply, -350, 250)
    edit.connect_material_expressions(hurt, "", glow, "A")
    edit.connect_material_expressions(white, "", glow, "B")
    edit.connect_material_property(glow, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    rough = node(m, unreal.MaterialExpressionConstant, -350, 400, r=0.85)
    edit.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    return finish(m)


def glow():
    m = material("M_Glow")
    colour = node(m, unreal.MaterialExpressionVectorParameter, -600, 0, parameter_name="Color",
                  default_value=unreal.LinearColor(0.6, 1.0, 0.8, 1))
    strength = node(m, unreal.MaterialExpressionScalarParameter, -600, 200, parameter_name="Strength", default_value=4.0)
    lit = node(m, unreal.MaterialExpressionMultiply, -350, 100)
    edit.connect_material_expressions(colour, "", lit, "A")
    edit.connect_material_expressions(strength, "", lit, "B")
    edit.connect_material_property(lit, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    edit.connect_material_property(colour, "", unreal.MaterialProperty.MP_BASE_COLOR)
    return finish(m)


def telegraph():
    m = material("M_Telegraph")
    m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_TRANSLUCENT)
    m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
    colour = node(m, unreal.MaterialExpressionVectorParameter, -600, 0, parameter_name="Color",
                  default_value=unreal.LinearColor(1.0, 0.25, 0.1, 1))
    bright = node(m, unreal.MaterialExpressionMultiply, -350, 0, const_b=2.0)
    edit.connect_material_expressions(colour, "", bright, "A")
    edit.connect_material_property(bright, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    opacity = node(m, unreal.MaterialExpressionScalarParameter, -600, 200, parameter_name="Opacity", default_value=0.3)
    edit.connect_material_property(opacity, "", unreal.MaterialProperty.MP_OPACITY)
    return finish(m)


vc = vertex_colour()
glow_mat = glow()
telegraph()

task = unreal.AssetImportTask()
task.filename = GLB
task.destination_path = BEAR
task.automated = True
task.replace_existing = True
task.save = True
tools.import_asset_tasks([task])
unreal.log("RTT_ASSETS imported: " + ", ".join(str(p) for p in task.imported_object_paths))

registry = unreal.AssetRegistryHelpers.get_asset_registry()
registry.scan_paths_synchronous([BEAR], True)
for a in registry.get_assets_by_path(BEAR, recursive=True):
    obj = a.get_asset()
    if isinstance(obj, unreal.StaticMesh):
        box = obj.get_bounding_box()
        name = str(a.asset_name)
        mat = glow_mat if "Glow" in name else vc
        for i in range(len(obj.static_materials)):
            obj.set_material(i, mat)
        assets.save_loaded_asset(obj)
        unreal.log("RTT_ASSETS mesh %s %s min %s max %s" % (name, a.package_name, box.min, box.max))
    else:
        unreal.log("RTT_ASSETS other %s %s" % (a.asset_class_path.asset_name, a.package_name))

hero = "/Game/ParagonGreystone/Characters/Heroes/Greystone/Animations"
for a in registry.get_assets_by_path(hero, recursive=True):
    name = str(a.asset_name)
    if not name.startswith(("Attack_", "HitReact", "Death", "Jump_Melee")):
        continue
    obj = a.get_asset()
    slot = ""
    if isinstance(obj, unreal.AnimMontage):
        tracks = obj.get_editor_property("slot_anim_tracks")
        slot = ",".join(str(t.get_editor_property("slot_name")) for t in tracks)
    notes = [str(n) for n in unreal.AnimationLibrary.get_animation_notify_event_names(obj)] if isinstance(obj, unreal.AnimSequenceBase) else []
    unreal.log("RTT_ASSETS anim %s %s %.2fs slot=%s notifies=%s" % (a.asset_class_path.asset_name, name, obj.get_play_length(), slot, notes))
print("RTT_ASSETS_OK")
