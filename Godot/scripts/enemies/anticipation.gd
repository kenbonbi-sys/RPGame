class_name Anticipation
extends RefCounted
## How a monster tells that a blow is coming, in beats (the animation principle of anticipation,
## and the rule action games keep: enemies get long, readable wind-ups so a player can react,
## while the hero's own moves stay short and snappy, which is what makes the fight feel tight):
##
##   1. tell   a tiny counter-move first, the other way (dips before it rears, leans in before it
##             draws back): the cue the eye picks up before the player knows why;
##   2. wind   the build-up, big and easing in, exaggerated so it reads from afar;
##   3. hold   a beat frozen at the peak, trembling, with a glint and a "shing": now;
##   4. strike fast;
##   5. recover, slow enough to leave an opening.
##
## Times are seconds since the attack began. `Beats` keeps one attack's timings together.

class Beats:
	var tell := 0.12
	var wind := 0.4
	var hold := 0.14

	func _init(tell_s := 0.12, wind_s := 0.4, hold_s := 0.14) -> void:
		tell = tell_s
		wind = wind_s
		hold = hold_s

	## When the strike starts.
	func strike_at() -> float:
		return tell + wind + hold

	## When the hold (the glint) starts.
	func hold_at() -> float:
		return tell + wind

	## The wind-up at `t`: dips to −dip through the tell, eases up to 1 by the end of the wind, stays
	## at 1 through the hold; 0 before the attack.
	func windup(t: float, dip := 0.25) -> float:
		if t <= 0.0:
			return 0.0
		if t < tell:
			return -dip * sin(t / tell * PI * 0.5)
		if t < tell + wind:
			var k := (t - tell) / wind
			return lerpf(-dip, 1.0, k * k * (3.0 - 2.0 * k))
		return 1.0

	## A fast small shiver while it holds at the peak (0 outside the hold).
	func tremble(t: float) -> float:
		if t < hold_at() or t >= strike_at():
			return 0.0
		return sin(t * 75.0)

	## The strike's progress, 0 → 1 over `seconds` from strike_at(), fast out of the gate.
	func strike(t: float, seconds: float) -> float:
		var k := clampf((t - strike_at()) / maxf(0.001, seconds), 0.0, 1.0)
		return 1.0 - pow(1.0 - k, 3.0)

	## True on the one frame the hold begins (for the glint and the sound).
	func hold_begins(t: float, dt: float) -> bool:
		return t >= hold_at() and t - dt < hold_at()


## The "now" cue at the peak of a wind-up: a star glint where the blow comes from, a short flash of
## light and the tell sound (lower for big monsters).
static func glint(pos: Vector3, color := Color(1.0, 0.9, 0.6), size := 1.0, pitch := 1.0) -> void:
	if not Game.has_screen():
		return
	Fx.impact_star(pos, color, size)
	Fx.flash(pos, color, 2.5 * size, 4.0 * size, 0.25)
	Sfx.play("tell", pos, -4.0 + 4.0 * minf(size, 2.0), pitch)
