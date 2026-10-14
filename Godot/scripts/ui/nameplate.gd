class_name Nameplate
extends Node3D
## "Slime Rêu · Cấp 2" and a small red life bar over a monster, shown once it has been hit.

var health: Health
var _label: Label3D
var _fill: MeshInstance3D
var _width := 1.2

static var _bg_mat: StandardMaterial3D
static var _fill_mat: StandardMaterial3D


func setup(owner_body: Node3D, text: String, height: float) -> void:
	health = owner_body.get("health")
	position = Vector3.UP * height
	_label = Label3D.new()
	_label.text = text
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.fixed_size = true
	_label.pixel_size = 0.0007
	_label.font_size = 30
	_label.outline_size = 10
	_label.modulate = Color(1.0, 0.55, 0.5)
	_label.no_depth_test = true
	_label.position = Vector3.UP * 0.22
	_label.layers = 2
	add_child(_label)
	if _bg_mat == null:
		_bg_mat = _mat(Color(0, 0, 0, 0.65))
		_fill_mat = _mat(Color(0.85, 0.22, 0.2, 1.0))
	var bg := MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = Vector2(_width + 0.06, 0.12)
	bg.mesh = q
	bg.material_override = _bg_mat
	bg.layers = 2
	add_child(bg)
	_fill = MeshInstance3D.new()
	var qf := QuadMesh.new()
	qf.size = Vector2(_width, 0.08)
	qf.center_offset = Vector3(_width * 0.5, 0, 0.001)
	_fill.mesh = qf
	_fill.material_override = _fill_mat
	_fill.position = Vector3(-_width * 0.5, 0, 0)
	_fill.layers = 2
	add_child(_fill)


static func _mat(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = c
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA if c.a < 1.0 else BaseMaterial3D.TRANSPARENCY_DISABLED
	m.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	m.no_depth_test = true
	m.render_priority = 5
	return m


func _process(_delta: float) -> void:
	if health == null:
		return
	_fill.scale.x = maxf(0.001, health.fraction())
	visible = not health.is_dead
