class_name Hero
extends CharacterBody3D
## The hero, played from behind like Wander in Shadow of the Colossus: runs, sprints, jumps,
## Lướt (dodge roll with a moment of invulnerability), a three-hit sword combo, and the grip:
## hold "grip" to cling to anything furry (colossus fur, climbable rock), climb it while the
## grip meter lasts, ride a colossus's back, and stab its glowing weak points with a charged thrust.
##
## Its life, statuses, level and the six ability scores follow the Unity game's rules (Health,
## StatusEffects, PlayerStats children); energy regenerates 3.5/s, life 2.5/s after 5 s unhurt;
## Z X C drink the three potions; a Lướt that dodges a blow in its first 0.15 s is Hoàn Hảo.

signal hp_changed(hp: float, max_hp: float)
signal grip_changed(grip: float, max_grip: float)
signal energy_changed(energy: float, max_energy: float)
## Hurt by a blow (not damage over time): for the screen's red flash.
signal hurt(amount: float)
signal perfect_dodge
signal died

enum State { GROUND, AIR, ROLL, CLIMB, STAB, KNOCKED, DEAD }

const LAYER_WORLD := 1
const LAYER_ENEMY := 4
const LAYER_COLOSSUS := 8
const CHEST := 1.15

@export var hero_name := "Hồng Anh"
## Until the character creator comes (stage 5) the hero is a human Chiến Binh with a sword.
@export var class_id := "fighter"
@export var race_id := "human"
@export var weapon_id := "sword"
@export var max_grip := 100.0
@export var walk_speed := 2.2
@export var run_speed := 5.4
@export var sprint_speed := 8.2
@export var accel := 18.0
@export var air_accel := 5.0
@export var gravity := 21.0
@export var jump_speed := 7.4
@export var climb_speed := 1.8
@export var model_scale := 1.0
## Falls shorter than this do not hurt.
@export var safe_fall := 7.5

var grip := 100.0
var energy := 50.0
var state := State.GROUND
var health: Health
var status: StatusEffects
var stats: PlayerStats
var inventory: Inventory
var skills: PlayerSkills
## ability id -> {count, last}: where each weapon combo stands (ComboEffect).
var combo_state := {}
var combo_step := 0
## Buffs: id -> {"spec": BuffSpec, "until": time}; their sum is below.
var buffs := {}
var imbue_power := 0.0
var imbue_charge := 0
var attack_speed_bonus := 0.0
var chill_attackers := 0
var _buff_speed := 1.0
var _buff_dealt := 1.0
var _action_until := 0.0
var _action_dir := Vector3.FORWARD
var _action_move := 1.0
var _action_ability: Ability
var _counter_skill := ""
var _counter_until := 0.0
var _roll_speed_now := 10.0
var _roll_time_now := 0.48
var _roll_style := "roll"
var _air_dash := false
var hp: float:
	get:
		return health.hp if health else 0.0
var max_hp: float:
	get:
		return health.max_hp if health else 1.0
var level: int:
	get:
		return stats.level if stats else 1
var max_energy: float:
	get:
		return Combat.round_half_even(stats.max_energy) if stats else 50.0
## Until when the next skill hits 30% harder (Lướt Hoàn Hảo).
var perfect_bonus_until := 0.0

const ENERGY_REGEN := 3.5
const HP_REGEN := 2.5
const HP_REGEN_DELAY := 5.0
const POTIONS := ["potion_red", "potion_blue", "potion_green"]
const POTION_COOLDOWN := 1.0
const DASH_INVULNERABLE := 0.28

var _knock := Vector3.ZERO
var _potion_ready := 0.0
var _regen_acc := 0.0
var _perfect_until := 0.0
var _perfect_used := false
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
var _sprinting := false
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
## Model-specific clip names (HeroModel maps the KayKit names this script uses).
var _resolve: Callable

