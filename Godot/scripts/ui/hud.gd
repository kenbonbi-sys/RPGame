class_name Hud
extends CanvasLayer
## The screen in the Dauntless layout: top left the hero's live portrait, level, name, a slanted
## teal life bar (blue where a guard buff shields it), the yellow stamina bar (the grip), the
## segmented energy bar and the buffs; top centre a compass with the colossus and the village
## marked and the colossus's name and life under it; top right the hunt emblem with how much life
## the colossus has left; bottom right the consumable slots, bottom left the event log; nothing
## else (Dauntless shows no skill bar: the weapon is the moveset, the loadout lives in its menu).
## All text in Barlow Condensed.

const TEAL := Color(0.25, 0.85, 0.68)
## Healthy life of monsters and colossi (JuicyBar turns it amber, then red).
const CREATURE := Color(0.56, 0.84, 0.32)
const TEAL_DARK := Color(0.08, 0.32, 0.28)
const SHIELD := Color(0.35, 0.62, 1.0)
const STAMINA := Color(1.0, 0.78, 0.22)
const ENERGY := Color(0.45, 0.78, 1.0)
const GOLD := Color(0.95, 0.78, 0.38)
const PANEL := Color(0.03, 0.05, 0.07, 0.62)
const TEXT := Color(0.95, 0.94, 0.9)
const FONT_SEMI := preload("res://assets/fonts/BarlowCondensed-SemiBold.ttf")
const FONT_BOLD := preload("res://assets/fonts/BarlowCondensed-Bold.ttf")
const FONT_XBOLD := preload("res://assets/fonts/BarlowCondensed-ExtraBold.ttf")

var hero: Node3D
var boss: Node3D

var _frame: PlayerFrame
var _compass: Compass
var _emblem: Emblem
var _potions: Array[PotionSlot] = []
var _heavy: HeavyMeter
var _log: LogBox
var _hint: Label
var _area: Label
var _center: Label
var _skill_label: Label
var _help: Label
var _debug: Label
var _fps: Label
var _flash: ColorRect
var _portrait_cam: Camera3D
var _area_name := ""
var _skill_until := 0.0
var _center_until := 0.0


func _ready() -> void:
	layer = 10
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	_flash = ColorRect.new()
	_flash.set_anchors_preset(Control.PRESET_FULL_RECT)
	_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_flash.color = Color(1, 0, 0, 0)
	root.add_child(_flash)

	_frame = PlayerFrame.new()
	_place(root, _frame, Control.PRESET_TOP_LEFT, Vector2(14, 10), Vector2(640, 150))

	_compass = Compass.new()
	_place(root, _compass, Control.PRESET_CENTER_TOP, Vector2(-330, 6), Vector2(660, 100))

	_emblem = Emblem.new()
	_place(root, _emblem, Control.PRESET_TOP_RIGHT, Vector2(-230, 6), Vector2(220, 190))
	_fps = _label(root, 14, Color(1, 1, 1, 0.6), FONT_SEMI)
	_place(root, _fps, Control.PRESET_TOP_RIGHT, Vector2(-170, 6), Vector2(160, 20))
	_fps.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	_heavy = HeavyMeter.new()
	_place(root, _heavy, Control.PRESET_CENTER_BOTTOM, Vector2(-110, -120), Vector2(220, 9))

	# Dauntless's four consumable slots (the fourth is free for now)
	for i in 4:
		var p := PotionSlot.new()
		p.index = i
		_place(root, p, Control.PRESET_BOTTOM_RIGHT, Vector2(-292 + i * 68, -122), Vector2(60, 108))
		_potions.append(p)

	_log = LogBox.new()
	_place(root, _log, Control.PRESET_BOTTOM_LEFT, Vector2(16, -236), Vector2(470, 220))

	_skill_label = _label(root, 26, Color(1.0, 0.8, 0.5), FONT_XBOLD, true)
	_place(root, _skill_label, Control.PRESET_CENTER_TOP, Vector2(-450, 108), Vector2(900, 34))
	_hint = _label(root, 21, TEXT, FONT_SEMI, true)
	_place(root, _hint, Control.PRESET_CENTER_BOTTOM, Vector2(-450, -170), Vector2(900, 30))

	_area = _label(root, 46, Color(0.98, 0.95, 0.85), FONT_XBOLD, true)
	_place(root, _area, Control.PRESET_CENTER_TOP, Vector2(-450, 190), Vector2(900, 60))
	_area.modulate.a = 0.0

	_center = _label(root, 58, Color(0.98, 0.94, 0.82), FONT_XBOLD, true)
	_place(root, _center, Control.PRESET_CENTER, Vector2(-600, -90), Vector2(1200, 80))
	_center.modulate.a = 0.0

	_help = _label(root, 18, TEXT, FONT_SEMI)
	_help.text = "WASD chạy · Chuột xoay camera · Space nhảy · Shift lướt (cả khi đang bay; giữ để chạy nhanh)\n" + \
		"Chuột trái đòn thường · Chuột phải đòn nặng (giữ để tụ lực) · Z X C bình thuốc · F nói chuyện\n" + \
		"Giữ E để bám và leo · đang bám thì giữ rồi thả chuột trái để đâm · Q / chuột giữa nhìn về phía boss\n" + \
		"B trang bị · Esc menu và cài đặt · F1 bảng phím · F3 gỡ lỗi"
	_place(root, _help, Control.PRESET_TOP_RIGHT, Vector2(-780, 196), Vector2(760, 110))
	_help.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	get_tree().create_timer(14.0).timeout.connect(func() -> void: _fade(_help, 0.0, 1.5))

	_debug = _label(root, 14, Color(0.8, 1.0, 0.8), FONT_SEMI)
	_place(root, _debug, Control.PRESET_TOP_LEFT, Vector2(20, 168), Vector2(600, 20))
	_debug.visible = false

	Game.screen_flash.connect(_screen_flash)
	Game.message.connect(func(t: String, c: Color, icon: String) -> void: _log.add(t, c, icon))
	Game.boss_engaged.connect(_on_boss_engaged)
	Game.boss_defeated.connect(_on_boss_defeated)
	Game.hero_died.connect(func() -> void: show_center("BẠN ĐÃ GỤC NGÃ", 3.0))
	Game.enemy_killed.connect(func(e: Node3D, _credited: Array) -> void:
		if is_instance_valid(e) and "display_name" in e:
			_log.add("Hạ gục %s" % e.display_name, Color(0.82, 0.84, 0.86), "kill"))


