extends RefCounted
## Dauntless weapons and elements: matchups, the six procs on small monsters and on a colossus,
## every family's basic combo and heavy move. Run by test_runner.

const HERO := preload("res://scenes/hero.tscn")

var t: Node


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("elements: strong, weak and neutral", matchups)
	await t._run("elements: the six procs on a small monster", procs)
	await t._run("elements: procs on Gấu Ma", colossus_procs)
	await t._run("weapons: every family in hand", families)
	await t._run("weapons: every family's heavy move lands", heavies)


func _dummy(element := "", hp := 1000.0) -> Node3D:
	var n := Node3D.new()
	var st := StatusEffects.new()
	n.add_child(st)
	var h := Health.new()
	h.max_hp = hp
	h.element = element
	n.add_child(h)
	n.set_meta("health", h)
	t.add_child(n)
	return n


func _hit(h: Health, amount: float, type: int, weapon := true) -> float:
	var d := DamageInfo.make(amount, Combat.Team.PLAYER, null, h.head_position(), Vector3.FORWARD, type)
	d.from_weapon = weapon
	return h.take_damage(d)


func matchups() -> void:
	check(is_equal_approx(Elements.matchup(Combat.Element.LIGHTNING, "terra"), 1.5), "Shock on Terra +50%")
	check(is_equal_approx(Elements.matchup(Combat.Element.TERRA, "shock"), 1.5), "Terra on Shock +50%")
	check(is_equal_approx(Elements.matchup(Combat.Element.FIRE, "frost"), 1.5), "Blaze on Frost +50%")
	check(is_equal_approx(Elements.matchup(Combat.Element.DARK, "radiant"), 1.5), "Umbral on Radiant +50%")
	check(is_equal_approx(Elements.matchup(Combat.Element.TERRA, "terra"), 0.8), "Terra on Terra −20%")
	check(is_equal_approx(Elements.matchup(Combat.Element.FIRE, "terra"), 1.0), "Blaze on Terra plain")
	check(is_equal_approx(Elements.matchup(Combat.Element.PHYSICAL, "terra"), 1.0), "neutral plain")
	Health.spread_override = 1.0
	var n := _dummy("terra")
	var h: Health = n.get_meta("health")
	await t._frames(1)
	check(is_equal_approx(_hit(h, 100.0, Combat.Element.LIGHTNING, false), 150.0), "a Shock blow on a Terra monster deals 150 of 100")
	check(is_equal_approx(_hit(h, 100.0, Combat.Element.TERRA, false), 80.0), "a Terra blow on it deals 80")
	n.queue_free()
	Health.spread_override = -1.0
	await t._frames(1)


func procs() -> void:
	Health.spread_override = 1.0
	# threshold: 12% of 1000 = 120 elemental damage from weapon blows
	var n := _dummy()
	var h: Health = n.get_meta("health")
	await t._frames(1)
	_hit(h, 100.0, Combat.Element.FIRE)
	check(not h.status.is_burning(), "below the threshold nothing happens")
	_hit(h, 30.0, Combat.Element.FIRE)
	check(h.status.is_burning(), "Blaze at the threshold: it burns")
	_hit(h, 130.0, Combat.Element.ICE)
	check(h.status.speed_multiplier() < 0.7, "Frost: it is slowed (%.2f)" % h.status.speed_multiplier())
	_hit(h, 130.0, Combat.Element.LIGHTNING)
	check(h.status.is_stunned(), "Shock: it is paralysed")
	var before := t.get_tree().get_nodes_in_group("heal_orbs").size()
	_hit(h, 130.0, Combat.Element.TERRA)
	await t._frames(1)
	check(t.get_tree().get_nodes_in_group("heal_orbs").size() == before + 3, "Terra: three healing orbs")
	_hit(h, 130.0, Combat.Element.HOLY)
	check(RadiantMark.on(h) != null, "Radiant: a mark over it")
	_hit(h, 130.0, Combat.Element.DARK)
	check(h.get_children().any(func(c) -> bool: return c is UmbralBlast), "Umbral: a blast swelling in it")
	_hit(h, 130.0, Combat.Element.FIRE)
	check(float(h.proc_meter.get("blaze", 0.0)) == 0.0 or Game.now() < float(h.proc_ready_at.get("blaze", 0.0)),
		"an element cannot proc again for a while")
	var skill := _dummy()
	var hs: Health = skill.get_meta("health")
	await t._frames(1)
	_hit(hs, 500.0, Combat.Element.FIRE, false)
	check(not hs.status.is_burning(), "only weapon blows proc (skills keep their own statuses)")
	var same := _dummy("blaze")
	var hb: Health = same.get_meta("health")
	await t._frames(1)
	_hit(hb, 500.0, Combat.Element.FIRE)
	check(not hb.status.is_burning(), "an element never procs on its own kind")
	for c in t.get_tree().get_nodes_in_group("heal_orbs"):
		c.queue_free()
	for x in [n, skill, same]:
		x.queue_free()
	Health.spread_override = -1.0
	await t._frames(2)


func _bear() -> GauMa:
	var bear := GauMa.new()
	bear.scripted = true
	t.add_child(bear)
	bear.reset_to(Transform3D.IDENTITY)
	return bear


