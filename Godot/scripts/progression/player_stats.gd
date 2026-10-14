class_name PlayerStats
extends Node
## A hero's level, XP, the six D&D ability scores and everything derived from them, as the Unity
## game's PlayerStats.cs + ProgressionConfig.cs (numbers from DB.progression, classes and races
## from DB.classes / DB.races). Pushes max HP, armour and resistances into the hero's Health.

signal changed
signal leveled_up(level: int)
signal xp_gained(amount: int)

enum Score { STR, INT, DEX, CON, WIS, CHA }
const SCORE_NAMES := ["Sức Mạnh", "Trí Tuệ", "Khéo Léo", "Thể Chất", "Thông Thái", "Sức Hút"]
const SHEET_ORDER := [Score.STR, Score.DEX, Score.CON, Score.INT, Score.WIS, Score.CHA]
const STANDARD_ARRAY := [15, 14, 13, 12, 10, 8]
## A hero with no class yet.
const CLASSLESS := [16, 16, 11, 14, 10, 10]
enum ArmorKind { NONE, LIGHT, MEDIUM, HEAVY, UNARMORED }

## WeaponKinds.cs: attack, how it strikes, and the basic attack it puts on the left mouse button.
const WEAPONS := {
	"sword": {"name": "Kiếm Dài", "attack": 20.0, "basic": "slash"},
	"axe": {"name": "Rìu Lớn", "attack": 23.0, "basic": "cleave"},
	"mace": {"name": "Chùy", "attack": 21.0, "basic": "smash"},
	"spear": {"name": "Giáo", "attack": 20.0, "basic": "thrust"},
	"rapier": {"name": "Kiếm Mảnh", "attack": 19.0, "basic": "thrust", "finesse": true},
	"scimitar": {"name": "Mã Tấu", "attack": 18.0, "basic": "slash", "finesse": true},
	"dagger": {"name": "Dao Găm", "attack": 16.0, "basic": "stab", "finesse": true},
	"fist": {"name": "Quyền Thủ", "attack": 17.0, "basic": "flurry", "finesse": true},
	"quarterstaff": {"name": "Côn Gỗ", "attack": 19.0, "basic": "smash", "finesse": true},
	"bow": {"name": "Cung Dài", "attack": 19.0, "basic": "arrow", "ranged": true},
	"staff": {"name": "Trượng Phép", "attack": 20.0, "focus": true},
	"wand": {"name": "Đũa Phép", "attack": 19.0, "focus": true},
	"orb": {"name": "Ngọc Phép", "attack": 20.0, "focus": true},
	"tome": {"name": "Sách Phép", "attack": 20.0, "focus": true},
	"lute": {"name": "Đàn Luýt", "attack": 19.0, "focus": true},
}
const ATTACK_PER_UPGRADE := 0.08
const MAX_UPGRADE := 10

@export var class_id := ""
@export var race_id := ""
@export var weapon_id := "sword"
@export var weapon_upgrade := 0
@export var skin_index := 0

var level := 1
var xp := 0
var stat_points := 0
var talent_points := 0
var skill_points := 0
## Points spent on each score (Score order).
var allocated: Array[int] = [0, 0, 0, 0, 0, 0]
## Flat bonuses from gear and buffs: {stat name: value}. Stage 3 fills this from worn items.
var bonuses := {}

# derived (recalculate() fills these)
var scores: Array[float] = [10.0, 10.0, 10.0, 10.0, 10.0, 10.0]
var max_hp := 100.0
var max_energy := 50.0
var physical_attack := 24.5
var magic_attack := 24.5
var crit_chance := 0.0
var crit_damage := 1.8
var attack_speed := 0.0
var dash_cooldown_reduction := 0.0
var armor := 0.0
var elemental_resist := 0.0
var poise_multiplier := 1.0
var damage_dealt := 1.0
var cooldown_reduction := 0.0
var healing_power := 1.0
var gold_find := 0.0
var speed_multiplier := 1.0
## Extra crit chance from buffs (Nhật Thực).
var buff_crit := 0.0

