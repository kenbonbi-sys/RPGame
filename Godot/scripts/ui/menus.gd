class_name Menus
extends CanvasLayer
## Every full-screen menu, one at a time: Esc opens the pause menu (Tiếp Tục, Trang Bị, Cài Đặt,
## Thoát Game), B / I the loadout, NPCs open their own. While one is up the hero stands still and
## the mouse is free; the world does not stop (Dauntless never pauses). Esc goes back a step.

static var current: Menus

var page: Control
## Where Esc returns to from the page that is open ("" = back to the game).
var _back := ""
var _back_arg: Variant = null
var _page_name := ""


func _enter_tree() -> void:
	current = self


func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS


func is_open() -> bool:
	return page != null and is_instance_valid(page)


## Opens a page; `back` (and `back_arg`) is where Esc returns to.
func open(page_name: String, back := "", arg: Variant = null, back_arg: Variant = null) -> void:
	_drop_page()
	var p: Control
	match page_name:
		"pause":
			p = PauseMenu.new()
		"settings":
			p = SettingsMenu.new()
		"loadout":
			var l := LoadoutMenu.new()
			l.hero = Game.hero
			if arg != null:
				l.selected = int(arg)
			p = l
		"talk":
			var d := TalkMenu.new()
			d.npc = arg
			p = d
		_:
			return
	_page_name = page_name
	_back = back
	_back_arg = back_arg
	page = p
	add_child(p)
	if p.has_signal("closed"):
		p.closed.connect(back_out)
	if p.has_signal("go"):
		p.go.connect(func(what: String) -> void: open(what, page_name))
	Game.menu_open = true
	_show_hud(false)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


## Esc: one step back (to where the page came from, or to the game).
func back_out() -> void:
	if _back != "":
		open(_back, "", _back_arg)
	else:
		close()


func close() -> void:
	_drop_page()
	_page_name = ""
	_back = ""
	_back_arg = null
	Game.menu_open = false
	_show_hud(true)
	if Game.has_screen() and not Game.has_arg("--autoshot"):
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


## Dauntless clears the HUD away while a menu or a talk is up.
func _show_hud(on: bool) -> void:
	var hud := Game.world.get_node_or_null("HUD") as CanvasLayer if is_instance_valid(Game.world) else null
	if hud:
		hud.visible = on


func _drop_page() -> void:
	if is_open():
		page.queue_free()
	page = null


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("pause"):
		if is_open():
			back_out()
		else:
			open("pause")
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("window_character"):
		if _page_name == "loadout":
			close()
		elif not is_open():
			open("loadout")
		get_viewport().set_input_as_handled()


class PauseMenu:
	extends Control
	## The Esc menu: a column of slanted buttons over the dimmed game.
	signal closed
	signal go(what: String)

	const ITEMS := [["TIẾP TỤC", ""], ["TRANG BỊ", "loadout"], ["CÀI ĐẶT", "settings"], ["THOÁT GAME", "quit"]]
	var selected := 0

	func _ready() -> void:
		set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		mouse_filter = Control.MOUSE_FILTER_STOP

	func _process(_delta: float) -> void:
		queue_redraw()

	func _item_rect(i: int) -> Rect2:
		return Rect2(Vector2(size.x * 0.5 - 170, size.y * 0.5 - 120 + i * 66), Vector2(340, 54))

	func _pick(i: int) -> void:
		var what: String = ITEMS[i][1]
		if what == "":
			closed.emit()
		elif what == "quit":
			get_tree().quit()
		else:
			go.emit(what)

	func _gui_input(event: InputEvent) -> void:
		if event is InputEventMouseMotion:
			for i in ITEMS.size():
				if _item_rect(i).has_point(event.position):
					selected = i
		elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			for i in ITEMS.size():
				if _item_rect(i).has_point(event.position):
					_pick(i)
					accept_event()
					return

	func _unhandled_key_input(event: InputEvent) -> void:
		if event.is_action_pressed("ui_up") or event.is_action_pressed("move_forward"):
			selected = wrapi(selected - 1, 0, ITEMS.size())
		elif event.is_action_pressed("ui_down") or event.is_action_pressed("move_back"):
			selected = wrapi(selected + 1, 0, ITEMS.size())
		elif event.is_action_pressed("ui_accept"):
			_pick(selected)
		else:
			return
		get_viewport().set_input_as_handled()

	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.0, 0.01, 0.02, 0.55))
		var top := _item_rect(0).position + Vector2(0, -70)
		MenuKit.text(self, top, "RỪNG THÌ THẦM", 40, MenuKit.VALUE, HORIZONTAL_ALIGNMENT_CENTER, 340, "xbold")
		for i in ITEMS.size():
			var r := _item_rect(i)
			var on := i == selected
			Hud.slant(self, r, Color(0.2, 0.27, 0.34, 0.95) if on else MenuKit.PANEL, 14)
			if on:
				Hud.slant_outline(self, r, MenuKit.CYAN, 14, 2.0)
			MenuKit.text(self, Vector2(r.position.x, r.position.y + 36), ITEMS[i][0], 26, MenuKit.VALUE if on else MenuKit.LABEL,
				HORIZONTAL_ALIGNMENT_CENTER, r.size.x, "xbold")