func colossus_procs() -> void:
	Health.spread_override = 1.0
	var bear := _bear()
	await t._frames(2)
	check(bear.health.element == "terra", "Gấu Ma is Terra")
	var at: Vector3 = bear.legs[0].lower.global_position
	var shock := DamageInfo.make(400.0, Combat.Team.PLAYER, null, at, Vector3.FORWARD, Combat.Element.LIGHTNING)
	shock.from_weapon = true
	bear.health.take_damage(shock)
	check(bear.mode == GauMa.Mode.STUNNED and bear.is_staggered(), "Shock paralyses it into a stagger")
	bear.set_mode(GauMa.Mode.IDLE)
	var frost := DamageInfo.make(400.0, Combat.Team.PLAYER, null, at, Vector3.FORWARD, Combat.Element.ICE)
	frost.from_weapon = true
	bear.health.take_damage(frost)
	check(bear.time_factor() < 0.7, "Frost slows everything it does (%.2f)" % bear.time_factor())
	var umbral := DamageInfo.make(400.0, Combat.Team.PLAYER, null, at, Vector3.FORWARD, Combat.Element.DARK)
	umbral.from_weapon = true
	bear.health.take_damage(umbral)
	var cysts := bear.find_children("*", "Area3D", true, false).filter(func(c) -> bool: return c is UmbralCyst)
	check(cysts.size() == 1, "Umbral grows a cyst on one of its parts")
	if cysts.size() == 1:
		var cyst: UmbralCyst = cysts[0]
		await t._frames(1)
		var parts_before := 0.0
		for p in bear.parts:
			parts_before += float(p.hp)
		cyst.health.take_damage(DamageInfo.make(cyst.health.max_hp + 10.0, Combat.Team.PLAYER, null, cyst.global_position, Vector3.FORWARD))
		var parts_after := 0.0
		for p in bear.parts:
			parts_after += float(p.hp)
		check(parts_after < parts_before, "the cyst bursts for part damage (%.0f)" % (parts_before - parts_after))
	var radiant := DamageInfo.make(400.0, Combat.Team.PLAYER, null, at, Vector3.FORWARD, Combat.Element.HOLY)
	radiant.from_weapon = true
	bear.health.take_damage(radiant)
	check(RadiantMark.on(bear.health) != null, "Radiant marks it")
	bear.poise.reset_poise()
	bear.set_mode(GauMa.Mode.CHARGE)
	await t._frames(4)
	check(bear.mode == GauMa.Mode.STUNNED and RadiantMark.on(bear.health) == null, "Húc under the mark: it detonates and interrupts")
	bear.queue_free()
	Health.spread_override = -1.0
	await t._frames(2)


func families() -> void:
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	await t._frames(2)
	check(hero.weapons.size() == 7, "the hero carries seven weapons")
	var basics := {"sword": "slash", "axe": "cleave", "hammer": "smash", "chain_blades": "stab", "war_pike": "thrust",
		"aether_strikers": "flurry", "repeaters": "arrow"}
	for id in hero.weapons:
		hero.equip_weapon(id, false)
		await t._frames(1)
		var fam: String = Weapons.item(id).family
		check(hero.skills.slots[0] and hero.skills.slots[0].id == basics[fam], "%s: its basic is %s" % [id, basics[fam]])
		var hands: int = hero.model.skeleton.find_children("Weapon", "Node3D", true, false).size()
		var want := 2 if Weapons.FAMILIES[fam].get("dual", false) else 1
		check(hands == want, "%s: %d piece(s) in hand (%d)" % [id, want, hands])
	hero.equip_weapon("w_song_liem_loi_dieu", false)
	check(hero.weapon_element_type() == Combat.Element.LIGHTNING, "Song Liêm Lôi Điểu deals Shock")
	hero.cycle_weapon()
	check(hero.weapon_item == "w_thuong_moc_lam", "V takes the next weapon")
	hero.queue_free()
	await t._frames(2)


func heavies() -> void:
	var ground := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(200, 1, 200)
	cs.shape = box
	cs.position = Vector3(0, -0.5, 0)
	ground.add_child(cs)
	t.add_child(ground)
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	hero.revive(Vector3(40, 0.1, 40))
	var slime := Slime.new()
	t.add_child(slime)
	await t._frames(3)
	for id in hero.weapons:
		hero.equip_weapon(id, false)
		var fam: Dictionary = Weapons.family(id)
		var dist := 8.0 if fam.heavy.shape == "shot" else 1.9
		slime.revive(Vector3(40, 0.1, 40 - dist))
		slime.health.reset_health(5000.0)
		slime._knock = Vector3.ZERO
		hero.revive(Vector3(40, 0.05, 40))
		hero.facing = Vector3.FORWARD
		await t._frames(20)
		slime.global_position = Vector3(40, 0.05, 40 - dist)
		var hp0 := slime.health.hp
		Input.action_press("heavy")
		await t._frames(int(float(fam.heavy.charge) * 60.0) + 4)
		Input.action_release("heavy")
		await t._frames(50)
		check(slime.health.hp < hp0, "%s: %s lands (%.0f)" % [id, fam.heavy.name, hp0 - slime.health.hp])
	for n in [hero, slime, ground]:
		n.queue_free()
	await t._frames(2)
