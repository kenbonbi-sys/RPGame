class_name WeaponArt
extends RefCounted
## The hero's weapons, built face by face so their bevels catch the light (flat shading, vertex
## colours), with the weapon's element glowing in runes, edges and gems (a second, emissive mesh).
## Every piece is made in the hand's T-pose frame: the fist holds it at the origin, +Z is where a
## blade points, the arm runs along -X for the right hand (+X for the left).
##
## build(id) -> [{"hand": "r"/"l", "metal": mesh, "glow": mesh or null, "grip": mesh or null}]

const STEEL := Color(0.64, 0.68, 0.72)
const STEEL_EDGE := Color(0.9, 0.92, 0.95)
const STEEL_DARK := Color(0.42, 0.46, 0.52)
const WOOD := Color(0.36, 0.24, 0.16)
const GOLD := Color(0.8, 0.6, 0.28)
const DARK_GOLD := Color(0.52, 0.38, 0.18)


static func build(id: String) -> Array:
	var it := Weapons.item(id)
	var glow := Elements.color(it.element) if it.element != "neutral" else Color(0.2, 0.85, 0.75)
	match String(it.family):
		"axe":
			return [_piece("r", _axe_metal(), _axe_glow(glow), _grip(-0.32, 0.05, 0.026))]
		"hammer":
			return [_piece("r", _hammer_metal(), _hammer_glow(glow), _grip(-0.3, 0.05, 0.028))]
		"war_pike":
			return [_piece("r", _pike_metal(), _pike_glow(glow), _grip(-0.2, 0.25, 0.022))]
		"chain_blades":
			return [_piece("r", _sickle_metal(1.0), _sickle_glow(glow, 1.0), _grip(-0.11, 0.05, 0.02)),
				_piece("l", _sickle_metal(-1.0), _sickle_glow(glow, -1.0), _grip(-0.11, 0.05, 0.02))]
		"aether_strikers":
			return [_piece("r", _gauntlet_metal(1.0), _gauntlet_glow(glow, 1.0), null),
				_piece("l", _gauntlet_metal(-1.0), _gauntlet_glow(glow, -1.0), null)]
		"repeaters":
			return [_piece("r", _pistol_metal(1.0), _pistol_glow(glow, 1.0), null),
				_piece("l", _pistol_metal(-1.0), _pistol_glow(glow, -1.0), null)]
	return [_piece("r", _sword_metal(), _sword_glow(glow, it.element != "neutral"), _grip(-0.17, 0.045, 0.024))]


static var _mats := {}


## The materials every weapon uses: polished metal (vertex colours, sRGB), leather for the grip,
## and "glow:<element>" for the accents, lit from inside in the element's colour (so they bloom).
static func material(kind: String) -> Material:
	if _mats.has(kind):
		return _mats[kind]
	var m := StandardMaterial3D.new()
	if kind == "metal":
		m.vertex_color_use_as_albedo = true
		m.vertex_color_is_srgb = true
		m.metallic = 0.6
		m.roughness = 0.34
		m.rim_enabled = true
		m.rim = 0.4
		m.rim_tint = 0.4
	elif kind.begins_with("glow"):
		var e := kind.trim_prefix("glow:")
		var col := Elements.color(e) if e != "neutral" and e != "glow" else Color(0.2, 0.85, 0.75)
		m.albedo_color = col
		m.emission_enabled = true
		m.emission = col
		m.emission_energy_multiplier = 2.6
		m.roughness = 0.3
	else:
		m.albedo_color = Color(0.24, 0.14, 0.09)
		m.roughness = 0.85
	_mats[kind] = m
	return m


static func _piece(hand: String, metal: ArrayMesh, glow: ArrayMesh, grip: ArrayMesh) -> Dictionary:
	return {"hand": hand, "metal": metal, "glow": glow, "grip": grip}


