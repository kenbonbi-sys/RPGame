class_name Foliage
extends Node3D
## Scatters the land (same seed, same world every time): old trees in the forest with a dense wall
## along the map's edge, pines toward the mountains, crooked trees in the swamp, flat-topped
## acacias on the steppe, bushes (TreeArt), boulders and the rock chunks that make the steep slopes
## read as cliffs (RockArt), and logs, mushrooms and flowers from the Kenney Nature Kit. Drawn with
## MultiMeshes in 128 m cells, built things with a light far version; trees and rocks are solid.

const CELL := 128.0
const KIT := "res://assets/third_party/kenney_nature/%s.glb"

## "gen:" models are built by TreeArt (metres); the rest are Kenney models.
const FOREST_TREES := ["gen:oak_0", "gen:oak_1", "gen:oak_2", "gen:round_0", "gen:round_1", "gen:tall_0", "gen:tall_1",
	"gen:oakdark_0", "gen:oakdark_1", "gen:oakdark_2"]
const BLOSSOMS := ["gen:blossom_0", "gen:blossom_1"]
const PINES := ["gen:pine_0", "gen:pine_1", "gen:pine_2", "gen:pine_3"]
const SWAMP_TREES := ["gen:swamp_0", "gen:swamp_1", "gen:swamp_2"]
const STEPPE_TREES := ["gen:acacia_0", "gen:acacia_1", "gen:acacia_2"]
const BUSHES := ["gen:bush_0", "gen:bush_1", "gen:bush_2", "gen:bush_3"]
const ROCKS := ["gen:rock_boulder_0", "gen:rock_boulder_1", "gen:rock_boulder_2", "gen:rock_boulder_3",
	"gen:rock_slab_0", "gen:rock_slab_1", "gen:rock_tall_0", "gen:rock_tall_1"]
const CLIFFS := ["gen:rock_cliff_0", "gen:rock_cliff_1", "gen:rock_cliff_2", "gen:rock_cliff_3", "gen:rock_tall_2"]
## (the grass itself is the Grass node's)
const SMALL := ["mushroom_red", "mushroom_redGroup", "mushroom_tan", "mushroom_tanGroup", "flower_purpleA",
	"flower_redA", "flower_yellowA", "flower_purpleA", "flower_redA", "flower_yellowA"]
const LOGS := ["stump_old", "stump_oldTall", "log", "log_large", "stump_round"]

## draw distance per kind
const RANGE := {"tree": 650.0, "rock": 520.0, "cliff": 900.0, "bush": 180.0, "small": 75.0, "log": 220.0}
## built things switch to their far version here
const NEAR := {"tree": 150.0, "bush": 70.0, "rock": 140.0, "cliff": 200.0}

@export var data: TerrainData = preload("res://world/terrain_data.res")

var _parts_cache := {}
var _batches := {}          # key "kind|model|cx|cz" -> Array[Transform3D]
var _solid: StaticBody3D
var _rng := RandomNumberGenerator.new()
var _density := FastNoiseLite.new()
var placed := 0


func _ready() -> void:
	_rng.seed = 20260924
	_density.seed = 77
	_density.frequency = 0.018
	_solid = StaticBody3D.new()
	_solid.name = "Solid"
	_solid.collision_layer = 1
	_solid.collision_mask = 0
	add_child(_solid)
	_scatter_trees()
	_scatter_rocks()
	_scatter_cliffs()
	_scatter_bushes()
	_scatter_logs()
	_scatter_small()
	_build_batches()


## The meshes inside a Kenney model, with their transforms relative to the model.
func parts(model: String) -> Array:
	if _parts_cache.has(model):
		return _parts_cache[model]
	var out := []
	if model.begins_with("gen:"):
		out.append([built(model.substr(4)), Transform3D.IDENTITY])
		_parts_cache[model] = out
		return out
	var scene: PackedScene = load(KIT % model)
	var root := scene.instantiate() as Node3D
	for n in root.find_children("*", "MeshInstance3D", true, false):
		var mi := n as MeshInstance3D
		var xf := Transform3D.IDENTITY
		var p: Node = mi
		while p and p != root:
			xf = (p as Node3D).transform * xf
			p = p.get_parent()
		out.append([Palette.recolor(mi.mesh), xf])
	root.free()
	_parts_cache[model] = out
	return out


