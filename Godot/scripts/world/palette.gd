class_name Palette
extends RefCounted
## Re-colours the Kenney Nature Kit (bright pastel, faceted) into the game's Dauntless-like palette
## by the kit's material names: canopies become soft painted blobs that sway (leaves.gdshader),
## stone gets moss on its tops (rock.gdshader), wood and flowers turn warm and saturated.

const COLORS := {
	"leafsGreen": Color(0.30, 0.50, 0.20),
	"leafsDark": Color(0.20, 0.40, 0.20),
	"leafsFall": Color(0.80, 0.48, 0.18),
	"grass": Color(0.38, 0.56, 0.22),
	"wood": Color(0.52, 0.36, 0.22),
	"woodBark": Color(0.40, 0.28, 0.19),
	"woodBarkDark": Color(0.30, 0.22, 0.16),
	"woodDark": Color(0.34, 0.24, 0.16),
	"woodBirch": Color(0.84, 0.80, 0.70),
	"woodInner": Color(0.76, 0.60, 0.40),
	"stone": Color(0.60, 0.56, 0.50),
	"stoneDark": Color(0.46, 0.45, 0.44),
	"dirt": Color(0.56, 0.41, 0.27),
	"dirtDark": Color(0.42, 0.31, 0.22),
	"colorRed": Color(0.85, 0.25, 0.22),
	"colorRedDark": Color(0.60, 0.18, 0.16),
	"colorPurple": Color(0.62, 0.42, 0.85),
	"colorYellow": Color(0.98, 0.78, 0.30),
	"colorTan": Color(0.86, 0.70, 0.50),
	"corn": Color(0.90, 0.74, 0.36),
}
## leaves: [top colour, underside colour]
const LEAF_TONES := {
	"leafsGreen": [Color(0.66, 0.80, 0.30), Color(0.08, 0.27, 0.23)],
	"leafsDark": [Color(0.46, 0.62, 0.26), Color(0.05, 0.20, 0.18)],
	"leafsFall": [Color(0.98, 0.72, 0.30), Color(0.45, 0.18, 0.12)],
}
const STONES := ["stone", "stoneDark"]
const SWAY := preload("res://shaders/leaves.gdshader")
const ROCK := preload("res://shaders/rock.gdshader")

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
	if LEAF_TONES.has(key):
		var sm := ShaderMaterial.new()
		sm.shader = SWAY
		sm.set_shader_parameter("albedo", COLORS[key])
		sm.set_shader_parameter("top_color", LEAF_TONES[key][0])
		sm.set_shader_parameter("under_color", LEAF_TONES[key][1])
		m = sm
	elif key in STONES:
		var rm := ShaderMaterial.new()
		rm.shader = ROCK
		rm.set_shader_parameter("albedo", COLORS[key])
		m = rm
	else:
		var st := StandardMaterial3D.new()
		st.albedo_color = COLORS[key]
		st.roughness = 0.9
		st.rim_enabled = true
		st.rim = 0.2
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
