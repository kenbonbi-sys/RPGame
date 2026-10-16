class_name Shroom
extends Enemy
## Nấm Độc (ShroomAI.cs): keeps its distance — backs off when crowded, walks in when far, sidesteps
## in between — and shoots a slowing spore when the hero is in range and in sight. Weak to fire.
## The shot is told (Anticipation): a small nod, then it rears back while its cap swells and a
## glowing spore gathers in front of it, a quiver and a glint at the peak, then it snaps forward
## and spits.
## Ranges are the Unity ones × RANGE (the 3D camera sees further).

const RANGE := 1.3

@export var preferred_distance := 4.2
@export var shoot_range := 6.5
@export var spore_damage := 9.0
@export var spore_speed := 5.5

var _fired := false
var _seed := 0.0
var _body: Node3D
var _cap: Node3D
var _orb: MeshInstance3D
## The shot's beats: 0.64 s of warning (the 2D game's was 0.32 s).
var beats := Anticipation.Beats.new(0.1, 0.42, 0.12)
const RECOVER := 0.4


func _init() -> void:
	enemy_id = "shroom"
	display_name = "Nấm Độc"
	element = "umbral"
	level = 3
	max_hp = 48.0
	move_speed = 1.9
	aggro_range = 7.5
	leash_range = 13.0
	attack_cooldown = 2.2
	contact_damage = 0.0
	wander_radius = 2.5
	body_radius = 0.45
	body_height = 1.4
	head_height = 1.6
	resistances = PackedFloat32Array([0, -0.3, 0, 0, 0, 0, 0])
	loot = [
		{"item": "shroom_cap", "chance": 0.75, "min": 1, "max": 1},
		{"item": "coin", "chance": 0.8, "min": 1, "max": 3},
		{"item": "potion_blue", "chance": 0.1, "min": 1, "max": 1},
		{"item": "herb", "chance": 0.25, "min": 1, "max": 1},
	]


func build_model(root: Node3D) -> void:
	_body = CreatureArt.poison_shroom(root, 1.0)
	_cap = _body.get_node("Cap")
	_seed = randf() * 100.0
	# the spore it gathers before spitting
	_orb = MeshInstance3D.new()
	_orb.mesh = CreatureArt.sphere(0.16, 12)
	var glow := StandardMaterial3D.new()
	glow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	glow.albedo_color = Color(0.85, 0.55, 1.0)
	glow.emission_enabled = true
	glow.emission = Color(0.75, 0.4, 1.0)
	glow.emission_energy_multiplier = 3.0
	_orb.material_override = glow
	_orb.position = Vector3(0, 1.12, -0.42)
	_orb.visible = false
	_body.add_child(_orb)


func chase(t: Node3D, dist: float, _delta: float) -> void:
	var pref := preferred_distance * RANGE
	face(t.global_position)
	if dist <= shoot_range * RANGE and ready_to_attack() and _can_see(t):
		stop()
		_fired = false
		set_state(State.ATTACK)
	elif dist < pref * 0.65:
		steer(global_position - t.global_position, move_speed * 0.8)
	elif dist > pref:
		move_to(t.global_position, pref * 0.9)
	else:
		var to := t.global_position - global_position
		var side := Vector3(-to.z, 0, to.x).normalized() * signf(sin(Game.now() * 0.7 + _seed))
		_desired = side * move_speed * 0.5
	face(t.global_position)


func attack(t: Node3D, _dist: float, delta: float) -> void:
	stop()
	if t and state_time < beats.strike_at():
		face(t.global_position)
	if beats.hold_begins(state_time, delta) and _orb:
		Anticipation.glint(_orb.global_position, Color(0.85, 0.55, 1.0), 0.7, 1.1)
	if state_time >= beats.strike_at() and not _fired:
		_fired = true
		if t and is_instance_valid(t):
			_fire(t)
	if state_time > beats.strike_at() + RECOVER:
		start_cooldown(attack_cooldown * randf_range(0.9, 1.3))
		set_state(State.CHASE)


func _fire(t: Node3D) -> void:
	var from := global_position + Vector3.UP * 1.1 + _facing * 0.3
	var tv: Vector3 = t.velocity if "velocity" in t else Vector3.ZERO
	var aim := t.global_position + Vector3.UP * 1.0 + Vector3(tv.x, 0, tv.z) * 0.25
	var shot := Shot.new()
	shot.team = Combat.Team.ENEMY
	shot.source = self
	shot.speed = spore_speed * RANGE
	shot.lifetime = 2.2
	shot.radius = 0.35
	shot.color = Color(0.7, 0.45, 0.85)
	var d := DamageInfo.make(spore_damage, Combat.Team.ENEMY, self, from, aim - from, Combat.Element.POISON, 3.0)
	d.status = {"slow": 0.35, "slowDuration": 1.8}
	d.skill_name = "Bào Tử"
	d.source_level = level
	shot.hit = d
	get_parent().add_child(shot)
	shot.launch(from, (aim - from).normalized())
	Fx.burst(from, Vector3.UP, Color(0.75, 0.55, 0.85, 0.6), 10, 1.5, 0.35, 0.6, 60.0, 0.5)


func _can_see(t: Node3D) -> bool:
	var q := PhysicsRayQueryParameters3D.create(global_position + Vector3.UP * 1.1, t.global_position + Vector3.UP, Combat.LAYER_WORLD)
	return get_world_3d().direct_space_state.intersect_ray(q).is_empty()


func on_death() -> void:
	Fx.burst(global_position + Vector3.UP * 0.9, Vector3.UP, Color(0.7, 0.5, 0.85, 0.7), 24, 2.5, 0.6, 1.2, 80.0, 0.3)


func on_attack_interrupted() -> void:
	_fired = true


func _physics_process(delta: float) -> void:
	super._physics_process(delta)
	if not _body:
		return
	var aiming := state == State.ATTACK and not _fired and state_time < beats.strike_at()
	_orb.visible = aiming and state_time > beats.tell
	if state == State.DEAD or not on_screen:
		return
	var t := Game.now()
	var walk := Vector2(velocity.x, velocity.z).length()
	_body.position.y = absf(sin(t * 10.0)) * 0.08 * clampf(walk, 0.0, 1.0)
	_body.rotation.z = sin(t * 10.0) * 0.08 * clampf(walk, 0.0, 1.0)
	var puff := 1.0
	var lean := 0.0
	if state == State.ATTACK:
		var st := state_time
		if st < beats.strike_at():
			var w := beats.windup(st, 0.3)
			# the nod leans in a touch, the wind rears back with the cap swelling
			lean = 0.34 * w + beats.tremble(st) * 0.03
			puff = 1.0 + 0.3 * maxf(w, 0.0)
			_orb.scale = Vector3.ONE * (0.3 + 0.9 * maxf(w, 0.0) + beats.tremble(st) * 0.08)
		elif st < beats.strike_at() + 0.16:
			# the spit: snaps forward, cap squeezed
			lean = -0.32
			puff = 0.8
	_body.rotation.x = lerpf(_body.rotation.x, lean, 0.45)
	_cap.scale = _cap.scale.lerp(Vector3(puff, 2.0 - puff, puff), 0.35)
