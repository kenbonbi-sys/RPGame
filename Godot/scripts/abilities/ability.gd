class_name Ability
extends RefCounted
## One skill from DB.abilities (the Unity AbilityDef assets): costs, cooldown, pose, aim, and the
## timeline of effect blocks that AbilityRunner plays.

enum Targeting { DIRECTION, POINT, SELF }

const TAG_PHYSICAL := 1 << 0
const TAG_FIRE := 1 << 1
const TAG_ICE := 1 << 2
const TAG_LIGHTNING := 1 << 3
const TAG_WIND := 1 << 4
const TAG_DARK := 1 << 5
const TAG_HOLY := 1 << 6
const TAG_POISON := 1 << 7
const TAG_MELEE := 1 << 10
const TAG_PROJECTILE := 1 << 11
const TAG_AREA := 1 << 12
const TAG_CHANNEL := 1 << 13
const TAG_SUMMON := 1 << 14
const TAG_MOVEMENT := 1 << 15
const TAG_SUPPORT := 1 << 16
const TAG_ULTIMATE := 1 << 17
const TAG_NAMES := {TAG_PHYSICAL: "#Vật lý", TAG_FIRE: "#Lửa", TAG_ICE: "#Băng", TAG_LIGHTNING: "#Lôi",
	TAG_WIND: "#Phong", TAG_DARK: "#Ám", TAG_HOLY: "#Thánh Mộc", TAG_POISON: "#Độc", TAG_MELEE: "#Cận",
	TAG_PROJECTILE: "#Đạn", TAG_AREA: "#Vùng", TAG_CHANNEL: "#Kênh", TAG_SUMMON: "#Triệu hồi",
	TAG_MOVEMENT: "#Dịch chuyển", TAG_SUPPORT: "#Hỗ trợ", TAG_ULTIMATE: "#Tuyệt kỹ"}

var id := ""
var data := {}

static var _cache := {}


static func get_by_id(ability_id: String) -> Ability:
	if ability_id == "" or not DB.abilities.has(ability_id):
		return null
	if not _cache.has(ability_id):
		var a := Ability.new()
		a.id = ability_id
		a.data = DB.abilities[ability_id]
		_cache[ability_id] = a
	return _cache[ability_id]


func display_name() -> String:
	return data.get("displayName", id)


func description() -> String:
	return data.get("description", "")


func tags() -> int:
	return int(data.get("tags", 0))


func has_tag(t: int) -> bool:
	return (tags() & t) != 0


func tag_names() -> PackedStringArray:
	var out := PackedStringArray()
	for t in TAG_NAMES:
		if has_tag(t):
			out.append(TAG_NAMES[t])
	return out


func cooldown() -> float:
	return float(data.get("cooldown", 1.0))


func cooldown_at(level: int) -> float:
	return cooldown() * maxf(0.2, 1.0 - float(data.get("cooldownPerLevel", 0.04)) * (clampi(level, 1, 5) - 1))


func energy_cost() -> float:
	return float(data.get("energyCost", 0.0))


func targeting() -> int:
	return int(data.get("targeting", 0))


func max_range() -> float:
	return float(data.get("maxRange", 12.0)) * AbilityRunner.SPACE


func lock_time() -> float:
	return float(data.get("lockTime", 0.3))


## The part of the pose a Movement skill cannot cut short (the whole pose for an ultimate).
func commit_time() -> float:
	if has_tag(TAG_ULTIMATE):
		return lock_time()
	return clampf(float(data.get("commitTime", 0.0)), 0.0, lock_time())


func anim_base() -> String:
	var a = data.get("animBase", "attack")
	return a if a != null else ""


func move_while_casting() -> float:
	return float(data.get("moveWhileCasting", 0.25))


func power_scale(level: int) -> float:
	return 1.0 + float(data.get("powerPerLevel", 0.12)) * (maxi(1, level) - 1)


func effects() -> Array:
	return data.get("effects", [])