const SWINGS := ["1H_Melee_Attack_Slice_Diagonal", "1H_Melee_Attack_Slice_Horizontal", "1H_Melee_Attack_Chop"]
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
		if model.has_method("resolve"):
			_resolve = model.resolve
		for n in model.find_children("*", "MeshInstance3D", true, false):
			(n as MeshInstance3D).layers = 2 | HeroModel.PORTRAIT_LAYER
			if n.name in HIDDEN_GEAR:
				n.visible = false
	if anim:
		for a in anim.get_animation_list():
			var res := anim.get_animation(a)
			_anim_len[a] = res.length
			if a in LOOPING:
				res.loop_mode = Animation.LOOP_LINEAR
		play("Idle")
	_build_components()
	grip = max_grip
	add_to_group("heroes")
	if not is_instance_valid(Game.hero):
		Game.hero = self
	_peak_y = global_position.y


func _build_components() -> void:
	status = StatusEffects.new()
	status.name = "Status"
	add_child(status)
	health = Health.new()
	health.name = "Health"
	health.team = Combat.Team.PLAYER
	health.display_name = hero_name
	health.head_height = 2.0
	add_child(health)
	stats = PlayerStats.new()
	stats.name = "Stats"
	stats.class_id = class_id
	stats.race_id = race_id
	stats.weapon_id = weapon_id
	add_child(stats)
	inventory = Inventory.new()
	inventory.name = "Inventory"
	add_child(inventory)
	skills = PlayerSkills.new()
	skills.name = "Skills"
	add_child(skills)
	skills.apply_kit(class_id, weapon_id)
	# the Unity game's starting kit
	for kit in [["potion_red", 4], ["potion_blue", 4], ["potion_green", 2], ["apple", 3]]:
		inventory.add(kit[0], kit[1])
	energy = max_energy
	health.damaged.connect(_on_damaged)
	health.died.connect(func(_d: DamageInfo) -> void: _die())
	health.healed.connect(func(_a: float) -> void: hp_changed.emit(hp, max_hp))
	health.evaded.connect(_on_evaded)
	stats.changed.connect(func() -> void:
		energy = minf(energy, max_energy)
		hp_changed.emit(hp, max_hp)
		energy_changed.emit(energy, max_energy))
	stats.leveled_up.connect(_on_level_up)


## The colossus this hero is holding on to or standing on, if any.
func attached_colossus() -> Node:
	if _climb_body and (state == State.CLIMB or state == State.STAB):
		return _colossus_of(_climb_body)
	if _ride_body and is_instance_valid(_ride_body):
		return _colossus_of(_ride_body)
	return null


# ================================================================== animation
func _clip(anim_name: String) -> String:
	return _resolve.call(anim_name) if _resolve.is_valid() else anim_name


func play(anim_name: String, blend := 0.15, speed := 1.0) -> void:
	var clip := _clip(anim_name)
	if not anim or not anim.has_animation(clip):
		return
	if _anim_name == anim_name:
		anim.speed_scale = speed
		return
	_anim_name = anim_name
	anim.speed_scale = speed
	anim.play(clip, blend)


## Hold one moment of an animation (used for the climbing and stab poses).
func hold(anim_name: String, t: float, blend := 0.12) -> void:
	var clip := _clip(anim_name)
	if not anim or not anim.has_animation(clip):
		return
	if _anim_name != anim_name:
		_anim_name = anim_name
		anim.play(clip, blend)
	anim.speed_scale = 0.0
	anim.seek(clampf(t, 0.0, _anim_len.get(clip, 1.0)), true)


func anim_length(anim_name: String) -> float:
	return _anim_len.get(_clip(anim_name), 1.0)


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
	return state == State.DEAD or (health != null and health.is_dead)


func _physics_process(delta: float) -> void:
	_state_time += delta
	health.invulnerable = Game.now() < invulnerable_until
	_update_vitals(delta)
	_update_buffs()
	_follow_attachment()
	weak_point_near = _find_weak_point()
	match state:
		State.GROUND, State.AIR:
			_locomotion(delta)
		State.ROLL:
			_roll(delta)
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
	var cam := Game.cam()
	if not cam:
		return Vector3(mv.x, 0, -mv.y)
	return cam.right_flat() * mv.x + cam.forward_flat() * mv.y


