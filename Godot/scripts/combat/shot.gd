class_name Shot
extends Node3D
## A flying projectile (the Unity game's Projectile.cs): goes straight, stops on rock and trees,
## hits the first enemy of its team within `radius` (or pierces), may burst in an area, carries a
## DamageInfo template. Used by monster spores now and by hero spells.

var team := Combat.Team.ENEMY
var source: Node
var hit: DamageInfo
var speed := 10.0
var lifetime := 2.0
var radius := 0.3
var pierce := false
## > 0: bursts on impact, hitting everyone around.
var explode_radius := 0.0
var crit_chance := 0.0
var color := Color(1, 1, 1)
var size := 0.35
var gravity := 0.0
## Effects on impact (Fx names), optional.
var on_hit: Callable

var _dir := Vector3.FORWARD
var _vel := Vector3.ZERO
var _age := 0.0
var _hit_list: Array[Health] = []
var _done := false


func launch(from: Vector3, dir: Vector3) -> void:
	global_position = from
	_dir = dir.normalized()
	_vel = _dir * speed
	_build()


func _build() -> void:
	var mi := MeshInstance3D.new()
	var s := SphereMesh.new()
	s.radius = size
	s.height = size * 2.0
	s.radial_segments = 12
	s.rings = 6
	mi.mesh = s
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = color
	m.emission_enabled = true
	m.emission = color
	m.emission_energy_multiplier = 2.0
	mi.material_override = m
	mi.layers = 2
	add_child(mi)
	var trail := GPUParticles3D.new()
	trail.amount = 24
	trail.lifetime = 0.4
	trail.local_coords = false
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3.UP
	pm.spread = 180.0
	pm.initial_velocity_min = 0.1
	pm.initial_velocity_max = 0.5
	pm.gravity = Vector3.ZERO
	var grad := Gradient.new()
	grad.set_color(0, Color(color, 0.8))
	grad.set_color(1, Color(color, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = grad
	pm.color_ramp = gt
	trail.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2.ONE * size * 1.6
	q.material = Fx._quad_material(true)
	trail.draw_pass_1 = q
	add_child(trail)
	var l := OmniLight3D.new()
	l.light_color = color
	l.light_energy = 0.8
	l.omni_range = 3.0
	add_child(l)


func _physics_process(delta: float) -> void:
	if _done:
		return
	_age += delta
	_vel.y -= gravity * delta
	var from := global_position
	var to := from + _vel * delta
	# rock, trees, the ground
	var q := PhysicsRayQueryParameters3D.create(from, to, Combat.LAYER_WORLD)
	var wall := get_world_3d().direct_space_state.intersect_ray(q)
	if not wall.is_empty():
		global_position = wall.position
		_impact(null)
		return
	global_position = to
	for h in Combat.healths_in_sphere(get_world_3d(), to, radius + 0.45, team, _hit_list):
		_hit_list.append(h)
		if explode_radius <= 0.0:
			_apply(h)
		if not pierce or explode_radius > 0.0:
			_impact(h)
			return
	if _age >= lifetime:
		_impact(null)


func _apply(h: Health) -> void:
	var d := hit.copy()
	d.point = h.hit_point(global_position)
	d.direction = Vector3(_dir.x, 0, _dir.z).normalized()
	d.roll_crit(crit_chance)
	if h.take_damage(d) > 0.0:
		Combat.on_hit_feedback(h, d)


func _impact(_target: Health) -> void:
	_done = true
	if explode_radius > 0.0:
		var d := hit.copy()
		Combat.damage_circle(get_world_3d(), global_position, explode_radius, d, crit_chance)
		Fx.burst(global_position, Vector3.UP, color, 30, 6.0, 0.5, 0.6, 90.0, -2.0, true)
		Fx.flash(global_position, color, 4.0, explode_radius * 3.0)
	else:
		Fx.burst(global_position, -_dir + Vector3.UP * 0.3, color, 12, 3.0, 0.3, 0.5, 70.0, -3.0, true)
	if on_hit.is_valid():
		on_hit.call(global_position)
	queue_free()
