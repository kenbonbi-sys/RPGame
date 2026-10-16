class_name TalkMenu
extends Control
## Talking to one of the townsfolk (Npc): their name and title, what they say in a box along the
## bottom, and what they offer as a column of slanted buttons on the right, the way Dauntless
## frames a talk in Ramsgate. The camera turns to them and they to the slayer. Some offers open
## a page in the middle: Granny Strega's shelf, Dr. Priyani's behemoth notes, Gaius's map,
## Bulvar's weapon lessons, Kat's slayer's path; the smiths open the loadout.

signal closed

const BEASTS := [
	{"name": "Gấu Ma Rừng Già", "where": "Hang Gấu Ma", "element": "terra", "level": "Colossus · 3200 máu",
		"parts": "Đầu 640 · bốn chân, mỗi chân 400", "notes": "Cúi đầu lấy đà rồi Húc: một đòn nặng vào đầu lúc đó làm nó ngã. Đâm điểm yếu ở bắp chân sau trái làm nó quỵ gối."},
	{"name": "Slime Rêu", "where": "Rừng Thì Thầm", "element": "terra", "level": "Cấp 2 · 60 máu",
		"parts": "—", "notes": "Thụp xuống, rung lên một nhịp rồi lao tới. Lướt xuyên qua nó đúng lúc nó lao."},
	{"name": "Nấm Độc", "where": "Rừng Thì Thầm", "element": "umbral", "level": "Cấp 3 · 48 máu · yếu Lửa",
		"parts": "—", "notes": "Ngửa ra sau gom bào tử rồi phun. Bào tử làm chậm. Áp sát thì nó lùi."},
]
const PLACES := [["Làng Lá Xanh", Vector2(18, 20)], ["Hang Gấu Ma", Vector2(84, 45)], ["Đầm Lầy Sương Mù", Vector2(150, 30)],
	["Thảo Nguyên Gió", Vector2(50, 95)], ["Hang Pha Lê", Vector2(168, 64)]]
## Kat's path: Dauntless's main quests by the level that opens them.
const PATH := [["Thăng Cấp Chiến Trường", 10, "Đạt cấp 10."], ["Dẫn Đường", 30, "Đạt cấp 30."],
	["Thành Công Nhờ Chuẩn Bị", 40, "Đạt cấp 40: mở thuốc và lựu đạn cao cấp."]]

var npc: Npc
var page := "main"
var line := ""
var selected := 0
var _rows: Array = []      # [label, action]
var _hover_item := -1


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	if npc == null:
		return
	line = String(npc.data.greet)
	npc.begin_talk()
	var hero := Game.hero
	if is_instance_valid(hero):
		var to: Vector3 = npc.global_position - hero.global_position
		to.y = 0.0
		if to.length() > 0.1:
			hero.facing = to.normalized()
	var cam := Game.cam()
	if cam:
		cam.look_at_point(npc.head_position())
	_build_rows()


func _exit_tree() -> void:
	if npc and is_instance_valid(npc):
		npc.end_talk()


func _process(_delta: float) -> void:
	queue_redraw()


func _build_rows() -> void:
	_rows = []
	if page == "main":
		for s in npc.data.services:
			_rows.append(s)
		_rows.append(["Kể về bản thân", "about"])
		_rows.append(["Tạm biệt", "leave"])
	else:
		_rows.append(["Quay lại", "back"])
	selected = clampi(selected, 0, _rows.size() - 1)


func _do(action: String) -> void:
	var hero := Game.hero as Hero
	match action:
		"leave":
			closed.emit()
		"back":
			page = "main"
			line = String(npc.data.greet)
			selected = 0
		"about":
			line = String(npc.data.about)
		"advice":
			line = _advice(hero)
		"cells":
			line = "Hàng chưa về. Khi có, ngươi sẽ biết.  (Cell và perk chưa có trong game.)"
		"grenades":
			line = "Lựu đạn còn trên tàu. Cập bến xong ta sẽ chia cho ngươi.  (Lựu đạn chưa có trong game.)"
		"repeater":
			if hero and hero.weapons.has("w_sung_hu_anh"):
				hero.equip_weapon("w_sung_hu_anh")
				line = "Súng Hư Ảnh. Bóp cò nhẹ thôi, giữ chuột phải để tụ một phát xuyên giáp. Đừng bắn vào tàu của ta."
			else:
				line = "Ngươi chưa có súng nào. Đợi ta mở xưởng."
		"dummies":
			Game.log("Bulvar: đánh hình nộm ở bãi tập, bảng trên đầu ghi tổng sát thương và DPS.", Color(1.0, 0.8, 0.45), "kill")
			closed.emit()
		"shop", "beasts", "map", "weapons", "path":
			page = action
			selected = 0
			line = {"shop": "Chọn đi, bà không bán chịu.", "beasts": "Đây là những gì tôi biết. Đọc kỹ, đời ngươi phụ thuộc vào nó.",
				"map": "Đây là mọi thứ tôi đã vẽ được. Chỗ trống là chỗ chưa ai sống sót trở về.",
				"weapons": "Bảy loại vũ khí. Chọn cái hợp với ngươi, rồi tập tới khi nó thành tay ngươi.",
				"path": "Đây là con đường của một thợ săn. Đi tới đâu, ta đánh dấu tới đó."}[action]
		_:
			if action.begins_with("loadout:") and Menus.current:
				Menus.current.open("loadout", "talk", int(action.get_slice(":", 1)), npc)
				return
	_build_rows()


