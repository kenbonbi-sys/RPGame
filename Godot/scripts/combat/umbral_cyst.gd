class_name UmbralCyst
extends Area3D
## Umbral's proc on a colossus: a dark cyst grows on one of its parts for 30 s. It has its own small
## life (4% of the colossus's). Every hit on it bursts for part damage around it; destroyed, it
## explodes for Umbral damage to the colossus and heavy part damage around it.

var colossus: Node
var health: Health
var power := 30.0
var source: Node
var _age := 0.0
var _mesh: MeshInstance3D

const LIFE := 30.0
const RADIUS := 3.2


static func grow(c: Node, threshold: float, by: Node) -> UmbralCyst:
	var shapes: Array = []
	for part in c.parts:
		for b in part.bodies:
			for ch in b.get_children():
				if ch is CollisionShape3D:
					shapes.append(ch)
	if shapes.is_empty():
		return null
	var shape: CollisionShape3D = shapes.pick_random()
	var body: Node3D = shape.get_parent()
	var dir := Vector3(randf_range(-1, 1), randf_range(0.2, 1.0), randf_range(-1, 1)).normalized()
	var at := Colossus._closest_on_shape(shape, shape.global_position + dir * 30.0)
	var cyst := UmbralCyst.new()
	cyst.colossus = c
	cyst.power = threshold
	cyst.source = by
	body.add_child(cyst)
	cyst.global_position = at
	return cyst


func _ready() -> void:
	collision_layer = Combat.LAYER_ENEMY
	collision_mask = 0
	monitoring = false
	var cs := CollisionShape3D.new()
	var s := SphereShape3D.new()
	s.radius = 0.9
	cs.shape = s
	add_child(cs)
	health = Health.new()
	health.name = "Health"
	health.display_name = "Nang Ám"
	health.team = Combat.Team.ENEMY
	health.max_hp = maxf(60.0, float(colossus.max_hp) * 0.04) if colossus else 100.0
	health.head_height = 0.8
	add_child(health)
	health.damaged.connect(_on_hit)
	health.died.connect(func(_d: DamageInfo) -> void: _explode())
	_mesh = MeshInstance3D.new()
	_mesh.mesh = CreatureArt.sphere(0.75, 16)
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.22, 0.05, 0.3)
	m.emission_enabled = true
	m.emission = Color(0.55, 0.2, 0.95)
	m.emission_energy_multiplier = 1.6
	m.roughness = 0.3
	_mesh.material_override = m
	_mesh.layers = 4
	add_child(_mesh)
	var l := OmniLight3D.new()
	l.light_color = Color(0.6, 0.3, 1.0)
	l.light_energy = 1.5
	l.omni_range = 4.0
	add_child(l)
	if Game.has_screen():
		Fx.burst(global_position, Vector3.UP, Color(0.6, 0.3, 1.0, 1.0), 30, 4.0, 0.35, 0.6, 90.0, 0.0, true)


func _physics_process(delta: float) -> void:
	_age += delta
	_mesh.scale = Vector3.ONE * (1.0 + 0.08 * sin(_age * 5.0)) * clampf(_age * 3.0, 0.2, 1.0)
	if _age >= LIFE or colossus == null or colossus.dead:
		queue_free()


func _on_hit(d: DamageInfo, amount: float) -> void:
	if colossus and not colossus.dead:
		colossus.part_damage_area(global_position, RADIUS, amount * 1.5)
	if Game.has_screen():
		Fx.burst(global_position, Vector3.UP, Color(0.6, 0.3, 1.0, 0.9), 14, 5.0, 0.25, 0.4, 90.0, -2.0, true)


func _explode() -> void:
	if colossus and not colossus.dead and colossus.health:
		var d := DamageInfo.make(power, Combat.Team.PLAYER, source if is_instance_valid(source) else null, global_position, Vector3.DOWN,
			Combat.Element.DARK)
		d.pure = true
		colossus.health.take_damage(d)
		colossus.part_damage_area(global_position, RADIUS * 1.4, power * 3.0)
	if Game.has_screen():
		Fx.burst(global_position, Vector3.UP, Color(0.6, 0.3, 1.0, 1.0), 60, 10.0, 0.45, 0.8, 90.0, -3.0, true, 1.5)
		Fx.flash(global_position, Color(0.6, 0.3, 1.0), 8.0, 12.0)
		Sfx.play("crunch", global_position, 2.0, 0.8)
	queue_free()
