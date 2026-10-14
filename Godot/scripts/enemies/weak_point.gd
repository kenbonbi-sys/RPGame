class_name WeakPoint
extends Node3D
## A glowing rune on a colossus's hide. Stab it (charged thrust, see Hero) to hurt the colossus;
## vital points carry its life, the others make it kneel or stagger. Place it on a part with its
## local +Z pointing out of the hide.

signal destroyed(wp: WeakPoint)

@export var max_hp := 100.0
@export var vital := true
@export var size := 1.6

var hp := 100.0
var active := true
var colossus: Node

var _quad: MeshInstance3D
var _mat: ShaderMaterial
var _light: OmniLight3D
var _flare := 0.0

const SIGIL := preload("res://shaders/sigil.gdshader")


func _ready() -> void:
	hp = max_hp
	_mat = ShaderMaterial.new()
	_mat.shader = SIGIL
	_quad = MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = Vector2.ONE * size
	_quad.mesh = q
	_quad.material_override = _mat
	_quad.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_quad.position = Vector3(0, 0, 0.3)   # clear of the fur's clumps
	add_child(_quad)
	_light = OmniLight3D.new()
	_light.light_color = Color(0.55, 0.95, 0.85)
	_light.light_energy = 0.8
	_light.omni_range = 3.5
	_light.position = Vector3(0, 0, 0.5)
	add_child(_light)


func _process(delta: float) -> void:
	_flare = move_toward(_flare, 0.0, delta * 2.0)
	_mat.set_shader_parameter("flare", _flare)
	_mat.set_shader_parameter("health", hp / max_hp)
	_light.light_energy = (0.5 + 0.5 * hp / max_hp) * (0.8 + _flare * 3.0) if active else 0.0


func outward() -> Vector3:
	return global_transform.basis.z.normalized()


func stab(amount: float, hero: Node3D) -> void:
	if not active:
		return
	hp = maxf(0.0, hp - amount)
	_flare = 1.0
	if Game.has_screen():
		DamageNumbers.show(global_position + outward() * 0.6, str(roundi(amount)) + ("!" if amount >= 40.0 else ""), "crit", true)
	var out := outward()
	Fx.burst(global_position + out * 0.2, out + Vector3.UP * 0.6, Color(0.04, 0.05, 0.06, 0.95), 40 + int(amount), 9.0, 0.55, 1.6, 22.0, -9.0)
	Fx.burst(global_position + out * 0.2, out, Color(0.55, 1.0, 0.85, 0.9), 16, 5.0, 0.25, 0.7, 60.0, -2.0, true)
	Fx.flash(global_position + out * 0.8, Color(0.55, 1.0, 0.85), 6.0, 9.0)
	Game.shake(0.35 + amount / 80.0)
	if colossus and colossus.has_method("on_weak_point_stabbed"):
		colossus.on_weak_point_stabbed(self, amount, hero)
	if hp <= 0.0:
		active = false
		_quad.visible = false
		destroyed.emit(self)


## Brings a non-vital point back (after the colossus stands up again).
func restore() -> void:
	hp = max_hp
	active = true
	_quad.visible = true
