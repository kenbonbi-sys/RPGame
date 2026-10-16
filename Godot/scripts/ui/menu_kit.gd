class_name MenuKit
extends RefCounted
## How Dauntless draws its menus, for every menu here: slate panels, section bands whose top line
## curls down at both ends with the title in the middle, rows with the label on the left and the
## value between two chevrons, page dots under a choice, cyan sliders with a white knob, a pill
## switch, and the loadout's dark navy rows with a bright edge on the one selected. Barlow Condensed.

const PANEL := Color(0.075, 0.1, 0.13, 0.94)
const BAND := Color(0.15, 0.19, 0.235, 0.98)
const BAND_LINE := Color(0.62, 0.67, 0.72)
const HOVER := Color(0.32, 0.42, 0.52, 0.32)
const LABEL := Color(0.74, 0.79, 0.84)
const VALUE := Color(0.96, 0.97, 0.98)
const DIM := Color(0.5, 0.55, 0.6)
const CYAN := Color(0.27, 0.8, 1.0)
const CYAN_DEEP := Color(0.06, 0.42, 0.66)
const TRACK := Color(0.02, 0.035, 0.05, 0.95)
const NAVY := Color(0.09, 0.13, 0.19, 0.95)
const NAVY_EDGE := Color(0.27, 0.34, 0.43)
const GOOD := Color(0.0, 0.8, 0.4)
const BAD := Color(1.0, 0.4, 0.4)
const GOLD := Color(0.96, 0.8, 0.42)


static func font(weight := "semi") -> Font:
	match weight:
		"bold":
			return Hud.FONT_BOLD
		"xbold":
			return Hud.FONT_XBOLD
	return Hud.FONT_SEMI


static func text(ci: CanvasItem, at: Vector2, s: String, size: int, col: Color, align := HORIZONTAL_ALIGNMENT_LEFT,
		width := -1.0, weight := "semi") -> void:
	ci.draw_string(font(weight), at, s, align, width, size, col)


