class_name AutoShot
extends Node
## Plays a tour by itself, saves screenshots and quits, like the Unity game's -autoshot:
##   Godot.exe --path Godot -- --autoshot [--shots-dir D:/shots] [--autoshot-only climb]
## Exit code 0 = clean, 1 = errors in the log, 2 = ran out of time.
## Tours: "world" (village, forest, arena), "climb" (legs, kneel, back, stabs), "skills" (the bear's attacks).

const TIMEOUT := 300.0


class ErrorCounter:
	extends Logger
	var errors := 0
	var first: PackedStringArray = []

	func _log_error(function: String, file: String, line: int, code: String, rationale: String,
			_editor_notify: bool, error_type: int, _script_backtraces: Array[ScriptBacktrace]) -> void:
		if error_type == ERROR_TYPE_WARNING:
			return
		errors += 1
		if first.size() < 12:
			first.append("%s:%d %s %s %s" % [file, line, function, code, rationale])

	func _log_message(message: String, error: bool) -> void:
		if error and first.size() < 12:
			first.append(message.strip_edges())


var dir := ""
var only := ""
var counter := ErrorCounter.new()
var main: Node
var hero: Hero
var cam: SotcCamera
var bear: GauMa
var _start := 0.0
var _taken := 0


func _ready() -> void:
	OS.add_logger(counter)
	dir = Game.arg_value("--shots-dir", OS.get_user_data_dir().path_join("autoshot"))
	only = Game.arg_value("--autoshot-only", "")
	DirAccess.make_dir_recursive_absolute(dir)
	main = get_parent()
	hero = main.hero
	cam = main.camera
	bear = main.bear
	_start = Game.now()
	hero.invulnerable_until = 1e9
	bear.scripted = true
	get_tree().create_timer(TIMEOUT, true, false, true).timeout.connect(func() -> void:
		print("AUTOSHOT: timeout")
		get_tree().quit(2))
	_run()


func _run() -> void:
	await _wait(2.0)
	if only == "" or only == "world":
		await _world()
	if only == "" or only == "forest":
		await _forest()
	if only == "" or only == "climb":
		await _climb()
	if only == "" or only == "skills":
		await _skills()
	if only == "" or only == "brawl":
		await _brawl()
	print("AUTOSHOT: %d shots in %s, %d errors, %.1f s" % [_taken, dir, counter.errors, Game.now() - _start])
	for l in counter.first:
		print("  ", l)
	get_tree().quit(1 if counter.errors > 0 else 0)


# ------------------------------------------------------------------ tours
func _world() -> void:
	await _shot("01_lang_la_xanh")
	_put(WorldMap.tile(Vector2(47, 27)), WorldMap.arena_center())
	await _wait(1.2)
	await _shot("02_duong_rung")
	var gate := WorldMap.arena_center() + WorldMap.arena_gate_dir() * 58.0
	_put(gate, WorldMap.arena_center())
	await _look_boss(1.6)
	await _shot("03_cong_hang_gau_ma")
	_put(_beside_bear(Vector3(-7, 0, -16)), WorldMap.map_of(bear.global_position))
	await _look_boss(1.6)
	await _shot("04_duoi_chan_gau")
	var ridge := WorldMap.arena_center() - WorldMap.arena_gate_dir().orthogonal() * 78.0
	_put(ridge, WorldMap.arena_center())
	await _wait(1.0)
	await _shot("05_tren_vach_da")


