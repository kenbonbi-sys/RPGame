class_name Colossus
extends Node3D
## What every colossus shares: a body made of moving parts (AnimatableBody3D on the "colossus"
## layer) that heroes climb where there is fur, glowing weak points to stab, shaking heroes off,
## falling, and coming back (every boss in this game returns, 3 minutes after it falls).
##
## Its life works like a Dauntless behemoth's: one pool (BossHealth) that every hit on any part
## lowers. Breakable parts take part damage on top (a blade's cut counts +50%) and break at their
## threshold: the part's armour falls off, it drops loot, the colossus flinches. A stagger bar fills
## from every hit (more from the head and from heavy hits); full, the colossus is knocked down and
## takes staggered damage (+50%). Numbers: yellow = part damage on a part not yet broken,
## white-blue = core damage, orange = staggered damage.

signal health_changed(hp: float, max_hp: float)
signal defeated
signal skill_used(skill_name: String)
signal part_broken(part: Dictionary)

const LAYER_COLOSSUS := 8
const FUR_SHADER := preload("res://shaders/fur.gdshader")

@export var display_name := "Colossus"
@export var aggro_range := 95.0
@export var respawn_seconds := 180.0
## For XP and the armour maths on heroes (the Unity boss's level).
@export var level := 6
## {"item": id, "chance": 0..1, "min": n, "max": n}; boss coins included as "coin" rows.
@export var loot: Array[Dictionary] = []
## The Unity boss's numbers: life, and the stagger bar (Poise) with how long a stagger lasts.
@export var max_hp := 3200.0
## Dauntless element (Elements).
@export var element := ""
@export var max_stagger := 300.0
@export var stagger_seconds := 6.0
## A stab into a weak point hurts its life this many times the stab.
@export var stab_multiplier := 3.0
## A vital weak point destroyed: this share of its life, and a stagger.
@export var vital_burst := 0.08

const PART_CUT := 1.5

var health: BossHealth
var poise: Poise
## Its skin flashes white for a blink when struck.
var flash_materials: Array[Material] = []
var _flash := 0.0
var _flash_hold_until := 0.0
var status: StatusEffects
## Every part of the body: {id, name, bodies, max, hp, broken, breakable, stagger, interrupt, loot,
## on_break}. Parts that are not breakable (the trunk) still say how much stagger a hit there gives.
var parts: Array[Dictionary] = []
var _part_of := {}
## The hit being taken right now: {"part": the part, "stab": true for a weak point}.
var _hit := {}

## Heroes who stabbed it this fight: they share the XP and roll the loot.
var attackers: Array[Node] = []

var weak_points: Array[WeakPoint] = []
## 0..1: how hard the body is shaking right now (heroes holding on lose grip faster).
var shake_amount := 0.0
var dead := false
var engaged := false
var home: Transform3D

## False when the looks come from a sculpt: shapes then only collide.
var draw_shapes := true
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


# ================================================================== life
## The pool every hit lowers, the stagger bar and statuses (burns tick on it too).
func make_life() -> void:
	status = StatusEffects.new()
	status.name = "Status"
	add_child(status)
	poise = Poise.new()
	poise.name = "Poise"
	poise.max_poise = max_stagger
	poise.break_stun = stagger_seconds
	poise.decay_delay = 4.0
	poise.decay_per_second = 12.0
	add_child(poise)
	health = BossHealth.new()
	health.name = "Health"
	health.colossus = self
	health.display_name = display_name
	health.level = level
	health.max_hp = max_hp
	health.element = element
	health.team = Combat.Team.ENEMY
	add_child(health)
	health.damaged.connect(func(_d: DamageInfo, _a: float) -> void: health_changed.emit(health.hp, health.max_hp))
	health.died.connect(func(_d: DamageInfo) -> void:
		if not dead:
			die())
	poise.broken.connect(on_staggered)


func life_fraction() -> float:
	return health.fraction() if health else 1.0


## 0..1 toward the next stagger (1 while staggered).
func stagger_fraction() -> float:
	return poise.fraction() if poise else 0.0


func is_staggered() -> bool:
	return poise != null and poise.is_broken()


## Names a group of bodies as one part. `breakable_hp` 0 = it only takes core damage.
func add_body_part(id: String, part_name: String, bodies: Array, breakable_hp: float, loot_item := "", stagger := 1.0,
		interrupt := false, on_break := Callable()) -> Dictionary:
	var part := {"id": id, "name": part_name, "bodies": bodies, "max": breakable_hp, "hp": breakable_hp,
		"broken": false, "breakable": breakable_hp > 0.0, "stagger": stagger, "interrupt": interrupt, "loot": loot_item,
		"on_break": on_break}
	parts.append(part)
	for b in bodies:
		_part_of[b] = part
	return part


