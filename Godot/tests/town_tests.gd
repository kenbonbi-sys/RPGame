extends RefCounted
## The village hub and the menus: the Dauntless settings (presets and Tuỳ Chỉnh), the nine
## townsfolk by their wiki names and trades, talking to them (Granny Strega's shelf, Janek's
## repeater), changing gear in the loadout, the hero standing still while a menu is up, and the
## practice dummy's count. Run by test_runner.

const HERO := preload("res://scenes/hero.tscn")
const WIKI_CAST := {
	"kat": ["Katerin Sorrel", "Người Hướng Dẫn"], "wils": ["Wils Bormen", "Thợ Rèn Vũ Khí"],
	"moyra": ["Moyra Heigsketter", "Thợ Rèn Giáp"], "middleman": ["The Middleman", "Người Buôn Cell"],
	"priyani": ["Dr. Shaed Priyani", "Nhà Nghiên Cứu Behemoth"], "gaius": ["Gaius Copperwheel", "Nhà Vẽ Bản Đồ"],
	"janek": ["Janek Zai", "Đô Đốc Ostia"], "strega": ["Granny Strega", "Bà Bán Thuốc"],
	"bulvar": ["Bulvar Rosk", "Bậc Thầy Vũ Khí · Crimson Blades"],
}

var t: Node


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("town: graphics settings", settings)
	await t._run("town: the nine townsfolk", townsfolk)
	await t._run("town: talking, buying, changing gear", talking)
	await t._run("town: the practice dummy", dummy)


func settings() -> void:
	GameSettings.use_defaults = true
	GameSettings._loaded = false
	check(int(GameSettings.get_value("quality")) == 2 and int(GameSettings.get_value("shadows")) == 2, "the defaults are Cao, the tuned look")
	GameSettings.set_value("quality", 0)
	var all_low := true
	for k in GameSettings.ADVANCED:
		all_low = all_low and int(GameSettings.get_value(k)) == 0
	check(all_low and is_equal_approx(Fx.detail, GameSettings.FX_DETAIL[0]), "Chất Lượng Thấp sets every Advanced row to Thấp")
	GameSettings.set_value("shadows", 3)
	check(int(GameSettings.get_value("quality")) == GameSettings.CUSTOM, "one row on its own turns it into Tuỳ Chỉnh")
	for k in GameSettings.ADVANCED:
		GameSettings.set_value(k, 3)
	check(int(GameSettings.get_value("quality")) == 3, "all rows the same again: back to that level (Epic)")
	GameSettings.reset()
	check(int(GameSettings.get_value("quality")) == 2 and is_equal_approx(Fx.detail, 1.0), "Mặc định restores Cao")
	check(GameSettings.resolutions().size() > 0, "there is a resolution to pick")


func _hero_at(p: Vector3) -> Hero:
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	hero.revive(p)
	return hero


func townsfolk() -> void:
	var town := Townsfolk.new()
	t.add_child(town)
	await t._frames(2)
	check(town.npcs.size() == 9, "nine townsfolk (%d)" % town.npcs.size())
	var named := true
	var built := true
	var off_portrait := true
	for id in WIKI_CAST:
		var npc: Npc = town.npcs.get(id)
		if npc == null:
			named = false
			continue
		named = named and npc.data.name == WIKI_CAST[id][0] and npc.data.title == WIKI_CAST[id][1]
		built = built and npc.model != null and npc.model.skeleton != null and npc.model.anim != null and npc.model.anim.is_playing()
		for mi in npc.model.find_children("*", "MeshInstance3D", true, false):
			off_portrait = off_portrait and ((mi as MeshInstance3D).layers & HeroModel.PORTRAIT_LAYER) == 0
	check(named, "names and trades as the Dauntless wiki has them")
	check(built, "each has a body and is animating")
	check(off_portrait, "none of them shows up in the hero's portrait")
	var apart := true
	var ids: Array = town.npcs.keys()
	for i in ids.size():
		for j in range(i + 1, ids.size()):
			if (town.npcs[ids[i]] as Npc).global_position.distance_to((town.npcs[ids[j]] as Npc).global_position) < 2.0:
				apart = false
	check(apart, "everyone has their own spot")
	var dummies := town.get_children().filter(func(c) -> bool: return c is TrainingDummy)
	check(dummies.size() == 3, "the Training Grounds have three dummies")
	town.queue_free()
	await t._frames(2)