var health: Health


func _ready() -> void:
	for c in get_parent().get_children():
		if c is Health:
			health = c
	recalculate()
	if health:
		health.hp = health.max_hp


# ------------------------------------------------------------------ data
func class_data() -> Dictionary:
	return DB.classes.get(class_id, {})


func race_data() -> Dictionary:
	return DB.races.get(race_id, {})


func weapon() -> Dictionary:
	return WEAPONS.get(weapon_id, WEAPONS.sword)


func base_score(s: int) -> float:
	var c := class_data()
	var v := 0.0
	if c.is_empty():
		v = CLASSLESS[s]
	else:
		# JSON numbers come back as floats: compare as ints
		var order: Array = c.get("arrayOrder", [0, 1, 2, 3, 4, 5])
		var i := -1
		for k in order.size():
			if int(order[k]) == s:
				i = k
		v = STANDARD_ARRAY[i] if i >= 0 else 10.0
	var bonus: Array = race_data().get("bonus", [0, 0, 0, 0, 0, 0])
	if s < bonus.size():
		v += bonus[s]
	return v


func score(s: int) -> float:
	return scores[s]


func hit_die_avg() -> float:
	var c := class_data()
	return c.hitDie / 2.0 + 0.5 if c.has("hitDie") else 4.5


func casting_score() -> float:
	var c := class_data()
	if c.is_empty():
		return maxf(scores[Score.INT], scores[Score.STR])
	return scores[int(c.get("casting", 0))]


func attack_score() -> float:
	var w := weapon()
	if w.get("focus", false):
		return casting_score()
	if w.get("ranged", false):
		return scores[Score.DEX]
	if w.get("finesse", false):
		return maxf(scores[Score.STR], scores[Score.DEX])
	return scores[Score.STR]


func weapon_attack() -> float:
	return float(weapon().attack) * (1.0 + ATTACK_PER_UPGRADE * clampi(weapon_upgrade, 0, MAX_UPGRADE))


func class_base_armor() -> float:
	var c := class_data()
	if c.is_empty():
		return 2.0
	match int(c.get("armor", 0)):
		ArmorKind.LIGHT: return 2.0
		ArmorKind.MEDIUM: return 5.0
		ArmorKind.HEAVY: return 8.0
		ArmorKind.UNARMORED:
			var x := scores[Score.WIS] if class_id == "monk" else scores[Score.CON]
			return 1.0 + 0.5 * maxf(0.0, x - 10.0)
	return 0.0


func has_save(s: int) -> bool:
	for v in class_data().get("savingThrows", []):
		if int(v) == s:
			return true
	return false


func _bonus(stat: String) -> float:
	return float(bonuses.get(stat, 0.0))


