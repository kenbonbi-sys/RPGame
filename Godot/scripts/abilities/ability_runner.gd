class_name AbilityRunner
extends RefCounted
## Plays a skill's effect blocks exactly as the Unity game's AbilityRunner + Effects do: Cue,
## Damage, Projectile, Dash, Heal, Buff, Line, Burst, Pulse, Combo, Chain, Beam, Orb, Clone.
## Distances in the data are the 2D game's; SPACE stretches them a little for the 3D view.

const SPACE := 1.25


static func cast(caster: Hero, ability: Ability, aim: Vector3, level := 1, slot := 0, damage_multiplier := 1.0) -> AbilityContext:
	var from := caster.global_position
	if ability.targeting() == Ability.Targeting.SELF:
		aim = from
	var flat := aim - from
	flat.y = 0.0
	var ctx := AbilityContext.new()
	ctx.caster = caster
	ctx.ability = ability
	ctx.slot = slot
	ctx.level = clampi(level, 1, 5)
	ctx.origin = from
	ctx.aim = aim
	ctx.dir = flat.normalized() if flat.length_squared() > 0.0001 else caster.facing
	ctx.point = aim
	ctx.damage_multiplier = damage_multiplier
	caster.begin_action(ability, ctx.dir)
	ctx.run(ability.effects())
	return ctx


static func run_block(ctx: AbilityContext, b: Dictionary) -> void:
	match String(b.get("$type", "")):
		"CueEffect": cue(ctx, b)
		"DamageEffect": damage(ctx, b)
		"ProjectileEffect": projectile(ctx, b)
		"DashEffect": dash(ctx, b)
		"HealEffect": heal(ctx, b)
		"BuffEffect": buff(ctx, b)
		"LineEffect": line(ctx, b)
		"BurstEffect": burst(ctx, b)
		"PulseEffect": pulse(ctx, b)
		"ComboEffect": combo(ctx, b)
		"ChainEffect": chain(ctx, b)
		"BeamEffect": beam(ctx, b)
		"OrbEffect": orb(ctx, b)
		"CloneEffect": clone(ctx, b)
		var other:
			push_warning("AbilityRunner: unknown block " + other)


static func _ground(p: Vector3) -> Vector3:
	return Vector3(p.x, Terrain.height_at_world(p) if Terrain.current else p.y, p.z)


static func _local(ctx: AbilityContext) -> bool:
	return ctx.caster == Game.hero


# ------------------------------------------------------------------ presentation
static func cue(ctx: AbilityContext, b: Dictionary) -> void:
	var p := ctx.resolve(b.get("at"))
	var vfx = b.get("vfx")
	if vfx is String and vfx != "":
		SkillFx.play(vfx, p, ctx.dir, float(b.get("vfxScale", 1.0)) * ctx.scale,
			int(b.get("flipOnOddCombo", 0)) == 1 and ctx.combo % 2 == 1)
	if _local(ctx):
		var shake := float(b.get("shake", 0.0))
		if shake > 0.0:
			Game.shake(shake)
		var flash := float(b.get("flashStrength", 0.0))
		if flash > 0.0:
			var c: Dictionary = b.get("flashColor", {"r": 1, "g": 1, "b": 1, "a": 1})
			Game.flash_screen(Color(c.r, c.g, c.b), flash, float(b.get("flashDuration", 0.2)))


# ------------------------------------------------------------------ hitting
static func damage(ctx: AbilityContext, b: Dictionary) -> void:
	var p := ctx.resolve(b.get("at"))
	var r := float(b.get("radius", 1.5)) * ctx.scale * SPACE
	var hit: Dictionary = b.get("hit", {})
	var d := ctx.make_damage(hit)
	d.point = p
	var crit := float(hit.get("critChance", 0.0))
	var hits := 0
	if int(b.get("shape", 0)) == 1:
		hits = Combat.damage_cone(ctx.world(), p + Vector3.UP * 0.6, ctx.dir, r, float(b.get("angle", 120.0)), d, crit)
	else:
		hits = Combat.damage_circle(ctx.world(), p + Vector3.UP * 0.6, r, d, crit)
	_spark_colossus(ctx, p, r)
	if hits > 0:
		ctx.copy_at(p).run(b.get("onAnyHit", []))
		if ctx.slot == 0 and ctx.caster.imbue_power > 0.0:
			SkillFx.play("hit_lightning", p, ctx.dir)