## A built model by name: "rock_*" from RockArt, the rest from TreeArt.
static func built(model_name: String, far := false) -> ArrayMesh:
	if model_name.begins_with("rock_"):
		return RockArt.mesh(model_name.substr(5), far)
	return TreeArt.mesh(model_name, far)


func _blocked(u: float, v: float, margin := 0.0) -> bool:
	var m := Vector2(u, v)
	if m.distance_to(WorldMap.village_center()) < WorldMap.VILLAGE_R + 8.0 + margin:
		return true
	if m.distance_to(WorldMap.arena_center()) < WorldMap.ARENA_RIDGE_IN - 2.0 + margin:
		return true
	for i in WorldMap.CAMPS.size():
		if m.distance_to(WorldMap.CAMPS[i] * WorldMap.TILE) < WorldMap.CAMP_RADIUS[i] * WorldMap.TILE * 1.1 + margin:
			return true
	return false


func _add(kind: String, model: String, u: float, v: float, scale: float, sink := 0.0, tilt := 0.05) -> Transform3D:
	var h := data.height_at(u, v)
	var b := Basis(Vector3.UP, _rng.randf() * TAU)
	b = Basis(Vector3(_rng.randf_range(-1, 1), 0, _rng.randf_range(-1, 1)).normalized(), _rng.randf_range(0.0, tilt)) * b
	var xf := Transform3D(b.scaled(Vector3.ONE * scale), Vector3(u, h - sink, -v))
	var key := "%s|%s|%d|%d" % [kind, model, int(u / CELL), int(v / CELL)]
	if not _batches.has(key):
		_batches[key] = []
	_batches[key].append(xf)
	placed += 1
	return xf


func _jitter_grid(step: float, cb: Callable) -> void:
	var v := step * 0.5
	while v < WorldMap.SIZE_V:
		var u := step * 0.5
		while u < WorldMap.SIZE_U:
			cb.call(u + _rng.randf_range(-0.45, 0.45) * step, v + _rng.randf_range(-0.45, 0.45) * step)
			u += step
		v += step


func _scatter_trees() -> void:
	_jitter_grid(7.0, _tree_at)


func _tree_at(u: float, v: float) -> void:
	var h := data.height_at(u, v)
	if h < 0.5 or h > 95.0 or _blocked(u, v):
		return
	var s := data.splat_at(u, v)
	if s.g > 0.15 or s.b > 0.55 or data.normal_at(u, v).y < 0.8:
		return
	var n := _density.get_noise_2d(u, v)
	var border := WorldMap.border_weight(u, v)
	var swamp := WorldMap.swamp_weight(u, v)
	var north := WorldMap.north_weight(u, v)
	var mountain := WorldMap.mountain_weight(u, v)
	var list: Array = FOREST_TREES if _rng.randf() > 0.04 else BLOSSOMS
	var p := 0.3 + 0.45 * smoothstep(-0.2, 0.45, n) + border * 0.6
	var scale := _rng.randf_range(0.85, 1.3)
	if mountain > 0.5:
		list = PINES
		p = 0.18 + border * 0.3
	elif north > 0.5:
		list = STEPPE_TREES
		p = 0.035
		scale = _rng.randf_range(0.85, 1.15)
	elif swamp > 0.5:
		list = SWAMP_TREES
		p = 0.2 + 0.25 * smoothstep(0.0, 0.5, n)
		scale = _rng.randf_range(0.85, 1.2)
	elif v > WorldMap.LOW_V - 50.0 or border > 0.3:
		list = PINES if _rng.randf() < 0.55 else FOREST_TREES
	if WorldMap.path_distance(u, v).x < 8.0:
		p *= 0.35
	if _rng.randf() > p:
		return
	var model: String = list[_rng.randi() % list.size()]
	_add("tree", model, u, v, scale, 0.15, 0.04)
	var cs := CollisionShape3D.new()
	var cyl := CylinderShape3D.new()
	cyl.radius = TreeArt.trunk_radius(model.substr(4)) * scale
	cyl.height = 8.0
	cs.shape = cyl
	cs.position = Vector3(u, data.height_at(u, v) + 3.5, -v)
	_solid.add_child(cs)


