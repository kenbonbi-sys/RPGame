class_name SkillFx
extends RefCounted
## The Unity game's VFX names (slash, fire_explosion, ice_spike, lightning_strike, holy_strike,
## storm_circle, …) turned into code-built 3D effects by keyword, so every one of the 92 skills
## shows something fitting until hand-made effects arrive.

const FIRE := Color(1.0, 0.5, 0.18)
const ICE := Color(0.6, 0.88, 1.0)
const BOLT := Color(0.72, 0.8, 1.0)
const HOLY := Color(1.0, 0.9, 0.55)
const DARK := Color(0.62, 0.35, 0.9)
const NATURE := Color(0.5, 0.9, 0.35)
const DUST := Color(0.65, 0.58, 0.48)

static var _arc_mesh: ArrayMesh
static var _arc_mat: StandardMaterial3D


static func _world() -> Node:
	return Engine.get_main_loop().current_scene


static func color_for(vfx: String) -> Color:
	if vfx.contains("fire") or vfx.contains("meteor") or vfx.contains("flame"):
		return FIRE
	if vfx.contains("ice") or vfx.contains("frost") or vfx.contains("blizzard"):
		return ICE
	if vfx.contains("lightning") or vfx.contains("thunder") or vfx.contains("storm") or vfx.contains("chain") or vfx.contains("arcane"):
		return BOLT
	if vfx.contains("holy") or vfx.contains("light") or vfx.contains("heal") or vfx.contains("radiance"):
		return HOLY if not vfx.contains("heal") else Color(0.5, 1.0, 0.55)
	if vfx.contains("dark") or vfx.contains("curse") or vfx.contains("wisp") or vfx.contains("eclipse") or vfx.contains("void"):
		return DARK
	if vfx.contains("nature") or vfx.contains("thorn") or vfx.contains("poison") or vfx.contains("spore"):
		return NATURE
	if vfx.contains("crystal"):
		return Color(0.55, 1.0, 0.95)
	return Color(1.0, 0.95, 0.85)


## Plays an effect by name at a ground point (it adds its own height).
static func play(vfx: String, at: Vector3, dir := Vector3.FORWARD, scale := 1.0, flip := false) -> void:
	if vfx == "" or not Game.has_screen():
		return
	var col := color_for(vfx)
	var chest := at + Vector3.UP * 1.0
	if vfx.begins_with("slash"):
		arc(chest, dir, (1.9 if vfx == "slash" else 2.3) * scale * AbilityRunner.SPACE, flip, vfx == "slash_big")
	elif vfx.contains("lightning_strike") or vfx.contains("thunder_step") or vfx == "chain_bolt":
		bolt(at, col, scale)
	elif vfx.contains("ice_spike") or vfx.contains("ice_prison") or vfx.contains("ice_age"):
		spikes(at, scale * (2.2 if vfx.contains("prison") else (5.0 if vfx.contains("age") else 1.0)))
	elif vfx.contains("holy_strike") or vfx.contains("light_pillar") or vfx == "holy_hit":
		pillar(at, HOLY, scale)
	elif vfx.contains("explosion") or (vfx.contains("burst") and not vfx.contains("heal")):
		Fx.burst(chest, Vector3.UP, Color(col, 0.95), 36, 7.0 * scale, 0.6 * scale, 0.6, 90.0, -3.0, true)
		Fx.flash(chest, col, 5.0, 6.0 * scale)
	elif vfx.contains("heal"):
		Fx.burst(at + Vector3.UP * 0.3, Vector3.UP, Color(0.55, 1.0, 0.55, 0.9), 30, 3.0, 0.3, 1.0, 25.0, 2.0, true, 0.6)
	elif vfx.contains("shield") or vfx.contains("bubble"):
		bubble(chest, Color(1.0, 0.9, 0.55, 0.35), 1.3 * scale)
	elif vfx.contains("stomp") or vfx.contains("shockwave") or vfx.contains("boulder"):
		Fx.dust(at, 2.5 * scale * AbilityRunner.SPACE, 40)
	elif vfx.contains("dash") or vfx.contains("step_dust"):
		Fx.burst(at + Vector3.UP * 0.2, Vector3.UP, Color(DUST, 0.6), 14, 2.5, 0.6 * scale, 0.6, 80.0, -1.0)
	elif vfx.contains("smoke"):
		Fx.burst(chest, Vector3.UP, Color(0.5, 0.5, 0.5, 0.7), 40, 3.0, 1.6 * scale, 1.6, 90.0, 0.3)
	elif vfx.begins_with("cast_"):
		Fx.burst(chest + dir * 0.5, Vector3.UP, Color(col, 0.9), 14, 2.0, 0.3, 0.4, 90.0, 0.5, true)
		Fx.flash(chest + dir * 0.5, col, 2.0, 3.0)
	elif vfx.contains("hit"):
		Fx.burst(chest, Vector3.UP, Color(col, 0.95), 12, 4.0, 0.25, 0.35, 80.0, -6.0, true)
	elif vfx.contains("aura") or vfx.contains("circle") or vfx.contains("field") or vfx.contains("blizzard") or vfx.contains("sigil"):
		ring(at, col, 2.5 * scale * AbilityRunner.SPACE, 1.0)
	else:
		Fx.burst(chest, Vector3.UP, Color(col, 0.9), 20, 4.0, 0.4, 0.5, 80.0, -2.0, true)


