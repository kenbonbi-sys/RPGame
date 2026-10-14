class_name RockArt
extends RefCounted
## Rocks in the Dauntless style, built in code: a ball cut by a dozen or so random planes, so it
## has broad flat facets with softened edges, a flat top where moss and grass settle and a flat
## bottom that sits in the ground. Each facet gets its own warm or cool shade (painterly), cliff
## pieces add darker strata. Unit size (about 1 m radius) before scaling; near and far versions.
##
## Names are "<kind>_<seed>": boulder, slab, tall, cliff.

const PAINTED := preload("res://shaders/painted.gdshader")
const KINDS := {
	# radii, planes, cut depth range, top cut, bottom cut
	"boulder": [Vector3(1.0, 0.82, 0.92), 13, Vector2(0.72, 0.9), 0.72, 0.55],
	"slab": [Vector3(1.35, 0.5, 1.05), 11, Vector2(0.7, 0.88), 0.62, 0.5],
	"tall": [Vector3(0.7, 1.55, 0.72), 12, Vector2(0.74, 0.9), 0.82, 0.6],
	"cliff": [Vector3(1.0, 1.45, 0.85), 15, Vector2(0.66, 0.86), 0.78, 0.45],
}

static var _meshes := {}
static var _plans := {}
static var _hulls := {}
static var _mat: ShaderMaterial


static func mesh(rock_name: String, far := false) -> ArrayMesh:
	var key := rock_name + ("|far" if far else "")
	if _meshes.has(key):
		return _meshes[key]
	var m := _build(_plan(rock_name), 2 if far else 3)
	_meshes[key] = m
	return m


## Corner points of the rock's solid shape (for a convex collider): the cutting planes plus a
## ring of planes standing in for the ball, intersected exactly.
static func hull(rock_name: String) -> PackedVector3Array:
	if _hulls.has(rock_name):
		return _hulls[rock_name]
	var p := _plan(rock_name)
	var planes: Array[Plane] = []
	planes.append_array(p.planes)
	var ico: PackedVector3Array = MeshKit._icosphere(1)[0]
	for v in ico:
		planes.append(Plane(v, 1.0))
	var pts := Geometry3D.compute_convex_mesh_points(planes)
	var out := PackedVector3Array()
	for q in pts:
		var w: Vector3 = q * p.radii
		var dup := false
		for o in out:
			if o.distance_to(w) < 0.05:
				dup = true
				break
		if not dup:
			out.append(w)
	_hulls[rock_name] = out
	return out


static func material() -> ShaderMaterial:
	if _mat:
		return _mat
	_mat = ShaderMaterial.new()
	_mat.shader = PAINTED
	_mat.set_shader_parameter("albedo", Color(0.68, 0.63, 0.56))
	_mat.set_shader_parameter("paint", 0.14)
	_mat.set_shader_parameter("paint_scale", 2.2)
	_mat.set_shader_parameter("world_paint", 0.13)
	_mat.set_shader_parameter("world_paint_scale", 0.8)
	_mat.set_shader_parameter("moss_amount", 0.9)
	_mat.set_shader_parameter("moss", Color(0.42, 0.58, 0.22))
	_mat.set_shader_parameter("roughness", 0.92)
	_mat.set_shader_parameter("specular_amount", 0.2)
	return _mat


static func _plan(rock_name: String) -> Dictionary:
	if _plans.has(rock_name):
		return _plans[rock_name]
	var bits := rock_name.split("_")
	var kind := bits[0]
	var seed := int(bits[1]) if bits.size() > 1 else 0
	var spec: Array = KINDS.get(kind, KINDS.boulder)
	var radii: Vector3 = spec[0]
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(kind) * 131 + seed * 977 + 5
	radii *= Vector3(rng.randf_range(0.9, 1.1), rng.randf_range(0.9, 1.1), rng.randf_range(0.9, 1.1))
	# the cutting planes: spread round the ball, plus a top and a bottom
	var planes: Array[Plane] = []
	var count: int = spec[1]
	var golden := PI * (3.0 - sqrt(5.0))
	var depth: Vector2 = spec[2]
	for i in count:
		var y := 1.0 - (i + 0.5) / count * 2.0
		var r := sqrt(maxf(0.0, 1.0 - y * y))
		var a := golden * i + rng.randf_range(-0.5, 0.5)
		var n := Vector3(cos(a) * r, y * 0.85, sin(a) * r)
		n += Vector3(rng.randf_range(-0.25, 0.25), rng.randf_range(-0.2, 0.2), rng.randf_range(-0.25, 0.25))
		planes.append(Plane(n.normalized(), rng.randf_range(depth.x, depth.y)))
	planes.append(Plane((Vector3.UP + Vector3(rng.randf_range(-0.15, 0.15), 0, rng.randf_range(-0.15, 0.15))).normalized(), spec[3]))
	planes.append(Plane(Vector3.DOWN, spec[4]))
	var shades: Array[Color] = []
	for i in planes.size():
		var warm := rng.randf()
		var f := rng.randf_range(0.86, 1.1)
		shades.append(Color(f * lerpf(0.9, 1.06, warm), f * lerpf(0.94, 1.0, warm), f * lerpf(1.04, 0.9, warm)))
	var plan := {"radii": radii, "planes": planes, "shades": shades, "strata": kind == "cliff" or kind == "tall"}
	_plans[rock_name] = plan
	return plan


static func _build(plan: Dictionary, subdiv: int) -> ArrayMesh:
	var radii: Vector3 = plan.radii
	var planes: Array[Plane] = plan.planes
	var shades: Array[Color] = plan.shades
	var strata: bool = plan.strata
	var ico := MeshKit._icosphere(subdiv)
	var vs: PackedVector3Array = ico[0]
	var idx: PackedInt32Array = ico[1]
	var pos := PackedVector3Array()
	var nrm := PackedVector3Array()
	var col := PackedColorArray()
	for v in vs:
		var p := v
		var nsum := Vector3.ZERO
		var cut := -1
		var deepest := 0.0
		for pi in planes.size():
			var pl: Plane = planes[pi]
			var s := p.dot(pl.normal) - pl.d
			if s > -0.035:
				# inside the soft band: lean the normal toward this facet
				var w := clampf((s + 0.035) / 0.07, 0.0, 1.0)
				nsum += pl.normal * w
				if s > 0.0:
					p -= pl.normal * s
					if s > deepest:
						deepest = s
						cut = pi
		var n := nsum.normalized() if nsum.length() > 0.2 else v
		n = n.lerp(v, 0.12).normalized()
		var wp := p * radii
		pos.append(wp)
		nrm.append((n / radii).normalized())
		var c := shades[cut] if cut >= 0 else Color(0.96, 0.95, 0.94)
		if strata:
			var band := fposmod(wp.y * 2.4 + MeshKit._noise3(wp * 1.3) * 0.8, 1.0)
			var k := 0.86 if band < 0.18 else (1.06 if band > 0.86 else 1.0)
			c = Color(c.r * k, c.g * k, c.b * k)
		col.append(c)
	var kit := MeshKit.new()
	for i in range(0, idx.size(), 3):
		var a := idx[i]
		var b := idx[i + 1]
		var cc := idx[i + 2]
		kit.tri(pos[a], pos[b], pos[cc], nrm[a], nrm[b], nrm[cc], col[a], col[b], col[cc])
	var m := ArrayMesh.new()
	kit.commit(m, material())
	return m
