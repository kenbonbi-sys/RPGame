class_name HouseArt
extends RefCounted
## Village buildings in the Dauntless style, built in code from bevelled blocks: a stone plinth
## studded with stones, warm plaster walls in a dark timber frame (posts, braces, beams), an
## optional jettied upper floor, a steep gable roof laid tile by tile with mossy shingles, barge
## boards and crossed horns at the gables, a stone chimney that smokes, a plank door up two steps
## with a lantern, windows with frames, open shutters and flower boxes. Also the small things a
## village square needs: a market stall, barrels, crates, fences, banners.
##
## Houses face +Z (the door), the origin is the ground at the door.

const PAINTED := preload("res://shaders/painted.gdshader")
const ROOFS := [Color(0.82, 0.40, 0.24), Color(0.28, 0.50, 0.62), Color(0.74, 0.30, 0.24), Color(0.40, 0.56, 0.36), Color(0.34, 0.44, 0.68)]
const PLASTERS := [Color(0.97, 0.91, 0.78), Color(0.94, 0.85, 0.69), Color(0.99, 0.95, 0.88), Color(0.92, 0.82, 0.66)]
const SHUTTERS := [Color(0.24, 0.55, 0.60), Color(0.70, 0.25, 0.20), Color(0.30, 0.45, 0.72), Color(0.42, 0.58, 0.28)]
const FLOWERS := [Color(0.95, 0.30, 0.35), Color(1.0, 0.82, 0.30), Color(0.95, 0.55, 0.80), Color(0.98, 0.95, 0.92)]
const TIMBER := Color(0.46, 0.31, 0.20)
const STONE := Color(0.72, 0.69, 0.64)

static var _mats := {}


## Materials by role. Colour comes from the vertex colours.
static func material(role: String) -> ShaderMaterial:
	if _mats.has(role):
		return _mats[role]
	var m := ShaderMaterial.new()
	m.shader = PAINTED
	match role:
		"plaster":
			m.set_shader_parameter("paint", 0.1)
			m.set_shader_parameter("paint_scale", 0.9)
		"wood":
			m.set_shader_parameter("paint", 0.22)
			m.set_shader_parameter("paint_scale", 3.0)
		"stone":
			m.set_shader_parameter("paint", 0.2)
			m.set_shader_parameter("paint_scale", 1.6)
			m.set_shader_parameter("moss_amount", 0.7)
		"roof":
			m.set_shader_parameter("paint", 0.14)
			m.set_shader_parameter("paint_scale", 1.3)
			m.set_shader_parameter("moss_amount", 0.22)
			m.set_shader_parameter("specular_amount", 0.4)
		"glass":
			m.set_shader_parameter("glow", Color(1.0, 0.68, 0.32))
			m.set_shader_parameter("glow_energy", 1.6)
			m.set_shader_parameter("paint", 0.05)
		"trim":
			m.set_shader_parameter("paint", 0.12)
			m.set_shader_parameter("paint_scale", 2.0)
	_mats[role] = m
	return m


## A set of kits, one per material role, committed together into one mesh.
class Kits:
	var by_role := {}

	func k(role: String) -> MeshKit:
		if not by_role.has(role):
			by_role[role] = MeshKit.new()
		return by_role[role]

	func mesh() -> ArrayMesh:
		var out := ArrayMesh.new()
		for role in by_role:
			(by_role[role] as MeshKit).commit(out, HouseArt.material(role))
		return out


static func _xf(pos: Vector3, basis := Basis.IDENTITY) -> Transform3D:
	return Transform3D(basis, pos)


static func _shade(c: Color, rng: RandomNumberGenerator, amount := 0.1) -> Color:
	var f := rng.randf_range(1.0 - amount, 1.0 + amount)
	return Color(c.r * f, c.g * f, c.b * f)


