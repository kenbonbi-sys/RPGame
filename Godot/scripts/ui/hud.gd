class_name Hud
extends CanvasLayer
## The screen as the Dauntless HUD lays it out, and nothing more: top left the slayer's name, the
## lantern emblem, a long thin green life bar with the yellow stamina line under it, the key and
## level badges and the short weapon meter (energy); top centre a compass strip fading at both
## ends with the colossi and the village marked; top right the hunt emblem with the behemoth's life
## in per cent (Dauntless shows no behemoth life bar); bottom right the four consumable slots;
## bottom left the event log and the chat line. No skill bar: the weapon is the moveset and the
## loadout lives in its menu. All text in Barlow Condensed.

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
var _help: Label
var _debug: Label
var _fps: Label
var _flash: ColorRect
var _area_name := ""
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
	_place(root, _frame, Control.PRESET_TOP_LEFT, Vector2(12, 6), Vector2(540, 140))

	_compass = Compass.new()
	_place(root, _compass, Control.PRESET_CENTER_TOP, Vector2(-300, 2), Vector2(600, 52))

	_emblem = Emblem.new()
	_place(root, _emblem, Control.PRESET_TOP_RIGHT, Vector2(-118, 72), Vector2(90, 64))
	_fps = _label(root, 14, Color(1, 1, 1, 0.6), FONT_SEMI)
	_place(root, _fps, Control.PRESET_TOP_RIGHT, Vector2(-170, 6), Vector2(160, 20))
	_fps.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	_heavy = HeavyMeter.new()
	_place(root, _heavy, Control.PRESET_CENTER_BOTTOM, Vector2(-110, -120), Vector2(220, 9))

	# Dauntless's four consumable slots (the fourth is free for now)
	for i in 4:
		var p := PotionSlot.new()
		p.index = i
		_place(root, p, Control.PRESET_BOTTOM_RIGHT, Vector2(-272 + i * 64, -112), Vector2(56, 98))
		_potions.append(p)

	_log = LogBox.new()
	_place(root, _log, Control.PRESET_BOTTOM_LEFT, Vector2(16, -236), Vector2(470, 220))

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


func _on_boss_engaged(b: Node3D) -> void:
	if boss != b:
		if boss and boss.is_connected("skill_used", _on_skill):
			boss.disconnect("skill_used", _on_skill)
		boss = b
		b.skill_used.connect(_on_skill)
		_log.add("%s đã phát hiện ngươi!" % b.display_name, Color(1.0, 0.55, 0.45), "kill")
	_compass.boss = b
	_emblem.boss = b


