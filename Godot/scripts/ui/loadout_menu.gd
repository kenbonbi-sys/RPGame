class_name LoadoutMenu
extends Control
## The loadout as Dauntless shows it. The camera swings round in front of the slayer, who stands
## in the middle of the screen in the world as it is (no dimming). Down the left, over a dark wash:
## the "Trang Bị" title with its emblem, a column of slot glyphs, and one dark navy row per slot
## (weapon, head, torso, arms, legs, lantern) with the piece itself as a picture (ItemStudio), its
## number (the weapon's power, a piece's resistance), its element chips "+X vs" and "−X vs", and its
## cell sockets; the row picked has a bright edge and "Đổi Vũ Khí / Cell" under it. Enter or a click
## opens the list to change that slot. On the right, the summary panel under a purple header (where
## Dauntless sums the perks: here the weapon's element and the armour's resistances), and the
## consumables; along the bottom a bar with the buttons.
##
## The numbers are this game's own: a weapon's power is its Attack; its element hits +50% against
## the opposing element and −20% against its own (Elements); an armour piece is worth its points
## for its element and against the opposing one (Armour, 0.5% damage each). Cells and perks are
## not in the game yet, so the sockets stay empty (Prismatic: any cell will go in), and there is
## no lantern.

signal closed

const SLOTS := ["weapon", "head", "torso", "arms", "legs", "lantern"]
const GLYPHS := {"weapon": "axe", "head": "helm", "torso": "armor", "arms": "fist", "legs": "boot", "lantern": "orb"}
const ROW_W := 520.0
const ROW_H := 86.0
const GAP := 10.0
const PURPLE := Color(0.45, 0.16, 0.72)

var hero: Node3D
var selected := 0
## The list to change the selected slot is open, and the line picked in it.
var choosing := false
var pick := 0
var _options: Array = []
var _studio: ItemStudio


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	_studio = ItemStudio.new()
	add_child(_studio)
	var cam := Game.cam()
	if cam:
		cam.showcase = true


func _exit_tree() -> void:
	var cam := Game.cam()
	if cam:
		cam.showcase = false


func _process(_delta: float) -> void:
	queue_redraw()


# ------------------------------------------------------------------ what each row shows
func _armour_slot(slot: String) -> bool:
	return slot in Armour.SLOTS


func _picture_weapon(id: String) -> Texture2D:
	var tex := _studio.picture("w:" + id, ItemStudio.weapon_builder(id))
	return tex if tex else GameIcons.weapon(id, 96)


func _picture_piece(slot: String, set_id: String) -> Texture2D:
	var model := hero.get("model") as HeroModel
	var tex: Texture2D = null
	# a bare head (Krolachi has no helm) is better told by the glyph
	if model and (ArmourArt.drawn(set_id) or slot != "head"):
		tex = _studio.picture("a:%s:%s" % [slot, set_id], ItemStudio.armour_builder(model, slot, set_id))
	return tex if tex else GameIcons.armour_piece(slot, String(Armour.set_of(set_id).element), 96)


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
		return {"icon": _picture_weapon(id), "name": it.name, "number": roundi(power), "word": "SỨC MẠNH",
			"glyph": "fist", "chips": chips, "cells": 2}
	if _armour_slot(slot):
		var set_id := String(hero.armour.get(slot, ""))
		var p := Armour.piece(set_id, slot)
		if p.is_empty():
			return {"empty": true, "name": "Chưa mặc %s" % Armour.SLOT_NAMES[slot], "cells": 0}
		var chips := []
		if p.element != "neutral":
			chips = [["+%d" % int(p.resist), p.element, true], ["−%d" % int(p.resist), Elements.ALL[p.element].opposite, false]]
		return {"icon": _picture_piece(slot, set_id), "name": p.name, "number": int(p.resist), "word": "KHÁNG",
			"glyph": "shield", "chips": chips, "cells": 1}
	return {"empty": true, "name": "Đèn Lồng", "sub": "Chưa có đèn lồng", "cells": 1}


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
				_picture_weapon(id), id, id == hero.weapon_item])
	elif _armour_slot(slot):
		for set_id in hero.wardrobe:
			var p := Armour.piece(set_id, slot)
			if p.is_empty():
				continue
			var sub := "Bộ %s · %s %d" % [Armour.set_of(set_id).name, Elements.display_name(p.element), int(p.resist)]
			out.append([p.name, sub, _picture_piece(slot, set_id), set_id, String(hero.armour.get(slot, "")) == set_id])
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
	return Vector2(36.0, maxf(96.0, size.y * 0.5 - 330.0))


