extends Node
## Every key and gamepad button in one place (like GameControls.cs in the Unity game).
## The actions are added at start-up, so the defaults live here and not in project.godot.
## Rebinding later goes through InputMap and is saved to user://controls.cfg.

const KEYS := {
	"move_forward": [KEY_W, KEY_UP],
	"move_back": [KEY_S, KEY_DOWN],
	"move_left": [KEY_A, KEY_LEFT],
	"move_right": [KEY_D, KEY_RIGHT],
	"jump": [KEY_SPACE],
	"dodge": [KEY_SHIFT],
	"look_boss": [KEY_Q],
	"interact": [KEY_F],
	# hold to cling to fur (right mouse is the sword's heavy hit)
	"grip": [KEY_E],
	"pause": [KEY_ESCAPE],
	"debug_overlay": [KEY_F3],
	"screenshot": [KEY_F12],
	# the Unity game's W E R A S skill slots and D (ultimate); Q (the basic attack) is the left mouse button
	"skill_1": [KEY_1],
	"skill_2": [KEY_2],
	"skill_3": [KEY_3],
	"skill_4": [KEY_4],
	"skill_5": [KEY_5],
	"skill_ult": [KEY_R],
	# the Unity game's potions on 1 2 3
	"potion_1": [KEY_Z],
	"potion_2": [KEY_X],
	"potion_3": [KEY_C],
	"window_character": [KEY_B, KEY_I],
	"window_bestiary": [KEY_J],
	"window_spellbook": [KEY_K],
	"switch_weapon": [KEY_V],
	# G for giáp: the next armour set
	"switch_armour": [KEY_G],
	"track_quest": [KEY_TAB],
}

## Skill slots in the order of the Unity game's PlayerSkills.slots (0 = the basic attack).
const SKILL_ACTIONS := ["attack", "skill_1", "skill_2", "skill_3", "skill_4", "skill_5", "skill_ult"]

const MOUSE := {
	"attack": [MOUSE_BUTTON_LEFT],
	"heavy": [MOUSE_BUTTON_RIGHT],
	"look_boss": [MOUSE_BUTTON_MIDDLE],
}

const PAD := {
	"jump": [JOY_BUTTON_A],
	"dodge": [JOY_BUTTON_B],
	"attack": [JOY_BUTTON_X],
	"interact": [JOY_BUTTON_Y],
	"switch_weapon": [JOY_BUTTON_DPAD_RIGHT],
	"switch_armour": [JOY_BUTTON_DPAD_LEFT],
	"grip": [JOY_BUTTON_RIGHT_SHOULDER],
	"look_boss": [JOY_BUTTON_LEFT_SHOULDER],
	"pause": [JOY_BUTTON_START],
}

## action: [axis, direction]
const PAD_AXES := {
	"move_forward": [JOY_AXIS_LEFT_Y, -1.0],
	"move_back": [JOY_AXIS_LEFT_Y, 1.0],
	"move_left": [JOY_AXIS_LEFT_X, -1.0],
	"move_right": [JOY_AXIS_LEFT_X, 1.0],
	"cam_left": [JOY_AXIS_RIGHT_X, -1.0],
	"cam_right": [JOY_AXIS_RIGHT_X, 1.0],
	"cam_up": [JOY_AXIS_RIGHT_Y, -1.0],
	"cam_down": [JOY_AXIS_RIGHT_Y, 1.0],
	"heavy": [JOY_AXIS_TRIGGER_RIGHT, 1.0],
}

## Seconds Shift must be held before a tap (Lướt) turns into sprinting.
const DODGE_TAP_TIME := 0.22


func _enter_tree() -> void:
	for action in KEYS:
		for key in KEYS[action]:
			var e := InputEventKey.new()
			e.physical_keycode = key
			_add(action, e)
	for action in MOUSE:
		for button in MOUSE[action]:
			var e := InputEventMouseButton.new()
			e.button_index = button
			_add(action, e)
	for action in PAD:
		for button in PAD[action]:
			var e := InputEventJoypadButton.new()
			e.button_index = button
			_add(action, e)
	for action in PAD_AXES:
		var e := InputEventJoypadMotion.new()
		e.axis = PAD_AXES[action][0]
		e.axis_value = PAD_AXES[action][1]
		_add(action, e)


func _add(action: StringName, e: InputEvent) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action, 0.2)
	InputMap.action_add_event(action, e)


## Movement input on the ground plane: x = right, y = forward (both -1..1).
static func move_vector() -> Vector2:
	if Game.menu_open:
		return Vector2.ZERO
	var v := Input.get_vector("move_left", "move_right", "move_back", "move_forward")
	return v.limit_length(1.0)


static func camera_stick() -> Vector2:
	if Game.menu_open:
		return Vector2.ZERO
	return Input.get_vector("cam_left", "cam_right", "cam_up", "cam_down")
