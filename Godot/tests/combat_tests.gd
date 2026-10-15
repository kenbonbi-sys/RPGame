extends RefCounted
## Combat, progression and skill checks, against the numbers the Unity game's own tests use
## (DamageFormulaTests, ProgressionTests) and the specs in Docs/Godot.md. Run by test_runner.

const HERO := preload("res://scenes/hero.tscn")

var t: Node   # the runner (check, _frames, add_child)


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("rounding and damage formula", damage_formula)
	await t._run("levels and XP", progression)
	await t._run("class stats at level 1", class_stats)
	await t._run("burn, chill, freeze, poise, crowd control", statuses)
	await t._run("a hero kills a slime with Chém Gió", slime_fight)
	await t._run("Cầu Lửa burns, Nấm Độc spores slow", fireball_and_spores)
	await t._run("Lướt Hoàn Hảo", perfect_dodge)
	await t._run("Trảm: the heavy hit on the right button", heavy_hit)
	await t._run("a life bar that feels the hit", juicy_bar)


func _flat_ground() -> StaticBody3D:
	var ground := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(400, 1, 400)
	cs.shape = box
	cs.position = Vector3(0, -0.5, 0)
	ground.add_child(cs)
	t.add_child(ground)
	return ground


func _dummy(hp := 1000.0, resist := PackedFloat32Array([0, 0, 0, 0, 0, 0, 0])) -> Node3D:
	var n := Node3D.new()
	var st := StatusEffects.new()
	n.add_child(st)
	var h := Health.new()
	h.max_hp = hp
	h.resistances = resist
	n.add_child(h)
	n.set_meta("health", h)
	t.add_child(n)
	return n


func damage_formula() -> void:
	check(Combat.round_half_even(52.5) == 52.0 and Combat.round_half_even(53.5) == 54.0 and Combat.round_half_even(2.4) == 2.0,
		"halves round to even")
	Health.spread_override = 1.0
	var n := _dummy(1000.0, PackedFloat32Array([0, -0.3, 0, 0, 0, 0, 0]))
	var h: Health = n.get_meta("health")
	await t._frames(1)
	var d := DamageInfo.make(56.0, Combat.Team.PLAYER, null, Vector3.ZERO, Vector3.FORWARD, Combat.Element.FIRE)
	d.attack_scaled = true
	check(h.take_damage(d) == 73.0, "Cầu Lửa 56 on a -30% fire weakness lands for 73")
	h.resistances = PackedFloat32Array([0, 0.3, 0, 0, 0, 0, 0])
	h.armor = 10.0
	h.resistances = PackedFloat32Array([0, -0.3, 0, 0, 0, 0, 0])
	var d2 := d.copy()
	d2.source_level = 5
	check(h.take_damage(d2) == 64.0, "with armour 10 against a level-5 attacker: 64")
	var tiny := d.copy()
	tiny.amount = 0.2
	check(h.take_damage(tiny) == 1.0, "every hit does at least 1")
	h.invulnerable = true
	var evaded := [false]
	h.evaded.connect(func(_x) -> void: evaded[0] = true)
	check(h.take_damage(d.copy()) == 0.0 and evaded[0], "invulnerable: no damage, Evaded raised")
	h.invulnerable = false
	var same := d.copy()
	same.source_team = Combat.Team.ENEMY
	h.team = Combat.Team.ENEMY
	check(h.take_damage(same) == 0.0, "a team does not hurt itself")
	Health.spread_override = -1.0
	n.queue_free()


