class_name JuicyBar
extends RefCounted
## A life bar that feels the hit. Instead of snapping to the new value: the fill drains to it over
## 0.75 s, a pale ghost of the old value waits 0.4 s and then drains after it (so you see what you
## lost), the bar flashes and is shoved sideways on the hit, and the colour turns amber at half
## life and red at a quarter. Feed it the real fraction every frame; draw from `fill`, `ghost`,
## `flash()`, `shove()` and `color()`.

const DRAIN := 0.75
const GHOST_DELAY := 0.4
const FLASH := 0.16
const SHOVE := 1.0
const SHOVE_TIME := 0.32
const AMBER := Color(1.0, 0.7, 0.18)
const RED := Color(0.95, 0.2, 0.16)

var fill := 1.0
var ghost := 1.0
var _target := 1.0
var _from := 1.0
var _t := 99.0
var _ghost_from := 1.0
var _ghost_t := 99.0
var _hit_age := 99.0
var _side := 1.0


func _init(start := 1.0) -> void:
	fill = start
	ghost = start
	_target = start
	_from = start
	_ghost_from = start


func update(value: float, delta: float) -> void:
	value = clampf(value, 0.0, 1.0)
	if value < _target - 0.0005:
		# a hit: the fill starts draining from where it is, the ghost holds, then follows
		_from = fill
		_t = 0.0
		_ghost_from = maxf(ghost, fill)
		ghost = _ghost_from
		_ghost_t = -GHOST_DELAY
		_hit_age = 0.0
		_side = -_side
	elif value > _target + 0.0005:
		# healing rises quickly and the ghost has nothing to show
		_from = fill
		_t = DRAIN * 0.6
		_ghost_from = value
		_ghost_t = 99.0
	_target = value
	_t += delta
	_ghost_t += delta
	_hit_age += delta
	fill = lerpf(_from, _target, _ease(_t / DRAIN))
	if _ghost_t >= 0.0:
		ghost = lerpf(_ghost_from, _target, _ease(_ghost_t / DRAIN))
	ghost = maxf(ghost, fill)


static func _ease(k: float) -> float:
	k = clampf(k, 0.0, 1.0)
	return 1.0 - (1.0 - k) * (1.0 - k) * (1.0 - k)


## 0..1, bright right after a hit.
func flash() -> float:
	return clampf(1.0 - _hit_age / FLASH, 0.0, 1.0)


## A sideways shove that rings out: multiply by how far the bar may move (pixels or metres).
func shove() -> float:
	if _hit_age >= SHOVE_TIME:
		return 0.0
	return SHOVE * _side * exp(-_hit_age * 13.0) * cos(_hit_age * 48.0)


## Healthy colour above half life, amber from half, red from a quarter; lightened by the flash.
func color(healthy: Color) -> Color:
	var c := healthy
	if _target <= 0.25:
		c = RED
	elif _target <= 0.5:
		c = AMBER
	return c.lerp(Color.WHITE, flash() * 0.75)


## The ghost: pale, warm, and briefly white with the hit.
func ghost_color() -> Color:
	return Color(1.0, 0.9, 0.72, 0.9).lerp(Color.WHITE, flash() * 0.6)
