class_name Health
extends Node3D
## Life, armour and resistances of anything that can be hurt; the Unity game's Health.cs.
## Put it as a child of the creature (at its origin); the creature exposes it as `health`.
##
## take_damage() follows the Unity order exactly: team check, invulnerability (Evaded), the lucky
## dodge hook, attacker-side scaling (curse on the attacker), damage-taken multipliers, a guard
## multiplier by direction, armour against the attacker's level, resistance, a ±5% spread, rounding
## half to even with a minimum of 1, the survive-at-1 hook, then statuses (never on the killing blow).

signal damaged(d: DamageInfo, amount: float)
signal died(d: DamageInfo)
signal healed(amount: float)
## A hostile hit landed while invulnerable (Lướt Hoàn Hảo listens to this).
signal evaded(d: DamageInfo)

@export var display_name := ""
@export var level := 1
@export var team := Combat.Team.ENEMY
@export var max_hp := 100.0
@export var armor := 0.0
## All-element resistance on top of per-element (0..0.75).
@export var elemental_resist := 0.0
## Per element (Combat.Element order), -0.5 (weak, +50% damage) .. 0.75.
@export var resistances := PackedFloat32Array([0, 0, 0, 0, 0, 0, 0])
## Height of the head above the origin (damage numbers, status words).
@export var head_height := 1.6
@export var show_damage_numbers := true

var hp := 100.0
var damage_taken_multiplier := 1.0
var invulnerable := false
var is_dead := false
var last_damage_time := -100.0
## Heroes who hurt it (kill credit, shared XP and loot).
var attackers: Array[Node] = []

## Optional hooks: evade(d) -> true to miss; endure() -> true to survive at 1; guard(d) -> multiplier.
var evade: Callable
var endure: Callable
var guard: Callable

var status: StatusEffects
var poise: Poise


func _ready() -> void:
	hp = max_hp
	# wire the siblings both ways, whichever was added first
	for c in get_parent().get_children():
		if c is StatusEffects:
			status = c
			c.health = self
		elif c is Poise:
			poise = c
			c.health = self
			if not damaged.is_connected(c._on_damaged):
				damaged.connect(c._on_damaged)
	if status and poise:
		status.poise = poise
		poise.status = status


## The creature this Health belongs to.
func owner_body() -> Node3D:
	return get_parent() as Node3D


func head_position() -> Vector3:
	return global_position + Vector3.UP * head_height


## Where a hit from `from` lands on it (big bodies override this with their closest spot).
func hit_point(_from: Vector3) -> Vector3:
	return head_position()


## Where a skill aimed at it from `from` should go.
func aim_at(_from: Vector3) -> Vector3:
	return global_position


func fraction() -> float:
	return hp / max_hp if max_hp > 0.0 else 0.0


func reset_health(new_max := -1.0) -> void:
	if new_max > 0.0:
		max_hp = new_max
	hp = max_hp
	is_dead = false
	invulnerable = false
	attackers.clear()


## Changes max HP keeping the same fraction.
func scale_max(new_max: float) -> void:
	var f := fraction()
	max_hp = new_max
	hp = maxf(1.0, roundf(new_max * f)) if not is_dead else 0.0


func resistance(type: int) -> float:
	var r := resistances[type] if type < resistances.size() else resistances[0]
	if type != Combat.Element.PHYSICAL:
		r += elemental_resist
	return clampf(r, DB.p("resistMin", -0.5), DB.p("resistMax", 0.75))


## The element it is weakest to (most negative resistance), or -1.
func weakness() -> int:
	var best := -1
	var low := 0.0
	for i in resistances.size():
		if resistances[i] < low:
			low = resistances[i]
			best = i
	return best


func can_be_damaged_by(team_of_attacker: int) -> bool:
	return Combat.can_hurt(team_of_attacker, team)