## Anchors `c` to a corner or edge of `parent` and places it at `offset` from there.
static func _place(parent: Control, c: Control, preset: int, offset: Vector2, size: Vector2) -> void:
	if c.get_parent() == null:
		parent.add_child(c)
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	c.set_anchors_preset(preset)
	c.offset_left = offset.x
	c.offset_top = offset.y
	c.offset_right = offset.x + size.x
	c.offset_bottom = offset.y + size.y


static func _label(parent: Control, size: int, color: Color, font: Font, centered := false) -> Label:
	var l := Label.new()
	l.add_theme_font_override("font", font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.add_theme_constant_override("outline_size", 6)
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if centered:
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	parent.add_child(l)
	return l


func bind_hero(h: Node3D) -> void:
	hero = h
	_frame.hero = h
	_compass.hero = h
	_heavy.hero = h
	for p in _potions:
		p.hero = h
	h.hurt.connect(func(amount: float) -> void:
		_screen_flash(Color(0.9, 0.1, 0.1), clampf(amount / 80.0, 0.12, 0.35), 0.3))
	h.stats.leveled_up.connect(func(lv: int) -> void: _log.add("Lên cấp %d!" % lv, GOLD, "level"))
	h.stats.xp_gained.connect(func(x: int) -> void: _log.add("+%d kinh nghiệm" % x, Color(0.72, 0.84, 1.0), "xp"))
	if Game.has_screen():
		_make_portrait()


## A small camera that films the hero's face for the portrait (it sees only the hero's layer).
func _make_portrait() -> void:
	var vp := SubViewport.new()
	vp.size = Vector2i(160, 160)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(vp)
	_portrait_cam = Camera3D.new()
	_portrait_cam.fov = 26.0
	_portrait_cam.near = 0.05
	_portrait_cam.cull_mask = HeroModel.PORTRAIT_LAYER
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.13, 0.2, 0.24)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.7, 0.82)
	env.ambient_light_energy = 0.9
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	_portrait_cam.environment = env
	vp.add_child(_portrait_cam)
	_frame.portrait = vp.get_texture()