func part_by_id(id: String) -> Dictionary:
	for part in parts:
		if part.id == id:
			return part
	return {}


## The closest spot on any of its colliders to `p`, with the body it is on (a point inside a
## collider belongs to that one).
func closest_point(p: Vector3) -> Dictionary:
	var best := {"point": look_point(), "body": null}
	var best_d := INF
	for c in get_children():
		if not (c is AnimatableBody3D):
			continue
		for cs in c.get_children():
			if not (cs is CollisionShape3D):
				continue
			var q := _closest_on_shape(cs, p)
			var d := 0.0 if _inside(cs, p) else q.distance_squared_to(p)
			if d < best_d:
				best_d = d
				best = {"point": q, "body": c}
	return best


static func _inside(cs: CollisionShape3D, p: Vector3) -> bool:
	var local := cs.global_transform.affine_inverse() * p
	var s := cs.shape
	if s is SphereShape3D:
		return local.length() < s.radius
	if s is CapsuleShape3D:
		var half := maxf(0.0, s.height * 0.5 - s.radius)
		return (local - Vector3(0, clampf(local.y, -half, half), 0)).length() < s.radius
	if s is BoxShape3D:
		var h: Vector3 = s.size * 0.5
		return absf(local.x) < h.x and absf(local.y) < h.y and absf(local.z) < h.z
	return false


static func _closest_on_shape(cs: CollisionShape3D, p: Vector3) -> Vector3:
	var xf := cs.global_transform
	var local := xf.affine_inverse() * p
	var s := cs.shape
	var q := Vector3.ZERO
	if s is SphereShape3D:
		q = local.normalized() * s.radius if local.length() > 0.0001 else Vector3(0, s.radius, 0)
	elif s is CapsuleShape3D:
		var half := maxf(0.0, s.height * 0.5 - s.radius)
		var c := Vector3(0, clampf(local.y, -half, half), 0)
		var dv := local - c
		q = c + (dv.normalized() * s.radius if dv.length() > 0.0001 else Vector3(s.radius, 0, 0))
	elif s is BoxShape3D:
		var h: Vector3 = s.size * 0.5
		q = local.clamp(-h, h)
	return xf * q


## The lowest point of a body's colliders (is a charging head in reach?).
static func _lowest_of(body: Node) -> Vector3:
	var best := Vector3(0, INF, 0)
	for cs in body.get_children():
		if cs is CollisionShape3D:
			var q := _closest_on_shape(cs, (cs as Node3D).global_position - Vector3.UP * 50.0)
			if q.y < best.y:
				best = q
	return best


func part_at(p: Vector3) -> Dictionary:
	var body = closest_point(p)["body"]
	return _part_of.get(body, {})


## Before a hit counts: which part it landed on, and the head's extra stagger.
func prepare_hit(d: DamageInfo) -> void:
	if _hit.get("stab", false):
		return
	var part := {} if d.dot else part_at(d.point)
	_hit = {"part": part}
	if not part.is_empty():
		d.poise *= float(part.stagger)


## After it landed: part damage, a break at the threshold, an interrupt.
func after_hit(d: DamageInfo, amount: float) -> void:
	var part: Dictionary = _hit.get("part", {})
	var stab: bool = _hit.get("stab", false)
	_hit = {}
	if d.source and is_instance_valid(d.source) and d.source.is_in_group("heroes") and not d.source in attackers:
		attackers.append(d.source)
	if dead:
		return
	if not part.is_empty() and part.breakable and not part.broken and not d.dot:
		part.hp = maxf(0.0, float(part.hp) - amount * part_multiplier(d))
		if part.hp <= 0.0:
			break_part(part, d.point)
	if d.heavy and not stab and not part.is_empty() and part.interrupt and can_interrupt():
		interrupt(d.source)


## Part damage per point of core damage: a blade cuts +50% (Dauntless slashing weapons), a pierce
## +25%, blunt and spells 100%.
static func part_multiplier(d: DamageInfo) -> float:
	return PART_CUT if d.cutting else (1.25 if d.piercing else 1.0)