# ================================================================== the sword
static func _sword_metal() -> ArrayMesh:
	var st := _begin()
	_blade(st, [[0.1, 0.082, 0.026], [0.55, 0.09, 0.024], [0.98, 0.078, 0.021], [1.28, 0.0, 0.003]])
	_box(st, Vector3(0, 0, 0.08), Vector3(0.075, 0.2, 0.07), GOLD, DARK_GOLD)
	for side in [-1.0, 1.0]:
		_beam(st, Vector3(0, side * 0.1, 0.08), Vector3(0, side * 0.2, 0.17), 0.032, 0.018, GOLD, DARK_GOLD)
		_ball(st, Vector3(0, side * 0.2, 0.17), 0.026, GOLD)
	_ball(st, Vector3(0, 0, -0.2), 0.04, GOLD)
	return _end(st)


static func _sword_glow(col: Color, runes: bool) -> ArrayMesh:
	var st := _begin()
	_ball(st, Vector3(0.04, 0, 0.08), 0.028, col)
	_ball(st, Vector3(-0.04, 0, 0.08), 0.028, col)
	if runes:
		for side in [-1.0, 1.0]:
			_strip(st, Vector3(side * 0.0255, 0, 0.18), Vector3(side * 0.022, 0, 0.92), Vector3(0, 0.016, 0), col)
	return _end(st)


# ================================================================== the axe
static func _axe_metal() -> ArrayMesh:
	var st := _begin()
	_beam(st, Vector3(0, 0, -0.34), Vector3(0, 0, 0.9), 0.03, 0.026, WOOD, WOOD.darkened(0.25))
	# a broad crescent on +Y, thinning to the edge, and a spike behind
	# a bearded blade: two horns, a long curved edge, a narrow neck at the haft
	var crescent := [Vector2(0.03, 0.6), Vector2(0.14, 0.56), Vector2(0.27, 0.42), Vector2(0.35, 0.47), Vector2(0.41, 0.6),
		Vector2(0.43, 0.74), Vector2(0.41, 0.88), Vector2(0.35, 1.0), Vector2(0.27, 1.02), Vector2(0.15, 0.9), Vector2(0.03, 0.88)]
	_slab(st, crescent, 0.034, STEEL, STEEL_EDGE, 0.34)
	_slab(st, [Vector2(-0.03, 0.66), Vector2(-0.17, 0.74), Vector2(-0.03, 0.84)], 0.03, STEEL_DARK, STEEL, -1.0)
	_box(st, Vector3(0, 0, 0.74), Vector3(0.07, 0.1, 0.34), STEEL_DARK, STEEL)
	_ball(st, Vector3(0, 0, -0.37), 0.04, GOLD)
	return _end(st)


static func _axe_glow(col: Color) -> ArrayMesh:
	var st := _begin()
	var edge := [Vector2(0.27, 0.5), Vector2(0.34, 0.6), Vector2(0.36, 0.74), Vector2(0.34, 0.88), Vector2(0.27, 0.97)]
	for side in [-1.0, 1.0]:
		for i in edge.size() - 1:
			_strip(st, Vector3(side * 0.017, edge[i].x, edge[i].y), Vector3(side * 0.017, edge[i + 1].x, edge[i + 1].y),
				Vector3(0, -0.02, 0), col)
	_ball(st, Vector3(0, 0, 0.74), 0.045, col)
	return _end(st)


# ================================================================== the hammer
static func _hammer_metal() -> ArrayMesh:
	var st := _begin()
	_beam(st, Vector3(0, 0, -0.32), Vector3(0, 0, 0.72), 0.032, 0.03, WOOD, WOOD.darkened(0.25))
	_box(st, Vector3(0, 0, 0.84), Vector3(0.24, 0.42, 0.26), STEEL, STEEL_DARK)
	for side in [-1.0, 1.0]:
		_box(st, Vector3(0, side * 0.24, 0.84), Vector3(0.28, 0.06, 0.3), STEEL_EDGE, STEEL)
		_box(st, Vector3(0, side * 0.12, 0.98), Vector3(0.18, 0.08, 0.04), GOLD, DARK_GOLD)
	_box(st, Vector3(0, 0, 0.68), Vector3(0.12, 0.12, 0.08), GOLD, DARK_GOLD)
	_ball(st, Vector3(0, 0, -0.35), 0.045, GOLD)
	return _end(st)


