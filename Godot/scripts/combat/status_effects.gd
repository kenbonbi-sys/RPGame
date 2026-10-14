class_name StatusEffects
extends Node
## Bỏng, Lạnh/Đóng Băng, Tích Điện/Phóng Điện, Độc, Choáng, Trói, Làm Chậm, Nguyền and Phán Xét,
## as the Unity game's StatusEffects.cs (numbers from DB.combat). Sits next to the Health.
## "Heavy" owners are those with a Poise (bosses): a freeze also adds poise and leaves them immune
## to crowd control for a while; repeated crowd control within 6 s gets shorter (×0.6 each time).

signal changed

## Set by hero buffs: no Choáng, Trói or freezing.
var stun_immune := false
## Multiplies Choáng only (bosses 0.5).
@export var stun_resist := 1.0
## 0..1: shortens Choáng, Trói and Nguyền (races, saving throws).
var control_shorter := 0.0
## 0..1: shortens Độc.
var poison_shorter := 0.0

var health: Health
var poise: Poise

var _burn_stacks: Array[float] = []    # damage per second of each stack
var _burn_until := 0.0
var _next_burn_tick := 0.0
var _burn_team := Combat.Team.PLAYER
var chill_stacks := 0
var _chill_until := 0.0
var _freeze_until := 0.0
var charge_stacks := 0
var _charge_until := 0.0
var poison_stacks := 0
var _poison_until := 0.0
var _next_poison_tick := 0.0
var _poison_team := Combat.Team.PLAYER
var _stun_until := 0.0
var _root_until := 0.0
var _slow_amount := 0.0
var _slow_until := 0.0
var _curse_until := 0.0
var _judgment_until := 0.0
var _cc_repeats := 0
var _cc_repeat_until := 0.0
var _cc_immune_until := 0.0
var _was_frozen := false


func _ready() -> void:
	for c in get_parent().get_children():
		if c is Health:
			health = c
		elif c is Poise:
			poise = c


## Scaled game time (statuses slow down with hit-stop like everything else): physics steps
## advance with Engine.time_scale.
static var _scaled := 0.0
static var _scaled_frame := -1


static func _scaled_now() -> float:
	var f := Engine.get_physics_frames()
	if f != _scaled_frame:
		_scaled_frame = f
		_scaled = f / float(Engine.physics_ticks_per_second)
	return _scaled


func now() -> float:
	return _scaled_now()


# ------------------------------------------------------------------ queries
func is_heavy() -> bool:
	return poise != null


func is_frozen() -> bool:
	return now() < _freeze_until


func is_stunned() -> bool:
	return now() < _stun_until or is_frozen()


func is_rooted() -> bool:
	return now() < _root_until


func is_slowed() -> bool:
	return now() < _slow_until


func is_cursed() -> bool:
	return now() < _curse_until


func is_judged() -> bool:
	return now() < _judgment_until


func is_burning() -> bool:
	return _burn_stacks.size() > 0 and now() < _burn_until


func is_poisoned() -> bool:
	return poison_stacks > 0 and now() < _poison_until


func live_chill() -> int:
	return chill_stacks if now() < _chill_until else 0


func live_charge() -> int:
	return charge_stacks if now() < _charge_until else 0


func crowd_control_immune() -> bool:
	return is_heavy() and now() < _cc_immune_until


func chill_multiplier() -> float:
	return 1.0 - DB.c("chillSlowPerStack", 0.12) * live_chill()


## Movement speed multiplier (0 while frozen or rooted).
func speed_multiplier() -> float:
	if is_frozen() or is_rooted():
		return 0.0
	return (1.0 - _slow_amount if is_slowed() else 1.0) * chill_multiplier()


func attack_speed_multiplier() -> float:
	return chill_multiplier()


func damage_taken_multiplier() -> float:
	var m := 1.0
	if is_cursed():
		m += DB.c("curseDamageTaken", 0.15)
	if is_judged():
		m += DB.c("judgmentDamageTaken", 0.2)
	return m