## A blade on a colossus's hide only sparks (only stabbing its weak points hurts it).
static func _spark_colossus(ctx: AbilityContext, p: Vector3, r: float) -> void:
	var q := PhysicsShapeQueryParameters3D.new()
	var s := SphereShape3D.new()
	s.radius = r
	q.shape = s
	q.transform = Transform3D(Basis.IDENTITY, p + Vector3.UP)
	q.collision_mask = Combat.LAYER_COLOSSUS
	for h in ctx.world().direct_space_state.intersect_shape(q, 4):
		var col = h.collider.get_meta("colossus") if h.collider.has_meta("colossus") else null
		if col and col.has_method("hide_hit"):
			col.hide_hit(p + Vector3.UP * 1.2)
			return


static func projectile(ctx: AbilityContext, b: Dictionary) -> void:
	var from := ctx.resolve(b.get("spawn")) + Vector3.UP * 1.1
	var dir := ctx.dir.rotated(Vector3.UP, deg_to_rad(float(b.get("angle", 0.0))))
	var hit: Dictionary = b.get("hit", {})
	var d := ctx.make_damage(hit)
	var shot := Shot.new()
	shot.team = Combat.Team.PLAYER
	shot.source = ctx.caster
	shot.speed = float(b.get("speed", 10.0)) * SPACE
	var life := float(b.get("lifetime", 0.0))
	shot.lifetime = life if life > 0.0 else ctx.ability.max_range() / shot.speed
	shot.radius = 0.3
	shot.explode_radius = float(b.get("explodeRadius", 0.0)) * SPACE
	shot.pierce = int(b.get("pierce", 0)) == 1
	shot.crit_chance = float(hit.get("critChance", 0.0))
	shot.color = Combat.ELEMENT_COLORS[clampi(d.type, 0, 6)] if d.type != Combat.Element.PHYSICAL else Color(0.95, 0.92, 0.8)
	shot.size = 0.22
	shot.hit = d
	var hit_vfx = b.get("hitVfx")
	var hit_shake := float(b.get("hitShake", 0.1))
	var local := _local(ctx)
	shot.on_hit = func(at: Vector3) -> void:
		if hit_vfx is String:
			SkillFx.play(hit_vfx, at - Vector3.UP * 0.8, dir)
		if local and hit_shake > 0.0:
			Game.shake(hit_shake)
	ctx.caster.get_parent().add_child(shot)
	shot.launch(from, dir)


static func chain(ctx: AbilityContext, b: Dictionary) -> void:
	var range_ := float(b.get("range", 8.0)) * SPACE
	var jump := float(b.get("jumpRange", 4.5)) * SPACE
	var hit: Dictionary = b.get("hit", {})
	var caster_p := ctx.caster.global_position
	var first: Health = null
	var best := INF
	for h in ctx.enemies_near(caster_p, range_):
		var dd := h.global_position.distance_to(ctx.aim)
		if dd < best and not ctx.line_blocked(caster_p, h.global_position):
			best = dd
			first = h
	var from := caster_p + Vector3.UP * 1.3
	if first == null:
		zap(from, ctx.aim + Vector3.UP, SkillFx.BOLT)
		return
	var struck: Array[Health] = []
	var target := first
	for i in int(b.get("jumps", 4)) + 1:
		if target == null:
			break
		struck.append(target)
		var to := target.head_position() - Vector3.UP * 0.4
		zap(from, to, SkillFx.BOLT)
		var d := ctx.make_damage(hit)
		d.amount *= maxf(0.1, 1.0 - float(b.get("falloff", 0.15)) * i)
		d.point = to
		var away := target.global_position - from
		away.y = 0.0
		d.direction = away.normalized() if away.length() > 0.01 else ctx.dir
		d.roll_crit(float(hit.get("critChance", 0.0)))
		if target.take_damage(d) > 0.0:
			Combat.on_hit_feedback(target, d)
		from = to
		var next: Health = null
		var nd := jump
		for h in ctx.enemies_near(target.global_position, jump):
			if h in struck:
				continue
			var dd := h.global_position.distance_to(target.global_position)
			if dd < nd and not ctx.line_blocked(target.global_position, h.global_position):
				nd = dd
				next = h
		target = next
		await ctx.wait(float(b.get("jumpDelay", 0.07)))