## A ground ring that lasts `seconds` (area skills: storms, auras); returns it so callers can end it.
static func ring(at: Vector3, col: Color, radius: float, seconds: float) -> Node3D:
	var d := Fx.telegraph(at, radius, seconds, Color(col, 0.8))
	return d


## A sword's crescent: a flat glowing arc sweeping in front at chest height.
static func arc(at: Vector3, dir: Vector3, radius: float, flip: bool, big: bool) -> void:
	if _arc_mesh == null:
		_arc_mesh = _make_arc(150.0, 0.55, 1.0)
		_arc_mat = StandardMaterial3D.new()
		_arc_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_arc_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		_arc_mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
		_arc_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		_arc_mat.vertex_color_use_as_albedo = true
	var mi := MeshInstance3D.new()
	mi.mesh = _arc_mesh
	var mat := _arc_mat.duplicate() as StandardMaterial3D
	mat.albedo_color = Color(1.0, 0.97, 0.85, 0.9) if not big else Color(1.0, 0.9, 0.6, 1.0)
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_world().add_child(mi)
	var flat := Vector3(dir.x, 0, dir.z).normalized()
	var b := Basis.looking_at(flat, Vector3.UP)
	b = b * Basis(Vector3.FORWARD, deg_to_rad(18.0 if flip else -18.0))
	if flip:
		b = b.scaled(Vector3(-1, 1, 1))
	mi.global_transform = Transform3D(b.scaled(Vector3.ONE * radius * 0.6), at)
	var tw := mi.create_tween()
	tw.set_parallel(true)
	tw.tween_property(mi, "scale", mi.scale * 1.7, 0.18).set_ease(Tween.EASE_OUT)
	tw.tween_property(mat, "albedo_color:a", 0.0, 0.2)
	tw.chain().tween_callback(mi.queue_free)


static func _make_arc(degrees: float, inner: float, outer: float) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var n := 24
	var half := deg_to_rad(degrees) * 0.5
	for i in n:
		var a0 := -half + deg_to_rad(degrees) * i / n
		var a1 := -half + deg_to_rad(degrees) * (i + 1) / n
		# arc lies in the XZ plane, centred on -Z (forward); brighter in the middle
		var c0 := Color(1, 1, 1, 0.25 + 0.75 * sin(PI * i / n))
		var c1 := Color(1, 1, 1, 0.25 + 0.75 * sin(PI * (i + 1) / n))
		var p0i := Vector3(sin(a0) * inner, 0, -cos(a0) * inner)
		var p0o := Vector3(sin(a0) * outer, 0, -cos(a0) * outer)
		var p1i := Vector3(sin(a1) * inner, 0, -cos(a1) * inner)
		var p1o := Vector3(sin(a1) * outer, 0, -cos(a1) * outer)
		for v in [[p0i, c0], [p0o, Color(c0, 0.0)], [p1o, Color(c1, 0.0)], [p0i, c0], [p1o, Color(c1, 0.0)], [p1i, c1]]:
			st.set_color(v[1])
			st.add_vertex(v[0])
	return st.commit()