# ================================================================== the house
## Builds a house `w` wide and `d` deep with one or two floors. Returns {node, size, height}.
static func house(rng: RandomNumberGenerator, w: float, d: float, storeys: int) -> Dictionary:
	var kits := Kits.new()
	var root := Node3D.new()
	root.name = "House"
	var plaster: Color = PLASTERS[rng.randi() % PLASTERS.size()]
	var roof_c: Color = ROOFS[rng.randi() % ROOFS.size()]
	var shutter: Color = SHUTTERS[rng.randi() % SHUTTERS.size()]
	var base_h := 0.8
	var h1 := 2.9
	var h2 := 2.5
	var jetty := 0.35 if storeys == 2 else 0.0
	var d_top := d + jetty * 2.0

	# plinth
	kits.k("stone").rounded_box(Vector3(w + 0.5, base_h + 1.4, d + 0.5), 0.14, _xf(Vector3(0, (base_h - 1.4) * 0.5, 0)), STONE)
	_plinth_stones(kits.k("stone"), rng, w + 0.5, d + 0.5, base_h)

	# floors
	var y := base_h
	var levels := []
	levels.append([y, h1, d])
	if storeys == 2:
		levels.append([y + h1 + 0.32, h2, d_top])
	for li in levels.size():
		var l: Array = levels[li]
		var y0: float = l[0]
		var hs: float = l[1]
		var ds: float = l[2]
		kits.k("plaster").rounded_box(Vector3(w, hs, ds), 0.1, _xf(Vector3(0, y0 + hs * 0.5, 0)), _shade(plaster, rng, 0.03))
		kits.k("wood").rounded_box(Vector3(w + 0.22, 0.26, ds + 0.22), 0.07, _xf(Vector3(0, y0 + 0.13, 0)), TIMBER)
		kits.k("wood").rounded_box(Vector3(w + 0.22, 0.3, ds + 0.22), 0.07, _xf(Vector3(0, y0 + hs - 0.12, 0)), _shade(TIMBER, rng, 0.05))
		_frame_walls(kits, rng, w, ds, y0, hs, li == 0, shutter)
	if storeys == 2:
		# joist ends under the jetty
		var yj: float = base_h + h1 + 0.12
		for side in [-1.0, 1.0]:
			var n := int(w / 0.9)
			for i in n + 1:
				var x := -w * 0.5 + 0.3 + (w - 0.6) * i / n
				kits.k("wood").rounded_box(Vector3(0.22, 0.24, 0.5), 0.05, _xf(Vector3(x, yj, side * (d * 0.5 + 0.2))), _shade(TIMBER, rng, 0.08))
	var y_top: float = levels[-1][0] + levels[-1][1]
	var pitch := rng.randf_range(0.95, 1.15)
	var ridge_y := _roof(kits, rng, w, d_top, y_top, pitch, roof_c, plaster)
	_chimney(kits, rng, root, Vector3(w * 0.28 * (1.0 if rng.randf() < 0.5 else -1.0), 0, -d * 0.22), ridge_y + 1.1)
	_door(kits, rng, root, d * 0.5, base_h)

	var mi := MeshInstance3D.new()
	mi.name = "Mesh"
	mi.mesh = kits.mesh()
	root.add_child(mi)
	return {"node": root, "size": Vector3(w + 0.5, ridge_y + 0.4, d_top + 0.5)}


## Two rows of stones of mixed lengths around the plinth.
static func _plinth_stones(k: MeshKit, rng: RandomNumberGenerator, w: float, d: float, base_h: float) -> void:
	var walls := [[w, d * 0.5, 0.0], [w, d * 0.5, PI], [d, w * 0.5, PI * 0.5], [d, w * 0.5, -PI * 0.5]]
	for wall in walls:
		var length: float = wall[0]
		var out: float = wall[1]
		var b := Basis(Vector3.UP, wall[2])
		for row in 2:
			var yy := 0.16 + row * 0.36
			var x := -length * 0.5 + (0.0 if row == 0 else -0.3)
			while x < length * 0.5:
				var l := rng.randf_range(0.45, 0.95)
				var cx := clampf(x + l * 0.5, -length * 0.5 + 0.2, length * 0.5 - 0.2)
				var sz := Vector3(minf(l, length * 0.5 - absf(cx) + 0.2) - 0.06, rng.randf_range(0.3, 0.38), 0.26)
				if sz.x > 0.15:
					k.rounded_box(sz, 0.08, _xf(b * Vector3(cx, yy, out + 0.04), b), _shade(STONE, rng, 0.14), 0.025, rng)
				x += l


