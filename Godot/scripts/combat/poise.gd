class_name Poise
extends Node
## Thanh Trấn Áp, the Unity game's Poise.cs: heroes' hits fill it (each hit's poise × Sức Mạnh);
## full, it breaks: 3 s of Choáng and +50% damage taken. Each break raises the bar by 25% of its
## base; untouched for 2.5 s it drains at 40 per second. Its owner counts as "heavy".

signal broken

@export var max_poise := 300.0
@export var decay_delay := 2.5
@export var decay_per_second := 40.0
@export var break_stun := 3.0
@export var broken_damage_bonus := 0.5
@export var threshold_growth := 0.25

var current := 0.0
var threshold := 300.0
var breaks := 0
var health: Health
var status: StatusEffects

var _broken_until := 0.0
var _last_hit := -100.0
var _bonus_applied := false


func _ready() -> void:
	threshold = max_poise
	for c in get_parent().get_children():
		if c is Health:
			health = c
		elif c is StatusEffects:
			status = c
	if health and not health.damaged.is_connected(_on_damaged):
		health.damaged.connect(_on_damaged)


func now() -> float:
	return StatusEffects._scaled_now()


func is_broken() -> bool:
	return now() < _broken_until


func fraction() -> float:
	return 1.0 if is_broken() else current / threshold


func broken_remaining() -> float:
	return maxf(0.0, _broken_until - now())


func _on_damaged(d: DamageInfo, _amount: float) -> void:
	if d.source_team != Combat.Team.PLAYER or d.poise <= 0.0:
		return
	var st := d.source_stats()
	add_poise(d.poise * (st.poise_multiplier if st else 1.0))


func add_poise(x: float) -> void:
	if not Game.is_authority() or x <= 0.0 or is_broken() or (health and health.is_dead):
		return
	current += x
	_last_hit = now()
	if current >= threshold:
		_break()


func _break() -> void:
	breaks += 1
	current = 0.0
	threshold = max_poise * (1.0 + threshold_growth * breaks)
	_broken_until = now() + break_stun
	if status:
		status.force_stun(break_stun)
	if health and not _bonus_applied:
		health.damage_taken_multiplier *= 1.0 + broken_damage_bonus
		_bonus_applied = true
	if health:
		DamageNumbers.show(health.head_position() + Vector3.UP * 0.5, "Vỡ Trấn Áp!", "crit", true)
		Fx.burst(health.head_position(), Vector3.UP, Color(1.0, 0.85, 0.4, 1.0), 40, 8.0, 0.4, 0.8, 90.0, -8.0, true)
	Game.hitstop(DB.c("hitStopBreak", 0.12))
	Game.shake(0.5)
	broken.emit()


func _physics_process(delta: float) -> void:
	if _bonus_applied and not is_broken():
		if health:
			health.damage_taken_multiplier /= 1.0 + broken_damage_bonus
		_bonus_applied = false
	if not is_broken() and current > 0.0 and now() - _last_hit > decay_delay:
		current = maxf(0.0, current - decay_per_second * delta)


func reset_poise() -> void:
	if _bonus_applied and health:
		health.damage_taken_multiplier /= 1.0 + broken_damage_bonus
	_bonus_applied = false
	_broken_until = 0.0
	current = 0.0
	breaks = 0
	threshold = max_poise
