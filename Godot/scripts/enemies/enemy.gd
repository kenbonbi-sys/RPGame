class_name Enemy
extends CharacterBody3D
## A regular monster, as the Unity game's EnemyBase.cs: Idle ↔ Wander around home, Chase the hero
## it hates most, Attack (each species decides how), Return home and heal when it strays too far,
## Dead. Threat comes from damage; a hit staggers it for 0.12 s; knockback decays and slamming into
## something hard stuns it; it bumps heroes it touches; far from every hero it sleeps.
##
## Distances: the 3D world is roomier than the 2D map, so areas (aggro, leash, wander, sleep) are
## the Unity numbers × AREA, while body-sized ones (attack reach, lunge, projectiles) stay as they are.

signal died_at(enemy: Enemy)

enum State { IDLE, WANDER, CHASE, ATTACK, RETURN, DEAD }
enum Rank { NORMAL, ELITE, MINIBOSS, BOSS }

const AREA := 1.6
const SLEEP_RADIUS := 40.0 * AREA * 1.25
const STAGGER := 0.12
const GRAVITY := 22.0
const ACCEL := 40.0
const KNOCK_DECAY := 18.0

@export var enemy_id := "slime"
@export var display_name := "Slime Rêu"
@export var level := 2
@export var rank := Rank.NORMAL
@export var max_hp := 60.0
@export var move_speed := 2.6
@export var aggro_range := 6.0
@export var leash_range := 13.0
@export var attack_range := 1.2
@export var attack_cooldown := 1.5
@export var contact_damage := 0.0
@export var contact_radius := 1.0
@export var wander_radius := 2.5
@export var knockback_resist := 1.0
@export var stun_resist := 1.0
@export var resistances := PackedFloat32Array([0, 0, 0, 0, 0, 0, 0])
@export var body_radius := 0.5
@export var body_height := 1.0
@export var head_height := 1.2
## {"item": id, "chance": 0..1, "min": n, "max": n}
@export var loot: Array[Dictionary] = []

var state := State.IDLE
var state_time := 0.0
var home := Vector3.ZERO
var target: Node3D
var spawner: Node
var health: Health
var status: StatusEffects
var model: Node3D
var threat := {}

var _next_attack := 0.0
var _stagger_until := 0.0
var _knock := Vector3.ZERO
var _move := Vector3.ZERO
var _desired := Vector3.ZERO
var _next_contact := 0.0
var _sleep_check := 0.0
var _asleep := false
var _idle_for := 2.0
var _wander_to := Vector3.ZERO
var _flash := 0.0
var _flash_hold_until := 0.0
var _plate: Nameplate
var _facing := Vector3.FORWARD


func _ready() -> void:
	add_to_group("enemies")
	collision_layer = Combat.LAYER_ENEMY
	collision_mask = Combat.LAYER_WORLD | Combat.LAYER_HERO | Combat.LAYER_ENEMY
	floor_snap_length = 0.4
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = body_radius
	cap.height = maxf(body_height, body_radius * 2.0)
	cs.shape = cap
	cs.position = Vector3(0, cap.height * 0.5, 0)
	add_child(cs)
	health = Health.new()
	health.name = "Health"
	health.team = Combat.Team.ENEMY
	health.display_name = display_name
	health.level = level
	health.max_hp = max_hp
	health.resistances = resistances
	health.head_height = head_height
	status = StatusEffects.new()
	status.name = "Status"
	status.stun_resist = stun_resist
	add_child(status)
	add_child(health)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	model = Node3D.new()
	model.name = "Model"
	add_child(model)
	build_model(model)
	home = global_position
	_idle_for = randf_range(1.5, 3.5)


## Species fill the model in.
func build_model(_root: Node3D) -> void:
	pass


func now() -> float:
	return StatusEffects._scaled_now()


func set_state(s: State) -> void:
	state = s
	state_time = 0.0


