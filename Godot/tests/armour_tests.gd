extends RefCounted
## Dauntless armour: the wiki's sets, the resist and weak maths, hits on a hero wearing them, and
## the hero putting sets on and switching them. Run by test_runner.

const HERO := preload("res://scenes/hero.tscn")

var t: Node


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("armour: the wiki's sets", sets)
	await t._run("armour: resist and weak maths", maths)
	await t._run("armour: hits on a hero in armour", hits)
	await t._run("armour: the hero wears and switches sets", wearing)


func sets() -> void:
	check(Armour.SETS.size() == 35, "35 sets as on the wiki (%d)" % Armour.SETS.size())
	check(Armour.SETS.keys().slice(0, 2) == ["krolachi", "recruit"], "the neutral starter sets come first")
	var tiers_ok := true
	var last := 0
	var per_element := {}
	for id in Armour.SETS:
		var s: Dictionary = Armour.SETS[id]
		if s.pieces.size() != 4 or not Elements.ALL.has(s.element) or not int(s.resist) in [3, 5, 10, 15, 20, 25]:
			tiers_ok = false
		if int(s.resist) < last:
			tiers_ok = false
		last = int(s.resist)
		per_element[s.element] = int(per_element.get(s.element, 0)) + 1
	check(tiers_ok, "every set: four slots, a known element, a wiki tier, in tier order")
	for e in Elements.ALL:
		check(int(per_element.get(e, 0)) > 0, "sets of %s exist" % e)
	check(Armour.set_of("krolachi").element == "neutral" and Armour.set_of("recruit").element == "neutral", "the starter sets are neutral")
	check(Armour.full_set("krolachi").size() == 4, "Krolachi has all four pieces")
	check(not Armour.has_piece("recruit", "head") and Armour.full_set("recruit").size() == 3, "Recruit has no head piece")
	var aegis := Armour.piece("charrogg", "torso")
	check(aegis.name == "Volcanic Aegis" and aegis.element == "blaze" and aegis.resist == 10, "Volcanic Aegis: Blaze 10")
	var apex := Armour.piece("torgadoro", "head")
	check(apex.name == "Torgadoro's Apex" and apex.resist == 25, "Torgadoro's Apex: 25")
	check(Armour.piece("thrax", "legs").name == "Thrax's Guile" and Armour.piece("thrax", "legs").element == "umbral", "Thrax's Guile: Umbral")
	for id in Armour.WARDROBE:
		check(Armour.SETS.has(id), "the wardrobe's %s is a set" % id)
	var carried := {}
	for id in Armour.WARDROBE:
		carried[Armour.set_of(id).element] = true
	check(carried.size() == 7, "the wardrobe has the starter set and one set per element")


