class_name SotcCamera
extends Node3D
## The over-the-shoulder camera of Shadow of the Colossus: it hangs back behind the hero on a
## spring arm, lags a little so movement has weight, drifts around behind the hero while running,
## keeps the hero low in the frame so the sky and the colossi tower, and turns to face the colossus
## while "look_boss" is held. Near a colossus it pulls back and widens; on its body it closes in.

@export var mouse_sensitivity := 0.0024
@export var stick_speed := 2.8
@export var distance := 4.4
@export var look_height := 1.75          ## the point the camera orbits, above the hero's feet
@export var min_pitch := -72.0
@export var max_pitch := 52.0
@export var default_pitch := -8.0
@export var follow_lag := 6.0            ## higher = tighter follow
@export var vertical_lag := 3.5
@export var fov_base := 62.0
@export var recenter_delay := 1.3        ## seconds without camera input before it drifts behind the hero
@export var recenter_speed := 1.5

var yaw := 0.0
var pitch := deg_to_rad(-8.0)
var target: Node3D
## The loadout's shot: the camera swings round in front of the slayer and frames them head to toe
## (Dauntless shows its loadout over the slayer standing in the world).
var showcase := false

var _focus := Vector3.ZERO
var _last_input := -100.0
var _trauma := 0.0
var _arm_len := 4.4
var _height := 1.75
var _fov := 62.0
var _noise := FastNoiseLite.new()
var _boss_look := 0.0

var arm: SpringArm3D
var cam: Camera3D


func _ready() -> void:
	top_level = true
	Game.camera = self
	arm = SpringArm3D.new()
	arm.name = "Arm"
	arm.collision_mask = 1
	var sphere := SphereShape3D.new()
	sphere.radius = 0.3
	arm.shape = sphere
	arm.margin = 0.2
	arm.spring_length = distance
	add_child(arm)
	cam = Camera3D.new()
	cam.name = "Camera"
	cam.fov = fov_base
	cam.near = 0.08
	cam.far = 1600.0
	arm.add_child(cam)
	cam.make_current()
	_noise.frequency = 2.2
	_arm_len = distance
	_height = look_height
	_fov = fov_base
	if not Game.has_arg("--autoshot") and DisplayServer.get_name() != "headless":
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


## The settings' motion blur (0 = off): a compositor effect on this camera.
func set_motion_blur(strength: float) -> void:
	if cam == null:
		return
	if strength <= 0.001:
		cam.compositor = null
		return
	if cam.compositor == null:
		var c := Compositor.new()
		c.compositor_effects = [MotionBlur.new()]
		cam.compositor = c
	(cam.compositor.compositor_effects[0] as MotionBlur).strength = strength


func attach(t: Node3D) -> void:
	target = t
	snap()


## Jump straight to the resting place behind the target (after a teleport).
func snap() -> void:
	if not target:
		return
	_focus = target.global_position + Vector3.UP * look_height
	var fwd: Vector3 = target.get("facing") if target.get("facing") != null else Vector3.FORWARD
	yaw = atan2(-fwd.x, -fwd.z)
	pitch = deg_to_rad(default_pitch)
	_apply()


func add_trauma(amount: float) -> void:
	_trauma = clampf(_trauma + amount, 0.0, 1.0)


var _kick := 0.0
var _kick_at := 0.0
const KICK_TIME := 0.09


## A jolt that lasts about five frames of real time (it keeps going through a hit-stop).
func kick(amount: float) -> void:
	_kick = maxf(_kick * _kick_left(), amount)
	_kick_at = Game.now()


func _kick_left() -> float:
	return clampf(1.0 - (Game.now() - _kick_at) / KICK_TIME, 0.0, 1.0)


## Camera-relative directions on the ground, for the hero's movement.
func forward_flat() -> Vector3:
	return Vector3(-sin(yaw), 0.0, -cos(yaw))


func right_flat() -> Vector3:
	return Vector3(cos(yaw), 0.0, -sin(yaw))