func break_part(part: Dictionary, at: Vector3) -> void:
	part.broken = true
	part.hp = 0.0
	if Game.has_screen():
		Sfx.play("crunch", at, 3.0)
		DamageNumbers.show(at + Vector3.UP * 0.6, "VỠ %s!" % String(part.name).to_upper(), "part", true)
		Fx.burst(at, Vector3.UP, Color(0.95, 0.82, 0.45, 1.0), 60, 9.0, 0.5, 1.0, 80.0, -9.0, true)
		Fx.dust(at, 3.0, 40)
		Game.log("Phá vỡ %s · %s" % [part.name, display_name], Color(1.0, 0.84, 0.4), part.loot if part.loot != "" else "level")
	Game.shake(0.5)
	Game.hitstop(0.1)
	if part.loot != "" and Game.is_authority():
		var credited: Array = attackers.filter(func(h) -> bool: return is_instance_valid(h))
		Loot.drop(get_parent(), at, [{"item": part.loot, "chance": 1.0, "min": 1, "max": 1}], credited)
	var cb: Callable = part.on_break
	if cb.is_valid():
		cb.call()
	part_broken.emit(part)


## Part damage to every breakable part within `radius` of `at` (an Umbral cyst bursting).
func part_damage_area(at: Vector3, radius: float, amount: float) -> void:
	for part in parts:
		if not part.breakable or part.broken:
			continue
		var near := INF
		for b in part.bodies:
			for cs in b.get_children():
				if cs is CollisionShape3D:
					near = minf(near, _closest_on_shape(cs, at).distance_to(at))
		if near <= radius:
			part.hp = maxf(0.0, float(part.hp) - amount)
			if Game.has_screen():
				DamageNumbers.show(at + Vector3(randf_range(-0.5, 0.5), 0.6, randf_range(-0.5, 0.5)), str(roundi(amount)), "part", true)
			if part.hp <= 0.0:
				break_part(part, at)


## Frost's proc: everything it does runs slower for a while.
var slow_factor := 1.0
var _slow_until := 0.0


func proc_slow(seconds: float, amount: float) -> void:
	slow_factor = 1.0 - amount
	_slow_until = Game.now() + seconds


func time_factor() -> float:
	if slow_factor < 1.0 and Game.now() >= _slow_until:
		slow_factor = 1.0
	return slow_factor


## Umbral's proc: a cyst on one of its parts.
func proc_cyst(threshold: float, by: Node) -> void:
	UmbralCyst.grow(self, threshold, by)


## A light blink of white over its whole hide (a full white flash on something this big blinds
## the screen), then a quick fade.
func flash(_color: Color, strength: float, seconds: float) -> void:
	_flash = maxf(_flash, strength * 0.22)
	_flash_hold_until = Game.now() + seconds


func update_flash(delta: float) -> void:
	if _flash <= 0.0 and flash_materials.is_empty():
		return
	var v := _flash if Game.now() < _flash_hold_until else _flash * 0.6
	if Game.now() >= _flash_hold_until:
		_flash = move_toward(_flash, 0.0, delta * 10.0)
	for m in flash_materials:
		if m is ShaderMaterial:
			(m as ShaderMaterial).set_shader_parameter("hurt", v)


## Colossi that can be interrupted say so (a charge in its window).
func can_interrupt() -> bool:
	return false


func interrupt(_by: Node) -> void:
	pass


## The stagger bar filled.
func on_staggered() -> void:
	pass


func show_hit_number(d: DamageInfo, amount: float) -> void:
	var at := d.point + Vector3(randf_range(-0.3, 0.3), randf_range(0.0, 0.4), randf_range(-0.3, 0.3))
	var part: Dictionary = _hit.get("part", {})
	if _hit.get("stab", false):
		DamageNumbers.show(at, "%d!" % roundi(amount), "staggered" if is_staggered() else "crit", true)
	elif is_staggered():
		DamageNumbers.show(at, str(roundi(amount)), "staggered", not d.dot)
	elif not part.is_empty() and part.breakable and not part.broken and not d.dot:
		DamageNumbers.show(at, str(roundi(amount * part_multiplier(d))), "part", d.crit or d.heavy)
	else:
		DamageNumbers.show(at, str(roundi(amount)), "core", d.crit or d.heavy)


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
	if mesh == null or not draw_shapes:
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
	var cs := _add_shape(body, s, fur_mesh(radius, height) if draw_shapes else null, xf, climbable, fur_material if climbable else armor_material)
	if tufts > 0 and draw_shapes:
		_add_tufts(body, xf, radius, height, tufts)
	return cs


func add_sphere(body: Node3D, radius: float, xf: Transform3D, climbable := true, tufts := 0) -> CollisionShape3D:
	var s := SphereShape3D.new()
	s.radius = radius
	var cs := _add_shape(body, s, fur_mesh(radius, radius * 2.0) if draw_shapes else null, xf, climbable, fur_material if climbable else armor_material)
	if tufts > 0 and draw_shapes:
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
static var _fur_meshes := {}


