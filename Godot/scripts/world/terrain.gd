class_name Terrain
extends Node3D
## Builds the land from the baked TerrainData: 64 m mesh chunks painted by terrain.gdshader,
## one heightmap collider over the whole map at 1 m, and the swamp's water.

const CHUNK_CELLS := 32

@export var data: TerrainData = preload("res://world/terrain_data.res")
@export var material: Material = preload("res://shaders/terrain_material.tres")
@export var water_material: Material = preload("res://shaders/water_material.tres")

static var current: Terrain


func _enter_tree() -> void:
	current = self


func _ready() -> void:
	_build_meshes()
	_build_collision()
	_build_water()


## Ground height under a world position (no physics needed).
static func height_at_world(p: Vector3) -> float:
	return current.data.height_at_world(p) if current else 0.0


## Height of the land as drawn (feet are put on this, not on the collider).
static func drawn_height_at_world(p: Vector3) -> float:
	return current.data.mesh_height_at(p.x, -p.z) if current else 0.0


static func is_ground(body: Object) -> bool:
	return body is Node and current != null and (body as Node).get_parent() == current


func _build_meshes() -> void:
	var d := data
	var normals := PackedVector3Array()
	normals.resize(d.width * d.depth)
	for iv in d.depth:
		for ix in d.width:
			var hl := d.height_at_sample(ix - 1, iv)
			var hr := d.height_at_sample(ix + 1, iv)
			var hd := d.height_at_sample(ix, iv - 1)
			var hu := d.height_at_sample(ix, iv + 1)
			normals[iv * d.width + ix] = Vector3(hl - hr, 2.0 * d.spacing, hu - hd).normalized()

	var cells_u := d.width - 1
	var cells_v := d.depth - 1
	for cv in range(0, cells_v, CHUNK_CELLS):
		for cu in range(0, cells_u, CHUNK_CELLS):
			var mi := MeshInstance3D.new()
			mi.name = "Chunk_%d_%d" % [cu / CHUNK_CELLS, cv / CHUNK_CELLS]
			mi.mesh = _chunk_mesh(cu, cv, mini(CHUNK_CELLS, cells_u - cu), mini(CHUNK_CELLS, cells_v - cv), normals)
			mi.material_override = material
			add_child(mi)


func _chunk_mesh(cu: int, cv: int, nu: int, nv: int, normals: PackedVector3Array) -> ArrayMesh:
	var d := data
	var verts := PackedVector3Array()
	var norms := PackedVector3Array()
	var colors := PackedColorArray()
	var idx := PackedInt32Array()
	for j in nv + 1:
		var iv := cv + j
		for i in nu + 1:
			var ix := cu + i
			var k := iv * d.width + ix
			verts.append(Vector3(ix * d.spacing, d.heights[k], -iv * d.spacing))
			norms.append(normals[k])
			colors.append(Color(d.splat[k * 4] / 255.0, d.splat[k * 4 + 1] / 255.0, d.splat[k * 4 + 2] / 255.0, d.splat[k * 4 + 3] / 255.0))
	var row := nu + 1
	for j in nv:
		for i in nu:
			var a := j * row + i
			var b := a + 1
			var c := a + row
			var e := c + 1
			# world z = -v, so rows run toward -z; wind the triangles to face up
			idx.append_array([a, c, b, b, c, e])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = idx
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


func _build_collision() -> void:
	var d := data
	# 2 m samples -> 1 m heightmap; rows of HeightMapShape3D run along +z, i.e. from the north edge south
	var img := Image.create_from_data(d.width, d.depth, false, Image.FORMAT_RF, d.heights.to_byte_array())
	img.flip_y()
	var w := (d.width - 1) * 2 + 1
	var h := (d.depth - 1) * 2 + 1
	img.resize(w, h, Image.INTERPOLATE_BILINEAR)
	var shape := HeightMapShape3D.new()
	shape.map_width = w
	shape.map_depth = h
	shape.map_data = img.get_data().to_float32_array()
	var body := StaticBody3D.new()
	body.name = "Ground"
	body.collision_layer = 1
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.position = Vector3((w - 1) * 0.5, 0.0, -(h - 1) * 0.5)
	body.add_child(cs)
	add_child(body)


func _build_water() -> void:
	var plane := PlaneMesh.new()
	plane.size = Vector2(WorldMap.SIZE_U - 360.0, WorldMap.LOW_V + 20.0)
	plane.subdivide_width = 8
	plane.subdivide_depth = 8
	var mi := MeshInstance3D.new()
	mi.name = "SwampWater"
	mi.mesh = plane
	mi.material_override = water_material
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = Vector3(360.0 + plane.size.x * 0.5, WorldMap.WATER_LEVEL, -plane.size.y * 0.5 + 10.0)
	add_child(mi)
