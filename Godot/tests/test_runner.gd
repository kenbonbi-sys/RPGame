extends Node
## Headless tests (the autoloads are loaded because this runs as a scene):
##   Godot_console.exe --headless --path . res://tests/test_runner.tscn
## Exit code 0 when every check passes.

const HERO := preload("res://scenes/hero.tscn")

var failures: PackedStringArray = []
var checks := 0
var _current := ""


func _ready() -> void:
	await _run("world layout", _test_world_layout)
	await _run("baked land matches WorldMap", _test_bake_up_to_date)
	await _run("knee solver", _test_knee)
	await _run("controls", _test_controls)
	await _run("weak points, kneel and fall", _test_colossus_damage)
	await _run("fall damage", _test_fall_damage)
	await _run("climbing a hind leg", _test_climb_leg)
	await _run("thrown rocks are capped", _test_boulder_cap)
	await preload("res://tests/combat_tests.gd").new(self).run_all()
	print("\n%d checks, %d failed" % [checks, failures.size()])
	for f in failures:
		print("  FAIL ", f)
	get_tree().quit(1 if failures.size() > 0 else 0)


func _run(test_name: String, f: Callable) -> void:
	_current = test_name
	print("- ", test_name)
	await f.call()


func check(ok: bool, what: String) -> void:
	checks += 1
	if not ok:
		failures.append("%s: %s" % [_current, what])


func _frames(n: int) -> void:
	for i in n:
		await get_tree().physics_frame


# ------------------------------------------------------------------ the land
func _test_world_layout() -> void:
	var v := WorldMap.village_center()
	var lo := INF
	var hi := -INF
	for i in 24:
		var p := v + Vector2.from_angle(i * TAU / 24.0) * WorldMap.VILLAGE_R * 0.6
		var h := WorldMap.height(p.x, p.y)
		lo = minf(lo, h)
		hi = maxf(hi, h)
	check(hi - lo < 0.6, "the village green is flat (%.2f m)" % (hi - lo))
	var a := WorldMap.arena_center()
	lo = INF
	hi = -INF
	for i in 36:
		var p := a + Vector2.from_angle(i * TAU / 36.0) * WorldMap.ARENA_FLOOR_R * 0.85
		var h := WorldMap.height(p.x, p.y)
		lo = minf(lo, h)
		hi = maxf(hi, h)
	check(hi - lo < 1.2, "the arena floor is flat (%.2f m)" % (hi - lo))
	var gate := a + WorldMap.arena_gate_dir() * (WorldMap.ARENA_RIDGE_IN + 14.0)
	check(WorldMap.arena_ridge_weight(gate.x, gate.y) < 0.05, "the road into the arena is open")
	var back := a - WorldMap.arena_gate_dir() * (WorldMap.ARENA_RIDGE_IN + 14.0)
	check(WorldMap.height(back.x, back.y) > WorldMap.ARENA_HEIGHT + 10.0, "a ridge rings the arena")
	var lake: Vector2 = WorldMap.PONDS[6][0] * WorldMap.TILE
	check(WorldMap.height(lake.x + 10.0, lake.y) < WorldMap.WATER_LEVEL, "Đầm Xà Mẫu holds water")
	var mound: Vector2 = WorldMap.MOUNDS[0] * WorldMap.TILE
	check(WorldMap.height(mound.x, mound.y) > WorldMap.WATER_LEVEL, "the lake's mounds stay dry")
	check(WorldMap.area_name(v) == "Làng Lá Xanh", "the village is named")
	check(WorldMap.area_name(a) == "Hang Gấu Ma", "the den is named")


func _test_bake_up_to_date() -> void:
	var d: TerrainData = load("res://world/terrain_data.res")
	check(d.width == int(WorldMap.SIZE_U / d.spacing) + 1, "width matches the map")
	var worst := 0.0
	var rng := RandomNumberGenerator.new()
	rng.seed = 3
	for i in 60:
		var ix := rng.randi_range(0, d.width - 1)
		var iv := rng.randi_range(0, d.depth - 1)
		worst = maxf(worst, absf(d.height_at_sample(ix, iv) - WorldMap.height(ix * d.spacing, iv * d.spacing)))
	check(worst < 0.01, "baked heights equal WorldMap.height (off by %.3f m: run tools/bake_world.gd)" % worst)


# ------------------------------------------------------------------ bones
func _test_knee() -> void:
	var hip := Vector3(0, 10, 0)
	var foot := Vector3(0, 3, -1)
	var knee := GauMa.solve_knee(hip, foot, 4.0, 4.5, Vector3.BACK)
	check(absf(knee.distance_to(hip) - 4.0) < 0.01, "upper bone keeps its length")
	check(absf(knee.distance_to(foot) - 4.5) < 0.01, "lower bone keeps its length")
	check(knee.z > 0.0, "the joint bends toward the pole")
	var far := GauMa.solve_knee(hip, Vector3(0, -20, 0), 4.0, 4.5, Vector3.BACK)
	check(absf(far.distance_to(hip) - 4.0) < 0.01, "out of reach: the leg straightens")


