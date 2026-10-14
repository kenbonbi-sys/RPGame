class_name Colossus
extends Node3D
## What every colossus shares: a body made of moving parts (AnimatableBody3D on the "colossus"
## layer) that heroes climb where there is fur, weak points that carry its life, shaking heroes off,
## falling, and coming back (every boss in this game returns, 3 minutes after it falls).

signal health_changed(hp: float, max_hp: float)
signal defeated
signal skill_used(skill_name: String)

const LAYER_COLOSSUS := 8
const FUR_SHADER := preload("res://shaders/fur.gdshader")

@export var display_name := "Colossus"
@export var aggro_range := 95.0
@export var respawn_seconds := 180.0
## For XP and the armour maths on heroes (the Unity boss's level).
@export var level := 6
## {"item": id, "chance": 0..1, "min": n, "max": n}; boss coins included as "coin" rows.
@export var loot: Array[Dictionary] = []

## Heroes who stabbed it this fight: they share the XP and roll the loot.
var attackers: Array[Node] = []

var weak_points: Array[WeakPoint] = []
## 0..1: how hard the body is shaking right now (heroes holding on lose grip faster).
var shake_amount := 0.0
var dead := false
var engaged := false
var home: Transform3D

var fur_material: ShaderMaterial
var armor_material: ShaderMaterial
var claw_material: ShaderMaterial
var lock_material: ShaderMaterial


func _enter_tree() -> void:
	if not Game.colossi.has(self):
		Game.colossi.append(self)


func _exit_tree() -> void:
	Game.colossi.erase(self)


func _init_materials(fur_base: Color, fur_tip: Color, armor: Color) -> void:
	fur_material = ShaderMaterial.new()
	fur_material.shader = FUR_SHADER
	fur_material.set_shader_parameter("base", fur_base)
	fur_material.set_shader_parameter("tip", fur_tip)
	# bark-like armour plates, painted, with moss on whatever faces up
	armor_material = CreatureArt.painted(armor, 0.32, 0.6, 0.95)
	armor_material.set_shader_parameter("paint_scale", 0.9)
	claw_material = CreatureArt.painted(armor.darkened(0.55), 0.12, 0.0, 0.6)
	claw_material.set_shader_parameter("specular_amount", 0.25)
	lock_material = CreatureArt.painted(fur_tip, 0.2, 0.45, 0.95)
	lock_material.set_shader_parameter("paint_scale", 1.2)


# ================================================================== building the body
func make_part(part_name: String) -> AnimatableBody3D:
	var b := AnimatableBody3D.new()
	b.name = part_name
	b.collision_layer = LAYER_COLOSSUS
	b.collision_mask = 0
	b.set_meta("colossus", self)
	add_child(b)
	return b


func _add_shape(body: Node3D, shape: Shape3D, mesh: Mesh, xf: Transform3D, climbable: bool, mat: Material) -> CollisionShape3D:
	var cs := CollisionShape3D.new()
	cs.shape = shape
	cs.transform = xf
	cs.set_meta("climbable", climbable)
	body.add_child(cs)
	if mesh == null:
		return cs
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.transform = xf
	mi.material_override = mat
	body.add_child(mi)
	return cs


func add_capsule(body: Node3D, radius: float, height: float, xf: Transform3D, climbable := true, tufts := 0) -> CollisionShape3D:
	var s := CapsuleShape3D.new()
	s.radius = radius
	s.height = height
	var m := CapsuleMesh.new()
	m.radius = radius
	m.height = height
	m.radial_segments = 24
	m.rings = 10
	var cs := _add_shape(body, s, m, xf, climbable, fur_material if climbable else armor_material)
	if tufts > 0:
		_add_tufts(body, xf, radius, height, tufts)
	return cs


func add_sphere(body: Node3D, radius: float, xf: Transform3D, climbable := true, tufts := 0) -> CollisionShape3D:
	var s := SphereShape3D.new()
	s.radius = radius
	var m := SphereMesh.new()
	m.radius = radius
	m.height = radius * 2.0
	m.radial_segments = 24
	m.rings = 12
	var cs := _add_shape(body, s, m, xf, climbable, fur_material if climbable else armor_material)
	if tufts > 0:
		_add_tufts(body, xf, radius, radius * 2.0, tufts)
	return cs


