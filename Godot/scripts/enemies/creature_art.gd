class_name CreatureArt
extends RefCounted
## Builds the bodies of the code-made creatures (until real models come): the moss slime, the
## poison mushroom, and helpers for eyes and simple limbs. Every material can flash white
## (`set_hurt`) when the creature is struck.

const JELLY := preload("res://shaders/jelly.gdshader")
const SKIN := preload("res://shaders/creature.gdshader")


static func skin(color: Color, spots := 0.0, spot_color := Color(0.95, 0.92, 0.82), roughness := 0.85) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = SKIN
	m.set_shader_parameter("albedo", color)
	m.set_shader_parameter("spots", spots)
	m.set_shader_parameter("spot_color", spot_color)
	m.set_shader_parameter("roughness", roughness)
	return m


static func part(parent: Node3D, mesh: Mesh, mat: Material, pos: Vector3, scale := Vector3.ONE, rot := Vector3.ZERO) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	mi.rotation = rot
	mi.scale = scale
	parent.add_child(mi)
	return mi


static func sphere(r := 1.0, segments := 20) -> SphereMesh:
	var s := SphereMesh.new()
	s.radius = r
	s.height = r * 2.0
	s.radial_segments = segments
	s.rings = maxi(6, segments / 2)
	return s


static func capsule(r: float, h: float) -> CapsuleMesh:
	var c := CapsuleMesh.new()
	c.radius = r
	c.height = h
	c.radial_segments = 16
	c.rings = 6
	return c


## Two eyes looking along -Z (the creature's front): white balls with dark pupils.
static func eyes(parent: Node3D, at: Vector3, spacing: float, size: float, glow := Color(0, 0, 0, 0)) -> void:
	var white := StandardMaterial3D.new()
	white.albedo_color = Color(0.95, 0.95, 0.9) if glow.a == 0.0 else glow
	white.roughness = 0.3
	if glow.a > 0.0:
		white.emission_enabled = true
		white.emission = glow
		white.emission_energy_multiplier = 3.0
	var dark := StandardMaterial3D.new()
	dark.albedo_color = Color(0.05, 0.05, 0.06)
	dark.roughness = 0.2
	for s in [-1.0, 1.0]:
		var p := at + Vector3(s * spacing * 0.5, 0, 0)
		part(parent, sphere(size, 12), white, p)
		if glow.a == 0.0:
			part(parent, sphere(size * 0.55, 10), dark, p + Vector3(0, 0, -size * 0.6))


## Slime Rêu: a mossy jelly blob. Returns the body (scale it to squash and stretch).
static func moss_slime(root: Node3D, radius := 0.75) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var mat := ShaderMaterial.new()
	mat.shader = JELLY
	var blob := part(body, sphere(radius, 24), mat, Vector3(0, radius * 0.85, 0), Vector3(1.0, 0.85, 1.0))
	blob.name = "Blob"
	blob.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	eyes(body, Vector3(0, radius * 1.0, -radius * 0.78), radius * 0.62, radius * 0.17)
	# a few moss tufts and a sprout on top
	var leaf := StandardMaterial3D.new()
	leaf.albedo_color = Color(0.28, 0.45, 0.18)
	leaf.roughness = 0.9
	var cone := CylinderMesh.new()
	cone.top_radius = 0.0
	cone.bottom_radius = radius * 0.12
	cone.height = radius * 0.45
	cone.radial_segments = 5
	for i in 7:
		var a := i * TAU / 7.0
		part(body, cone, leaf, Vector3(cos(a) * radius * 0.45, radius * 1.45, sin(a) * radius * 0.45),
			Vector3.ONE, Vector3(sin(a) * 0.5, 0, -cos(a) * 0.5))
	return body


## Nấm Độc: a walking toadstool with a spotted purple cap. Returns the body.
static func poison_shroom(root: Node3D, size := 1.0) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var stem_mat := skin(Color(0.86, 0.80, 0.68), 0.0)
	var cap_mat := skin(Color(0.48, 0.20, 0.52), 3.2, Color(0.95, 0.92, 0.75), 0.6)
	part(body, capsule(0.32 * size, 1.1 * size), stem_mat, Vector3(0, 0.6 * size, 0))
	var cap := part(body, sphere(0.72 * size, 22), cap_mat, Vector3(0, 1.15 * size, 0), Vector3(1.0, 0.55, 1.0))
	cap.name = "Cap"
	var gill := StandardMaterial3D.new()
	gill.albedo_color = Color(0.62, 0.52, 0.48)
	var disc := CylinderMesh.new()
	disc.top_radius = 0.66 * size
	disc.bottom_radius = 0.5 * size
	disc.height = 0.1 * size
	part(body, disc, gill, Vector3(0, 1.0 * size, 0))
	eyes(body, Vector3(0, 0.78 * size, -0.28 * size), 0.22 * size, 0.07 * size)
	for s in [-1.0, 1.0]:
		var foot := part(body, sphere(0.16 * size, 10), stem_mat, Vector3(s * 0.2 * size, 0.1 * size, 0), Vector3(1.0, 0.7, 1.3))
		foot.name = "FootL" if s < 0 else "FootR"
	return body


## Flashes every creature material on a model white for a moment.
static func set_hurt(model: Node, amount: float) -> void:
	for n in model.find_children("*", "MeshInstance3D", true, false):
		var m := (n as MeshInstance3D).material_override
		if m is ShaderMaterial:
			(m as ShaderMaterial).set_shader_parameter("hurt", amount)