func _update_portrait() -> void:
	if _portrait_cam == null:
		return
	var head := hero.global_position + Vector3.UP * 1.62
	var model := hero.get("model") as HeroModel
	if model and model.skeleton:
		var i := model.skeleton.find_bone("Head")
		if i >= 0:
			head = model.skeleton.global_transform * model.skeleton.get_bone_global_pose(i).origin + Vector3.UP * 0.07
	var f: Vector3 = hero.facing
	var from := head + f * 0.95 - f.cross(Vector3.UP) * 0.3 + Vector3.UP * 0.04
	_portrait_cam.global_transform = Transform3D(Basis.looking_at(head - from, Vector3.UP), from)


func _on_boss_engaged(b: Node3D) -> void:
	if boss != b:
		if boss and boss.is_connected("skill_used", _on_skill):
			boss.disconnect("skill_used", _on_skill)
		boss = b
		b.skill_used.connect(_on_skill)
		_log.add("%s đã phát hiện ngươi!" % b.display_name, Color(1.0, 0.55, 0.45), "kill")
	_compass.boss = b
	_emblem.boss = b


func _on_skill(skill: String) -> void:
	_skill_label.text = skill.to_upper()
	_skill_until = Game.now() + 2.2


func _on_boss_defeated(b: Node3D) -> void:
	show_center("%s ĐÃ NGÃ XUỐNG" % String(b.display_name).to_upper(), 5.0)
	_log.add("%s đã ngã xuống." % b.display_name, GOLD, "level")


func show_center(text: String, seconds: float) -> void:
	_center.text = text
	_center_until = Game.now() + seconds
	_fade(_center, 1.0, 0.6)


func show_area(text: String) -> void:
	if text == _area_name:
		return
	_area_name = text
	_area.text = text.to_upper()
	_compass.area = text
	var tw := create_tween()
	tw.tween_property(_area, "modulate:a", 1.0, 0.8)
	tw.tween_interval(2.2)
	tw.tween_property(_area, "modulate:a", 0.0, 1.2)


func _screen_flash(color: Color, strength: float, seconds: float) -> void:
	_flash.color = Color(color, strength)
	var tw := create_tween()
	tw.tween_property(_flash, "color:a", 0.0, seconds)


func _process(_delta: float) -> void:
	var t := Game.now()
	if _center.modulate.a > 0.99 and t > _center_until:
		_fade(_center, 0.0, 1.0)
	_skill_label.modulate.a = clampf((_skill_until - t) / 0.4, 0.0, 1.0)
	_fps.text = "FPS %d" % Engine.get_frames_per_second()
	if not is_instance_valid(hero):
		return
	_update_portrait()
	_hint.text = _hint_text()
	show_area(WorldMap.area_name(WorldMap.map_of(hero.global_position)))
	if Input.is_action_just_pressed("debug_overlay"):
		_debug.visible = not _debug.visible
	if _debug.visible:
		_debug.text = "%s · %s · bám %.0f" % [Hero.State.keys()[hero.state], str(hero.global_position.snapped(Vector3.ONE * 0.1)), hero.grip]


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.physical_keycode == KEY_F1:
		_fade(_help, 1.0 if _help.modulate.a < 0.5 else 0.0, 0.3)


func _hint_text() -> String:
	if hero.is_dead():
		return ""
	if Game.menu_open:
		return ""
	if Npc.focus and is_instance_valid(Npc.focus):
		return "[F]  Nói chuyện với %s" % Npc.focus.data.name
	if hero.weak_point_near and (hero.is_climbing or hero.on_colossus):
		return "Giữ chuột trái rồi thả để đâm vào điểm yếu"
	if hero.is_climbing:
		return "WASD leo · Space bật nhảy · thả E để buông"
	if hero.grip_hint:
		return "Giữ E để bám"
	if hero.on_colossus:
		return "Giữ E để bám chặt khi nó lắc mình"
	return ""


func _fade(c: CanvasItem, to: float, seconds: float) -> void:
	var tw := create_tween()
	tw.tween_property(c, "modulate:a", to, seconds)


## A slanted bar, the Dauntless parallelogram.
static func slant(ci: CanvasItem, r: Rect2, col: Color, slope := 8.0) -> void:
	if r.size.x <= 0.5 or r.size.y <= 0.5:
		return
	# never let the slope eat the whole width (a thin bar would be a line and fail to triangulate)
	var s := minf(slope, r.size.x * 0.5)
	ci.draw_colored_polygon(PackedVector2Array([r.position + Vector2(s, 0), r.position + Vector2(r.size.x, 0),
		r.position + Vector2(r.size.x - s, r.size.y), r.position + Vector2(0, r.size.y)]), col)


