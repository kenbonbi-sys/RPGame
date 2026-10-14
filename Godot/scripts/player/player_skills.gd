class_name PlayerSkills
extends Node
## The hero's skill bar, as the Unity game's PlayerSkills.cs: slot 0 the basic attack (from the
## weapon, or the class cantrip with a focus), 1–5 the class kit (or spells from the Sách Chiêu),
## 6 the ultimate, 7 Lướt. Cooldowns belong to the skill, not the slot; a 0.08 s global cooldown;
## a press up to 0.15 s early is remembered and fires the moment the skill is ready. A skill waits
## for the previous pose to end — except Movement skills, which may cut it short once its commit
## time has passed (an ultimate commits for its whole pose).

signal cast(slot: int, ability: Ability)
signal failed(slot: int, reason: String)

const SLOT_COUNT := 8
const DASH_SLOT := 7
const GLOBAL_COOLDOWN := 0.08
const INPUT_BUFFER := 0.15

var hero: Hero
var slots: Array[Ability] = []
var levels := [1, 1, 1, 1, 1, 1, 1, 1]
## ability id -> time it is ready again
var ready_at := {}
## ability id -> its full cooldown last time (for the cooldown sweep)
var cooldown_of := {}

var _gcd_until := 0.0
var _commit_until := 0.0
var _buffered := -1
var _buffer_until := 0.0
var _last_fail := 0.0


func _ready() -> void:
	hero = get_parent() as Hero
	slots.resize(SLOT_COUNT)


func now() -> float:
	return StatusEffects._scaled_now()


## Fills the bar from the class kit and the weapon (ApplyKit).
func apply_kit(class_id: String, weapon_id: String, learned_bar := {}) -> void:
	var cls: Dictionary = DB.classes.get(class_id, {})
	var kit: Array = cls.get("kit", [])
	for i in 7:
		var id = kit[i] if i < kit.size() else null
		if id is String and id != "":
			slots[i] = Ability.get_by_id(id)
	var w: Dictionary = PlayerStats.WEAPONS.get(weapon_id, {})
	if not w.get("focus", false) and w.has("basic"):
		slots[0] = Ability.get_by_id(w.basic)
	for i in learned_bar:
		var a := Ability.get_by_id(learned_bar[i])
		if a and i >= 1 and i <= 6:
			slots[i] = a
	slots[DASH_SLOT] = Ability.get_by_id("dash")
	if slots[0] == null:
		slots[0] = Ability.get_by_id("slash")
	_gcd_until = 0.0
	_commit_until = 0.0
	_buffered = -1


func cooldown_left(i: int) -> float:
	var a := slots[i]
	return maxf(0.0, float(ready_at.get(a.id, 0.0)) - now()) if a else 0.0


func cooldown_fraction(i: int) -> float:
	var a := slots[i]
	if a == null:
		return 0.0
	var full := float(cooldown_of.get(a.id, 0.0))
	return cooldown_left(i) / full if full > 0.0 else 0.0


## How long a skill must still wait for the previous pose.
func pose_wait(i: int) -> float:
	var w := _commit_until - now()
	var a := slots[i]
	if a and not a.has_tag(Ability.TAG_MOVEMENT):
		w = maxf(w, hero.action_remaining())
	return maxf(0.0, w)


func ready_in(i: int) -> float:
	return maxf(maxf(cooldown_left(i), _gcd_until - now()), pose_wait(i))


## A key press: casts now, or remembers it for up to 0.15 s if it is nearly ready.
func request(i: int) -> bool:
	var a := slots[i] if i < slots.size() else null
	if a == null:
		return false
	var wait := ready_in(i)
	if wait > 0.0 and wait <= INPUT_BUFFER and hero.energy >= a.energy_cost():
		_buffered = i
		_buffer_until = now() + wait + 0.1
		return false
	_buffered = -1
	return try_cast(i)


func _process(_delta: float) -> void:
	if _buffered < 0:
		return
	if hero.is_dead() or hero.status.is_stunned() or now() > _buffer_until:
		_buffered = -1
		return
	if ready_in(_buffered) <= 0.0:
		var i := _buffered
		_buffered = -1
		try_cast(i)


func try_cast(i: int) -> bool:
	var a := slots[i] if i < slots.size() else null
	if a == null or hero.is_dead() or hero.status.is_stunned() or not hero.can_cast():
		return false
	if now() < _gcd_until or pose_wait(i) > 0.0:
		return false
	if cooldown_left(i) > 0.0:
		_fail(i, "")
		return false
	if hero.energy < a.energy_cost():
		_fail(i, "Không đủ năng lượng!")
		return false
	if a.has_tag(Ability.TAG_MOVEMENT) and hero.status.is_rooted():
		_fail(i, "Đang bị Trói!")
		return false
	var aim := Aim.point_for(hero, a)
	_commit(i, a)
	var bonus := hero.take_perfect_bonus(a)
	AbilityRunner.cast(hero, a, aim, levels[i], i, bonus)
	cast.emit(i, a)
	return true


func _commit(i: int, a: Ability) -> void:
	hero.energy -= a.energy_cost()
	hero.energy_changed.emit(hero.energy, hero.max_energy)
	var cd := a.cooldown_at(levels[i]) * hero.stats.cooldown_multiplier(i == 0, a.has_tag(Ability.TAG_MOVEMENT))
	if i == 0:
		cd /= maxf(0.1, hero.status.chill_multiplier())
		cd /= maxf(0.5, 1.0 + hero.attack_speed_bonus)
	ready_at[a.id] = now() + cd
	cooldown_of[a.id] = cd
	_gcd_until = now() + GLOBAL_COOLDOWN
	_commit_until = now() + a.commit_time()


func _fail(i: int, reason: String) -> void:
	if reason == "" or Game.now() - _last_fail < 0.8:
		return
	_last_fail = Game.now()
	failed.emit(i, reason)
	if Game.has_screen() and hero == Game.hero:
		DamageNumbers.show(hero.global_position + Vector3.UP * 2.3, reason, "text")
