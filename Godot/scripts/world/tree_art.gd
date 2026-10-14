class_name TreeArt
extends RefCounted
## Trees and bushes in the Dauntless style, built in code: thick tapering trunks with root flares
## and a few branches, crowns of big puffy lumps that shade as one soft mass (canopy.gdshader).
## Sizes are in metres. Every kind comes in a few seeds and two levels of detail (near and far)
## that share the same plan, so the swap at distance does not change the silhouette.
##
## Names are "<kind>_<seed>": oak, oakdark, round, tall, blossom, pine, swamp, acacia, bush, and
## "village" (the great oak on the green).

const CANOPY := preload("res://shaders/canopy.gdshader")
const PAINTED := preload("res://shaders/painted.gdshader")

## crown tones: [top, mid, under]
const TONES := {
	"forest": [Color(0.74, 0.86, 0.34), Color(0.33, 0.60, 0.22), Color(0.06, 0.26, 0.22)],
	"dark": [Color(0.54, 0.72, 0.30), Color(0.21, 0.46, 0.22), Color(0.04, 0.19, 0.19)],
	"light": [Color(0.86, 0.92, 0.40), Color(0.45, 0.70, 0.26), Color(0.10, 0.32, 0.22)],
	"blossom": [Color(1.0, 0.82, 0.90), Color(0.94, 0.55, 0.70), Color(0.46, 0.20, 0.36)],
	"pine": [Color(0.44, 0.70, 0.44), Color(0.15, 0.42, 0.32), Color(0.03, 0.17, 0.20)],
	"swamp": [Color(0.60, 0.64, 0.30), Color(0.30, 0.40, 0.20), Color(0.07, 0.15, 0.12)],
	"steppe": [Color(0.98, 0.84, 0.38), Color(0.76, 0.62, 0.24), Color(0.34, 0.24, 0.12)],
}
const KIND_TONE := {"oak": "forest", "oakdark": "dark", "round": "light", "tall": "forest", "blossom": "blossom",
	"pine": "pine", "swamp": "swamp", "acacia": "steppe", "bush": "light", "village": "forest"}
const BARK := {"swamp": Color(0.30, 0.26, 0.22), "pine": Color(0.40, 0.27, 0.20), "acacia": Color(0.52, 0.40, 0.30)}

static var _plans := {}
static var _meshes := {}
static var _mats := {}


## The mesh for a named tree; `far` is the light version for distant trees.
static func mesh(tree_name: String, far := false) -> ArrayMesh:
	var key := tree_name + ("|far" if far else "")
	if _meshes.has(key):
		return _meshes[key]
	var m := _build(plan(tree_name), far)
	_meshes[key] = m
	return m


## How thick the trunk is at the ground (for the collider).
static func trunk_radius(tree_name: String) -> float:
	return plan(tree_name).trunk_r