## A jagged bolt between two points.
static func zap(a: Vector3, b: Vector3, col: Color) -> void:
	if not Game.has_screen():
		return
	var root := Node3D.new()
	SkillFx._world().add_child(root)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(col.lerp(Color.WHITE, 0.4), 1.0)
	var segs := 5
	var prev := a
	for i in segs:
		var t := float(i + 1) / segs
		var p := a.lerp(b, t)
		if i < segs - 1:
			p += Vector3(randf_range(-0.3, 0.3), randf_range(-0.3, 0.3), randf_range(-0.3, 0.3))
		var mi := MeshInstance3D.new()
		var bm := BoxMesh.new()
		bm.size = Vector3(0.07, prev.distance_to(p), 0.07)
		mi.mesh = bm
		mi.material_override = mat
		root.add_child(mi)
		mi.global_transform = Transform3D(GauMa._axis_basis((p - prev).normalized()), (p + prev) * 0.5)
		prev = p
	Fx.flash(b, col, 3.0, 5.0, 0.15)
	var tw := root.create_tween()
	tw.tween_property(mat, "albedo_color:a", 0.0, 0.16)
	tw.tween_callback(root.queue_free)


static func beam(ctx: AbilityContext, b: Dictionary) -> void:
	var duration := float(b.get("duration", 2.0))
	var interval := float(b.get("interval", 0.2))
	var range_ := float(b.get("range", 7.0)) * SPACE
	var hit: Dictionary = b.get("hit", {})
	var lifesteal := float(b.get("lifesteal", 0.3))
	var c: Dictionary = b.get("color", {"r": 0.8, "g": 0.2, "b": 0.45, "a": 1})
	var col := Color(c.r, c.g, c.b)
	var target: Health = null
	var ticks := int(ceil(duration / interval))
	for i in ticks:
		if not ctx.alive():
			return
		var cp := ctx.caster.global_position
		if target == null or target.is_dead or target.global_position.distance_to(cp) > range_ + 1.0:
			target = null
			var best := INF
			for h in ctx.enemies_near(cp, range_):
				var dd := h.global_position.distance_to(ctx.aim)
				if dd < best and not ctx.line_blocked(cp, h.global_position):
					best = dd
					target = h
		if target:
			var to := target.head_position() - Vector3.UP * 0.4
			zap(cp + Vector3.UP * 1.3, to, col)
			var d := ctx.make_damage(hit)
			d.point = to
			d.roll_crit(float(hit.get("critChance", 0.0)))
			var dealt := target.take_damage(d)
			if dealt > 0.0:
				Combat.on_hit_feedback(target, d)
				ctx.caster.health.heal(dealt * lifesteal)
		await ctx.wait(interval)


static func orb(ctx: AbilityContext, b: Dictionary) -> void:
	var p := ctx.resolve(b.get("spawn")) + Vector3.UP * 1.1
	var speed := float(b.get("speed", 3.0)) * SPACE
	var duration := float(b.get("duration", 2.6))
	var radius := float(b.get("radius", 2.0)) * SPACE
	var interval := float(b.get("interval", 0.3))
	var tick: Dictionary = b.get("tick", {})
	var ball := MeshInstance3D.new()
	ball.mesh = CreatureArt.sphere(0.45, 16)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = SkillFx.BOLT
	mat.emission_enabled = true
	mat.emission = SkillFx.BOLT
	mat.emission_energy_multiplier = 4.0
	ball.material_override = mat
	var light := OmniLight3D.new()
	light.light_color = SkillFx.BOLT
	light.light_energy = 2.0
	light.omni_range = radius * 1.5
	ball.add_child(light)
	SkillFx._world().add_child(ball)
	ball.global_position = p
	var t := 0.0
	var next_zap := 0.15
	while t < duration and ctx.alive():
		var dt := ctx.tree().root.get_physics_process_delta_time()
		var step := ctx.dir * speed * dt
		var q := PhysicsRayQueryParameters3D.create(p, p + step * 3.0, Combat.LAYER_WORLD)
		if not ctx.world().direct_space_state.intersect_ray(q).is_empty():
			break
		p += step
		ball.global_position = p
		t += dt
		if t >= next_zap:
			next_zap += interval
			for h in ctx.enemies_near(p - Vector3.UP * 1.1, radius):
				zap(p, h.head_position() - Vector3.UP * 0.4, SkillFx.BOLT)
				var d := ctx.make_damage(tick)
				d.point = h.head_position()
				h.take_damage(d)
		await ctx.tree().physics_frame
	ball.queue_free()
	if ctx.alive():
		var burst: Dictionary = b.get("burst", {})
		var d := ctx.make_damage(burst)
		d.point = p
		Combat.damage_circle(ctx.world(), p, float(b.get("burstRadius", 2.6)) * SPACE, d, float(burst.get("critChance", 0.0)))
		SkillFx.play("lightning_burst", p - Vector3.UP * 1.1, ctx.dir, 1.6)


