class_name Fx
extends RefCounted
## One-shot effects built from code: particle bursts (dust, ichor, sparks), the red warning ring
## on the ground before a colossus's attack (a Decal, so it follows the slope), and flashes of light.

static var _dot: ImageTexture
static var _ring: ImageTexture
static var _disc: ImageTexture
static var _quad_mats := {}


## The settings' "Hiệu Ứng Đặc Biệt": how many particles a burst throws (1 = all).
static var detail := 1.0


static func _world() -> Node:
	return Engine.get_main_loop().current_scene


static func dot_texture() -> ImageTexture:
	if _dot == null:
		var n := 64
		var img := Image.create(n, n, false, Image.FORMAT_RGBA8)
		for y in n:
			for x in n:
				var d := Vector2(x + 0.5 - n * 0.5, y + 0.5 - n * 0.5).length() / (n * 0.5)
				var a := clampf(1.0 - d, 0.0, 1.0)
				img.set_pixel(x, y, Color(1, 1, 1, a * a))
		_dot = ImageTexture.create_from_image(img)
	return _dot


static func ring_texture() -> ImageTexture:
	if _ring == null:
		var n := 256
		var img := Image.create(n, n, false, Image.FORMAT_RGBA8)
		for y in n:
			for x in n:
				var d := Vector2(x + 0.5 - n * 0.5, y + 0.5 - n * 0.5).length() / (n * 0.5)
				var edge := clampf(1.0 - absf(d - 0.94) / 0.05, 0.0, 1.0)
				var fill := 0.18 if d < 0.94 else 0.0
				# colour premultiplied by alpha: decal emission reads only RGB
				var a := maxf(edge, fill)
				img.set_pixel(x, y, Color(a, a, a, a))
		_ring = ImageTexture.create_from_image(img)
	return _ring


static func disc_texture() -> ImageTexture:
	if _disc == null:
		var n := 128
		var img := Image.create(n, n, false, Image.FORMAT_RGBA8)
		for y in n:
			for x in n:
				var d := Vector2(x + 0.5 - n * 0.5, y + 0.5 - n * 0.5).length() / (n * 0.5)
				var a := 0.35 if d < 0.97 else 0.0
				img.set_pixel(x, y, Color(a, a, a, a))
		_disc = ImageTexture.create_from_image(img)
	return _disc


static func _quad_material(additive: bool) -> StandardMaterial3D:
	if _quad_mats.has(additive):
		return _quad_mats[additive]
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD if additive else BaseMaterial3D.BLEND_MODE_MIX
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_texture = dot_texture()
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	_quad_mats[additive] = m
	return m


