class_name AbilityContext
extends RefCounted
## One cast in flight (the Unity AbilityContext): who cast what, from where, toward where, at
## which level, with which damage bonus; the point a nested block is working on; the size scale a
## Line gives its points; the combo count. Runs a list of effect blocks, each after its delay.

var caster: Hero
var ability: Ability
var slot := 0
var level := 1
var origin := Vector3.ZERO
var aim := Vector3.ZERO
var dir := Vector3.FORWARD
var point := Vector3.ZERO
var damage_multiplier := 1.0
var scale := 1.0
var combo := 0

## Casts with blocks still waiting on a delay. A waiting coroutine does not keep its RefCounted
## owner alive, so without this a cast nobody holds on to would vanish with its delayed hits.
static var _live: Array[AbilityContext] = []


func copy_at(p: Vector3, new_scale := -1.0) -> AbilityContext:
	var c := AbilityContext.new()
	c.caster = caster
	c.ability = ability
	c.slot = slot
	c.level = level
	c.origin = origin
	c.aim = aim
	c.dir = dir
	c.point = p
	c.damage_multiplier = damage_multiplier
	c.scale = scale if new_scale < 0.0 else new_scale
	c.combo = combo
	return c


func alive() -> bool:
	return is_instance_valid(caster) and not caster.is_dead()


func tree() -> SceneTree:
	return caster.get_tree()


func world() -> World3D:
	return caster.get_world_3d()


func team() -> int:
	return Combat.Team.PLAYER


## Anchor {from: Caster 0 | Point 1 | Aim 2 | Origin 3, up, forward} -> a ground point. The Unity
## "up" was a northward nudge standing in for chest height; here height is added where needed.
func resolve(anchor) -> Vector3:
	if not (anchor is Dictionary):
		return point
	var base: Vector3
	match int(anchor.get("from", 0)):
		0: base = caster.global_position
		1: base = point
		2: base = aim
		_: base = origin
	return base + dir * float(anchor.get("forward", 0.0)) * AbilityRunner.SPACE


## A hit worth `hit.power` × Attack (+ skill level, the hero's damage bonuses, Lôi Ấn on the basic attack).
func make_damage(hit: Dictionary) -> DamageInfo:
	var power := float(hit.get("power", 1.0))
	var status: Dictionary = (hit.get("status", {}) as Dictionary).duplicate()
	if slot == 0 and caster.imbue_power > 0.0:
		power *= 1.0 + caster.imbue_power
		status["charge"] = int(status.get("charge", 0)) + caster.imbue_charge
	var type := int(hit.get("type", 0))
	var weapon_hit := slot == 0 and caster.has_method("weapon_element_type")
	if weapon_hit and type == Combat.Element.PHYSICAL:
		type = caster.weapon_element_type()
	var amount := power * ability.power_scale(level) * caster.stats.attack_for(type) * caster.damage_dealt_multiplier() * damage_multiplier
	var d := DamageInfo.make(amount, Combat.Team.PLAYER, caster, point, dir, type, float(hit.get("knockback", 0.0)))
	d.attack_scaled = true
	d.source_level = caster.level
	d.skill_name = ability.display_name()
	d.poise = float(hit.get("poise", 0.0))
	d.hit_stop = float(hit.get("hitStop", 0.0))
	d.status = status
	d.poise += caster.counter_poise(d.skill_name)
	if weapon_hit:
		d.from_weapon = true
		d.poise *= caster.weapon_stagger()
		var fam: Dictionary = caster.weapon_family()
		d.cutting = fam.get("cut", false)
		d.piercing = fam.get("pierce", false)
	return d


func run(blocks) -> void:
	if not (blocks is Array):
		return
	for b in blocks:
		if not (b is Dictionary):
			continue
		var delay := float(b.get("delay", 0.0))
		if delay > 0.0:
			_run_later(b, delay)
		else:
			AbilityRunner.run_block(self, b)


func _run_later(b: Dictionary, delay: float) -> void:
	_live.append(self)
	await tree().create_timer(delay, false, true).timeout
	_live.erase(self)
	if alive():
		AbilityRunner.run_block(self, b)


func wait(seconds: float) -> void:
	await tree().create_timer(maxf(0.0, seconds), false, true).timeout


func enemies_near(p: Vector3, r: float) -> Array[Health]:
	return Combat.healths_in_sphere(world(), p + Vector3.UP * 0.5, r, team())


func line_blocked(a: Vector3, b: Vector3) -> bool:
	var q := PhysicsRayQueryParameters3D.create(a + Vector3.UP * 0.8, b + Vector3.UP * 0.8, Combat.LAYER_WORLD)
	return not world().direct_space_state.intersect_ray(q).is_empty()
