class_name CreatureArt
extends RefCounted
## Builds the bodies of the code-made creatures in the Dauntless style: chunky painted shapes
## (MeshKit lumps and tubes, painted.gdshader), big readable eyes with a glint and brows. The moss
## slime is see-through jelly with pebbles inside and a mossy cap; the poison mushroom has a lumpy
## cap with raised spots, gills, stubby arms and root feet. Faces look along -Z (the creature's
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


## Kept for older callers: a plain painted skin.
static func skin(color: Color, _spots := 0.0, _spot_color := Color.WHITE, roughness := 0.85) -> ShaderMaterial:
	return painted(color, 0.15, 0.0, roughness)


static func part(parent: Node3D, mesh: Mesh, mat: Material, pos: Vector3, scale := Vector3.ONE, rot := Vector3.ZERO) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	mi.rotation = rot
	mi.scale = scale
	parent.add_child(mi)
	return mi


## Commits a MeshKit as one part.
static func built(parent: Node3D, kit: MeshKit, mat: Material, pos := Vector3.ZERO) -> MeshInstance3D:
	var mesh := ArrayMesh.new()
	kit.commit(mesh, null)
	return part(parent, mesh, mat, pos)


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


static func _solid(c: Color) -> Callable:
	return func(_p: Vector3, _centre: Vector3) -> Color:
		return c


## Two eyes looking along -Z: white balls, dark pupils, a glint; `brow` > 0 adds a frowning brow
## of that tilt (radians). A glowing colour instead makes them lamps.
static func eyes(parent: Node3D, at: Vector3, spacing: float, size: float, glow := Color(0, 0, 0, 0), brow := 0.0) -> void:
	var white := StandardMaterial3D.new()
	white.albedo_color = Color(0.97, 0.96, 0.92) if glow.a == 0.0 else glow
	white.roughness = 0.25
	if glow.a > 0.0:
		white.emission_enabled = true
		white.emission = glow
		white.emission_energy_multiplier = 3.0
	var dark := StandardMaterial3D.new()
	dark.albedo_color = Color(0.04, 0.04, 0.06)
	dark.roughness = 0.15
	var glint := StandardMaterial3D.new()
	glint.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	glint.albedo_color = Color.WHITE
	for s in [-1.0, 1.0]:
		var p := at + Vector3(s * spacing * 0.5, 0, 0)
		part(parent, sphere(size, 14), white, p, Vector3(1.0, 1.15, 0.8))
		if glow.a == 0.0:
			part(parent, sphere(size * 0.55, 10), dark, p + Vector3(s * size * 0.08, -size * 0.05, -size * 0.55))
			part(parent, sphere(size * 0.17, 6), glint, p + Vector3(s * size * 0.08 + size * 0.2, size * 0.22, -size * 0.92))
		if brow > 0.0:
			var bm := BoxMesh.new()
			bm.size = Vector3(size * 1.9, size * 0.45, size * 0.5)
			part(parent, bm, dark, p + Vector3(0, size * 1.15, -size * 0.45), Vector3.ONE, Vector3(0, 0, s * brow))


## Slime Rêu: a mossy jelly blob. Returns the body (scale it to squash and stretch).
static func moss_slime(root: Node3D, radius := 0.75) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var mat := ShaderMaterial.new()
	mat.shader = JELLY
	var blob := part(body, sphere(radius, 28), mat, Vector3(0, radius * 0.85, 0), Vector3(1.0, 0.85, 1.0))
	blob.name = "Blob"
	blob.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	var rng := RandomNumberGenerator.new()
	rng.seed = 41
	# pebbles and a twig caught inside, seen through the jelly
	var inside := MeshKit.new()
	for i in 3:
		var a := TAU * i / 3.0 + 0.6
		inside.lump(Vector3(1.0, 0.7, 0.9) * radius * rng.randf_range(0.14, 0.22), 1,
			Transform3D(Basis(Vector3.UP, a), Vector3(cos(a) * radius * 0.35, radius * rng.randf_range(0.45, 0.8), sin(a) * radius * 0.3)),
			0.2, i, _solid(Color(0.62, 0.6, 0.55)))
	inside.tube(radius * 0.05, radius * 0.03, radius * 0.9, 5, Transform3D(Basis(Vector3.BACK, 1.2), Vector3(-radius * 0.3, radius * 0.6, 0.1)),
		Color(0.55, 0.4, 0.28))
	built(body, inside, painted(Color(0.85, 0.85, 0.85), 0.2))
	# a cap of moss with sprouts and a tiny red toadstool on top
	var top_y := radius * 0.85 + radius * 0.85 * 0.82
	var moss := MeshKit.new()
	var greens := [Color(0.45, 0.68, 0.26), Color(0.36, 0.58, 0.22), Color(0.55, 0.75, 0.3)]
	for i in 6:
		var a := TAU * i / 6.0 + rng.randf_range(-0.3, 0.3)
		var r := radius * rng.randf_range(0.25, 0.5)
		var at := Vector3(cos(a) * r, top_y - r * 0.55, sin(a) * r)
		moss.lump(Vector3(1.0, 0.55, 1.0) * radius * rng.randf_range(0.2, 0.3), 1, Transform3D(Basis.IDENTITY, at), 0.25, i + 3,
			_solid(greens[i % greens.size()]))
	moss.lump(Vector3(1.0, 0.6, 1.0) * radius * 0.32, 1, Transform3D(Basis.IDENTITY, Vector3(0, top_y + radius * 0.02, 0)), 0.25, 11, _solid(greens[2]))
	for s in [-1.0, 1.0]:
		moss.lump(Vector3(0.06, 0.3, 0.14) * radius, 1, Transform3D(Basis(Vector3.BACK, s * 0.6), Vector3(s * radius * 0.12, top_y + radius * 0.3, radius * 0.05)),
			0.05, 20, _solid(Color(0.62, 0.86, 0.34)))
	moss.tube(radius * 0.05, radius * 0.04, radius * 0.22, 6, Transform3D(Basis.IDENTITY, Vector3(radius * 0.28, top_y, radius * 0.18)), Color(0.95, 0.92, 0.84))
	moss.lump(Vector3(1.0, 0.6, 1.0) * radius * 0.13, 1, Transform3D(Basis.IDENTITY, Vector3(radius * 0.28, top_y + radius * 0.24, radius * 0.18)), 0.05, 30,
		_solid(Color(0.92, 0.26, 0.2)))
	built(body, moss, painted(Color.WHITE, 0.18))
	eyes(body, Vector3(0, radius * 0.98, -radius * 0.9), radius * 0.62, radius * 0.18, Color(0, 0, 0, 0), 0.35)
	return body


