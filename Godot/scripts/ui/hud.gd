class_name Hud
extends CanvasLayer
## The screen: the hero's life bar, the grip ring (the pink circle of Shadow of the Colossus,
## here in the game's gold), the colossus's life bar and the skill it is using, hints for the keys
## that matter right now, the area banner, and the fall/victory messages.

const GOLD := Color(0.86, 0.73, 0.40)
const GREEN_DARK := Color(0.06, 0.11, 0.08, 0.78)
const LIFE := Color(0.80, 0.22, 0.20)
const BOSS := Color(0.55, 0.85, 0.75)

var hero: Node3D
var boss: Node3D

var _hp_bar: ProgressBar
var _name_label: Label
var _grip: GripRing
var _boss_box: Control
var _boss_bar: ProgressBar
var _boss_name: Label
var _skill_label: Label
var _hint: Label
var _area: Label
var _center: Label
var _help: Label
var _debug: Label
var _area_name := ""
var _skill_until := 0.0
var _center_until := 0.0


func _ready() -> void:
	layer = 10
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	# life (top left)
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", _box(GREEN_DARK, GOLD, 1))
	panel.position = Vector2(24, 20)
	panel.custom_minimum_size = Vector2(320, 0)
	root.add_child(panel)
	var vb := VBoxContainer.new()
	panel.add_child(vb)
	_name_label = _label("Hồng Anh · Cấp 1", 17, GOLD)
	vb.add_child(_name_label)
	_hp_bar = _bar(LIFE, Vector2(300, 14))
	vb.add_child(_hp_bar)

	# grip ring (bottom right)
	_grip = GripRing.new()
	_grip.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT)
	_grip.position = Vector2(-150, -150)
	_grip.size = Vector2(110, 110)
	root.add_child(_grip)

	# colossus (bottom centre)
	_boss_box = VBoxContainer.new()
	_boss_box.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	_boss_box.position = Vector2(-330, -96)
	_boss_box.custom_minimum_size = Vector2(660, 0)
	_boss_box.visible = false
	root.add_child(_boss_box)
	_skill_label = _label("", 20, Color(1.0, 0.85, 0.6))
	_skill_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_boss_box.add_child(_skill_label)
	_boss_name = _label("", 19, GOLD)
	_boss_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_boss_box.add_child(_boss_name)
	_boss_bar = _bar(BOSS, Vector2(660, 12))
	_boss_box.add_child(_boss_bar)

	# hint just above the boss bar
	_hint = _label("", 17, Color(0.95, 0.93, 0.86))
	_hint.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	_hint.position = Vector2(-400, -150)
	_hint.size = Vector2(800, 30)
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hint.add_theme_constant_override("outline_size", 6)
	_hint.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	root.add_child(_hint)

	# area banner (top centre)
	_area = _label("", 34, Color(0.96, 0.92, 0.8))
	_area.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_area.position = Vector2(-400, 60)
	_area.size = Vector2(800, 50)
	_area.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_area.add_theme_constant_override("outline_size", 8)
	_area.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	_area.modulate.a = 0.0
	root.add_child(_area)

	# fall / victory (centre)
	_center = _label("", 42, Color(0.95, 0.9, 0.78))
	_center.set_anchors_preset(Control.PRESET_CENTER)
	_center.position = Vector2(-500, -60)
	_center.size = Vector2(1000, 60)
	_center.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_center.add_theme_constant_override("outline_size", 10)
	_center.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	_center.modulate.a = 0.0
	root.add_child(_center)

	# keys (top right, fades after a while; F1 shows it again)
	_help = _label("WASD: chạy · Chuột: xoay camera · Space: nhảy · Shift: Lướt (giữ: chạy nhanh)\n" +
		"Chuột trái: chém · Giữ chuột phải: bám và leo · Khi đang bám, giữ rồi thả chuột trái: đâm\n" +
		"Q hoặc chuột giữa: nhìn về phía boss · Esc: nhả chuột · F1: bảng phím", 15, Color(0.92, 0.9, 0.84))
	_help.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_help.position = Vector2(-760, 22)
	_help.size = Vector2(740, 70)
	_help.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_help.add_theme_constant_override("outline_size", 5)
	_help.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	root.add_child(_help)
	get_tree().create_timer(14.0).timeout.connect(func() -> void: _fade(_help, 0.0, 1.5))

	_debug = _label("", 13, Color(0.8, 1.0, 0.8))
	_debug.position = Vector2(24, 90)
	_debug.visible = false
	root.add_child(_debug)

	Game.boss_engaged.connect(_on_boss_engaged)
	Game.boss_defeated.connect(_on_boss_defeated)
	Game.hero_died.connect(func() -> void: show_center("Bạn đã gục ngã...", 3.0))


func bind_hero(h: Node3D) -> void:
	hero = h
	h.hp_changed.connect(func(v: float, m: float) -> void: _hp_bar.max_value = m; _hp_bar.value = v)
	_hp_bar.max_value = h.max_hp
	_hp_bar.value = h.hp
	_name_label.text = "%s · Cấp %d" % [h.hero_name, h.level]


