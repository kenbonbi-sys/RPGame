class_name Hero
extends CharacterBody3D
## The hero, played from behind like Wander in Shadow of the Colossus: runs, sprints, jumps,
## Lướt (dodge roll with a moment of invulnerability), a three-hit sword combo, and the grip:
## hold "grip" to cling to anything furry (colossus fur, climbable rock), climb it while the
## grip meter lasts, ride a colossus's back, and stab its glowing weak points with a charged thrust.

signal hp_changed(hp: float, max_hp: float)
signal grip_changed(grip: float, max_grip: float)
signal died

enum State { GROUND, AIR, ROLL, ATTACK, CLIMB, STAB, KNOCKED, DEAD }

const LAYER_WORLD := 1
const LAYER_ENEMY := 4
const LAYER_COLOSSUS := 8
const CHEST := 1.15

@export var hero_name := "Hồng Anh"
@export var max_hp := 120.0
@export var max_grip := 100.0
@export var walk_speed := 2.2
@export var run_speed := 5.4
@export var sprint_speed := 8.2
@export var accel := 18.0
@export var air_accel := 5.0
@export var gravity := 21.0
@export var jump_speed := 7.4
@export var roll_speed := 10.0
@export var roll_time := 0.48
@export var climb_speed := 1.8
@export var model_scale := 0.75
## Falls shorter than this do not hurt.
@export var safe_fall := 7.5

var hp := 120.0
var grip := 100.0
var level := 1
var state := State.GROUND
## The world direction the hero looks (flat).
var facing := Vector3.FORWARD
var is_climbing := false
var on_colossus := false
var invulnerable_until := 0.0
## The weak point within reach, if any (for the HUD prompt).
var weak_point_near: Node3D
## A grippable surface right in front (for the HUD prompt).
var grip_hint := false

var _state_time := 0.0
var _dodge_down_at := -1.0
var _dodge_held := false
var _sprinting := false
var _combo := 0
var _combo_queued := false
var _attack_hit_done := false
var _peak_y := 0.0
var _ground_time := 0.0
var _stab_charge := 0.0
var _stab_released := false
var _stab_target: Node3D
var _roll_dir := Vector3.FORWARD
var _knock_landed := -1.0

var _climb_body: CollisionObject3D
var _climb_local := Vector3.ZERO
var _climb_normal_local := Vector3.UP
var _climb_cycle := 0.0
var _ride_body: CollisionObject3D
var _ride_local := Vector3.ZERO
var _ride_basis := Basis.IDENTITY

var model: Node3D
var anim: AnimationPlayer
var _anim_name := ""
var _anim_len := {}

const COMBO := [
	{"anim": "1H_Melee_Attack_Slice_Diagonal", "time": 0.56, "hit": 0.24, "damage": 14.0, "lunge": 2.2},
	{"anim": "1H_Melee_Attack_Slice_Horizontal", "time": 0.54, "hit": 0.22, "damage": 14.0, "lunge": 2.2},
	{"anim": "1H_Melee_Attack_Chop", "time": 0.78, "hit": 0.36, "damage": 24.0, "lunge": 3.0},
]
const LOOPING := ["Idle", "Walking_A", "Running_A", "Running_B", "Jump_Idle", "Spellcasting", "Blocking", "Lie_Idle", "Sit_Floor_Idle"]
const SHOWN_GEAR := ["1H_Sword", "Round_Shield"]
const HIDDEN_GEAR := ["1H_Sword_Offhand", "Badge_Shield", "Rectangle_Shield", "Spike_Shield", "2H_Sword"]


