class_name UmbralBlast
extends Node3D
## Umbral's proc. On a colossus (UmbralCyst) it grows a cyst on one of its parts. On a small monster
## a knot of darkness swells in it for 3 s and bursts, hurting every monster within 3.5 m.

var health: Health
var damage := 30.0
var source: Node
var _age := 0.0
var _orb: MeshInstance3D

const FUSE := 3.0
const RADIUS := 3.5


static func prime(h: Health, dmg: float, by: Node) -> void:
	var b := UmbralBlast.new()
	b.health = h
	b.damage = dmg
	b.source = by
	h.add_child(b)


func _ready() -> void:
	_orb = MeshInstance3D.new()
	_orb.mesh = CreatureArt.sphere(0.3, 12)
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.25, 0.05, 0.35)
	m.emission_enabled = true
	m.emission = Color(0.6, 0.25, 1.0)
	m.emission_energy_multiplier = 2.0
	_orb.material_override = m
	add_child(_orb)


func _process(delta: float) -> void:
	_age += delta
	var at := health.hit_point(health.global_position + Vector3.FORWARD) if is_instance_valid(health) else global_position
	_orb.global_position = at
	_orb.scale = Vector3.ONE * (0.6 + 1.2 * _age / FUSE) * (1.0 + 0.1 * sin(_age * 20.0))
	if _age >= FUSE:
		_burst(at)


func _burst(at: Vector3) -> void:
	var template := DamageInfo.make(damage, Combat.Team.PLAYER, source if is_instance_valid(source) else null, at, Vector3.FORWARD,
		Combat.Element.DARK, 6.0)
	template.poise = 20.0
	Combat.damage_circle(get_world_3d(), at, RADIUS, template)
	if Game.has_screen():
		Fx.burst(at, Vector3.UP, Color(0.6, 0.3, 1.0, 1.0), 50, 9.0, 0.4, 0.7, 90.0, -3.0, true, RADIUS * 0.5)
		Fx.flash(at, Color(0.6, 0.3, 1.0), 6.0, 9.0)
		Sfx.play("whack_heavy", at, 0.0, 0.8)
		Game.kick(0.12)
	queue_free()
