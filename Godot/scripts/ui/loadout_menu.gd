class_name LoadoutMenu
extends Control
## The loadout, laid out like Dauntless's: a column of slot glyphs on the left (weapon, head,
## torso, arms, legs, lantern), one dark navy row per slot with the item's icon, its number (the
## weapon's power, an armour piece's resistance) and its element chips, "+X vs" what it is good
## against and "−X vs" what it is poor against, and its cell sockets; the row picked has a bright
## edge and "Đổi Vũ Khí / Cell" under it. Enter (or a click) opens the list to change that slot.
## On the right the slayer turns slowly in what they wear, over the element resistances.
##
## The numbers are this game's own: a weapon's power is its Attack; its element hits +50% against
## the opposing element and −20% against its own (Elements); an armour piece is worth its points
## for its element and against the opposing one (Armour, 0.5% damage each). Cells and perks are
## not in the game yet, so the sockets stay empty (Prismatic: any cell will go in), and there is
## no lantern.

signal closed

const SLOTS := ["weapon", "head", "torso", "arms", "legs", "lantern"]
const GLYPHS := {"weapon": "sword", "head": "helm", "torso": "armor", "arms": "fist", "legs": "boot", "lantern": "orb"}
const ROW_W := 560.0
const ROW_H := 84.0
const GAP := 12.0

var hero: Node3D
var selected := 0
## The list to change the selected slot (-1 = closed) and the line picked in it.
var choosing := false
var pick := 0
var _options: Array = []
var _preview_vp: SubViewport
var _preview_cam: Camera3D
var _spin := 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	if Game.has_screen() and hero:
		_make_preview()


func _exit_tree() -> void:
	if _preview_vp:
		_preview_vp.queue_free()


func _process(delta: float) -> void:
	_spin += delta * 0.35
	_update_preview()
	queue_redraw()


# ------------------------------------------------------------------ the slayer turning in their gear
func _make_preview() -> void:
	_preview_vp = SubViewport.new()
	_preview_vp.size = Vector2i(420, 560)
	_preview_vp.transparent_bg = true
	_preview_vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_preview_vp)
	_preview_cam = Camera3D.new()
	_preview_cam.fov = 30.0
	_preview_cam.cull_mask = HeroModel.PORTRAIT_LAYER
	var env := Environment.new()
	env.background_mode = Environment.BG_CLEAR_COLOR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.7, 0.82)
	env.ambient_light_energy = 0.85
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	_preview_cam.environment = env
	_preview_vp.add_child(_preview_cam)


func _update_preview() -> void:
	if _preview_cam == null or not is_instance_valid(hero):
		return
	var center: Vector3 = hero.global_position + Vector3.UP * 0.95
	# from the front, swinging slowly side to side
	var f: Vector3 = hero.facing
	var a := atan2(f.x, f.z) + 0.5 + sin(_spin) * 0.6
	var dir := Vector3(sin(a), 0.12, cos(a)).normalized()
	var from := center + dir * 4.6
	_preview_cam.global_transform = Transform3D(Basis.looking_at(center - from, Vector3.UP), from)


# ------------------------------------------------------------------ what each row shows
func _armour_slot(slot: String) -> bool:
	return slot in Armour.SLOTS