# ------------------------------------------------------------------ derived stats
func recalculate() -> void:
	for s in 6:
		scores[s] = base_score(s) + allocated[s] + _bonus(SCORE_NAMES[s])
	var str_ := scores[Score.STR]
	var dex := scores[Score.DEX]
	var con := scores[Score.CON]
	var int_ := scores[Score.INT]
	var wis := scores[Score.WIS]
	var cha := scores[Score.CHA]
	var race := race_data()
	var w_atk := weapon_attack()
	var per_point := DB.p("attackPerPoint", 1.5)
	var from_score := DB.p("attackFromScore", 13.0)
	max_hp = DB.p("hpBase", 56.0) + DB.p("hpPerHitDie", 1.8) * hit_die_avg() * level \
		+ DB.p("hpPerConstitution", 10.0) * (con - 10.0) + float(race.get("hpPerLevel", 0.0)) * level + _bonus("MaxHp")
	max_energy = DB.p("energyBase", 45.0) + DB.p("energyPerLevel", 1.5) * level \
		+ DB.p("energyPerCastingPoint", 1.5) * (casting_score() - 10.0) + _bonus("MaxEnergy")
	physical_attack = w_atk + per_point * (attack_score() - from_score) + _bonus("PhysicalAttack")
	magic_attack = w_atk + per_point * (casting_score() - from_score) + _bonus("MagicAttack")
	crit_chance = DB.p("critPerDexterity", 0.0025) * (dex - 10.0) + _bonus("CritChance")
	crit_damage = DB.p("critMultiplier", 1.8) + float(race.get("savageCrit", 0.0)) + _bonus("CritDamage")
	attack_speed = DB.p("attackSpeedPerDexterity", 0.005) * (dex - 10.0) + _bonus("AttackSpeed")
	dash_cooldown_reduction = DB.p("dashCooldownPerDexterity", 0.005) * (dex - 10.0) + _bonus("DashCooldownReduction")
	armor = maxf(0.0, class_base_armor() + DB.p("armorPerConstitution", 0.5) * (con - 10.0)) + _bonus("Armor")
	elemental_resist = maxf(0.0, DB.p("resistPerWisdom", 0.003) * (wis - 10.0)) + _bonus("ElementalResist")
	poise_multiplier = 1.0 + DB.p("poisePerStrength", 0.01) * (str_ - 10.0) + _bonus("PoiseDamage")
	damage_dealt = 1.0 + _bonus("DamageDealt")
	cooldown_reduction = clampf(DB.p("cooldownPerIntelligence", 0.004) * (int_ - 10.0), 0.0, DB.p("cooldownReductionMax", 0.3)) + _bonus("CooldownReduction")
	healing_power = maxf(0.5, 1.0 + DB.p("healingPerWisdom", 0.02) * (wis - 10.0)) + _bonus("HealingPower")
	gold_find = maxf(0.0, DB.p("goldPerCharisma", 0.02) * (cha - 10.0)) + _bonus("GoldFind")
	speed_multiplier = maxf(0.5, float(race.get("speedMultiplier", 1.0)))
	_apply()
	changed.emit()


func _apply() -> void:
	if health == null:
		return
	var frac := health.fraction() if health.max_hp > 0.0 else 1.0
	health.max_hp = Combat.round_half_even(max_hp)
	if not health.is_dead:
		health.hp = minf(health.max_hp, Combat.round_half_even(health.max_hp * frac))
	health.level = level
	health.armor = armor
	health.elemental_resist = minf(DB.p("resistMax", 0.75), elemental_resist)
	var r := PackedFloat32Array([0, 0, 0, 0, 0, 0, 0])
	var race_resist: Dictionary = race_data().get("resist", {})
	var keys := ["physical", "fire", "ice", "lightning", "poison", "holy", "dark"]
	for i in keys.size():
		r[i] = float(race_resist.get(keys[i], 0.0))
	if int(race_data().get("draconic", 0)) == 1:
		var draconic := [Combat.Element.FIRE, Combat.Element.FIRE, Combat.Element.LIGHTNING, Combat.Element.LIGHTNING,
			Combat.Element.ICE, Combat.Element.ICE, Combat.Element.POISON, Combat.Element.POISON]
		r[draconic[clampi(skin_index, 0, 7)]] += 0.5
	health.resistances = r
	if health.status:
		health.status.control_shorter = minf(0.6, float(race_data().get("controlShorter", 0.0)) + (0.2 if has_save(Score.WIS) else 0.0))
		health.status.poison_shorter = minf(0.75, float(race_data().get("poisonShorter", 0.0)) + (0.25 if has_save(Score.CON) else 0.0))


## Attack for a hit of this element (physical uses the weapon, the rest the casting ability).
func attack_for(type: int) -> float:
	return physical_attack if type == Combat.Element.PHYSICAL else magic_attack