func damage_dealt_multiplier() -> float:
	return 1.0 - DB.c("curseDamageDealt", 0.2) if is_cursed() else 1.0


# ------------------------------------------------------------------ applying
## What a landed hit does; `dealt` is the attacker-side damage (Bỏng and Phóng Điện scale from it).
func apply(d: DamageInfo, dealt: float) -> void:
	var s := d.status
	if int(s.get("burn", 0)) > 0:
		burn(int(s.burn), dealt * DB.c("burnShare", 0.3), d.source_team)
	if int(s.get("chill", 0)) > 0:
		chill(int(s.chill))
	if int(s.get("poison", 0)) > 0:
		add_poison(int(s.poison), d.source_team)
	if float(s.get("stun", 0)) > 0.0:
		stun(float(s.stun))
	if float(s.get("root", 0)) > 0.0:
		root(float(s.root))
	if float(s.get("slow", 0)) > 0.0:
		slow(float(s.slow), float(s.get("slowDuration", 0.0)))
	if float(s.get("curse", 0)) > 0.0:
		curse(float(s.curse))
	if float(s.get("judgment", 0)) > 0.0:
		judge(float(s.judgment))
	# last: a discharge hits others
	if int(s.get("charge", 0)) > 0:
		charge(int(s.charge), dealt, d.source_team, d.source)
	changed.emit()


func burn(stacks: int, dps: float, team: int) -> void:
	if stacks <= 0 or dps <= 0.0:
		return
	var t := now()
	if not is_burning():
		_burn_stacks.clear()
		_next_burn_tick = t + DB.c("tickInterval", 0.5)
	var cap := int(DB.c("burnMaxStacks", 3))
	for i in stacks:
		if _burn_stacks.size() < cap:
			_burn_stacks.append(dps)
		else:
			var weakest := 0
			for j in _burn_stacks.size():
				if _burn_stacks[j] < _burn_stacks[weakest]:
					weakest = j
			if dps > _burn_stacks[weakest]:
				_burn_stacks[weakest] = dps
	_burn_until = t + DB.c("burnSeconds", 3.0)
	_burn_team = team


func chill(stacks: int) -> void:
	var have := live_chill() + stacks
	_chill_until = now() + DB.c("chillSeconds", 4.0)
	if have < int(DB.c("chillMaxStacks", 4)):
		chill_stacks = have
	else:
		chill_stacks = 0
		_chill_until = 0.0
		freeze()


func freeze() -> void:
	if is_heavy():
		poise.add_poise(DB.c("heavyFreezePoise", 60.0))
	if stun_immune:
		return
	var seconds := _crowd_control(DB.c("heavyFreezeSeconds", 0.6) if is_heavy() else DB.c("freezeSeconds", 1.5))
	if seconds <= 0.0:
		return
	_freeze_until = maxf(_freeze_until, now() + seconds)
	if is_heavy():
		_cc_immune_until = maxf(_cc_immune_until, _freeze_until + DB.c("heavyCrowdControlImmunity", 4.0))
	_word("Đóng Băng!", "shock")
	Fx.burst(_center(), Vector3.UP, Color(0.7, 0.9, 1.0, 0.9), 24, 4.0, 0.35, 0.8, 80.0, -4.0, true)


func charge(stacks: int, dealt: float, team: int, source: Node) -> void:
	var have := live_charge() + stacks
	_charge_until = now() + DB.c("chargeSeconds", 5.0)
	if have < int(DB.c("chargeMaxStacks", 3)):
		charge_stacks = have
	else:
		charge_stacks = 0
		discharge(dealt * DB.c("dischargeShare", 0.8), team, source)


