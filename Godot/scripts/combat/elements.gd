class_name Elements
extends RefCounted
## Dauntless elements on weapons and monsters. Blaze↔Frost, Shock↔Terra and Radiant↔Umbral oppose
## each other: a weapon is exceptionally strong against a monster of the opposing element (+50%)
## and slightly weaker against one of its own (−20%). They ride on the combat elements (Blaze is
## Lửa, Frost Băng, Shock Lôi, Terra Thổ, Radiant Thánh Mộc, Umbral Ám), so skills of those
## elements follow the same matchups.
##
## Weapon hits also fill an element meter on what they hit; at the threshold (12% of its life, at
## least 30) the element procs, unless the target is of that element itself:
##   Blaze   burns it (damage over time)          Frost   slows it
##   Shock   paralyses it (a short stagger)       Terra   drops healing orbs for the heroes
##   Radiant marks it: an Unstable Move (Húc) or an attack detonates the mark and interrupts it;
##           otherwise it does a little damage when it fades
##   Umbral  grows a cyst on it: hits on the cyst burst for part damage around it; destroyed, it
##           explodes (on a small monster: a delayed blast that hurts the monsters around it)

const STRONG := 1.5
const WEAK := 0.8
const PROC_SHARE := 0.12
const PROC_MIN := 30.0
const PROC_COOLDOWN := 6.0

const ALL := {
	"neutral": {"name": "Vô Hệ", "type": Combat.Element.PHYSICAL, "opposite": "", "icon": "sword", "color": Color(0.9, 0.88, 0.82)},
	"blaze": {"name": "Hoả", "type": Combat.Element.FIRE, "opposite": "frost", "icon": "flame", "color": Color(1.0, 0.45, 0.15)},
	"frost": {"name": "Băng", "type": Combat.Element.ICE, "opposite": "blaze", "icon": "snowflake", "color": Color(0.5, 0.85, 1.0)},
	"shock": {"name": "Lôi", "type": Combat.Element.LIGHTNING, "opposite": "terra", "icon": "bolt", "color": Color(0.72, 0.6, 1.0)},
	"terra": {"name": "Thổ", "type": Combat.Element.TERRA, "opposite": "shock", "icon": "leaf", "color": Color(0.5, 0.85, 0.3)},
	"radiant": {"name": "Quang", "type": Combat.Element.HOLY, "opposite": "umbral", "icon": "sun", "color": Color(1.0, 0.86, 0.4)},
	"umbral": {"name": "Ám", "type": Combat.Element.DARK, "opposite": "radiant", "icon": "moon", "color": Color(0.66, 0.36, 0.95)},
}

const PROC_WORDS := {"blaze": "Thiêu Đốt!", "frost": "Băng Giá!", "shock": "Tê Liệt!", "terra": "Mầm Sống!",
	"radiant": "Ấn Quang!", "umbral": "Nang Ám!"}


## The Dauntless element a combat element belongs to ("" for physical and poison).
static func of_type(type: int) -> String:
	for e in ALL:
		if e != "neutral" and int(ALL[e].type) == type:
			return e
	return ""


static func color(element: String) -> Color:
	return ALL.get(element, ALL.neutral).color


static func display_name(element: String) -> String:
	return ALL.get(element, ALL.neutral).name


## Damage multiplier of a hit of combat element `type` on a target of element `target`.
static func matchup(type: int, target: String) -> float:
	var e := of_type(type)
	if e == "" or target == "" or target == "neutral":
		return 1.0
	if ALL[e].opposite == target:
		return STRONG
	if e == target:
		return WEAK
	return 1.0


## After a weapon hit landed: fill the target's meter and proc at the threshold.
static func on_hit(h: Health, d: DamageInfo, amount: float) -> void:
	if not d.from_weapon or d.dot or h.is_dead or not Game.is_authority():
		return
	var e := of_type(d.type)
	if e == "" or e == h.element:
		return
	var now := Game.now()
	if now < float(h.proc_ready_at.get(e, 0.0)):
		return
	var meter := float(h.proc_meter.get(e, 0.0)) + amount
	var threshold := maxf(PROC_MIN, h.max_hp * PROC_SHARE)
	if meter < threshold:
		h.proc_meter[e] = meter
		return
	h.proc_meter[e] = 0.0
	h.proc_ready_at[e] = now + PROC_COOLDOWN
	proc(h, e, threshold, d)


static func proc(h: Health, e: String, threshold: float, d: DamageInfo) -> void:
	var owner := h.owner_body()
	var at := d.point
	if Game.has_screen():
		DamageNumbers.show(h.head_position() + Vector3.UP * 0.4, String(PROC_WORDS[e]), "proc_" + e, true)
		Fx.burst(at, Vector3.UP, color(e), 30, 6.0, 0.3, 0.6, 80.0, -4.0, true)
		Fx.flash(at, color(e), 4.0, 6.0)
	match e:
		"blaze":
			if h.status:
				h.status.burn(3, threshold * 0.12, d.source_team)
		"frost":
			if owner and owner.has_method("proc_slow"):
				owner.proc_slow(5.0, 0.45)
			elif h.status:
				h.status.slow(0.45, 5.0)
		"shock":
			if owner and owner.has_method("proc_paralyse"):
				owner.proc_paralyse(2.5)
			elif h.status:
				h.status.force_stun(2.0)
		"terra":
			for i in 3:
				var off := Vector3.FORWARD.rotated(Vector3.UP, TAU * i / 3.0 + randf()) * randf_range(1.5, 2.6)
				HealOrb.spawn(h.global_position + off, 0.12)
		"radiant":
			RadiantMark.attach(h, threshold * 0.25, d.source)
		"umbral":
			if owner and owner.has_method("proc_cyst"):
				owner.proc_cyst(threshold, d.source)
			else:
				UmbralBlast.prime(h, threshold, d.source)