func _row_rect(i: int) -> Rect2:
	var o := _origin()
	var extra := 20.0 if i > selected else 0.0
	return Rect2(o + Vector2(62, i * (ROW_H + GAP) + extra), Vector2(ROW_W, ROW_H))


func _right_rect() -> Rect2:
	return Rect2(Vector2(size.x - 430, _origin().y - 10), Vector2(396, 470))


func _option_rect(i: int) -> Rect2:
	var l := _right_rect()
	return Rect2(l.position + Vector2(10, 52 + i * 62), Vector2(l.size.x - 20, 56))


func _buttons() -> Array:
	var y := size.y - 50.0
	return [[Rect2(Vector2(size.x - 170, y), Vector2(130, 34)), "QUAY LẠI", "back"],
		[Rect2(Vector2(size.x - 320, y), Vector2(140, 34)), "ĐỔI", "change"]]


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
		for b in _buttons():
			if (b[0] as Rect2).has_point(event.position):
				if b[2] == "back":
					closed.emit()
				else:
					_open_choice()
				accept_event()
				return
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
## A wash of colour across the screen: `col` at x0, fading to nothing at x1.
func _wash(x0: float, x1: float, col: Color) -> void:
	var n := 16
	for i in n:
		var u0 := float(i) / n
		var u1 := float(i + 1) / n
		var a0 := col.a * (1.0 - u0) * (1.0 - u0)
		var a1 := col.a * (1.0 - u1) * (1.0 - u1)
		var xa := lerpf(x0, x1, u0)
		var xb := lerpf(x0, x1, u1)
		draw_polygon(PackedVector2Array([Vector2(xa, 0), Vector2(xb, 0), Vector2(xb, size.y), Vector2(xa, size.y)]),
			PackedColorArray([Color(col, a0), Color(col, a1), Color(col, a1), Color(col, a0)]))


