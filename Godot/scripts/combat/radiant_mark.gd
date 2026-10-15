class_name RadiantMark
extends Node3D
## Radiant's proc: a sun mark over a monster for 12 s. If it starts an Unstable Move (a colossus's
## charge) or, for a small monster, an attack, the mark detonates: a little damage and the move is
## interrupted. If nothing happens, it does that little damage as it fades.

var health: Health
var damage := 10.0
var source: Node
var _age := 0.0
var _sprite: Sprite3D

const LIFE := 12.0


static func attach(h: Health, dmg: float, by: Node) -> void:
	for c in h.get_children():
		if c is RadiantMark:
			c._age = 0.0
			return
	var m := RadiantMark.new()
	m.health = h
	m.damage = dmg
	m.source = by
	h.add_child(m)


static func on(h: Health) -> RadiantMark:
	if h == null:
		return null
	for c in h.get_children():
		if c is RadiantMark:
			return c
	return null


func _ready() -> void:
	_sprite = Sprite3D.new()
	_sprite.texture = GameIcons.icon("prompt:sun", [["sun", "main", 50, 50, 0.9, 0]], "holy", 96)
	_sprite.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_sprite.no_depth_test = true
	_sprite.shaded = false
	_sprite.pixel_size = 0.012
	add_child(_sprite)


func _process(delta: float) -> void:
	_age += delta
	if not is_instance_valid(health) or health.is_dead:
		queue_free()
		return
	_sprite.global_position = health.head_position() + Vector3.UP * 0.9
	_sprite.modulate.a = 0.6 + 0.4 * sin(_age * 6.0)
	if _age >= LIFE:
		_burst(false)


## The monster started an Unstable Move: the mark detonates and interrupts it.
func detonate() -> void:
	_burst(true)


func _burst(interrupts: bool) -> void:
	if not is_instance_valid(health) or health.is_dead:
		queue_free()
		return
	var at := health.head_position()
	var d := DamageInfo.make(damage, Combat.Team.PLAYER, source if is_instance_valid(source) else null, at, Vector3.DOWN, Combat.Element.HOLY)
	d.pure = true
	health.take_damage(d)
	if Game.has_screen():
		Fx.flash(at, Color(1.0, 0.9, 0.5), 7.0, 10.0)
		Fx.burst(at, Vector3.UP, Color(1.0, 0.9, 0.5, 1.0), 40, 8.0, 0.35, 0.7, 90.0, -2.0, true)
		Sfx.play("interrupt" if interrupts else "whack_heavy", at, 0.0 if interrupts else -4.0)
	if interrupts:
		var owner := health.owner_body()
		if owner and owner.has_method("interrupt"):
			owner.interrupt(source)
		elif health.status:
			health.status.force_stun(1.5)
	queue_free()