func is_busy() -> bool:
	return state == State.CHASE or state == State.ATTACK


# ================================================================== every frame
func _physics_process(delta: float) -> void:
	if Game.now() >= _flash_hold_until:
		_flash = move_toward(_flash, 0.0, delta * 12.0)
	_update_look()
	if state == State.DEAD:
		_move_body(delta, Vector3.ZERO)
		return
	if not Game.is_authority():
		return
	_sleep_check -= delta
	if _sleep_check <= 0.0:
		_sleep_check = randf_range(0.5, 0.75)
		_asleep = _nearest_hero(SLEEP_RADIUS) == null
	if _asleep:
		velocity = Vector3.ZERO
		return
	state_time += delta
	_desired = Vector3.ZERO
	if status.is_stunned():
		if state == State.ATTACK:
			on_attack_interrupted()
			set_state(State.CHASE)
		_move_body(delta, Vector3.ZERO)
		return
	if now() < _stagger_until:
		_move_body(delta, Vector3.ZERO)
		return
	_think(delta)
	_contact_damage()
	_move_body(delta, _desired)


func _think(delta: float) -> void:
	match state:
		State.IDLE:
			if notice():
				set_state(State.CHASE)
			elif state_time > _idle_for:
				_wander_to = home + _random_in_disc(wander_radius * AREA)
				set_state(State.WANDER)
		State.WANDER:
			if notice():
				set_state(State.CHASE)
			elif move_to(_wander_to, 0.45) or state_time > 4.0:
				_idle_for = randf_range(1.5, 3.5)
				set_state(State.IDLE)
		State.CHASE:
			target = pick_target()
			if target == null or _flat_distance(global_position, home) > leash_range * AREA:
				give_up()
			else:
				chase(target, _flat_distance(global_position, target.global_position), delta)
		State.ATTACK:
			var d := 999.0
			if target and is_instance_valid(target) and not target.is_dead():
				d = _flat_distance(global_position, target.global_position)
			attack(target, d, delta)
		State.RETURN:
			if move_to(home, 1.0) or state_time > 6.0:
				health.hp = health.max_hp
				health.attackers.clear()
				_idle_for = randf_range(1.5, 3.5)
				set_state(State.IDLE)


## Default chase: walk in and attack when close and ready.
func chase(t: Node3D, dist: float, _delta: float) -> void:
	if dist <= attack_range and now() >= _next_attack:
		set_state(State.ATTACK)
	else:
		move_to(t.global_position, attack_range * 0.8)


## Default attack: nothing but the cooldown (species override).
func attack(_t: Node3D, _dist: float, _delta: float) -> void:
	start_cooldown()
	set_state(State.CHASE)


func on_attack_interrupted() -> void:
	pass


func start_cooldown(seconds := -1.0) -> void:
	var cd := attack_cooldown if seconds < 0.0 else seconds
	_next_attack = now() + cd / maxf(0.1, status.attack_speed_multiplier())


func ready_to_attack() -> bool:
	return now() >= _next_attack


# ================================================================== finding heroes
func heroes() -> Array:
	return get_tree().get_nodes_in_group("heroes").filter(func(h) -> bool: return is_instance_valid(h) and not h.is_dead())


func _nearest_hero(r: float) -> Node3D:
	var best: Node3D = null
	var best_d := r
	for h in heroes():
		var d := global_position.distance_to(h.global_position)
		if d <= best_d:
			best_d = d
			best = h
	return best


## A hero within reach makes it notice (and hate them a little).
func notice() -> bool:
	var h := _nearest_hero(aggro_range * AREA)
	if h:
		threat[h] = float(threat.get(h, 0.0)) + 1.0
		return true
	return false


func pick_target() -> Node3D:
	var keep := aggro_range * AREA * 1.8
	var best: Node3D = null
	var best_t := -1.0
	for h in threat.keys():
		if not is_instance_valid(h) or h.is_dead() or h.global_position.distance_to(global_position) > keep:
			threat.erase(h)
			continue
		if threat[h] > best_t:
			best_t = threat[h]
			best = h
	if best == null:
		best = _nearest_hero(keep)
		if best:
			threat[best] = 1.0
	return best