## Dauntless does not name a behemoth's attacks on screen; the log keeps them.
func _on_skill(_skill: String) -> void:
	pass


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
	_fps.text = "FPS %d" % Engine.get_frames_per_second()
	if not is_instance_valid(hero):
		return
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
	## Top left as Dauntless has it: the slayer's name in small letters, the lantern emblem in a
	## cyan ring, a long thin green life bar (blue where a guard shields it, a pale ghost of what
	## was just lost), the yellow stamina line under it (the grip), a key badge and the level badge,
	## the short weapon meter with its round knob (energy), and the buffs.
	var hero: Node3D
	var _life := JuicyBar.new()
	const GREEN := Color(0.24, 0.8, 0.43)
	const GREEN_TOP := Color(0.5, 0.94, 0.62)
	const CYAN := Color(0.33, 0.88, 1.0)

	func _process(delta: float) -> void:
		if hero and is_instance_valid(hero):
			_life.update(hero.hp / maxf(1.0, hero.max_hp), delta)
		queue_redraw()

	func _draw() -> void:
		if hero == null or not is_instance_valid(hero):
			return
		# the name, small, over the bars
		var nm := String(hero.hero_name)
		draw_string_outline(Hud.FONT_BOLD, Vector2(66, 22), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, 4, Color(0, 0, 0, 0.55))
		draw_string(Hud.FONT_BOLD, Vector2(66, 22), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Hud.TEXT)
		# the life bar: long and thin, slanted at the far end, tucked behind the emblem
		var bar := Rect2(58 + _life.shove() * 8.0, 32, 452, 15)
		Hud.slant(self, bar.grow(1.5), Color(0, 0, 0, 0.8), 9)
		Hud.slant(self, bar, Color(0.03, 0.09, 0.06, 0.8), 9)
		Hud.slant(self, Rect2(bar.position, Vector2(bar.size.x * _life.ghost, bar.size.y)), _life.ghost_color(), 9)
		var col := _life.color(GREEN)
		Hud.slant(self, Rect2(bar.position, Vector2(bar.size.x * _life.fill, bar.size.y)), col, 9)
		Hud.slant(self, Rect2(bar.position, Vector2(bar.size.x * _life.fill, 5)), col.lerp(GREEN_TOP, 0.6) if col == GREEN else col.lightened(0.25), 3)
		var guard: float = clampf(1.0 - hero.health.damage_taken_multiplier, 0.0, 1.0)
		if guard > 0.01:
			var gw: float = bar.size.x * _life.fill * guard
			Hud.slant(self, Rect2(bar.position + Vector2(bar.size.x * _life.fill - gw, 0), Vector2(gw, bar.size.y)), Color(Hud.SHIELD, 0.9), 9)
		if _life.flash() > 0.0:
			Hud.slant_outline(self, bar.grow(1.5), Color(1, 1, 1, _life.flash()), 9, 2.0)
		# stamina: a thin yellow line under it
		var st := Rect2(62, 50, 428, 5)
		Hud.slant(self, st.grow(1), Color(0, 0, 0, 0.6), 4)
		var g: float = clampf(hero.grip / maxf(1.0, hero.max_grip), 0.0, 1.0)
		Hud.slant(self, Rect2(st.position, Vector2(st.size.x * g, st.size.y)), Hud.STAMINA if g > 0.25 else Color(1.0, 0.35, 0.25), 4)
		# the lantern emblem
		var c := Vector2(36, 46)
		var r := 25.0
		for k in 3:
			draw_circle(c, r + 3.0 + k * 3.0, Color(CYAN, 0.07))
		draw_circle(c, r + 2.0, Color(0, 0, 0, 0.75))
		draw_circle(c, r, Color(0.04, 0.09, 0.13))
		draw_arc(c, r, 0.0, TAU, 48, CYAN, 3.0, true)
		_lantern(c, 14.0)
		# key badge and level badge under the emblem
		var key := Rect2(Vector2(13, 78), Vector2(19, 19))
		draw_rect(key, Color(0.36, 0.38, 0.42))
		draw_rect(key, Color(0, 0, 0, 0.6), false, 1.0)
		draw_string(Hud.FONT_BOLD, key.position + Vector2(0, 15), "V", HORIZONTAL_ALIGNMENT_CENTER, key.size.x, 14, Color(0.9, 0.92, 0.95))
		var lvl := Rect2(Vector2(34, 78), Vector2(19, 19))
		draw_rect(lvl, Color(0.93, 0.47, 0.22))
		draw_rect(lvl, Color(0, 0, 0, 0.6), false, 1.0)
		draw_string(Hud.FONT_XBOLD, lvl.position + Vector2(0, 16), str(hero.level), HORIZONTAL_ALIGNMENT_CENTER, lvl.size.x, 15, Color.WHITE)
		# the weapon meter (energy): a short dark capsule, cyan fill, a round knob at its end
		var m := Rect2(Vector2(64, 79), Vector2(250, 15))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.03, 0.06, 0.09, 0.85)
		sb.set_corner_radius_all(7)
		sb.border_color = Color(1, 1, 1, 0.16)
		sb.set_border_width_all(1)
		draw_style_box(sb, m)
		var f: float = clampf(hero.energy / maxf(1.0, hero.max_energy), 0.0, 1.0)
		if f > 0.01:
			var fill := StyleBoxFlat.new()
			fill.bg_color = Color(0.13, 0.55, 0.85)
			fill.set_corner_radius_all(5)
			draw_style_box(fill, Rect2(m.position + Vector2(3, 3), Vector2((m.size.x - 6) * f, m.size.y - 6)))
			draw_rect(Rect2(m.position + Vector2(5, 4), Vector2(maxf(0.0, (m.size.x - 10) * f), 2)), Color(0.6, 0.9, 1.0, 0.6))
		var kx := m.position.x + 3 + (m.size.x - 6) * f
		draw_circle(Vector2(kx, m.get_center().y), 8.0, Color(0.03, 0.06, 0.09))
		draw_arc(Vector2(kx, m.get_center().y), 8.0, 0.0, TAU, 24, CYAN if f >= 0.999 else Color(0.75, 0.85, 0.95), 2.0, true)
		draw_circle(Vector2(kx, m.get_center().y), 3.5, CYAN if f >= 0.999 else Color(0.75, 0.85, 0.95))
		# buffs: small squares under the meter
		var x := 64.0
		var now := StatusEffects._scaled_now()
		for id in hero.buffs:
			var b: Dictionary = hero.buffs[id]
			var box := Rect2(x, 104, 26, 26)
			draw_rect(box, Color(0.05, 0.08, 0.11, 0.85))
			draw_rect(box, Color(CYAN, 0.6), false, 1.0)
			var label := String(b.spec.get("displayName", id))
			draw_string(Hud.FONT_XBOLD, box.position + Vector2(0, 18), label.substr(0, 2).to_upper(), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 14, Hud.TEXT)
			draw_string_outline(Hud.FONT_BOLD, box.position + Vector2(0, 40), str(ceili(b.until - now)), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 13, 3, Color(0, 0, 0, 0.7))
			draw_string(Hud.FONT_BOLD, box.position + Vector2(0, 40), str(ceili(b.until - now)), HORIZONTAL_ALIGNMENT_CENTER, box.size.x, 13, Hud.TEXT)
			x += 31.0

	## A little lantern: handle, cap, glowing glass, base.
	func _lantern(c: Vector2, s: float) -> void:
		draw_arc(c + Vector2(0, -s * 0.62), s * 0.32, PI, TAU, 12, CYAN, 2.0, true)
		draw_colored_polygon(PackedVector2Array([c + Vector2(-s * 0.42, -s * 0.48), c + Vector2(s * 0.42, -s * 0.48),
			c + Vector2(s * 0.3, -s * 0.66), c + Vector2(-s * 0.3, -s * 0.66)]), CYAN)
		var glass := Rect2(c + Vector2(-s * 0.36, -s * 0.46), Vector2(s * 0.72, s * 0.9))
		draw_rect(glass, Color(CYAN, 0.35))
		draw_rect(glass.grow(-s * 0.12), Color(0.85, 1.0, 1.0, 0.9))
		draw_rect(glass, CYAN, false, 1.5)
		draw_rect(Rect2(c + Vector2(-s * 0.46, s * 0.44), Vector2(s * 0.92, s * 0.16)), CYAN)


