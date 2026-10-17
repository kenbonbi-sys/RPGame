"""Builds the Unreal port's maps from scratch (editor Python): /Game/Maps/ThuNghiem, the test
ground, and /Game/Maps/HangGau, Gấu Ma's den (the MVP boss fight):

    Tools\\Unreal\\ue.cmd level

A 400 m ground, a sun with a sky, clouds and fog all lit live (Lumen, no baked light), a few blocks
for scale and a PlayerStart, all in M_Grid: a 1 m checker laid by world position, so every surface
shows metres whatever its size (the engine's grid follows each mesh's UVs and turns into noise on a
plane stretched 400 times). The game mode (ARttGameMode, set in Config/DefaultEngine.ini) brings
the hero. ue.cmd deletes the old map and material first, so this always builds them anew.
"""
import unreal

MAP = "/Game/Maps/ThuNghiem"
ARENA = "/Game/Maps/HangGau"
MATERIALS = "/Game/Materials"
GRID_NAME = "M_Grid"
CUBE = "/Engine/BasicShapes/Cube"
PLANE = "/Engine/BasicShapes/Plane"
SPHERE = "/Engine/BasicShapes/Sphere"

assets = unreal.EditorAssetLibrary
edit = unreal.MaterialEditingLibrary
levels = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)


def node(material, cls, x, y, **props):
    expression = edit.create_material_expression(material, cls, x, y)
    for key, value in props.items():
        expression.set_editor_property(key, value)
    return expression


def make_grid():
    """A grey checker of 1 m cells by world position: floor((p - n) / 100), x + y + z, mod 2. The
    point is moved 1 cm into the surface first so faces lying on a cell boundary do not flicker."""
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    material = tools.create_asset(GRID_NAME, MATERIALS, unreal.Material, unreal.MaterialFactoryNew())
    position = node(material, unreal.MaterialExpressionWorldPosition, -1200, 0)
    normal = node(material, unreal.MaterialExpressionVertexNormalWS, -1200, 150)
    inside = node(material, unreal.MaterialExpressionSubtract, -1000, 0)
    edit.connect_material_expressions(position, "", inside, "A")
    edit.connect_material_expressions(normal, "", inside, "B")
    metres = node(material, unreal.MaterialExpressionDivide, -850, 0, const_b=100.0)
    edit.connect_material_expressions(inside, "", metres, "A")
    cell = node(material, unreal.MaterialExpressionFloor, -700, 0)
    edit.connect_material_expressions(metres, "", cell, "")
    ones = node(material, unreal.MaterialExpressionConstant3Vector, -700, 150, constant=unreal.LinearColor(1, 1, 1, 1))
    total = node(material, unreal.MaterialExpressionDotProduct, -550, 0)
    edit.connect_material_expressions(cell, "", total, "A")
    edit.connect_material_expressions(ones, "", total, "B")
    two = node(material, unreal.MaterialExpressionConstant, -550, 150, r=2.0)
    parity = node(material, unreal.MaterialExpressionFmod, -400, 0)
    edit.connect_material_expressions(total, "", parity, "A")
    edit.connect_material_expressions(two, "", parity, "B")
    # fmod of a negative sum is negative: |x| makes both signs 0 or 1
    odd = node(material, unreal.MaterialExpressionAbs, -250, 0)
    edit.connect_material_expressions(parity, "", odd, "")
    dark = node(material, unreal.MaterialExpressionConstant3Vector, -250, 150, constant=unreal.LinearColor(0.16, 0.16, 0.15, 1))
    light = node(material, unreal.MaterialExpressionConstant3Vector, -250, 300, constant=unreal.LinearColor(0.26, 0.26, 0.24, 1))
    colour = node(material, unreal.MaterialExpressionLinearInterpolate, -100, 0)
    edit.connect_material_expressions(dark, "", colour, "A")
    edit.connect_material_expressions(light, "", colour, "B")
    edit.connect_material_expressions(odd, "", colour, "Alpha")
    edit.connect_material_property(colour, "", unreal.MaterialProperty.MP_BASE_COLOR)
    rough = node(material, unreal.MaterialExpressionConstant, -100, 200, r=0.85)
    edit.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    edit.recompile_material(material)
    assets.save_loaded_asset(material)
    return material