static func clone(ctx: AbilityContext, b: Dictionary) -> void:
	var p := ctx.resolve(b.get("at"))
	if ctx.line_blocked(ctx.caster.global_position, p):
		p = ctx.caster.global_position
	p = _ground(p)
	var c: Dictionary = b.get("tint", {"r": 0.45, "g": 0.25, "b": 0.7, "a": 0.85})
	var shade := MeshInstance3D.new()
	shade.mesh = CreatureArt.capsule(0.35, 1.8)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(c.r, c.g, c.b, 0.6)
	shade.material_override = mat
	SkillFx._world().add_child(shade)
	shade.global_position = p + Vector3.UP * 0.9
	var duration := float(b.get("duration", 8.0))
	var interval := float(b.get("interval", 0.8))
	var reach := float(b.get("reach", 2.4)) * SPACE
	var hit: Dictionary = b.get("hit", {})
	await ctx.wait(0.4)
	var t := 0.4
	while t < duration and ctx.alive():
		var best: Health = null
		var bd := INF
		for h in ctx.enemies_near(p, reach):
			var dd := h.global_position.distance_to(p)
			if dd < bd:
				bd = dd
				best = h
		if best:
			var d := ctx.make_damage(hit)
			d.point = best.head_position()
			var away := best.global_position - p
			away.y = 0.0
			d.direction = away.normalized() if away.length() > 0.01 else ctx.dir
			d.roll_crit(float(hit.get("critChance", 0.0)))
			if best.take_damage(d) > 0.0:
				Combat.on_hit_feedback(best, d)
			SkillFx.arc(p + Vector3.UP, away, 2.0, false, false)
		await ctx.wait(interval)
		t += interval
	if ctx.alive():
		var burst: Dictionary = b.get("burst", {})
		if float(burst.get("power", 0.0)) > 0.0:
			var d := ctx.make_damage(burst)
			d.point = p
			Combat.damage_circle(ctx.world(), p + Vector3.UP * 0.6, float(b.get("burstRadius", 2.2)) * SPACE, d, float(burst.get("critChance", 0.0)))
			SkillFx.play("dark_strike", p, ctx.dir, 1.5)
	shade.queue_free()


# ------------------------------------------------------------------ the caster
static func dash(ctx: AbilityContext, b: Dictionary) -> void:
	var dir := ctx.dir
	var move := ctx.caster.move_dir()
	if int(b.get("preferMoveDirection", 0)) == 1 and move.length() > 0.3:
		dir = move.normalized()
	if int(b.get("away", 0)) == 1:
		dir = -ctx.dir
	var time := float(b.get("time", 0.16))
	var dist := float(b.get("distance", 4.0)) * SPACE
	var style := "roll" if ctx.ability.id == "dash" else ("blink" if time <= 0.06 else "charge")
	SkillFx.play(String(b.get("startVfx", "")) if b.get("startVfx") else "", ctx.caster.global_position, dir)
	ctx.caster.dash(dir, dist, time, float(b.get("invulnerableTime", 0.28)), int(b.get("perfectDodge", 0)) == 1, style)
	var end_vfx = b.get("endVfx")
	if end_vfx is String and end_vfx != "":
		await ctx.wait(time)
		SkillFx.play(end_vfx, ctx.caster.global_position, dir, float(b.get("endVfxScale", 1.0)))


static func _allies(ctx: AbilityContext, radius: float) -> Array:
	var out := [ctx.caster]
	if radius > 0.0:
		for h in ctx.tree().get_nodes_in_group("heroes"):
			if h != ctx.caster and not h.is_dead() and h.global_position.distance_to(ctx.caster.global_position) <= radius * SPACE:
				out.append(h)
	return out


static func heal(ctx: AbilityContext, b: Dictionary) -> void:
	var power := ctx.caster.stats.healing_power
	var targets := _allies(ctx, float(b.get("allies", 0.0)))
	for t in targets:
		var amount: float = (float(b.get("instant", 0.0)) + float(b.get("instantPercentOfMaxHp", 0.0)) * t.health.max_hp) * power
		if amount > 0.0:
			t.health.heal(amount)
	var per_s := float(b.get("perSecond", 0.0))
	var per_pct := float(b.get("perSecondPercentOfMaxHp", 0.0))
	var duration := float(b.get("duration", 0.0))
	if (per_s <= 0.0 and per_pct <= 0.0) or duration <= 0.0:
		return
	var acc := 0.0
	var t := 0.0
	while t < duration and ctx.alive():
		await ctx.wait(0.5)
		t += 0.5
		acc += 0.5
		for target in targets:
			if is_instance_valid(target) and not target.is_dead():
				target.health.heal((per_s + per_pct * target.health.max_hp) * power * acc, false)
		acc = 0.0