static func slant_outline(ci: CanvasItem, r: Rect2, col: Color, slope: float, width: float) -> void:
	var s := minf(slope, r.size.x)
	ci.draw_polyline(PackedVector2Array([r.position + Vector2(s, 0), r.position + Vector2(r.size.x, 0),
		r.position + Vector2(r.size.x - s, r.size.y), r.position + Vector2(0, r.size.y), r.position + Vector2(s, 0)]), col, width, true)


# ======================================================================== widgets
class PlayerFrame:
	extends Control
	## Portrait in a ring that doubles as a life gauge, level badge, name and class, life bar with
	## the shield and the lingering loss, stamina (grip), energy in segments, XP sliver, buffs.
	var hero: Node3D
	var portrait: Texture2D
	var _life := JuicyBar.new()

	func _process(delta: float) -> void:
		if hero and is_instance_valid(hero):
			_life.update(hero.hp / maxf(1.0, hero.max_hp), delta)
		queue_redraw()

	func _draw() -> void:
		if hero == null or not is_instance_valid(hero):
			return
		var c := Vector2(62, 66)
		var r := 46.0
		var hp_f: float = clampf(hero.hp / maxf(1.0, hero.max_hp), 0.0, 1.0)
		# portrait
		draw_circle(c, r + 7, Color(0, 0, 0, 0.5))
		draw_circle(c, r, Color(0.13, 0.2, 0.24))
		if portrait:
			var pts := PackedVector2Array()
			var uvs := PackedVector2Array()
			for i in 48:
				var d := Vector2.from_angle(TAU * i / 48.0)
				pts.append(c + d * (r - 1))
				uvs.append(Vector2(0.5, 0.5) + d * 0.5)
			draw_colored_polygon(pts, Color.WHITE, uvs, portrait)
		draw_arc(c, r + 3, 0.0, TAU, 64, Color(Hud.TEAL_DARK, 0.95), 5.0, true)
		draw_arc(c, r + 3, PI * 0.5, PI * 0.5 + TAU * _life.fill, 64, _life.color(Hud.TEAL), 5.0, true)
		# level badge
		var badge := Rect2(c + Vector2(r * 0.42, r * 0.5), Vector2(30, 26))
		Hud.slant(self, badge.grow(2), Color(0, 0, 0, 0.7), 6)
		Hud.slant(self, badge, Color(0.95, 0.52, 0.26), 6)
		draw_string(Hud.FONT_XBOLD, badge.position + Vector2(0, 21), str(hero.level), HORIZONTAL_ALIGNMENT_CENTER, badge.size.x, 21, Color.WHITE)
		# name and class
		var nm := String(hero.hero_name).to_upper()
		draw_string_outline(Hud.FONT_XBOLD, Vector2(124, 28), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 25, 5, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, Vector2(124, 28), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 25, Hud.TEXT)
		var cls := String(DB.classes.get(hero.stats.class_id, {}).get("displayName", "")).to_upper()
		var nx := 124.0 + Hud.FONT_XBOLD.get_string_size(nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 25).x + 10.0
		draw_string_outline(Hud.FONT_BOLD, Vector2(nx, 28), cls, HORIZONTAL_ALIGNMENT_LEFT, -1, 17, 4, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_BOLD, Vector2(nx, 28), cls, HORIZONTAL_ALIGNMENT_LEFT, -1, 17, Color(Hud.TEAL, 0.95))
		# life
		var bar := Rect2(120 + _life.shove() * 9.0, 36, 440, 20)
		Hud.slant(self, bar.grow(2), Color(0, 0, 0, 0.62), 11)
		Hud.slant(self, Rect2(bar.position, Vector2(bar.size.x * _life.ghost, bar.size.y)), _life.ghost_color(), 11)
		Hud.slant(self, Rect2(bar.position, Vector2(bar.size.x * _life.fill, bar.size.y)), _life.color(Hud.TEAL), 11)
		if _life.flash() > 0.0:
			Hud.slant_outline(self, bar.grow(2), Color(1, 1, 1, _life.flash()), 11, 2.5)
		hp_f = _life.fill
		var guard: float = clampf(1.0 - hero.health.damage_taken_multiplier, 0.0, 1.0)
		if guard > 0.01:
			var gw: float = bar.size.x * hp_f * guard
			Hud.slant(self, Rect2(bar.position + Vector2(bar.size.x * hp_f - gw, 0), Vector2(gw, bar.size.y)), Color(Hud.SHIELD, 0.9), 11)
		Hud.slant(self, Rect2(bar.position + Vector2(2, 0), Vector2(bar.size.x * hp_f - 2, 5)), Color(1, 1, 1, 0.22), 3)
		draw_string(Hud.FONT_BOLD, bar.position + Vector2(16, 16), "%d / %d" % [roundi(hero.hp), roundi(hero.max_hp)],
			HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(0.02, 0.12, 0.1, 0.9))
		# stamina = the grip
		var st := Rect2(126, 61, 400, 6)
		Hud.slant(self, st.grow(1), Color(0, 0, 0, 0.55), 4)
		var g: float = hero.grip / maxf(1.0, hero.max_grip)
		Hud.slant(self, Rect2(st.position, Vector2(st.size.x * g, st.size.y)), Hud.STAMINA if g > 0.25 else Color(1.0, 0.35, 0.25), 4)
		# energy in segments of 10
		var en := Rect2(130, 73, 330, 11)
		var segs := maxi(1, ceili(hero.max_energy / 10.0))
		var seg_w := en.size.x / segs
		for i in segs:
			var cell := Rect2(en.position + Vector2(i * seg_w, 0), Vector2(seg_w - 3, en.size.y))
			Hud.slant(self, cell, Color(0, 0, 0, 0.55), 4)
			var fill := clampf((hero.energy - i * 10.0) / 10.0, 0.0, 1.0)
			if fill > 0.0:
				Hud.slant(self, Rect2(cell.position, Vector2(cell.size.x * fill, cell.size.y)), Hud.ENERGY if fill >= 1.0 else Color(Hud.ENERGY, 0.55), 4)
		# XP
		var need := PlayerStats.xp_to_next(hero.level)
		var xr := Rect2(132, 89, 326, 3)
		draw_rect(xr, Color(0, 0, 0, 0.5))
		if need > 0:
			draw_rect(Rect2(xr.position, Vector2(xr.size.x * clampf(float(hero.stats.xp) / need, 0.0, 1.0), xr.size.y)), Hud.GOLD)
		# buffs
		var x := 124.0
		var now := StatusEffects._scaled_now()
		for id in hero.buffs:
			var b: Dictionary = hero.buffs[id]
			var box := Rect2(x, 100, 32, 32)
			draw_rect(box, Color(0.06, 0.09, 0.11, 0.85))
			draw_rect(box, Hud.GOLD, false, 1.5)
			var label := String(b.spec.get("displayName", id))
			draw_string(Hud.FONT_XBOLD, box.position + Vector2(0, 22), label.substr(0, 2).to_upper(), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 17, Hud.GOLD)
			draw_string_outline(Hud.FONT_BOLD, box.position + Vector2(0, 46), str(ceili(b.until - now)), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 14, 4, Color(0, 0, 0, 0.7))
			draw_string(Hud.FONT_BOLD, box.position + Vector2(0, 46), str(ceili(b.until - now)), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 14, Hud.TEXT)
			x += 38.0