func _locomotion(delta: float) -> void:
	var stunned := status.is_stunned()
	var rooted := status.is_rooted()
	var dir := Vector3.ZERO if stunned else _move_dir()
	var amount := dir.length()
	var on_floor := is_on_floor()
	if not stunned:
		_potions()

	# Shift: Lướt the moment it is pressed (on the ground or in the air); keep holding it to sprint
	if Input.is_action_just_pressed("dodge") and not stunned and not rooted:
		_dodge_down_at = Game.now()
		skills.request(PlayerSkills.DASH_SLOT)
		if state == State.ROLL:
			return
	_sprinting = Input.is_action_pressed("dodge") and Game.now() - _dodge_down_at >= Controls.DODGE_TAP_TIME and amount > 0.3

	if not stunned and Input.is_action_pressed("grip") and _try_grip():
		return
	if not stunned and Input.is_action_just_pressed("attack") and weak_point_near and on_colossus:
		_start_stab(weak_point_near)
		return
	if not stunned:
		_skill_keys()

	var acting := action_remaining() > 0.0
	var speed := walk_speed if amount < 0.45 else (sprint_speed if _sprinting and not acting else run_speed)
	speed *= status.speed_multiplier() * stats.speed_multiplier * _buff_speed
	if acting:
		speed *= _action_move
	var want := dir.normalized() * speed * minf(1.0, amount * 1.6) if amount > 0.05 else Vector3.ZERO
	var a := accel if on_floor else air_accel
	var flat := Vector3(velocity.x - _knock.x, 0, velocity.z - _knock.z).move_toward(want, a * delta * (1.6 if want == Vector3.ZERO and on_floor else 1.0))
	_knock = _knock.move_toward(Vector3.ZERO, 18.0 * delta)
	velocity.x = flat.x + _knock.x
	velocity.z = flat.z + _knock.z
	if acting:
		facing = facing.slerp(_action_dir, 1.0 - exp(-30.0 * delta)).normalized()
	elif amount > 0.05:
		facing = facing.slerp(dir.normalized(), 1.0 - exp(-(12.0 if on_floor else 4.0) * delta)).normalized()

	if on_floor and not stunned and not rooted:
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
		if stunned:
			play("Hit_A" if status.is_frozen() else "Idle", 0.1, 0.0 if status.is_frozen() else 1.0)
		elif _cast_anim_playing():
			pass
		elif _state_time > 0.15 or _anim_name != "Jump_Land":
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
		_fall_damage(fall)
		Game.shake(clampf(fall / 30.0, 0.15, 0.6))
	if not is_dead() and state != State.KNOCKED:
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
## A DashEffect: Lướt rolls (a little longer than the 2D dash so the roll reads), charges run,
## blinks vanish for the moment they travel. Invulnerable for `invuln` seconds; Lướt opens the
## Lướt Hoàn Hảo window.
func dash(dir: Vector3, distance: float, time: float, invuln: float, perfect: bool, style := "roll") -> void:
	if state == State.CLIMB or state == State.STAB or state == State.KNOCKED or is_dead():
		return
	_roll_dir = Vector3(dir.x, 0, dir.z).normalized() if Vector2(dir.x, dir.z).length() > 0.05 else facing
	facing = _roll_dir
	_roll_style = style
	_roll_time_now = time * (2.2 if style == "roll" else (1.4 if style == "charge" else 1.0))
	_roll_speed_now = distance / _roll_time_now
	invulnerable_until = maxf(invulnerable_until, Game.now() + invuln)
	if perfect:
		_perfect_until = Game.now() + DB.c("perfectWindow", 0.15)
		_perfect_used = false
	_knock = Vector3.ZERO
	_air_dash = not is_on_floor()
	if _air_dash:
		velocity.y = maxf(velocity.y, 0.5)
	set_state(State.ROLL)
	match style:
		"roll":
			play("Dodge_Forward", 0.05, anim_length("Dodge_Forward") / maxf(0.2, _roll_time_now * 1.3))
		"charge":
			play("Running_B", 0.05, 1.6)
		_:
			if model:
				model.visible = false


func _roll(delta: float) -> void:
	var t := _state_time / _roll_time_now
	var spd := _roll_speed_now * ((1.0 - t * t * 0.6) if _roll_style == "roll" else 1.0)
	velocity.x = _roll_dir.x * spd
	velocity.z = _roll_dir.z * spd
	# an air dash glides: gravity barely pulls until it ends
	velocity.y -= gravity * delta * (0.2 if _air_dash else 1.0)
	if not status.is_stunned():
		_skill_keys()
	move_and_slide()
	_after_move(delta)
	if _state_time >= _roll_time_now:
		if model:
			model.visible = true
		velocity.x *= 0.3
		velocity.z *= 0.3
		set_state(State.GROUND if is_on_floor() else State.AIR)


