class_name HudIcons
extends RefCounted
## Little vector icons for the HUD, drawn into any Control: a glyph per skill element/kind
## (sword, flame, snowflake, bolt, leaf, sun, moon, shield, wing) and potion flasks.


static func element_of(a: Ability) -> String:
	if a == null:
		return ""
	if a.has_tag(Ability.TAG_MOVEMENT):
		return "move"
	if a.has_tag(Ability.TAG_SUPPORT):
		return "support"
	if a.has_tag(Ability.TAG_FIRE):
		return "fire"
	if a.has_tag(Ability.TAG_ICE):
		return "ice"
	if a.has_tag(Ability.TAG_LIGHTNING):
		return "bolt"
	if a.has_tag(Ability.TAG_POISON):
		return "poison"
	if a.has_tag(Ability.TAG_HOLY):
		return "holy"
	if a.has_tag(Ability.TAG_DARK):
		return "dark"
	return "blade"


const TINTS := {
	"blade": Color(0.92, 0.88, 0.8), "fire": Color(1.0, 0.55, 0.2), "ice": Color(0.6, 0.88, 1.0),
	"bolt": Color(0.75, 0.82, 1.0), "poison": Color(0.6, 0.95, 0.35), "holy": Color(1.0, 0.88, 0.45),
	"dark": Color(0.75, 0.5, 1.0), "support": Color(0.45, 0.95, 0.75), "move": Color(0.9, 0.95, 1.0),
}


static func tint(kind: String) -> Color:
	return TINTS.get(kind, Color.WHITE)


## Draws a glyph centred at `c` with radius `r`.
static func glyph(ci: CanvasItem, kind: String, c: Vector2, r: float, col: Color) -> void:
	var w := maxf(2.0, r * 0.14)
	match kind:
		"blade":
			ci.draw_line(c + Vector2(-r, r) * 0.7, c + Vector2(r, -r) * 0.75, col, w * 1.3, true)
			ci.draw_line(c + Vector2(-r * 0.25, r * 0.05), c + Vector2(r * 0.05, r * 0.35), col, w, true)
			ci.draw_line(c + Vector2(-r * 0.75, r * 0.45), c + Vector2(-r * 0.45, r * 0.75), col, w, true)
		"fire":
			var pts := PackedVector2Array()
			for i in 13:
				var a := PI * 2.0 * i / 12.0
				var rr := r * (0.55 + 0.35 * absf(sin(a * 1.5)))
				pts.append(c + Vector2(sin(a), -cos(a)) * rr * Vector2(0.8, 1.0) + Vector2(0, r * 0.15))
			ci.draw_colored_polygon(pts, Color(col, 0.9))
			ci.draw_circle(c + Vector2(0, r * 0.3), r * 0.28, Color(1, 0.95, 0.7))
		"ice":
			for i in 3:
				var a := PI * i / 3.0
				var d := Vector2(cos(a), sin(a)) * r * 0.85
				ci.draw_line(c - d, c + d, col, w, true)
				for s in [-1.0, 1.0]:
					var p: Vector2 = c + d * 0.55 * s
					ci.draw_line(p, p + d.rotated(0.8 * s) * 0.3, col, w * 0.8, true)
		"bolt":
			var pts := PackedVector2Array([c + Vector2(0.15, -1.0) * r, c + Vector2(-0.45, 0.1) * r, c + Vector2(0.0, 0.1) * r,
				c + Vector2(-0.2, 1.0) * r, c + Vector2(0.5, -0.2) * r, c + Vector2(0.05, -0.2) * r])
			ci.draw_colored_polygon(pts, col)
		"poison":
			var leaf := PackedVector2Array()
			for i in 17:
				var t := float(i) / 16.0
				var y := lerpf(-0.85, 0.85, t)
				leaf.append(c + Vector2(sin(t * PI) * 0.55, y) * r)
			for i in range(15, 0, -1):
				var t := float(i) / 16.0
				var y := lerpf(-0.85, 0.85, t)
				leaf.append(c + Vector2(-sin(t * PI) * 0.55, y) * r)
			ci.draw_colored_polygon(leaf, Color(col, 0.9))
			ci.draw_line(c + Vector2(0, -0.8) * r, c + Vector2(0, 0.9) * r, col.darkened(0.5), w * 0.7, true)
		"holy":
			ci.draw_circle(c, r * 0.38, col)
			for i in 8:
				var d := Vector2.from_angle(PI * 2.0 * i / 8.0)
				ci.draw_line(c + d * r * 0.55, c + d * r * 0.95, col, w, true)
		"dark":
			ci.draw_circle(c, r * 0.75, col)
			ci.draw_circle(c + Vector2(r * 0.32, -r * 0.18), r * 0.62, Color(0.08, 0.06, 0.12))
		"support":
			var shield := PackedVector2Array([c + Vector2(0, -0.9) * r, c + Vector2(0.75, -0.6) * r, c + Vector2(0.65, 0.25) * r,
				c + Vector2(0, 0.95) * r, c + Vector2(-0.65, 0.25) * r, c + Vector2(-0.75, -0.6) * r])
			ci.draw_colored_polygon(shield, Color(col, 0.85))
			ci.draw_line(c + Vector2(0, -0.45) * r, c + Vector2(0, 0.45) * r, Color(1, 1, 1, 0.9), w, true)
			ci.draw_line(c + Vector2(-0.35, 0) * r, c + Vector2(0.35, 0) * r, Color(1, 1, 1, 0.9), w, true)
		"move":
			for k in 3:
				var o := Vector2(-0.6 + 0.45 * k, 0) * r
				ci.draw_polyline(PackedVector2Array([c + o + Vector2(-0.2, -0.5) * r, c + o + Vector2(0.2, 0) * r, c + o + Vector2(-0.2, 0.5) * r]),
					Color(col, 0.4 + 0.3 * k), w, true)


## A potion flask (round body, neck, cork) filled with a colour.
static func flask(ci: CanvasItem, c: Vector2, r: float, fill: Color) -> void:
	ci.draw_circle(c + Vector2(0, r * 0.25), r * 0.62, Color(0.85, 0.92, 0.95, 0.35))
	ci.draw_circle(c + Vector2(0, r * 0.3), r * 0.52, fill)
	ci.draw_circle(c + Vector2(-r * 0.18, r * 0.12), r * 0.14, Color(1, 1, 1, 0.55))
	ci.draw_rect(Rect2(c + Vector2(-r * 0.16, -r * 0.62), Vector2(r * 0.32, r * 0.42)), Color(0.85, 0.92, 0.95, 0.5))
	ci.draw_rect(Rect2(c + Vector2(-r * 0.2, -r * 0.82), Vector2(r * 0.4, r * 0.22)), Color(0.55, 0.38, 0.22))