static func text_width(s: String, size: int, weight := "semi") -> float:
	return font(weight).get_string_size(s, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x


static func panel(ci: CanvasItem, r: Rect2, col := PANEL) -> void:
	var sb := StyleBoxFlat.new()
	sb.bg_color = col
	sb.set_corner_radius_all(4)
	sb.border_color = Color(1, 1, 1, 0.06)
	sb.set_border_width_all(1)
	ci.draw_style_box(sb, r)


## A section band: dark, full width, its top line curling down at both ends, the title centred.
static func header(ci: CanvasItem, r: Rect2, title: String) -> void:
	ci.draw_rect(r, BAND)
	var y := r.position.y + 1.5
	var curl := 14.0
	var pts := PackedVector2Array()
	# left curl: down at the very end, rounding up into the line
	for i in 9:
		var a := PI * 0.5 * i / 8.0
		pts.append(Vector2(r.position.x + curl - curl * cos(a), y + curl - curl * sin(a)))
	for i in 9:
		var a := PI * 0.5 * i / 8.0
		pts.append(Vector2(r.end.x - curl + curl * sin(a), y + curl - curl * cos(a)))
	ci.draw_polyline(pts, BAND_LINE, 2.0, true)
	# the title sits on a raised tab
	var tw := text_width(title, 22, "semi") + 70.0
	var tab := PackedVector2Array([Vector2(r.get_center().x - tw * 0.5, r.end.y), Vector2(r.get_center().x - tw * 0.5 + 12, y),
		Vector2(r.get_center().x + tw * 0.5 - 12, y), Vector2(r.get_center().x + tw * 0.5, r.end.y)])
	ci.draw_colored_polygon(tab, Color(0.2, 0.25, 0.31, 1.0))
	ci.draw_polyline(PackedVector2Array([tab[0], tab[1], tab[2], tab[3]]), Color(BAND_LINE, 0.6), 1.5, true)
	text(ci, Vector2(r.position.x, r.position.y + r.size.y * 0.5 + 8), title, 22, Color(0.86, 0.89, 0.92),
		HORIZONTAL_ALIGNMENT_CENTER, r.size.x)


## A chevron "<" (dir −1) or ">" (dir 1).
static func chevron(ci: CanvasItem, c: Vector2, dir: int, s := 9.0, col := VALUE) -> void:
	ci.draw_polyline(PackedVector2Array([c + Vector2(-dir * s * 0.45, -s), c + Vector2(dir * s * 0.45, 0), c + Vector2(-dir * s * 0.45, s)]),
		col, 2.6, true)


## Page dots under a choice: one per option, the chosen one cyan.
static func dots(ci: CanvasItem, c: Vector2, count: int, index: int) -> void:
	var n := mini(count, 16)
	var step := 7.0
	var x0 := c.x - (n - 1) * step * 0.5
	for i in n:
		var on := i == index or (count > n and i == n - 1 and index >= n - 1)
		ci.draw_circle(Vector2(x0 + i * step, c.y), 2.3 if on else 1.8, CYAN if on else Color(1, 1, 1, 0.55))


## A slider: dark track, cyan fill brightening toward the knob, a white knob.
static func slider(ci: CanvasItem, r: Rect2, f: float) -> void:
	f = clampf(f, 0.0, 1.0)
	var track := Rect2(r.position + Vector2(0, r.size.y * 0.5 - 4), Vector2(r.size.x, 8))
	ci.draw_rect(track, TRACK)
	ci.draw_rect(track, Color(1, 1, 1, 0.12), false, 1.0)
	var fill := Rect2(track.position, Vector2(track.size.x * f, track.size.y))
	var steps := 12
	for i in steps:
		var seg := Rect2(fill.position + Vector2(fill.size.x * i / steps, 0), Vector2(fill.size.x / steps + 0.5, fill.size.y))
		ci.draw_rect(seg, CYAN_DEEP.lerp(CYAN, float(i) / (steps - 1)))
	var kx := track.position.x + track.size.x * f
	var knob := Rect2(Vector2(kx - 4, r.position.y + r.size.y * 0.5 - 11), Vector2(8, 22))
	ci.draw_rect(knob.grow(1), Color(0, 0, 0, 0.5))
	ci.draw_rect(knob, Color(0.95, 0.97, 1.0))


## The pill switch: cyan with the knob right when on, dark with it left when off.
static func toggle(ci: CanvasItem, c: Vector2, on: bool) -> void:
	var w := 46.0
	var h := 18.0
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.2, 0.62, 0.78) if on else TRACK
	sb.set_corner_radius_all(9)
	sb.border_color = Color(1, 1, 1, 0.2)
	sb.set_border_width_all(1)
	ci.draw_style_box(sb, Rect2(c - Vector2(w, h) * 0.5, Vector2(w, h)))
	var k := c + Vector2((w * 0.5 - h * 0.5) * (1.0 if on else -1.0), 0)
	ci.draw_circle(k, h * 0.62, Color(0.05, 0.25, 0.36) if on else Color(0.25, 0.28, 0.32))
	ci.draw_circle(k, h * 0.42, Color(0.9, 0.97, 1.0))
	ci.draw_arc(k, h * 0.62, 0.0, TAU, 24, CYAN if on else Color(1, 1, 1, 0.3), 2.0, true)


## A key hint at the bottom of a menu: "[Esc] Quay lại".
static func hint(ci: CanvasItem, at: Vector2, key: String, what: String) -> float:
	var kw := text_width(key, 15, "bold") + 14.0
	var box := Rect2(at + Vector2(0, -16), Vector2(kw, 22))
	ci.draw_rect(box, Color(1, 1, 1, 0.1))
	ci.draw_rect(box, Color(1, 1, 1, 0.35), false, 1.0)
	text(ci, at + Vector2(7, 0), key, 15, VALUE, HORIZONTAL_ALIGNMENT_LEFT, -1, "bold")
	text(ci, at + Vector2(kw + 8, 0), what, 16, LABEL)
	return kw + 18.0 + text_width(what, 16)
