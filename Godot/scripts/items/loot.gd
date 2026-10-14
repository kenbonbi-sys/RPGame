class_name Loot
extends Node3D
## Drops, as the Unity game's Loot.cs + LootPickup.cs: each table entry rolls on its own; coins come
## out one per pickup (more with Sức Hút); things pop out in a little arc, wait a moment, then fly
## to the nearest hero close by. Offline there is one roll anyone can take; online (later) each
## credited hero rolls their own.

const MAGNET := 3.5
const PICKUP := 1.0
const RARITY_COLORS := [Color(0.92, 0.92, 0.88), Color(0.45, 0.9, 0.45), Color(0.4, 0.65, 1.0), Color(0.78, 0.45, 1.0)]

var item_id := ""
var count := 1
var owner_hero: Node3D
var _age := 0.0
var _from := Vector3.ZERO
var _to := Vector3.ZERO
var _speed := 0.0
var _visual: Node3D


static func drop(parent: Node, at: Vector3, table: Array, credited: Array) -> void:
	var owners: Array = [null] if not Game.is_online() or credited.is_empty() else credited
	for owner in owners:
		var gold_owner: Node3D = owner if owner else (Game.hero if is_instance_valid(Game.hero) else null)
		for e in table:
			if randf() > float(e.get("chance", 0.5)):
				continue
			var n := randi_range(int(e.get("min", 1)), int(e.get("max", 1)))
			var id: String = e.item
			if id == "coin":
				var gf: float = gold_owner.stats.gold_find if gold_owner and gold_owner.get("stats") else 0.0
				n = maxi(0, int(Combat.round_half_even(n * (1.0 + gf))))
				for i in n:
					spawn(parent, at, id, 1, owner)
			elif n > 0:
				spawn(parent, at, id, n, owner)


static func spawn(parent: Node, at: Vector3, id: String, n: int, owner: Node3D) -> Loot:
	var l := Loot.new()
	l.item_id = id
	l.count = n
	l.owner_hero = owner
	parent.add_child(l)
	l.global_position = at
	var a := randf() * TAU
	var r := randf_range(0.6, 1.6)
	l._from = at
	l._to = at + Vector3(cos(a) * r, 0, sin(a) * r)
	l._to.y = Terrain.height_at_world(l._to) if Terrain.current else at.y
	return l


func _ready() -> void:
	var rarity := int(DB.item(item_id).get("rarity", 0))
	var col: Color = RARITY_COLORS[clampi(rarity, 0, 3)]
	_visual = Node3D.new()
	add_child(_visual)
	var mi := MeshInstance3D.new()
	var mat := StandardMaterial3D.new()
	if item_id == "coin":
		var cyl := CylinderMesh.new()
		cyl.top_radius = 0.16
		cyl.bottom_radius = 0.16
		cyl.height = 0.05
		mi.mesh = cyl
		mi.rotation.x = PI * 0.5
		mat.albedo_color = Color(1.0, 0.8, 0.3)
		mat.metallic = 0.9
		mat.roughness = 0.3
		mat.emission_enabled = true
		mat.emission = Color(1.0, 0.7, 0.2)
		mat.emission_energy_multiplier = 0.4
	else:
		var s := SphereMesh.new()
		s.radius = 0.18
		s.height = 0.36
		mi.mesh = s
		mat.albedo_color = col
		mat.emission_enabled = true
		mat.emission = col
		mat.emission_energy_multiplier = 1.2
	mi.material_override = mat
	mi.layers = 2
	_visual.add_child(mi)
	if rarity >= 1:
		var l := OmniLight3D.new()
		l.light_color = col
		l.light_energy = 0.8
		l.omni_range = 2.5
		_visual.add_child(l)


func _physics_process(delta: float) -> void:
	_age += delta
	if _age < 0.45:
		var k := _age / 0.45
		global_position = _from.lerp(_to, k) + Vector3.UP * (0.35 + 3.0 * k * (1.0 - k))
		return
	var hero := owner_hero if owner_hero and is_instance_valid(owner_hero) else _nearest_hero()
	var bob := 0.35 + sin(_age * 4.0) * 0.07
	_visual.rotation.y += delta * 2.0
	if hero and _age > 0.7 and hero.global_position.distance_to(global_position) < MAGNET:
		_speed = minf(14.0, _speed + 30.0 * delta)
		var goal: Vector3 = hero.global_position + Vector3.UP * 0.8
		global_position = global_position.move_toward(goal, _speed * delta)
	else:
		global_position.y = lerpf(global_position.y, _to.y + bob, 0.2)
	if hero and hero.global_position.distance_to(global_position) < PICKUP + 0.8:
		_collect(hero)


func _nearest_hero() -> Node3D:
	var best: Node3D = null
	var best_d := MAGNET
	for h in get_tree().get_nodes_in_group("heroes"):
		if h.is_dead():
			continue
		var d: float = h.global_position.distance_to(global_position)
		if d < best_d:
			best_d = d
			best = h
	return best


func _collect(hero: Node3D) -> void:
	var inv: Inventory = hero.get("inventory")
	if inv == null:
		return
	inv.add(item_id, count)
	if Game.has_screen():
		var it := DB.item(item_id)
		var label := "+%d vàng" % count if item_id == "coin" else "+%d %s" % [count, it.get("displayName", item_id)]
		DamageNumbers.show(hero.global_position + Vector3.UP * 2.0, label, "text")
		if hero == Game.hero:
			Game.log(label, Color(1.0, 0.85, 0.4) if item_id == "coin" else RARITY_COLORS[clampi(int(DB.item(item_id).get("rarity", 0)), 0, 3)])
	queue_free()
