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


TEXTURES = "/Game/Creatures/Textures"
SOURCE_ART = os.path.join(ROOT, "Unreal", "SourceArt", "Creatures")


def import_textures():
    """The detail textures from make_textures.py: two normal maps and a mask."""
    out = {}
    for name, normal in (("T_FibreNormal", True), ("T_LumpNormal", True), ("T_CreatureDetail", False)):
        task = unreal.AssetImportTask()
        task.filename = os.path.join(SOURCE_ART, name + ".png")
        task.destination_path = TEXTURES
        task.automated = True
        task.replace_existing = True
        task.save = False
        tools.import_asset_tasks([task])
        tex = assets.load_asset(TEXTURES + "/" + name)
        tex.set_editor_property("srgb", False)
        tex.set_editor_property("compression_settings",
                                unreal.TextureCompressionSettings.TC_NORMALMAP if normal else unreal.TextureCompressionSettings.TC_MASKS)
        assets.save_loaded_asset(tex)
        out[name] = tex
    return out


def custom(m, x, y, code, inputs, output=unreal.CustomMaterialOutputType.CMOT_FLOAT3):
    c = node(m, unreal.MaterialExpressionCustom, x, y)
    c.set_editor_property("code", code)
    c.set_editor_property("output_type", output)
    pins = []
    for name in inputs:
        pin = unreal.CustomInput()
        pin.set_editor_property("input_name", name)
        pins.append(pin)
    c.set_editor_property("inputs", pins)
    return c


def wire(m, pairs):
    for src, dst, pin in pairs:
        edit.connect_material_expressions(src, "", dst, pin)