func alert(h: Node3D) -> void:
	threat[h] = float(threat.get(h, 0.0)) + 1.0
	set_state(State.CHASE)


func give_up() -> void:
	threat.clear()
	target = null
	set_state(State.RETURN)


# ================================================================== moving
## Walks toward a point; true once within `stop`.
func move_to(p: Vector3, stop: float, speed_mul := 1.0) -> bool:
	var to := p - global_position
	to.y = 0.0
	if to.length() <= stop:
		_desired = Vector3.ZERO
		return true
	_desired = to.normalized() * move_speed * speed_mul * status.speed_multiplier()
	return false


## Steers in a direction this frame.
func steer(dir: Vector3, speed: float) -> void:
	dir.y = 0.0
	_desired = dir.normalized() * speed * status.speed_multiplier() if dir.length() > 0.001 else Vector3.ZERO


func stop() -> void:
	_desired = Vector3.ZERO
	_move = Vector3.ZERO


func face(p: Vector3) -> void:
	var to := p - global_position
	to.y = 0.0
	if to.length() > 0.01:
		_facing = to.normalized()


## A short burst of speed that overrides walking (lunges).
var _dash_vel := Vector3.ZERO
var _dash_until := 0.0


func dash(vel: Vector3, seconds: float) -> void:
	if status.is_rooted():
		return
	_dash_vel = Vector3(vel.x, 0, vel.z)
	_dash_until = now() + seconds


func is_dashing() -> bool:
	return now() < _dash_until


func add_knockback(impulse: Vector3) -> void:
	_knock += Vector3(impulse.x, 0, impulse.z) * knockback_resist


func _move_body(delta: float, desired: Vector3) -> void:
	if status and status.is_rooted():
		desired = Vector3.ZERO
		_dash_until = 0.0
	_move = _move.move_toward(desired, ACCEL * delta)
	_knock = _knock.move_toward(Vector3.ZERO, KNOCK_DECAY * delta)
	var flat := _dash_vel if is_dashing() else _move + _knock
	velocity.x = flat.x
	velocity.z = flat.z
	velocity.y = velocity.y - GRAVITY * delta if not is_on_floor() else -0.5
	var knock_before := _knock
	move_and_slide()
	if knock_before.length() >= DB.c("wallSlamSpeed", 5.0) and not is_dashing():
		for i in get_slide_collision_count():
			var c := get_slide_collision(i)
			if c.get_normal().y < 0.5 and knock_before.normalized().dot(c.get_normal()) <= -0.5 \
					and c.get_collider() is StaticBody3D:
				_knock = Vector3.ZERO
				status.on_wall_slam()
				break
	if _move.length() > 0.2 and not is_dashing():
		_facing = Vector3(_move.x, 0, _move.z).normalized()


func _contact_damage() -> void:
	if contact_damage <= 0.0 or now() < _next_contact:
		return
	var h := _nearest_hero(contact_radius)
	if h == null:
		return
	_next_contact = now() + 1.0
	var d := DamageInfo.make(contact_damage, Combat.Team.ENEMY, self, h.global_position + Vector3.UP, h.global_position - global_position,
		Combat.Element.PHYSICAL, 4.0)
	d.contact = true
	hit_hero(h, d)


## Deals a hit to a hero (with knockback and feedback when it lands).
func hit_hero(h: Node3D, d: DamageInfo) -> float:
	var hh: Health = h.get("health")
	if hh == null:
		return 0.0
	d.source_level = level
	var dealt := hh.take_damage(d)
	if dealt > 0.0:
		Combat.on_hit_feedback(hh, d)
	return dealt


