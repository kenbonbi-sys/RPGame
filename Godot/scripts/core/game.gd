extends Node
## Game-wide state and the few things every system reaches for: the hero on this screen,
## the camera, hit-stop and screen shake. Time is only slowed here (the Unity game's TimeFX rule).

signal boss_engaged(boss: Node3D)
signal boss_defeated(boss: Node3D)
signal hero_died

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


func _ready() -> void:
	args = OS.get_cmdline_user_args()
	process_mode = Node.PROCESS_MODE_ALWAYS


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


## Slow motion for a while (Lướt Hoàn Hảo: 35% for 0.3 s).
func slow_motion(scale: float, seconds: float) -> void:
	_slow_scale = scale
	_slow_until = now() + seconds


func shake(amount: float) -> void:
	if camera and camera.has_method("add_trauma"):
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
		Engine.time_scale = _slow_scale
	else:
		Engine.time_scale = 1.0
