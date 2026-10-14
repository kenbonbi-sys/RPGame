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
## life, and Lắc Mình when heroes ride it.

enum Mode { IDLE, WALK, SWIPE, STOMP, THROW, POUNCE, SHAKE, KNEEL, STUNNED, DYING, DEAD }

## Every length below is in "bear units"; S turns them into metres (about 18 m to the crown).
const S := 1.4
const STAND_HEIGHT := 7.4 * S
const STRIDE := 2.8 * S
const STEP_TIME := 0.85
const LIFT := 1.5 * S
const NECK_BASE := Vector3(0, 1.4, -8.6) * S
const HEAD_OFFSET := Vector3(0, 1.4, -4.6) * S
const DEN_RADIUS := 38.0
const ROCK_SCENES := [
	"res://assets/third_party/kenney_nature/rock_largeA.glb",
	"res://assets/third_party/kenney_nature/rock_largeC.glb",
	"res://assets/third_party/kenney_nature/rock_largeE.glb",
]


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

var _cooldowns := {"swipe": 2.0, "stomp": 4.0, "throw": 3.0, "pounce": 8.0, "shake": 0.0}
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
var _rock_scenes: Array[PackedScene] = []


func _ready() -> void:
	display_name = "Gấu Ma Rừng Già"
	_init_materials(Color(0.17, 0.16, 0.13), Color(0.38, 0.37, 0.29), Color(0.66, 0.62, 0.53))
	process_physics_priority = -10
	home = global_transform
	for p in ROCK_SCENES:
		_rock_scenes.append(load(p))
	_build()
	_snap_to_ground()
	_plant_feet()
	_update_body(0.0)
	_place_legs()
	health_changed.emit(vital_hp(), max_vital_hp())