## Phóng Điện: lightning jumps to the nearest others around the charged target.
func discharge(amount: float, team: int, source: Node) -> void:
	if health == null:
		return
	var at := _center()
	_word("Phóng Điện!", "shock")
	Fx.burst(at, Vector3.UP, Color(0.7, 0.8, 1.0, 1.0), 30, 8.0, 0.3, 0.5, 90.0, 0.0, true)
	Fx.flash(at, Color(0.6, 0.75, 1.0), 5.0, 8.0)
	var others := Combat.healths_in_sphere(health.get_world_3d(), at, DB.c("dischargeRadius", 4.0), team, [health])
	others.sort_custom(func(a: Health, b: Health) -> bool: return a.global_position.distance_to(at) < b.global_position.distance_to(at))
	var n := 0
	for h in others:
		if n >= int(DB.c("dischargeTargets", 3)):
			break
		if h.is_dead:
			continue
		var p := h.global_position
		var d := DamageInfo.make(amount, team, source, h.head_position(), p - at, Combat.Element.LIGHTNING, 1.5)
		d.attack_scaled = true
		d.skill_name = "Phóng Điện"
		if h.take_damage(d) > 0.0:
			Combat.on_hit_feedback(h, d)
		n += 1


func add_poison(stacks: int, team: int) -> void:
	var t := now()
	if not is_poisoned():
		poison_stacks = 0
		_next_poison_tick = t + DB.c("tickInterval", 0.5)
		Fx.burst(_center(), Vector3.UP, Color(0.55, 0.85, 0.3, 0.7), 14, 2.0, 0.5, 0.9, 60.0, 0.5)
	poison_stacks = mini(int(DB.c("poisonMaxStacks", 5)), poison_stacks + stacks)
	_poison_until = t + DB.c("poisonSeconds", 6.0) * (1.0 - poison_shorter)
	_poison_team = team


func stun(seconds: float) -> void:
	if stun_immune or seconds <= 0.0:
		return
	seconds = _crowd_control(seconds * stun_resist * (1.0 - control_shorter))
	if seconds > 0.0:
		_apply_stun(seconds)


## Poise breaks and wall rams: no resistance, immunity or shortening.
func force_stun(seconds: float) -> void:
	_apply_stun(seconds)


func _apply_stun(seconds: float) -> void:
	var fresh := now() >= _stun_until
	_stun_until = maxf(_stun_until, now() + seconds)
	if is_heavy():
		_cc_immune_until = maxf(_cc_immune_until, _stun_until + DB.c("heavyCrowdControlImmunity", 4.0))
	if fresh:
		_word("Choáng!", "text")


func clear_stun() -> void:
	_stun_until = 0.0
	_freeze_until = 0.0


func root(seconds: float) -> void:
	if stun_immune or seconds <= 0.0:
		return
	seconds = _crowd_control(seconds * (1.0 - control_shorter))
	if seconds <= 0.0:
		return
	var fresh := not is_rooted()
	_root_until = maxf(_root_until, now() + seconds)
	if fresh:
		_word("Trói!", "text")


func slow(amount: float, seconds := 0.0) -> void:
	_slow_amount = maxf(_slow_amount if is_slowed() else 0.0, clampf(amount, 0.0, 1.0))
	_slow_until = maxf(_slow_until, now() + (seconds if seconds > 0.0 else DB.c("slowSeconds", 2.0)))


func curse(seconds: float) -> void:
	var fresh := not is_cursed()
	_curse_until = maxf(_curse_until, now() + seconds * (1.0 - control_shorter))
	if fresh:
		_word("Nguyền!", "text")


func judge(seconds: float) -> void:
	var fresh := not is_judged()
	_judgment_until = maxf(_judgment_until, now() + seconds)
	if fresh:
		_word("Phán Xét!", "text")


## Bị đẩy vào tường đủ mạnh: choáng một chút.
func on_wall_slam() -> void:
	if health and health.is_dead:
		return
	Fx.dust(_center() + Vector3.DOWN, 1.2, 12)
	Game.shake(0.12)
	stun(DB.c("wallSlamStun", 0.5))