func _ready() -> void:
	collision_layer = 2
	collision_mask = LAYER_WORLD | LAYER_ENEMY | LAYER_COLOSSUS
	floor_max_angle = deg_to_rad(50.0)
	floor_snap_length = 0.45
	platform_floor_layers = 0
	platform_wall_layers = 0
	if not has_node("Shape"):
		var cs := CollisionShape3D.new()
		cs.name = "Shape"
		var cap := CapsuleShape3D.new()
		cap.radius = 0.34
		cap.height = 1.75
		cs.shape = cap
		cs.position = Vector3(0, 0.875, 0)
		add_child(cs)
	model = get_node_or_null("Model")
	if model:
		model.scale = Vector3.ONE * model_scale
		anim = model.find_children("*", "AnimationPlayer", true, false).front() as AnimationPlayer
		for n in model.find_children("*", "MeshInstance3D", true, false):
			(n as MeshInstance3D).layers = 2
			if n.name in HIDDEN_GEAR:
				n.visible = false
	if anim:
		for a in anim.get_animation_list():
			var res := anim.get_animation(a)
			_anim_len[a] = res.length
			if a in LOOPING:
				res.loop_mode = Animation.LOOP_LINEAR
		play("Idle")
	hp = max_hp
	grip = max_grip
	add_to_group("heroes")
	if Game.hero == null:
		Game.hero = self
	_peak_y = global_position.y


## The colossus this hero is holding on to or standing on, if any.
func attached_colossus() -> Node:
	if _climb_body and (state == State.CLIMB or state == State.STAB):
		return _colossus_of(_climb_body)
	if _ride_body and is_instance_valid(_ride_body):
		return _colossus_of(_ride_body)
	return null


# ================================================================== animation
func play(anim_name: String, blend := 0.15, speed := 1.0) -> void:
	if not anim or not anim.has_animation(anim_name):
		return
	if _anim_name == anim_name:
		anim.speed_scale = speed
		return
	_anim_name = anim_name
	anim.speed_scale = speed
	anim.play(anim_name, blend)


## Hold one moment of an animation (used for the climbing and stab poses).
func hold(anim_name: String, t: float, blend := 0.12) -> void:
	if not anim or not anim.has_animation(anim_name):
		return
	if _anim_name != anim_name:
		_anim_name = anim_name
		anim.play(anim_name, blend)
	anim.speed_scale = 0.0
	anim.seek(clampf(t, 0.0, _anim_len.get(anim_name, 1.0)), true)


func anim_length(anim_name: String) -> float:
	return _anim_len.get(anim_name, 1.0)


# ================================================================== state
func set_state(s: State) -> void:
	if s == state:
		return
	if state == State.CLIMB or state == State.STAB:
		if s != State.CLIMB and s != State.STAB:
			_release_grip()
	state = s
	_state_time = 0.0


func is_dead() -> bool:
	return state == State.DEAD


func _physics_process(delta: float) -> void:
	_state_time += delta
	_follow_attachment()
	weak_point_near = _find_weak_point()
	match state:
		State.GROUND, State.AIR:
			_locomotion(delta)
		State.ROLL:
			_roll(delta)
		State.ATTACK:
			_attack(delta)
		State.CLIMB:
			_climb(delta)
		State.STAB:
			_stab(delta)
		State.KNOCKED:
			_knocked(delta)
		State.DEAD:
			_dead(delta)
	_update_grip(delta)
	_orient_model(delta)
	is_climbing = _climb_body != null and (state == State.CLIMB or state == State.STAB)


# ================================================================== on the ground and in the air
func _move_dir() -> Vector3:
	var mv := Controls.move_vector()
	var cam := Game.camera as SotcCamera
	if not cam:
		return Vector3(mv.x, 0, -mv.y)
	return cam.right_flat() * mv.x + cam.forward_flat() * mv.y