## A lightning bolt from the sky onto a point, with a flash.
static func bolt(at: Vector3, col: Color, scale := 1.0) -> void:
	var root := Node3D.new()
	_world().add_child(root)
	root.global_position = at
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(col.lerp(Color.WHITE, 0.5), 1.0)
	mat.emission_enabled = true
	mat.emission = col
	mat.emission_energy_multiplier = 6.0
	var y := 16.0
	var segs := 7
	for i in segs:
		var top := Vector3(randf_range(-0.7, 0.7) * scale, y * (segs - i) / segs, randf_range(-0.7, 0.7) * scale)
		var bottom := Vector3(randf_range(-0.5, 0.5) * scale, y * (segs - i - 1) / segs, randf_range(-0.5, 0.5) * scale) if i < segs - 1 else Vector3.ZERO
		var mi := MeshInstance3D.new()
		var bm := BoxMesh.new()
		bm.size = Vector3(0.12 * scale, top.distance_to(bottom), 0.12 * scale)
		mi.mesh = bm
		mi.material_override = mat
		root.add_child(mi)
		mi.position = (top + bottom) * 0.5
		mi.basis = GauMa._axis_basis((top - bottom).normalized())
	Fx.flash(at + Vector3.UP * 2.0, col, 8.0, 12.0 * scale, 0.25)
	Fx.burst(at + Vector3.UP * 0.3, Vector3.UP, Color(col, 1.0), 24, 7.0, 0.25, 0.4, 90.0, -6.0, true)
	var tw := root.create_tween()
	tw.tween_interval(0.08)
	tw.tween_property(mat, "albedo_color:a", 0.0, 0.12)
	tw.tween_callback(root.queue_free)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA


## Ice spikes bursting from the ground.
static func spikes(at: Vector3, scale := 1.0) -> void:
	var root := Node3D.new()
	_world().add_child(root)
	root.global_position = at
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.75, 0.92, 1.0, 0.85)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.roughness = 0.1
	mat.metallic = 0.2
	mat.emission_enabled = true
	mat.emission = Color(0.4, 0.7, 1.0)
	mat.emission_energy_multiplier = 0.6
	var count := int(clampf(5.0 * scale, 4.0, 28.0))
	for i in count:
		var mi := MeshInstance3D.new()
		var cm := CylinderMesh.new()
		cm.top_radius = 0.0
		cm.bottom_radius = randf_range(0.15, 0.3) * minf(scale, 2.0)
		cm.height = randf_range(0.9, 1.8) * minf(scale, 2.0)
		cm.radial_segments = 5
		mi.mesh = cm
		mi.material_override = mat
		var a := randf() * TAU
		var r := sqrt(randf()) * 0.9 * scale
		mi.position = Vector3(cos(a) * r, -cm.height * 0.5, sin(a) * r)
		mi.rotation = Vector3(randf_range(-0.35, 0.35), randf() * TAU, randf_range(-0.35, 0.35))
		root.add_child(mi)
		var tw := mi.create_tween()
		tw.tween_property(mi, "position:y", cm.height * 0.35, 0.08).set_ease(Tween.EASE_OUT)
	Fx.burst(at + Vector3.UP * 0.5, Vector3.UP, Color(0.7, 0.9, 1.0, 0.9), 16, 4.0, 0.25, 0.5, 70.0, -8.0, true)
	var tw := root.create_tween()
	tw.tween_interval(0.9)
	tw.tween_property(root, "scale", Vector3(1, 0.01, 1), 0.3)
	tw.tween_callback(root.queue_free)


## A pillar of light.
static func pillar(at: Vector3, col: Color, scale := 1.0) -> void:
	var mi := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.6 * scale
	cm.bottom_radius = 0.9 * scale
	cm.height = 14.0
	mi.mesh = cm
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	mat.albedo_color = Color(col, 0.7)
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_world().add_child(mi)
	mi.global_position = at + Vector3.UP * 7.0
	Fx.flash(at + Vector3.UP, col, 5.0, 8.0 * scale, 0.4)
	Fx.burst(at + Vector3.UP * 0.3, Vector3.UP, Color(col, 1.0), 20, 5.0, 0.3, 0.6, 50.0, 1.0, true)
	var tw := mi.create_tween()
	tw.tween_property(mat, "albedo_color:a", 0.0, 0.45)
	tw.tween_callback(mi.queue_free)


static func bubble(at: Vector3, col: Color, radius: float) -> void:
	var mi := MeshInstance3D.new()
	mi.mesh = CreatureArt.sphere(radius, 24)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	mat.albedo_color = col
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mi.material_override = mat
	_world().add_child(mi)
	mi.global_position = at
	var tw := mi.create_tween()
	tw.set_parallel(true)
	tw.tween_property(mi, "scale", Vector3.ONE * 1.25, 0.5)
	tw.tween_property(mat, "albedo_color:a", 0.0, 0.5)
	tw.chain().tween_callback(mi.queue_free)