def spawn(cls, label, x=0.0, y=0.0, z=0.0, pitch=0.0, yaw=0.0):
    actor = actors.spawn_actor_from_class(cls, unreal.Vector(x, y, z), unreal.Rotator(roll=0.0, pitch=pitch, yaw=yaw))
    actor.set_actor_label(label)
    return actor


def movable(actor, component_class):
    component = actor.get_component_by_class(component_class)
    component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
    return component


def block(label, mesh, x, y, z, sx, sy, sz, yaw=0.0):
    actor = spawn(unreal.StaticMeshActor, label, x, y, z, yaw=yaw)
    component = actor.static_mesh_component
    component.set_static_mesh(assets.load_asset(mesh))
    component.set_material(0, GRID)
    actor.set_actor_scale3d(unreal.Vector(sx, sy, sz))
    return actor


GRID = make_grid()


def outdoors(map_path):
    """A new map with the ground, a sun, sky, clouds and fog, all lit live."""
    # new_level refuses to replace a map, so ue.cmd deletes the old ones first
    if not levels.new_level(map_path):
        raise RuntimeError("cannot create " + map_path + " (delete its .umap first)")
    # the ground: Plane is 1 m across. Stretched that far its distance field is too coarse to light
    # anything by, so it stays out of distance-field lighting.
    ground = block("Ground", PLANE, 0, 0, 0, 400, 400, 1)
    ground.static_mesh_component.set_editor_property("affect_distance_field_lighting", False)
    sun = spawn(unreal.DirectionalLight, "Sun", z=800, pitch=-38.0, yaw=-35.0)
    sun_light = movable(sun, unreal.DirectionalLightComponent)
    sun_light.set_editor_property("atmosphere_sun_light", True)
    sun_light.set_editor_property("intensity", 10.0)
    spawn(unreal.SkyAtmosphere, "Sky Atmosphere")
    sky = spawn(unreal.SkyLight, "Sky Light", z=600)
    sky_light = movable(sky, unreal.SkyLightComponent)
    sky_light.set_editor_property("real_time_capture", True)
    spawn(unreal.VolumetricCloud, "Clouds")
    spawn(unreal.ExponentialHeightFog, "Height Fog", z=-200)
    post = spawn(unreal.PostProcessVolume, "Post Process")
    post.set_editor_property("unbound", True)


def save(map_path):
    if not levels.save_current_level():
        raise RuntimeError("cannot save " + map_path)
    print("RTT_LEVEL_OK " + map_path)


# ThuNghiem: blocks for scale (Cube is 1 m): a 2 m wall, steps, a ring of 4 m pillars, a boulder
outdoors(MAP)
block("Wall", CUBE, 900, -300, 100, 0.5, 6, 2)
for i in range(5):
    block("Step %d" % (i + 1), CUBE, 500 + i * 80, 500, 20 + i * 20, 0.8, 3, 0.4 + i * 0.4)
for i in range(8):
    angle = i * 45.0
    v = unreal.MathLibrary.rotate_angle_axis(unreal.Vector(1800, 0, 0), angle, unreal.Vector(0, 0, 1))
    block("Pillar %d" % (i + 1), CUBE, v.x, v.y, 200, 1, 1, 4, yaw=angle)
block("Boulder", SPHERE, -700, 400, 100, 3, 3, 2.2)
spawn(unreal.PlayerStart, "Player Start", z=120)
save(MAP)

# HangGau: the first colossus's den, the MVP fight. The colossus (ARttBehemoth, a Paragon monster
# named by ue.cmd beast) stands 35 m off facing the hero; its den reaches 38 m around where it
# stands (BearStats::DenRadius). Gấu Ma (ARttGauMa) can stand in for it: COLOSSUS below.
COLOSSUS = "/Script/RungThiTham.RttBehemoth"
outdoors(ARENA)
spawn(unreal.PlayerStart, "Player Start", z=120)
colossus = unreal.load_class(None, COLOSSUS)
if colossus is None:
    raise RuntimeError(COLOSSUS + " is not built: run ue.cmd build first")
spawn(colossus, "Colossus", x=3500, yaw=180.0)
save(ARENA)