## Posts, braces, windows (and the door bay) on the four walls of one floor.
static func _frame_walls(kits: Kits, rng: RandomNumberGenerator, w: float, ds: float, y0: float, hs: float, ground: bool, shutter: Color) -> void:
	var walls := [[w, ds * 0.5, 0.0, true], [w, ds * 0.5, PI, false], [ds, w * 0.5, PI * 0.5, false], [ds, w * 0.5, -PI * 0.5, false]]
	for wall in walls:
		var length: float = wall[0]
		var out: float = wall[1] + 0.05
		var b := Basis(Vector3.UP, wall[2])
		var front: bool = wall[3]
		var gable_side := absf(absf(wall[2]) - PI * 0.5) < 0.01
		var bays := maxi(2, roundi(length / 2.4))
		if front and ground and bays % 2 == 0:
			bays += 1
		var xs := []
		for i in bays + 1:
			xs.append(-length * 0.5 + 0.14 + (length - 0.28) * i / bays)
		for x in xs:
			kits.k("wood").rounded_box(Vector3(0.28, hs, 0.18), 0.06, _xf(b * Vector3(x, y0 + hs * 0.5, out), b), _shade(TIMBER, rng, 0.06))
		for i in bays:
			var x0: float = xs[i]
			var x1: float = xs[i + 1]
			var cx := (x0 + x1) * 0.5
			var bw := x1 - x0
			if front and ground and i == bays / 2:
				continue   # the door
			var brace := (gable_side and i % 2 == 0) or (not gable_side and not ground and bays >= 3 and (i == 0 or i == bays - 1))
			if brace or bw < 1.3:
				var dx := bw - 0.32
				var dy := hs - 0.5
				var ang := atan2(dx, dy) * (1.0 if i % 2 == 0 else -1.0)
				var bb := b * Basis(Vector3.BACK, ang)
				kits.k("wood").rounded_box(Vector3(0.2, sqrt(dx * dx + dy * dy), 0.14), 0.05, _xf(b * Vector3(cx, y0 + hs * 0.5, out), bb), _shade(TIMBER, rng, 0.08))
			else:
				_window(kits, rng, b, cx, y0 + hs * 0.56, out, minf(1.05, bw - 0.75), 1.15, shutter, ground and not gable_side)


static func _window(kits: Kits, rng: RandomNumberGenerator, b: Basis, cx: float, cy: float, out: float, ww: float, wh: float, shutter: Color, flowers: bool) -> void:
	var at := func(x: float, yy: float, z: float) -> Transform3D:
		return _xf(b * Vector3(x, yy, z), b)
	kits.k("glass").rounded_box(Vector3(ww, wh, 0.06), 0.02, at.call(cx, cy, out - 0.02), Color(1, 1, 1))
	var wood := kits.k("wood")
	var t := _shade(TIMBER, rng, 0.05)
	wood.rounded_box(Vector3(ww + 0.3, 0.16, 0.2), 0.05, at.call(cx, cy + wh * 0.5 + 0.06, out + 0.04), t)
	wood.rounded_box(Vector3(ww + 0.4, 0.14, 0.3), 0.05, at.call(cx, cy - wh * 0.5 - 0.06, out + 0.08), t)
	for s in [-1.0, 1.0]:
		wood.rounded_box(Vector3(0.14, wh + 0.2, 0.18), 0.04, at.call(cx + s * (ww * 0.5 + 0.05), cy, out + 0.03), t)
	wood.rounded_box(Vector3(0.07, wh, 0.08), 0.02, at.call(cx, cy, out + 0.02), t)
	wood.rounded_box(Vector3(ww, 0.07, 0.08), 0.02, at.call(cx, cy + wh * 0.08, out + 0.02), t)
	var sh := _shade(shutter, rng, 0.05)
	for s in [-1.0, 1.0]:
		var sx: float = cx + s * (ww * 0.75 + 0.14)
		kits.k("trim").rounded_box(Vector3(ww * 0.5, wh + 0.05, 0.07), 0.03, at.call(sx, cy, out + 0.05), sh)
		kits.k("trim").rounded_box(Vector3(ww * 0.5 - 0.1, 0.06, 0.09), 0.02, at.call(sx, cy + wh * 0.25, out + 0.07), sh * 0.8)
		kits.k("trim").rounded_box(Vector3(ww * 0.5 - 0.1, 0.06, 0.09), 0.02, at.call(sx, cy - wh * 0.25, out + 0.07), sh * 0.8)
	if flowers:
		var by := cy - wh * 0.5 - 0.3
		wood.rounded_box(Vector3(ww + 0.3, 0.28, 0.3), 0.05, at.call(cx, by, out + 0.22), _shade(TIMBER, rng, 0.1) * 1.15)
		var green := func(_p: Vector3, _c: Vector3) -> Color:
			return Color(0.35, 0.62, 0.24)
		for i in 5:
			var fx := cx - ww * 0.5 + 0.05 + (ww - 0.1) * i / 4.0
			kits.k("trim").lump(Vector3(0.17, 0.13, 0.15), 1, at.call(fx, by + 0.2, out + 0.24), 0.2, i, green)
			var fc: Color = FLOWERS[rng.randi() % FLOWERS.size()]
			var bloom := func(_p: Vector3, _c: Vector3) -> Color:
				return fc
			kits.k("trim").lump(Vector3.ONE * 0.09, 1, at.call(fx + rng.randf_range(-0.06, 0.06), by + 0.33, out + 0.26 + rng.randf_range(-0.05, 0.05)), 0.1, i + 7, bloom)


