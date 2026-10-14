class_name Aim
extends RefCounted
## Where a skill goes in the 3D view (the Unity game aimed at the mouse cursor). Direction skills
## follow the camera, bending toward a monster close to the line of sight (a soft lock); point
## skills land where the middle of the screen meets the ground, or on the soft-locked monster;
## all of it clamped to the skill's range.

const SOFT_LOCK_DEGREES := 32.0


static func point_for(hero: Hero, a: Ability) -> Vector3:
	var from := hero.global_position
	var range_ := a.max_range()
	var cam := Game.cam()
	var fwd := cam.forward_flat() if cam else hero.facing
	var target := soft_target(hero, fwd, range_)
	var p: Vector3
	if a.targeting() == Ability.Targeting.POINT:
		if target:
			p = target.global_position
		else:
			p = _screen_ground(hero, range_)
	else:
		p = target.global_position if target else from + fwd * minf(range_, 6.0)
	var off := p - from
	off.y = 0.0
	if off.length() < 0.1:
		p = from + hero.facing * 0.1
	elif off.length() > range_:
		p = from + off.normalized() * range_
	p.y = Terrain.height_at_world(p) if Terrain.current else from.y
	return p


## The nearest living monster within range and within a narrow cone of the camera's view.
static func soft_target(hero: Hero, fwd: Vector3, range_: float) -> Health:
	var best: Health = null
	var best_score := INF
	for h in Combat.healths_in_sphere(hero.get_world_3d(), hero.global_position + Vector3.UP, range_, Combat.Team.PLAYER):
		var to := h.global_position - hero.global_position
		to.y = 0.0
		if to.length() < 0.1:
			return h
		var ang := rad_to_deg(fwd.angle_to(to.normalized()))
		if ang > SOFT_LOCK_DEGREES:
			continue
		var score := ang * 0.15 + to.length()
		if score < best_score:
			best_score = score
			best = h
	return best


static func _screen_ground(hero: Hero, range_: float) -> Vector3:
	var cam := Game.cam()
	if cam == null or cam.cam == null:
		return hero.global_position + hero.facing * range_ * 0.6
	var c := cam.cam
	var from := c.global_position
	var dir := -c.global_basis.z
	var q := PhysicsRayQueryParameters3D.create(from, from + dir * (range_ + 30.0), Combat.LAYER_WORLD | Combat.LAYER_ENEMY)
	var hit := hero.get_world_3d().direct_space_state.intersect_ray(q)
	if hit.is_empty():
		var flat := Vector3(dir.x, 0, dir.z).normalized()
		return hero.global_position + flat * range_ * 0.7
	return hit.position
