class_name DamageInfo
extends RefCounted
## One hit, as the Unity game's DamageInfo struct (Combat/DamageInfo.cs). Positions are 3D; the
## Unity game's 2D directions map onto the ground plane.

var amount := 0.0
var type := Combat.Element.PHYSICAL
var source_team := Combat.Team.ENEMY
## The attacker (a node with `health`, and for heroes `stats`), or null (damage over time).
var source: Node
var point := Vector3.ZERO
var direction := Vector3.FORWARD
var knockback := 0.0
var crit := false
## StatusHit: burn, chill, charge, poison (stacks), stun, root, curse, judgment (seconds),
## slow (share of speed lost), slowDuration (0 = the default).
var status := {}
var hit_stop := 0.0
var poise := 0.0
var skill_name := ""
## Attacker level for armour; 0 = read it from the source's Health.
var source_level := 0
## Already includes the attacker's Attack (every hero skill hit).
var attack_scaled := false
## Skips armour, resistances, damage-taken multipliers and the spread.
var pure := false
## A body bump (Lướt Hoàn Hảo ignores those).
var contact := false
## A damage-over-time tick.
var dot := false
## Show knockback, flash and sparks.
var feedback := true
## 3D only: a big blow that throws a hero to the ground (colossus stomps, charges).
var knockdown := false
var lift := 0.0


static func make(amount: float, team: int, source: Node, point: Vector3, direction: Vector3,
		type := Combat.Element.PHYSICAL, knockback := 0.0) -> DamageInfo:
	var d := DamageInfo.new()
	d.amount = amount
	d.source_team = team
	d.source = source
	d.point = point
	var flat := Vector3(direction.x, 0.0, direction.z)
	d.direction = flat.normalized() if flat.length() > 0.0001 else Vector3.FORWARD
	d.type = type
	d.knockback = knockback
	return d


func copy() -> DamageInfo:
	var d := DamageInfo.new()
	for p in ["amount", "type", "source_team", "source", "point", "direction", "knockback", "crit", "hit_stop",
			"poise", "skill_name", "source_level", "attack_scaled", "pure", "contact", "dot", "feedback", "knockdown", "lift"]:
		d.set(p, get(p))
	d.status = status.duplicate()
	return d


func has_status() -> bool:
	for k in status:
		if float(status[k]) > 0.0 and k != "slowDuration":
			return true
	return false


## The hero's stats if a hero dealt this hit.
func source_stats() -> PlayerStats:
	return source.get("stats") as PlayerStats if source and is_instance_valid(source) else null


## Rolls a critical hit once per target: heroes add their Nhanh Nhẹn crit and use their own
## crit damage; everyone else crits for ×1.8 at the given chance.
func roll_crit(chance: float) -> void:
	var mult := DB.p("critMultiplier", 1.8)
	var st := source_stats()
	if source_team == Combat.Team.PLAYER and st:
		chance = minf(DB.p("critMax", 0.6), chance + st.crit_chance + st.buff_crit)
		mult = st.crit_damage
	if chance > 0.0 and randf() < chance:
		crit = true
		amount *= mult