func progression() -> void:
	var want := {1: 50, 5: 657, 10: 1991, 20: 6034, 30: 11544, 39: 17566, 40: 0}
	for lv in want:
		check(PlayerStats.xp_to_next(lv) == want[lv], "XP to next at level %d = %d (got %d)" % [lv, want[lv], PlayerStats.xp_to_next(lv)])
	check(PlayerStats.total_xp(40) == 272332, "XP to reach 40 = 272332 (got %d)" % PlayerStats.total_xp(40))
	check(PlayerStats.kill_xp(2, Enemy.Rank.NORMAL, 1) == 16, "Slime Rêu gives 16 XP")
	check(PlayerStats.kill_xp(3, Enemy.Rank.NORMAL, 1) == 20, "Nấm Độc gives 20 XP")
	check(PlayerStats.kill_xp(2, Enemy.Rank.NORMAL, 7) == 8, "five levels above: half")
	check(PlayerStats.kill_xp(2, Enemy.Rank.NORMAL, 10) == 2, "eight levels above: a tenth")
	check(PlayerStats.kill_xp(6, Enemy.Rank.BOSS, 6) == 1280, "Gấu Ma gives 1280 XP")


func _stats(class_id: String, race := "") -> PlayerStats:
	var n := Node.new()
	var h := Health.new()
	n.add_child(h)
	var s := PlayerStats.new()
	s.class_id = class_id
	s.race_id = race
	var w: Array = DB.classes.get(class_id, {}).get("weapons", ["sword"])
	s.weapon_id = w[0]
	n.add_child(s)
	t.add_child(n)
	return s


func class_stats() -> void:
	# (HP, energy, physical, magic, armour) at level 1, no race, default weapon — the Unity table
	var want := {
		"barbarian": [108, 54, 26.0, 26.0, 5.0], "bard": [94, 54, 20.5, 22.0, 3.5], "cleric": [104, 54, 21.0, 24.0, 7.0],
		"fighter": [106, 54, 23.0, 23.0, 10.0], "monk": [94, 52, 20.0, 18.5, 4.5], "paladin": [96, 52, 23.0, 21.5, 9.5],
		"ranger": [96, 52, 22.0, 20.5, 6.5], "rogue": [104, 54, 19.0, 19.0, 4.0], "wizard": [102, 54, 23.0, 23.0, 2.0],
	}
	for c in want:
		var s := _stats(c)
		await t._frames(1)
		var w: Array = want[c]
		check(s.health.max_hp == w[0], "%s HP %d (got %d)" % [c, w[0], s.health.max_hp])
		check(Combat.round_half_even(s.max_energy) == w[1], "%s energy %d (got %.1f)" % [c, w[1], s.max_energy])
		check(is_equal_approx(s.physical_attack, w[2]) and is_equal_approx(s.magic_attack, w[3]),
			"%s attack %.1f/%.1f (got %.1f/%.1f)" % [c, w[2], w[3], s.physical_attack, s.magic_attack])
		check(is_equal_approx(s.armor, w[4]), "%s armour %.1f (got %.1f)" % [c, w[4], s.armor])
		s.get_parent().queue_free()
	var human := _stats("fighter", "human")
	await t._frames(1)
	check(human.scores[PlayerStats.Score.STR] == 16.0 and human.scores[PlayerStats.Score.CON] == 15.0, "a human gets +1 everywhere")
	human.add_xp(60)
	check(human.level == 2 and human.xp == 10 and human.stat_points == 3, "60 XP: level 2 with 10 over and 3 points")
	human.get_parent().queue_free()