func maths() -> void:
	var charrogg := Armour.full_set("charrogg")
	check(Armour.points(charrogg) == {"blaze": 40, "frost": -40}, "four Blaze 10 pieces: +40 Blaze, −40 Frost")
	check(is_equal_approx(Armour.multiplier(Armour.points(charrogg), "blaze"), 0.8), "Blaze hits −20%")
	check(is_equal_approx(Armour.multiplier(Armour.points(charrogg), "frost"), 1.2), "Frost hits +20% (the opposing element)")
	check(is_equal_approx(Armour.multiplier(Armour.points(charrogg), "shock"), 1.0), "Shock hits unchanged")
	check(is_equal_approx(Armour.multiplier(Armour.points(charrogg), ""), 1.0), "plain hits unchanged")
	check(Armour.points(Armour.full_set("krolachi")).is_empty(), "neutral armour resists nothing")
	# every pair, both ways
	for e in ["blaze", "frost", "shock", "terra", "radiant", "umbral"]:
		var id := ""
		for s in Armour.SETS:
			if Armour.SETS[s].element == e:
				id = s
				break
		var r := float(Armour.SETS[id].resist) * 4.0
		var pts := Armour.points(Armour.full_set(id))
		var opp: String = Elements.ALL[e].opposite
		check(is_equal_approx(Armour.multiplier(pts, e), 1.0 - r * 0.005) and is_equal_approx(Armour.multiplier(pts, opp), 1.0 + r * 0.005),
			"%s armour resists %s and is weak to %s" % [id, e, opp])
	# pieces of different sets add up, and cancel out
	check(Armour.points({"head": "charrogg", "torso": "pangar"}).is_empty(), "a Blaze and a Frost piece cancel out")
	var mixed := Armour.points({"head": "gnasher", "torso": "gnasher", "arms": "shrike"})
	check(mixed == {"terra": 10, "shock": -10}, "two Terra pieces and a Shock one: Terra +10, Shock −10 (%s)" % str(mixed))
	var top := Armour.points(Armour.full_set("torgadoro"))
	check(is_equal_approx(Armour.multiplier(top, "blaze"), 0.5) and is_equal_approx(Armour.multiplier(top, "frost"), 1.5),
		"a full tier-25 set: half from its element, half again from the opposing one")
	check(is_equal_approx(Armour.multiplier({"blaze": 300}, "blaze"), 0.5) and is_equal_approx(Armour.multiplier({"blaze": -300}, "blaze"), 1.5),
		"never past ±50%")
	check(Armour.whole_set(charrogg) == "charrogg" and Armour.whole_set({"head": "charrogg", "torso": "pangar"}) == "",
		"a whole set is named, mixed pieces are not")
	check(Armour.main_element({"head": "gnasher", "torso": "gnasher", "arms": "shrike"}) == "terra", "mixed pieces lean to the most worn element")
	check(Armour.summary(charrogg) == "Kháng Hoả 40 · Yếu Băng 40", "the HUD line: " + Armour.summary(charrogg))
	check(Armour.summary(Armour.full_set("krolachi")) == "Không kháng hệ", "the HUD line for neutral armour")


func _target(team := Combat.Team.PLAYER, element := "") -> Node3D:
	var n := Node3D.new()
	n.add_child(StatusEffects.new())
	var h := Health.new()
	h.max_hp = 100000.0
	h.team = team
	h.element = element
	n.add_child(h)
	n.set_meta("health", h)
	t.add_child(n)
	return n


func _blow(h: Health, amount: float, type: int, source: Node = null) -> float:
	var d := DamageInfo.make(amount, Combat.Team.ENEMY, source, h.head_position(), Vector3.FORWARD, type)
	d.source_level = 1
	return h.take_damage(d)


func hits() -> void:
	Health.spread_override = 1.0
	var n := _target()
	var h: Health = n.get_meta("health")
	var bear := _target(Combat.Team.ENEMY, "terra")
	await t._frames(1)
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.FIRE), 100.0), "no armour: a Blaze blow deals 100")
	h.armour_points = Armour.points(Armour.full_set("charrogg"))
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.FIRE), 80.0), "in Charrogg a Blaze blow deals 80")
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.ICE), 120.0), "in Charrogg a Frost blow deals 120")
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.LIGHTNING), 100.0), "in Charrogg a Shock blow deals 100")
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.PHYSICAL), 100.0), "in Charrogg a plain blow deals 100")
	var pure := DamageInfo.make(100.0, Combat.Team.ENEMY, null, h.head_position(), Vector3.FORWARD, Combat.Element.ICE)
	pure.pure = true
	check(is_equal_approx(h.take_damage(pure), 100.0), "pure hits skip armour")
	# a behemoth's plain blows are of its element
	h.armour_points = Armour.points(Armour.full_set("gnasher"))
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.PHYSICAL, bear), 80.0), "Gnasher (Terra) takes a Terra monster's plain blow −20%")
	h.armour_points = Armour.points(Armour.full_set("shrike"))
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.PHYSICAL, bear), 120.0), "Shrike (Shock) takes it +20%")
	check(is_equal_approx(_blow(h, 100.0, Combat.Element.LIGHTNING, bear), 80.0), "the blow's own element comes first")
	# the Unity armour and resistances stay the base, the armour's share comes on top
	h.armour_points = {}
	h.armor = 50.0
	h.resistances[Combat.Element.FIRE] = 0.2
	var base := _blow(h, 100.0, Combat.Element.FIRE)
	h.armour_points = Armour.points(Armour.full_set("charrogg"))
	var worn := _blow(h, 100.0, Combat.Element.FIRE)
	var want := Combat.round_half_even(100.0 * (1.0 - Health.armor_reduction(50.0, 1)) * 0.8 * 0.8)
	check(base < 100.0 and is_equal_approx(worn, want), "Unity armour and resistance first, then the armour: %d -> %d (want %d)" % [base, worn, want])
	n.queue_free()
	bear.queue_free()
	Health.spread_override = -1.0
	await t._frames(1)


