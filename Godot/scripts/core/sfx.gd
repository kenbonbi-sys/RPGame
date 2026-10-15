class_name Sfx
extends RefCounted
## Sound effects in the world: a small pool of 3D players under the scene, so a hit sounds where it
## lands. The same sound twice within 30 ms (one swing hitting five slimes) plays once, louder.
## Sounds are made by Tools/AudioGen/gen_godot_sfx.py.

const DIR := "res://assets/sfx/%s.wav"
const POOL := 12

static var _streams := {}
static var _players: Array[AudioStreamPlayer3D] = []
static var _next := 0
static var _last := {}


static func play(sound: String, at: Vector3, volume_db := 0.0, pitch := 1.0) -> void:
	if not Game.has_screen():
		return
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null or tree.current_scene == null:
		return
	var now := Game.now()
	if now - float(_last.get(sound, -1.0)) < 0.03:
		return
	_last[sound] = now
	var p := _player(tree.current_scene)
	p.stream = _stream(sound)
	p.global_position = at
	p.volume_db = volume_db
	p.pitch_scale = pitch
	p.play()


## A sharp whack on the impact frame; `heavy` for Trảm and crits.
static func whack(at: Vector3, heavy := false) -> void:
	if heavy:
		play("whack_heavy", at, 2.0, randf_range(0.92, 1.04))
	else:
		play("whack_%d" % randi_range(1, 4), at, 0.0, randf_range(0.94, 1.1))


static func _stream(sound: String) -> AudioStream:
	if not _streams.has(sound):
		_streams[sound] = load(DIR % sound)
	return _streams[sound]


static func _player(scene: Node) -> AudioStreamPlayer3D:
	_players = _players.filter(func(p) -> bool: return is_instance_valid(p))
	if _players.size() < POOL:
		var p := AudioStreamPlayer3D.new()
		p.unit_size = 12.0
		p.max_distance = 90.0
		p.attenuation_filter_cutoff_hz = 12000.0
		p.bus = &"Master"
		scene.add_child(p)
		_players.append(p)
		return p
	_next = (_next + 1) % _players.size()
	return _players[_next]