func _scatter_rocks() -> void:
	_jitter_grid(16.0, _rock_at)


func _rock_at(u: float, v: float) -> void:
	var h := data.height_at(u, v)
	if h < 0.3 or _blocked(u, v, 4.0):
		return
	var s := data.splat_at(u, v)
	if s.g > 0.2:
		return
	var p := 0.07 + s.b * 0.35
	if _rng.randf() > p:
		return
	var model: String = ROCKS[_rng.randi() % ROCKS.size()]
	if data.normal_at(u, v).y < 0.85 and model.contains("slab"):
		model = ROCKS[0]
	var scale := _rng.randf_range(1.4, 3.6) * (1.5 if s.b > 0.5 else 1.0)
	var xf := _add("rock", model, u, v, scale, 0.3 * scale, 0.2)
	_solid_mesh(model, xf)


## Big rock chunks sunk into every steep slope, so hillsides and the mountains read as stacked,
## faceted cliffs instead of a smooth stretched surface. Kept off roads and paths.
func _scatter_cliffs() -> void:
	_jitter_grid(11.0, _cliff_at)


func _cliff_at(u: float, v: float) -> void:
	var h := data.height_at(u, v)
	if h < 1.5 or _blocked(u, v, 2.0):
		return
	var n := data.normal_at(u, v)
	if n.y > 0.8:
		return
	if data.splat_at(u, v).g > 0.1 or WorldMap.path_distance(u, v).x < 9.0:
		return
	var steep := smoothstep(0.8, 0.55, n.y)
	if _rng.randf() > 0.25 + 0.5 * steep:
		return
	var model: String = CLIFFS[_rng.randi() % CLIFFS.size()]
	var scale := _rng.randf_range(3.0, 6.0) * (1.0 + 0.6 * smoothstep(40.0, 90.0, h))
	# lean the chunk with the slope a little, and sink it well in
	var up := Vector3.UP.lerp(n, 0.35).normalized()
	var b := Basis(Vector3.UP, _rng.randf() * TAU)
	var axis := Vector3.UP.cross(up)
	if axis.length() > 0.001:
		b = Basis(axis.normalized(), Vector3.UP.angle_to(up)) * b
	var xf := Transform3D(b.scaled(Vector3.ONE * scale), Vector3(u, h - 0.45 * scale, -v))
	var key := "cliff|%s|%d|%d" % [model, int(u / CELL), int(v / CELL)]
	if not _batches.has(key):
		_batches[key] = []
	_batches[key].append(xf)
	placed += 1
	_solid_mesh(model, xf)


func _solid_mesh(model: String, xf: Transform3D) -> void:
	if model.begins_with("gen:rock_"):
		var shape := ConvexPolygonShape3D.new()
		var pts := PackedVector3Array()
		for q in RockArt.hull(model.substr(9)):
			pts.append(xf * q)
		shape.points = pts
		var rcs := CollisionShape3D.new()
		rcs.shape = shape
		_solid.add_child(rcs)
		return
	for part in parts(model):
		var mesh: Mesh = part[0]
		var local: Transform3D = xf * part[1]
		var pts := PackedVector3Array()
		var faces := mesh.get_faces()
		var step := maxi(1, faces.size() / 180)
		for i in range(0, faces.size(), step):
			pts.append(local * faces[i])
		var shape := ConvexPolygonShape3D.new()
		shape.points = pts
		var cs := CollisionShape3D.new()
		cs.shape = shape
		_solid.add_child(cs)