# ================================================================== skills
func move_dir() -> Vector3:
	return _move_dir()


func _skill_keys() -> void:
	for i in Controls.SKILL_ACTIONS.size():
		if Input.is_action_just_pressed(Controls.SKILL_ACTIONS[i]):
			skills.request(i)


func can_cast() -> bool:
	return state == State.GROUND or state == State.AIR or state == State.ROLL


func action_remaining() -> float:
	return maxf(0.0, _action_until - StatusEffects._scaled_now())


## A skill's pose: faces its direction, slows walking, plays its animation.
func begin_action(a: Ability, dir: Vector3) -> void:
	_action_ability = a
	_action_until = StatusEffects._scaled_now() + a.lock_time()
	_action_move = a.move_while_casting()
	_action_dir = Vector3(dir.x, 0, dir.z).normalized() if Vector2(dir.x, dir.z).length() > 0.01 else facing
	if a.anim_base() != "":
		facing = _action_dir
	_play_cast_anim.call_deferred(a)


func _play_cast_anim(a: Ability) -> void:
	var name_ := ""
	if a.anim_base() == "attack":
		if a.has_tag(Ability.TAG_PROJECTILE):
			name_ = "Throw" if not a.id.contains("arrow") and a.id != "piercing" and a.id != "multishot" else "1H_Ranged_Shoot"
		elif a.has_tag(Ability.TAG_AREA) or a.has_tag(Ability.TAG_CHANNEL):
			name_ = "2H_Melee_Attack_Spin" if a.id == "whirl" or a.id == "bladestorm" else "1H_Melee_Attack_Chop"
		elif a.has_tag(Ability.TAG_MOVEMENT):
			name_ = ""
		else:
			name_ = SWINGS[combo_step % SWINGS.size()]
	elif a.anim_base() == "cast":
		if a.has_tag(Ability.TAG_PROJECTILE):
			name_ = "Spellcast_Shoot"
		elif a.has_tag(Ability.TAG_SUPPORT):
			name_ = "Spellcast_Raise"
		else:
			name_ = "Spellcast_Long"
	if name_ == "" or state != State.GROUND and state != State.AIR:
		return
	var length := anim_length(name_)
	_anim_name = ""
	play(name_, 0.05, clampf(length / maxf(a.lock_time() * 2.0, 0.35), 1.0, 3.0))


func _cast_anim_playing() -> bool:
	return anim != null and anim.is_playing() and (_anim_name in SWINGS or _anim_name.begins_with("Spellcast") \
		or _anim_name in ["Throw", "1H_Ranged_Shoot", "2H_Melee_Attack_Spin", "1H_Melee_Attack_Chop"]) \
		and anim.current_animation_position < anim.current_animation_length * 0.8


## Lướt Hoàn Hảo's bonus: the next non-Movement skill hits 30% harder (and its first hit on a
## boss adds 25 to the Trấn Áp bar).
func take_perfect_bonus(a: Ability) -> float:
	if a.has_tag(Ability.TAG_MOVEMENT) or Game.now() >= perfect_bonus_until:
		return 1.0
	perfect_bonus_until = 0.0
	buffs.erase("perfect_dodge")
	_counter_skill = a.display_name()
	_counter_until = Game.now() + DB.c("perfectBonusSeconds", 1.5)
	return DB.c("perfectDamageMultiplier", 1.3)


## Once, for the first hit of the skill cast with the Hoàn Hảo bonus.
func counter_poise(skill_name: String) -> float:
	if skill_name == _counter_skill and Game.now() < _counter_until:
		_counter_skill = ""
		return DB.c("perfectCounterPoise", 25.0)
	return 0.0


func damage_dealt_multiplier() -> float:
	return stats.damage_dealt * _buff_dealt


func add_buff(spec: Dictionary) -> void:
	var id := String(spec.get("id", "buff"))
	var fresh := not buffs.has(id)
	buffs[id] = {"spec": spec, "until": StatusEffects._scaled_now() + float(spec.get("duration", 5.0))}
	if fresh and Game.has_screen():
		DamageNumbers.show(global_position + Vector3.UP * 2.3, String(spec.get("displayName", id)), "text")
	_update_buffs()