func _locomotion(delta: float) -> void:
	var dir := _move_dir()
	var amount := dir.length()
	var on_floor := is_on_floor()

	# Shift: a tap is Lướt, holding it sprints
	if Input.is_action_just_pressed("dodge"):
		_dodge_down_at = Game.now()
		_dodge_held = true
	if _dodge_held and Input.is_action_just_released("dodge"):
		_dodge_held = false
		if Game.now() - _dodge_down_at < Controls.DODGE_TAP_TIME and on_floor:
			_start_roll(dir)
			return
	_sprinting = _dodge_held and Game.now() - _dodge_down_at >= Controls.DODGE_TAP_TIME and amount > 0.3

	if Input.is_action_pressed("grip") and _try_grip():
		return
	if Input.is_action_just_pressed("attack"):
		if weak_point_near and on_colossus:
			_start_stab(weak_point_near)
			return
		if on_floor:
			_start_attack(0)
			return

	var speed := walk_speed if amount < 0.45 else (sprint_speed if _sprinting else run_speed)
	var want := dir.normalized() * speed * minf(1.0, amount * 1.6) if amount > 0.05 else Vector3.ZERO
	var a := accel if on_floor else air_accel
	var flat := Vector3(velocity.x, 0, velocity.z).move_toward(want, a * delta * (1.6 if want == Vector3.ZERO and on_floor else 1.0))
	velocity.x = flat.x
	velocity.z = flat.z
	if amount > 0.05:
		facing = facing.slerp(dir.normalized(), 1.0 - exp(-(12.0 if on_floor else 4.0) * delta)).normalized()

	if on_floor:
		if Input.is_action_just_pressed("jump"):
			velocity.y = jump_speed
			_peak_y = global_position.y
			set_state(State.AIR)
			play("Jump_Start", 0.08)
			_ride_body = null
			on_colossus = false
			return
	velocity.y -= gravity * delta
	move_and_slide()
	_after_move(delta)

	if is_on_floor():
		if state == State.AIR:
			_land()
		set_state(State.GROUND)
		_ground_time += delta
		var hs := Vector2(velocity.x, velocity.z).length()
		if _state_time > 0.15 or _anim_name != "Jump_Land":
			if hs > 6.5:
				play("Running_B", 0.15, hs / 7.5)
			elif hs > 2.6:
				play("Running_A", 0.15, hs / 5.4)
			elif hs > 0.3:
				play("Walking_A", 0.15, maxf(0.6, hs / 2.2))
			else:
				play("Idle", 0.2)
	else:
		if state != State.AIR:
			_peak_y = global_position.y
		set_state(State.AIR)
		_ground_time = 0.0
		_peak_y = maxf(_peak_y, global_position.y)
		if _anim_name != "Jump_Start" or _state_time > 0.25:
			play("Jump_Idle", 0.2)


func _land() -> void:
	var fall := _peak_y - global_position.y
	if fall > safe_fall:
		var dmg := (fall - safe_fall) * 7.0
		take_hit({"amount": dmg, "fall": true})
		Game.shake(clampf(fall / 30.0, 0.15, 0.6))
	if state != State.DEAD and state != State.KNOCKED:
		play("Jump_Land", 0.05)
	_peak_y = global_position.y


## Keeps the hero standing on whatever colossus part is under its feet.
func _after_move(_delta: float) -> void:
	_ride_body = null
	on_colossus = false
	if not is_on_floor():
		return
	for i in get_slide_collision_count():
		var c := get_slide_collision(i)
		if c.get_normal().y < 0.55:
			continue
		var body := c.get_collider() as CollisionObject3D
		if body and (body.collision_layer & LAYER_COLOSSUS) != 0:
			_ride_body = body
			_ride_basis = body.global_transform.basis.orthonormalized()
			_ride_local = body.global_transform.affine_inverse() * global_position
			on_colossus = true
			return


## Moves the hero along with the colossus part it holds or stands on.
func _follow_attachment() -> void:
	if _climb_body and (state == State.CLIMB or state == State.STAB):
		if not is_instance_valid(_climb_body):
			_release_grip()
			set_state(State.AIR)
			return
		_place_on_grip()
	elif _ride_body and is_instance_valid(_ride_body) and state != State.AIR:
		var xf := _ride_body.global_transform
		global_position = xf * _ride_local
		var b := xf.basis.orthonormalized()
		var turn := b * _ride_basis.inverse()
		facing = (turn * facing)
		facing.y = 0.0
		facing = facing.normalized() if facing.length() > 0.01 else Vector3.FORWARD
		_ride_basis = b
		var shake := _colossus_shake(_ride_body)
		if shake > 0.4 and not Input.is_action_pressed("grip"):
			_thrown_off(_ride_body)