class Compass:
	extends Control
	## The heading strip: a dark band fading out at both ends, the eight headings and ticks, the
	## colossi (red) and the village (green) marked on it. Nothing under it (Dauntless shows no
	## behemoth life bar).
	var hero: Node3D
	var boss: Node3D
	var area := ""
	const MARKS := ["B", "ĐB", "Đ", "ĐN", "N", "TN", "T", "TB"]
	const SPAN := 95.0          ## degrees either side of the centre

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		var cam := Game.cam()
		if cam == null:
			return
		var strip := Rect2(0, 6, size.x, 30)
		# quads sharing their edges, the colour fading out toward both ends
		var n := 24
		for i in n:
			var u0 := float(i) / n
			var u1 := float(i + 1) / n
			var a0 := 0.5 * smoothstep(0.0, 0.22, u0) * smoothstep(1.0, 0.78, u0)
			var a1 := 0.5 * smoothstep(0.0, 0.22, u1) * smoothstep(1.0, 0.78, u1)
			var x0 := strip.position.x + strip.size.x * u0
			var x1 := strip.position.x + strip.size.x * u1
			draw_polygon(PackedVector2Array([Vector2(x0, strip.position.y), Vector2(x1, strip.position.y), Vector2(x1, strip.end.y), Vector2(x0, strip.end.y)]),
				PackedColorArray([Color(0.02, 0.04, 0.06, a0), Color(0.02, 0.04, 0.06, a1), Color(0.02, 0.04, 0.06, a1), Color(0.02, 0.04, 0.06, a0)]))
		var fwd := cam.forward_flat()
		var heading := rad_to_deg(atan2(fwd.x, -fwd.z))
		for deg in range(0, 360, 15):
			var d := wrapf(deg - heading, -180.0, 180.0)
			if absf(d) > SPAN:
				continue
			var x := _x(strip, d)
			var fade := clampf(1.0 - absf(d) / SPAN, 0.0, 1.0)
			fade = smoothstep(0.0, 0.35, fade)
			if deg % 45 == 0:
				var label: String = MARKS[deg / 45]
				var big := label.length() == 1
				draw_string_outline(Hud.FONT_BOLD, Vector2(x - 20, strip.position.y + (23 if big else 21)), label, HORIZONTAL_ALIGNMENT_CENTER, 40,
					20 if big else 15, 3, Color(0, 0, 0, 0.4 * fade))
				draw_string(Hud.FONT_BOLD, Vector2(x - 20, strip.position.y + (23 if big else 21)), label, HORIZONTAL_ALIGNMENT_CENTER, 40,
					20 if big else 15, Color(Hud.TEXT, fade))
			else:
				draw_line(Vector2(x, strip.position.y + 12), Vector2(x, strip.position.y + 18), Color(1, 1, 1, 0.5 * fade), 1.5)
		if hero and is_instance_valid(hero):
			_marker(strip, heading, WorldMap.world(WorldMap.village_center()), Color(0.5, 1.0, 0.62), false)
			for c in Game.colossi:
				if is_instance_valid(c) and not c.dead:
					_marker(strip, heading, c.global_position, Color(1.0, 0.36, 0.3), true)

	func _x(strip: Rect2, d: float) -> float:
		return strip.position.x + strip.size.x * (0.5 + d / (2.0 * SPAN))

	func _marker(strip: Rect2, heading: float, p: Vector3, col: Color, diamond: bool) -> void:
		var to := p - hero.global_position
		var d := clampf(wrapf(rad_to_deg(atan2(to.x, -to.z)) - heading, -180.0, 180.0), -SPAN * 0.92, SPAN * 0.92)
		var x := _x(strip, d)
		var y := strip.end.y + 5.0
		if diamond:
			draw_colored_polygon(PackedVector2Array([Vector2(x, y - 5), Vector2(x + 5, y), Vector2(x, y + 5), Vector2(x - 5, y)]), col)
		else:
			draw_circle(Vector2(x, y), 3.5, col)