func _update_buffs() -> void:
	var t := StatusEffects._scaled_now()
	var taken := 1.0
	_buff_speed = 1.0
	_buff_dealt = 1.0
	attack_speed_bonus = 0.0
	imbue_power = 0.0
	imbue_charge = 0
	chill_attackers = 0
	var crit := 0.0
	var immune := false
	for id in buffs.keys():
		if t >= buffs[id].until:
			buffs.erase(id)
			continue
		var s: Dictionary = buffs[id].spec
		taken *= float(s.get("damageTakenMultiplier", 1.0))
		_buff_speed *= float(s.get("speedMultiplier", 1.0))
		var dealt := float(s.get("damageDealtMultiplier", 1.0))
		_buff_dealt *= dealt if dealt > 0.0 else 1.0
		attack_speed_bonus += float(s.get("attackSpeedBonus", 0.0))
		imbue_power += float(s.get("imbuePower", 0.0))
		imbue_charge += int(s.get("imbueCharge", 0))
		crit += float(s.get("critBonus", 0.0))
		chill_attackers += int(s.get("chillAttackers", 0))
		immune = immune or int(s.get("stunImmune", 0)) == 1
	health.damage_taken_multiplier = taken
	status.stun_immune = immune
	stats.buff_crit = crit


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
	var cam := Game.cam()
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
		var a := "Climb"
		hold(a, anim_length(a) * (0.12 + 0.1 * absf(sin(_climb_cycle * PI))))


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
			# a full thrust is worth twice the hero's Attack
			var dmg := stats.physical_attack * (0.25 + 1.75 * _stab_charge * _stab_charge)
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
		State.GROUND, State.ROLL:
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
func _on_damaged(d: DamageInfo, amount: float) -> void:
	hp_changed.emit(hp, max_hp)
	if d.dot or is_dead():
		return
	hurt.emit(amount)
	if chill_attackers > 0 and d.source and is_instance_valid(d.source) and d.source is Node3D \
			and (d.source as Node3D).global_position.distance_to(global_position) <= 3.0 * AbilityRunner.SPACE:
		var sh := Combat.health_of(d.source)
		if sh and sh.status:
			sh.status.chill(chill_attackers)
	if d.knockdown:
		var away := d.direction
		away.y = 0.0
		away = away.normalized() if away.length() > 0.1 else -facing
		if state == State.CLIMB or state == State.STAB:
			_release_grip()
		velocity = away * d.knockback + Vector3.UP * d.lift
		_knock = Vector3.ZERO
		_peak_y = global_position.y
		set_state(State.KNOCKED)
		_knock_landed = -1.0
		invulnerable_until = Game.now() + 1.6
		play("Hit_B", 0.05)
	elif amount >= 12.0 and state == State.GROUND:
		play("Hit_A", 0.05)
	if self == Game.hero:
		Game.shake(clampf(amount / 60.0, 0.1, 0.45))


## Knockback from a landed hit (Combat.on_hit_feedback); decays at 18 m/s per second.
func add_knockback(impulse: Vector3) -> void:
	if state == State.CLIMB or state == State.STAB or state == State.KNOCKED or is_dead():
		return
	_knock += Vector3(impulse.x, 0, impulse.z)


func flash(_color: Color, _strength: float, _seconds: float) -> void:
	pass


func _fall_damage(fall: float) -> void:
	var d := DamageInfo.make((fall - safe_fall) * 7.0, Combat.Team.NEUTRAL, null, global_position, Vector3.UP)
	d.pure = true
	d.skill_name = "Rơi"
	health.take_damage(d)