## Monsters of the forest and the hero's skills.
func _forest() -> void:
	var camp := WorldMap.tile(Vector2(47, 27.5))
	_put(camp + Vector2(-14, -6), camp)
	await _wait(1.5)
	await _shot("20_bai_slime")
	_put(camp + Vector2(-7, -3), camp)
	await _wait(2.5)
	await _shot("21_slime_lao_toi")
	for i in 3:
		_aim_at_nearest()
		_press("attack")
		await _wait(0.12)
		_release("attack")
		await _wait(0.3)
	await _shot("22_chem_gio")
	for i in 6:
		_aim_at_nearest()
		hero.skills.request(0)
		await _wait(0.45)
	await _wait(1.0)
	await _shot("23_roi_do")
	hero.skills.request(2)
	await _wait(0.5)
	await _shot("24_chem_xoay")
	var shrooms := WorldMap.tile(Vector2(62, 29.5))
	_put(shrooms + Vector2(-13, -4), shrooms)
	await _wait(3.0)
	await _shot("25_nam_doc_ban")
	_aim_at_nearest()
	AbilityRunner.cast(hero, Ability.get_by_id("fireball"), _nearest_enemy_pos(), 1, 1)
	await _wait(0.45)
	await _shot("26_cau_lua")
	AbilityRunner.cast(hero, Ability.get_by_id("lightning"), _nearest_enemy_pos(), 1, 3)
	await _wait(0.9)
	await _shot("27_loi_phat")
	print("AUTOSHOT: hero level %d xp %d gold %d bag %s" % [hero.level, hero.stats.xp, hero.inventory.gold,
		str(hero.inventory.stacks.map(func(s): return "%s×%d" % [s.id, s.count]))])


func _nearest_enemy_pos() -> Vector3:
	var best := hero.global_position + hero.facing * 6.0
	var bd := INF
	for e in get_tree().get_nodes_in_group("enemies"):
		if e.state == Enemy.State.DEAD or not e.visible:
			continue
		var d: float = e.global_position.distance_to(hero.global_position)
		if d < bd:
			bd = d
			best = e.global_position
	return best


func _aim_at_nearest() -> void:
	var p := _nearest_enemy_pos()
	var to := p - hero.global_position
	cam.yaw = atan2(-to.x, -to.z)


func _climb() -> void:
	# behind the left hind leg, facing its calf
	var hl: GauMa.Leg = bear.legs[2]
	var calf := hl.lower.global_position
	var back := bear.global_basis.z.normalized()
	var stand := calf + back * 2.2
	stand.y = Terrain.height_at_world(stand)
	hero.revive(stand)
	hero.facing = -back
	cam.snap()
	await _wait(0.6)
	_press("grip")
	await _wait(0.4)
	_press("move_forward")
	await _wait(0.7)
	_release("move_forward")
	await _wait(0.4)
	await _shot("06_bam_chan_sau")
	# stab the calf: the bear kneels
	_press("attack")
	await _wait(1.0)
	await _shot("07_giu_kiem_dam")
	_release("attack")
	await _wait(0.25)
	await _shot("08_dam_trung")
	if bear.leg_point.active:
		_press("attack")
		await _wait(1.0)
		_release("attack")
		await _wait(0.3)
	_release("grip")
	await _wait(2.5)
	hero.revive(bear.global_position + bear.global_basis.x * 16.0 + back * 8.0)
	cam.snap()
	await _look_boss(1.2)
	await _shot("09_gau_quy_goi")
	# up on its back by the hump
	hero.revive(bear.torso.global_transform * Vector3(0, 4.4, 3.4))
	cam.snap()
	_press("grip")
	await _wait(0.8)
	await _shot("10_tren_lung")
	_press("move_forward")
	await _wait(0.6)
	_release("move_forward")
	_press("attack")
	await _wait(1.1)
	_release("attack")
	await _wait(0.2)
	await _shot("11_dam_u_lung")
	bear.set_mode(GauMa.Mode.SHAKE)
	await _wait(1.0)
	await _shot("12_lac_minh")
	await _wait(1.5)
	_release("grip")
	await _wait(1.5)