class Emblem:
	extends Control
	## The hunt emblem, top right: a small downturned triangle with how much life the behemoth has
	## left; it burns orange while the behemoth is staggered.
	var boss: Node3D

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if boss == null or not is_instance_valid(boss) or not boss.engaged or boss.dead:
			return
		var w := size.x
		var tri := PackedVector2Array([Vector2(4, 4), Vector2(w - 4, 4), Vector2(w * 0.5, size.y - 6)])
		var staggered: bool = boss.has_method("is_staggered") and boss.is_staggered()
		draw_colored_polygon(tri, Color(0.33, 0.2, 0.11, 0.92) if not staggered else Color(0.62, 0.3, 0.08, 0.95))
		var inner := PackedVector2Array([tri[0] + Vector2(7, 4), tri[1] + Vector2(-7, 4), tri[2] + Vector2(0, -8)])
		draw_polyline(PackedVector2Array([inner[0], inner[1], inner[2], inner[0]]), Color(0.9, 0.62, 0.32, 0.6), 1.5, true)
		draw_polyline(PackedVector2Array([tri[0], tri[1], tri[2], tri[0]]), Color(0.95, 0.9, 0.82, 0.9) if not staggered else Color(1.0, 0.7, 0.3), 2.0, true)
		var f: float = boss.life_fraction()
		var txt := "%d%%" % roundi(f * 100.0)
		draw_string_outline(Hud.FONT_XBOLD, Vector2(0, size.y * 0.42), txt, HORIZONTAL_ALIGNMENT_CENTER, w, 22, 4, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, Vector2(0, size.y * 0.42), txt, HORIZONTAL_ALIGNMENT_CENTER, w, 22, Hud.TEXT)


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
	## A consumable slot as Dauntless draws them: a tall rounded see-through slot, a round glowing
	## badge for the item (red with a white cross for the life flask), how many are left under it,
	## the key in small letters above. Empty slots stay as dim frames.
	var hero: Node3D
	var index := 0
	const KEYS := ["Z", "X", "C", ""]
	const ORBS := [Color(0.86, 0.12, 0.1), Color(0.15, 0.45, 0.95), Color(0.22, 0.7, 0.3)]

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		if hero == null or not is_instance_valid(hero):
			return
		var box := Rect2(Vector2(2, 16), Vector2(size.x - 4, size.y - 18))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.0, 0.01, 0.02, 0.4)
		sb.set_corner_radius_all(10)
		sb.border_color = Color(1, 1, 1, 0.1)
		sb.set_border_width_all(1)
		draw_style_box(sb, box)
		if index >= Hero.POTIONS.size():
			return
		var id: String = Hero.POTIONS[index]
		var n: int = hero.inventory.count(id)
		var col: Color = ORBS[index] if n > 0 else Color(0.35, 0.35, 0.37)
		var c := box.position + Vector2(box.size.x * 0.5, box.size.y * 0.4)
		var r := box.size.x * 0.33
		for k in 3:
			draw_circle(c, r + 2.0 + k * 2.5, Color(col, 0.08 if n > 0 else 0.0))
		draw_circle(c, r + 1.5, Color(0, 0, 0, 0.55))
		draw_circle(c, r, col.darkened(0.35))
		draw_circle(c + Vector2(0, -r * 0.08), r * 0.86, col)
		draw_circle(c + Vector2(-r * 0.36, -r * 0.4), r * 0.24, Color(1, 1, 1, 0.3))
		var w := Color(1, 1, 1, 0.95 if n > 0 else 0.5)
		match index:
			0:
				draw_rect(Rect2(c - Vector2(r * 0.14, r * 0.5), Vector2(r * 0.28, r)), w)
				draw_rect(Rect2(c - Vector2(r * 0.5, r * 0.14), Vector2(r, r * 0.28)), w)
			1:
				draw_colored_polygon(PackedVector2Array([c + Vector2(r * 0.12, -r * 0.6), c + Vector2(-r * 0.32, r * 0.08),
					c + Vector2(-r * 0.02, r * 0.08), c + Vector2(-r * 0.14, r * 0.62), c + Vector2(r * 0.32, -r * 0.1),
					c + Vector2(r * 0.02, -r * 0.1)]), w)
			2:
				var leaf := PackedVector2Array()
				for i in 17:
					var t := float(i) / 16.0
					var y := lerpf(-0.55, 0.55, t)
					leaf.append(c + Vector2(sin(t * PI) * 0.34, y) * r)
				for i in range(15, 0, -1):
					var t := float(i) / 16.0
					leaf.append(c + Vector2(-sin(t * PI) * 0.34, lerpf(-0.55, 0.55, t)) * r)
				draw_colored_polygon(leaf, w)
		draw_string(Hud.FONT_BOLD, Vector2(0, 12), KEYS[index], HORIZONTAL_ALIGNMENT_CENTER, size.x, 13, Color(1, 1, 1, 0.5))
		var cnt := "×%d" % n
		draw_string_outline(Hud.FONT_XBOLD, Vector2(0, box.end.y - 9), cnt, HORIZONTAL_ALIGNMENT_CENTER, size.x, 17, 4, Color(0, 0, 0, 0.6))
		draw_string(Hud.FONT_XBOLD, Vector2(0, box.end.y - 9), cnt, HORIZONTAL_ALIGNMENT_CENTER, size.x, 17, Hud.TEXT if n > 0 else Color(1, 0.42, 0.36))


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
		lines.append([text, color, Game.now(), icon, Time.get_time_string_from_system().substr(0, 5)])
		while lines.size() > 8:
			lines.pop_front()

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		var t := Game.now()
		var y := size.y - 40.0
		for i in range(lines.size() - 1, -1, -1):
			var l: Array = lines[i]
			var a := clampf(1.0 - (t - float(l[2]) - 8.0) / 2.0, 0.0, 1.0)
			if a <= 0.0:
				continue
			var col: Color = l[1]
			var x := 10.0
			var stamp: String = l[4] if l.size() > 4 else ""
			draw_string_outline(Hud.FONT_SEMI, Vector2(x, y), stamp, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, 3, Color(0, 0, 0, 0.5 * a))
			draw_string(Hud.FONT_SEMI, Vector2(x, y), stamp, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(0.75, 0.78, 0.8, a))
			x += 42.0
			if l[3] != "":
				var tex := GameIcons.item(l[3], 48)
				if tex:
					draw_texture_rect(tex, Rect2(Vector2(x, y - 16), Vector2(20, 20)), false, Color(1, 1, 1, a))
				x += 24.0
			draw_string_outline(Hud.FONT_SEMI, Vector2(x, y), l[0], HORIZONTAL_ALIGNMENT_LEFT, size.x - x - 8, 16, 3, Color(0, 0, 0, 0.55 * a))
			draw_string(Hud.FONT_SEMI, Vector2(x, y), l[0], HORIZONTAL_ALIGNMENT_LEFT, size.x - x - 8, 16, Color(col, a))
			y -= 22.0
		var chat := Rect2(0, size.y - 28, size.x * 0.88, 26)
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.02, 0.03, 0.05, 0.55)
		sb.set_corner_radius_all(3)
		draw_style_box(sb, chat)
		draw_string(Hud.FONT_SEMI, chat.position + Vector2(10, 18), "[Enter] Để trò chuyện" + ("" if Game.is_online() else "  ·  khi chơi online"),
			HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(1, 1, 1, 0.55))