static func _hammer_glow(col: Color) -> ArrayMesh:
	var st := _begin()
	for side in [-1.0, 1.0]:
		_box(st, Vector3(side * 0.123, 0, 0.84), Vector3(0.012, 0.16, 0.14), col, col)
	_box(st, Vector3(0, 0, 0.972), Vector3(0.1, 0.24, 0.012), col, col)
	return _end(st)


# ================================================================== the war pike
static func _pike_metal() -> ArrayMesh:
	var st := _begin()
	_beam(st, Vector3(0, 0, -0.78), Vector3(0, 0, 1.14), 0.024, 0.022, WOOD, WOOD.darkened(0.25))
	_blade(st, [[1.12, 0.03, 0.02], [1.24, 0.08, 0.024], [1.4, 0.06, 0.02], [1.62, 0.0, 0.003]], 1.12)
	for side in [-1.0, 1.0]:
		_beam(st, Vector3(0, side * 0.02, 1.1), Vector3(0, side * 0.12, 1.02), 0.022, 0.01, GOLD, DARK_GOLD)
	_box(st, Vector3(0, 0, 1.09), Vector3(0.06, 0.06, 0.06), GOLD, DARK_GOLD)
	_beam(st, Vector3(0, 0, -0.78), Vector3(0, 0, -0.92), 0.026, 0.004, STEEL_DARK, STEEL)
	return _end(st)


static func _pike_glow(col: Color) -> ArrayMesh:
	var st := _begin()
	for side in [-1.0, 1.0]:
		_strip(st, Vector3(side * 0.0235, 0, 1.18), Vector3(side * 0.018, 0, 1.46), Vector3(0, 0.014, 0), col)
	_ring(st, Vector3(0, 0, 1.0), 0.034, 0.012, col)
	_ring(st, Vector3(0, 0, 0.93), 0.032, 0.008, col)
	return _end(st)


# ================================================================== chain blades
static func _sickle_profile() -> Array:
	return [Vector2(-0.02, 0.05), Vector2(0.03, 0.18), Vector2(0.08, 0.32), Vector2(0.08, 0.43), Vector2(0.02, 0.51),
		Vector2(-0.08, 0.54), Vector2(-0.04, 0.47), Vector2(0.0, 0.4), Vector2(-0.01, 0.26), Vector2(-0.04, 0.12)]


static func _sickle_metal(_side: float) -> ArrayMesh:
	var st := _begin()
	_slab(st, _sickle_profile(), 0.022, STEEL, STEEL_EDGE, 0.06)
	_box(st, Vector3(0, 0, 0.055), Vector3(0.05, 0.11, 0.04), GOLD, DARK_GOLD)
	for i in 4:
		_ball(st, Vector3(0, 0, -0.14 - i * 0.05), 0.018, STEEL_DARK)
	return _end(st)


static func _sickle_glow(col: Color, _side: float) -> ArrayMesh:
	var st := _begin()
	var p: Array = _sickle_profile()
	for side in [-1.0, 1.0]:
		for i in range(1, 5):
			var a: Vector2 = p[i].lerp(p[p.size() - i], 0.25)
			var b: Vector2 = p[i + 1].lerp(p[p.size() - i - 1], 0.25)
			_strip(st, Vector3(side * 0.012, a.x, a.y), Vector3(side * 0.012, b.x, b.y), Vector3(0, -0.012, 0), col)
	return _end(st)


