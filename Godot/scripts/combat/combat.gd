class_name Combat
extends RefCounted
## Shared combat rules (the Unity game's Combat.cs): teams, elements, area hits, hit feedback
## and hit-stop. Numbers come from DB.combat / DB.progression.

enum Team { PLAYER, ENEMY, NEUTRAL }
## Same order as the Unity DamageType enum (the JSON stores these numbers).
enum Element { PHYSICAL, FIRE, ICE, LIGHTNING, POISON, HOLY, DARK }

const ELEMENT_NAMES := ["Vật lý", "Lửa", "Băng", "Lôi", "Độc", "Thánh Mộc", "Ám"]
const ELEMENT_COLORS := [Color(1, 1, 1), Color(1.0, 0.52, 0.2), Color(0.55, 0.85, 1.0), Color(0.72, 0.78, 1.0),
	Color(0.6, 0.95, 0.3), Color(1.0, 0.92, 0.55), Color(0.72, 0.45, 0.95)]

const LAYER_WORLD := 1
const LAYER_HERO := 2
const LAYER_ENEMY := 4
const LAYER_COLOSSUS := 8


## Who can hurt whom: a team never hurts itself; neutral things only take hits from heroes.
static func can_hurt(attacker_team: int, target_team: int) -> bool:
	if attacker_team == target_team:
		return false
	if target_team == Team.NEUTRAL:
		return attacker_team == Team.PLAYER
	return true


## Mathf.Round: halves go to the even number (2.5 -> 2, 3.5 -> 4).
static func round_half_even(x: float) -> float:
	var f := floorf(x)
	var diff := x - f
	if diff > 0.5:
		return f + 1.0
	if diff < 0.5:
		return f
	return f if int(f) % 2 == 0 else f + 1.0


## The Health of whatever a physics query found (bodies carry `health`, or a "health" meta).
static func health_of(n: Object) -> Health:
	var node := n as Node
	while node:
		var h = node.get("health")
		if h is Health:
			return h
		if node.has_meta("health"):
			return node.get_meta("health")
		if node is Health:
			return node
		node = node.get_parent()
		if node is Window:
			break
	return null


## Every living Health within `radius` of `center` that `team` may hurt (heroes and enemies).
static func healths_in_sphere(world: World3D, center: Vector3, radius: float, team: int, exclude: Array = []) -> Array[Health]:
	var q := PhysicsShapeQueryParameters3D.new()
	var s := SphereShape3D.new()
	s.radius = radius
	q.shape = s
	q.transform = Transform3D(Basis.IDENTITY, center)
	q.collision_mask = LAYER_HERO | LAYER_ENEMY
	q.collide_with_areas = true
	var out: Array[Health] = []
	for r in world.direct_space_state.intersect_shape(q, 64):
		var h := health_of(r.collider)
		if h == null or h in out or h in exclude or h.is_dead or not can_hurt(team, h.team):
			continue
		out.append(h)
	return out


## Hits everyone in a circle: crit rolled per target, knockback away from the centre.
static func damage_circle(world: World3D, center: Vector3, radius: float, template: DamageInfo,
		crit_chance := 0.0, feedback := true) -> int:
	var hits := 0
	for h in healths_in_sphere(world, center, radius, template.source_team):
		var d := template.copy()
		d.point = h.head_position()
		var away := h.global_position - center
		away.y = 0.0
		if away.length() > 0.03:
			d.direction = away.normalized()
		d.roll_crit(crit_chance)
		d.feedback = feedback
		if h.take_damage(d) > 0.0:
			hits += 1
			if feedback:
				on_hit_feedback(h, d)
	return hits


## Hits everyone in a cone (half angle = angle/2) in front of `origin`; anyone closer than 0.6 m is hit.
static func damage_cone(world: World3D, origin: Vector3, dir: Vector3, radius: float, angle_deg: float,
		template: DamageInfo, crit_chance := 0.0) -> int:
	var hits := 0
	var flat_dir := Vector3(dir.x, 0, dir.z).normalized()
	for h in healths_in_sphere(world, origin, radius, template.source_team):
		var to := h.global_position - origin
		to.y = 0.0
		if to.length() > 0.6 and rad_to_deg(flat_dir.angle_to(to.normalized())) > angle_deg * 0.5:
			continue
		var d := template.copy()
		d.point = h.head_position()
		d.direction = to.normalized() if to.length() > 0.03 else flat_dir
		d.roll_crit(crit_chance)
		if h.take_damage(d) > 0.0:
			hits += 1
			on_hit_feedback(h, d)
	return hits


## What a landed hit looks and feels like: knockback, a flash, sparks, hit-stop.
static func on_hit_feedback(h: Health, d: DamageInfo) -> void:
	var owner := h.owner_body()
	if d.knockback > 0.0 and owner and owner.has_method("add_knockback"):
		owner.add_knockback(d.direction * d.knockback)
	show_hit(h, d)


static func show_hit(h: Health, d: DamageInfo) -> void:
	var owner := h.owner_body()
	if owner and owner.has_method("flash"):
		owner.flash(Color(1, 0.95, 0.6) if d.crit else Color.WHITE, 1.0, 0.12)
	var col: Color = ELEMENT_COLORS[clampi(d.type, 0, ELEMENT_COLORS.size() - 1)]
	Fx.burst(d.point, -d.direction + Vector3.UP * 0.5, col, 10, 5.0, 0.18, 0.35, 70.0, -8.0, true)
	if d.crit and d.source_team == Team.PLAYER:
		Game.shake(0.12)
	var stop := hit_stop_for(h, d)
	if stop > 0.0:
		Game.hitstop(stop)


## Hero hits: crits stop a little longer, kills at least a light stop (a heavy one on a boss).
static func hit_stop_for(h: Health, d: DamageInfo) -> float:
	var s := d.hit_stop
	if d.source_team != Team.PLAYER or h == null:
		return s
	if d.crit and s > 0.0:
		s = maxf(s, DB.c("hitStopHeavy", 0.07))
	if h.is_dead:
		s = maxf(s, DB.c("hitStopBreak", 0.12) if h.poise else DB.c("hitStopLight", 0.035))
	return s
