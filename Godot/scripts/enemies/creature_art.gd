class_name CreatureArt
extends RefCounted
## The bodies of the code-made creatures in the Dauntless style. Their meshes are sculpted by
## Tools/Sculpt (signed-distance shapes, layered fur, baked occlusion in the vertex colours) and
## saved as .glb; this puts them together with their materials: painted.gdshader for skin, moss and
## bark, the jelly shader for slimes, glowing lamps for eyes. Faces look along -Z (the creature's
## front). Every material belongs to one creature, so `hurt` can flash just that one white.

const JELLY := preload("res://shaders/jelly.gdshader")
const PAINTED := preload("res://shaders/painted.gdshader")


## A painted material; colour comes from `color` times the mesh's vertex colours.
static func painted(color: Color, paint := 0.15, moss := 0.0, roughness := 0.8) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = PAINTED
	m.set_shader_parameter("albedo", color)
	m.set_shader_parameter("paint", paint)
	m.set_shader_parameter("paint_scale", 3.0)
	m.set_shader_parameter("moss_amount", moss)
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


## Meshes sculpted by Tools/Sculpt/creatures.py, by part name, loaded once per file.
static var _sculpts := {}


static func sculpt(path: String) -> Dictionary:
	if _sculpts.has(path):
		return _sculpts[path]
	var out := {}
	var scene: Node = (load(path) as PackedScene).instantiate()
	for n in scene.find_children("*", "MeshInstance3D", true, false):
		out[String(n.name)] = (n as MeshInstance3D).mesh
	scene.free()
	_sculpts[path] = out
	return out


## A glowing colour (eye glints, glowing eyes, mushroom caps) that still flashes on hits like the rest.
static func lamp(color: Color, energy := 1.0) -> ShaderMaterial:
	var m := painted(color, 0.0)
	m.set_shader_parameter("glow", color)
	m.set_shader_parameter("glow_energy", energy)
	m.set_shader_parameter("specular_amount", 0.0)
	return m


## Slime Rêu: glossy jelly sagging into a drippy skirt, angry eyes, a grumpy mouth, a thick moss
## cap with hanging strands, sprouts and a red toadstool, pebbles and a twig inside. Returns the
## body (scale it to squash and stretch).
static func moss_slime(root: Node3D, radius := 0.75) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var parts := sculpt("res://assets/creatures/slime.glb")
	var jelly := ShaderMaterial.new()
	jelly.shader = JELLY
	jelly.set_shader_parameter("moss_line", 2.0)
	# mint, so the jelly stands apart from the grass and its own moss
	jelly.set_shader_parameter("jelly", Color(0.36, 0.82, 0.66))
	jelly.set_shader_parameter("core", Color(0.08, 0.36, 0.3))
	var blob := part(body, parts["Jelly"], jelly, Vector3.ZERO, Vector3.ONE * radius)
	blob.name = "Blob"
	part(body, parts["Body"], painted(Color.WHITE, 0.12, 0.0, 0.75), Vector3.ZERO, Vector3.ONE * radius)
	var glint := part(body, parts["Glints"], lamp(Color.WHITE, 1.5), Vector3.ZERO, Vector3.ONE * radius)
	glint.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return body


## Nấm Độc: a pot-bellied toadstool with a droopy purple cap tipped over glowing eyes, warts,
## gills, a skin ring, a fanged grin, stubby arms and root feet. Returns the body; the cap is "Cap".
static func poison_shroom(root: Node3D, size := 1.0) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var parts := sculpt("res://assets/creatures/shroom.glb")
	part(body, parts["Body"], painted(Color.WHITE, 0.12, 0.0, 0.8), Vector3.ZERO, Vector3.ONE * size)
	var eyes := part(body, parts["Eyes"], lamp(Color(0.85, 1.0, 0.35), 2.2), Vector3.ZERO, Vector3.ONE * size)
	eyes.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var cap := part(body, parts["Cap"], painted(Color.WHITE, 0.12, 0.0, 0.6), Vector3(0, 1.06 * size, 0), Vector3.ONE * size)
	cap.name = "Cap"
	return body


## Flashes every creature material on a model white for a moment.
static func set_hurt(model: Node, amount: float) -> void:
	for n in model.find_children("*", "MeshInstance3D", true, false):
		var m := (n as MeshInstance3D).material_override
		if m is ShaderMaterial:
			(m as ShaderMaterial).set_shader_parameter("hurt", amount)