# ================================================================== the body
func _build() -> void:
	var bone := StandardMaterial3D.new()
	bone.albedo_color = Color(0.80, 0.77, 0.68)
	bone.roughness = 0.75
	var dark := StandardMaterial3D.new()
	dark.albedo_color = Color(0.05, 0.05, 0.05)
	dark.roughness = 0.4

	torso = make_part("Torso")
	add_capsule(torso, 3.7 * S, 16.0 * S, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 0.5, -2.0) * S), true, 620)
	for side in [-1.0, 1.0]:
		add_box(torso, Vector3(0.8, 3.6, 9.5) * S, Transform3D(Basis(Vector3.FORWARD, side * 0.2), Vector3(side * 3.45, -0.3, -3.2) * S))
	add_box(torso, Vector3(5.8, 4.4, 1.0) * S, Transform3D(Basis(Vector3.RIGHT, 0.28), Vector3(0, -0.6, -9.7) * S))
	add_weak_point(torso, Vector3(0, 4.18, 1.6) * S, Vector3(0, 1, 0.2), 120.0, true, 2.4)

	neck = make_part("Neck")
	var nd := Vector3(0, 1.0, -3.4) * S
	add_capsule(neck, 2.4 * S, 5.6 * S, Transform3D(_axis_basis(nd.normalized()), nd * 0.5), true, 160)

	head = make_part("Head")
	var skull := add_sphere(head, 2.7 * S, Transform3D.IDENTITY, true, 130)
	_reshape(head, skull, Vector3(1.0, 0.92, 1.12))
	# the bone mask over the face, the muzzle and the nose (none of it can be held)
	add_box(head, Vector3(3.6, 2.8, 1.0) * S, Transform3D(Basis.IDENTITY, Vector3(0, 0.1, -2.3) * S), null, false)
	_ellipsoid(head, Vector3(2.0, 1.6, 0.8) * S, Vector3(0, 0.15, -2.2) * S, bone)
	var snout := CapsuleShape3D.new()
	snout.radius = 0.95 * S
	snout.height = 3.4 * S
	var scs := CollisionShape3D.new()
	scs.shape = snout
	scs.transform = Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, -1.1, -3.0) * S)
	scs.set_meta("climbable", false)
	head.add_child(scs)
	_ellipsoid(head, Vector3(1.05, 0.95, 1.9) * S, Vector3(0, -1.1, -3.1) * S, fur_material)
	_ellipsoid(head, Vector3(0.95, 0.5, 1.5) * S, Vector3(0, -0.75, -3.0) * S, bone)
	_ellipsoid(head, Vector3(0.5, 0.38, 0.35) * S, Vector3(0, -0.95, -4.85) * S, dark)
	_ellipsoid(head, Vector3(0.8, 0.35, 1.3) * S, Vector3(0, -1.85, -2.6) * S, fur_material)
	add_weak_point(head, Vector3(0, 2.5, 0.45) * S, Vector3(0, 1, 0.15), 120.0, true, 2.2)
	eye_mat = StandardMaterial3D.new()
	eye_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	eye_mat.albedo_color = Color(0.7, 1.0, 0.85)
	eye_mat.emission_enabled = true
	eye_mat.emission = Color(0.6, 1.0, 0.8)
	eye_mat.emission_energy_multiplier = 4.0
	for side in [-1.0, 1.0]:
		_ellipsoid(head, Vector3(0.5, 0.42, 0.3) * S, Vector3(side * 0.82, 0.45, -2.85) * S, dark)
		_ellipsoid(head, Vector3(0.24, 0.2, 0.12) * S, Vector3(side * 0.82, 0.45, -3.05) * S, eye_mat)
		var ear := MeshInstance3D.new()
		var cm := CapsuleMesh.new()
		cm.radius = 0.7 * S
		cm.height = 2.3 * S
		ear.mesh = cm
		ear.material_override = fur_material
		ear.transform = Transform3D(Basis(Vector3.FORWARD, side * -0.5), Vector3(side * 1.8, 2.3, 0.3) * S)
		ear.layers = 4
		head.add_child(ear)
	var glow := OmniLight3D.new()
	glow.light_color = Color(0.6, 1.0, 0.8)
	glow.light_energy = 2.0
	glow.omni_range = 9.0
	glow.position = Vector3(0, 0.4, -3.8) * S
	head.add_child(glow)

	_add_leg("FL", Vector3(-2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 0)
	_add_leg("FR", Vector3(2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 1)
	_add_leg("HL", Vector3(-2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 1)
	_add_leg("HR", Vector3(2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 0)
	# the left hind calf: stab it and the bear kneels
	var hl := legs[2]
	leg_point = add_weak_point(hl.lower, Vector3(0, 0.2 * S, hl.r2 * 0.98), Vector3(0, 0, 1), 45.0, false, 1.9)
	for n in find_children("*", "VisualInstance3D", true, false):
		if not (n is Light3D):
			(n as VisualInstance3D).layers = 4


## A visual-only ellipsoid (a sphere of radius 1 stretched to `radii`).
func _ellipsoid(body: Node3D, radii: Vector3, pos: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = 1.0
	sm.height = 2.0
	sm.radial_segments = 20
	sm.rings = 10
	mi.mesh = sm
	mi.material_override = mat
	mi.transform = Transform3D(Basis.IDENTITY.scaled(radii), pos)
	body.add_child(mi)
	return mi


## Stretches the mesh drawn for a shape (the collider stays as it is).
func _reshape(body: Node3D, shape: CollisionShape3D, stretch: Vector3) -> void:
	for c in body.get_children():
		if c is MeshInstance3D and c.transform.origin == shape.transform.origin and not (c is MultiMeshInstance3D):
			c.scale = stretch
			return


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
	add_capsule(leg.upper, leg.r1, leg.l1 + leg.r1, Transform3D.IDENTITY, true, 90)
	leg.lower = make_part(leg_name + "_Lower")
	add_capsule(leg.lower, leg.r2, leg.l2 + leg.r2 * 0.6, Transform3D.IDENTITY, true, 60)
	# bark shin plate on the front of the lower leg
	add_box(leg.lower, Vector3(leg.r2 * 1.7, leg.l2 * 0.7, 0.55 * S), Transform3D(Basis.IDENTITY, Vector3(0, -0.2 * S, -leg.r2 * 0.95)))
	leg.paw = make_part(leg_name + "_Paw")
	add_box(leg.paw, Vector3(2.5, 1.1, 3.1) * S, Transform3D(Basis.IDENTITY, Vector3(0, -0.1, -0.5) * S), null, false)
	_ellipsoid(leg.paw, Vector3(1.35, 0.62, 1.7) * S, Vector3(0, 0.0, -0.45) * S, fur_material)
	for i in 4:
		var claw := MeshInstance3D.new()
		var cm := PrismMesh.new()
		cm.size = Vector3(0.35, 0.9, 0.5) * S
		claw.mesh = cm
		claw.material_override = claw_material
		claw.transform = Transform3D(Basis(Vector3.RIGHT, -PI * 0.5), Vector3(-0.9 + i * 0.6, -0.3, -2.25) * S)
		leg.paw.add_child(claw)
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
	shake_amount = maxf(0.0, shake_amount - delta * (0.0 if mode == Mode.SHAKE else 1.5))


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
	if not rage and vital_hp() < max_vital_hp() * 0.5:
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
	if d < 15.0 and front > 0.45 and _cooldowns.swipe <= 0.0:
		_swipe_side = 1.0 if global_basis.x.dot(to) > 0.0 else -1.0
		set_mode(Mode.SWIPE)
		_cooldowns.swipe = 3.6 * cd
		skill_used.emit("Vồ")
	elif d < 17.0 and front > 0.0 and _cooldowns.stomp <= 0.0:
		set_mode(Mode.STOMP)
		_cooldowns.stomp = 6.5 * cd
		skill_used.emit("Dậm Đất")
	elif d > 17.0 and d < 42.0 and _cooldowns.pounce <= 0.0 and front > 0.6:
		set_mode(Mode.POUNCE)
		_cooldowns.pounce = 13.0 * cd
		skill_used.emit("Chụp Quăng")
	elif d > 26.0 and _cooldowns.throw <= 0.0 and front > 0.7:
		set_mode(Mode.THROW)
		_cooldowns.throw = 7.5 * cd
		skill_used.emit("Ném Đá Lớn")
	else:
		mode = Mode.WALK
		_walk_to(target.global_position, 3.4 if rage else 2.4, 11.0, delta)


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
				h.take_hit({"amount": 28.0, "from": leg.foot, "knockdown": true, "knock": 12.0, "lift": 6.0, "source": self})
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
			hit_area(_stomp_point, 11.0 * S, 32.0, 10.0, 6.0)
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
			Fx.telegraph(aim, 5.5, flight)
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
	var rock: Node3D = _rock_scenes.pick_random().instantiate()
	rock.scale = Vector3.ONE * radius * 2.2
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
			hit_area(front, 8.5 * S, 40.0, 12.0, 6.0)
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
				set_mode(Mode.STUNNED)
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
	if t < 7.0:
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
	elif t < 8.5:
		eye_mat.emission_energy_multiplier = 4.0
		for leg in legs:
			leg.override_on = false
		_goal_pitch = 0.0
		_goal_height = STAND_HEIGHT
		_head_free = true
	else:
		set_mode(Mode.IDLE)


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
	var h: Node3D = Game.hero
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
	var step_time := STEP_TIME * (0.72 if rage else 1.0)
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
			h.take_hit({"amount": 22.0, "from": leg.foot, "knockdown": true, "knock": 8.0, "lift": 3.0, "source": self})


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
