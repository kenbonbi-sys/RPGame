class_name GauMa
extends Colossus
## Gấu Ma Rừng Già, the forest's colossus: a bear the size of a hill, mossy bark plates on its
## flanks, a pale bone mask, fur everywhere else. It walks on four procedurally stepping legs.
##
## Climb: the fur on the backs of its legs and on its rump, back, neck and crown. The bark plates,
## the chest plate, the mask and the paws cannot be held.
## Weak points: the back of the left hind calf (stab it and the bear kneels), the hump of its back
## and the crown of its head (these two carry its life).
## Skills (from the 2D boss): Vồ, Dậm Đất, Ném Đá Lớn (the rock stays as a Tảng Đá Lớn), Chụp Quăng
## (landing next to a Tảng Đá Lớn stuns it: its head drops to the ground), Cuồng Nộ below half its
## life, and Lắc Mình when heroes ride it. Húc, a Dauntless charge: it lowers its head (rays glow
## on it) and rams; a heavy hit on its head in that window interrupts it and knocks it down.
## Breakable parts (Colossus): the head (its antlers snap off) and each leg (the bark shin falls).

enum Mode { IDLE, WALK, SWIPE, STOMP, THROW, POUNCE, CHARGE, SHAKE, KNEEL, STUNNED, DYING, DEAD }

## Every length below is in "bear units"; S turns them into metres (about 10 m to the crown).
const S := 0.8
## Reach of its skills, set when it was 1.4 bear units to the metre; shrinks with it.
const REACH := S / 1.4
const STAND_HEIGHT := 7.4 * S
const STRIDE := 2.8 * S
const STEP_TIME := 0.85
const LIFT := 1.5 * S
const NECK_BASE := Vector3(0, 1.4, -8.6) * S
const HEAD_OFFSET := Vector3(0, 1.4, -4.6) * S
const DEN_RADIUS := 38.0
const ROCKS := ["rock_boulder_5", "rock_boulder_6", "rock_boulder_7"]


class Leg:
	var name := ""
	var hip := Vector3.ZERO      ## body space
	var l1 := 4.0
	var l2 := 4.0
	var r1 := 1.4
	var r2 := 1.2
	var bend := 1.0              ## +1 joint bends toward the tail, -1 toward the head
	var group := 0
	var foot := Vector3.ZERO     ## world
	var stepping := false
	var from := Vector3.ZERO
	var to := Vector3.ZERO
	var t := 0.0
	var override_on := false
	var override_pos := Vector3.ZERO
	var override_rate := 10.0
	var upper: AnimatableBody3D
	var lower: AnimatableBody3D
	var paw: AnimatableBody3D


var mode := Mode.IDLE
var mode_time := 0.0
var rage := false
var target: Node3D
## Tests and AutoShot drive the bear by hand: it does not pick skills by itself.
var scripted := false

var height := STAND_HEIGHT
var pitch := 0.0
var roll := 0.0
var pitch_pivot_z := 3.6 * S
var head_look := Vector2.ZERO    ## yaw, pitch
var body_xf := Transform3D.IDENTITY
var head_xf := Transform3D.IDENTITY

var torso: AnimatableBody3D
var neck: AnimatableBody3D
var head: AnimatableBody3D
var legs: Array[Leg] = []
var eye_mat: StandardMaterial3D
var leg_point: WeakPoint

var _cooldowns := {"swipe": 2.0, "stomp": 4.0, "throw": 3.0, "pounce": 8.0, "charge": 6.0, "shake": 0.0}
var _on_me_time := 0.0
var _speed := 0.0
var _phase := 0.0
var _wander_goal := Vector3.ZERO
var _wander_wait := 2.0
var _goal_height := STAND_HEIGHT
var _goal_pitch := 0.0
var _goal_roll := 0.0
var _goal_head := Vector2.ZERO
var _head_free := true
var _event_done := {}
var _swipe_leg: Leg
var _swipe_side := 1.0
var _stomp_point := Vector3.ZERO
var _jump_from := Vector3.ZERO
var _jump_to := Vector3.ZERO
var _held_rock: Node3D
var _hit_this_swing := {}
## Húc: where the run goes, who was rammed, and whether a heavy hit on the head would interrupt.
var _charge_from := Vector3.ZERO
var _charge_to := Vector3.ZERO
var _charge_open := false
var _rammed := {}
var _step_scale := 1.0
## Knocked down (STUNNED): for how long, and whether it is a stagger (head on the ground) or a flinch.
var _stun_for := 7.0
var _stun_down := true
var _prompt: Sprite3D
var _stars: Node3D
var _antlers: MeshInstance3D
var _greaves := {}

const LEG_NAMES := {"FL": "Chân trước trái", "FR": "Chân trước phải", "HL": "Chân sau trái", "HR": "Chân sau phải"}
const CHARGE_WIND := 1.1
const CHARGE_RUN := 1.9
const CHARGE_STOP := 0.9


func _ready() -> void:
	display_name = "Gấu Ma Rừng Già"
	level = 6
	# BossBear's chest, one per hero who fought it
	loot = [
		{"item": "claw", "chance": 1.0, "min": 1, "max": 1},
		{"item": "pelt", "chance": 1.0, "min": 1, "max": 1},
		{"item": "gem_red", "chance": 0.7, "min": 1, "max": 1},
		{"item": "gem_blue", "chance": 0.7, "min": 1, "max": 1},
		{"item": "potion_red", "chance": 1.0, "min": 2, "max": 2},
		{"item": "meat", "chance": 1.0, "min": 1, "max": 2},
		{"item": "coin", "chance": 1.0, "min": 12, "max": 12},
	]
	_init_materials(Color(0.27, 0.19, 0.13), Color(0.56, 0.42, 0.29), Color(0.42, 0.32, 0.23))
	process_physics_priority = -10
	home = global_transform
	make_life()
	_build()
	part_broken.connect(_on_part_broken)
	_snap_to_ground()
	_plant_feet()
	_update_body(0.0)
	_place_legs()
	health_changed.emit(health.hp, health.max_hp)


# ================================================================== the body
const SCULPT := "res://assets/creatures/gau_ma.glb"