func _scatter_bushes() -> void:
	_jitter_grid(6.0, _bush_at)


func _bush_at(u: float, v: float) -> void:
	var h := data.height_at(u, v)
	if h < 0.4 or WorldMap.north_weight(u, v) > 0.5 or _blocked(u, v):
		return
	var s := data.splat_at(u, v)
	if s.g > 0.12 or s.b > 0.4:
		return
	var road := WorldMap.path_distance(u, v).x
	var p := 0.12 + (0.25 if road < 7.0 else 0.0)
	if _rng.randf() > p:
		return
	_add("bush", BUSHES[_rng.randi() % BUSHES.size()], u, v, _rng.randf_range(0.75, 1.5), 0.1, 0.1)


func _scatter_logs() -> void:
	_jitter_grid(22.0, _log_at)


func _log_at(u: float, v: float) -> void:
	var h := data.height_at(u, v)
	if h < 0.4 or WorldMap.north_weight(u, v) > 0.5 or _blocked(u, v):
		return
	if data.splat_at(u, v).g > 0.12 or _rng.randf() > 0.22:
		return
	_add("log", LOGS[_rng.randi() % LOGS.size()], u, v, _rng.randf_range(3.0, 5.0), 0.1, 0.05)


func _scatter_small() -> void:
	_jitter_grid(2.6, _small_at)


func _small_at(u: float, v: float) -> void:
	if v > WorldMap.LOW_V + 4.0 or u > 470.0:
		return
	var h := data.height_at(u, v)
	if h < 0.6:
		return
	var s := data.splat_at(u, v)
	if s.g > 0.25 or s.b > 0.3:
		return
	var n := _density.get_noise_2d(u * 3.0, v * 3.0)
	if _rng.randf() > 0.12 + 0.25 * smoothstep(0.0, 0.5, n):
		return
	_add("small", SMALL[_rng.randi() % SMALL.size()], u, v, _rng.randf_range(2.0, 3.6), 0.05, 0.15)


func _build_batches() -> void:
	for key in _batches:
		var bits: PackedStringArray = key.split("|")
		var kind := bits[0]
		var model := bits[1]
		var xfs: Array = _batches[key]
		if model.begins_with("gen:"):
			_add_built(kind, model.substr(4), xfs, key)
			continue
		for part in parts(model):
			var mm := MultiMesh.new()
			mm.transform_format = MultiMesh.TRANSFORM_3D
			mm.mesh = part[0]
			mm.instance_count = xfs.size()
			for i in xfs.size():
				mm.set_instance_transform(i, xfs[i] * part[1])
			var mmi := MultiMeshInstance3D.new()
			mmi.name = key.replace("|", "_")
			mmi.multimesh = mm
			mmi.visibility_range_end = RANGE[kind]
			mmi.visibility_range_end_margin = 20.0
			mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
			if kind == "small":
				mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(mmi)


## A built batch (trees, bushes, rocks): the full version up close, the light one beyond NEAR[kind].
func _add_built(kind: String, tree_name: String, xfs: Array, key: String) -> void:
	var split: float = NEAR.get(kind, 120.0)
	for far in [false, true]:
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = built(tree_name, far)
		mm.instance_count = xfs.size()
		for i in xfs.size():
			mm.set_instance_transform(i, xfs[i])
		var mmi := MultiMeshInstance3D.new()
		mmi.name = key.replace("|", "_").replace(":", "_") + ("_far" if far else "")
		mmi.multimesh = mm
		if far:
			mmi.visibility_range_begin = split
			mmi.visibility_range_begin_margin = 12.0
			mmi.visibility_range_end = RANGE[kind]
			mmi.visibility_range_end_margin = 20.0
		else:
			mmi.visibility_range_end = split
			mmi.visibility_range_end_margin = 12.0
		mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		if kind == "bush" and far:
			mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mmi)