## The gable roof (ridge along X). Returns the ridge height.
static func _roof(kits: Kits, rng: RandomNumberGenerator, w: float, d_top: float, y_top: float, pitch: float, roof_c: Color, plaster: Color) -> float:
	var oe := 0.75
	var og := 0.55
	var half := d_top * 0.5
	var ridge_y := y_top + half * pitch
	var eave_y := y_top - oe * pitch
	var s := half + oe
	var length := Vector2(s, ridge_y - eave_y).length()
	var rw := w + og * 2.0
	# gable ends
	for side in [-1.0, 1.0]:
		var gx: float = side * (w * 0.5 - 0.08)
		kits.k("plaster").prism(Vector2(-half, 0), Vector2(half, 0), Vector2(0, half * pitch), 0.18,
			_xf(Vector3(gx, y_top, 0), Basis(Vector3.UP, PI * 0.5)), plaster * 0.96)
		kits.k("wood").rounded_box(Vector3(0.16, half * pitch, 0.24), 0.04, _xf(Vector3(gx + side * 0.06, y_top + half * pitch * 0.5, 0)), TIMBER)
	for side in [-1.0, 1.0]:
		var e := Vector3(0, eave_y, side * s)
		var r := Vector3(0, ridge_y, 0)
		var up := (r - e) / length
		var z := -up
		var x := Vector3.RIGHT
		var y := z.cross(x).normalized()
		if y.y < 0.0:
			x = -x
			y = z.cross(x).normalized()
		var basis := Basis(x, y, z)
		# the deck
		var mid := (e + r) * 0.5 - y * 0.12
		kits.k("wood").rounded_box(Vector3(rw, 0.24, length + 0.1), 0.06, _xf(mid, basis), TIMBER * 0.8)
		# tiles, row by row from the eave up
		var rows := int(ceil(length / 0.46))
		var tile_w := 0.82
		var cols := int(ceil(rw / tile_w)) + 1
		for row in rows:
			var t := (row + 0.55) * length / rows
			var shade := 0.88 + 0.12 * float(row) / rows
			var off := 0.0 if row % 2 == 0 else tile_w * 0.5
			for c in cols:
				var tx := -rw * 0.5 + off + c * tile_w
				if tx < -rw * 0.5 - 0.1 or tx > rw * 0.5 + 0.1:
					continue
				var tw := tile_w - 0.06
				var lo := maxf(tx - tw * 0.5, -rw * 0.5)
				var hi := minf(tx + tw * 0.5, rw * 0.5)
				if hi - lo < 0.2:
					continue
				var cxx := (lo + hi) * 0.5
				var tb := basis * Basis(Vector3.RIGHT, -0.1) * Basis(Vector3.UP, rng.randf_range(-0.04, 0.04))
				var p := e + up * t + x * cxx * (1.0 if x.x > 0 else -1.0) + y * 0.07
				kits.k("roof").rounded_box(Vector3(hi - lo, 0.11, 0.6), 0.045, _xf(p, tb), _shade(roof_c * shade, rng, 0.08), 0.012, rng)
		# barge boards along the gable edges
		for gs in [-1.0, 1.0]:
			var bp := (e + r) * 0.5 + Vector3(gs * (rw * 0.5 + 0.04), 0, 0) + y * 0.02
			kits.k("wood").rounded_box(Vector3(0.18, 0.34, length + 0.15), 0.05, _xf(bp, basis), TIMBER)
		# fascia under the eave
		kits.k("wood").rounded_box(Vector3(rw + 0.1, 0.26, 0.16), 0.05, _xf(e - y * 0.05, basis), TIMBER * 0.9)
	# ridge and the crossed horns at the gables
	kits.k("roof").rounded_box(Vector3(rw + 0.15, 0.3, 0.5), 0.1, _xf(Vector3(0, ridge_y + 0.1, 0)), roof_c * 0.72)
	for gs in [-1.0, 1.0]:
		for hs in [-1.0, 1.0]:
			# each horn carries one barge board on past the ridge
			var hb := Basis(Vector3.RIGHT, hs * atan(pitch))
			kits.k("wood").rounded_box(Vector3(0.16, 0.16, 1.3), 0.05, _xf(Vector3(gs * (rw * 0.5 + 0.04), ridge_y + 0.3, -hs * 0.3), hb), TIMBER)
	return ridge_y