func take_damage(d: DamageInfo) -> float:
	if not Game.is_authority() or is_dead or not can_be_damaged_by(d.source_team):
		return 0.0
	if invulnerable:
		evaded.emit(d)
		return 0.0
	if evade.is_valid() and not d.dot and not d.pure and evade.call(d):
		DamageNumbers.show(head_position(), "May mắn!", "text")
		return 0.0
	var dealt := d.amount
	var amount: float
	if d.pure:
		amount = maxf(1.0, Combat.round_half_even(dealt))
	else:
		var st := d.source_stats()
		if st and not d.attack_scaled:
			dealt *= st.damage_scale(d.type)
		dealt *= _attacker_dealt(d)
		var raw := dealt * damage_taken_multiplier * (status.damage_taken_multiplier() if status else 1.0)
		if guard.is_valid() and not d.dot:
			raw *= maxf(0.0, guard.call(d))
		raw = raw * (1.0 - armor_reduction(armor, _attacker_level(d))) * (1.0 - resistance(d.type)) * spread()
		amount = maxf(1.0, Combat.round_half_even(raw))
	if amount >= hp and hp > 1.0 and endure.is_valid() and endure.call():
		amount = hp - 1.0
		DamageNumbers.show(head_position(), "Kiên Trì Bất Khuất!", "text")
	hp = maxf(0.0, hp - amount)
	last_damage_time = Game.now()
	if d.source and d.source.is_in_group("heroes") and not d.source in attackers:
		attackers.append(d.source)
	if status and hp > 0.0 and d.has_status():
		status.apply(d, dealt)
	damaged.emit(d, amount)
	_show_number(d, amount)
	if hp <= 0.0:
		is_dead = true
		died.emit(d)
	return amount


static func armor_reduction(armor_value: float, attacker_level: int) -> float:
	if armor_value <= 0.0:
		return 0.0
	return armor_value / (armor_value + DB.p("armorConstant", 50.0) + DB.p("armorPerAttackerLevel", 5.0) * maxi(1, attacker_level))


## Tests pin the ±5% spread with this (negative = random).
static var spread_override := -1.0


static func spread() -> float:
	if spread_override >= 0.0:
		return spread_override
	return 1.0 + (randf() * 2.0 - 1.0) * DB.p("damageSpread", 0.05)


func _attacker_dealt(d: DamageInfo) -> float:
	if d.source == null or not is_instance_valid(d.source):
		return 1.0
	var h := Combat.health_of(d.source)
	return h.status.damage_dealt_multiplier() if h and h.status else 1.0


func _attacker_level(d: DamageInfo) -> int:
	if d.source_level > 0:
		return d.source_level
	if d.source and is_instance_valid(d.source):
		var h := Combat.health_of(d.source)
		if h:
			return h.level
	return level


func heal(amount: float, show_text := true) -> void:
	if not Game.is_authority() or is_dead or amount <= 0.0:
		return
	var before := hp
	hp = minf(max_hp, hp + amount)
	var got := hp - before
	healed.emit(got)
	if show_text and got > 0.0:
		DamageNumbers.show(head_position() + Vector3.LEFT * 0.3, "+%d" % roundi(got), "heal")


## Dies at once (a pure hit from the other side).
func kill() -> void:
	invulnerable = false
	var d := DamageInfo.make(hp + 1.0, Combat.Team.PLAYER if team != Combat.Team.PLAYER else Combat.Team.ENEMY,
		null, head_position(), Vector3.FORWARD)
	d.pure = true
	take_damage(d)


func _show_number(d: DamageInfo, amount: float) -> void:
	if not show_damage_numbers or not Game.has_screen():
		return
	var at := head_position() + Vector3(randf_range(-0.25, 0.25), randf_range(-0.1, 0.2), randf_range(-0.25, 0.25))
	if team == Combat.Team.PLAYER:
		DamageNumbers.show(at, "-%d" % roundi(amount), "hero")
	elif d.crit:
		DamageNumbers.show(at, "%d!" % roundi(amount), "crit", true)
	else:
		var kind := "normal"
		match d.type:
			Combat.Element.FIRE: kind = "burn"
			Combat.Element.POISON: kind = "poison"
			Combat.Element.LIGHTNING: kind = "shock"
		DamageNumbers.show(at, str(roundi(amount)), kind, not d.dot and amount >= 8.0)