func _draw() -> void:
	if hero == null or not is_instance_valid(hero):
		return
	_wash(0.0, size.x * 0.46, Color(0.03, 0.05, 0.08, 0.82))
	_wash(size.x, size.x * 0.66, Color(0.03, 0.05, 0.08, 0.7))
	var o := _origin()
	# the title with its round emblem, on a steel strip fading to the right
	var strip := Rect2(Vector2(0, o.y - 74), Vector2(ROW_W + 90, 34))
	var n := 12
	for i in n:
		var u0 := float(i) / n
		var u1 := float(i + 1) / n
		draw_polygon(PackedVector2Array([Vector2(strip.size.x * u0, strip.position.y), Vector2(strip.size.x * u1, strip.position.y),
			Vector2(strip.size.x * u1, strip.end.y), Vector2(strip.size.x * u0, strip.end.y)]),
			PackedColorArray([Color(0.55, 0.6, 0.66, 0.85 * (1.0 - u0)), Color(0.55, 0.6, 0.66, 0.85 * (1.0 - u1)),
				Color(0.32, 0.36, 0.42, 0.85 * (1.0 - u1)), Color(0.32, 0.36, 0.42, 0.85 * (1.0 - u0))]))
	var em := Vector2(o.x + 26, strip.get_center().y)
	draw_circle(em, 25, Color(0, 0, 0, 0.6))
	draw_circle(em, 22, Color(0.12, 0.22, 0.4))
	draw_arc(em, 22, 0, TAU, 40, Color(0.75, 0.82, 0.9), 2.0, true)
	var g := GameIcons.icon("glyph:helm", [["helm", "main", 50, 50, 0.8, 0]], "blade", 48)
	if g:
		draw_texture_rect(g, Rect2(em - Vector2(14, 14), Vector2(28, 28)), false)
	MenuKit.text(self, Vector2(o.x + 58, strip.end.y - 8), "Trang Bị", 28, Color(0.98, 0.98, 1.0), HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
	# the slot glyphs down the left
	draw_rect(Rect2(o + Vector2(0, -6), Vector2(52, 6 * (ROW_H + GAP) + 26)), Color(0.02, 0.03, 0.05, 0.75))
	for i in SLOTS.size():
		var r := _row_rect(i)
		var gl := GameIcons.icon("glyph:" + SLOTS[i], [[GLYPHS[SLOTS[i]], "main", 50, 50, 0.85, 0]], "blade", 64)
		if gl:
			draw_texture_rect(gl, Rect2(Vector2(o.x + 7, r.position.y + r.size.y * 0.5 - 19), Vector2(38, 38)), false,
				Color(1, 1, 1, 0.95 if i == selected else 0.45))
		_row(i, r)
	if choosing:
		_draw_list()
	else:
		_draw_summary()
	# consumables over the bottom bar, right
	var cx := size.x - 300.0
	var cy := size.y - 160.0
	var pnl := Rect2(Vector2(cx - 12, cy - 10), Vector2(270, 100))
	MenuKit.panel(self, pnl, Color(0.05, 0.08, 0.12, 0.78))
	for k in 4:
		var box := Rect2(Vector2(cx + k * 64, cy), Vector2(54, 80))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.0, 0.01, 0.02, 0.5)
		sb.set_corner_radius_all(8)
		sb.border_color = Color(1, 1, 1, 0.12)
		sb.set_border_width_all(1)
		draw_style_box(sb, box)
		if k < Hero.POTIONS.size():
			var id: String = Hero.POTIONS[k]
			var tex := GameIcons.item(id, 96)
			if tex:
				draw_texture_rect(tex, Rect2(box.position + Vector2(4, 6), Vector2(46, 46)), false)
			MenuKit.text(self, Vector2(box.position.x, box.end.y - 8), str(hero.inventory.count(id)), 18, MenuKit.VALUE,
				HORIZONTAL_ALIGNMENT_CENTER, box.size.x, "xbold")
	# the bottom bar with its buttons
	var bar := Rect2(Vector2(0, size.y - 64), Vector2(size.x, 64))
	draw_rect(bar, Color(0.03, 0.05, 0.07, 0.82))
	draw_line(Vector2(0, bar.position.y), Vector2(size.x, bar.position.y), Color(0.6, 0.66, 0.72, 0.6), 2.0)
	for b in _buttons():
		var br: Rect2 = b[0]
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.12, 0.16, 0.2, 0.95)
		sb.set_corner_radius_all(3)
		sb.border_color = Color(0.75, 0.8, 0.86, 0.7)
		sb.set_border_width_all(1)
		draw_style_box(sb, br)
		MenuKit.text(self, Vector2(br.position.x, br.position.y + 23), b[1], 17, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_CENTER, br.size.x, "bold")
	var x := o.x
	x += MenuKit.hint(self, Vector2(x, size.y - 24), "Enter", "Đổi")
	x += MenuKit.hint(self, Vector2(x, size.y - 24), "↑ ↓", "Chọn ô")
	MenuKit.hint(self, Vector2(x, size.y - 24), "Esc", "Quay lại")


