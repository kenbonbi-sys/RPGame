extends Node
## Measures what a frame costs: stands the hero at a few places, turns the camera through eight
## directions at each and averages draw calls, triangles, objects and CPU / GPU frame times.
##   Godot_console.exe --path Godot res://tools/perf_probe.tscn [-- --out D:/perf.txt] [--skip Grass,Foliage] [--no-shadows] [--motion-blur 0]
## Needs a window (the GPU has to draw). Prints one line per place and direction, then the means.

const SPOTS := [
	["lang", Vector2(18, 20), Vector2(60, 26)],
	["duong_rung", Vector2(47, 27), Vector2(95, 30)],
	["hang_gau", Vector2(78, 40), Vector2(84, 45)],
	["dam_lay", Vector2(140, 21.5), Vector2(120, 40)],
	["thao_nguyen", Vector2(40, 90), Vector2(60, 100)],
]
const SAMPLE_FRAMES := 24

var main: Node
var lines: PackedStringArray = []


func _ready() -> void:
	var t0 := Time.get_ticks_msec()
	GameSettings.use_defaults = true
	GameSettings.values = GameSettings.DEFAULTS.duplicate()
	GameSettings._loaded = true
	if Game.has_arg("--motion-blur"):
		GameSettings.values.motion_blur = float(Game.arg_value("--motion-blur"))
	main = (load("res://scenes/main.tscn") as PackedScene).instantiate()
	var started := {}
	main.child_entered_tree.connect(func(n: Node) -> void:
		started[n.name] = Time.get_ticks_msec()
		n.ready.connect(func() -> void:
			var ms: int = Time.get_ticks_msec() - started[n.name]
			if ms > 30:
				_say("  ready %-10s %5d ms" % [n.name, ms])))
	add_child(main)
	for skip in Game.arg_value("--skip", "").split(",", false):
		var n := main.get_node_or_null(skip)
		if n:
			n.queue_free()
	if Game.has_arg("--no-shadows"):
		(main.get_node("Sun") as DirectionalLight3D).shadow_enabled = false
	var vp := get_viewport().get_viewport_rid()
	RenderingServer.viewport_set_measure_render_time(vp, true)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	_say("load %d ms" % (Time.get_ticks_msec() - t0))
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	main.hero.invulnerable_until = 1e9
	main.bear.scripted = true
	await _wait(3.0)
	var sums := {}
	for spot in SPOTS:
		var m: Vector2 = WorldMap.tile(spot[1])
		var p := Props.ground(m) + Vector3.UP * 0.1
		main.hero.revive(p)
		main.camera.snap()
		await _wait(1.0)
		for i in 8:
			var yaw := TAU * i / 8.0
			var s := await _sample(yaw)
			_say("%-11s %3d°  draws %5d (shadow %5d)  tris %8d (shadow %8d)  objects %5d  cpu %5.2f ms  gpu %5.2f ms" % [spot[0],
				int(rad_to_deg(yaw)), s.draws, s.sdraws, s.tris, s.stris, s.objects, s.cpu, s.gpu])
			for k in s:
				sums[k] = sums.get(k, 0.0) + s[k]
	var n := SPOTS.size() * 8.0
	_say("MEAN draws %d (shadow %d)  tris %d (shadow %d)  objects %d  cpu %.2f ms  gpu %.2f ms" % [sums.draws / n,
		sums.sdraws / n, sums.tris / n, sums.stris / n, sums.objects / n, sums.cpu / n, sums.gpu / n])
	var out := Game.arg_value("--out", "")
	if out != "":
		var f := FileAccess.open(out, FileAccess.WRITE)
		f.store_string("\n".join(lines) + "\n")
	get_tree().quit()


func _sample(yaw: float) -> Dictionary:
	var cam: SotcCamera = main.camera
	for f in 12:
		cam.yaw = yaw
		await get_tree().process_frame
	var s := {"draws": 0.0, "tris": 0.0, "sdraws": 0.0, "stris": 0.0, "objects": 0.0, "cpu": 0.0, "gpu": 0.0}
	var vp := get_viewport().get_viewport_rid()
	for f in SAMPLE_FRAMES:
		cam.yaw = yaw
		await get_tree().process_frame
		s.draws += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)
		s.tris += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME)
		s.objects += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_OBJECTS_IN_FRAME)
		s.sdraws += RenderingServer.viewport_get_render_info(vp, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_SHADOW,
			RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME)
		s.stris += RenderingServer.viewport_get_render_info(vp, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_SHADOW,
			RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME)
		s.cpu += RenderingServer.viewport_get_measured_render_time_cpu(vp) + RenderingServer.get_frame_setup_time_cpu()
		s.gpu += RenderingServer.viewport_get_measured_render_time_gpu(vp)
	for k in s:
		s[k] /= SAMPLE_FRAMES
	return s


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _say(l: String) -> void:
	print(l)
	lines.append(l)
