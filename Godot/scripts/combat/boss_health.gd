class_name BossHealth
extends Health
## The life of a colossus, as a Dauntless behemoth's: one pool shared by all its moving parts, so a
## hit anywhere on it lands here. It knows where each hit landed, which lets the colossus count it
## toward breaking that part, give the head more stagger, be interrupted, and show the hit as part
## damage (yellow), core damage (white-blue) or staggered damage (orange).

var colossus: Node


## The colossus's head (status words, poise breaks).
func head_position() -> Vector3:
	return colossus.look_point() if colossus else super.head_position()


## The closest spot on its body to `from` (where a blade or a bolt meets it).
func hit_point(from: Vector3) -> Vector3:
	return colossus.closest_point(from)["point"] if colossus else super.hit_point(from)


## Where a skill aimed at it should land: under the closest spot of its body.
func aim_at(from: Vector3) -> Vector3:
	return hit_point(from) if colossus else super.aim_at(from)


func take_damage(d: DamageInfo) -> float:
	if colossus == null:
		return super.take_damage(d)
	colossus.prepare_hit(d)
	var amount := super.take_damage(d)
	if amount > 0.0:
		colossus.after_hit(d, amount)
	return amount


func _show_number(d: DamageInfo, amount: float) -> void:
	if colossus and show_damage_numbers and Game.has_screen():
		colossus.show_hit_number(d, amount)
	elif colossus == null:
		super._show_number(d, amount)