## A solid box nobody can hold on to; `show` false keeps only the collider.
func add_box(body: Node3D, size: Vector3, xf: Transform3D, mat: Material = null, show := true) -> CollisionShape3D:
	var s := BoxShape3D.new()
	s.size = size
	var m: Mesh = null
	if show:
		m = plate_mesh(size)
	return _add_shape(body, s, m, xf, false, mat if mat else armor_material)


static var _plates := {}


## A chunky bevelled plate for armour boxes.
static func plate_mesh(size: Vector3) -> ArrayMesh:
	var key := size.snapped(Vector3.ONE * 0.01)
	if _plates.has(key):
		return _plates[key]
	var k := MeshKit.new()
	k.rounded_box(size, minf(size.x, minf(size.y, size.z)) * 0.3, Transform3D.IDENTITY, Color.WHITE)
	var m := ArrayMesh.new()
	k.commit(m, null)
	_plates[key] = m
	return m


static var _lock_mesh: ArrayMesh


## One chunky, curled lock of fur (1 m long, along +Y), shared by every colossus.
static func lock_mesh() -> ArrayMesh:
	if _lock_mesh:
		return _lock_mesh
	var k := MeshKit.new()
	k.tube(0.2, 0.03, 1.0, 6, Transform3D(Basis.IDENTITY.scaled(Vector3(1.8, 1.0, 0.6)), Vector3(0, -0.5, 0)),
		Color(0.62, 0.6, 0.58), Vector3(0, 0, 0.3), 3, Color(1.08, 1.06, 1.02))
	_lock_mesh = ArrayMesh.new()
	k.commit(_lock_mesh, null)
	return _lock_mesh


## Shaggy locks over a capsule (or sphere) so the silhouette reads as fur.
func _add_tufts(body: Node3D, xf: Transform3D, radius: float, height: float, count: int) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = lock_mesh()
	mm.instance_count = count
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(body.name) ^ count
	var half := maxf(0.0, height * 0.5 - radius)
	for i in count:
		var y := rng.randf_range(-half - radius * 0.6, half + radius * 0.6)
		var ang := rng.randf() * TAU
		var axis_y := clampf(y, -half, half)
		var n := Vector3(cos(ang), 0, sin(ang))
		if absf(y) > half:
			var cap := (absf(y) - half) / radius
			n = (n * sqrt(maxf(0.0, 1.0 - cap * cap)) + Vector3(0, signf(y) * cap, 0)).normalized()
		var p := Vector3(0, axis_y, 0) + n * radius * 0.96
		var dir := (n + Vector3(0, -0.8, 0) + Vector3(rng.randf_range(-0.3, 0.3), 0, rng.randf_range(-0.3, 0.3))).normalized()
		var len := rng.randf_range(0.55, 1.1)
		var side := dir.cross(Vector3.RIGHT if absf(dir.x) < 0.9 else Vector3.FORWARD).normalized()
		var b := Basis(side, dir * len, side.cross(dir).normalized())
		mm.set_instance_transform(i, xf * Transform3D(b, p + dir * len * 0.45))
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	mmi.material_override = lock_material
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	body.add_child(mmi)


func add_weak_point(body: Node3D, at: Vector3, outward: Vector3, hp: float, vital: bool, size := 1.8) -> WeakPoint:
	var wp := WeakPoint.new()
	wp.max_hp = hp
	wp.vital = vital
	wp.size = size
	wp.colossus = self
	var z := outward.normalized()
	var x := (Vector3.UP if absf(z.y) < 0.9 else Vector3.RIGHT).cross(z).normalized()
	wp.transform = Transform3D(Basis(x, z.cross(x), z), at)
	body.add_child(wp)
	wp.destroyed.connect(on_weak_point_destroyed)
	weak_points.append(wp)
	return wp