class Compass:
	extends Control
	## The heading strip; the colossi and the village are marked on it. While a colossus hunts the
	## hero, its name and life (a JuicyBar) sit under the strip; otherwise the area's name.
	var hero: Node3D
	var boss: Node3D
	var area := ""
	var _boss_life := JuicyBar.new()
	var _life_of: Node3D
	const MARKS := ["B", "ĐB", "Đ", "ĐN", "N", "TN", "T", "TB"]
	const SPAN := 100.0          ## degrees either side of the centre

	func _process(delta: float) -> void:
		if boss and is_instance_valid(boss) and boss.has_method("life_fraction") and _life_of == boss:
			_boss_life.update(boss.life_fraction(), delta)
		queue_redraw()

	func _draw() -> void:
		var cam := Game.cam()
		if cam == null:
			return
		var w := size.x
		var strip := Rect2(70, 4, w - 140, 34)
		Hud.slant(self, strip, Hud.PANEL, 14)
		draw_rect(Rect2(strip.position + Vector2(10, strip.size.y - 2), Vector2(strip.size.x - 20, 2)), Color(Hud.TEAL, 0.45))
		var fwd := cam.forward_flat()
		var heading := rad_to_deg(atan2(fwd.x, -fwd.z))
		for deg in range(0, 360, 15):
			var d := wrapf(deg - heading, -180.0, 180.0)
			if absf(d) > SPAN:
				continue
			var x := _x(strip, d)
			var fade := 1.0 - absf(d) / SPAN
			if deg % 45 == 0:
				var label: String = MARKS[deg / 45]
				var big := label.length() == 1
				draw_string(Hud.FONT_XBOLD, Vector2(x - 20, strip.position.y + (25 if big else 23)), label, HORIZONTAL_ALIGNMENT_CENTER, 40,
					23 if big else 17, Color(Hud.TEXT if label != "B" else Color(1.0, 0.6, 0.45), 0.25 + 0.75 * fade))
			else:
				draw_line(Vector2(x, strip.position.y + 22), Vector2(x, strip.position.y + 29), Color(1, 1, 1, 0.35 * fade), 2.0)
		var mid := w * 0.5
		draw_colored_polygon(PackedVector2Array([Vector2(mid - 6, strip.end.y + 1), Vector2(mid + 6, strip.end.y + 1), Vector2(mid, strip.end.y - 6)]), Hud.TEAL)
		if hero and is_instance_valid(hero):
			_marker(strip, heading, WorldMap.world(WorldMap.village_center()), Color(0.5, 1.0, 0.62), false)
			for c in Game.colossi:
				if is_instance_valid(c) and not c.dead:
					_marker(strip, heading, c.global_position, Color(1.0, 0.36, 0.3), true)
		if boss and is_instance_valid(boss) and boss.engaged and not boss.dead:
			var title := String(boss.display_name).to_upper()
			if String(boss.get("element")) != "":
				title += "  ·  " + Elements.display_name(boss.element).to_upper()
			draw_string_outline(Hud.FONT_XBOLD, Vector2(0, 64), title, HORIZONTAL_ALIGNMENT_CENTER, w, 22, 5, Color(0, 0, 0, 0.6))
			draw_string(Hud.FONT_XBOLD, Vector2(0, 64), title, HORIZONTAL_ALIGNMENT_CENTER, w, 22, Color(1.0, 0.84, 0.66))
			if _life_of != boss:
				_life_of = boss
				_boss_life = JuicyBar.new(boss.life_fraction())
			var br := Rect2(120 + _boss_life.shove() * 7.0, 72, w - 240, 8)
			Hud.slant(self, br.grow(2), Color(0, 0, 0, 0.62), 6)
			Hud.slant(self, Rect2(br.position, Vector2(br.size.x * _boss_life.ghost, br.size.y)), _boss_life.ghost_color(), 6)
			Hud.slant(self, Rect2(br.position, Vector2(br.size.x * _boss_life.fill, br.size.y)), _boss_life.color(Hud.CREATURE), 6)
			if _boss_life.flash() > 0.0:
				Hud.slant_outline(self, br.grow(2), Color(1, 1, 1, _boss_life.flash()), 6, 2.0)
			# the stagger bar (Dauntless keeps it hidden; here a thin blue line), orange while staggered
			var sr := Rect2(br.position + Vector2(0, 12), Vector2(br.size.x, 4))
			Hud.slant(self, sr.grow(1), Color(0, 0, 0, 0.5), 3)
			if boss.is_staggered():
				var left: float = boss.poise.broken_remaining()
				Hud.slant(self, Rect2(sr.position, Vector2(sr.size.x * clampf(left / boss.stagger_seconds, 0.0, 1.0), sr.size.y)),
					Color(1.0, 0.55, 0.16), 3)
				var txt := "CHOÁNG  %.1f s  ·  SÁT THƯƠNG +50%%" % left
				draw_string_outline(Hud.FONT_XBOLD, Vector2(0, sr.end.y + 18), txt, HORIZONTAL_ALIGNMENT_CENTER, w, 16, 4, Color(0, 0, 0, 0.6))
				draw_string(Hud.FONT_XBOLD, Vector2(0, sr.end.y + 18), txt, HORIZONTAL_ALIGNMENT_CENTER, w, 16, Color(1.0, 0.62, 0.25))
			else:
				var sf: float = boss.stagger_fraction()
				Hud.slant(self, Rect2(sr.position, Vector2(sr.size.x * sf, sr.size.y)), Color(0.45, 0.72, 1.0) if sf < 0.7 else Color(0.7, 0.88, 1.0), 3)
		elif area != "":
			var a := area.to_upper()
			draw_string_outline(Hud.FONT_BOLD, Vector2(0, 60), a, HORIZONTAL_ALIGNMENT_CENTER, w, 17, 4, Color(0, 0, 0, 0.55))
			draw_string(Hud.FONT_BOLD, Vector2(0, 60), a, HORIZONTAL_ALIGNMENT_CENTER, w, 17, Color(Hud.TEXT, 0.8))

	func _x(strip: Rect2, d: float) -> float:
		return strip.position.x + strip.size.x * (0.5 + d / (2.0 * SPAN))

	func _marker(strip: Rect2, heading: float, p: Vector3, col: Color, diamond: bool) -> void:
		var to := p - hero.global_position
		var d := clampf(wrapf(rad_to_deg(atan2(to.x, -to.z)) - heading, -180.0, 180.0), -SPAN, SPAN)
		var x := _x(strip, d)
		var y := strip.position.y + 9.0
		if diamond:
			draw_colored_polygon(PackedVector2Array([Vector2(x, y - 7), Vector2(x + 7, y), Vector2(x, y + 7), Vector2(x - 7, y)]), col)
			draw_polyline(PackedVector2Array([Vector2(x, y - 7), Vector2(x + 7, y), Vector2(x, y + 7), Vector2(x - 7, y), Vector2(x, y - 7)]), Color(0, 0, 0, 0.6), 1.5)
		else:
			draw_circle(Vector2(x, y), 5.0, col)
			draw_arc(Vector2(x, y), 5.0, 0.0, TAU, 16, Color(0, 0, 0, 0.6), 1.5)