func wearing() -> void:
	Health.spread_override = 1.0
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	await t._frames(2)
	check(Armour.whole_set(hero.armour) == "krolachi", "the hero starts in Krolachi")
	check(hero.wardrobe.size() == Armour.WARDROBE.size(), "the hero carries the wardrobe (%d sets)" % hero.wardrobe.size())
	check(hero.health.armour_points.is_empty(), "Krolachi resists nothing")
	check(hero.model.armour_plates() == 0 and not hero.model.wears_helmet(), "Krolachi is the outfit's own cloth: nothing drawn over it")
	var t0 := Time.get_ticks_usec()
	hero.cycle_armour()
	var took := (Time.get_ticks_usec() - t0) / 1000.0
	check(took < 40.0, "putting a set on is quick, the plates were shaped while loading (%.1f ms)" % took)
	check(Armour.whole_set(hero.armour) == "charrogg", "G puts on the next set")
	check(hero.health.armour_points == {"blaze": 40, "frost": -40}, "its pieces reach Health")
	check(hero.model.armour_plates() >= 16 and hero.model.wears_helmet(), "Charrogg is drawn: helm, breastplate, mantle, pauldrons, vambraces, greaves (%d meshes)" % hero.model.armour_plates())
	var bent := 0
	for mi in hero.model.skeleton.get_children():
		if mi is MeshInstance3D and String(mi.name).begins_with("Armour_") and mi.skin != null and mi.get_aabb().size.length() > 0.05:
			bent += 1
	check(bent == hero.model.armour_plates(), "every plate is skinned to the hero's bones")
	var seen := ["charrogg"]
	for i in hero.wardrobe.size() - 1:
		hero.cycle_armour()
		seen.append(Armour.whole_set(hero.armour))
	check(seen.back() == "krolachi" and seen.size() == hero.wardrobe.size(), "G goes through every set and back")
	hero.wear_set("recruit", false)
	check(not hero.armour.has("head") and hero.armour.size() == 3, "Recruit leaves the head bare")
	check(not hero.model.wears_helmet() and hero.model.armour_plates() > 0, "Recruit: leather drawn, no helm, the hair shows")
	hero.wear_set("charrogg", false)
	hero.wear_piece("head", "gnasher", false)
	check(Armour.whole_set(hero.armour) == "" and hero.health.armour_points == {"blaze": 30, "frost": -30, "terra": 10, "shock": -10},
		"one Gnasher piece over Charrogg mixes (%s)" % str(hero.health.armour_points))
	hero.wear_piece("head", "recruit", false)
	check(hero.armour.head == "gnasher", "a piece a set does not make cannot be worn")
	hero.cycle_armour()
	check(Armour.whole_set(hero.armour) == hero.wardrobe[0], "G after mixed pieces starts the wardrobe again")
	# a real hit on the hero: the slime's lunge is Terra (its element)
	var slime := Slime.new()
	t.add_child(slime)
	await t._frames(1)
	var blow := func() -> float:
		hero.health.reset_health()
		var d := DamageInfo.make(40.0, Combat.Team.ENEMY, slime, hero.global_position + Vector3.UP, Vector3.FORWARD)
		return hero.health.take_damage(d)
	hero.wear_set("krolachi", false)
	var plain: float = blow.call()
	hero.wear_set("gnasher", false)
	var resisted: float = blow.call()
	hero.wear_set("shrike", false)
	var weak: float = blow.call()
	check(plain > 0.0 and absf(resisted - plain * 0.8) <= 1.0 and absf(weak - plain * 1.2) <= 1.0,
		"a slime's lunge on the hero: Krolachi %d, Gnasher %d, Shrike %d" % [plain, resisted, weak])
	check(GameIcons.item("a:gnasher") != null and GameIcons.armour_piece("head", "terra") != null, "armour icons draw")
	slime.queue_free()
	hero.queue_free()
	Health.spread_override = -1.0
	await t._frames(2)