# ================================================================== heroes
func heroes() -> Array[Node3D]:
	var out: Array[Node3D] = []
	for h in get_tree().get_nodes_in_group("heroes"):
		if is_instance_valid(h) and not h.is_dead():
			out.append(h)
	return out


func nearest_hero(max_distance := INF) -> Node3D:
	var best: Node3D = null
	var best_d := max_distance
	for h in heroes():
		var d := h.global_position.distance_to(global_position)
		if d < best_d:
			best_d = d
			best = h
	return best


func is_on_me(h: Node3D) -> bool:
	return h.has_method("attached_colossus") and h.attached_colossus() == self


func heroes_on_me() -> Array[Node3D]:
	return heroes().filter(func(h): return is_on_me(h))


## Hurts heroes on the ground within a radius (those clinging to the colossus are spared).
func hit_area(center: Vector3, radius: float, amount: float, knock := 8.0, lift := 5.0, skill := "") -> int:
	var n := 0
	for h in heroes():
		if is_on_me(h) or h.get("is_climbing"):
			continue
		var d := Vector2(h.global_position.x - center.x, h.global_position.z - center.z).length()
		if d <= radius and absf(h.global_position.y - center.y) < 6.0:
			hurt_hero(h, amount * (1.0 - 0.4 * d / radius), center, knock, lift, skill)
			n += 1
	return n


## A blow that throws a hero to the ground (armour still counts against the colossus's level).
func hurt_hero(h: Node3D, amount: float, from: Vector3, knock := 8.0, lift := 5.0, skill := "") -> float:
	var hh: Health = h.get("health")
	if hh == null:
		return 0.0
	var d := DamageInfo.make(amount, Combat.Team.ENEMY, self, h.global_position + Vector3.UP, h.global_position - from,
		Combat.Element.PHYSICAL, knock)
	d.knockdown = true
	d.lift = lift
	d.source_level = level
	d.skill_name = skill
	return hh.take_damage(d)


# ================================================================== damage
func vital_hp() -> float:
	var s := 0.0
	for wp in weak_points:
		if wp.vital:
			s += wp.hp
	return s


func max_vital_hp() -> float:
	var s := 0.0
	for wp in weak_points:
		if wp.vital:
			s += wp.max_hp
	return s


## A sword on the hide does nothing but spark (only the weak points matter).
func hide_hit(p: Vector3) -> void:
	Fx.burst(p, Vector3.UP, Color(1.0, 0.85, 0.6, 0.9), 8, 4.0, 0.15, 0.4, 70.0, -9.0, true)


func prodded(_hero: Node3D) -> void:
	shake_amount = maxf(shake_amount, 0.12)


func on_weak_point_stabbed(_wp: WeakPoint, _amount: float, hero: Node3D) -> void:
	if hero and not hero in attackers:
		attackers.append(hero)
	health_changed.emit(vital_hp(), max_vital_hp())


func on_weak_point_destroyed(wp: WeakPoint) -> void:
	if wp.vital and vital_hp() <= 0.0 and not dead:
		die()


func die() -> void:
	dead = true
	engaged = false
	defeated.emit()
	Game.boss_defeated.emit(self)
	if Game.is_authority():
		var credited: Array = attackers.filter(func(h) -> bool: return is_instance_valid(h))
		if credited.is_empty():
			credited = heroes()
		for h in credited:
			if h.get("stats"):
				h.stats.on_kill(level, Enemy.Rank.BOSS, look_point())
		Loot.drop(get_parent(), look_point().lerp(global_position, 0.6), loot, credited)
		Game.enemy_killed.emit(self, credited)
	get_tree().create_timer(respawn_seconds, false).timeout.connect(respawn)


func respawn() -> void:
	attackers.clear()
	for wp in weak_points:
		wp.restore()
	global_transform = home
	dead = false
	shake_amount = 0.0
	health_changed.emit(vital_hp(), max_vital_hp())


func look_point() -> Vector3:
	return global_position + Vector3.UP * 8.0
