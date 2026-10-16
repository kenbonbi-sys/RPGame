class_name SettingsMenu
extends Control
## The graphics options as Dauntless lays them out: a "Hiển Thị" band (window mode, resolution,
## quality scale, brightness, motion blur, field of view, VSync) and a "Nâng Cao" band (quality
## preset, view distance, anti-aliasing, shadows, post effects, textures, special effects).
## Each row: label on the left, the value between < and > (page dots under a choice), a slider or
## a switch. Mouse: hover a row, click the chevrons, click or drag a slider, click a switch.
## Keys / pad: up and down pick a row, left and right change it. Changes apply and save at once.

signal closed

const W := 720.0
const BAND_H := 38.0
const ROW_H := 44.0
const LABEL_W := 250.0

var rows: Array = []
var selected := 1
var _hover := -1
var _drag := -1
var _top := 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL
	rows = [
		{"band": "Hiển Thị"},
		{"key": "window_mode", "label": "Chế Độ Hiển Thị", "kind": "choice", "options": GameSettings.WINDOW_MODES},
		{"key": "resolution", "label": "Độ Phân Giải", "kind": "choice", "options": GameSettings.resolutions(), "by_name": true},
		{"key": "quality_scale", "label": "Tỉ Lệ Chất Lượng", "kind": "slider", "min": 0.5, "max": 1.0, "step": 0.05},
		{"key": "brightness", "label": "Độ Sáng", "kind": "slider", "min": 0.5, "max": 1.5, "step": 0.05},
		{"key": "motion_blur", "label": "Làm Mờ Chuyển Động", "kind": "slider", "min": 0.0, "max": 1.0, "step": 0.05},
		{"key": "fov", "label": "Góc Nhìn (FOV)", "kind": "slider", "min": 50.0, "max": 90.0, "step": 1.0},
		{"key": "vsync", "label": "VSync", "kind": "toggle"},
		{"band": "Nâng Cao"},
		{"key": "quality", "label": "Chất Lượng", "kind": "choice", "options": GameSettings.LEVELS + ["Tuỳ Chỉnh"], "levels": true},
		{"key": "view_distance", "label": "Tầm Nhìn", "kind": "choice", "options": GameSettings.LEVELS},
		{"key": "anti_aliasing", "label": "Khử Răng Cưa", "kind": "choice", "options": GameSettings.LEVELS},
		{"key": "shadows", "label": "Đổ Bóng", "kind": "choice", "options": GameSettings.LEVELS},
		{"key": "post_effects", "label": "Hậu Kỳ", "kind": "choice", "options": GameSettings.LEVELS},
		{"key": "textures", "label": "Kết Cấu", "kind": "choice", "options": GameSettings.LEVELS},
		{"key": "effects", "label": "Hiệu Ứng Đặc Biệt", "kind": "choice", "options": GameSettings.LEVELS},
	]
	grab_focus()


func _process(_delta: float) -> void:
	queue_redraw()


# ------------------------------------------------------------------ layout
func _panel_rect() -> Rect2:
	var h := 0.0
	for r in rows:
		h += BAND_H + 10.0 if r.has("band") else ROW_H
	h += 64.0
	return Rect2(Vector2((size.x - W) * 0.5, maxf(40.0, (size.y - h) * 0.5)), Vector2(W, h))


func _row_rect(i: int) -> Rect2:
	var p := _panel_rect()
	var y := p.position.y + 12.0
	for j in i:
		y += BAND_H + 10.0 if rows[j].has("band") else ROW_H
	var h := BAND_H if rows[i].has("band") else ROW_H
	return Rect2(Vector2(p.position.x, y), Vector2(W, h))


func _control_rect(i: int) -> Rect2:
	var r := _row_rect(i)
	return Rect2(Vector2(r.position.x + LABEL_W + 20.0, r.position.y), Vector2(W - LABEL_W - 40.0, r.size.y))


func _row_at(p: Vector2) -> int:
	for i in rows.size():
		if not rows[i].has("band") and _row_rect(i).has_point(p):
			return i
	return -1


# ------------------------------------------------------------------ values
func _index(row: Dictionary) -> int:
	var v: Variant = GameSettings.get_value(row.key)
	if row.get("by_name", false):
		return maxi(0, (row.options as PackedStringArray).find(String(v)))
	return int(v)


func _fraction(row: Dictionary) -> float:
	return inverse_lerp(row.min, row.max, float(GameSettings.get_value(row.key)))


func _step(i: int, dir: int) -> void:
	var row: Dictionary = rows[i]
	match row.kind:
		"choice":
			var n: int = row.options.size()
			if row.get("levels", false):
				n = 4    # "Tuỳ Chỉnh" is shown, never picked
			var cur := _index(row)
			var nxt := clampi((cur if cur < n else 2) + dir, 0, n - 1)
			if row.get("by_name", false):
				GameSettings.set_value(row.key, row.options[nxt])
			else:
				GameSettings.set_value(row.key, nxt)
		"slider":
			var v := clampf(float(GameSettings.get_value(row.key)) + dir * float(row.step), row.min, row.max)
			GameSettings.set_value(row.key, snappedf(v, row.step))
		"toggle":
			GameSettings.set_value(row.key, not bool(GameSettings.get_value(row.key)))


