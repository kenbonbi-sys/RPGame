class_name GameSettings
extends RefCounted
## The graphics settings of the Dauntless options screen (Display and Advanced), kept in
## user://settings.cfg and put into effect at once. Advanced rows go Thấp / Trung Bình / Cao / Epic;
## "Chất Lượng" sets all of them, and turns into "Tuỳ Chỉnh" when one is changed on its own.
## "Cao" is the look the game was tuned at; Epic draws further and finer.

const PATH := "user://settings.cfg"
const LEVELS := ["Thấp", "Trung Bình", "Cao", "Epic"]
const CUSTOM := 4
const WINDOW_MODES := ["Cửa Sổ", "Cửa Sổ Không Viền", "Toàn Màn Hình"]
const RESOLUTIONS := [Vector2i(1280, 720), Vector2i(1366, 768), Vector2i(1600, 900), Vector2i(1920, 1080),
	Vector2i(2560, 1440), Vector2i(3840, 2160)]
const ADVANCED := ["view_distance", "anti_aliasing", "shadows", "post_effects", "textures", "effects"]

## Per Advanced level (Thấp, Trung Bình, Cao, Epic).
const VIEW_SCALE := [0.45, 0.65, 1.0, 1.25]
const CAMERA_FAR := [700.0, 1000.0, 1600.0, 2000.0]
const GRASS_RADIUS := [1, 2, 3, 3]
const SHADOW_DISTANCE := [60.0, 100.0, 150.0, 220.0]
const SHADOW_ATLAS := [2048, 2048, 4096, 4096]
const SHADOW_FILTER := [1, 2, 3, 4]
const ANISOTROPY := [1, 2, 2, 4]
const MIP_BIAS := [1.0, 0.5, 0.0, 0.0]
const FX_DETAIL := [0.35, 0.6, 1.0, 1.0]
const GRASS_DENSITY := [0.4, 0.7, 1.0, 1.25]

const DEFAULTS := {
	"window_mode": 0,
	"resolution": "1600x900",
	"quality_scale": 1.0,
	"brightness": 1.0,
	"motion_blur": 0.0,
	"fov": 62.0,
	"vsync": true,
	"quality": 2,
	"view_distance": 2,
	"anti_aliasing": 2,
	"shadows": 2,
	"post_effects": 2,
	"textures": 2,
	"effects": 2,
}

static var values := DEFAULTS.duplicate()
## Tools and test tours run on the defaults and never write the file.
static var use_defaults := false
static var _loaded := false


static func get_value(key: String) -> Variant:
	_load_once()
	return values.get(key, DEFAULTS[key])


## Changes one setting, keeps "Chất Lượng" in step, saves and applies.
static func set_value(key: String, v: Variant) -> void:
	_load_once()
	values[key] = v
	if key == "quality" and int(v) != CUSTOM:
		for k in ADVANCED:
			values[k] = int(v)
	elif key in ADVANCED:
		var first := int(values[ADVANCED[0]])
		var same := true
		for k in ADVANCED:
			same = same and int(values[k]) == first
		values["quality"] = first if same else CUSTOM
	save()
	apply(Game.world)


static func reset() -> void:
	values = DEFAULTS.duplicate()
	save()
	apply(Game.world)


static func _load_once() -> void:
	if _loaded:
		return
	_loaded = true
	values = DEFAULTS.duplicate()
	if use_defaults or Game.has_arg("--autoshot"):
		return
	var cfg := ConfigFile.new()
	if cfg.load(PATH) != OK:
		return
	for k in DEFAULTS:
		if cfg.has_section_key("graphics", k):
			values[k] = cfg.get_value("graphics", k)


static func save() -> void:
	if use_defaults or Game.has_arg("--autoshot"):
		return
	var cfg := ConfigFile.new()
	for k in values:
		cfg.set_value("graphics", k, values[k])
	cfg.save(PATH)


## The resolutions this screen can show, as "WxH".
static func resolutions() -> PackedStringArray:
	var out := PackedStringArray()
	var screen := DisplayServer.screen_get_size() if Game.has_screen() else Vector2i(3840, 2160)
	for r in RESOLUTIONS:
		if r.x <= screen.x and r.y <= screen.y:
			out.append("%dx%d" % [r.x, r.y])
	if out.is_empty():
		out.append("%dx%d" % [screen.x, screen.y])
	return out