func _row_item(slot: String) -> Dictionary:
	if slot == "weapon":
		var id: String = hero.weapon_item
		var it := Weapons.item(id)
		var fam: Dictionary = Weapons.FAMILIES[it.family]
		var power := float(PlayerStats.WEAPONS.get(fam.stat, PlayerStats.WEAPONS.sword).attack)
		var e: String = it.element
		var chips := []
		if e != "neutral":
			chips = [["+%d%%" % roundi((Elements.STRONG - 1.0) * 100.0), Elements.ALL[e].opposite, true],
				["−%d%%" % roundi((1.0 - Elements.WEAK) * 100.0), e, false]]
		return {"icon": GameIcons.weapon(id, 96), "name": it.name, "sub": "%s · %s" % [fam.name, Elements.display_name(e)],
			"number": roundi(power), "glyph": "fist", "chips": chips, "cells": 2}
	if _armour_slot(slot):
		var set_id := String(hero.armour.get(slot, ""))
		var p := Armour.piece(set_id, slot)
		if p.is_empty():
			return {"empty": true, "name": "Chưa mặc %s" % Armour.SLOT_NAMES[slot]}
		var chips := []
		if p.element != "neutral":
			chips = [["+%d" % int(p.resist), p.element, true], ["−%d" % int(p.resist), Elements.ALL[p.element].opposite, false]]
		return {"icon": GameIcons.armour_piece(slot, p.element, 96), "name": p.name,
			"sub": "Bộ %s · %s" % [Armour.set_of(set_id).name, Elements.display_name(p.element)],
			"number": int(p.resist), "glyph": "shield", "chips": chips, "cells": 1}
	return {"empty": true, "name": "Đèn Lồng", "sub": "Chưa có đèn lồng"}


func _change_label(slot: String) -> String:
	if slot == "weapon":
		return "Đổi Vũ Khí / Cell"
	if slot == "lantern":
		return "Đèn Lồng (chưa có)"
	return "Đổi %s / Cell" % Armour.SLOT_NAMES[slot]


## The things that can go in a slot: [label, sub, icon, id, worn?].
func _options_for(slot: String) -> Array:
	var out := []
	if slot == "weapon":
		for id in hero.weapons:
			var it := Weapons.item(id)
			out.append([it.name, "%s · %s" % [Weapons.FAMILIES[it.family].name, Elements.display_name(it.element)],
				GameIcons.weapon(id, 64), id, id == hero.weapon_item])
	elif _armour_slot(slot):
		for set_id in hero.wardrobe:
			var p := Armour.piece(set_id, slot)
			if p.is_empty():
				continue
			var sub := "Bộ %s · %s %d" % [Armour.set_of(set_id).name, Elements.display_name(p.element), int(p.resist)]
			out.append([p.name, sub, GameIcons.armour_piece(slot, p.element, 64), set_id, String(hero.armour.get(slot, "")) == set_id])
	return out


func _open_choice() -> void:
	var slot: String = SLOTS[selected]
	_options = _options_for(slot)
	if _options.is_empty():
		return
	choosing = true
	pick = 0
	for i in _options.size():
		if _options[i][4]:
			pick = i


func _choose(i: int) -> void:
	var slot: String = SLOTS[selected]
	var id: String = _options[i][3]
	if slot == "weapon":
		hero.equip_weapon(id)
	else:
		hero.wear_piece(slot, id)
	_options = _options_for(slot)


# ------------------------------------------------------------------ layout
func _origin() -> Vector2:
	return Vector2(maxf(40.0, size.x * 0.5 - 640.0), maxf(90.0, size.y * 0.5 - 330.0))


func _row_rect(i: int) -> Rect2:
	var o := _origin()
	var extra := 22.0 if i > selected else 0.0
	return Rect2(o + Vector2(70, i * (ROW_H + GAP) + extra), Vector2(ROW_W, ROW_H))


func _list_rect() -> Rect2:
	var o := _origin()
	return Rect2(o + Vector2(70 + ROW_W + 24, 0), Vector2(380, 6 * (ROW_H + GAP)))


func _option_rect(i: int) -> Rect2:
	var l := _list_rect()
	return Rect2(l.position + Vector2(10, 46 + i * 58), Vector2(l.size.x - 20, 52))


# ------------------------------------------------------------------ input
func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		if choosing:
			for i in _options.size():
				if _option_rect(i).has_point(event.position):
					pick = i
		else:
			for i in SLOTS.size():
				if _row_rect(i).has_point(event.position):
					selected = i
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if choosing:
			for i in _options.size():
				if _option_rect(i).has_point(event.position):
					_choose(i)
					accept_event()
					return
		for i in SLOTS.size():
			if _row_rect(i).has_point(event.position):
				selected = i
				_open_choice()
				accept_event()
				return
		choosing = false
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		choosing = false