func _skills() -> void:
	bear.set_mode(GauMa.Mode.IDLE)
	await _wait(1.0)
	var fwd := -bear.global_basis.z.normalized()
	_put(WorldMap.map_of(bear.global_position + fwd * 15.0 + bear.global_basis.x * 6.0), WorldMap.map_of(bear.global_position))
	await _wait(0.5)
	bear.target = hero
	bear.set_mode(GauMa.Mode.STOMP)
	await _look_boss(0.9)
	await _shot("13_dam_dat_canh_bao")
	await _wait(0.65)
	await _shot("14_dam_dat")
	await _wait(1.5)
	_put(WorldMap.map_of(bear.global_position + fwd * 34.0), WorldMap.map_of(bear.global_position))
	await _wait(0.4)
	bear.set_mode(GauMa.Mode.THROW)
	await _look_boss(1.3)
	await _shot("15_nem_da")
	await _wait(1.6)
	await _shot("16_tang_da_lon")
	await _wait(1.0)
	var rock := _nearest_boulder(bear.global_position)
	if rock:
		var to_rock := rock.global_position - bear.global_position
		to_rock.y = 0.0
		var spot := rock.global_position - to_rock.normalized() * 2.0
		_put(WorldMap.map_of(spot + to_rock.normalized().cross(Vector3.UP) * 6.0), WorldMap.map_of(bear.global_position))
		bear.rotation.y = atan2(-to_rock.x, -to_rock.z)
		bear.target = hero
		await _wait(0.3)
		bear.set_mode(GauMa.Mode.POUNCE)
		await _wait(1.6)
		await _shot("17_chup_quang")
		await _wait(1.2)
		var side := bear.global_basis.x.normalized()
		_put(WorldMap.map_of(bear.global_position - bear.global_basis.z * 14.0 + side * 24.0), WorldMap.map_of(bear.look_point()))
		await _look_boss(1.0)
		await _shot("18_choang")
	bear.set_mode(GauMa.Mode.IDLE)


## The bear decides for itself for a while; every skill should come up and nothing may break.
func _brawl() -> void:
	var used := {}
	bear.skill_used.connect(func(s: String) -> void: used[s] = used.get(s, 0) + 1)
	bear.scripted = false
	var fps := []
	var spots := [Vector3(0, 0, -20), Vector3(14, 0, -10), Vector3(0, 0, -36), Vector3(-20, 0, -24)]
	for i in 8:
		var off: Vector3 = spots[i % spots.size()]
		_put(WorldMap.map_of(bear.global_transform * off), WorldMap.map_of(bear.global_position))
		await _wait(5.0)
		fps.append(Engine.get_frames_per_second())
	await _shot("19_tu_do")
	bear.scripted = true
	print("AUTOSHOT: brawl skills ", used, "  fps ", fps)


# ------------------------------------------------------------------ helpers
func _put(m: Vector2, look_at_m: Vector2) -> void:
	var p := Props.ground(m) + Vector3.UP * 0.1
	hero.revive(p)
	var to := WorldMap.world(look_at_m) - p
	hero.facing = Vector3(to.x, 0, to.z).normalized()
	cam.snap()


func _beside_bear(local: Vector3) -> Vector2:
	return WorldMap.map_of(bear.global_transform * local)


func _nearest_boulder(p: Vector3) -> Node3D:
	var best: Node3D = null
	var best_d := INF
	for b in get_tree().get_nodes_in_group("boulders"):
		var n := b as Node3D
		if n is StaticBody3D or n == null:
			continue
		var d := n.global_position.distance_to(p)
		if d < best_d:
			best_d = d
			best = n
	return best


func _look_boss(seconds: float) -> void:
	_press("look_boss")
	await _wait(seconds)
	_release("look_boss")


func _press(action: String) -> void:
	var e := InputEventAction.new()
	e.action = action
	e.pressed = true
	Input.parse_input_event(e)


func _release(action: String) -> void:
	var e := InputEventAction.new()
	e.action = action
	e.pressed = false
	Input.parse_input_event(e)


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout


func _shot(shot_name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var path := dir.path_join(shot_name + ".png")
	img.save_png(path)
	_taken += 1
	print("AUTOSHOT: %s  hero %s %s  cam %s arm %.1f/%.1f" % [shot_name, str(hero.global_position.snapped(Vector3.ONE * 0.1)),
		Hero.State.keys()[hero.state], str(cam.cam.global_position.snapped(Vector3.ONE * 0.1)),
		cam.arm.get_hit_length(), cam.arm.spring_length])