## Puts every setting into effect on the world scene (main.tscn's root).
static func apply(world: Node) -> void:
	_load_once()
	var lv := func(k: String) -> int: return clampi(int(values[k]), 0, 3)
	Fx.detail = FX_DETAIL[lv.call("effects")]
	if world == null or not is_instance_valid(world):
		return
	var vp := world.get_viewport()
	if Game.has_screen() and not Game.has_arg("--autoshot") and not use_defaults:
		_apply_window()
	vp.scaling_3d_mode = Viewport.SCALING_3D_MODE_FSR if float(values.quality_scale) < 0.99 else Viewport.SCALING_3D_MODE_BILINEAR
	vp.scaling_3d_scale = clampf(float(values.quality_scale), 0.5, 1.0)
	var aa: int = lv.call("anti_aliasing")
	vp.msaa_3d = [Viewport.MSAA_DISABLED, Viewport.MSAA_DISABLED, Viewport.MSAA_2X, Viewport.MSAA_4X][aa]
	vp.screen_space_aa = [Viewport.SCREEN_SPACE_AA_DISABLED, Viewport.SCREEN_SPACE_AA_FXAA, Viewport.SCREEN_SPACE_AA_FXAA,
		Viewport.SCREEN_SPACE_AA_SMAA][aa]
	var tx: int = lv.call("textures")
	vp.anisotropic_filtering_level = ANISOTROPY[tx]
	vp.texture_mipmap_bias = MIP_BIAS[tx]
	var sh: int = lv.call("shadows")
	RenderingServer.directional_shadow_atlas_set_size(SHADOW_ATLAS[sh], true)
	RenderingServer.directional_soft_shadow_filter_set_quality(SHADOW_FILTER[sh])
	RenderingServer.positional_soft_shadow_filter_set_quality(SHADOW_FILTER[sh])
	var sun := world.get_node_or_null("Sun") as DirectionalLight3D
	if sun:
		sun.directional_shadow_max_distance = SHADOW_DISTANCE[sh]
	var we := world.get_node_or_null("WorldEnvironment") as WorldEnvironment
	if we and we.environment:
		var env := we.environment
		var pe: int = lv.call("post_effects")
		env.glow_enabled = pe >= 1
		env.ssao_enabled = pe >= 2
		RenderingServer.environment_set_ssao_quality(RenderingServer.ENV_SSAO_QUALITY_MEDIUM if pe < 3 else RenderingServer.ENV_SSAO_QUALITY_HIGH,
			pe < 3, 0.5, 2, 50.0, 300.0)
		env.adjustment_enabled = true
		env.adjustment_brightness = clampf(float(values.brightness), 0.5, 1.5)
	var vd: int = lv.call("view_distance")
	var foliage := world.get_node_or_null("Foliage")
	if foliage and foliage.has_method("set_view_distance"):
		foliage.set_view_distance(VIEW_SCALE[vd])
	var grass := world.get_node_or_null("Grass")
	if grass and grass.has_method("set_quality"):
		grass.set_quality(GRASS_RADIUS[vd], GRASS_DENSITY[lv.call("effects")])
	var cam := Game.cam()
	if cam:
		cam.fov_base = clampf(float(values.fov), 50.0, 90.0)
		if cam.cam:
			cam.cam.far = CAMERA_FAR[vd]
		cam.set_motion_blur(clampf(float(values.motion_blur), 0.0, 1.0))


static func _apply_window() -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if values.vsync else DisplayServer.VSYNC_DISABLED)
	var mode := int(values.window_mode)
	var want: int = [DisplayServer.WINDOW_MODE_WINDOWED, DisplayServer.WINDOW_MODE_FULLSCREEN,
		DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN][clampi(mode, 0, 2)]
	if DisplayServer.window_get_mode() != want:
		DisplayServer.window_set_mode(want)
	if mode == 0:
		var bits := String(values.resolution).split("x")
		if bits.size() == 2:
			var size := Vector2i(int(bits[0]), int(bits[1]))
			if DisplayServer.window_get_size() != size:
				DisplayServer.window_set_size(size)
				var screen := DisplayServer.screen_get_usable_rect()
				DisplayServer.window_set_position(screen.position + (screen.size - size) / 2)