func talking() -> void:
	var town := Townsfolk.new()
	t.add_child(town)
	var menus := Menus.new()
	t.add_child(menus)
	await t._frames(2)
	var strega: Npc = town.npcs.strega
	var fwd := Vector3(sin(strega.rotation.y), 0, cos(strega.rotation.y))
	var ground := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(20, 1, 20)
	cs.shape = box
	ground.add_child(cs)
	ground.position = strega.global_position + Vector3.DOWN * 0.5
	t.add_child(ground)
	var hero := _hero_at(strega.global_position + fwd * 1.6 + Vector3.UP * 0.05)
	var was_hero := Game.hero
	Game.hero = hero
	await t._frames(12)
	check(Npc.focus == strega, "standing by Granny Strega, F would talk to her")
	menus.open("talk", "", strega)
	check(Game.menu_open and menus.page is TalkMenu, "a talk opens and has the screen")
	Input.action_press("jump")
	await t._frames(3)
	Input.action_release("jump")
	check(hero.state == Hero.State.GROUND and hero._jump_squat <= 0.0, "the hero does not jump while a menu is up")
	var talk := menus.page as TalkMenu
	talk._do("shop")
	hero.inventory.gold = 25
	var before := hero.inventory.count("potion_red")
	talk._buy("potion_red")
	check(hero.inventory.gold == 15 and hero.inventory.count("potion_red") == before + 1, "a Bình Máu costs its 10 gold")
	hero.inventory.gold = 20
	talk._buy("potion_green")
	check(hero.inventory.gold == 20, "and nothing is sold on credit (a 25-gold tonic with 20)")
	menus.close()
	check(not Game.menu_open, "Esc closes it")
	menus.open("talk", "", town.npcs.janek)
	(menus.page as TalkMenu)._do("repeater")
	check(hero.weapon_item == "w_sung_hu_anh", "Janek Zai hands over the Ostian repeater")
	menus.close()
	menus.open("loadout")
	var lo := menus.page as LoadoutMenu
	lo.selected = 2
	lo._open_choice()
	var pick := -1
	for i in lo._options.size():
		if lo._options[i][3] == "charrogg":
			pick = i
	check(pick >= 0, "the torso list holds the wardrobe's pieces")
	if pick >= 0:
		lo._choose(pick)
	check(String(hero.armour.get("torso", "")) == "charrogg", "picking one puts it on (%s)" % str(hero.armour))
	lo.selected = 0
	lo._open_choice()
	for i in lo._options.size():
		if lo._options[i][3] == "w_riu_hoa_nguc":
			lo._choose(i)
	check(hero.weapon_item == "w_riu_hoa_nguc", "and the weapon row changes the weapon")
	menus.close()
	Game.hero = was_hero
	for n in [hero, town, menus, ground]:
		n.queue_free()
	await t._frames(2)


func dummy() -> void:
	var d := TrainingDummy.new()
	t.add_child(d)
	await t._frames(1)
	for i in 3:
		var hit := DamageInfo.make(40.0, Combat.Team.PLAYER, null, d.global_position + Vector3.UP, Vector3.FORWARD)
		hit.pure = true
		hit.poise = 5.0
		d.health.take_damage(hit)
	check(d.hits == 3 and is_equal_approx(d.total, 120.0) and is_equal_approx(d.stagger, 15.0), "it counts every hit, the damage and the stagger")
	check(not d.health.is_dead and d.health.hp == d.health.max_hp, "and never falls")
	check(d.dps() > 0.0, "with a DPS")
	d.queue_free()
	await t._frames(1)