func _test_controls() -> void:
	for a in ["move_forward", "jump", "dodge", "attack", "grip", "look_boss"]:
		check(InputMap.has_action(a), "action " + a)


# ------------------------------------------------------------------ the colossus
func _new_bear() -> GauMa:
	var bear := GauMa.new()
	bear.scripted = true
	add_child(bear)
	bear.reset_to(Transform3D.IDENTITY)
	return bear


func _test_colossus_damage() -> void:
	var bear := _new_bear()
	await _frames(2)
	check(bear.weak_points.size() == 3, "three weak points")
	check(is_equal_approx(bear.vital_hp(), 240.0), "life is the two vital points")
	bear.leg_point.stab(45.0, null)
	check(bear.mode == GauMa.Mode.KNEEL, "stabbing the calf makes it kneel")
	check(not bear.dead, "the calf is not vital")
	var vitals := bear.weak_points.filter(func(w: WeakPoint) -> bool: return w.vital)
	vitals[0].stab(200.0, null)
	check(not bear.dead and bear.vital_hp() > 0.0, "one vital point is not enough")
	vitals[1].stab(200.0, null)
	check(bear.dead, "both vital points down: it falls")
	check(bear.mode == GauMa.Mode.DYING, "it collapses")
	bear.respawn()
	check(not bear.dead and is_equal_approx(bear.vital_hp(), 240.0), "it comes back whole")
	check(bear.leg_point.active, "its calf heals")
	bear.queue_free()
	await _frames(1)


func _test_fall_damage() -> void:
	var hero: Hero = HERO.instantiate()
	add_child(hero)
	hero.global_position = Vector3(300, 50, 300)
	await _frames(1)
	hero._peak_y = hero.global_position.y + 20.0
	hero.state = Hero.State.AIR
	hero._land()
	var want := hero.max_hp - Combat.round_half_even((20.0 - hero.safe_fall) * 7.0)
	check(absf(hero.hp - want) < 0.5, "a 20 m fall hurts by the rule (%.1f, want %.1f)" % [hero.hp, want])
	var hp := hero.hp
	hero._peak_y = hero.global_position.y + 5.0
	hero._land()
	check(is_equal_approx(hero.hp, hp), "a short drop is free")
	hero.queue_free()
	await _frames(1)


func _test_climb_leg() -> void:
	# flat ground so the test does not depend on the world
	var ground := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(200, 1, 200)
	cs.shape = box
	cs.position = Vector3(0, -0.5, 0)
	ground.add_child(cs)
	add_child(ground)
	var bear := _new_bear()
	await _frames(3)
	var cam := SotcCamera.new()
	add_child(cam)
	var hero: Hero = HERO.instantiate()
	add_child(hero)
	cam.attach(hero)
	var hl: GauMa.Leg = bear.legs[2]
	var back := bear.global_basis.z.normalized()
	var calf := hl.lower.global_position
	hero.revive(Vector3(calf.x, 0.0, calf.z) + back * (hl.r2 + 0.9))
	hero.facing = -back
	await _frames(5)
	Input.action_press("grip")
	await _frames(3)
	check(hero.state == Hero.State.CLIMB, "holding grip next to the fur catches hold")
	var y0 := hero.global_position.y
	Input.action_press("move_forward")
	await _frames(90)
	Input.action_release("move_forward")
	check(hero.state == Hero.State.CLIMB, "still holding on")
	check(hero.global_position.y > y0 + 1.5, "climbed up the leg (%.1f m)" % (hero.global_position.y - y0))
	check(hero.grip < hero.max_grip, "climbing costs grip")
	Input.action_release("grip")
	await _frames(2)
	check(hero.state != Hero.State.CLIMB, "letting go drops the hero")
	for n in [hero, cam, bear, ground]:
		n.queue_free()
	await _frames(2)


func _test_boulder_cap() -> void:
	var rocks := []
	for i in Boulder.MAX_ROCKS + 2:
		var b := Boulder.new()
		add_child(b)
		b.global_position = Vector3(i * 10.0, 0, 0)
		b._land(0.0)
		rocks.append(b)
	await _frames(2)
	var alive := rocks.filter(func(r) -> bool: return is_instance_valid(r) and not r.is_queued_for_deletion())
	check(alive.size() == Boulder.MAX_ROCKS, "at most %d rocks stay (%d)" % [Boulder.MAX_ROCKS, alive.size()])
	for r in alive:
		r.queue_free()