func _advice(hero: Hero) -> String:
	var bear: Node = Game.colossi[0] if not Game.colossi.is_empty() and is_instance_valid(Game.colossi[0]) else null
	var lv: int = hero.level if hero else 1
	if bear and bear.get("dead"):
		return "Gấu Ma nằm xuống rồi, nhưng behemoth luôn quay lại. Lên cấp 10 rồi quay về gặp ta."
	if lv < 3:
		return "Ra bãi slime phía đông luyện tay đã. Thấy con slime thụp xuống rung rung là nó sắp lao: lướt xuyên qua nó, đừng lùi."
	return "Gấu Ma Rừng Già ở Hang phía đông. Nó là Thổ, mang vũ khí Lôi và mặc giáp Thổ. Lúc nó cúi đầu lấy đà để húc, nện một đòn nặng vào đầu."


func _buy(id: String) -> void:
	var hero := Game.hero as Hero
	var price := int(DB.item(id).get("value", 10))
	if hero.inventory.gold < price:
		line = "Không đủ vàng thì đừng nhìn, cưng."
		return
	if not hero.inventory.has_room_for(id):
		line = "Túi ngươi đầy rồi."
		return
	hero.inventory.gold -= price
	hero.inventory.add(id, 1)
	line = "Một %s. Uống khi cần, đừng uống khi vui." % String(DB.item(id).get("displayName", id))
	Game.log("+1 %s" % String(DB.item(id).get("displayName", id)), Color(0.9, 0.9, 0.8), id)


# ------------------------------------------------------------------ layout and input
func _row_rect(i: int) -> Rect2:
	return Rect2(Vector2(size.x - 420, size.y * 0.5 - 60 + i * 58), Vector2(360, 48))


func _page_rect() -> Rect2:
	return Rect2(Vector2(size.x * 0.5 - 520, 70), Vector2(560, size.y - 330))


func _item_rect(i: int) -> Rect2:
	var p := _page_rect()
	return Rect2(p.position + Vector2(16, 54 + i * 76), Vector2(p.size.x - 32, 68))


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		for i in _rows.size():
			if _row_rect(i).has_point(event.position):
				selected = i
		_hover_item = -1
		if page == "shop":
			for i in Townsfolk.SHOP.size():
				if _item_rect(i).has_point(event.position):
					_hover_item = i
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		for i in _rows.size():
			if _row_rect(i).has_point(event.position):
				_do(_rows[i][1])
				accept_event()
				return
		if page == "shop":
			for i in Townsfolk.SHOP.size():
				if _item_rect(i).has_point(event.position):
					_buy(Townsfolk.SHOP[i])
					accept_event()
					return


func _unhandled_key_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_up") or event.is_action_pressed("move_forward"):
		selected = wrapi(selected - 1, 0, _rows.size())
	elif event.is_action_pressed("ui_down") or event.is_action_pressed("move_back"):
		selected = wrapi(selected + 1, 0, _rows.size())
	elif event.is_action_pressed("ui_accept") or event.is_action_pressed("interact"):
		_do(_rows[selected][1])
	else:
		return
	get_viewport().set_input_as_handled()


