class_name Npc
extends Node3D
## One of the village's townsfolk (the Ramsgate cast of Dauntless, Townsfolk.CAST): a body from
## the hero's own kit dressed for their trade, an idle that suits it, their name and title over
## their head as Dauntless shows them, and a turn of the head and body toward a slayer who comes
## near. Walk up and press F to talk (TalkMenu). Off screen their animation stops.

const TALK_RANGE := 3.4
const NOTICE_RANGE := 7.0

## The one the hero would talk to on F (the nearest in reach), for the HUD's hint.
static var focus: Npc

var id := ""
var data := {}
var model: HeroModel
var talking := false
var _anim: AnimationPlayer
var _idle := "Idle"
var _home_yaw := 0.0
var _labels: Array[Label3D] = []


func setup(npc_id: String, d: Dictionary, yaw: float) -> void:
	id = npc_id
	data = d
	_home_yaw = yaw
	rotation.y = yaw


func _ready() -> void:
	add_to_group("npcs")
	var look: Dictionary = data.get("look", {})
	model = HeroModel.new()
	model.name = "Model"
	model.portrait = false
	model.warm_armour = false
	model.body = look.get("body", "Male")
	model.outfit = look.get("outfit", "Peasant")
	model.outfit_texture = look.get("texture", "")
	model.outfit_tint = look.get("tint", Color.WHITE)
	model.hood = look.get("hood", false)
	model.hair = look.get("hair", "")
	model.hair_color = look.get("hair_color", Color(0.2, 0.14, 0.1))
	model.light_skin = look.get("light_skin", true)
	model.scarf = look.get("scarf", Color(0, 0, 0, 0))
	model.weapon_id = look.get("weapon", "")
	model.armour = look.get("armour", {})
	model.scale = Vector3.ONE * float(look.get("scale", 1.0))
	add_child(model)
	for extra in look.get("extras", []):
		NpcGear.add(model, String(extra))
	# from beyond the village they are not drawn at all
	for mi in model.find_children("*", "MeshInstance3D", true, false):
		(mi as MeshInstance3D).visibility_range_end = 70.0
		(mi as MeshInstance3D).visibility_range_end_margin = 8.0
		(mi as MeshInstance3D).visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	_anim = model.anim
	_idle = String(look.get("idle", "Idle"))
	if _anim:
		_anim.animation_finished.connect(func(_n: StringName) -> void: _play(_idle if not talking else "Idle_Talking", 0.3))
		_play(_idle, 0.0)
		_anim.seek(randf() * 2.0, true)
	# the hero bumps into them
	var body := StaticBody3D.new()
	body.collision_layer = Combat.LAYER_WORLD
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = 0.35
	cap.height = 1.8
	cs.shape = cap
	cs.position = Vector3(0, 0.9, 0)
	body.add_child(cs)
	add_child(body)
	if Game.has_screen():
		_make_labels()
		# nothing to animate while no camera sees them
		# (the path is set before it enters the tree: by default it would switch off this whole
		# townsperson, not just the animation)
		var seen := VisibleOnScreenEnabler3D.new()
		seen.aabb = AABB(Vector3(-0.8, 0, -0.8), Vector3(1.6, 2.4, 1.6))
		seen.enable_node_path = NodePath("../" + String(get_path_to(_anim))) if _anim else NodePath()
		add_child(seen)


func _make_labels() -> void:
	var top := 2.15 * float(data.get("look", {}).get("scale", 1.0))
	for k in 2:
		var l := Label3D.new()
		l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		l.no_depth_test = false
		l.font = Hud.FONT_BOLD if k == 0 else Hud.FONT_SEMI
		# the same size on screen near or far, as Dauntless draws names
		l.fixed_size = true
		l.font_size = 40 if k == 0 else 30
		l.outline_size = 10
		l.outline_modulate = Color(0, 0, 0, 0.75)
		l.pixel_size = 0.0007
		l.text = String(data.name).to_upper() if k == 0 else String(data.title)
		l.modulate = Color(0.97, 0.97, 0.95) if k == 0 else Color(1.0, 0.8, 0.45)
		l.position = Vector3(0, top + (0.16 if k == 0 else 0.0), 0)
		l.visibility_range_end = 16.0
		l.visibility_range_end_margin = 3.0
		l.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		add_child(l)
		_labels.append(l)


func _play(clip: String, blend := 0.2) -> void:
	if _anim and _anim.has_animation(clip):
		_anim.play(clip, blend)


func begin_talk() -> void:
	talking = true
	_play("Idle_Talking", 0.3)


func end_talk() -> void:
	talking = false
	_play(_idle, 0.4)


func head_position() -> Vector3:
	return global_position + Vector3.UP * 1.62 * float(data.get("look", {}).get("scale", 1.0))


func _process(_delta: float) -> void:
	# names step aside while a menu or a talk has the screen
	for l in _labels:
		l.visible = not Game.menu_open


func _physics_process(delta: float) -> void:
	var hero := Game.hero
	if not is_instance_valid(hero):
		return
	var to: Vector3 = hero.global_position - global_position
	to.y = 0.0
	var d := to.length()
	# turn toward a slayer who comes close (a working smith keeps at the anvil until spoken to)
	var want := _home_yaw
	if talking or (d < NOTICE_RANGE and not data.get("busy", false)):
		want = atan2(to.x, to.z)
	rotation.y = lerp_angle(rotation.y, want, 1.0 - exp(-(6.0 if talking else 2.5) * delta))
	# who answers F
	var can: bool = d < TALK_RANGE and not hero.get("is_climbing") and not hero.is_dead()
	if can and (focus == null or not is_instance_valid(focus) or focus == self
			or focus.global_position.distance_to(hero.global_position) > d):
		focus = self
	elif focus == self and not can:
		focus = null


func _unhandled_input(event: InputEvent) -> void:
	if focus == self and not Game.menu_open and event.is_action_pressed("interact") and Menus.current:
		Menus.current.open("talk", "", self)
		get_viewport().set_input_as_handled()


func _exit_tree() -> void:
	if focus == self:
		focus = null