func _row(i: int, r: Rect2) -> void:
	var slot: String = SLOTS[i]
	var on := i == selected
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.1, 0.14, 0.21, 0.94) if not on else Color(0.13, 0.18, 0.27, 0.97)
	sb.set_corner_radius_all(5)
	sb.border_color = Color(0.95, 0.97, 1.0, 0.95) if on else Color(0.3, 0.37, 0.46, 0.9)
	sb.set_border_width_all(2 if on else 1)
	draw_style_box(sb, r)
	var it := _row_item(slot)
	# the two upgrade chevrons on the left edge
	for k in 2:
		draw_polyline(PackedVector2Array([Vector2(r.position.x + 9, r.position.y + 36 + k * 9), Vector2(r.position.x + 15, r.position.y + 30 + k * 9),
			Vector2(r.position.x + 21, r.position.y + 36 + k * 9)]), Color(1, 1, 1, 0.4), 2.0, true)
	if it.get("empty", false):
		MenuKit.text(self, Vector2(r.position.x + 120, r.position.y + 42), it.name, 20, MenuKit.DIM, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
		if it.has("sub"):
			MenuKit.text(self, Vector2(r.position.x + 120, r.position.y + 64), it.sub, 15, MenuKit.DIM)
	else:
		# the piece itself, on a dark tile
		var tile := Rect2(r.position + Vector2(30, 5), Vector2(r.size.y - 10, r.size.y - 10))
		var ts := StyleBoxFlat.new()
		ts.bg_color = Color(0.03, 0.05, 0.08, 0.9)
		ts.set_corner_radius_all(3)
		draw_style_box(ts, tile)
		if it.icon:
			draw_texture_rect(it.icon, tile.grow(-2), false)
		var x := tile.end.x + 14.0
		var g := GameIcons.icon("glyph:" + String(it.glyph), [[it.glyph, "main", 50, 50, 0.8, 0]], "blade", 48)
		if g:
			draw_texture_rect(g, Rect2(Vector2(x, r.position.y + 16), Vector2(28, 28)), false, Color(1, 1, 1, 0.85))
		MenuKit.text(self, Vector2(x + 32, r.position.y + 42), str(it.number), 34, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		MenuKit.text(self, Vector2(x, r.position.y + 62), it.word, 12, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
		MenuKit.text(self, Vector2(x, r.position.y + 78), it.name, 14, Color(0.86, 0.89, 0.93), HORIZONTAL_ALIGNMENT_LEFT, 190, "semi")
		# element chips: "+X vs" then "−X vs", each with the element's sign
		var cy := r.position.y + 12.0
		for chip in it.chips:
			var cr := Rect2(Vector2(r.position.x + 300, cy), Vector2(104, 28))
			var cs := StyleBoxFlat.new()
			cs.bg_color = Color(0.03, 0.05, 0.08, 0.85)
			cs.set_corner_radius_all(3)
			draw_style_box(cs, cr)
			MenuKit.text(self, cr.position + Vector2(8, 20), "%s vs" % chip[0], 17, MenuKit.GOOD if chip[2] else MenuKit.BAD,
				HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
			var et := GameIcons.element(String(chip[1]), 48)
			if et:
				draw_texture_rect(et, Rect2(cr.end - Vector2(29, 26), Vector2(24, 24)), false)
			cy += 33.0
	# cell sockets: empty Prismatic hexagons in the cell purple
	var cells: int = int(it.get("cells", 0))
	for k in cells:
		var c := Vector2(r.end.x - 32 - k * 44, r.get_center().y)
		var hexa := PackedVector2Array()
		for a in 7:
			hexa.append(c + Vector2.from_angle(TAU * a / 6.0 + PI / 6.0) * 18.0)
		draw_colored_polygon(hexa, Color(0.2, 0.06, 0.3, 0.95))
		draw_polyline(hexa, Color(0.73, 0.0, 1.0, 0.85), 2.0, true)
		var star := GameIcons.icon("glyph:cellstar", [["star", "main", 50, 50, 0.7, 0]], "dark", 32)
		if star:
			draw_texture_rect(star, Rect2(c - Vector2(10, 10), Vector2(20, 20)), false, Color(1, 1, 1, 0.45))
	if on:
		MenuKit.text(self, Vector2(r.position.x, r.end.y + 16), _change_label(slot), 15, MenuKit.VALUE,
			HORIZONTAL_ALIGNMENT_CENTER, r.size.x, "bold")


## A panel under a purple header with a round emblem, as Dauntless's perk summary.
func _purple_panel(r: Rect2, title: String, glyph: String) -> void:
	MenuKit.panel(self, r, Color(0.06, 0.09, 0.13, 0.85))
	var head := Rect2(r.position + Vector2(18, 0), Vector2(r.size.x - 18, 34))
	draw_rect(head, PURPLE)
	draw_rect(Rect2(head.position, Vector2(head.size.x, 12)), Color(1, 1, 1, 0.12))
	var c := Vector2(r.position.x + 20, head.get_center().y)
	draw_circle(c, 24, Color(0.18, 0.06, 0.3))
	draw_arc(c, 24, 0, TAU, 40, Color(0.95, 0.9, 1.0), 2.0, true)
	draw_circle(c, 20, PURPLE.darkened(0.2))
	var g := GameIcons.icon("glyph:" + glyph, [[glyph, "main", 50, 50, 0.8, 0]], "holy", 48)
	if g:
		draw_texture_rect(g, Rect2(c - Vector2(13, 13), Vector2(26, 26)), false)
	MenuKit.text(self, head.position + Vector2(36, 24), title.to_upper(), 18, Color.WHITE, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")


## A card in the summary: a glyph, a title, one or two lines.
func _card(r: Rect2, glyph: String, kind: String, title: String, lines: Array) -> void:
	draw_rect(r, Color(0.1, 0.13, 0.18, 0.9))
	draw_rect(r, Color(0.85, 0.88, 0.92, 0.55), false, 1.0)
	var g := GameIcons.icon("glyph:%s:%s" % [glyph, kind], [[glyph, "main", 50, 50, 0.8, 0]], kind, 48)
	if g:
		draw_texture_rect(g, Rect2(r.position + Vector2(8, r.size.y * 0.5 - 15), Vector2(30, 30)), false)
	MenuKit.text(self, r.position + Vector2(48, 20), title, 17, Color.WHITE, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 56, "bold")
	var y := r.position.y + 37.0
	for l in lines:
		MenuKit.text(self, Vector2(r.position.x + 48, y), String(l), 13, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 56)
		y += 15.0


func _draw_summary() -> void:
	var r := _right_rect()
	_purple_panel(r, "Tổng Kết", "star")
	var y := r.position.y + 48.0
	var it := Weapons.item(String(hero.weapon_item))
	var fam: Dictionary = Weapons.FAMILIES[it.family]
	var e: String = it.element
	var w_lines := ["Đòn nặng: %s" % fam.heavy.name]
	if e != "neutral":
		w_lines.append("+50%% vs %s · −20%% vs %s" % [Elements.display_name(Elements.ALL[e].opposite), Elements.display_name(e)])
	else:
		w_lines.append("Vô hệ: không khắc, không kỵ")
	_card(Rect2(Vector2(r.position.x + 12, y), Vector2(r.size.x - 24, 66)), "sword", GameIcons.ELEMENT_KINDS.get(e, "blade"),
		"%s · %s" % [fam.name, it.name], w_lines)
	y += 74.0
	var pts := Armour.points(hero.armour)
	var shown := 0
	for el in ["blaze", "frost", "shock", "terra", "radiant", "umbral"]:
		var v := int(pts.get(el, 0))
		if v == 0:
			continue
		var pct := roundi(absf(1.0 - Armour.multiplier(pts, el)) * 100.0)
		var title := ("Kháng %s %d" if v > 0 else "Yếu %s %d") % [Elements.display_name(el), absi(v)]
		var line := ("−%d%% sát thương %s" if v > 0 else "+%d%% sát thương %s") % [pct, Elements.display_name(el)]
		_card(Rect2(Vector2(r.position.x + 12, y), Vector2(r.size.x - 24, 52)), String(Elements.ALL[el].icon),
			GameIcons.ELEMENT_KINDS.get(el, "blade"), title, [line])
		y += 60.0
		shown += 1
		if shown >= 5:
			break
	if shown == 0:
		_card(Rect2(Vector2(r.position.x + 12, y), Vector2(r.size.x - 24, 52)), "shield", "blade", "Không kháng hệ",
			["Giáp vô hệ: đòn của hệ nào cũng như nhau"])


func _draw_list() -> void:
	var l := _right_rect()
	_purple_panel(l, _change_label(SLOTS[selected]).split(" /")[0], GLYPHS[SLOTS[selected]])
	for i in _options.size():
		var r := _option_rect(i)
		var opt: Array = _options[i]
		var on := i == pick
		draw_rect(r, Color(0.13, 0.18, 0.26, 0.95) if on else Color(0.08, 0.11, 0.16, 0.85))
		draw_rect(r, Color(1, 1, 1, 0.85) if on else Color(1, 1, 1, 0.12), false, 1.5 if on else 1.0)
		var tile := Rect2(r.position + Vector2(4, 4), Vector2(48, 48))
		draw_rect(tile, Color(0.03, 0.05, 0.08, 0.9))
		# the picture may have been taken since the list opened
		var tex: Texture2D = _picture_weapon(opt[3]) if SLOTS[selected] == "weapon" else _picture_piece(SLOTS[selected], opt[3])
		if tex:
			draw_texture_rect(tex, tile, false)
		MenuKit.text(self, r.position + Vector2(62, 24), opt[0], 17, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 130, "bold")
		MenuKit.text(self, r.position + Vector2(62, 44), opt[1], 13, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 130)
		if opt[4]:
			MenuKit.text(self, r.position + Vector2(0, 32), "ĐANG DÙNG", 12, MenuKit.CYAN, HORIZONTAL_ALIGNMENT_RIGHT, r.size.x - 10, "bold")
