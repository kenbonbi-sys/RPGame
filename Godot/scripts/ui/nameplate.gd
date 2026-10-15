class_name Nameplate
extends Node3D
## "Slime Rêu · Cấp 2" and a small life bar over a monster, shown once it has been hit. The whole
## plate turns to face the camera as one piece (billboarding each part on its own pulled the fill
## away from its frame). The bar is a JuicyBar: it drains, a ghost trails what was lost, it flashes
## and jolts sideways on a hit, and goes amber at half life, red at a quarter.

const WIDTH := 1.3

var health: Health
var _label: Label3D
var _bar_root: Node3D
var _fill: MeshInstance3D
var _lag: MeshInstance3D
var _fill_mat: StandardMaterial3D
var _lag_mat: StandardMaterial3D
var _life: JuicyBar

static var _mats := {}


func setup(owner_body: Node3D, text: String, height: float) -> void:
	health = owner_body.get("health")
	position = Vector3.UP * height
	_label = Label3D.new()
	_label.text = text
	_label.font = load("res://assets/fonts/BarlowCondensed-Bold.ttf")
	_label.fixed_size = true
	_label.pixel_size = 0.0007
	_label.font_size = 34
	_label.outline_size = 10
	_label.modulate = Color(1.0, 0.92, 0.82)
	_label.no_depth_test = true
	_label.position = Vector3.UP * 0.2
	_label.layers = 2
	add_child(_label)
	_bar_root = Node3D.new()
	add_child(_bar_root)
	_life = JuicyBar.new(health.fraction() if health else 1.0)
	_bar(Vector2(WIDTH + 0.06, 0.13), Color(0.03, 0.04, 0.05, 0.8), 0.0, Vector3.ZERO)
	_lag = _bar(Vector2(WIDTH, 0.085), Color(1.0, 0.85, 0.55, 1.0), 0.001, Vector3(-WIDTH * 0.5, 0, 0), true)
	_fill = _bar(Vector2(WIDTH, 0.085), Hud.CREATURE, 0.002, Vector3(-WIDTH * 0.5, 0, 0), true)
	# the fill and the ghost change colour on their own, so they get their own materials
	_lag_mat = (_lag.material_override as StandardMaterial3D).duplicate()
	_lag.material_override = _lag_mat
	_fill_mat = (_fill.material_override as StandardMaterial3D).duplicate()
	_fill.material_override = _fill_mat


## A flat quad in the plate's own plane; anchored at its left edge when it is a fill.
func _bar(size: Vector2, col: Color, z: float, pos: Vector3, left_anchored := false) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = size
	if left_anchored:
		q.center_offset = Vector3(size.x * 0.5, 0, 0)
	mi.mesh = q
	mi.material_override = _mat(col)
	mi.position = pos + Vector3(0, 0, z)
	mi.layers = 2
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_bar_root.add_child(mi)
	return mi


static func _mat(c: Color) -> StandardMaterial3D:
	if _mats.has(c):
		return _mats[c]
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = c
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA if c.a < 1.0 else BaseMaterial3D.TRANSPARENCY_DISABLED
	m.no_depth_test = true
	m.render_priority = 5
	_mats[c] = m
	return m


func _process(delta: float) -> void:
	if health == null:
		return
	var cam := get_viewport().get_camera_3d()
	if cam:
		global_basis = cam.global_basis
	_life.update(health.fraction(), delta)
	_fill.scale.x = maxf(0.001, _life.fill)
	_lag.scale.x = maxf(0.001, _life.ghost)
	_fill_mat.albedo_color = _life.color(Hud.CREATURE)
	_lag_mat.albedo_color = _life.ghost_color()
	_bar_root.position.x = _life.shove() * 0.07
	visible = not health.is_dead