static func _chimney(kits: Kits, rng: RandomNumberGenerator, root: Node3D, at: Vector3, top: float) -> void:
	var k := kits.k("stone")
	k.rounded_box(Vector3(1.0, top, 1.0), 0.1, _xf(at + Vector3(0, top * 0.5, 0)), STONE * 0.95)
	var y := top - 2.6
	while y < top - 0.3:
		k.rounded_box(Vector3(1.12, 0.32, 1.12), 0.08, _xf(at + Vector3(0, y, 0)), _shade(STONE, rng, 0.12), 0.02, rng)
		y += 0.7
	k.rounded_box(Vector3(1.25, 0.26, 1.25), 0.08, _xf(at + Vector3(0, top + 0.1, 0)), STONE * 0.85)
	k.rounded_box(Vector3(0.6, 0.06, 0.6), 0.02, _xf(at + Vector3(0, top + 0.22, 0)), Color(0.15, 0.13, 0.12))
	if Game.has_screen():
		root.add_child(_smoke(at + Vector3(0, top + 0.4, 0)))


static func _smoke(at: Vector3) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.amount = 14
	p.lifetime = 4.0
	p.position = at
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0.3, 1, 0)
	pm.spread = 10.0
	pm.initial_velocity_min = 0.6
	pm.initial_velocity_max = 1.0
	pm.gravity = Vector3(0.25, 0.25, 0)
	pm.scale_min = 0.8
	pm.scale_max = 1.3
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.4))
	curve.add_point(Vector2(1, 1.6))
	var ct := CurveTexture.new()
	ct.curve = curve
	pm.scale_curve = ct
	var grad := Gradient.new()
	grad.set_color(0, Color(0.92, 0.9, 0.88, 0.0))
	grad.add_point(0.15, Color(0.9, 0.88, 0.86, 0.45))
	grad.set_color(grad.get_point_count() - 1, Color(0.85, 0.85, 0.88, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = grad
	pm.color_ramp = gt
	p.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(1.4, 1.4)
	q.material = Fx._quad_material(false)
	p.draw_pass_1 = q
	return p


static func _door(kits: Kits, rng: RandomNumberGenerator, root: Node3D, front: float, base_h: float) -> void:
	var z := front + 0.05
	var wood := kits.k("wood")
	for i in 3:
		wood.rounded_box(Vector3(0.4, 2.2, 0.12), 0.04, _xf(Vector3(-0.41 + i * 0.41, base_h + 1.1, z)), _shade(TIMBER * 1.3, rng, 0.08))
	for s in [-1.0, 1.0]:
		wood.rounded_box(Vector3(0.24, 2.6, 0.24), 0.06, _xf(Vector3(s * 0.75, base_h + 1.3, z + 0.04)), TIMBER)
		wood.rounded_box(Vector3(1.2, 0.1, 0.06), 0.02, _xf(Vector3(0, base_h + 0.6 + (s + 1.0) * 0.55, z + 0.08)), TIMBER * 0.7)
	wood.rounded_box(Vector3(1.9, 0.3, 0.3), 0.07, _xf(Vector3(0, base_h + 2.55, z + 0.06)), TIMBER)
	var gold := func(_p: Vector3, _c: Vector3) -> Color:
		return Color(0.95, 0.75, 0.35)
	kits.k("trim").lump(Vector3.ONE * 0.07, 1, _xf(Vector3(0.38, base_h + 1.1, z + 0.1)), 0.0, 3, gold)
	# steps
	kits.k("stone").rounded_box(Vector3(2.0, 0.42, 0.95), 0.08, _xf(Vector3(0, 0.15, z + 0.55)), _shade(STONE, rng, 0.08), 0.02, rng)
	kits.k("stone").rounded_box(Vector3(1.8, 0.42, 0.6), 0.08, _xf(Vector3(0, 0.5, z + 0.3)), _shade(STONE, rng, 0.08), 0.02, rng)
	# lantern on a bracket
	var lx := 1.15
	wood.rounded_box(Vector3(0.08, 0.08, 0.45), 0.02, _xf(Vector3(lx, base_h + 2.25, z + 0.22)), TIMBER)
	kits.k("glass").rounded_box(Vector3(0.24, 0.32, 0.24), 0.04, _xf(Vector3(lx, base_h + 2.0, z + 0.42)), Color(1, 1, 1))
	kits.k("trim").rounded_box(Vector3(0.32, 0.08, 0.32), 0.03, _xf(Vector3(lx, base_h + 2.2, z + 0.42)), Color(0.2, 0.18, 0.16))
	if Game.has_screen():
		var light := OmniLight3D.new()
		light.light_color = Color(1.0, 0.7, 0.4)
		light.light_energy = 1.4
		light.omni_range = 6.0
		light.position = Vector3(lx, base_h + 2.0, z + 0.6)
		root.add_child(light)


# ================================================================== village bits
## A market stall: four posts, a counter, a striped cloth awning, goods on top.
static func stall(rng: RandomNumberGenerator, cloth_a: Color, cloth_b: Color) -> Node3D:
	var kits := Kits.new()
	var w := 3.4
	var d := 2.0
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			var h := 2.7 if sz < 0 else 2.2
			kits.k("wood").rounded_box(Vector3(0.18, h, 0.18), 0.05, _xf(Vector3(sx * w * 0.5, h * 0.5, sz * d * 0.5)), _shade(TIMBER, rng, 0.08))
	kits.k("wood").rounded_box(Vector3(w + 0.2, 0.9, 0.7), 0.06, _xf(Vector3(0, 0.45, d * 0.5 - 0.2)), TIMBER * 1.2)
	kits.k("wood").rounded_box(Vector3(w + 0.3, 0.1, 0.85), 0.03, _xf(Vector3(0, 0.95, d * 0.5 - 0.2)), TIMBER * 0.9)
	var stripes := 6
	var slope := Basis(Vector3.RIGHT, 0.24)
	for i in stripes:
		var x := -w * 0.5 - 0.15 + (w + 0.3) * (i + 0.5) / stripes
		kits.k("trim").rounded_box(Vector3((w + 0.3) / stripes, 0.06, d + 0.6), 0.02, _xf(Vector3(x, 2.5, 0.05), slope), cloth_a if i % 2 == 0 else cloth_b)
		kits.k("trim").rounded_box(Vector3((w + 0.3) / stripes, 0.3, 0.05), 0.02, _xf(Vector3(x, 2.17, d * 0.5 + 0.33)), cloth_a if i % 2 == 0 else cloth_b)
	var fruit := [Color(0.9, 0.3, 0.25), Color(0.95, 0.75, 0.3), Color(0.55, 0.75, 0.3)]
	for i in 7:
		var fc: Color = fruit[i % 3]
		var cf := func(_p: Vector3, _c: Vector3) -> Color:
			return fc
		kits.k("trim").lump(Vector3.ONE * rng.randf_range(0.12, 0.16), 1, _xf(Vector3(-1.3 + i * 0.42, 1.12, d * 0.5 - 0.2 + rng.randf_range(-0.15, 0.15))), 0.1, i, cf)
	var root := Node3D.new()
	root.name = "Stall"
	var mi := MeshInstance3D.new()
	mi.mesh = kits.mesh()
	root.add_child(mi)
	return root


## A banded barrel.
static func barrel_mesh(rng: RandomNumberGenerator) -> ArrayMesh:
	var kits := Kits.new()
	var c := _shade(TIMBER * 1.25, rng, 0.08)
	kits.k("wood").tube(0.42, 0.48, 0.55, 10, _xf(Vector3.ZERO), c, Vector3.ZERO, 2)
	kits.k("wood").tube(0.48, 0.42, 0.55, 10, _xf(Vector3(0, 0.55, 0)), c, Vector3.ZERO, 2)
	for yy in [0.12, 0.55, 0.98]:
		var r := 0.43 + 0.05 * (1.0 - absf(yy - 0.55) / 0.43)
		kits.k("trim").tube(r + 0.02, r + 0.02, 0.08, 10, _xf(Vector3(0, yy, 0)), Color(0.32, 0.3, 0.3), Vector3.ZERO, 1)
	return kits.mesh()


static func crate_mesh(rng: RandomNumberGenerator) -> ArrayMesh:
	var kits := Kits.new()
	kits.k("wood").rounded_box(Vector3(0.9, 0.9, 0.9), 0.06, _xf(Vector3(0, 0.45, 0)), _shade(TIMBER * 1.35, rng, 0.08))
	for s in [-1.0, 1.0]:
		kits.k("wood").rounded_box(Vector3(0.96, 0.14, 0.96), 0.04, _xf(Vector3(0, 0.45 + s * 0.36, 0)), TIMBER)
	return kits.mesh()


## A fence of posts and two rails, `length` long along +X.
static func fence_mesh(rng: RandomNumberGenerator, length: float) -> ArrayMesh:
	var kits := Kits.new()
	var n := maxi(1, roundi(length / 2.0))
	for i in n + 1:
		var x := -length * 0.5 + length * i / n
		kits.k("wood").rounded_box(Vector3(0.2, 1.2, 0.2), 0.05, _xf(Vector3(x, 0.5, 0), Basis(Vector3.BACK, rng.randf_range(-0.05, 0.05))), _shade(TIMBER * 1.1, rng, 0.1))
	for yy in [0.45, 0.85]:
		kits.k("wood").rounded_box(Vector3(length + 0.2, 0.12, 0.1), 0.03, _xf(Vector3(0, yy, 0.1)), _shade(TIMBER * 1.2, rng, 0.06))
	return kits.mesh()


## A tall pole with a hanging banner in the village colours.
static func banner_mesh(cloth: Color, trim: Color) -> ArrayMesh:
	var kits := Kits.new()
	kits.k("wood").tube(0.09, 0.07, 5.2, 6, _xf(Vector3.ZERO), TIMBER)
	kits.k("wood").rounded_box(Vector3(1.3, 0.1, 0.1), 0.03, _xf(Vector3(0.45, 4.9, 0)), TIMBER)
	kits.k("trim").rounded_box(Vector3(1.0, 2.0, 0.05), 0.02, _xf(Vector3(0.45, 3.85, 0)), cloth)
	kits.k("trim").rounded_box(Vector3(1.04, 0.12, 0.07), 0.02, _xf(Vector3(0.45, 2.85, 0)), trim)
	kits.k("trim").prism(Vector2(-0.5, 0), Vector2(0.5, 0), Vector2(0, -0.45), 0.05, _xf(Vector3(0.45, 2.8, 0)), cloth)
	var gold := func(_p: Vector3, _c: Vector3) -> Color:
		return trim
	kits.k("trim").lump(Vector3.ONE * 0.2, 1, _xf(Vector3(0.45, 3.95, 0.04)), 0.0, 1, gold)
	return kits.mesh()