func _colossus_of(body: Node) -> Node:
	return body.get_meta("colossus") if body and body.has_meta("colossus") else null


func _colossus_shake(body: Node) -> float:
	var c := _colossus_of(body)
	return c.get("shake_amount") if c else 0.0


func _thrown_off(body: Node3D) -> void:
	var away := (global_position - body.global_position)
	away.y = 0.0
	away = away.normalized() if away.length() > 0.1 else -facing
	velocity = away * 6.5 + Vector3.UP * 5.0
	_ride_body = null
	on_colossus = false
	_peak_y = global_position.y
	set_state(State.KNOCKED)
	_knock_landed = -1.0
	play("Hit_B", 0.05)


# ================================================================== Lướt
func _start_roll(dir: Vector3) -> void:
	_roll_dir = dir.normalized() if dir.length() > 0.1 else facing
	facing = _roll_dir
	invulnerable_until = Game.now() + 0.32
	set_state(State.ROLL)
	play("Dodge_Forward", 0.05, anim_length("Dodge_Forward") / roll_time)


func _roll(delta: float) -> void:
	var t := _state_time / roll_time
	var spd := roll_speed * (1.0 - t * t * 0.7)
	velocity.x = _roll_dir.x * spd
	velocity.z = _roll_dir.z * spd
	velocity.y -= gravity * delta
	move_and_slide()
	_after_move(delta)
	if _state_time >= roll_time:
		set_state(State.GROUND if is_on_floor() else State.AIR)


# ================================================================== sword
func _start_attack(step: int) -> void:
	_combo = step
	_combo_queued = false
	_attack_hit_done = false
	var dir := _move_dir()
	if dir.length() > 0.2:
		facing = dir.normalized()
	set_state(State.ATTACK)
	var c: Dictionary = COMBO[step]
	play(c.anim, 0.06, anim_length(c.anim) / c.time)
	_anim_name = ""  # allow the same swing to restart


func _attack(delta: float) -> void:
	var c: Dictionary = COMBO[_combo]
	if Input.is_action_just_pressed("attack") and _state_time > c.time * 0.35:
		_combo_queued = true
	var lunge: float = c.lunge * (1.0 - smoothstep(0.0, c.hit + 0.08, _state_time)) if _state_time < c.hit + 0.08 else 0.0
	velocity.x = facing.x * lunge
	velocity.z = facing.z * lunge
	velocity.y -= gravity * delta
	move_and_slide()
	_after_move(delta)
	if not _attack_hit_done and _state_time >= c.hit:
		_attack_hit_done = true
		_sword_hit(c.damage, _combo == 2)
	if _state_time >= c.time:
		if _combo_queued and _combo < COMBO.size() - 1:
			_start_attack(_combo + 1)
		else:
			set_state(State.GROUND)
	elif Input.is_action_just_pressed("dodge") and _state_time > c.hit:
		_start_roll(_move_dir())


func _sword_hit(damage: float, heavy: bool) -> void:
	var q := PhysicsShapeQueryParameters3D.new()
	var s := SphereShape3D.new()
	s.radius = 1.25
	q.shape = s
	q.transform = Transform3D(Basis.IDENTITY, global_position + Vector3.UP * 1.0 + facing * 1.15)
	q.collision_mask = LAYER_ENEMY | LAYER_COLOSSUS
	q.exclude = [get_rid()]
	var hits := get_world_3d().direct_space_state.intersect_shape(q, 16)
	var seen := {}
	for h in hits:
		var receiver := _receiver_of(h.collider)
		if receiver == null or seen.has(receiver):
			continue
		seen[receiver] = true
		receiver.take_hit({"amount": damage, "from": global_position, "source": self, "heavy": heavy,
			"point": global_position + Vector3.UP + facing})
	if hits.size() > 0:
		Game.hitstop(0.07 if heavy else 0.035)