class Emblem:
	extends Control
	## The hunt emblem: an inverted triangle with how much life the colossus has left.
	var boss: Node3D

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if boss == null or not is_instance_valid(boss) or not boss.engaged or boss.dead:
			return
		var c := Vector2(size.x * 0.55, 92)
		var r := 58.0
		var tri := PackedVector2Array([c + Vector2(-r, -r * 0.62), c + Vector2(r, -r * 0.62), c + Vector2(0, r * 0.9)])
		draw_colored_polygon(tri, Color(0.04, 0.05, 0.07, 0.72))
		draw_polyline(PackedVector2Array([tri[0], tri[1], tri[2], tri[0]]), Color(0.78, 0.78, 0.8, 0.95), 3.0, true)
		var k := 0.8
		var inner := PackedVector2Array([c + (tri[0] - c) * k, c + (tri[1] - c) * k, c + (tri[2] - c) * k, c + (tri[0] - c) * k])
		draw_polyline(inner, Color(0.78, 0.78, 0.8, 0.35), 1.5, true)
		var f: float = boss.life_fraction()
		var txt := "%d%%" % roundi(f * 100.0)
		draw_string_outline(Hud.FONT_XBOLD, c + Vector2(-60, 0), txt, HORIZONTAL_ALIGNMENT_CENTER, 120, 34, 5, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, c + Vector2(-60, 0), txt, HORIZONTAL_ALIGNMENT_CENTER, 120, 34, Hud.TEXT)