def creature(textures):
    """The sculpted creatures' skin, as finely detailed as a Paragon hero's: their painted vertex
    colours (gamma undone), calmed toward Paragon's palette; fur strands and bark fibres, or moss
    lumps where the paint is green, projected on three planes in the part's own space (the sculpts
    have no UVs) into a real normal; rougher moss, smoother bone; a soft fur sheen at the rims; the
    Hurt flash."""
    m = material("M_Creature")
    m.set_editor_property("tangent_space_normal", False)
    vc = node(m, unreal.MaterialExpressionVertexColor, -1400, -200)
    if hasattr(unreal, "MaterialExpressionLocalPosition"):
        pos = node(m, unreal.MaterialExpressionLocalPosition, -1400, 0)
    else:
        world = node(m, unreal.MaterialExpressionWorldPosition, -1600, 0)
        pos = node(m, unreal.MaterialExpressionTransformPosition, -1400, 0,
                   transform_source_type=unreal.MaterialPositionTransformSource.TRANSFORMPOSSOURCE_WORLD,
                   transform_type=unreal.MaterialPositionTransformSource.TRANSFORMPOSSOURCE_LOCAL)
        edit.connect_material_expressions(world, "", pos, "")
    normal_ws = node(m, unreal.MaterialExpressionVertexNormalWS, -1600, 200)
    normal_ls = node(m, unreal.MaterialExpressionTransform, -1400, 200,
                     transform_source_type=unreal.MaterialVectorCoordTransformSource.TRANSFORMSOURCE_WORLD,
                     transform_type=unreal.MaterialVectorCoordTransform.TRANSFORM_LOCAL)
    edit.connect_material_expressions(normal_ws, "", normal_ls, "")
    tile = node(m, unreal.MaterialExpressionScalarParameter, -1400, 350, parameter_name="Tile", default_value=70.0)
    strength = node(m, unreal.MaterialExpressionScalarParameter, -1400, 450, parameter_name="DetailStrength", default_value=0.55)
    fibre = node(m, unreal.MaterialExpressionTextureObject, -1400, 550, texture=textures["T_FibreNormal"])
    lump = node(m, unreal.MaterialExpressionTextureObject, -1400, 700, texture=textures["T_LumpNormal"])
    detail_tex = node(m, unreal.MaterialExpressionTextureObject, -1400, 850, texture=textures["T_CreatureDetail"])

    # masks: x moss (green paint), y bone (pale, unsaturated paint), z brightness
    masks = custom(m, -1100, -200, """
float3 c = pow(saturate(VC), 2.2);
float hi = max(c.r, max(c.g, c.b));
float sat = (hi - min(c.r, min(c.g, c.b))) / max(hi, 0.001);
float lum = dot(c, float3(0.3, 0.59, 0.11));
float moss = saturate((c.g - c.r) * 8.0 + 0.15) * saturate(sat * 3.0);
float bone = saturate((lum - 0.32) * 6.0) * saturate(1.0 - sat * 2.2);
return float3(moss, bone, lum);""", ["VC"])
    # the detail mask on three planes: x fibre gaps, y broad blotches, z lumps
    detail = custom(m, -1100, 100, """
float3 w = pow(abs(normalize(N)), 4.0); w /= (w.x + w.y + w.z);
float3 a = Texture2DSample(T, TSampler, P.yz / Tile).rgb;
float3 b = Texture2DSample(T, TSampler, P.xz / Tile).rgb;
float3 c = Texture2DSample(T, TSampler, P.xy / Tile).rgb;
return a * w.x + b * w.y + c * w.z;""", ["P", "N", "T", "Tile"])
    colour = custom(m, -800, -200, """
float3 c = pow(saturate(VC), 2.2);
// calm the moss toward olive, and the whole palette a touch, as Paragon's
c = lerp(c, c * float3(0.78, 0.8, 0.55), M.x);
float l = dot(c, float3(0.3, 0.59, 0.11));
c = lerp(float3(l, l, l), c, 0.85) * 0.9;
// painted variation, dark gaps between strands (not on bone), lumpy moss
c *= lerp(0.82, 1.12, D.y);
c *= lerp(1.0, lerp(0.62, 1.04, D.x), 1.0 - M.y);
c *= lerp(1.0, lerp(0.75, 1.08, D.z), M.x);
return c;""", ["VC", "M", "D"])
    rough = custom(m, -800, 0, """
float r = lerp(0.74, 0.93, M.x);
r = lerp(r, 0.42, M.y);
return saturate(r + (D.y - 0.5) * 0.12 + (1.0 - D.x) * 0.06);""", ["M", "D"], unreal.CustomMaterialOutputType.CMOT_FLOAT1)
    normal = custom(m, -800, 300, """
float3 n = normalize(N);
float3 w = pow(abs(n), 4.0); w /= (w.x + w.y + w.z);
float2 a = Texture2DSample(F, FSampler, P.yz / Tile).xy * 2.0 - 1.0;
float2 b = Texture2DSample(F, FSampler, P.xz / Tile).xy * 2.0 - 1.0;
float2 c = Texture2DSample(F, FSampler, P.xy / Tile).xy * 2.0 - 1.0;
float2 la = Texture2DSample(L, LSampler, P.yz / (Tile * 0.6)).xy * 2.0 - 1.0;
float2 lb = Texture2DSample(L, LSampler, P.xz / (Tile * 0.6)).xy * 2.0 - 1.0;
float2 lc = Texture2DSample(L, LSampler, P.xy / (Tile * 0.6)).xy * 2.0 - 1.0;
a = lerp(a, la, M.x); b = lerp(b, lb, M.x); c = lerp(c, lc, M.x);
float3 d = w.x * float3(0.0, a.x, a.y) + w.y * float3(b.x, 0.0, b.y) + w.z * float3(c.x, c.y, 0.0);
return normalize(n + d * S * lerp(1.0, 0.3, M.y));""", ["P", "N", "F", "L", "Tile", "M", "S"])
    to_world = node(m, unreal.MaterialExpressionTransform, -500, 300,
                    transform_source_type=unreal.MaterialVectorCoordTransformSource.TRANSFORMSOURCE_LOCAL,
                    transform_type=unreal.MaterialVectorCoordTransform.TRANSFORM_WORLD)
    # a soft sheen on fur at the rims, and the white Hurt flash
    fresnel = node(m, unreal.MaterialExpressionFresnel, -800, 550, exponent=4.0, base_reflect_fraction=0.0)
    hurt = node(m, unreal.MaterialExpressionScalarParameter, -800, 700, parameter_name="Hurt", default_value=0.0)
    glow = custom(m, -500, 550, """
return C * Fr * 0.35 * (1.0 - M.x) + float3(1.6, 1.5, 1.4) * H;""", ["C", "Fr", "M", "H"])
    spec = node(m, unreal.MaterialExpressionConstant, -500, 750, r=0.35)

    wire(m, [(vc, masks, "VC"),
             (pos, detail, "P"), (normal_ls, detail, "N"), (detail_tex, detail, "T"), (tile, detail, "Tile"),
             (vc, colour, "VC"), (masks, colour, "M"), (detail, colour, "D"),
             (masks, rough, "M"), (detail, rough, "D"),
             (pos, normal, "P"), (normal_ls, normal, "N"), (fibre, normal, "F"), (lump, normal, "L"), (tile, normal, "Tile"),
             (masks, normal, "M"), (strength, normal, "S"),
             (normal, to_world, ""),
             (colour, glow, "C"), (fresnel, glow, "Fr"), (masks, glow, "M"), (hurt, glow, "H")])
    edit.connect_material_property(colour, "", unreal.MaterialProperty.MP_BASE_COLOR)
    edit.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    edit.connect_material_property(to_world, "", unreal.MaterialProperty.MP_NORMAL)
    edit.connect_material_property(glow, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    edit.connect_material_property(spec, "", unreal.MaterialProperty.MP_SPECULAR)
    return finish(m)


ENVIRONMENT = "/Game/Environment/HangGau"
TERRAIN_GLB = os.path.join(ROOT, "Unreal", "SourceArt", "Terrain", "HangGau_Ground.glb")


def ground():
    """The ground's paint: grass, bare earth where vertex colour R says (paths, the den), rock
    where G says (steep hills), each a colour texture and a normal map (texture parameters, so an
    instance swaps in an environment pack's and sets UseTextures to 1). Until then the ground is
    painted in tints alone, as Dauntless paints it: grass mottled between a sunlit yellow-green
    and a deep shade by broad and middling blotches, so it never tiles plainly."""
    m = material("M_Ground")
    # the glTF importer makes the terrain a Nanite mesh: without the flag the game draws the checker
    m.set_editor_property("used_with_nanite", True)
    # by object path: the commandlet's asset registry has not scanned the engine's content
    white = unreal.load_object(None, "/Engine/EngineResources/WhiteSquareTexture.WhiteSquareTexture")
    flat = unreal.load_object(None, "/Engine/EngineMaterials/DefaultNormal.DefaultNormal")
    if white is None or flat is None:
        raise RuntimeError("no engine default textures for M_Ground")
    vc = node(m, unreal.MaterialExpressionVertexColor, -1400, -300)
    wp = node(m, unreal.MaterialExpressionWorldPosition, -1400, -150)
    params = {}
    y = 0
    for name, tex in (("GrassColour", white), ("GrassNormal", flat), ("EarthColour", white), ("EarthNormal", flat),
                      ("RockColour", white), ("RockNormal", flat)):
        params[name] = node(m, unreal.MaterialExpressionTextureObjectParameter, -1400, y, parameter_name=name, texture=tex)
        y += 150
    tints = {}
    for i, (name, colour) in enumerate((("GrassTint", (0.115, 0.165, 0.035)), ("GrassShade", (0.035, 0.07, 0.018)),
                                        ("EarthTint", (0.16, 0.11, 0.06)), ("RockTint", (0.19, 0.18, 0.16)))):
        tints[name] = node(m, unreal.MaterialExpressionVectorParameter, -1100, 600 + 120 * i, parameter_name=name,
                           default_value=unreal.LinearColor(colour[0], colour[1], colour[2], 1))
    scalars = {}
    for i, (name, value) in enumerate((("GrassTile", 300.0), ("EarthTile", 400.0), ("RockTile", 800.0), ("UseTextures", 0.0))):
        scalars[name] = node(m, unreal.MaterialExpressionScalarParameter, -1100, 1100 + 100 * i, parameter_name=name, default_value=value)
    # blotches from smooth value noise (no texture: a borrowed one shows its own grain as streaks)
    common = """
struct Blotches
{
    float Hash(float2 c) { return frac(sin(dot(c, float2(127.1, 311.7))) * 43758.5453); }
    float Value(float2 q)
    {
        float2 i = floor(q);
        float2 f = frac(q);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
    }
    float Fbm(float2 q) { return Value(q) * 0.55 + Value(q * 2.03 + 17.0) * 0.3 + Value(q * 4.1 + 41.0) * 0.15; }
};
Blotches b;
float2 p = WP.xy;
float macro = b.Fbm(p / 2500.0);
float meso = b.Fbm(p / 450.0 + 7.3);
float earth = saturate(VC.r * 1.3 + (macro - 0.5) * 0.9 + (meso - 0.5) * 0.4);
float rock = saturate(VC.g * 1.2);
"""
    colour = custom(m, -700, -200, common + """
float sun = smoothstep(0.25, 0.75, macro * 0.75 + meso * 0.45);
float3 g = lerp(GS, GT, sun) * lerp(1.0, Texture2DSample(GrassColour, GrassColourSampler, p / GrassTile).rgb, UseTextures);
float3 e = ET * lerp(0.85, 1.1, meso) * lerp(1.0, Texture2DSample(EarthColour, EarthColourSampler, p / EarthTile).rgb, UseTextures);
float3 r = RT * lerp(0.8, 1.1, meso) * lerp(1.0, Texture2DSample(RockColour, RockColourSampler, p / RockTile).rgb, UseTextures);
return lerp(lerp(g, e, earth), r, rock);""", ["VC", "WP", "GrassColour", "EarthColour", "RockColour", "GT", "GS", "ET", "RT",
                                              "GrassTile", "EarthTile", "RockTile", "UseTextures"])
    normal = custom(m, -700, 200, common + """
float3 g = Texture2DSample(GrassNormal, GrassNormalSampler, p / GrassTile).xyz * 2.0 - 1.0;
float3 e = Texture2DSample(EarthNormal, EarthNormalSampler, p / EarthTile).xyz * 2.0 - 1.0;
float3 r = Texture2DSample(RockNormal, RockNormalSampler, p / RockTile).xyz * 2.0 - 1.0;
float3 n = lerp(lerp(g, e, earth), r, rock);
n.xy *= UseTextures;
n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
return normalize(n);""", ["VC", "WP", "GrassNormal", "EarthNormal", "RockNormal", "GrassTile", "EarthTile", "RockTile", "UseTextures"])
    rough = custom(m, -700, 500, common + """
return lerp(lerp(0.92, 0.85, earth), 0.75, rock);""", ["VC", "WP"], unreal.CustomMaterialOutputType.CMOT_FLOAT1)
    for c_node, names in ((colour, ["GrassColour", "EarthColour", "RockColour"]), (normal, ["GrassNormal", "EarthNormal", "RockNormal"])):
        for nm in names:
            edit.connect_material_expressions(params[nm], "", c_node, nm)
        for nm in ("GrassTile", "EarthTile", "RockTile", "UseTextures"):
            edit.connect_material_expressions(scalars[nm], "", c_node, nm)
    for c_node in (colour, normal, rough):
        edit.connect_material_expressions(vc, "", c_node, "VC")
        edit.connect_material_expressions(wp, "", c_node, "WP")
    for pin, name in (("GT", "GrassTint"), ("GS", "GrassShade"), ("ET", "EarthTint"), ("RT", "RockTint")):
        edit.connect_material_expressions(tints[name], "", colour, pin)
    edit.connect_material_property(colour, "", unreal.MaterialProperty.MP_BASE_COLOR)
    edit.connect_material_property(normal, "", unreal.MaterialProperty.MP_NORMAL)
    edit.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    return finish(m)


def import_terrain(ground_material):
    """Hang Gấu's ground (make_terrain.py): walked on exactly as it is drawn."""
    task = unreal.AssetImportTask()
    task.filename = TERRAIN_GLB
    task.destination_path = ENVIRONMENT
    task.automated = True
    task.replace_existing = True
    task.save = True
    tools.import_asset_tasks([task])
    registry = unreal.AssetRegistryHelpers.get_asset_registry()
    registry.scan_paths_synchronous([ENVIRONMENT], True)
    for a in registry.get_assets_by_path(ENVIRONMENT, recursive=True):
        obj = a.get_asset()
        if isinstance(obj, unreal.StaticMesh):
            obj.set_material(0, ground_material)
            body = obj.get_editor_property("body_setup")
            if body:
                body.set_editor_property("collision_trace_flag", unreal.CollisionTraceFlag.CTF_USE_COMPLEX_AS_SIMPLE)
            assets.save_loaded_asset(obj)
            unreal.log("RTT_ASSETS terrain %s %s painted %s" % (a.package_name, obj.get_bounding_box(),
                                                                 obj.get_material(0).get_name() if obj.get_material(0) else "NOTHING"))


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


vertex_colour()
vc = creature(import_textures())
glow_mat = glow()
telegraph()
import_terrain(ground())

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