func _receiver_of(node: Node) -> Node:
	var n := node
	while n:
		if n.has_method("take_hit") and n != self:
			return n
		if n.has_meta("colossus"):
			var c: Node = n.get_meta("colossus")
			return c if c.has_method("take_hit") else null
		n = n.get_parent()
	return null


# ================================================================== grip and climb
func _space() -> PhysicsDirectSpaceState3D:
	return get_world_3d().direct_space_state


func _ray(from: Vector3, to: Vector3, mask := LAYER_WORLD | LAYER_COLOSSUS) -> Dictionary:
	var q := PhysicsRayQueryParameters3D.create(from, to, mask, [get_rid()])
	return _space().intersect_ray(q)


static func is_climbable(hit: Dictionary) -> bool:
	if hit.is_empty():
		return false
	var body := hit.collider as CollisionObject3D
	if body == null:
		return false
	var owner_id := body.shape_find_owner(hit.shape)
	var shape_node := body.shape_owner_get_owner(owner_id) as Node
	if shape_node and shape_node.has_meta("climbable"):
		return shape_node.get_meta("climbable")
	return body.has_meta("climbable") and body.get_meta("climbable")


## Looks for something to hold on to around the hero; true when it caught hold.
func _try_grip() -> bool:
	if grip < 4.0:
		return false
	var chest := global_position + Vector3.UP * CHEST
	var right := facing.cross(Vector3.UP)
	var probes: Array[Vector3] = [facing, (facing + right * 0.6).normalized(), (facing - right * 0.6).normalized()]
	var vflat := Vector3(velocity.x, 0, velocity.z)
	if vflat.length() > 1.0:
		probes.append(vflat.normalized())
	for h: float in [CHEST, 1.7, 0.6]:
		for d in probes:
			var from := global_position + Vector3.UP * h
			var hit := _ray(from, from + d * 1.05)
			if is_climbable(hit):
				_grab(hit)
				return true
	# on a colossus's back: kneel and grab the fur underfoot
	if on_colossus or state == State.AIR:
		var hit := _ray(chest, global_position + Vector3.DOWN * 0.4)
		if is_climbable(hit):
			_grab(hit)
			return true
	return false


func _grab(hit: Dictionary) -> void:
	_set_grip_point(hit.collider, hit.position, hit.normal)
	_ride_body = null
	velocity = Vector3.ZERO
	set_state(State.CLIMB)
	collision_mask = LAYER_WORLD
	_place_on_grip()


func _set_grip_point(body: CollisionObject3D, p: Vector3, n: Vector3) -> void:
	_climb_body = body
	var inv := body.global_transform.affine_inverse()
	_climb_local = inv * p
	_climb_normal_local = (body.global_transform.basis.inverse() * n).normalized()


func _grip_world() -> Array:
	var xf := _climb_body.global_transform
	var n := (xf.basis * _climb_normal_local).normalized()
	return [xf * _climb_local, n]


## Up along a surface (world up flattened onto it); on near-flat tops, the hero's facing.
func _surface_up(n: Vector3) -> Vector3:
	var up := Vector3.UP - n * Vector3.UP.dot(n)
	if up.length() < 0.35:
		var f := facing - n * facing.dot(n)
		return f.normalized() if f.length() > 0.01 else up.normalized()
	return up.normalized()


func _place_on_grip() -> void:
	var g := _grip_world()
	var p: Vector3 = g[0]
	var n: Vector3 = g[1]
	var top := smoothstep(0.55, 0.8, n.y)
	var wall_feet := p + n * 0.36 - _surface_up(n) * 1.5
	var top_feet := p - facing * 0.35 + n * 0.02
	global_position = wall_feet.lerp(top_feet, top)