static func buff(ctx: AbilityContext, b: Dictionary) -> void:
	var spec: Dictionary = b.get("buff", {})
	for t in _allies(ctx, float(b.get("allies", 0.0))):
		if bool(int(spec.get("clearStun", 0))):
			t.status.clear_stun()
		t.add_buff(spec)


# ------------------------------------------------------------------ flows
static func line(ctx: AbilityContext, b: Dictionary) -> void:
	var count := int(b.get("count", 5))
	for i in count:
		if not ctx.alive():
			return
		var jitter := float(b.get("jitter", 0.0)) * SPACE
		var p := ctx.origin + ctx.dir * (float(b.get("startOffset", 1.0)) + i * float(b.get("spacing", 1.0))) * SPACE
		p += Enemy._random_in_disc(jitter)
		if int(b.get("stopAtWalls", 1)) == 1 and ctx.line_blocked(ctx.origin, p):
			return
		ctx.copy_at(_ground(p), float(b.get("startScale", 1.0)) + i * float(b.get("scaleStep", 0.0))).run(b.get("each", []))
		await ctx.wait(float(b.get("interval", 0.06)))


static func burst(ctx: AbilityContext, b: Dictionary) -> void:
	var c := _ground(ctx.resolve(b.get("center")))
	var radius := float(b.get("radius", 3.0)) * SPACE
	var count := int(b.get("count", 6))
	var charge := float(b.get("chargeTime", 0.4))
	var interval := float(b.get("interval", 0.14))
	var jitter := float(b.get("intervalJitter", 0.3))
	var area_vfx = b.get("areaVfx")
	if area_vfx is String and area_vfx != "":
		SkillFx.ring(c, SkillFx.color_for(area_vfx), radius * float(b.get("areaVfxScale", 1.0)), charge + count * interval + 0.2)
	await ctx.wait(charge)
	for i in count:
		if not ctx.alive():
			return
		var p := c + Enemy._random_in_disc(0.85 * radius)
		var near := ctx.enemies_near(c, radius)
		if near.size() > 0 and randf() < float(b.get("preferTargets", 0.75)):
			p = near.pick_random().global_position + Enemy._random_in_disc(0.2)
		if i == 0 and int(b.get("firstAtCenter", 1)) == 1:
			p = c
		ctx.copy_at(_ground(p)).run(b.get("each", []))
		await ctx.wait(interval * randf_range(1.0 - jitter, 1.0 + jitter))


static func pulse(ctx: AbilityContext, b: Dictionary) -> void:
	var duration := float(b.get("duration", 3.0))
	var interval := float(b.get("interval", 0.25))
	var at = b.get("at")
	var follows := at is Dictionary and int(at.get("from", 0)) == 0
	var aura = b.get("attachedVfx")
	var ring: Node3D = null
	if aura is String and aura != "" and Game.has_screen():
		ring = SkillFx.ring(ctx.resolve(at), SkillFx.color_for(aura), 2.4 * SPACE, duration)
	var pulses := int(ceil(duration / interval))
	for i in pulses:
		if not ctx.alive():
			break
		var p := ctx.resolve(at)
		if ring and is_instance_valid(ring) and follows:
			ring.global_position = ctx.caster.global_position
		ctx.copy_at(p).run(b.get("each", []))
		await ctx.wait(interval)


static func combo(ctx: AbilityContext, b: Dictionary) -> void:
	var stages: Array = b.get("stages", [])
	if stages.is_empty():
		return
	var key := ctx.ability.id
	var st: Dictionary = ctx.caster.combo_state.get(key, {"count": -1, "last": -100.0})
	var now := Game.now()
	var count := int(st.count) + 1 if now - float(st.last) <= float(b.get("window", 0.9)) else 0
	ctx.caster.combo_state[key] = {"count": count, "last": now}
	ctx.combo = count
	ctx.caster.combo_step = count % stages.size()
	ctx.run((stages[count % stages.size()] as Dictionary).get("effects", []))