# ================================================================== aether strikers
static func _gauntlet_metal(side: float) -> ArrayMesh:
	var st := _begin()
	# knuckles over the fist, a plate on the back of the hand, a cuff at the wrist
	_box(st, Vector3(0.0, 0.0, 0.08), Vector3(0.15, 0.16, 0.09), STEEL, STEEL_DARK)
	_box(st, Vector3(0.0, 0.0, 0.0), Vector3(0.14, 0.15, 0.11), STEEL_DARK, STEEL)
	_box(st, Vector3(0.0, 0.085, 0.03), Vector3(0.1, 0.03, 0.14), GOLD, DARK_GOLD)
	for i in 3:
		var y := -0.05 + i * 0.05
		_beam(st, Vector3(0.0, y, 0.12), Vector3(0.0, y, 0.2), 0.026, 0.006, GOLD, DARK_GOLD)
	_beam(st, Vector3(side * 0.07, 0, 0.0), Vector3(side * 0.2, 0, 0.0), 0.085, 0.075, STEEL_DARK, STEEL)
	return _end(st)


static func _gauntlet_glow(col: Color, side: float) -> ArrayMesh:
	var st := _begin()
	_ball(st, Vector3(0.0, 0.105, 0.03), 0.034, col)
	_ring(st, Vector3(side * 0.15, 0, 0.0), 0.09, 0.013, col, Vector3.RIGHT)
	return _end(st)


# ================================================================== Ostian repeaters
static func _pistol_metal(side: float) -> ArrayMesh:
	var st := _begin()
	var fwd := Vector3(-side, 0, 0)
	_beam(st, Vector3(0, 0, -0.07), Vector3(0, 0, 0.06), 0.028, 0.026, WOOD, WOOD.darkened(0.2))
	_box(st, Vector3(0, 0, 0.09) + fwd * 0.08, Vector3(0.22, 0.06, 0.07).abs(), STEEL_DARK, STEEL)
	_beam(st, Vector3(0, 0, 0.1) + fwd * 0.12, Vector3(0, 0, 0.1) + fwd * 0.36, 0.022, 0.02, STEEL, STEEL_DARK)
	_beam(st, Vector3(0, 0, 0.065) + fwd * 0.15, Vector3(0, 0, 0.065) + fwd * 0.3, 0.016, 0.014, STEEL_DARK, STEEL)
	_box(st, Vector3(0, 0, 0.13) + fwd * 0.02, Vector3(0.06, 0.05, 0.03), GOLD, DARK_GOLD)
	return _end(st)


static func _pistol_glow(col: Color, side: float) -> ArrayMesh:
	var st := _begin()
	var fwd := Vector3(-side, 0, 0)
	_ring(st, Vector3(0, 0, 0.1) + fwd * 0.2, 0.026, 0.01, col, fwd)
	_ring(st, Vector3(0, 0, 0.1) + fwd * 0.3, 0.025, 0.008, col, fwd)
	_ball(st, Vector3(0, 0, 0.1) + fwd * 0.37, 0.016, col)
	return _end(st)


# ================================================================== a wrapped grip
static func _grip(z0: float, z1: float, r: float) -> ArrayMesh:
	var st := _begin()
	var bands := maxi(2, int((z1 - z0) / 0.03))
	for i in bands:
		var a := lerpf(z1, z0, float(i) / bands)
		var b := lerpf(z1, z0, float(i + 1) / bands)
		var ra := r if i % 2 == 0 else r * 0.88
		var rb := r * 0.88 if i % 2 == 0 else r
		for k in 8:
			var a0 := TAU * k / 8.0
			var a1 := TAU * (k + 1) / 8.0
			_quad(st, Vector3(cos(a0) * ra, sin(a0) * ra, a), Vector3(cos(a0) * rb, sin(a0) * rb, b),
				Vector3(cos(a1) * rb, sin(a1) * rb, b), Vector3(cos(a1) * ra, sin(a1) * ra, a), Color.WHITE)
	return _end(st)


# ================================================================== building blocks
static func _begin() -> SurfaceTool:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	return st


static func _end(st: SurfaceTool) -> ArrayMesh:
	st.generate_normals()
	return st.commit()


static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color) -> void:
	for p in [a, b, c]:
		st.set_color(col)
		st.add_vertex(p)