func _release_grip() -> void:
	_climb_body = null
	collision_mask = LAYER_WORLD | LAYER_ENEMY | LAYER_COLOSSUS


func _climb(delta: float) -> void:
	if not _climb_body or not Input.is_action_pressed("grip") or grip <= 0.0:
		_let_go(Vector3.ZERO)
		return
	var g := _grip_world()
	var p: Vector3 = g[0]
	var n: Vector3 = g[1]
	var top := n.y > 0.72

	if Input.is_action_just_pressed("jump"):
		_let_go(n * 3.5 + Vector3.UP * 6.0)
		return
	if Input.is_action_just_pressed("attack"):
		_start_stab(weak_point_near)
		return

	var mv := Controls.move_vector()
	var cam := Game.camera as SotcCamera
	var up_t := _surface_up(n)
	var right_t := (-n).cross(up_t).normalized()
	if cam:
		var cr := cam.right_flat() - n * cam.right_flat().dot(n)
		if cr.length() > 0.2 and cr.dot(right_t) < 0.0:
			right_t = -right_t
	var up_dir := up_t
	if top and cam:
		var cf := cam.forward_flat() - n * cam.forward_flat().dot(n)
		up_dir = cf.normalized() if cf.length() > 0.1 else up_t
		facing = Vector3(up_dir.x, 0, up_dir.z).normalized() if Vector2(up_dir.x, up_dir.z).length() > 0.1 else facing
	var move := (right_t * mv.x + up_dir * mv.y)
	var moving := move.length() > 0.1
	if moving:
		var step := move.normalized() * climb_speed * minf(1.0, move.length()) * delta * (1.3 if top else 1.0)
		_slide_grip(p + step, n, mv.y > 0.3 and not top)
		if state != State.CLIMB:
			return  # pulled up over an edge onto bare ground
		_climb_cycle += delta * 3.2
	else:
		_climb_cycle = lerpf(_climb_cycle, roundf(_climb_cycle), delta * 4.0)
	_place_on_grip()

	# reached the ground while climbing down a leg
	var floor_y := Terrain.height_at_world(global_position)
	if global_position.y < floor_y + 0.05 and n.y < 0.5:
		_let_go(Vector3.ZERO)
		global_position.y = floor_y + 0.05
		return

	if top:
		hold("PickUp", anim_length("PickUp") * 0.42)
	else:
		var a := "Spellcast_Raise"
		hold(a, anim_length(a) * (0.55 + 0.18 * absf(sin(_climb_cycle * PI))))


## Moves the grip point to a new spot on the surface; false when nothing there can be held.
func _slide_grip(cand: Vector3, n: Vector3, going_up: bool) -> bool:
	var hit := _ray(cand + n * 0.7, cand - n * 0.9)
	if is_climbable(hit):
		_set_grip_point(hit.collider, hit.position, hit.normal)
		return true
	if not hit.is_empty():
		return false  # armour or bare rock: no hold here
	# over an edge: try to pull up onto the top
	if going_up:
		var over := cand - n * 0.9 + Vector3.UP * 1.6
		var down := _ray(over, over + Vector3.DOWN * 2.6)
		if not down.is_empty() and down.normal.y > 0.6:
			if is_climbable(down):
				_set_grip_point(down.collider, down.position, down.normal)
			else:
				_let_go(Vector3.ZERO)
				global_position = down.position + Vector3.UP * 0.05
			return true
	# around a tight curve: look again from further out
	var again := _ray(cand + n * 1.4, cand - n * 2.2)
	if is_climbable(again):
		_set_grip_point(again.collider, again.position, again.normal)
		return true
	return false


func _let_go(push: Vector3) -> void:
	_release_grip()
	velocity = push
	_peak_y = global_position.y
	set_state(State.AIR)
	play("Jump_Idle", 0.15)