class HeavyMeter:
	extends Control
	## Trảm gathering power: a bar that fills while the right mouse button is held, gold when full.
	var hero: Node3D

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if hero == null or not is_instance_valid(hero) or not hero.has_method("heavy_charge"):
			return
		var c: float = hero.heavy_charge()
		if c < 0.0:
			return
		var r := Rect2(Vector2.ZERO, size)
		Hud.slant(self, r.grow(2), Color(0, 0, 0, 0.6), 8)
		var col := Color(1.0, 0.82, 0.35) if c >= 1.0 else Color(0.95, 0.95, 0.9)
		Hud.slant(self, Rect2(r.position, Vector2(r.size.x * c, r.size.y)), col, 8)
		var move := String(hero.heavy_name()).to_upper() if hero.has_method("heavy_name") else "TRẢM"
		var label := move + " · TỤ LỰC ĐẦY" if c >= 1.0 else move
		draw_string_outline(Hud.FONT_XBOLD, Vector2(0, -6), label, HORIZONTAL_ALIGNMENT_CENTER, size.x, 16, 4, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, Vector2(0, -6), label, HORIZONTAL_ALIGNMENT_CENTER, size.x, 16, col)


class PotionSlot:
	extends Control
	## A consumable slot as Dauntless draws them: a tall rounded dark slot, the item's round badge
	## in it, how many are left underneath; the key in small letters on top. Empty slots stay as
	## dim frames.
	var hero: Node3D
	var index := 0
	const KEYS := ["Z", "X", "C", ""]

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if hero == null or not is_instance_valid(hero):
			return
		var box := Rect2(Vector2(2, 14), Vector2(size.x - 4, size.y - 16))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.02, 0.03, 0.05, 0.5)
		sb.set_corner_radius_all(9)
		sb.border_color = Color(1, 1, 1, 0.12)
		sb.set_border_width_all(1)
		draw_style_box(sb, box)
		if index >= Hero.POTIONS.size():
			return
		var id: String = Hero.POTIONS[index]
		var n: int = hero.inventory.count(id)
		var c := box.position + Vector2(box.size.x * 0.5, box.size.y * 0.4)
		var tex := GameIcons.item(id, 96)
		if tex:
			var side := box.size.x * 0.95
			draw_texture_rect(tex, Rect2(c - Vector2(side, side) * 0.5, Vector2(side, side)), false,
				Color.WHITE if n > 0 else Color(0.45, 0.45, 0.45, 0.4))
		draw_string(Hud.FONT_BOLD, Vector2(0, 11), KEYS[index], HORIZONTAL_ALIGNMENT_CENTER, size.x, 13, Color(1, 1, 1, 0.55))
		var cnt := "×%d" % n
		draw_string_outline(Hud.FONT_XBOLD, Vector2(0, box.end.y - 8), cnt, HORIZONTAL_ALIGNMENT_CENTER, size.x, 19, 4, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, Vector2(0, box.end.y - 8), cnt, HORIZONTAL_ALIGNMENT_CENTER, size.x, 19, Hud.TEXT if n > 0 else Color(1, 0.42, 0.36))