## Lướt Hoàn Hảo: a blow dodged in the first moment of a Lướt.
func _on_evaded(d: DamageInfo) -> void:
	if _perfect_used or d.contact or d.dot or Game.now() > _perfect_until or is_dead():
		return
	_perfect_used = true
	energy = minf(max_energy, energy + DB.c("perfectEnergy", 15.0))
	energy_changed.emit(energy, max_energy)
	perfect_bonus_until = Game.now() + DB.c("perfectBonusSeconds", 1.5)
	add_buff({"id": "perfect_dodge", "displayName": "Hoàn Hảo", "duration": DB.c("perfectBonusSeconds", 1.5)})
	if Game.has_screen():
		DamageNumbers.show(global_position + Vector3.UP * 2.2, "Hoàn Hảo!", "crit", true)
		Fx.burst(global_position + Vector3.UP, Vector3.UP, Color(1.0, 0.85, 0.4, 0.9), 30, 5.0, 0.3, 0.6, 90.0, 0.0, true)
	if not Game.is_online():
		Game.slow_motion(DB.c("perfectTimeScale", 0.35), DB.c("perfectSlowSeconds", 0.3), DB.c("perfectSlowEase", 0.1))
	perfect_dodge.emit()


func _update_vitals(delta: float) -> void:
	if is_dead() or not Game.is_authority():
		return
	if energy < max_energy:
		energy = minf(max_energy, energy + ENERGY_REGEN * delta)
		energy_changed.emit(energy, max_energy)
	if health.hp < health.max_hp and Game.now() - health.last_damage_time > HP_REGEN_DELAY:
		_regen_acc += HP_REGEN * delta
		if _regen_acc >= 1.0:
			var whole := floorf(_regen_acc)
			_regen_acc -= whole
			health.heal(whole, false)
	else:
		_regen_acc = 0.0


func _potions() -> void:
	for i in 3:
		if Input.is_action_just_pressed("potion_%d" % (i + 1)):
			use_item(POTIONS[i])


## Drinks or eats something from the bag (shared 1 s cooldown).
func use_item(id: String) -> bool:
	if is_dead() or Game.now() < _potion_ready:
		return false
	if inventory.count(id) <= 0:
		if Game.has_screen():
			DamageNumbers.show(global_position + Vector3.UP * 2.2, "Hết bình!", "text")
		return false
	_potion_ready = Game.now() + POTION_COOLDOWN
	inventory.remove(id, 1)
	var it := DB.item(id)
	var heal := float(it.get("healAmount", 0))
	var en := float(it.get("energyAmount", 0))
	if heal > 0.0:
		health.heal(heal)
	if en > 0.0:
		energy = minf(max_energy, energy + en)
		energy_changed.emit(energy, max_energy)
		if Game.has_screen():
			DamageNumbers.show(global_position + Vector3.UP * 2.0 + Vector3.RIGHT * 0.3, "+%d" % roundi(en), "shock")
	if int(it.get("cleanse", 0)) == 1:
		status.cleanse()
	var col := Color(0.5, 1.0, 0.5) if int(it.get("cleanse", 0)) == 1 else (Color(0.5, 0.7, 1.0) if heal <= 0.0 else Color(1.0, 0.45, 0.45))
	Fx.burst(global_position + Vector3.UP, Vector3.UP, Color(col, 0.8), 20, 2.5, 0.3, 0.8, 60.0, 1.0, true)
	return true


func _on_level_up(new_level: int) -> void:
	energy = max_energy
	energy_changed.emit(energy, max_energy)
	hp_changed.emit(hp, max_hp)
	if Game.has_screen():
		DamageNumbers.show(global_position + Vector3.UP * 2.4, "Cấp %d!" % new_level, "crit", true)
		Fx.burst(global_position + Vector3.UP, Vector3.UP, Color(1.0, 0.9, 0.5, 0.9), 50, 6.0, 0.35, 1.2, 40.0, 2.0, true)


func _knocked(delta: float) -> void:
	velocity.y -= gravity * delta
	if is_on_floor() and _state_time > 0.2:
		velocity.x = move_toward(velocity.x, 0.0, 30.0 * delta)
		velocity.z = move_toward(velocity.z, 0.0, 30.0 * delta)
		if _knock_landed < 0.0:
			_knock_landed = _state_time
			var fall := _peak_y - global_position.y
			if fall > safe_fall:
				_fall_damage(fall)
				if is_dead():
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
	_knock = Vector3.ZERO
	health.reset_health()
	status.cleanse()
	energy = max_energy
	energy_changed.emit(energy, max_energy)
	buffs.clear()
	_update_buffs()
	grip = max_grip
	if model:
		model.visible = true
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