## A tree standing on its own (the village oak, ornamental trees): a MeshInstance3D.
static func instance(tree_name: String, scale := 1.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh(tree_name)
	mi.scale = Vector3.ONE * scale
	return mi


# ------------------------------------------------------------------ plans
static func plan(tree_name: String) -> Dictionary:
	if _plans.has(tree_name):
		return _plans[tree_name]
	var bits := tree_name.split("_")
	var kind := bits[0]
	var seed := int(bits[1]) if bits.size() > 1 else 0
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(kind) * 31 + seed * 7919 + 17
	var p := {"kind": kind, "tone": KIND_TONE.get(kind, "forest"), "trunk_r": 0.0, "limbs": [], "lumps": [],
		"crown": [Vector3.ZERO, Vector3.ONE], "strands": [], "crown_base": 3.0}
	match kind:
		"oak", "oakdark", "village":
			var big := 1.6 if kind == "village" else 1.0
			_broadleaf(p, rng, rng.randf_range(4.0, 5.0) * big, 0.58 * big,
				Vector3(rng.randf_range(3.8, 4.4), rng.randf_range(2.8, 3.3), rng.randf_range(3.8, 4.4)) * big, 9, Vector2(1.7, 2.6) * big)
		"round", "blossom":
			_broadleaf(p, rng, rng.randf_range(3.0, 3.6), 0.44, Vector3(3.2, 2.7, 3.2) * rng.randf_range(0.9, 1.1), 7, Vector2(1.4, 2.2))
		"tall":
			_broadleaf(p, rng, rng.randf_range(5.0, 6.0), 0.46, Vector3(2.8, 4.6, 2.8) * rng.randf_range(0.9, 1.1), 8, Vector2(1.3, 2.0))
		"pine":
			_pine(p, rng)
		"swamp":
			_swamp(p, rng)
		"acacia":
			_acacia(p, rng)
		"bush":
			_bush(p, rng)
	_plans[tree_name] = p
	return p


## A trunk to `trunk_h`, a few limbs reaching up into a round crown of lumps.
static func _broadleaf(p: Dictionary, rng: RandomNumberGenerator, trunk_h: float, r0: float, radii: Vector3, count: int, lump_r: Vector2) -> void:
	var lean := Vector3(rng.randf_range(-0.5, 0.5), 0, rng.randf_range(-0.5, 0.5))
	var top := Vector3(0, trunk_h, 0) + lean
	p.trunk_r = r0
	p.limbs.append([Vector3.ZERO, top, r0, r0 * 0.62, lean])
	_roots(p, rng, r0)
	var centre := top + Vector3(0, radii.y * 0.72, 0)
	p.crown = [centre, radii]
	p.crown_base = trunk_h * 0.8
	_lumps(p, rng, centre, radii, count, lump_r, 0.88)
	# limbs from the top of the trunk toward the outer lumps
	var n := 3 if count >= 7 else 2
	for i in n:
		var a := TAU * (float(i) / n) + rng.randf_range(-0.4, 0.4)
		var to := centre + Vector3(cos(a) * radii.x * 0.55, rng.randf_range(-0.2, 0.25) * radii.y, sin(a) * radii.z * 0.55)
		p.limbs.append([top - Vector3(0, 0.3, 0), to, r0 * 0.42, r0 * 0.12, Vector3.ZERO])
	p.limbs.append([top, centre + Vector3(0, radii.y * 0.2, 0), r0 * 0.5, r0 * 0.18, Vector3.ZERO])


static func _roots(p: Dictionary, rng: RandomNumberGenerator, r0: float) -> void:
	var n := rng.randi_range(4, 5)
	for i in n:
		var a := TAU * i / n + rng.randf_range(-0.3, 0.3)
		var out := Vector3(cos(a), 0, sin(a))
		p.limbs.append([out * r0 * 0.3 + Vector3(0, r0 * 1.1, 0), out * r0 * rng.randf_range(1.9, 2.5) + Vector3(0, -0.15, 0),
			r0 * 0.42, r0 * 0.1, Vector3(0, -0.2, 0)])


## Lumps spread over the crown's ellipsoid: a big one in the heart, the rest on the shell, kept
## off the very bottom so the crown sits on the limbs.
static func _lumps(p: Dictionary, rng: RandomNumberGenerator, centre: Vector3, radii: Vector3, count: int, lump_r: Vector2, flatten: float) -> void:
	p.lumps.append([centre, Vector3(1.0, flatten, 1.0) * lump_r.y * 1.05, rng.randf()])
	var golden := PI * (3.0 - sqrt(5.0))
	for i in count - 1:
		var y := 1.0 - (float(i) + 0.5) / float(count - 1) * 1.55
		var r := sqrt(maxf(0.0, 1.0 - y * y))
		var a := golden * i + rng.randf_range(-0.35, 0.35)
		var dir := Vector3(cos(a) * r, y, sin(a) * r)
		var at := centre + dir * radii * rng.randf_range(0.55, 0.72)
		var s := rng.randf_range(lump_r.x, lump_r.y)
		p.lumps.append([at, Vector3(1.0, flatten, 1.0) * s, rng.randf()])


static func _pine(p: Dictionary, rng: RandomNumberGenerator) -> void:
	var h := rng.randf_range(10.0, 13.0)
	var lean := Vector3(rng.randf_range(-0.25, 0.25), 0, rng.randf_range(-0.25, 0.25))
	p.trunk_r = 0.42
	p.limbs.append([Vector3.ZERO, Vector3(0, h, 0) + lean, 0.42, 0.06, lean])
	_roots(p, rng, 0.42)
	var tiers := 5
	p.crown = [Vector3(0, h * 0.58, 0) + lean * 0.5, Vector3(3.0, h * 0.42, 3.0)]
	p.crown_base = h * 0.25
	for t in tiers:
		var k := float(t) / (tiers - 1)
		var y := lerpf(h * 0.3, h * 0.92, k)
		var c := Vector3(0, y, 0) + lean * (y / h)
		var r := lerpf(3.0, 0.9, k) * rng.randf_range(0.92, 1.08)
		p.lumps.append([c, Vector3(r, r * 0.42 + 0.35, r), rng.randf()])
		var rim := 4 if t < tiers - 1 else 0
		for j in rim:
			var a := TAU * j / rim + rng.randf_range(-0.4, 0.4) + t
			p.lumps.append([c + Vector3(cos(a) * r * 0.62, -0.35, sin(a) * r * 0.62), Vector3.ONE * r * 0.42 * Vector3(1, 0.7, 1), rng.randf()])


static func _swamp(p: Dictionary, rng: RandomNumberGenerator) -> void:
	var h := rng.randf_range(5.0, 6.5)
	var bend := Vector3(rng.randf_range(-1.6, 1.6), 0, rng.randf_range(-1.6, 1.6))
	p.trunk_r = 0.4
	p.limbs.append([Vector3.ZERO, Vector3(0, h, 0) + bend, 0.42, 0.2, bend])
	_roots(p, rng, 0.5)
	var top := Vector3(0, h, 0) + bend
	var radii := Vector3(3.6, 1.7, 3.6)
	var centre := top + Vector3(0, 1.0, 0)
	p.crown = [centre, radii]
	p.crown_base = h * 0.7
	for i in 3:
		var a := TAU * i / 3.0 + rng.randf_range(-0.5, 0.5)
		p.limbs.append([top - Vector3(0, 0.4, 0), centre + Vector3(cos(a) * 2.6, rng.randf_range(-0.6, 0.4), sin(a) * 2.6), 0.2, 0.06, Vector3(0, 0.5, 0)])
	_lumps(p, rng, centre, radii, 6, Vector2(1.1, 1.8), 0.62)
	# hanging moss
	for i in 9:
		var a := rng.randf() * TAU
		var r := rng.randf_range(1.0, 3.2)
		p.strands.append([centre + Vector3(cos(a) * r, -0.6, sin(a) * r), rng.randf_range(1.2, 2.8)])


static func _acacia(p: Dictionary, rng: RandomNumberGenerator) -> void:
	var h := rng.randf_range(3.0, 3.8)
	p.trunk_r = 0.34
	p.limbs.append([Vector3.ZERO, Vector3(0, h, 0), 0.36, 0.22, Vector3(rng.randf_range(-0.4, 0.4), 0, rng.randf_range(-0.4, 0.4))])
	_roots(p, rng, 0.34)
	var crown_y := h + rng.randf_range(2.2, 2.8)
	var radii := Vector3(4.6, 1.0, 4.6) * rng.randf_range(0.9, 1.1)
	var centre := Vector3(0, crown_y, 0)
	p.crown = [centre, radii]
	p.crown_base = h
	var n := 3
	for i in n:
		var a := TAU * i / n + rng.randf_range(-0.4, 0.4)
		p.limbs.append([Vector3(0, h - 0.2, 0), centre + Vector3(cos(a) * 2.6, -0.4, sin(a) * 2.6), 0.2, 0.08, Vector3(0, 0.6, 0)])
	p.lumps.append([centre, Vector3(2.4, 0.95, 2.4), rng.randf()])
	for i in 6:
		var a := TAU * i / 6.0 + rng.randf_range(-0.3, 0.3)
		var r := rng.randf_range(2.4, 3.3)
		p.lumps.append([centre + Vector3(cos(a) * r, rng.randf_range(-0.3, 0.2), sin(a) * r), Vector3(1.0, 0.5, 1.0) * rng.randf_range(1.3, 1.9), rng.randf()])


static func _bush(p: Dictionary, rng: RandomNumberGenerator) -> void:
	p.trunk_r = 0.0
	var radii := Vector3(1.3, 0.85, 1.3) * rng.randf_range(0.85, 1.15)
	var centre := Vector3(0, 0.55, 0)
	p.crown = [centre, radii]
	p.crown_base = 0.2
	p.lumps.append([centre, Vector3(0.95, 0.8, 0.95), rng.randf()])
	var n := rng.randi_range(3, 4)
	for i in n:
		var a := TAU * i / n + rng.randf_range(-0.4, 0.4)
		var r := rng.randf_range(0.55, 0.85)
		p.lumps.append([Vector3(cos(a) * r, rng.randf_range(0.35, 0.65), sin(a) * r), Vector3.ONE * rng.randf_range(0.55, 0.8), rng.randf()])


# ------------------------------------------------------------------ meshes
static func _build(p: Dictionary, far: bool) -> ArrayMesh:
	var out := ArrayMesh.new()
	var bark := MeshKit.new()
	var leaves := MeshKit.new()
	var sides := 5 if far else 8
	var bark_col: Color = BARK.get(p.kind, Color(0.46, 0.33, 0.24))
	for i in p.limbs.size():
		var l: Array = p.limbs[i]
		if far and i > 0 and l[3] < 0.15:
			continue
		var from: Vector3 = l[0]
		var to: Vector3 = l[1]
		var bend: Vector3 = l[4]
		var axis := to - from - bend
		var len := axis.length()
		if len < 0.01:
			continue
		var xf := Transform3D(_basis_y(axis / len), from)
		var local_bend := xf.basis.inverse() * bend
		bark.tube(l[2], l[3], len, sides, xf, Color(0.62, 0.58, 0.55), local_bend, 1 if far else 3, Color(1.0, 0.97, 0.92))
	for s in p.strands:
		if far:
			break
		var at: Vector3 = s[0]
		bark.tube(0.09, 0.02, s[1], 4, Transform3D(Basis(Vector3.RIGHT, PI), at), Color(1.35, 1.6, 1.0))
	var crown_c: Vector3 = p.crown[0]
	var crown_r: Vector3 = p.crown[1]
	var sub := 1 if far else 2
	for l in p.lumps:
		var c: Vector3 = l[0]
		var r: Vector3 = l[1]
		var shade: float = l[2]
		var color_of := func(wp: Vector3, _centre: Vector3) -> Color:
			var rel := (wp - crown_c) / crown_r
			var h := clampf(rel.y * 0.5 + 0.5, 0.0, 1.0)
			var outward := smoothstep(0.3, 1.05, rel.length())
			return Color(h, outward, shade)
		leaves.lump(r, sub, Transform3D(Basis.IDENTITY, c), 0.13, int(shade * 1000.0), color_of, crown_c, 0.55)
	if not bark.is_empty():
		bark.commit(out, _bark_material(p.kind, bark_col))
	leaves.commit(out, _canopy_material(p.tone, p.crown_base))
	return out


static func _basis_y(dir: Vector3) -> Basis:
	var y := dir.normalized()
	var x := (Vector3.FORWARD if absf(y.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT).cross(y).normalized()
	return Basis(x, y, x.cross(y).normalized())


static func _canopy_material(tone: String, crown_base: float) -> ShaderMaterial:
	var key := "%s|%.1f" % [tone, crown_base]
	if _mats.has(key):
		return _mats[key]
	var t: Array = TONES[tone]
	var m := ShaderMaterial.new()
	m.shader = CANOPY
	m.set_shader_parameter("top_color", t[0])
	m.set_shader_parameter("mid_color", t[1])
	m.set_shader_parameter("under_color", t[2])
	m.set_shader_parameter("crown_base", crown_base)
	m.set_shader_parameter("wrap_amount", 0.55)
	m.set_shader_parameter("rim_amount", 0.35)
	m.set_shader_parameter("shadow_tint", Color(0.45, 0.62, 0.85))
	_mats[key] = m
	return m


static func _bark_material(kind: String, col: Color) -> ShaderMaterial:
	var key := "bark|" + kind
	if _mats.has(key):
		return _mats[key]
	var m := ShaderMaterial.new()
	m.shader = PAINTED
	m.set_shader_parameter("albedo", col)
	m.set_shader_parameter("paint", 0.22)
	m.set_shader_parameter("paint_scale", 2.2)
	m.set_shader_parameter("moss_amount", 0.55 if kind != "acacia" else 0.0)
	m.set_shader_parameter("roughness", 0.95)
	m.set_shader_parameter("specular_amount", 0.15)
	_mats[key] = m
	return m