func _set_slider_from(i: int, x: float) -> void:
	var row: Dictionary = rows[i]
	var c := _control_rect(i)
	var track := Rect2(c.position + Vector2(34, 0), Vector2(c.size.x - 68, c.size.y))
	var f := clampf((x - track.position.x) / track.size.x, 0.0, 1.0)
	GameSettings.set_value(row.key, snappedf(lerpf(row.min, row.max, f), row.step))


# ------------------------------------------------------------------ input
func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		_hover = _row_at(event.position)
		if _hover >= 0:
			selected = _hover
		if _drag >= 0:
			_set_slider_from(_drag, event.position.x)
		accept_event()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if not event.pressed:
			_drag = -1
			return
		var i := _row_at(event.position)
		if i < 0:
			return
		selected = i
		var c := _control_rect(i)
		var row: Dictionary = rows[i]
		if row.kind == "toggle":
			_step(i, 1)
		elif event.position.x < c.position.x + 34:
			_step(i, -1)
		elif event.position.x > c.end.x - 34:
			_step(i, 1)
		elif row.kind == "slider":
			_drag = i
			_set_slider_from(i, event.position.x)
		elif row.kind == "choice":
			_step(i, 1)
		accept_event()


func _unhandled_key_input(event: InputEvent) -> void:
	_nav(event)


func _input(event: InputEvent) -> void:
	if event is InputEventJoypadButton or event is InputEventJoypadMotion:
		_nav(event)


func _nav(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_up") or event.is_action_pressed("move_forward"):
		_move(-1)
	elif event.is_action_pressed("ui_down") or event.is_action_pressed("move_back"):
		_move(1)
	elif event.is_action_pressed("ui_left") or event.is_action_pressed("move_left"):
		_step(selected, -1)
	elif event.is_action_pressed("ui_right") or event.is_action_pressed("move_right"):
		_step(selected, 1)
	elif event is InputEventKey and event.pressed and event.physical_keycode == KEY_R:
		GameSettings.reset()
	else:
		return
	get_viewport().set_input_as_handled()


func _move(dir: int) -> void:
	var i := selected
	for k in rows.size():
		i = wrapi(i + dir, 0, rows.size())
		if not rows[i].has("band"):
			selected = i
			return


# ------------------------------------------------------------------ drawing
func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.0, 0.01, 0.02, 0.55))
	var p := _panel_rect()
	MenuKit.panel(self, p)
	MenuKit.text(self, p.position + Vector2(0, -12), "CÀI ĐẶT  ·  ĐỒ HOẠ", 26, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_CENTER, W, "xbold")
	for i in rows.size():
		var row: Dictionary = rows[i]
		var r := _row_rect(i)
		if row.has("band"):
			MenuKit.header(self, Rect2(r.position + Vector2(0, 6), Vector2(r.size.x, r.size.y)), row.band)
			continue
		if i == selected:
			draw_rect(Rect2(r.position + Vector2(8, 3), r.size - Vector2(16, 6)), MenuKit.HOVER)
		MenuKit.text(self, Vector2(r.position.x, r.position.y + r.size.y * 0.5 + 7), row.label, 18,
			MenuKit.VALUE if i == selected else MenuKit.LABEL, HORIZONTAL_ALIGNMENT_RIGHT, LABEL_W)
		var c := _control_rect(i)
		var mid := c.position.y + c.size.y * 0.5
		if row.kind != "toggle":
			MenuKit.chevron(self, Vector2(c.position.x + 14, mid), -1)
			MenuKit.chevron(self, Vector2(c.end.x - 14, mid), 1)
		match row.kind:
			"choice":
				var idx := _index(row)
				var label: String = row.options[clampi(idx, 0, row.options.size() - 1)]
				MenuKit.text(self, Vector2(c.position.x, mid + 3), label, 20, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_CENTER, c.size.x, "bold")
				var n: int = 4 if row.get("levels", false) else row.options.size()
				MenuKit.dots(self, Vector2(c.get_center().x, mid + 14), n, idx)
			"slider":
				MenuKit.slider(self, Rect2(c.position + Vector2(34, 0), Vector2(c.size.x - 68, c.size.y)), _fraction(row))
			"toggle":
				MenuKit.toggle(self, Vector2(c.get_center().x, mid), bool(GameSettings.get_value(row.key)))
	var x := p.position.x + 24.0
	var y := p.end.y - 20.0
	x += MenuKit.hint(self, Vector2(x, y), "Esc", "Quay lại")
	x += MenuKit.hint(self, Vector2(x, y), "← →", "Đổi")
	MenuKit.hint(self, Vector2(x, y), "R", "Mặc định")