# ================================================================== being hit
func _on_damaged(d: DamageInfo, amount: float) -> void:
	if d.source and d.source.is_in_group("heroes"):
		threat[d.source] = float(threat.get(d.source, 0.0)) + amount
		if state == State.IDLE or state == State.WANDER or state == State.RETURN:
			set_state(State.CHASE)
	if not d.dot:
		_stagger_until = now() + STAGGER
		_flash = 1.0
		_flash_hold_until = maxf(_flash_hold_until, Game.now() + 0.06)
		if _plate == null and Game.has_screen():
			_plate = Nameplate.new()
			_plate.setup(self, "%s · Cấp %d" % [display_name, level], head_height + 0.55)
			add_child(_plate)


## Full white for a blink (`seconds`, real time), then a quick fade.
func flash(_color: Color, strength: float, seconds: float) -> void:
	_flash = maxf(_flash, strength)
	_flash_hold_until = Game.now() + seconds


func _on_died(_d: DamageInfo) -> void:
	set_state(State.DEAD)
	stop()
	_knock = Vector3.ZERO
	collision_layer = 0
	collision_mask = Combat.LAYER_WORLD
	on_death()
	if Game.is_authority():
		var credited: Array = health.attackers.duplicate()
		if credited.is_empty():
			credited = heroes()
		for h in credited:
			if is_instance_valid(h) and h.get("stats"):
				h.stats.on_kill(level, rank, global_position)
		Loot.drop(get_parent(), global_position + Vector3.UP * 0.4, loot, credited)
		Game.enemy_killed.emit(self, credited)
	died_at.emit(self)
	_despawn()


## Species add their own death flourish.
func on_death() -> void:
	Fx.burst(global_position + Vector3.UP * 0.5, Vector3.UP, Color(0.9, 0.9, 0.85, 0.7), 18, 3.0, 0.4, 0.8, 80.0, -2.0)


func _despawn() -> void:
	await get_tree().create_timer(1.2, false).timeout
	if not is_instance_valid(self):
		return
	var tw := create_tween()
	tw.tween_property(model, "scale", Vector3(1.3, 0.05, 1.3), 0.6).set_ease(Tween.EASE_IN)
	await tw.finished
	if spawner and is_instance_valid(spawner):
		spawner.notify_dead(self)
	visible = false
	process_mode = Node.PROCESS_MODE_DISABLED


## Back to life at a point (spawners call this).
func revive(at: Vector3) -> void:
	process_mode = Node.PROCESS_MODE_INHERIT
	visible = true
	global_position = at
	home = at
	health.reset_health()
	status.cleanse()
	threat.clear()
	target = null
	collision_layer = Combat.LAYER_ENEMY
	collision_mask = Combat.LAYER_WORLD | Combat.LAYER_HERO | Combat.LAYER_ENEMY
	model.scale = Vector3.ONE
	_knock = Vector3.ZERO
	_move = Vector3.ZERO
	_idle_for = randf_range(1.5, 3.5)
	set_state(State.IDLE)
	if _plate:
		_plate.queue_free()
		_plate = null


# ================================================================== looks
func _update_look() -> void:
	if not model:
		return
	# models look along -Z
	var want := atan2(-_facing.x, -_facing.z)
	model.rotation.y = lerp_angle(model.rotation.y, want, 0.25)
	var tint := status.tint() if status else Color(0, 0, 0, 0)
	for n in model.find_children("*", "MeshInstance3D", true, false):
		var m := (n as MeshInstance3D).material_override as ShaderMaterial
		if m:
			m.set_shader_parameter("hurt", 1.0 if Game.now() < _flash_hold_until else _flash * 0.8)
			m.set_shader_parameter("tint", tint)


static func _flat_distance(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x - b.x, a.z - b.z).length()


static func _random_in_disc(r: float) -> Vector3:
	var a := randf() * TAU
	var d := sqrt(randf()) * r
	return Vector3(cos(a) * d, 0, sin(a) * d)
