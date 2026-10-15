extends Node
## Game-wide state and the few things every system reaches for: the hero on this screen,
## the camera, hit-stop and screen shake. Time is only slowed here (the Unity game's TimeFX rule).

signal boss_engaged(boss: Node3D)
signal boss_defeated(boss: Node3D)
signal hero_died
## A line for the event log (loot, kills, level-ups).
## `icon`: a GameIcons.item id ("coin", "xp", "kill", "level" or an item) shown before the line.
signal message(text: String, color: Color, icon: String)
## A flash of colour over the whole screen (skills, being hurt).
signal screen_flash(color: Color, strength: float, seconds: float)
## A monster fell; `credited` are the heroes who share its XP and loot (quests, bestiary).
signal enemy_killed(enemy: Node3D, credited: Array)

## The hero on this screen (Players.Local in the Unity game). Game rules must not depend on it.
var hero: Node3D
var camera: Node3D
## Colossi in the world, for the camera's "look at the boss" and the HUD.
var colossi: Array[Node3D] = []

## Command line after "--", e.g. `godot --path . -- --autoshot --shots-dir D:/shots`.
var args := PackedStringArray()

var _hitstop_until := 0.0
var _slow_until := 0.0
var _slow_scale := 1.0
var _slow_ease := 0.4


## Counts errors from the very start when a test tour runs (AutoShot reads it).
var error_counter: Logger


func _enter_tree() -> void:
	args = OS.get_cmdline_user_args()
	if args.has("--autoshot"):
		error_counter = AutoShot.ErrorCounter.new()
		OS.add_logger(error_counter)


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS


## True where the game rules run (offline: always; online later: the server). Presentation
## goes through has_screen(). Same split as the Unity game's GameSession.IsAuthority / HasScreen.
func is_authority() -> bool:
	return true


## Online play comes in stage 7; until then every game is offline.
func is_online() -> bool:
	return false


func has_screen() -> bool:
	return DisplayServer.get_name() != "headless" or Engine.is_editor_hint() or OS.has_feature("test_screen")


## The camera, or null if it is gone.
func cam() -> SotcCamera:
	return camera as SotcCamera if is_instance_valid(camera) else null


func has_arg(name: String) -> bool:
	return args.has(name)


func arg_value(name: String, fallback := "") -> String:
	var i := args.find(name)
	return args[i + 1] if i >= 0 and i + 1 < args.size() else fallback


func now() -> float:
	return Time.get_ticks_msec() / 1000.0


## Freezes the action for a few real milliseconds so a hit lands with weight (35/70/120 ms tiers).
func hitstop(seconds: float) -> void:
	_hitstop_until = maxf(_hitstop_until, now() + seconds)


## Slow motion for `seconds` of real time, easing back to normal over the last `ease` seconds
## (Lướt Hoàn Hảo: 35% for 0.3 s, ease 0.1; a hero's fall: 30% for 1.2 s).
func slow_motion(scale: float, seconds: float, ease := 0.4) -> void:
	_slow_scale = maxf(0.01, scale)
	_slow_until = now() + seconds
	_slow_ease = maxf(0.0, ease)


func log(text: String, color := Color(0.92, 0.9, 0.84), icon := "") -> void:
	message.emit(text, color, icon)


func flash_screen(color: Color, strength: float, seconds: float) -> void:
	screen_flash.emit(color, strength, seconds)


func shake(amount: float) -> void:
	if is_instance_valid(camera) and camera.has_method("add_trauma"):
		camera.add_trauma(amount)


func nearest_colossus(from: Vector3, max_distance := INF) -> Node3D:
	var best: Node3D = null
	var best_d := max_distance
	for c in colossi:
		if not is_instance_valid(c) or c.get("dead"):
			continue
		var d := from.distance_to(c.global_position)
		if d < best_d:
			best_d = d
			best = c
	return best


func _process(_delta: float) -> void:
	var t := now()
	if t < _hitstop_until:
		Engine.time_scale = 0.03
	elif t < _slow_until:
		var remain := _slow_until - t
		Engine.time_scale = lerpf(1.0, _slow_scale, remain / _slow_ease) if remain < _slow_ease else _slow_scale
	else:
		Engine.time_scale = 1.0