## Old flat-number hits are scaled by Attack / Attack(16, 20).
func damage_scale(type: int) -> float:
	var reference := DB.p("weaponAttack", 20.0) + DB.p("attackPerPoint", 1.5) * (DB.p("referenceScore", 16.0) - DB.p("attackFromScore", 13.0))
	return attack_for(type) / reference


## Cooldown multiplier for a skill: Movement skills by Khéo Léo, the basic attack by attack speed,
## everything else by Trí Tuệ.
func cooldown_multiplier(is_basic: bool, is_movement: bool) -> float:
	if is_movement:
		return maxf(0.5, 1.0 - dash_cooldown_reduction)
	if is_basic:
		return 1.0 / maxf(0.5, 1.0 + attack_speed)
	return 1.0 - cooldown_reduction


# ------------------------------------------------------------------ points
func spend_point(s: int) -> bool:
	if stat_points <= 0:
		return false
	stat_points -= 1
	allocated[s] += 1
	recalculate()
	return true


## Changing class or race gives every spent point back.
func refund_points() -> void:
	for i in 6:
		stat_points += allocated[i]
		allocated[i] = 0
	recalculate()


func set_class(id: String, race := "") -> void:
	class_id = id
	if race != "":
		race_id = race
	var weapons: Array = class_data().get("weapons", [])
	if weapons.size() > 0 and not weapons.has(weapon_id):
		weapon_id = weapons[0]
	refund_points()


# ------------------------------------------------------------------ XP
static func xp_to_next(lv: int) -> int:
	if lv >= int(DB.p("maxLevel", 40.0)):
		return 0
	return int(Combat.round_half_even(DB.p("xpBase", 50.0) * pow(maxf(1.0, lv), DB.p("xpExponent", 1.6))))


static func total_xp(lv: int) -> int:
	var t := 0
	for l in range(1, lv):
		t += xp_to_next(l)
	return t


## XP for killing a monster: by its level and rank, less if it is far below the hero.
static func kill_xp(enemy_level: int, rank: int, hero_level: int) -> int:
	var mults: Array = DB.progression.get("rankMultiplier", [1, 4, 15, 40])
	var v := (DB.p("killXpBase", 8.0) + DB.p("killXpPerLevel", 4.0) * enemy_level) * float(mults[clampi(rank, 0, mults.size() - 1)])
	var gap := hero_level - enemy_level
	if gap >= int(DB.p("veryLowGap", 8.0)):
		v *= DB.p("veryLowGapFactor", 0.1)
	elif gap >= int(DB.p("lowGap", 5.0)):
		v *= DB.p("lowGapFactor", 0.5)
	return maxi(1, int(Combat.round_half_even(v)))


func on_kill(enemy_level: int, rank: int, at: Vector3) -> void:
	var gain := Combat.round_half_even(kill_xp(enemy_level, rank, level) * (1.0 + float(race_data().get("xpBonus", 0.0))))
	add_xp(int(gain), at)


func add_xp(amount: int, at := Vector3.ZERO) -> void:
	if amount <= 0 or level >= int(DB.p("maxLevel", 40.0)) or not Game.is_authority():
		return
	xp += amount
	xp_gained.emit(amount)
	if at != Vector3.ZERO and Game.has_screen():
		DamageNumbers.show(at + Vector3.UP * 1.4, "+%d XP" % amount, "text")
	var grew := false
	while level < int(DB.p("maxLevel", 40.0)) and xp >= xp_to_next(level):
		xp -= xp_to_next(level)
		level += 1
		stat_points += int(DB.p("statPointsPerLevel", 3.0))
		talent_points += int(DB.p("talentPointsPerLevel", 1.0))
		skill_points += int(DB.p("skillPointsPerLevel", 1.0))
		grew = true
	if level >= int(DB.p("maxLevel", 40.0)):
		xp = 0
	if grew:
		recalculate()
		if health:
			health.hp = health.max_hp
		leveled_up.emit(level)
	changed.emit()