class LogBox:
	extends Control
	## Recent events with their icons, newest at the bottom, fading after a while; the chat line
	## underneath. Pickups of the same thing in a row add up ("+1 vàng" twice is "+2 vàng").
	var lines: Array = []   # [text, color, time, icon]
	static var _count := RegEx.create_from_string("^[+]([0-9]+) (.+)$")

	func add(text: String, color: Color, icon := "") -> void:
		var m := _count.search(text)
		if m and not lines.is_empty():
			var last: Array = lines[-1]
			var lm := _count.search(last[0])
			if lm and lm.get_string(2) == m.get_string(2) and last[3] == icon and Game.now() - float(last[2]) < 4.0:
				last[0] = "+%d %s" % [int(lm.get_string(1)) + int(m.get_string(1)), m.get_string(2)]
				last[2] = Game.now()
				return
		lines.append([text, color, Game.now(), icon])
		while lines.size() > 8:
			lines.pop_front()

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		var t := Game.now()
		var y := size.y - 42.0
		for i in range(lines.size() - 1, -1, -1):
			var l: Array = lines[i]
			var a := clampf(1.0 - (t - float(l[2]) - 8.0) / 2.0, 0.0, 1.0)
			if a <= 0.0:
				continue
			var col: Color = l[1]
			var x := 12.0
			if l[3] != "":
				var tex := GameIcons.item(l[3], 64)
				if tex:
					draw_texture_rect(tex, Rect2(Vector2(8, y - 21), Vector2(28, 28)), false, Color(1, 1, 1, a))
				x = 40.0
			draw_string_outline(Hud.FONT_SEMI, Vector2(x, y), l[0], HORIZONTAL_ALIGNMENT_LEFT, size.x - x - 8, 18, 4, Color(0, 0, 0, 0.6 * a))
			draw_string(Hud.FONT_SEMI, Vector2(x, y), l[0], HORIZONTAL_ALIGNMENT_LEFT, size.x - x - 8, 18, Color(col, a))
			y -= 27.0
		var chat := Rect2(0, size.y - 30, size.x, 28)
		Hud.slant(self, chat, Color(0.03, 0.05, 0.07, 0.55), 10)
		draw_string(Hud.FONT_SEMI, chat.position + Vector2(14, 20), "[Enter] Để trò chuyện" + ("" if Game.is_online() else "  ·  khi chơi online"),
			HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(1, 1, 1, 0.55))