## Colliders here, looks from the sculpt (Tools/Sculpt/bear.py, built over these same shapes in
## bear units, so the fur heroes grab is where the mesh shows it).
func _build() -> void:
	draw_shapes = false
	var looks := CreatureArt.sculpt(SCULPT)
	var skin := CreatureArt.painted(Color.WHITE, 0.1, 0.0, 0.85)
	skin.set_shader_parameter("paint_scale", 1.4)
	var glow_mat := CreatureArt.lamp(Color(0.45, 1.0, 0.85), 1.8)
	flash_materials = [skin, glow_mat]

	torso = make_part("Torso")
	add_capsule(torso, 3.7 * S, 16.0 * S, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 0.5, -2.0) * S), true)
	add_sphere(torso, 3.4 * S, Transform3D(Basis.IDENTITY, Vector3(0, 2.2, -5.5) * S), true)   # shoulder hump
	for side in [-1.0, 1.0]:
		var flank := Transform3D(Basis(Vector3.FORWARD, side * 0.2), Vector3(side * 3.45, -0.3, -3.2) * S)
		add_box(torso, Vector3(0.8, 3.6, 9.5) * S, flank, null, false)
	add_box(torso, Vector3(5.8, 4.4, 1.0) * S, Transform3D(Basis(Vector3.RIGHT, 0.28), Vector3(0, -0.6, -9.7) * S), null, false)
	_look(torso, looks["Torso"], skin)
	_look(torso, looks["TorsoGlow"], glow_mat)
	add_weak_point(torso, Vector3(0, 4.18, 1.6) * S, Vector3(0, 1, 0.2), 120.0, true, 2.4 * S / 1.4)

	neck = make_part("Neck")
	var nd := Vector3(0, 1.0, -3.4) * S
	add_capsule(neck, 2.4 * S, 5.6 * S, Transform3D(_axis_basis(nd.normalized()), nd * 0.5), true)
	_look(neck, looks["Neck"], skin)

	head = make_part("Head")
	add_sphere(head, 2.7 * S, Transform3D.IDENTITY, true)
	# the bone mask over the face, the muzzle and the jaw (none of it can be held)
	add_box(head, Vector3(3.6, 2.8, 1.0) * S, Transform3D(Basis.IDENTITY, Vector3(0, 0.1, -2.3) * S), null, false)
	var snout := CapsuleShape3D.new()
	snout.radius = 0.95 * S
	snout.height = 3.4 * S
	var scs := CollisionShape3D.new()
	scs.shape = snout
	scs.transform = Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, -1.1, -3.0) * S)
	scs.set_meta("climbable", false)
	head.add_child(scs)
	_look(head, looks["Head"], skin)
	if looks.has("Antlers"):
		_antlers = _look(head, looks["Antlers"], skin)
	eye_mat = StandardMaterial3D.new()
	eye_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	eye_mat.albedo_color = Color(0.7, 1.0, 0.85)
	eye_mat.emission_enabled = true
	eye_mat.emission = Color(0.6, 1.0, 0.8)
	eye_mat.emission_energy_multiplier = 4.0
	_look(head, looks["HeadGlow"], eye_mat)
	add_weak_point(head, Vector3(0, 2.5, 0.45) * S, Vector3(0, 1, 0.15), 120.0, true, 2.2 * S / 1.4)
	var glow := OmniLight3D.new()
	glow.light_color = Color(0.6, 1.0, 0.8)
	glow.light_energy = 2.0
	glow.omni_range = 9.0 * S / 1.4
	glow.position = Vector3(0, 0.4, -3.8) * S
	head.add_child(glow)

	_add_leg("FL", Vector3(-2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 0)
	_add_leg("FR", Vector3(2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 1)
	_add_leg("HL", Vector3(-2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 1)
	_add_leg("HR", Vector3(2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 0)
	for leg in legs:
		var kind := "Front" if leg.bend > 0.0 else "Hind"
		_look(leg.upper, looks["Upper" + kind], skin)
		_look(leg.lower, looks["Lower" + kind], skin)
		_look(leg.paw, looks["Paw"], skin)
		if looks.has("Greave" + kind):
			_greaves[leg] = _look(leg.lower, looks["Greave" + kind], skin)
	# the left hind calf: stab it and the bear kneels
	var hl := legs[2]
	leg_point = add_weak_point(hl.lower, Vector3(0, 0.2 * S, hl.r2 * 0.98), Vector3(0, 0, 1), 45.0, false, 1.9 * S / 1.4)
	# Dauntless parts: the head (antlers) and the four legs (bark shins) break; trunk and neck do not
	add_body_part("head", "Đầu", [head], 640.0, "bone", 2.0, true, _break_antlers)
	add_body_part("neck", "Cổ", [neck], 0.0, "", 1.5, true)
	add_body_part("body", "Thân", [torso], 0.0, "", 1.0)
	for leg in legs:
		add_body_part(leg.name.to_lower(), LEG_NAMES[leg.name], [leg.upper, leg.lower, leg.paw], 400.0, "claw", 1.2, false,
			_break_greave.bind(leg))
	for n in find_children("*", "VisualInstance3D", true, false):
		if not (n is Light3D):
			(n as VisualInstance3D).layers = 4
	_make_prompt()
	_make_stars()


## The interrupt cue, Dauntless's rays on the head: shown while a heavy hit there would stop Húc.
func _make_prompt() -> void:
	_prompt = Sprite3D.new()
	_prompt.texture = GameIcons.icon("prompt:rays", [["rays", "main", 50, 50, 1.0, 0]], "holy", 128)
	_prompt.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_prompt.no_depth_test = true
	_prompt.shaded = false
	_prompt.pixel_size = 0.055 * S
	_prompt.render_priority = 5
	_prompt.modulate = Color(1.0, 0.92, 0.6)
	_prompt.position = Vector3(0, 1.8, -2.6) * S
	_prompt.visible = false
	head.add_child(_prompt)


## Dizzy stars over the head when the stagger bar is nearly full, and while staggered.
func _make_stars() -> void:
	_stars = Node3D.new()
	_stars.position = Vector3(0, 3.6, 0.4) * S
	_stars.visible = false
	head.add_child(_stars)
	var tex := GameIcons.icon("prompt:star", [["star", "main", 50, 50, 0.9, 0]], "gold", 64)
	for i in 5:
		var st := Sprite3D.new()
		st.texture = tex
		st.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		st.shaded = false
		st.pixel_size = 0.014 * S
		var a := TAU * i / 5.0
		st.position = Vector3(cos(a), 0.15 * sin(a * 2.0), sin(a)) * 1.7 * S
		_stars.add_child(st)


func _break_antlers() -> void:
	_drop_piece(_antlers)


func _break_greave(leg: Leg) -> void:
	_drop_piece(_greaves.get(leg))


## A broken piece of armour: a copy tumbles to the ground and sinks away; the original hides.
func _drop_piece(mi: MeshInstance3D) -> void:
	if mi == null or not mi.visible:
		return
	mi.visible = false
	if not Game.has_screen():
		return
	var piece := MeshInstance3D.new()
	piece.mesh = mi.mesh
	piece.material_override = mi.material_override
	get_parent().add_child(piece)
	piece.global_transform = mi.global_transform
	var ground := Terrain.height_at_world(piece.global_position)
	var side := Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)).normalized() * 2.5
	var tw := piece.create_tween()
	tw.set_parallel(true)
	tw.tween_property(piece, "global_position", Vector3(piece.global_position.x + side.x, ground + 0.3, piece.global_position.z + side.z), 0.8) \
		.set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_QUAD)
	tw.tween_property(piece, "rotation", piece.rotation + Vector3(randf_range(-1.2, 1.2), randf_range(-1, 1), randf_range(-1.2, 1.2)), 0.8)
	tw.chain().tween_property(piece, "global_position:y", ground - 3.0 * S, 5.0).set_delay(4.0)
	tw.chain().tween_callback(piece.queue_free)
	Fx.dust(Vector3(piece.global_position.x + side.x, ground, piece.global_position.z + side.z), 2.5, 30)


## A sculpted mesh on a part, in bear units.
func _look(body: Node3D, mesh: Mesh, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.scale = Vector3.ONE * S
	body.add_child(mi)
	return mi


func _add_leg(leg_name: String, hip: Vector3, l1: float, l2: float, r1: float, r2: float, bend: float, group: int) -> void:
	var leg := Leg.new()
	leg.name = leg_name
	leg.hip = hip * S
	leg.l1 = l1 * S
	leg.l2 = l2 * S
	leg.r1 = r1 * S
	leg.r2 = r2 * S
	leg.bend = bend
	leg.group = group
	leg.upper = make_part(leg_name + "_Upper")
	add_capsule(leg.upper, leg.r1, leg.l1 + leg.r1, Transform3D.IDENTITY, true)
	leg.lower = make_part(leg_name + "_Lower")
	add_capsule(leg.lower, leg.r2, leg.l2 + leg.r2 * 0.6, Transform3D.IDENTITY, true)
	# bark shin plate on the front of the lower leg
	add_box(leg.lower, Vector3(leg.r2 * 1.7, leg.l2 * 0.7, 0.55 * S), Transform3D(Basis.IDENTITY, Vector3(0, -0.2 * S, -leg.r2 * 0.95)), null, false)
	leg.paw = make_part(leg_name + "_Paw")
	add_box(leg.paw, Vector3(2.5, 1.1, 3.1) * S, Transform3D(Basis.IDENTITY, Vector3(0, -0.1, -0.5) * S), null, false)
	legs.append(leg)


static func _axis_basis(y: Vector3) -> Basis:
	y = y.normalized()
	var ref := Vector3.RIGHT if absf(y.x) < 0.9 else Vector3.FORWARD
	var z := ref.cross(y).normalized()
	var x := y.cross(z).normalized()
	return Basis(x, y, z)


# ================================================================== every frame
func _physics_process(delta: float) -> void:
	mode_time += delta
	if mode != Mode.DEAD:
		_think(delta)
		_run_mode(delta)
	_ease_pose(delta)
	_update_feet(delta)
	_update_body(delta)
	_place_legs()
	_update_cues(delta)
	shake_amount = maxf(0.0, shake_amount - delta * (0.0 if mode == Mode.SHAKE else 1.5))


func _update_cues(delta: float) -> void:
	update_flash(delta)
	if _prompt:
		_prompt.visible = _charge_open
		if _charge_open:
			var pulse := 1.0 + 0.18 * sin(Game.now() * 14.0)
			_prompt.scale = Vector3.ONE * pulse
			_prompt.modulate = Color(1.0, 0.92, 0.6, 0.75 + 0.25 * sin(Game.now() * 14.0))
	if _stars:
		var f := stagger_fraction()
		_stars.visible = not dead and (is_staggered() or f >= 0.7)
		if _stars.visible:
			_stars.rotate_y(delta * (4.0 if is_staggered() else 2.0))


func _snap_to_ground() -> void:
	global_position.y = Terrain.height_at_world(global_position)


func _plant_feet() -> void:
	for leg in legs:
		var p := global_transform * Vector3(leg.hip.x * 1.08, 0, leg.hip.z)
		p.y = Terrain.height_at_world(p) + 0.6 * S
		leg.foot = p
		leg.stepping = false
		leg.override_on = false


func set_mode(m: Mode) -> void:
	mode = m
	mode_time = 0.0
	_charge_open = false
	_step_scale = 1.0
	_event_done.clear()
	_hit_this_swing.clear()
	for leg in legs:
		leg.override_on = false
		leg.override_rate = 10.0
	_head_free = true
	_goal_height = STAND_HEIGHT
	_goal_pitch = 0.0
	_goal_roll = 0.0
	pitch_pivot_z = 3.6 * S


func _once(key: String) -> bool:
	if _event_done.has(key):
		return false
	_event_done[key] = true
	return true


# ================================================================== deciding what to do
func _think(delta: float) -> void:
	for k in _cooldowns:
		_cooldowns[k] = maxf(0.0, _cooldowns[k] - delta)
	if dead:
		return
	var was := engaged
	target = nearest_hero(aggro_range)
	engaged = target != null
	if engaged and not was:
		Game.boss_engaged.emit(self)
		skill_used.emit("Gầm")
		Game.shake(0.3)
	if not rage and life_fraction() < 0.5:
		rage = true
		skill_used.emit("Cuồng Nộ")
		eye_mat.albedo_color = Color(1.0, 0.35, 0.25)
		eye_mat.emission = Color(1.0, 0.25, 0.15)
		Game.shake(0.4)
	var on_me := heroes_on_me().size() > 0
	_on_me_time = _on_me_time + delta if on_me else 0.0

	if mode != Mode.IDLE and mode != Mode.WALK:
		return
	if scripted:
		_speed = move_toward(_speed, 0.0, 2.0 * delta)
		return
	if not engaged:
		_wander(delta)
		return
	if on_me:
		if _on_me_time > (1.0 if rage else 1.6) and _cooldowns.shake <= 0.0:
			set_mode(Mode.SHAKE)
			skill_used.emit("Lắc Mình")
		else:
			_walk_to(home.origin + Vector3(sin(mode_time * 0.3), 0, cos(mode_time * 0.3)) * 25.0, 1.6, 2.0, delta)
		return
	var to := target.global_position - global_position
	to.y = 0.0
	var d := to.length()
	var front := (-global_basis.z).dot(to.normalized()) if d > 0.1 else 1.0
	var cd := 0.7 if rage else 1.0
	if d < 15.0 * REACH and front > 0.45 and _cooldowns.swipe <= 0.0:
		_swipe_side = 1.0 if global_basis.x.dot(to) > 0.0 else -1.0
		set_mode(Mode.SWIPE)
		_cooldowns.swipe = 3.6 * cd
		skill_used.emit("Vồ")
	elif d < 17.0 * REACH and front > 0.0 and _cooldowns.stomp <= 0.0:
		set_mode(Mode.STOMP)
		_cooldowns.stomp = 6.5 * cd
		skill_used.emit("Dậm Đất")
	elif d > 14.0 * REACH and d < 46.0 * REACH and _cooldowns.charge <= 0.0 and front > 0.75:
		set_mode(Mode.CHARGE)
		_cooldowns.charge = 11.0 * cd
		skill_used.emit("Húc")
	elif d > 17.0 * REACH and d < 42.0 * REACH and _cooldowns.pounce <= 0.0 and front > 0.6:
		set_mode(Mode.POUNCE)
		_cooldowns.pounce = 13.0 * cd
		skill_used.emit("Chụp Quăng")
	elif d > 26.0 * REACH and _cooldowns.throw <= 0.0 and front > 0.7:
		set_mode(Mode.THROW)
		_cooldowns.throw = 7.5 * cd
		skill_used.emit("Ném Đá Lớn")
	else:
		mode = Mode.WALK
		_walk_to(target.global_position, 3.4 if rage else 2.4, 11.0 * REACH, delta)


func _wander(delta: float) -> void:
	_wander_wait -= delta
	if _wander_wait > 0.0:
		_walk_to(global_position - global_basis.z, 0.0, 99.0, delta)
		return
	if _wander_goal == Vector3.ZERO or Vector2(_wander_goal.x - global_position.x, _wander_goal.z - global_position.z).length() < 4.0:
		var a := randf() * TAU
		_wander_goal = home.origin + Vector3(cos(a), 0, sin(a)) * randf_range(5.0, DEN_RADIUS * 0.7)
		_wander_wait = randf_range(2.0, 5.0)
		return
	_walk_to(_wander_goal, 1.6, 2.0, delta)


func _walk_to(goal: Vector3, speed: float, stop_at: float, delta: float) -> void:
	var to := goal - global_position
	to.y = 0.0
	var want_yaw := atan2(-to.x, -to.z)
	var diff := angle_difference(rotation.y, want_yaw)
	var turn_speed := (0.75 if rage else 0.55) * delta
	rotation.y += clampf(diff, -turn_speed, turn_speed)
	var want := speed if to.length() > stop_at and absf(diff) < 1.1 else 0.0
	_speed = move_toward(_speed, want, 1.6 * delta)
	_move_forward(_speed * delta)


func _move_forward(dist: float) -> void:
	var p := global_position - global_basis.z * dist
	var off := Vector2(p.x - home.origin.x, p.z - home.origin.z)
	if off.length() > DEN_RADIUS:
		off = off.normalized() * DEN_RADIUS
		p.x = home.origin.x + off.x
		p.z = home.origin.z + off.y
	global_position = p
	_snap_to_ground()
	_phase += dist * 2.2 / S


func _face(p: Vector3, rate: float, delta: float) -> void:
	var to := p - global_position
	var want_yaw := atan2(-to.x, -to.z)
	rotation.y += clampf(angle_difference(rotation.y, want_yaw), -rate * delta, rate * delta)


# ================================================================== skills
func _run_mode(delta: float) -> void:
	match mode:
		Mode.SWIPE: _swipe(delta)
		Mode.STOMP: _stomp(delta)
		Mode.THROW: _throw(delta)
		Mode.POUNCE: _pounce(delta)
		Mode.CHARGE: _charge(delta)
		Mode.SHAKE: _shake(delta)
		Mode.KNEEL: _kneel(delta)
		Mode.STUNNED: _stunned(delta)
		Mode.DYING: _dying(delta)
		_:
			pass
	if mode != Mode.WALK and mode != Mode.IDLE:
		_speed = move_toward(_speed, 0.0, 3.0 * delta)


func _swipe(delta: float) -> void:
	var t := mode_time
	var leg := legs[1] if _swipe_side > 0.0 else legs[0]
	var root := global_transform
	if _once("start"):
		Fx.telegraph(root * (Vector3(_swipe_side * 1.5, 0, -11.0) * S), 8.5 * S, 0.95)
	if target and t < 0.8:
		_face(target.global_position, 0.6, delta)
	leg.override_on = true
	if t < 0.85:
		_goal_roll = -_swipe_side * 0.06
		leg.override_pos = body_xf * (Vector3(_swipe_side * 4.2, -0.6, -6.5) * S)
		leg.override_rate = 6.0
	elif t < 1.2:
		var k := (t - 0.85) / 0.35
		var p := root * (Vector3(lerpf(_swipe_side * 7.0, -_swipe_side * 3.5, k), 0, lerpf(-9.0, -12.0, k)) * S)
		p.y = Terrain.height_at_world(p) + 0.9 * S
		leg.override_pos = p
		leg.override_rate = 30.0
		for h in heroes():
			if _hit_this_swing.has(h) or is_on_me(h):
				continue
			if h.global_position.distance_to(leg.foot) < 3.4 * S:
				_hit_this_swing[h] = true
				hurt_hero(h, 22.0, leg.foot, 12.0, 6.0, "Vồ")
		if _once("whoosh"):
			Game.shake(0.15)
	elif t < 2.0:
		leg.override_on = false
		_goal_roll = 0.0
	else:
		set_mode(Mode.IDLE)


func _stomp(delta: float) -> void:
	var t := mode_time
	var root := global_transform
	if _once("start"):
		_stomp_point = root * (Vector3(0, 0, -11.5) * S)
		_stomp_point.y = Terrain.height_at_world(_stomp_point)
		Fx.telegraph(_stomp_point, 11.0 * S, 1.45)
	pitch_pivot_z = 3.6 * S
	var fl := legs[0]
	var fr := legs[1]
	if t < 1.15:
		_goal_pitch = 0.46
		_goal_height = STAND_HEIGHT + 0.5 * S
		for leg in [fl, fr]:
			leg.override_on = true
			leg.override_rate = 5.0
			leg.override_pos = body_xf * Vector3(leg.hip.x, leg.hip.y - 5.2 * S, leg.hip.z - 2.0 * S)
		_goal_head = Vector2(0, 0.25)
		_head_free = false
	elif t < 1.45:
		_goal_pitch = -0.08
		for leg in [fl, fr]:
			var p := _stomp_point + root.basis.x * signf(leg.hip.x) * 2.4 * S
			p.y = Terrain.height_at_world(p) + 0.6 * S
			leg.override_pos = p
			leg.override_rate = 22.0
	elif t < 2.7:
		if _once("impact"):
			hit_area(_stomp_point, 11.0 * S, 28.0, 10.0, 6.0, "Dậm Đất")
			Fx.dust(_stomp_point, 7.0 * S, 80)
			Fx.dust(_stomp_point, 3.0 * S, 30)
			_shake_near(_stomp_point, 0.85)
			shake_amount = maxf(shake_amount, 0.8)
		_goal_pitch = 0.0
		_head_free = true
		if t > 1.9:
			for leg in [fl, fr]:
				leg.override_on = false
	else:
		set_mode(Mode.IDLE)


func _throw(delta: float) -> void:
	var t := mode_time
	var leg := legs[1]
	var root := global_transform
	if target:
		_face(target.global_position, 0.5, delta)
	leg.override_on = true
	if t < 0.7:
		var p := root * (Vector3(1.8, 0, -10.5) * S)
		p.y = Terrain.height_at_world(p) + 0.7 * S
		leg.override_pos = p
		leg.override_rate = 6.0
		_goal_head = Vector2(0.2, -0.45)
		_head_free = false
		_goal_pitch = -0.1
	elif t < 1.35:
		if _once("grab"):
			_held_rock = _make_rock(2.3 * S * 0.8)
			Fx.dust(leg.foot, 3.0 * S, 30)
		leg.override_pos = body_xf * (Vector3(3.6, 4.5, -7.5) * S)
		leg.override_rate = 5.0
		_goal_head = Vector2(0, 0.2)
		_goal_pitch = 0.12
	elif t < 2.5:
		if _once("release") and _held_rock and target:
			var aim: Vector3 = target.global_position + (target.velocity if "velocity" in target else Vector3.ZERO) * 0.5
			aim.y = Terrain.height_at_world(aim)
			var from := _held_rock.global_position
			var flight := clampf(from.distance_to(aim) / 22.0, 1.0, 1.8)
			var rock := Boulder.new()
			rock.visual = _held_rock
			rock.radius = 2.3 * S * 0.8
			get_parent().add_child(rock)
			rock.launch(from, aim, flight, self)
			Fx.telegraph(aim, 5.5 * REACH, flight)
			_held_rock = null
		if t > 1.6:
			leg.override_on = false
		_head_free = true
		_goal_pitch = 0.0
	else:
		set_mode(Mode.IDLE)
	if _held_rock:
		_held_rock.global_position = leg.foot + Vector3.UP * 1.7 * S


func _make_rock(radius: float) -> Node3D:
	var rock := MeshInstance3D.new()
	rock.mesh = RockArt.mesh(ROCKS.pick_random())
	rock.scale = Vector3.ONE * radius * 1.05
	rock.rotation = Vector3(randf() * TAU, randf() * TAU, randf() * TAU)
	get_parent().add_child(rock)
	return rock


func _pounce(delta: float) -> void:
	var t := mode_time
	const CROUCH := 1.0
	const FLIGHT := 1.5
	if _once("start") and target:
		var to := target.global_position - global_position
		to.y = 0.0
		_jump_from = global_position
		_jump_to = target.global_position - to.normalized() * 8.0 * S
		var off := Vector2(_jump_to.x - home.origin.x, _jump_to.z - home.origin.z)
		if off.length() > DEN_RADIUS:
			off = off.normalized() * DEN_RADIUS
			_jump_to = Vector3(home.origin.x + off.x, 0, home.origin.z + off.y)
		_jump_to.y = Terrain.height_at_world(_jump_to)
		var land := _jump_to + to.normalized() * 9.0 * S
		land.y = Terrain.height_at_world(land)
		Fx.telegraph(land, 8.5 * S, CROUCH + FLIGHT)
	if t < CROUCH:
		_face(_jump_to + (_jump_to - _jump_from), 1.2, delta)
		_goal_height = STAND_HEIGHT - 2.4 * S
		_goal_pitch = -0.06
	elif t < CROUCH + FLIGHT:
		var k := (t - CROUCH) / FLIGHT
		var p := _jump_from.lerp(_jump_to, k)
		p.y = lerpf(_jump_from.y, _jump_to.y, k) + sin(k * PI) * 9.0 * S
		global_position = p
		_goal_height = STAND_HEIGHT
		_goal_pitch = lerpf(0.25, -0.2, k)
		for leg in legs:
			leg.override_on = true
			leg.override_rate = 12.0
			leg.override_pos = body_xf * Vector3(leg.hip.x, leg.hip.y - 4.2 * S, leg.hip.z + (2.0 if leg.bend < 0 else -2.0) * S * (1.0 - k))
	else:
		if _once("land"):
			global_position = _jump_to
			for leg in legs:
				leg.override_on = false
			_plant_feet()
			var front := global_transform * (Vector3(0, 0, -10.0) * S)
			front.y = Terrain.height_at_world(front)
			hit_area(front, 8.5 * S, 32.0, 12.0, 6.0, "Chụp Quăng")
			Fx.dust(front, 7.0 * S, 80)
			Fx.dust(global_position, 6.0 * S, 50)
			_shake_near(front, 1.0)
			shake_amount = maxf(shake_amount, 0.9)
			var rock := _boulder_near(front, 9.0 * S)
			if rock:
				if rock.has_method("shatter"):
					rock.shatter()
				else:
					Fx.dust(rock.global_position, 4.0, 40)
				stun(7.0)
				skill_used.emit("Choáng!")
				return
		_goal_height = STAND_HEIGHT
		_goal_pitch = 0.0
		if t > CROUCH + FLIGHT + 1.4:
			set_mode(Mode.IDLE)


func _boulder_near(p: Vector3, r: float) -> Node3D:
	for b in get_tree().get_nodes_in_group("boulders"):
		var n := b as Node3D
		if n and Vector2(n.global_position.x - p.x, n.global_position.z - p.z).length() < r:
			return n
	return null


func _shake(_delta: float) -> void:
	var t := mode_time
	var dur := 2.4 if rage else 2.0
	shake_amount = sin(clampf(t / dur, 0.0, 1.0) * PI) * 1.0
	_goal_pitch = 0.12 * shake_amount
	if t >= dur:
		shake_amount = 0.0
		_cooldowns.shake = 3.0 if rage else 4.5
		_on_me_time = 0.0
		set_mode(Mode.IDLE)


func _kneel(_delta: float) -> void:
	var t := mode_time
	pitch_pivot_z = -7.0 * S
	if t < 9.5:
		_goal_pitch = 0.38
		_goal_height = STAND_HEIGHT - 0.8 * S
		_goal_head = Vector2(0, -0.1)
	elif t < 11.0:
		_goal_pitch = 0.0
		_goal_height = STAND_HEIGHT
	else:
		leg_point.restore()
		set_mode(Mode.IDLE)


func _stunned(_delta: float) -> void:
	var t := mode_time
	pitch_pivot_z = 3.6 * S
	if not _stun_down:
		# a flinch (a part broke): it rears and shakes its head
		_goal_pitch = 0.16 * sin(clampf(t / _stun_for, 0.0, 1.0) * PI)
		_goal_head = Vector2(sin(t * 9.0) * 0.35, 0.25)
		_head_free = false
		if t >= _stun_for:
			set_mode(Mode.IDLE)
		return
	if t < _stun_for:
		_goal_pitch = -0.3
		_goal_height = STAND_HEIGHT - 1.4 * S
		_goal_head = Vector2(0, -0.75)
		_head_free = false
		eye_mat.emission_energy_multiplier = 1.0
		for leg in legs:
			if leg.bend > 0.0:
				leg.override_on = true
				leg.override_rate = 4.0
				var p := global_transform * Vector3(leg.hip.x * 1.6, 0, leg.hip.z - 2.5 * S)
				p.y = Terrain.height_at_world(p) + 0.6 * S
				leg.override_pos = p
	elif t < _stun_for + 1.5:
		eye_mat.emission_energy_multiplier = 4.0
		for leg in legs:
			leg.override_on = false
		_goal_pitch = 0.0
		_goal_height = STAND_HEIGHT
		_head_free = true
	else:
		set_mode(Mode.IDLE)


## Knocked down for `seconds` with its head on the ground; `staggered` opens the staggered-damage
## window (+50%). A short flinch (`down` false) only rears.
func stun(seconds: float, staggered := true, down := true) -> void:
	if dead:
		return
	if staggered and poise:
		poise.force_break(seconds)
	_stun_for = seconds
	_stun_down = down
	set_mode(Mode.STUNNED)


## The stagger bar filled (Poise already opened the staggered window).
func on_staggered() -> void:
	if dead or mode == Mode.KNEEL or mode == Mode.DYING or mode == Mode.DEAD:
		return
	stun(maxf(poise.broken_remaining(), 1.0), false)
	skill_used.emit("Choáng!")


func can_interrupt() -> bool:
	return mode == Mode.CHARGE and _charge_open


## A heavy hit on its head while it charged: it stumbles and goes down, open to staggered damage.
func interrupt(_by: Node) -> void:
	skill_used.emit("Gián Đoạn!")
	if Game.has_screen():
		Sfx.play("interrupt", head.global_position, 4.0)
		DamageNumbers.show(head.global_position + Vector3.UP * 2.0 * S, "GIÁN ĐOẠN!", "staggered", true)
		Fx.flash(head.global_position, Color(1.0, 0.85, 0.45), 8.0, 14.0)
		Fx.burst(head.global_position, Vector3.UP, Color(1.0, 0.85, 0.45, 1.0), 50, 10.0, 0.4, 0.8, 90.0, -6.0, true)
	Game.hitstop(0.16)
	Game.shake(0.7)
	stun(7.0)


func _on_part_broken(part: Dictionary) -> void:
	skill_used.emit("Vỡ " + String(part.name))
	if mode in [Mode.IDLE, Mode.WALK, Mode.SWIPE, Mode.THROW, Mode.STOMP, Mode.CHARGE, Mode.SHAKE, Mode.POUNCE] and not dead:
		if mode == Mode.POUNCE:
			return
		stun(1.8, false, false)


# ================================================================== Húc
## Dauntless's charge: head down, rays glowing on it (the window), then it runs at where the hero
## stood. Whoever its head or front paws meet is thrown; a heavy hit on its head interrupts it.
func _charge(delta: float) -> void:
	var t := mode_time
	if _once("start"):
		var aim := target.global_position if target else global_position - global_basis.z * 20.0
		var to := aim - global_position
		to.y = 0.0
		var dir := to.normalized() if to.length() > 0.5 else -global_basis.z
		_charge_from = global_position
		var end := global_position + dir * clampf(to.length() + 8.0, 12.0, 32.0)
		var off := Vector2(end.x - home.origin.x, end.z - home.origin.z)
		if off.length() > DEN_RADIUS:
			off = off.normalized() * DEN_RADIUS
			end = Vector3(home.origin.x + off.x, end.y, home.origin.z + off.y)
		_charge_to = end
		_rammed.clear()
		_charge_open = true
		for i in 4:
			var p := _charge_from.lerp(_charge_to, (i + 1) / 4.0)
			p.y = Terrain.height_at_world(p)
			Fx.telegraph(p, 3.2 * S, CHARGE_WIND + CHARGE_RUN * (i + 1) / 4.0)
	if t < CHARGE_WIND:
		_face(_charge_to, 2.6, delta)
		_goal_pitch = -0.3
		_goal_height = STAND_HEIGHT - 1.0 * S
		_goal_head = Vector2(0, -0.6)
		_head_free = false
		if Engine.get_physics_frames() % 10 == 0:
			for leg in [legs[0], legs[1]]:
				Fx.dust(leg.foot - Vector3.UP * 0.5 * S, 1.6 * S, 8)
	elif t < CHARGE_WIND + CHARGE_RUN:
		var k := (t - CHARGE_WIND) / CHARGE_RUN
		var p := _charge_from.lerp(_charge_to, k)
		p.y = Terrain.height_at_world(p)
		_speed = p.distance_to(global_position) / maxf(delta, 0.001)
		global_position = p
		_step_scale = 0.38
		_goal_pitch = -0.32
		_goal_height = STAND_HEIGHT - 1.2 * S
		_goal_head = Vector2(0, -0.7)
		_head_free = false
		_charge_open = k < 0.9
		if Engine.get_physics_frames() % 5 == 0:
			Fx.dust(global_position - global_basis.z * 4.0 * S, 3.0 * S, 14)
		_ram()
	elif t < CHARGE_WIND + CHARGE_RUN + CHARGE_STOP:
		_charge_open = false
		_step_scale = 1.0
		_goal_pitch = 0.0
		_goal_height = STAND_HEIGHT
		_head_free = true
	else:
		set_mode(Mode.IDLE)


## Heroes the charging head or front paws touch are thrown (once per charge).
func _ram() -> void:
	var front: Array = [head, legs[0].paw, legs[1].paw, legs[0].lower, legs[1].lower]
	for h in heroes():
		if _rammed.has(h) or is_on_me(h) or h.get("is_climbing"):
			continue
		var chest: Vector3 = h.global_position + Vector3.UP
		for body in front:
			for cs in body.get_children():
				if cs is CollisionShape3D and _closest_on_shape(cs, chest).distance_to(chest) < 0.9:
					_rammed[h] = true
					hurt_hero(h, 34.0, global_position, 15.0, 7.0, "Húc")
					_shake_near(chest, 0.6)
					break
			if _rammed.has(h):
				break


func _dying(_delta: float) -> void:
	var t := mode_time
	_goal_height = 3.0 * S
	_goal_roll = 1.15
	_goal_pitch = -0.12
	_goal_head = Vector2(0.3, -0.5)
	_head_free = false
	eye_mat.emission_energy_multiplier = maxf(0.0, 4.0 - t)
	if t < 4.0 and Engine.get_physics_frames() % 20 == 0:
		for wp in weak_points:
			Fx.burst(wp.global_position, Vector3.UP, Color(0.02, 0.02, 0.03, 0.9), 14, 7.0, 0.7, 2.2, 25.0, 1.5)
	if t > 4.5:
		mode = Mode.DEAD


func _shake_near(p: Vector3, amount: float) -> void:
	var h: Node3D = Game.hero if is_instance_valid(Game.hero) else null
	if h:
		var d := h.global_position.distance_to(p)
		Game.shake(amount * clampf(1.0 - d / 70.0, 0.15, 1.0))


# ================================================================== reacting
func on_weak_point_stabbed(wp: WeakPoint, amount: float, hero: Node3D) -> void:
	super.on_weak_point_stabbed(wp, amount, hero)
	shake_amount = maxf(shake_amount, 0.55)
	if wp.vital and wp.hp > 0.0 and mode != Mode.KNEEL and mode != Mode.STUNNED:
		_cooldowns.shake = 0.0
		_on_me_time = 99.0


func on_weak_point_destroyed(wp: WeakPoint) -> void:
	super.on_weak_point_destroyed(wp)
	if dead:
		return
	if wp == leg_point:
		set_mode(Mode.KNEEL)
		skill_used.emit("Quỵ Gối")
		Game.shake(0.5)


func die() -> void:
	super.die()
	set_mode(Mode.DYING)
	skill_used.emit("Ngã Xuống")


func respawn() -> void:
	super.respawn()
	if _antlers:
		_antlers.visible = true
	for leg in _greaves:
		_greaves[leg].visible = true
	rage = false
	eye_mat.albedo_color = Color(0.7, 1.0, 0.85)
	eye_mat.emission = Color(0.6, 1.0, 0.8)
	eye_mat.emission_energy_multiplier = 4.0
	set_mode(Mode.IDLE)
	height = STAND_HEIGHT
	pitch = 0.0
	roll = 0.0
	_snap_to_ground()
	_plant_feet()
	Fx.burst(global_position + Vector3.UP * 6.0 * S, Vector3.UP, Color(0.6, 1.0, 0.85, 0.6), 60, 6.0, 1.2, 2.0, 90.0, 0.5, true)


func look_point() -> Vector3:
	return head.global_position if head else global_position + Vector3.UP * 10.0 * S


## Moves the bear's den (and the bear) to a new place on the ground.
func reset_to(xf: Transform3D) -> void:
	global_transform = xf
	_snap_to_ground()
	home = global_transform
	_plant_feet()
	_update_body(0.0)
	_place_legs()


# ================================================================== posing
func _ease_pose(delta: float) -> void:
	var k := 1.0 - exp(-(5.0 if mode == Mode.STOMP else 2.6) * delta)
	if mode == Mode.DYING or mode == Mode.DEAD:
		k = 1.0 - exp(-0.9 * delta)
	height = lerpf(height, _goal_height, k)
	pitch = lerpf(pitch, _goal_pitch, k)
	roll = lerpf(roll, _goal_roll, k)
	var want := _goal_head
	if _head_free and target:
		var hp := head.global_position if head else global_position
		var local := body_xf.basis.inverse() * (target.global_position + Vector3.UP - hp)
		want = Vector2(clampf(atan2(-local.x, -local.z), -0.75, 0.75),
			clampf(atan2(local.y, Vector2(local.x, local.z).length()), -0.55, 0.3))
	elif _head_free:
		want = Vector2(sin(Game.now() * 0.25) * 0.3, -0.05)
	head_look = head_look.lerp(want, 1.0 - exp(-2.2 * delta))


func _update_feet(delta: float) -> void:
	var root := global_transform
	var vel := -root.basis.z * _speed
	var step_time := STEP_TIME * (0.72 if rage else 1.0) * _step_scale
	for leg in legs:
		if leg.override_on:
			leg.stepping = false
			leg.foot = leg.foot.lerp(leg.override_pos, 1.0 - exp(-leg.override_rate * delta))
			continue
		var ideal := root * Vector3(leg.hip.x * 1.08, 0, leg.hip.z)
		ideal += vel * step_time * 0.6
		ideal.y = Terrain.height_at_world(ideal) + 0.6 * S
		if leg.stepping:
			leg.t += delta / step_time
			var k := clampf(leg.t, 0.0, 1.0)
			var e := k * k * (3.0 - 2.0 * k)
			leg.foot = leg.from.lerp(leg.to, e) + Vector3.UP * sin(k * PI) * LIFT
			if leg.t >= 1.0:
				leg.stepping = false
				leg.foot = leg.to
				_footfall(leg)
		else:
			var off := Vector2(ideal.x - leg.foot.x, ideal.z - leg.foot.z).length()
			var lifted := leg.foot.y - ideal.y > 0.4 * S
			if (off > STRIDE * 0.5 or lifted) and _group_can_step(leg.group):
				leg.stepping = true
				leg.t = 0.0
				leg.from = leg.foot
				leg.to = ideal


func _group_can_step(group: int) -> bool:
	for l in legs:
		if l.stepping and l.group != group:
			return false
	return true


func _footfall(leg: Leg) -> void:
	Fx.dust(leg.foot - Vector3.UP * 0.5 * S, 2.6 * S, 18)
	_shake_near(leg.foot, 0.22)
	for h in heroes():
		if is_on_me(h):
			continue
		if Vector2(h.global_position.x - leg.foot.x, h.global_position.z - leg.foot.z).length() < 2.3 * S \
				and absf(h.global_position.y - leg.foot.y) < 2.5 * S:
			hurt_hero(h, 18.0, leg.foot, 8.0, 3.0, "Giẫm")


func _update_body(_delta: float) -> void:
	var root := global_transform
	var g := 0.0
	for leg in legs:
		g += (leg.foot.y - 0.6 * S) if not leg.override_on else Terrain.height_at_world(leg.foot)
	g = g / maxf(1.0, legs.size()) if legs.size() > 0 else root.origin.y
	var bob := sin(_phase) * 0.2 * S * clampf(_speed / 2.4, 0.0, 1.0)
	var pos := Vector3(0, g - root.origin.y + height + bob, 0)
	var p := pitch
	var r := roll
	var yaw_j := 0.0
	if shake_amount > 0.01 and mode != Mode.DYING:
		var t := Game.now()
		p += sin(t * 7.3) * 0.08 * shake_amount
		r += sin(t * 9.1) * 0.14 * shake_amount
		yaw_j = sin(t * 5.2) * 0.1 * shake_amount
		pos.y += sin(t * 11.0) * 0.45 * S * shake_amount
	var pivot := Vector3(0, 0, pitch_pivot_z)
	var rot := Basis.from_euler(Vector3(p, yaw_j, r))
	var local := Transform3D(Basis.IDENTITY, pos + pivot) * Transform3D(rot, Vector3.ZERO) * Transform3D(Basis.IDENTITY, -pivot)
	body_xf = root * local
	if torso:
		torso.global_transform = body_xf
		head_xf = body_xf * Transform3D(Basis.from_euler(Vector3(head_look.y, head_look.x, 0)), NECK_BASE)
		neck.global_transform = head_xf
		head.global_transform = head_xf * Transform3D(Basis.IDENTITY, HEAD_OFFSET)


func _place_legs() -> void:
	var root := global_transform
	var back := body_xf.basis.z.normalized()
	var side := body_xf.basis.x.normalized()
	for leg in legs:
		var hip := body_xf * leg.hip
		var pole := back * leg.bend + side * signf(leg.hip.x) * 0.3
		var knee := solve_knee(hip, leg.foot, leg.l1, leg.l2, pole)
		_place_segment(leg.upper, hip, knee, back)
		_place_segment(leg.lower, knee, leg.foot, back)
		leg.paw.global_transform = Transform3D(root.basis.orthonormalized(), leg.foot)


static func solve_knee(hip: Vector3, foot: Vector3, l1: float, l2: float, pole: Vector3) -> Vector3:
	var axis := foot - hip
	var dist := axis.length()
	var dir := axis / dist if dist > 0.001 else Vector3.DOWN
	var d := clampf(dist, absf(l1 - l2) + 0.01, l1 + l2 - 0.001)
	var a := (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
	var h := sqrt(maxf(0.0, l1 * l1 - a * a))
	var perp := pole - dir * pole.dot(dir)
	if perp.length() < 0.001:
		perp = dir.cross(Vector3.RIGHT)
	return hip + dir * a + perp.normalized() * h


static func _place_segment(body: Node3D, top: Vector3, bottom: Vector3, back: Vector3) -> void:
	var y := (top - bottom).normalized()
	var z := back - y * back.dot(y)
	z = z.normalized() if z.length() > 0.01 else y.cross(Vector3.RIGHT).normalized()
	var x := y.cross(z).normalized()
	body.global_transform = Transform3D(Basis(x, y, z), (top + bottom) * 0.5)