# ------------------------------------------------------------------ drawing
func _draw() -> void:
	if npc == null or not is_instance_valid(npc):
		return
	# darken the bottom and the right, keep the townsperson in view
	for i in 12:
		var y := size.y - 260 + i * 22.0
		draw_rect(Rect2(0, y, size.x, 22), Color(0, 0.01, 0.02, 0.06 + i * 0.05))
	draw_rect(Rect2(size.x - 470, 0, 470, size.y), Color(0, 0.01, 0.02, 0.35))
	var box := Rect2(Vector2(size.x * 0.5 - 520, size.y - 170), Vector2(1040, 120))
	Hud.slant(self, box, Color(0.04, 0.06, 0.08, 0.86), 18)
	Hud.slant_outline(self, box, Color(1, 1, 1, 0.12), 18, 1.5)
	var nm := String(npc.data.name).to_upper()
	MenuKit.text(self, box.position + Vector2(24, -46), nm, 40, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
	MenuKit.text(self, box.position + Vector2(26, -14), String(npc.data.title).to_upper(), 20, Color(1.0, 0.78, 0.42),
		HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
	draw_multiline_string(Hud.FONT_SEMI, box.position + Vector2(34, 42), line, HORIZONTAL_ALIGNMENT_LEFT, box.size.x - 70, 22, 4,
		Color(0.94, 0.94, 0.9))
	for i in _rows.size():
		var r := _row_rect(i)
		var on := i == selected
		Hud.slant(self, r, Color(0.2, 0.27, 0.34, 0.95) if on else Color(0.05, 0.07, 0.1, 0.85), 12)
		if on:
			Hud.slant_outline(self, r, MenuKit.CYAN, 12, 2.0)
		MenuKit.text(self, Vector2(r.position.x + 26, r.position.y + 32), String(_rows[i][0]).to_upper(), 21,
			MenuKit.VALUE if on else MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
	match page:
		"shop":
			_draw_shop()
		"beasts":
			_draw_beasts()
		"map":
			_draw_map()
		"weapons":
			_draw_weapons()
		"path":
			_draw_path()
	MenuKit.hint(self, Vector2(box.position.x + 24, size.y - 18), "Esc", "Rời đi")


func _page(title: String) -> Rect2:
	var p := _page_rect()
	MenuKit.panel(self, p)
	MenuKit.header(self, Rect2(p.position, Vector2(p.size.x, 36)), title)
	return p


func _draw_shop() -> void:
	var p := _page("Kệ Thuốc Của Bà Strega")
	var hero := Game.hero as Hero
	for i in Townsfolk.SHOP.size():
		var id: String = Townsfolk.SHOP[i]
		var r := _item_rect(i)
		draw_rect(r, MenuKit.HOVER if i == _hover_item else Color(1, 1, 1, 0.03))
		var tex := GameIcons.item(id, 96)
		if tex:
			draw_texture_rect(tex, Rect2(r.position + Vector2(6, 4), Vector2(60, 60)), false)
		var it := DB.item(id)
		MenuKit.text(self, r.position + Vector2(78, 26), String(it.get("displayName", id)), 20, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
		MenuKit.text(self, r.position + Vector2(78, 50), String(it.get("description", "")), 15, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_LEFT, r.size.x - 220)
		MenuKit.text(self, r.position + Vector2(0, 28), "%d vàng" % int(it.get("value", 10)), 20, MenuKit.GOLD, HORIZONTAL_ALIGNMENT_RIGHT, r.size.x - 12, "xbold")
		MenuKit.text(self, r.position + Vector2(0, 52), "đang có %d" % hero.inventory.count(id), 14, MenuKit.LABEL, HORIZONTAL_ALIGNMENT_RIGHT, r.size.x - 12)
	var coin := GameIcons.item("coin", 48)
	if coin:
		draw_texture_rect(coin, Rect2(Vector2(p.position.x + 18, p.end.y - 44), Vector2(30, 30)), false)
	MenuKit.text(self, Vector2(p.position.x + 54, p.end.y - 21), "%d vàng  ·  bấm vào món để mua" % hero.inventory.gold, 18, MenuKit.GOLD,
		HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")


func _draw_beasts() -> void:
	var p := _page("Hồ Sơ Behemoth")
	var y := p.position.y + 58.0
	for b in BEASTS:
		var e: String = b.element
		var opp: String = Elements.ALL[e].opposite
		var et := GameIcons.element(e, 48)
		if et:
			draw_texture_rect(et, Rect2(Vector2(p.position.x + 16, y - 4), Vector2(34, 34)), false)
		MenuKit.text(self, Vector2(p.position.x + 58, y + 20), String(b.name).to_upper(), 22, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		MenuKit.text(self, Vector2(p.position.x + 58, y + 40), "%s · %s · %s" % [Elements.display_name(e), b.level, b.where], 15, Elements.color(e))
		MenuKit.text(self, Vector2(p.position.x + 58, y + 62), "Vũ khí nên dùng: %s (+50%%)  ·  tránh: %s (−20%%)  ·  giáp nên mặc: %s" % [
			Elements.display_name(opp), Elements.display_name(e), Elements.display_name(e)], 15, MenuKit.GOOD)
		MenuKit.text(self, Vector2(p.position.x + 58, y + 82), "Bộ phận phá được: " + String(b.parts), 15, MenuKit.LABEL)
		draw_multiline_string(Hud.FONT_SEMI, Vector2(p.position.x + 58, y + 104), String(b.notes), HORIZONTAL_ALIGNMENT_LEFT,
			p.size.x - 80, 15, 2, Color(0.86, 0.88, 0.9))
		y += 150.0


func _draw_map() -> void:
	var p := _page("Bản Đồ Của Gaius")
	var tex: Texture2D = load("res://world/map_preview.png")
	var area := Rect2(p.position + Vector2(16, 50), p.size - Vector2(32, 66))
	var aspect := WorldMap.SIZE_U / WorldMap.SIZE_V
	var w := minf(area.size.x, area.size.y * aspect)
	var r := Rect2(area.position + (area.size - Vector2(w, w / aspect)) * 0.5, Vector2(w, w / aspect))
	draw_texture_rect(tex, r, false, Color(1.0, 0.95, 0.82))
	draw_rect(r, Color(0.6, 0.5, 0.3), false, 2.0)
	var to_px := func(m: Vector2) -> Vector2:
		return r.position + Vector2(m.x / WorldMap.SIZE_U * r.size.x, (1.0 - m.y / WorldMap.SIZE_V) * r.size.y)
	for place in PLACES:
		var at: Vector2 = to_px.call(WorldMap.tile(place[1]))
		draw_circle(at, 4.0, Color(0.15, 0.1, 0.05))
		MenuKit.text(self, at + Vector2(7, 5), String(place[0]), 15, Color(0.15, 0.1, 0.05), HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
	var hero := Game.hero
	if is_instance_valid(hero):
		var hp: Vector2 = to_px.call(WorldMap.map_of(hero.global_position))
		var f: Vector3 = hero.facing
		var dir := Vector2(f.x, f.z).normalized()
		draw_colored_polygon(PackedVector2Array([hp + dir * 9.0, hp + dir.orthogonal() * 5.0 - dir * 4.0, hp - dir.orthogonal() * 5.0 - dir * 4.0]),
			Color(0.9, 0.15, 0.1))
		MenuKit.text(self, hp + Vector2(8, -6), "Bạn", 14, Color(0.9, 0.15, 0.1), HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")


func _draw_weapons() -> void:
	var p := _page("Bảy Loại Vũ Khí")
	var y := p.position.y + 54.0
	for fam_id in Weapons.FAMILIES:
		var fam: Dictionary = Weapons.FAMILIES[fam_id]
		var kind := "chém (+50% bộ phận)" if fam.get("cut", false) else ("đâm (+25% bộ phận)" if fam.get("pierce", false) else "cùn (trấn áp ×1,6)")
		var icon := GameIcons.icon("family:" + fam_id, GameIcons.FAMILY_ICONS.get(fam_id, GameIcons.FAMILY_ICONS.sword), "blade", 64)
		if icon:
			draw_texture_rect(icon, Rect2(Vector2(p.position.x + 14, y - 6), Vector2(40, 40)), false)
		MenuKit.text(self, Vector2(p.position.x + 62, y + 12), String(fam.name).to_upper(), 20, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		MenuKit.text(self, Vector2(p.position.x + 62, y + 32), "Đòn nặng: %s  ·  %s" % [fam.heavy.name, kind], 15, MenuKit.LABEL)
		y += 50.0


func _draw_path() -> void:
	var p := _page("Con Đường Thợ Săn")
	var hero := Game.hero as Hero
	var lv: int = hero.level if hero else 1
	var y := p.position.y + 64.0
	for step in PATH:
		var done: bool = lv >= int(step[1])
		draw_rect(Rect2(Vector2(p.position.x + 18, y - 14), Vector2(22, 22)), MenuKit.GOOD if done else Color(1, 1, 1, 0.12))
		draw_rect(Rect2(Vector2(p.position.x + 18, y - 14), Vector2(22, 22)), Color(1, 1, 1, 0.4), false, 1.0)
		MenuKit.text(self, Vector2(p.position.x + 54, y + 4), String(step[0]).to_upper(), 21, MenuKit.VALUE if done else MenuKit.LABEL,
			HORIZONTAL_ALIGNMENT_LEFT, -1, "xbold")
		MenuKit.text(self, Vector2(p.position.x + 54, y + 26), "%s  (cấp hiện tại: %d)" % [step[2], lv], 15, MenuKit.LABEL)
		y += 66.0
