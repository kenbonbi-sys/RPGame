class_name Shroom
extends Enemy
## Nấm Độc (ShroomAI.cs): keeps its distance — backs off when crowded, walks in when far, sidesteps
## in between — and shoots a slowing spore when the hero is in range and in sight. Weak to fire.
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


func _init() -> void:
	enemy_id = "shroom"
	display_name = "Nấm Độc"
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


func attack(t: Node3D, _dist: float, _delta: float) -> void:
	stop()
	if t:
		face(t.global_position)
	if state_time > 0.32 and not _fired:
		_fired = true
		if t and is_instance_valid(t):
			_fire(t)
	if state_time > 0.75:
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


func _physics_process(delta: float) -> void:
	super._physics_process(delta)
	if not _body or state == State.DEAD:
		return
	var t := Game.now()
	var walk := Vector2(velocity.x, velocity.z).length()
	_body.position.y = absf(sin(t * 10.0)) * 0.08 * clampf(walk, 0.0, 1.0)
	_body.rotation.z = sin(t * 10.0) * 0.08 * clampf(walk, 0.0, 1.0)
	var puff := 1.0
	if state == State.ATTACK and state_time < 0.4:
		puff = 1.0 + sin(clampf(state_time / 0.32, 0.0, 1.0) * PI) * 0.25
	_cap.scale = _cap.scale.lerp(Vector3(puff, 0.55 * (2.0 - puff), puff), 0.3)