# ================================================================== stab
func _find_weak_point() -> Node3D:
	var best: Node3D = null
	var best_d := 2.4
	var chest := global_position + Vector3.UP * CHEST
	for c in Game.colossi:
		if not is_instance_valid(c) or c.get("dead"):
			continue
		for wp in c.get("weak_points"):
			if not is_instance_valid(wp) or not wp.get("active"):
				continue
			var d := chest.distance_to(wp.global_position)
			if d < best_d:
				best_d = d
				best = wp
	return best


func _start_stab(target: Node3D) -> void:
	_stab_target = target
	_stab_charge = 0.0
	_stab_released = false
	set_state(State.STAB)


func _stab(delta: float) -> void:
	var climbing := _climb_body != null
	if climbing and (not Input.is_action_pressed("grip") or grip <= 0.0):
		_let_go(Vector3.ZERO)
		return
	var a := "1H_Melee_Attack_Stab"
	var wind := anim_length(a) * 0.32
	if not _stab_released:
		_stab_charge = minf(1.0, _stab_charge + delta / 1.1)
		hold(a, wind * (0.4 + 0.6 * _stab_charge))
		if not Input.is_action_pressed("attack"):
			_stab_released = true
			_state_time = 0.0
			anim.speed_scale = 1.6
			anim.seek(wind, true)
	else:
		if _state_time >= 0.12 and _stab_target != null:
			var dmg := 6.0 + 36.0 * _stab_charge * _stab_charge
			if is_instance_valid(_stab_target) and _stab_target.global_position.distance_to(global_position + Vector3.UP * CHEST) < 2.8:
				_stab_target.call("stab", dmg, self)
				Game.hitstop(0.12 if _stab_charge > 0.8 else 0.07)
			_stab_target = null
			_stab_charge = -1.0
		elif _state_time >= 0.12 and _stab_target == null and _stab_charge >= 0.0:
			# no weak point: the blade only prods the fur
			var col := _colossus_of(_climb_body if climbing else _ride_body)
			if col and col.has_method("prodded"):
				col.call("prodded", self)
			_stab_charge = -1.0
		if _state_time >= 0.45:
			set_state(State.CLIMB if climbing else State.GROUND)
	if climbing:
		_place_on_grip()
	else:
		velocity.x = 0.0
		velocity.z = 0.0
		velocity.y -= gravity * delta
		move_and_slide()
		_after_move(delta)


# ================================================================== grip meter
func _update_grip(delta: float) -> void:
	var g := grip
	match state:
		State.CLIMB, State.STAB:
			var shake := _colossus_shake(_climb_body)
			var moving := Controls.move_vector().length() > 0.1
			grip -= (5.0 + (5.0 if moving else 0.0) + 34.0 * shake + (6.0 if state == State.STAB and not _stab_released else 0.0)) * delta
			grip_hint = false
		State.GROUND, State.ATTACK, State.ROLL:
			if _ground_time > 0.35 or on_colossus:
				grip += (14.0 if on_colossus else 38.0) * delta
		_:
			pass
	grip = clampf(grip, 0.0, max_grip)
	if state != State.CLIMB and state != State.STAB:
		grip_hint = _grip_in_reach()
	if not is_equal_approx(g, grip):
		grip_changed.emit(grip, max_grip)


func _grip_in_reach() -> bool:
	if Engine.get_physics_frames() % 6 != 0:
		return grip_hint
	var from := global_position + Vector3.UP * CHEST
	return is_climbable(_ray(from, from + facing * 1.1))