func _unhandled_key_input(event: InputEvent) -> void:
	var up := event.is_action_pressed("ui_up") or event.is_action_pressed("move_forward")
	var down := event.is_action_pressed("ui_down") or event.is_action_pressed("move_back")
	if choosing:
		if up:
			pick = wrapi(pick - 1, 0, _options.size())
		elif down:
			pick = wrapi(pick + 1, 0, _options.size())
		elif event.is_action_pressed("ui_accept"):
			_choose(pick)
		elif event.is_action_pressed("ui_left") or event.is_action_pressed("move_left"):
			choosing = false
		else:
			return
	else:
		if up:
			selected = wrapi(selected - 1, 0, SLOTS.size())
		elif down:
			selected = wrapi(selected + 1, 0, SLOTS.size())
		elif event.is_action_pressed("ui_accept") or event.is_action_pressed("ui_right") or event.is_action_pressed("move_right"):
			_open_choice()
		else:
			return
	get_viewport().set_input_as_handled()


func _input(event: InputEvent) -> void:
	# Esc closes the list before the menu
	if choosing and event.is_action_pressed("pause"):
		choosing = false
		get_viewport().set_input_as_handled()


# ------------------------------------------------------------------ drawing
func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.0, 0.01, 0.02, 0.6))
	if hero == null or not is_instance_valid(hero):
		return
	var o := _origin()
	MenuKit.text(self, o + Vector2(70, -30), "TRANG BỊ", 34, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
	var total := 0
	for slot in Armour.SLOTS:
		var p := Armour.piece(String(hero.armour.get(slot, "")), slot)
		if not p.is_empty():
			total += int(p.resist)
	var w_item := _row_item("weapon")
	_coin(o + Vector2(ROW_W + 10, -42), "fist", str(w_item.number), "SỨC MẠNH")
	_coin(o + Vector2(ROW_W - 120, -42), "shield", str(total), "KHÁNG")
	# the slot glyphs down the left
	draw_rect(Rect2(o + Vector2(0, -6), Vector2(56, 6 * (ROW_H + GAP) + 28)), Color(0.03, 0.05, 0.07, 0.7))
	for i in SLOTS.size():
		var r := _row_rect(i)
		var g := GameIcons.icon("glyph:" + SLOTS[i], [[GLYPHS[SLOTS[i]], "main", 50, 50, 0.85, 0]], "blade", 64)
		if g:
			draw_texture_rect(g, Rect2(Vector2(o.x + 8, r.position.y + r.size.y * 0.5 - 20), Vector2(40, 40)), false,
				Color(1, 1, 1, 0.9 if i == selected else 0.4))
		_row(i, r)
	if choosing:
		_draw_list()
	else:
		_draw_summary()
	# the consumables, bottom right as on the Dauntless loadout
	var cx := size.x - 330.0
	for k in 4:
		var box := Rect2(Vector2(cx + k * 72, size.y - 130), Vector2(62, 92))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.02, 0.03, 0.05, 0.6)
		sb.set_corner_radius_all(9)
		sb.border_color = Color(1, 1, 1, 0.15)
		sb.set_border_width_all(1)
		draw_style_box(sb, box)
		if k < Hero.POTIONS.size():
			var id: String = Hero.POTIONS[k]
			var tex := GameIcons.item(id, 96)
			if tex:
				draw_texture_rect(tex, Rect2(box.position + Vector2(4, 8), Vector2(54, 54)), false)
			MenuKit.text(self, Vector2(box.position.x, box.end.y - 10), "×%d" % hero.inventory.count(id), 18, MenuKit.VALUE,
				HORIZONTAL_ALIGNMENT_CENTER, box.size.x, "xbold")
	var y := size.y - 24.0
	var x := o.x + 70.0
	x += MenuKit.hint(self, Vector2(x, y), "Enter", "Đổi")
	x += MenuKit.hint(self, Vector2(x, y), "↑ ↓", "Chọn ô")
	MenuKit.hint(self, Vector2(x, y), "Esc", "Quay lại")


