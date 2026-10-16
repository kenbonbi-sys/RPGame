class_name Grass
extends Node3D
## Dense stylised grass around the camera (Dauntless meadows): 16 m cells of grass clumps in a
## window that follows the camera; a cell is built once from the land (no grass on roads, rock,
## mud or water) and reused. The shader handles wind, parting around the hero and the soft edge.

const CELL := 16.0
const RADIUS := 3            ## cells each way: a 7×7 window, about 50 m around the camera
const PER_CELL := 520

## The settings: cells each way, and clumps per cell as a share of PER_CELL.
var radius := RADIUS
var density := 1.0

var data: TerrainData
var _mesh: ArrayMesh
var _mat: ShaderMaterial
var _cells := {}             # Vector2i -> MultiMeshInstance3D
var _center := Vector2i(99999, 99999)


func _ready() -> void:
	data = Terrain.current.data if Terrain.current else null
	_mesh = _clump_mesh()
	_mat = ShaderMaterial.new()
	_mat.shader = preload("res://shaders/grass.gdshader")


## View distance and density from the settings; the window is rebuilt on the next frame.
func set_quality(cells: int, share: float) -> void:
	if cells == radius and is_equal_approx(share, density):
		return
	radius = cells
	density = share
	for k in _cells:
		if _cells[k]:
			_cells[k].queue_free()
	_cells.clear()
	_center = Vector2i(99999, 99999)


func _process(_delta: float) -> void:
	if data == null or not Game.has_screen():
		return
	var cam := get_viewport().get_camera_3d()
	if cam == null:
		return
	var p := cam.global_position
	_mat.set_shader_parameter("camera_pos", p)
	if is_instance_valid(Game.hero):
		_mat.set_shader_parameter("hero_pos", Game.hero.global_position)
	var c := Vector2i(floori(p.x / CELL), floori(p.z / CELL))
	if c == _center:
		return
	_center = c
	var keep := {}
	for dz in range(-radius, radius + 1):
		for dx in range(-radius, radius + 1):
			var k := Vector2i(c.x + dx, c.y + dz)
			keep[k] = true
			if not _cells.has(k):
				_cells[k] = _build_cell(k)
	for k in _cells.keys():
		if not keep.has(k):
			if _cells[k]:
				_cells[k].queue_free()
			_cells.erase(k)


func _build_cell(k: Vector2i) -> MultiMeshInstance3D:
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(k) ^ 0x5eed
	var xfs: Array[Transform3D] = []
	for i in roundi(PER_CELL * density):
		var x := (k.x + rng.randf()) * CELL
		var z := (k.y + rng.randf()) * CELL
		var u := x
		var v := -z
		if u < 1.0 or v < 1.0 or u > WorldMap.SIZE_U - 1.0 or v > WorldMap.SIZE_V - 1.0:
			continue
		var h := data.height_at(u, v)
		if h < 0.25:
			continue
		var s := data.splat_at(u, v)
		var grassy := s.r - s.g * 1.5 - s.b * 2.0 - s.a * 1.5
		if grassy < 0.35 or rng.randf() > grassy:
			continue
		if data.normal_at(u, v).y < 0.78:
			continue
		var scale := rng.randf_range(0.6, 1.15) * (0.8 + 0.4 * smoothstep(0.35, 1.0, grassy))
		var b := Basis(Vector3.UP, rng.randf() * TAU).scaled(Vector3(scale, scale * rng.randf_range(0.8, 1.3), scale))
		xfs.append(Transform3D(b, Vector3(x, h - 0.03, z)))
	if xfs.is_empty():
		return null
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = _mesh
	mm.instance_count = xfs.size()
	for i in xfs.size():
		mm.set_instance_transform(i, xfs[i])
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	mmi.material_override = _mat
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mmi)
	return mmi


## A clump of seven curved, tapering blades; UV.y runs from root (0) to tip (1).
static func _clump_mesh() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	for blade in 7:
		var a := rng.randf() * TAU
		var off := Vector3(cos(a), 0, sin(a)) * rng.randf_range(0.0, 0.16)
		var face := rng.randf() * TAU
		var side := Vector3(cos(face), 0, sin(face))
		var lean := Vector3(-sin(face), 0, cos(face)) * rng.randf_range(0.08, 0.25)
		var height := rng.randf_range(0.28, 0.5)
		var width := rng.randf_range(0.04, 0.065)
		var segs := 3
		for sgm in segs:
			var t0 := float(sgm) / segs
			var t1 := float(sgm + 1) / segs
			var p0 := off + Vector3.UP * height * t0 + lean * t0 * t0
			var p1 := off + Vector3.UP * height * t1 + lean * t1 * t1
			var w0 := width * (1.0 - t0)
			var w1 := width * (1.0 - t1) + 0.002
			var verts := [p0 - side * w0, p0 + side * w0, p1 + side * w1, p1 - side * w1]
			var uvs := [Vector2(0, t0), Vector2(1, t0), Vector2(1, t1), Vector2(0, t1)]
			for idx in [0, 1, 2, 0, 2, 3]:
				st.set_normal(Vector3.UP)
				st.set_uv(uvs[idx])
				st.add_vertex(verts[idx])
	return st.commit()