## A one-shot spray of soft particles.
static func burst(pos: Vector3, dir: Vector3, color: Color, amount := 24, speed := 6.0, size := 0.5,
		life := 1.0, spread := 35.0, gravity := -6.0, additive := false, ring_radius := 0.0) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.one_shot = true
	p.amount = maxi(1, roundi(amount * detail))
	p.lifetime = life
	p.explosiveness = 0.95
	p.local_coords = false
	var pm := ParticleProcessMaterial.new()
	pm.direction = dir.normalized() if dir.length() > 0.01 else Vector3.UP
	pm.spread = spread
	pm.initial_velocity_min = speed * 0.5
	pm.initial_velocity_max = speed
	pm.gravity = Vector3(0, gravity, 0)
	pm.damping_min = 1.0
	pm.damping_max = 3.0
	pm.scale_min = 0.6
	pm.scale_max = 1.3
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.6))
	curve.add_point(Vector2(0.25, 1.0))
	curve.add_point(Vector2(1, 0.0))
	var ct := CurveTexture.new()
	ct.curve = curve
	pm.scale_curve = ct
	var grad := Gradient.new()
	grad.set_color(0, color)
	grad.set_color(1, Color(color, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = grad
	pm.color_ramp = gt
	if ring_radius > 0.0:
		pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_RING
		pm.emission_ring_axis = Vector3.UP
		pm.emission_ring_radius = ring_radius
		pm.emission_ring_inner_radius = ring_radius * 0.8
		pm.emission_ring_height = 0.2
		pm.radial_velocity_min = speed * 0.4
		pm.radial_velocity_max = speed
	p.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2.ONE * size
	q.material = _quad_material(additive)
	p.draw_pass_1 = q
	p.visibility_aabb = AABB(Vector3(-30, -10, -30), Vector3(60, 40, 60))
	_world().add_child(p)
	p.global_position = pos
	p.emitting = true
	p.get_tree().create_timer(life + 0.6).timeout.connect(p.queue_free)
	return p


## Hot little streaks shot out in a narrow cone, the way a blow went.
static func sparks(pos: Vector3, dir: Vector3, color: Color, amount := 10, speed := 10.0) -> void:
	burst(pos, dir, color, amount, speed, 0.2, 0.32, 24.0, -12.0, false)
	burst(pos, dir, Color(1, 1, 1, 0.9), amount / 2, speed * 1.2, 0.12, 0.2, 14.0, -8.0, true)


static var _star_mat: StandardMaterial3D


## A bright star that pops at the point of impact and is gone in a tenth of a second.
static func impact_star(pos: Vector3, color: Color, size := 1.0) -> void:
	if not Game.has_screen():
		return
	if _star_mat == null:
		_star_mat = StandardMaterial3D.new()
		_star_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_star_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		_star_mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
		_star_mat.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
		_star_mat.no_depth_test = true
		_star_mat.vertex_color_use_as_albedo = true
		_star_mat.albedo_texture = GameIcons.icon("fx:star", [["sparkle", "main", 50, 50, 1.0, 0]], "holy", 64)
	var mi := MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = Vector2.ONE * size
	mi.mesh = q
	var m := _star_mat.duplicate() as StandardMaterial3D
	m.albedo_color = color
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_world().add_child(mi)
	mi.global_position = pos
	mi.rotation.z = randf() * TAU
	mi.scale = Vector3.ONE * 0.35
	var tw := mi.create_tween()
	tw.set_ignore_time_scale(true)
	tw.set_parallel(true)
	tw.tween_property(mi, "scale", Vector3.ONE * 1.15, 0.09).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	tw.tween_property(m, "albedo_color:a", 0.0, 0.12).set_delay(0.03)
	tw.chain().tween_callback(mi.queue_free)


static func dust(pos: Vector3, radius := 3.0, amount := 40) -> void:
	burst(pos + Vector3.UP * 0.3, Vector3.UP, Color(0.62, 0.56, 0.46, 0.55), amount, 4.0, radius * 0.9, 1.6, 70.0, -0.6, false, radius)


static func flash(pos: Vector3, color: Color, energy := 4.0, light_range := 8.0, life := 0.35) -> void:
	var l := OmniLight3D.new()
	l.light_color = color
	l.light_energy = energy
	l.omni_range = light_range
	l.shadow_enabled = false
	_world().add_child(l)
	l.global_position = pos
	var tw := l.create_tween()
	tw.tween_property(l, "light_energy", 0.0, life)
	tw.tween_callback(l.queue_free)


## A warning ring on the ground that fills up until the attack lands.
static func telegraph(pos: Vector3, radius: float, duration: float, color := Color(1.0, 0.18, 0.12)) -> Node3D:
	var root := Node3D.new()
	_world().add_child(root)
	root.global_position = pos
	var ring := Decal.new()
	ring.texture_albedo = ring_texture()
	ring.texture_emission = ring_texture()
	ring.emission_energy = 1.4
	ring.modulate = color
	ring.size = Vector3(radius * 2.0, 16.0, radius * 2.0)
	ring.upper_fade = 0.1
	ring.cull_mask = 1
	ring.lower_fade = 0.1
	root.add_child(ring)
	var fill := Decal.new()
	fill.texture_albedo = disc_texture()
	fill.texture_emission = disc_texture()
	fill.emission_energy = 1.0
	fill.modulate = color
	fill.cull_mask = 1
	fill.size = Vector3(0.1, 16.0, 0.1)
	root.add_child(fill)
	var tw := root.create_tween()
	tw.tween_property(fill, "size", Vector3(radius * 2.0, 16.0, radius * 2.0), duration)
	tw.tween_callback(root.queue_free)
	return root