## A coin badge in the header: a glyph, a number, a word under it.
func _coin(at: Vector2, glyph: String, number: String, word: String) -> void:
	draw_circle(at + Vector2(18, 18), 18, Color(0.12, 0.1, 0.06, 0.9))
	draw_arc(at + Vector2(18, 18), 18, 0, TAU, 32, MenuKit.GOLD, 2.0, true)
	var g := GameIcons.icon("glyph:" + glyph, [[glyph, "main", 50, 50, 0.8, 0]], "holy", 48)
	if g:
		draw_texture_rect(g, Rect2(at + Vector2(6, 6), Vector2(24, 24)), false)
	MenuKit.text(self, at + Vector2(42, 22), number, 26, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
	MenuKit.text(self, at + Vector2(42, 38), word, 13, MenuKit.LABEL)


func _row(i: int, r: Rect2) -> void:
	var slot: String = SLOTS[i]
	var on := i == selected
	var sb := StyleBoxFlat.new()
	sb.bg_color = MenuKit.NAVY if not on else Color(0.12, 0.17, 0.25, 0.97)
	sb.set_corner_radius_all(6)
	sb.border_color = Color(0.92, 0.95, 1.0, 0.95) if on else MenuKit.NAVY_EDGE
	sb.set_border_width_all(2 if on else 1)
	draw_style_box(sb, r)
	var it := _row_item(slot)
	# the upgrade chevrons on the left edge
	for k in 2:
		draw_polyline(PackedVector2Array([Vector2(r.position.x + 10, r.position.y + 34 + k * 10), Vector2(r.position.x + 16, r.position.y + 28 + k * 10),
			Vector2(r.position.x + 22, r.position.y + 34 + k * 10)]), Color(1, 1, 1, 0.35), 2.0, true)
	if it.get("empty", false):
		MenuKit.text(self, Vector2(r.position.x + 110, r.position.y + 40), it.name, 20, MenuKit.DIM, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
		if it.has("sub"):
			MenuKit.text(self, Vector2(r.position.x + 110, r.position.y + 62), it.sub, 15, MenuKit.DIM)
	else:
		var icon_r := Rect2(r.position + Vector2(34, 6), Vector2(r.size.y - 12, r.size.y - 12))
		draw_rect(icon_r, Color(0, 0, 0, 0.3))
		if it.icon:
			draw_texture_rect(it.icon, icon_r, false)
		var x := icon_r.end.x + 16.0
		var g := GameIcons.icon("glyph:" + String(it.glyph), [[it.glyph, "main", 50, 50, 0.8, 0]], "blade", 48)
		if g:
			draw_texture_rect(g, Rect2(Vector2(x, r.position.y + 14), Vector2(26, 26)), false, Color(1, 1, 1, 0.8))
		MenuKit.text(self, Vector2(x + 30, r.position.y + 36), str(it.number), 30, MenuKit.GOLD if slot == "weapon" else MenuKit.VALUE,
			HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		MenuKit.text(self, Vector2(x, r.position.y + 62), it.name, 16, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, 210, "bold")
		MenuKit.text(self, Vector2(x, r.position.y + 78), it.sub, 13, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, 210)
		# element chips: "+X vs" then "−X vs"
		var cy := r.position.y + 14.0
		for chip in it.chips:
			var cr := Rect2(Vector2(r.position.x + 330, cy), Vector2(118, 26))
			draw_rect(cr, Color(0, 0, 0, 0.35))
			draw_rect(Rect2(cr.position, Vector2(3, cr.size.y)), MenuKit.GOOD if chip[2] else MenuKit.BAD)
			MenuKit.text(self, cr.position + Vector2(10, 19), "%s vs" % chip[0], 17, MenuKit.GOOD if chip[2] else MenuKit.BAD,
				HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
			var et := GameIcons.element(String(chip[1]), 48)
			if et:
				draw_texture_rect(et, Rect2(cr.end - Vector2(28, 25), Vector2(24, 24)), false)
			cy += 30.0
	# cell sockets: empty Prismatic hexagons
	var n: int = int(it.get("cells", 1)) if not it.get("empty", false) or slot == "lantern" else 0
	for k in n:
		var c := Vector2(r.end.x - 34 - k * 46, r.get_center().y)
		var hexa := PackedVector2Array()
		for a in 7:
			hexa.append(c + Vector2.from_angle(TAU * a / 6.0 + PI / 6.0) * 18.0)
		draw_colored_polygon(hexa, Color(0.17, 0.06, 0.24, 0.9))
		draw_polyline(hexa, Color(0.73, 0.0, 1.0, 0.7), 2.0, true)
		var star := GameIcons.icon("glyph:cellstar", [["star", "main", 50, 50, 0.7, 0]], "dark", 32)
		if star:
			draw_texture_rect(star, Rect2(c - Vector2(10, 10), Vector2(20, 20)), false, Color(1, 1, 1, 0.35))
	if on:
		MenuKit.text(self, Vector2(r.position.x, r.end.y + 17), _change_label(slot), 15, MenuKit.VALUE,
			HORIZONTAL_ALIGNMENT_CENTER, r.size.x, "bold")


func _draw_list() -> void:
	var l := _list_rect()
	MenuKit.panel(self, l)
	MenuKit.header(self, Rect2(l.position, Vector2(l.size.x, 36)), _change_label(SLOTS[selected]).split(" /")[0])
	for i in _options.size():
		var r := _option_rect(i)
		var opt: Array = _options[i]
		var on := i == pick
		if on:
			draw_rect(r, MenuKit.HOVER)
			draw_rect(r, Color(1, 1, 1, 0.6), false, 1.5)
		if opt[2]:
			draw_texture_rect(opt[2], Rect2(r.position + Vector2(6, 4), Vector2(44, 44)), false)
		MenuKit.text(self, r.position + Vector2(60, 22), opt[0], 18, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 120, "bold")
		MenuKit.text(self, r.position + Vector2(60, 42), opt[1], 14, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 120)
		if opt[4]:
			MenuKit.text(self, r.position + Vector2(0, 32), "ĐANG DÙNG", 13, MenuKit.CYAN, HORIZONTAL_ALIGNMENT_RIGHT, r.size.x - 10, "bold")


## The slayer in their gear, and what the armour resists and what it is weak to.
func _draw_summary() -> void:
	var l := _list_rect()
	if _preview_vp:
		draw_texture_rect(_preview_vp.get_texture(), Rect2(l.position + Vector2(-20, -40), Vector2(420, 560)), false)
	var box := Rect2(Vector2(l.position.x, l.end.y - 150), Vector2(l.size.x, 170))
	MenuKit.panel(self, box, Color(0.075, 0.1, 0.13, 0.85))
	MenuKit.header(self, Rect2(box.position, Vector2(box.size.x, 34)), "Kháng Nguyên Tố")
	var pts := Armour.points(hero.armour)
	var i := 0
	for e in ["blaze", "frost", "shock", "terra", "radiant", "umbral"]:
		var at := box.position + Vector2(18 + (i % 3) * 120, 52 + (i / 3) * 52)
		var et := GameIcons.element(e, 48)
		if et:
			draw_texture_rect(et, Rect2(at, Vector2(30, 30)), false)
		var v := int(pts.get(e, 0))
		var col := MenuKit.GOOD if v > 0 else (MenuKit.BAD if v < 0 else MenuKit.DIM)
		MenuKit.text(self, at + Vector2(36, 16), ("+%d" % v) if v > 0 else str(v), 20, col, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		var pct := roundi((1.0 - Armour.multiplier(pts, e)) * 100.0)
		MenuKit.text(self, at + Vector2(36, 32), ("−%d%% sát thương" % pct) if pct > 0 else (("+%d%% sát thương" % -pct) if pct < 0 else "—"),
			12, MenuKit.LABEL)
		i += 1