## Nấm Độc: a walking toadstool with a lumpy purple cap. Returns the body; the cap is "Cap".
static func poison_shroom(root: Node3D, size := 1.0) -> Node3D:
	var body := Node3D.new()
	body.name = "Body"
	root.add_child(body)
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var s := size
	# stem with stubby arms
	var stem := MeshKit.new()
	stem.tube(0.36 * s, 0.25 * s, 1.0 * s, 12, Transform3D(Basis.IDENTITY, Vector3(0, 0.08 * s, 0)), Color(0.86, 0.8, 0.7), Vector3(0, 0, 0.04 * s), 3,
		Color(1.0, 0.97, 0.9))
	stem.lump(Vector3(0.36, 0.2, 0.36) * s, 2, Transform3D(Basis.IDENTITY, Vector3(0, 0.12 * s, 0)), 0.15, 2, _solid(Color(0.8, 0.74, 0.64)))
	for side in [-1.0, 1.0]:
		var sh := Vector3(side * 0.27 * s, 0.7 * s, -0.02 * s)
		stem.tube(0.07 * s, 0.05 * s, 0.38 * s, 6, Transform3D(Basis(Vector3.BACK, side * 2.3), sh), Color(0.9, 0.85, 0.75), Vector3(0, 0, -0.08 * s))
		stem.lump(Vector3.ONE * 0.08 * s, 1, Transform3D(Basis.IDENTITY, sh + Vector3(side * 0.28 * s, -0.27 * s, -0.08 * s)), 0.1, 5, _solid(Color(0.92, 0.88, 0.8)))
	built(body, stem, painted(Color(0.95, 0.9, 0.82), 0.14))
	for side in [-1.0, 1.0]:
		var foot := MeshKit.new()
		foot.lump(Vector3(0.17, 0.11, 0.24) * s, 1, Transform3D.IDENTITY, 0.15, 9, _solid(Color(0.78, 0.7, 0.6)))
		var f := built(body, foot, painted(Color(0.9, 0.86, 0.78), 0.14), Vector3(side * 0.2 * s, 0.07 * s, -0.04 * s))
		f.name = "FootL" if side < 0 else "FootR"
	# the cap: a lumpy dome, raised spots, gills underneath
	var cap := MeshKit.new()
	var purple_top := Color(0.66, 0.34, 0.74)
	var purple_low := Color(0.38, 0.14, 0.42)
	var cap_r := Vector3(0.8, 0.45, 0.8) * s
	var dome := func(p: Vector3, _c: Vector3) -> Color:
		var h := clampf(p.y / cap_r.y * 0.5 + 0.5, 0.0, 1.0)
		return purple_low.lerp(purple_top, h)
	cap.lump(cap_r, 2, Transform3D.IDENTITY, 0.07, 4, dome, Vector3.INF, 0.0, 0.2)
	for i in 10:
		var a := rng.randf() * TAU
		var up := rng.randf_range(0.25, 0.95)
		var dir := Vector3(cos(a) * sqrt(1.0 - up * up), up, sin(a) * sqrt(1.0 - up * up))
		var at := dir * cap_r * 0.97
		var n := (dir / cap_r).normalized()
		var r := rng.randf_range(0.08, 0.14) * s
		cap.lump(Vector3(r, r * 0.45, r), 1, Transform3D(_basis_y(n), at), 0.1, i + 40, _solid(Color(0.98, 0.94, 0.8)))
	cap.tube(0.74 * s, 0.32 * s, 0.14 * s, 16, Transform3D(Basis(Vector3.RIGHT, PI), Vector3(0, -0.02 * s, 0)), Color(0.72, 0.56, 0.6), Vector3.ZERO, 1,
		Color(0.55, 0.4, 0.45))
	var cap_mi := built(body, cap, painted(Color.WHITE, 0.12, 0.0, 0.6), Vector3(0, 1.12 * s, 0))
	cap_mi.name = "Cap"
	eyes(body, Vector3(0, 0.8 * s, -0.27 * s), 0.22 * s, 0.075 * s, Color(0, 0, 0, 0), 0.3)
	return body


static func _basis_y(dir: Vector3) -> Basis:
	var y := dir.normalized()
	var x := (Vector3.FORWARD if absf(y.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT).cross(y).normalized()
	return Basis(x, y, x.cross(y).normalized())


## Flashes every creature material on a model white for a moment.
static func set_hurt(model: Node, amount: float) -> void:
	for n in model.find_children("*", "MeshInstance3D", true, false):
		var m := (n as MeshInstance3D).material_override
		if m is ShaderMaterial:
			(m as ShaderMaterial).set_shader_parameter("hurt", amount)
