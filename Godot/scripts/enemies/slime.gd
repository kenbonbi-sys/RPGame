class_name Slime
extends Enemy
## Slime Rêu (SlimeAI.cs): hops after the hero (pause, hop, pause…), crouches for 0.25 s — its
## only tell — then lunges; anyone it reaches in the lunge takes 10 and is knocked back. Touching
## it hurts a little too. Wandering and going home it just slides along.

@export var hop_time := 0.45
@export var hop_pause := 0.5
@export var lunge_speed := 7.0
@export var lunge_damage := 10.0
@export var radius := 0.55

var _hop_timer := 0.0
var _hopping := false
var _lunged := false
var _lunge_dir := Vector3.FORWARD
var _body: Node3D


func _init() -> void:
	enemy_id = "slime"
	display_name = "Slime Rêu"
	level = 2
	max_hp = 60.0
	move_speed = 2.6
	aggro_range = 6.0
	leash_range = 13.0
	attack_range = 1.2
	attack_cooldown = 1.5
	contact_damage = 6.0
	wander_radius = 2.5
	body_radius = 0.5
	body_height = 1.0
	head_height = 1.25
	loot = [
		{"item": "gel", "chance": 0.6, "min": 1, "max": 1},
		{"item": "coin", "chance": 0.8, "min": 1, "max": 3},
		{"item": "potion_red", "chance": 0.08, "min": 1, "max": 1},
	]


func build_model(root: Node3D) -> void:
	_body = CreatureArt.moss_slime(root, radius)
	_hop_timer = randf() * 0.5


func chase(t: Node3D, dist: float, delta: float) -> void:
	if dist <= attack_range + 0.6 + body_radius and ready_to_attack() and not _hopping:
		stop()
		face(t.global_position)
		_lunged = false
		set_state(State.ATTACK)
		return
	_hop_timer -= delta
	if _hopping:
		move_to(t.global_position, 0.0)
		if _hop_timer <= 0.0:
			_hopping = false
			_hop_timer = hop_pause * randf_range(0.8, 1.3)
	else:
		stop()
		if _hop_timer <= 0.0:
			_hopping = true
			_hop_timer = hop_time


func attack(t: Node3D, _dist: float, _delta: float) -> void:
	var st := state_time
	if st < 0.25:
		stop()
		if t:
			face(t.global_position)
	elif not _lunged:
		_lunged = true
		if t:
			var to := t.global_position - global_position
			to.y = 0.0
			_lunge_dir = to.normalized() if to.length() > 0.01 else _facing
		face(global_position + _lunge_dir)
		dash(_lunge_dir * lunge_speed, 0.25)
	elif st >= 0.3 and st < 0.55 and t and is_instance_valid(t):
		if _flat_distance(global_position, t.global_position) < 0.8 + body_radius and absf(t.global_position.y - global_position.y) < 1.5:
			var d := DamageInfo.make(lunge_damage, Combat.Team.ENEMY, self, t.global_position + Vector3.UP, _lunge_dir,
				Combat.Element.PHYSICAL, 5.0)
			d.skill_name = "Lao Tới"
			hit_hero(t, d)
			state_time = 0.56
	elif st >= 0.9:
		start_cooldown()
		set_state(State.CHASE)


func on_attack_interrupted() -> void:
	_lunged = true


func on_death() -> void:
	Fx.burst(global_position + Vector3.UP * 0.4, Vector3.UP, Color(0.35, 0.75, 0.35, 0.85), 26, 5.0, 0.35, 0.8, 70.0, -12.0)


func _physics_process(delta: float) -> void:
	super._physics_process(delta)
	_animate(delta)


## Squash and stretch: hops arc up, the wind-up squats, idle breathes.
func _animate(_delta: float) -> void:
	if not _body:
		return
	var t := Game.now()
	var y := 0.0
	var squash := 1.0 + sin(t * 3.0 + get_instance_id()) * 0.04
	if state == State.DEAD:
		return
	if state == State.CHASE and _hopping:
		var k := 1.0 - clampf(_hop_timer / hop_time, 0.0, 1.0)
		y = sin(k * PI) * 0.45
		squash = 1.0 + sin(k * PI) * 0.25
	elif state == State.ATTACK:
		if state_time < 0.25:
			squash = 1.0 - state_time * 1.4
		elif state_time < 0.55:
			squash = 1.3
			y = 0.15
	elif (state == State.WANDER or state == State.RETURN) and velocity.length() > 0.2:
		y = absf(sin(t * 9.0)) * 0.12
	_body.position.y = lerpf(_body.position.y, y, 0.35)
	var s := Vector3(1.0 / sqrt(squash), squash, 1.0 / sqrt(squash))
	_body.scale = _body.scale.lerp(s, 0.3)