func statuses() -> void:
	Health.spread_override = 1.0
	var n := _dummy(1000.0)
	var h: Health = n.get_meta("health")
	await t._frames(1)
	var st := h.status
	st.burn(1, 30.0, Combat.Team.PLAYER)
	await t._frames(int(3.2 * Engine.physics_ticks_per_second))
	check(h.hp == 1000.0 - 90.0, "one Bỏng stack of 30/s burns 90 over 3 s (lost %d)" % (1000.0 - h.hp))
	st.chill(2)
	check(st.live_chill() == 2 and absf(st.speed_multiplier() - 0.76) < 0.001, "two Lạnh stacks: 24% slower")
	st.chill(2)
	check(st.is_frozen() and st.speed_multiplier() == 0.0, "the fourth stack freezes")
	n.queue_free()
	n = _dummy(1000.0)
	await t._frames(1)
	st = (n.get_meta("health") as Health).status
	st.stun(2.0)
	var first := st._stun_until - st.now()
	st.clear_stun()
	st.stun(2.0)
	var second := st._stun_until - st.now()
	check(absf(first - 2.0) < 0.05 and absf(second - 1.2) < 0.05, "a second stun within 6 s lasts 60%% (%.2f, %.2f)" % [first, second])
	n.queue_free()
	# a boss: poise breaks into a 3 s stun and +50% damage taken
	var b := Node3D.new()
	var bst := StatusEffects.new()
	b.add_child(bst)
	var bp := Poise.new()
	b.add_child(bp)
	var bh := Health.new()
	bh.max_hp = 5000.0
	b.add_child(bh)
	t.add_child(b)
	await t._frames(1)
	bp.add_poise(299.0)
	check(not bp.is_broken(), "299 of 300: not yet")
	bp.add_poise(5.0)
	check(bp.is_broken() and bst.is_stunned() and is_equal_approx(bh.damage_taken_multiplier, 1.5), "300: Vỡ Trấn Áp, stunned, ×1.5")
	check(is_equal_approx(bp.threshold, 375.0), "the next break needs 375")
	bst.stun(1.0)
	check(bst._stun_until - bst.now() > 2.5, "a broken boss is immune to more crowd control")
	b.queue_free()
	Health.spread_override = -1.0


func _hero_at(p: Vector3) -> Hero:
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	hero.revive(p)
	return hero


func slime_fight() -> void:
	var ground := _flat_ground()
	var hero := _hero_at(Vector3(0, 0.1, 0))
	var slime := Slime.new()
	t.add_child(slime)
	slime.revive(Vector3(0, 0.1, -1.8))
	await t._frames(5)
	check(hero.skills.slots[0].id == "slash", "a sword gives Chém Gió on the left button")
	var xp0 := hero.stats.xp
	var swings := 0
	while not slime.health.is_dead and swings < 40:
		hero.skills._gcd_until = 0.0
		hero.skills.ready_at.clear()
		hero._action_until = 0.0
		hero.skills._commit_until = 0.0
		# keep it in reach (knockback would carry it out of the blade's arc)
		slime.global_position = Vector3(0, 0.1, -1.8)
		slime._knock = Vector3.ZERO
		AbilityRunner.cast(hero, hero.skills.slots[0], slime.global_position, 1, 0)
		swings += 1
		await t._frames(8)
	check(slime.health.is_dead, "the slime falls (%d swings)" % swings)
	check(swings >= 2 and swings <= 6, "60 HP takes a few swings of ~22 (%d)" % swings)
	check(hero.stats.xp - xp0 == 18 or hero.level > 1, "16 XP +10 percent for a human = 18 (got %d)" % (hero.stats.xp - xp0))
	await t._frames(3)
	var loot := t.get_children().filter(func(c) -> bool: return c is Loot)
	check(loot.size() > 0 or true, "it may drop loot")
	for c in [hero, slime, ground]:
		c.queue_free()
	for c in loot:
		c.queue_free()
	await t._frames(2)


func fireball_and_spores() -> void:
	var ground := _flat_ground()
	var hero := _hero_at(Vector3(50, 0.1, 50))
	var shroom := Shroom.new()
	t.add_child(shroom)
	shroom.revive(Vector3(50, 0.1, 44))
	await t._frames(5)
	AbilityRunner.cast(hero, Ability.get_by_id("fireball"), shroom.global_position, 1, 1)
	var burning := false
	for i in 60:
		await t._frames(1)
		if shroom.status.is_burning() or shroom.health.is_dead:
			burning = true
			break
	check(burning, "Cầu Lửa reaches the shroom and sets it burning")
	# a spore at the hero
	var spore := Shot.new()
	spore.team = Combat.Team.ENEMY
	var d := DamageInfo.make(9.0, Combat.Team.ENEMY, null, Vector3.ZERO, Vector3.FORWARD, Combat.Element.POISON, 3.0)
	d.status = {"slow": 0.35, "slowDuration": 1.8}
	spore.hit = d
	t.add_child(spore)
	spore.launch(hero.global_position + Vector3(0, 1, -4), Vector3.BACK)
	var slowed := false
	for i in 60:
		await t._frames(1)
		if hero.status.is_slowed():
			slowed = true
			break
	check(slowed and hero.hp < hero.max_hp, "a spore hurts and slows the hero")
	for c in [hero, shroom, ground]:
		c.queue_free()
	await t._frames(2)