## A quad a-b-c-d, wound so its front faces out of the solid it bounds.
static func _quad(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3, col: Color) -> void:
	_tri(st, a, c, b, col)
	_tri(st, a, d, c, col)


## A blade along +Z with a diamond section (x thickness, y width): bright edge bevels, steel
## flats, a darker fuller. Stations [z, half width, half thickness]; the last is the point.
static func _blade(st: SurfaceTool, stations: Array, cap_z := 0.1) -> void:
	var ring := func(w: float, t: float) -> Array:
		return [Vector2(0, w), Vector2(t * 0.55, w * 0.74), Vector2(t, w * 0.18), Vector2(t, -w * 0.18), Vector2(t * 0.55, -w * 0.74),
			Vector2(0, -w), Vector2(-t * 0.55, -w * 0.74), Vector2(-t, -w * 0.18), Vector2(-t, w * 0.18), Vector2(-t * 0.55, w * 0.74)]
	var face_col := [STEEL_EDGE, STEEL, STEEL_DARK, STEEL, STEEL_EDGE, STEEL_EDGE, STEEL, STEEL_DARK, STEEL, STEEL_EDGE]
	for i in stations.size() - 1:
		var a: Array = stations[i]
		var b: Array = stations[i + 1]
		var ra: Array = ring.call(a[1], a[2])
		var rb: Array = ring.call(b[1], b[2])
		for k in ra.size():
			var k2 := (k + 1) % ra.size()
			var c: Color = face_col[k]
			if i == stations.size() - 2 and c == STEEL_DARK:
				c = STEEL
			_quad(st, Vector3(ra[k].x, ra[k].y, a[0]), Vector3(ra[k2].x, ra[k2].y, a[0]), Vector3(rb[k2].x, rb[k2].y, b[0]),
				Vector3(rb[k].x, rb[k].y, b[0]), c)
	var r0: Array = ring.call(stations[0][1], stations[0][2])
	for k in range(1, r0.size() - 1):
		_tri(st, Vector3(r0[0].x, r0[0].y, cap_z), Vector3(r0[k + 1].x, r0[k + 1].y, cap_z), Vector3(r0[k].x, r0[k].y, cap_z), STEEL)


## A flat plate from a 2D outline (y, z), `thick` across x, its faces fanned from the centre;
## outline points with y beyond `edge_from` (or below it, when negative) are the bright bevelled
## edge: there the plate thins to a cutting edge.
static func _slab(st: SurfaceTool, outline: Array, thick: float, col: Color, edge_col: Color, edge_from: float) -> void:
	var n := outline.size()
	var centre := Vector2.ZERO
	for p in outline:
		centre += p
	centre /= n
	var half := thick * 0.5
	var t := func(p: Vector2) -> float:
		var on_edge := (edge_from >= 0.0 and p.x >= edge_from) or (edge_from < 0.0 and p.x <= edge_from)
		return half * (0.18 if on_edge else 1.0)
	for side in [-1.0, 1.0]:
		var c3 := Vector3(side * half, centre.x, centre.y)
		for i in n:
			var a: Vector2 = outline[i]
			var b: Vector2 = outline[(i + 1) % n]
			var pa := Vector3(side * t.call(a), a.x, a.y)
			var pb := Vector3(side * t.call(b), b.x, b.y)
			if side > 0.0:
				_tri(st, c3, pb, pa, col)
			else:
				_tri(st, c3, pa, pb, col)
	for i in n:
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % n]
		var ec := edge_col if (edge_from >= 0.0 and a.x >= edge_from and b.x >= edge_from) or (edge_from < 0.0 and a.x <= edge_from) else col.darkened(0.15)
		_quad(st, Vector3(t.call(a), a.x, a.y), Vector3(-t.call(a), a.x, a.y), Vector3(-t.call(b), b.x, b.y), Vector3(t.call(b), b.x, b.y), ec)