func cleanse() -> void:
	_burn_stacks.clear()
	_burn_until = 0.0
	chill_stacks = 0
	_chill_until = 0.0
	_freeze_until = 0.0
	charge_stacks = 0
	_charge_until = 0.0
	poison_stacks = 0
	_poison_until = 0.0
	_stun_until = 0.0
	_root_until = 0.0
	_slow_until = 0.0
	_slow_amount = 0.0
	_curse_until = 0.0
	_judgment_until = 0.0
	changed.emit()


func _crowd_control(seconds: float) -> float:
	if seconds <= 0.0 or crowd_control_immune():
		return 0.0
	var t := now()
	_cc_repeats = _cc_repeats + 1 if t < _cc_repeat_until else 0
	_cc_repeat_until = t + DB.c("crowdControlRepeatWindow", 6.0)
	return seconds * pow(DB.c("crowdControlRepeatFactor", 0.6), _cc_repeats)


# ------------------------------------------------------------------ ticking
func _physics_process(_delta: float) -> void:
	if health == null or not Game.is_authority():
		return
	if health.is_dead:
		if _burn_stacks.size() > 0 or poison_stacks > 0 or now() < _stun_until:
			cleanse()
		return
	var t := now()
	var tick := DB.c("tickInterval", 0.5)
	while _burn_stacks.size() > 0 and _next_burn_tick <= t and _next_burn_tick <= _burn_until + 0.001:
		var dps := 0.0
		for s in _burn_stacks:
			dps += s
		_dot(dps * tick, _burn_team, Combat.Element.FIRE)
		_next_burn_tick += tick
		if health.is_dead:
			return
	if _burn_stacks.size() > 0 and t >= _burn_until:
		_burn_stacks.clear()
	while poison_stacks > 0 and _next_poison_tick <= t and _next_poison_tick <= _poison_until + 0.001:
		_dot(health.max_hp * DB.c("poisonMaxHpPerSecond", 0.015) * poison_stacks * tick, _poison_team, Combat.Element.POISON)
		_next_poison_tick += tick
		if health.is_dead:
			return
	if poison_stacks > 0 and t >= _poison_until:
		poison_stacks = 0
	var frozen := is_frozen()
	if _was_frozen and not frozen:
		Fx.burst(_center(), Vector3.UP, Color(0.75, 0.92, 1.0, 0.9), 18, 5.0, 0.25, 0.5, 90.0, -9.0)
	_was_frozen = frozen


func _dot(amount: float, team: int, type: int) -> void:
	var d := DamageInfo.make(amount, team, null, health.head_position(), Vector3.UP, type)
	d.attack_scaled = true
	d.dot = true
	health.take_damage(d)


## The colour and strength the owner should be tinted with now (frozen first, then burning, …).
func tint() -> Color:
	var t := Game.now()
	if is_frozen():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.ICE], 0.6)
	if is_burning():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.FIRE], 0.18 + 0.1 * sin(18.0 * t))
	if is_poisoned():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.POISON], 0.2 + 0.08 * sin(6.0 * t))
	if live_chill() > 0:
		return Color(Combat.ELEMENT_COLORS[Combat.Element.ICE], 0.12 + 0.08 * live_chill())
	if is_slowed():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.ICE], 0.35)
	if live_charge() > 0:
		return Color(Combat.ELEMENT_COLORS[Combat.Element.LIGHTNING], 0.12 + 0.12 * absf(sin(25.0 * t)))
	if is_cursed():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.DARK], 0.25)
	if is_judged():
		return Color(Combat.ELEMENT_COLORS[Combat.Element.HOLY], 0.25)
	return Color(0, 0, 0, 0)


func _center() -> Vector3:
	return health.head_position() - Vector3.UP * 0.5 if health else Vector3.ZERO


func _word(text: String, kind: String) -> void:
	if health and Game.has_screen():
		DamageNumbers.show(health.head_position() + Vector3.UP * 0.3, text, kind)