func look_at_point(p: Vector3) -> void:
	var d := p - (_focus if _focus != Vector3.ZERO else global_position)
	yaw = atan2(-d.x, -d.z)
	pitch = clampf(atan2(d.y, Vector2(d.x, d.z).length()), deg_to_rad(min_pitch), deg_to_rad(max_pitch))


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		yaw -= event.relative.x * mouse_sensitivity
		pitch -= event.relative.y * mouse_sensitivity
		pitch = clampf(pitch, deg_to_rad(min_pitch), deg_to_rad(max_pitch))
		_last_input = Game.now()
	elif event is InputEventMouseButton and event.pressed and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED \
			and not Game.has_arg("--autoshot") and not get_tree().paused and not Game.menu_open:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _process(delta: float) -> void:
	# the camera runs on real time so hit-stop and slow motion do not freeze it
	var dt := minf(delta / maxf(Engine.time_scale, 0.001), 0.1)
	if not target:
		return
	var stick := Controls.camera_stick()
	if stick.length() > 0.05:
		yaw -= stick.x * stick_speed * dt
		pitch = clampf(pitch - stick.y * stick_speed * 0.7 * dt, deg_to_rad(min_pitch), deg_to_rad(max_pitch))
		_last_input = Game.now()

	var climbing: bool = target.get("is_climbing") == true
	var riding: bool = target.get("on_colossus") == true
	var boss := Game.nearest_colossus(target.global_position, 140.0)

	# follow with a little lag; tighter on a colossus, where everything moves
	var lag := follow_lag * (2.2 if climbing or riding else 1.0)
	var goal := target.global_position + Vector3.UP * _height
	var k := 1.0 - exp(-lag * dt)
	var kv := 1.0 - exp(-vertical_lag * (2.5 if climbing or riding else 1.0) * dt)
	_focus.x = lerpf(_focus.x, goal.x, k)
	_focus.z = lerpf(_focus.z, goal.z, k)
	_focus.y = lerpf(_focus.y, goal.y, kv)
	# never let the hero slip far out of the frame
	var off := _focus - goal
	if off.length() > 1.6:
		_focus = goal + off.normalized() * 1.6

	if showcase:
		var f: Vector3 = target.get("facing") if target.get("facing") != null else Vector3.FORWARD
		var ks := 1.0 - exp(-5.0 * dt)
		yaw = lerp_angle(yaw, atan2(f.x, f.z), ks)
		pitch = lerpf(pitch, deg_to_rad(-2.0), ks)
		_last_input = Game.now()
	# look at the colossus while the button is held
	var looking := Input.is_action_pressed("look_boss") and boss != null and not Game.menu_open
	_boss_look = move_toward(_boss_look, 1.0 if looking else 0.0, dt * 3.0)
	if looking:
		var p: Vector3 = boss.call("look_point") if boss.has_method("look_point") else boss.global_position
		var d := p - _focus
		var want_yaw := atan2(-d.x, -d.z)
		var want_pitch := clampf(atan2(d.y, Vector2(d.x, d.z).length()) * 0.85, deg_to_rad(min_pitch), deg_to_rad(max_pitch))
		var kl := 1.0 - exp(-5.0 * dt)
		yaw = lerp_angle(yaw, want_yaw, kl)
		pitch = lerpf(pitch, want_pitch, kl)
		_last_input = Game.now()
	elif not climbing and Game.now() - _last_input > recenter_delay:
		_recenter(dt)

	# framing: wider and further back when a colossus is near, close on its body
	var want_len := distance
	var want_fov := fov_base
	var want_h := look_height
	if boss:
		var near := 1.0 - smoothstep(30.0, 110.0, boss.global_position.distance_to(target.global_position))
		want_len = lerpf(distance, 6.4, near)
		want_fov = lerpf(fov_base, 70.0, near)
		want_h = lerpf(look_height, 2.1, near)
	if climbing:
		want_len = 3.4
		want_fov = 66.0
		want_h = 1.2
	elif riding:
		want_len = 4.6
	if showcase:
		want_len = 4.7
		want_fov = 30.0
		want_h = 0.98
	var speed := Vector2(target.velocity.x, target.velocity.z).length() if "velocity" in target else 0.0
	want_fov += smoothstep(6.0, 8.5, speed) * 6.0
	var kf := 6.0 if showcase else 1.0
	_arm_len = lerpf(_arm_len, want_len, 1.0 - exp(-2.5 * kf * dt))
	_fov = lerpf(_fov, want_fov, 1.0 - exp(-3.0 * kf * dt))
	_height = lerpf(_height, want_h, 1.0 - exp(-3.0 * kf * dt))

	_trauma = maxf(0.0, _trauma - dt * 1.4)
	_apply()


func _recenter(dt: float) -> void:
	var v: Vector3 = target.velocity if "velocity" in target else Vector3.ZERO
	var flat := Vector2(v.x, v.z)
	if flat.length() < 2.0:
		return
	var want := atan2(-flat.x, -flat.y)
	# running toward the camera: leave it be rather than whip around
	if absf(angle_difference(yaw, want)) > deg_to_rad(125.0):
		return
	var k := 1.0 - exp(-recenter_speed * smoothstep(2.0, 6.0, flat.length()) * dt)
	yaw = lerp_angle(yaw, want, k)
	pitch = lerpf(pitch, deg_to_rad(default_pitch), k * 0.5)


func _apply() -> void:
	global_position = _focus
	basis = Basis.from_euler(Vector3(pitch, yaw, 0.0))
	arm.spring_length = _arm_len
	cam.fov = _fov
	# shake through the lens offsets: the spring arm owns the camera's position
	var s := _trauma * _trauma
	var t := Game.now() * 30.0
	cam.h_offset = _noise.get_noise_2d(t, 0.0) * 0.35 * s
	cam.v_offset = _noise.get_noise_2d(0.0, t) * 0.35 * s
	var k := _kick * _kick_left()
	if k > 0.0:
		cam.h_offset += randf_range(-1.0, 1.0) * k
		cam.v_offset += randf_range(-1.0, 1.0) * k
	cam.rotation = Vector3(0.0, 0.0, _noise.get_noise_2d(t, t) * 0.05 * s)