## A box with flat faces; the faces looking along ±Y get `end_col`.
static func _box(st: SurfaceTool, at: Vector3, size: Vector3, col: Color, end_col: Color) -> void:
	var h := size * 0.5
	var c := [Vector3(-h.x, -h.y, -h.z), Vector3(h.x, -h.y, -h.z), Vector3(h.x, h.y, -h.z), Vector3(-h.x, h.y, -h.z),
		Vector3(-h.x, -h.y, h.z), Vector3(h.x, -h.y, h.z), Vector3(h.x, h.y, h.z), Vector3(-h.x, h.y, h.z)]
	var faces := [[0, 3, 2, 1, col], [4, 5, 6, 7, col], [0, 1, 5, 4, end_col], [3, 7, 6, 2, end_col], [0, 4, 7, 3, col], [1, 2, 6, 5, col]]
	for f in faces:
		_quad(st, at + c[f[0]], at + c[f[1]], at + c[f[2]], at + c[f[3]], f[4])


## A square beam from a to b, tapering from r0 to r1, capped.
static func _beam(st: SurfaceTool, a: Vector3, b: Vector3, r0: float, r1: float, col: Color, side_col: Color) -> void:
	var axis := (b - a).normalized()
	var u := axis.cross(Vector3.RIGHT if absf(axis.x) < 0.9 else Vector3.UP).normalized()
	var v := axis.cross(u).normalized()
	var corners := [u + v, u - v, -u - v, -u + v]
	for k in 4:
		var k2 := (k + 1) % 4
		_quad(st, a + corners[k] * r0, a + corners[k2] * r0, b + corners[k2] * r1, b + corners[k] * r1, col if k % 2 == 0 else side_col)
	_quad(st, b + corners[0] * r1, b + corners[1] * r1, b + corners[2] * r1, b + corners[3] * r1, col)
	_quad(st, a + corners[3] * r0, a + corners[2] * r0, a + corners[1] * r0, a + corners[0] * r0, col)


## A faceted ball (an octahedron split once).
static func _ball(st: SurfaceTool, at: Vector3, r: float, col: Color) -> void:
	var dirs := [Vector3.RIGHT, Vector3.LEFT, Vector3.UP, Vector3.DOWN, Vector3.BACK, Vector3.FORWARD]
	var faces := [[0, 2, 4], [2, 1, 4], [1, 3, 4], [3, 0, 4], [2, 0, 5], [1, 2, 5], [3, 1, 5], [0, 3, 5]]
	for f in faces:
		var a: Vector3 = dirs[f[0]]
		var b: Vector3 = dirs[f[1]]
		var c: Vector3 = dirs[f[2]]
		var ab := (a + b).normalized()
		var bc := (b + c).normalized()
		var ca := (c + a).normalized()
		for t in [[a, ab, ca], [ab, b, bc], [ca, bc, c], [ab, bc, ca]]:
			_tri(st, at + t[0] * r, at + t[2] * r, at + t[1] * r, col)


## A thin glowing strip from a to b, `across` wide (runes, edge lights), seen from both sides.
static func _strip(st: SurfaceTool, a: Vector3, b: Vector3, across: Vector3, col: Color) -> void:
	_quad(st, a - across, b - across, b + across, a + across, col)
	_quad(st, a + across, b + across, b - across, a - across, col)


## A ring of square section around `axis` (tassels, bands).
static func _ring(st: SurfaceTool, at: Vector3, r: float, thick: float, col: Color, axis := Vector3.BACK) -> void:
	var u := axis.cross(Vector3.UP if absf(axis.y) < 0.9 else Vector3.RIGHT).normalized()
	var v := axis.cross(u).normalized()
	for k in 10:
		var a0 := TAU * k / 10.0
		var a1 := TAU * (k + 1) / 10.0
		var p0 := at + (u * cos(a0) + v * sin(a0)) * r
		var p1 := at + (u * cos(a1) + v * sin(a1)) * r
		_beam(st, p0, p1, thick, thick, col, col)