# ================================================================== being hit
func take_hit(info: Dictionary) -> void:
	if state == State.DEAD:
		return
	var fall: bool = info.get("fall", false)
	if not fall and Game.now() < invulnerable_until:
		return
	hp = maxf(0.0, hp - float(info.get("amount", 0.0)))
	hp_changed.emit(hp, max_hp)
	if hp <= 0.0:
		_die()
		return
	if info.get("knockdown", false):
		var from: Vector3 = info.get("from", global_position - facing)
		var away := global_position - from
		away.y = 0.0
		away = away.normalized() if away.length() > 0.1 else -facing
		if state == State.CLIMB or state == State.STAB:
			_release_grip()
		velocity = away * float(info.get("knock", 7.0)) + Vector3.UP * float(info.get("lift", 5.0))
		_peak_y = global_position.y
		set_state(State.KNOCKED)
		_knock_landed = -1.0
		invulnerable_until = Game.now() + 1.6
		play("Hit_B", 0.05)
	elif state == State.GROUND:
		play("Hit_A", 0.05)
		invulnerable_until = Game.now() + 0.4
	Game.shake(0.25)


func _knocked(delta: float) -> void:
	velocity.y -= gravity * delta
	if is_on_floor() and _state_time > 0.2:
		velocity.x = move_toward(velocity.x, 0.0, 30.0 * delta)
		velocity.z = move_toward(velocity.z, 0.0, 30.0 * delta)
		if _knock_landed < 0.0:
			_knock_landed = _state_time
			var fall := _peak_y - global_position.y
			if fall > safe_fall:
				take_hit({"amount": (fall - safe_fall) * 7.0, "fall": true})
				if state == State.DEAD:
					return
			play("Lie_Down", 0.08, 1.6)
		var lying := _state_time - _knock_landed
		if lying > 0.7:
			play("Lie_StandUp", 0.1, 1.4)
		if lying > 0.7 + anim_length("Lie_StandUp") / 1.4 * 0.85:
			set_state(State.GROUND)
	move_and_slide()
	_after_move(delta)


func _die() -> void:
	if state == State.CLIMB or state == State.STAB:
		_release_grip()
	set_state(State.DEAD)
	play("Death_A", 0.1)
	died.emit()
	Game.hero_died.emit()


func _dead(delta: float) -> void:
	velocity.x = move_toward(velocity.x, 0.0, 20.0 * delta)
	velocity.z = move_toward(velocity.z, 0.0, 20.0 * delta)
	velocity.y -= gravity * delta
	move_and_slide()


func revive(at: Vector3) -> void:
	_release_grip()
	_ride_body = null
	global_position = at
	velocity = Vector3.ZERO
	hp = max_hp
	grip = max_grip
	hp_changed.emit(hp, max_hp)
	grip_changed.emit(grip, max_grip)
	_peak_y = at.y
	set_state(State.GROUND)
	_anim_name = ""
	play("Idle", 0.0)


# ================================================================== model
func _orient_model(delta: float) -> void:
	if not model:
		return
	var want: Basis
	if _climb_body and (state == State.CLIMB or state == State.STAB):
		var g := _grip_world()
		var n: Vector3 = g[1]
		if n.y > 0.72:
			want = _basis_facing(facing, n)
		else:
			var up := _surface_up(n)
			var z := -n
			var x := up.cross(z).normalized()
			want = Basis(x, z.cross(x).normalized(), z)
			# swinging from the fur while the colossus shakes
			var shake := _colossus_shake(_climb_body)
			if shake > 0.05:
				want = want.rotated(n.cross(Vector3.UP).normalized() if absf(n.y) < 0.95 else Vector3.RIGHT,
					sin(Game.now() * 13.0) * 0.45 * shake)
	else:
		want = _basis_facing(facing, Vector3.UP)
	var cur := model.basis.orthonormalized()
	var q := Quaternion(cur).slerp(Quaternion(want.orthonormalized()), 1.0 - exp(-14.0 * delta))
	model.basis = Basis(q).scaled(Vector3.ONE * model_scale)


## A basis whose +Z (the KayKit model's front) points along `fwd`, upright along `up`.
static func _basis_facing(fwd: Vector3, up: Vector3) -> Basis:
	var z := (fwd - up * fwd.dot(up))
	if z.length() < 0.01:
		z = Vector3.FORWARD
	z = z.normalized()
	var x := up.cross(z).normalized()
	return Basis(x, z.cross(x).normalized(), z)
