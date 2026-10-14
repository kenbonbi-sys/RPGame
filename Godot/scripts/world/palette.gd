class_name Palette
extends RefCounted
## Re-colours the Kenney Nature Kit (bright pastel) into the game's muted forest palette, by the
## kit's material names. Leaves get a gentle sway in the wind.

const COLORS := {
	"leafsGreen": Color(0.31, 0.43, 0.22),
	"leafsDark": Color(0.20, 0.30, 0.17),
	"leafsFall": Color(0.60, 0.44, 0.22),
	"grass": Color(0.34, 0.46, 0.23),
	"wood": Color(0.42, 0.32, 0.23),
	"woodBark": Color(0.33, 0.26, 0.19),
	"woodBarkDark": Color(0.26, 0.21, 0.16),
	"woodDark": Color(0.28, 0.21, 0.15),
	"woodBirch": Color(0.76, 0.73, 0.66),
	"woodInner": Color(0.62, 0.52, 0.38),
	"stone": Color(0.56, 0.56, 0.53),
	"stoneDark": Color(0.42, 0.42, 0.40),
	"dirt": Color(0.44, 0.37, 0.29),
	"dirtDark": Color(0.34, 0.28, 0.22),
	"colorRed": Color(0.72, 0.30, 0.28),
	"colorRedDark": Color(0.52, 0.22, 0.20),
	"colorPurple": Color(0.55, 0.47, 0.72),
	"colorYellow": Color(0.86, 0.72, 0.38),
	"colorTan": Color(0.78, 0.66, 0.50),
	"corn": Color(0.80, 0.68, 0.40),
}
const LEAVES := ["leafsGreen", "leafsDark", "leafsFall"]
const SWAY := preload("res://shaders/leaves.gdshader")

static var _mats := {}
static var _meshes := {}


static func material_for(source: Material) -> Material:
	if source == null:
		return null
	var key := source.resource_name
	if not COLORS.has(key):
		return source
	if _mats.has(key):
		return _mats[key]
	var m: Material
	if key in LEAVES:
		var sm := ShaderMaterial.new()
		sm.shader = SWAY
		sm.set_shader_parameter("albedo", COLORS[key])
		m = sm
	else:
		var st := StandardMaterial3D.new()
		st.albedo_color = COLORS[key]
		st.roughness = 0.92
		m = st
	_mats[key] = m
	return m


## A copy of the mesh with the palette's materials (cached per mesh).
static func recolor(mesh: Mesh) -> Mesh:
	if _meshes.has(mesh):
		return _meshes[mesh]
	var copy := mesh.duplicate() as Mesh
	for s in copy.get_surface_count():
		copy.surface_set_material(s, material_for(mesh.surface_get_material(s)))
	_meshes[mesh] = copy
	return copy


## Re-colours every mesh under a placed model.
static func apply(root: Node) -> void:
	for n in root.find_children("*", "MeshInstance3D", true, false):
		var mi := n as MeshInstance3D
		mi.mesh = recolor(mi.mesh)