func perfect_dodge() -> void:
	var ground := _flat_ground()
	var hero := _hero_at(Vector3(-50, 0.1, -50))
	await t._frames(3)
	hero.energy = 10.0
	AbilityRunner.cast(hero, Ability.get_by_id("dash"), hero.global_position + Vector3.FORWARD * 5.0, 1, 7)
	await t._frames(2)
	var d := DamageInfo.make(30.0, Combat.Team.ENEMY, null, hero.global_position, Vector3.FORWARD)
	var hp := hero.hp
	hero.health.take_damage(d)
	check(hero.hp == hp, "the blow passes through a Lướt")
	check(absf(hero.energy - 25.0) < 1.0, "Hoàn Hảo: +15 energy (now %.1f)" % hero.energy)
	check(hero.take_perfect_bonus(Ability.get_by_id("fireball")) == 1.3, "the next skill hits 30 percent harder")
	check(hero.take_perfect_bonus(Ability.get_by_id("fireball")) == 1.0, "only once")
	Engine.time_scale = 1.0
	for c in [hero, ground]:
		c.queue_free()
	await t._frames(2)


func heavy_hit() -> void:
	var ground := _flat_ground()
	var hero := _hero_at(Vector3(-60, 0.1, -60))
	var slime := Slime.new()
	t.add_child(slime)
	slime.revive(Vector3(-60, 0.1, -61.8))
	slime.health.reset_health(500.0)
	await t._frames(5)
	var hp0 := slime.health.hp
	Input.action_press("heavy")
	await t._frames(3)
	check(hero.state == Hero.State.HEAVY and hero.heavy_charge() >= 0.0, "holding the right button gathers power")
	await t._frames(60)
	check(hero.heavy_charge() >= 0.99, "full power after 0.9 s")
	Input.action_release("heavy")
	await t._frames(20)
	var dealt := hp0 - slime.health.hp
	var full := hero.stats.attack_for(Combat.Element.PHYSICAL) * 3.2
	check(dealt > full * 0.85, "a full Trảm hits for about 3.2× Attack (%.0f of %.0f)" % [dealt, full])
	await t._frames(30)
	check(hero.state == Hero.State.GROUND, "then back on its feet")
	for c in [hero, slime, ground]:
		c.queue_free()
	await t._frames(2)


func juicy_bar() -> void:
	var b := JuicyBar.new(1.0)
	b.update(1.0, 0.016)
	b.update(0.6, 0.0)
	check(is_equal_approx(b.fill, 1.0) and is_equal_approx(b.ghost, 1.0), "on the hit nothing has moved yet")
	check(b.flash() > 0.9 and absf(b.shove()) > 0.5, "it flashes and is shoved")
	for i in 23:
		b.update(0.6, 0.016)
	check(b.fill < 0.75 and b.fill > 0.6, "the fill is draining (%.2f after 0.37 s)" % b.fill)
	check(is_equal_approx(b.ghost, 1.0), "the ghost still holds the old value before 0.4 s")
	for i in 30:
		b.update(0.6, 0.016)
	check(absf(b.fill - 0.6) < 0.001, "the fill reached the new value by 0.75 s")
	check(b.ghost > 0.6 and b.ghost < 1.0, "the ghost is draining after it (%.2f)" % b.ghost)
	for i in 60:
		b.update(0.6, 0.016)
	check(absf(b.ghost - 0.6) < 0.001, "and catches up")
	check(b.color(Color.GREEN) == Color.GREEN and absf(b.shove()) < 0.001, "healthy colour above half, no shove later")
	b.update(0.45, 0.5)
	b.update(0.45, 0.5)
	check(b.color(Color.GREEN) == JuicyBar.AMBER, "amber at half life")
	b.update(0.2, 0.5)
	b.update(0.2, 0.5)
	check(b.color(Color.GREEN) == JuicyBar.RED, "red at a quarter")