func _on_boss_engaged(b: Node3D) -> void:
	if boss != b:
		if boss and boss.is_connected("skill_used", _on_skill):
			boss.disconnect("skill_used", _on_skill)
			boss.disconnect("health_changed", _on_boss_health)
		boss = b
		b.skill_used.connect(_on_skill)
		b.health_changed.connect(_on_boss_health)
	_boss_name.text = b.display_name
	_boss_bar.max_value = b.max_vital_hp()
	_boss_bar.value = b.vital_hp()
	_boss_box.visible = true


func _on_boss_health(v: float, m: float) -> void:
	_boss_bar.max_value = m
	_boss_bar.value = v


func _on_skill(skill: String) -> void:
	_skill_label.text = "Kỹ năng: " + skill if skill != "Choáng!" and skill != "Ngã Xuống" else skill
	_skill_until = Game.now() + 2.2


func _on_boss_defeated(b: Node3D) -> void:
	show_center("%s đã ngã xuống" % b.display_name, 5.0)


func show_center(text: String, seconds: float) -> void:
	_center.text = text
	_center_until = Game.now() + seconds
	_fade(_center, 1.0, 0.6)


func show_area(text: String) -> void:
	if text == _area_name:
		return
	_area_name = text
	_area.text = text
	var tw := create_tween()
	tw.tween_property(_area, "modulate:a", 1.0, 0.8)
	tw.tween_interval(2.4)
	tw.tween_property(_area, "modulate:a", 0.0, 1.2)


func _process(_delta: float) -> void:
	var t := Game.now()
	if _center.modulate.a > 0.99 and t > _center_until:
		_fade(_center, 0.0, 1.0)
	_skill_label.modulate.a = clampf((_skill_until - t) / 0.4, 0.0, 1.0)
	if boss:
		_boss_box.visible = (boss.engaged or boss.dead) and is_instance_valid(boss) and not (boss.dead and t > _center_until)
	if not hero:
		return
	_grip.value = hero.grip / hero.max_grip
	_grip.active = hero.is_climbing or hero.grip < hero.max_grip - 0.5
	_hint.text = _hint_text()
	show_area(WorldMap.area_name(WorldMap.map_of(hero.global_position)))
	if Input.is_action_just_pressed("debug_overlay"):
		_debug.visible = not _debug.visible
	if _debug.visible:
		_debug.text = "FPS %d · %s · %s · bám %.0f" % [Engine.get_frames_per_second(), Hero.State.keys()[hero.state],
			str(hero.global_position.snapped(Vector3.ONE * 0.1)), hero.grip]


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.physical_keycode == KEY_F1:
		_fade(_help, 1.0 if _help.modulate.a < 0.5 else 0.0, 0.3)


func _hint_text() -> String:
	if hero.is_dead():
		return ""
	if hero.weak_point_near and (hero.is_climbing or hero.on_colossus):
		return "Giữ chuột trái rồi thả để đâm vào điểm yếu"
	if hero.is_climbing:
		return "WASD: leo · Space: bật nhảy · Thả chuột phải: buông tay"
	if hero.grip_hint:
		return "Giữ chuột phải để bám"
	if hero.on_colossus:
		return "Giữ chuột phải để bám chặt khi nó lắc mình"
	if boss and boss.engaged and not boss.dead:
		return "Q: nhìn về phía " + boss.display_name
	return ""


func _fade(c: CanvasItem, to: float, seconds: float) -> void:
	var tw := create_tween()
	tw.tween_property(c, "modulate:a", to, seconds)


static func _box(bg: Color, border: Color, width: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(width)
	s.set_corner_radius_all(4)
	s.set_content_margin_all(10)
	return s


static func _label(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


static func _bar(color: Color, min_size: Vector2) -> ProgressBar:
	var b := ProgressBar.new()
	b.custom_minimum_size = min_size
	b.show_percentage = false
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.55)
	bg.border_color = Color(GOLD, 0.6)
	bg.set_border_width_all(1)
	var fg := StyleBoxFlat.new()
	fg.bg_color = color
	b.add_theme_stylebox_override("background", bg)
	b.add_theme_stylebox_override("fill", fg)
	b.value = 100
	return b


class GripRing:
	extends Control
	## The grip meter as a ring that empties clockwise and turns red when nearly gone.
	var value := 1.0:
		set(v):
			value = v
			queue_redraw()
	var active := false:
		set(v):
			if v != active:
				active = v
				var tw := create_tween()
				tw.tween_property(self, "modulate:a", 1.0 if v else 0.0, 0.35)

	func _ready() -> void:
		modulate.a = 0.0
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.42
		draw_arc(c, r, 0.0, TAU, 64, Color(0, 0, 0, 0.45), 12.0, true)
		var col := Hud.GOLD if value > 0.25 else Color(0.95, 0.3, 0.25)
		draw_arc(c, r, -PI * 0.5, -PI * 0.5 + TAU * value, 64, col, 9.0, true)
		draw_arc(c, r - 9.0, 0.0, TAU, 48, Color(col, 0.25), 1.5, true)