## A capsule (or ball, when height = 2 × radius) along +Y whose surface is pushed in and out in
## long clumps, so the fur has a lumpy silhouette. It stays within about a hand of the collider
## (mostly just under it), so heroes still hold on to what they see.
static func fur_mesh(radius: float, height: float) -> ArrayMesh:
	var key := "%.2f|%.2f" % [radius, height]
	if _fur_meshes.has(key):
		return _fur_meshes[key]
	var half := maxf(0.0, height * 0.5 - radius)
	var segs := clampi(int(TAU * radius / 0.7), 20, 56)
	var cap_rows := clampi(int(PI * 0.5 * radius / 0.7), 5, 14)
	var body_rows := maxi(1, int(half * 2.0 / 0.8))
	var amp := minf(0.32, radius * 0.07)
	# rows from the bottom pole to the top: [centre y, angle above the equator]
	var rows := []
	for i in cap_rows:
		rows.append([-half, -PI * 0.5 + PI * 0.5 * i / cap_rows])
	for i in body_rows + 1:
		rows.append([-half + half * 2.0 * i / body_rows, 0.0])
	for i in range(1, cap_rows + 1):
		rows.append([half, PI * 0.5 * i / cap_rows])
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for r in rows:
		var cy: float = r[0]
		var th: float = r[1]
		for sgi in segs:
			var a := TAU * sgi / segs
			var d := Vector3(cos(a) * cos(th), sin(th), sin(a) * cos(th))
			var p := Vector3(0, cy, 0) + d * radius
			var n1 := MeshKit._noise3(Vector3(p.x / 1.3, p.y / 3.0, p.z / 1.3))
			var n2 := MeshKit._noise3(p / 0.6 + Vector3(7, 7, 7))
			var disp := amp * ((n1 - 0.5) * 2.2 + (n2 - 0.5) * 0.7 - 0.25)
			st.add_vertex(p + d * disp)
	var cols := segs
	for r in rows.size() - 1:
		for sgi in segs:
			var a := r * cols + sgi
			var b := r * cols + (sgi + 1) % segs
			var c := b + cols
			var d := a + cols
			st.add_index(c)
			st.add_index(d)
			st.add_index(a)
			st.add_index(c)
			st.add_index(a)
			st.add_index(b)
	st.generate_normals()
	var m := st.commit()
	_fur_meshes[key] = m
	return m


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


## Hurts heroes on the ground within a radius. Those clinging to the colossus or riding high on
## it are spared; one standing on a paw is not (the paw is what slams down).
func hit_area(center: Vector3, radius: float, amount: float, knock := 8.0, lift := 5.0, skill := "") -> int:
	var n := 0
	for h in heroes():
		if h.get("is_climbing") or (is_on_me(h) and h.global_position.y > center.y + 2.5):
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
func prodded(_hero: Node3D) -> void:
	shake_amount = maxf(shake_amount, 0.12)


## A stab into a weak point: the rune loses `amount`, the colossus's life `stab_multiplier` times it,
## and the stab staggers as much as it hurt the rune.
func on_weak_point_stabbed(wp: WeakPoint, amount: float, hero: Node3D) -> void:
	if hero and not hero in attackers:
		attackers.append(hero)
	if health == null or dead:
		return
	var d := DamageInfo.make(amount * stab_multiplier, Combat.Team.PLAYER, hero, wp.global_position + wp.outward() * 0.6, -wp.outward())
	d.attack_scaled = true
	d.poise = amount
	d.skill_name = "Đâm"
	_hit = {"stab": true, "part": _part_of.get(wp.get_parent(), {})}
	health.take_damage(d)
	_hit = {}


## A vital rune destroyed takes a chunk of life and staggers the colossus.
func on_weak_point_destroyed(wp: WeakPoint) -> void:
	if not wp.vital or dead or health == null:
		return
	var d := DamageInfo.make(max_hp * vital_burst, Combat.Team.PLAYER, null, wp.global_position + wp.outward(), -wp.outward())
	d.pure = true
	_hit = {"stab": true}
	health.take_damage(d)
	_hit = {}
	if not dead and poise:
		poise.force_break(stagger_seconds)
		on_staggered()


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
	for part in parts:
		part.hp = part.max
		part.broken = false
	if health:
		health.reset_health(max_hp)
	if poise:
		poise.reset_poise()
	if status:
		status.cleanse()
	global_transform = home
	dead = false
	shake_amount = 0.0
	health_changed.emit(health.hp if health else 1.0, health.max_hp if health else 1.0)


func look_point() -> Vector3:
	return global_position + Vector3.UP * 8.0
