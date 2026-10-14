class_name MeshKit
extends RefCounted
## Builds chunky stylised geometry in code (the Dauntless look: bevelled blocks, puffy lumps,
## tapered limbs) into one surface: positions, normals and a per-vertex colour that the painted
## shaders read (tint, or height/occlusion/variation for canopies). Add shapes with a transform,
## then `commit` the surface to a mesh with its material.

var verts := PackedVector3Array()
var norms := PackedVector3Array()
var cols := PackedColorArray()


func is_empty() -> bool:
	return verts.is_empty()


## One triangle. Winding is fixed up so the face points the way the normals do (Godot draws
## clockwise faces as front faces).
func tri(a: Vector3, b: Vector3, c: Vector3, na: Vector3, nb: Vector3, nc: Vector3, ca: Color, cb: Color, cc: Color) -> void:
	if (b - a).cross(c - a).dot(na + nb + nc) > 0.0:
		var t := b
		b = c
		c = t
		var tn := nb
		nb = nc
		nc = tn
		var tc := cb
		cb = cc
		cc = tc
	verts.append(a)
	verts.append(b)
	verts.append(c)
	norms.append(na)
	norms.append(nb)
	norms.append(nc)
	cols.append(ca)
	cols.append(cb)
	cols.append(cc)


## A box with bevelled, softly shaded edges, centred on the origin of `xf`. `jitter` nudges every
## vertex a little so a row of them looks hand-made.
func rounded_box(size: Vector3, bevel: float, xf: Transform3D, color: Color, jitter := 0.0, rng: RandomNumberGenerator = null) -> void:
	var h := size * 0.5
	var b := minf(bevel, minf(h.x, minf(h.y, h.z)) * 0.95)
	var inner := h - Vector3.ONE * b
	var nb := xf.basis.inverse().transposed()
	var grid := [-1.0, -1.0, 1.0, 1.0]
	var off := [0.0, 1.0, -1.0, 0.0]
	var faces := [
		[Vector3(1, 0, 0), Vector3(0, 1, 0), Vector3(0, 0, 1)],
		[Vector3(-1, 0, 0), Vector3(0, 0, 1), Vector3(0, 1, 0)],
		[Vector3(0, 1, 0), Vector3(0, 0, 1), Vector3(1, 0, 0)],
		[Vector3(0, -1, 0), Vector3(1, 0, 0), Vector3(0, 0, 1)],
		[Vector3(0, 0, 1), Vector3(1, 0, 0), Vector3(0, 1, 0)],
		[Vector3(0, 0, -1), Vector3(0, 1, 0), Vector3(1, 0, 0)],
	]
	var cache := {}
	for f in faces:
		var n: Vector3 = f[0]
		var u: Vector3 = f[1]
		var v: Vector3 = f[2]
		var pts := []
		var nrm := []
		for j in 4:
			var row := []
			var nrow := []
			for i in 4:
				var p: Vector3 = n * h.dot(n.abs()) + u * (grid[i] * h.dot(u.abs()) + off[i] * b) + v * (grid[j] * h.dot(v.abs()) + off[j] * b)
				var key := p.snapped(Vector3.ONE * 0.0001)
				var q := p.clamp(-inner, inner)
				var d := p - q
				var out := q + d.normalized() * b if d.length() > 0.00001 else p
				var nn := d.normalized() if d.length() > 0.00001 else n
				if jitter > 0.0 and rng:
					if not cache.has(key):
						cache[key] = Vector3(rng.randf_range(-1, 1), rng.randf_range(-1, 1), rng.randf_range(-1, 1)) * jitter
					out += cache[key]
				row.append(xf * out)
				nrow.append((nb * nn).normalized())
			pts.append(row)
			nrm.append(nrow)
		for j in 3:
			for i in 3:
				_quad(pts[j][i], pts[j][i + 1], pts[j + 1][i + 1], pts[j + 1][i],
					nrm[j][i], nrm[j][i + 1], nrm[j + 1][i + 1], nrm[j + 1][i], color)


func _quad(a: Vector3, b: Vector3, c: Vector3, d: Vector3, na: Vector3, nb: Vector3, nc: Vector3, nd: Vector3, col: Color) -> void:
	tri(a, b, c, na, nb, nc, col, col, col)
	tri(a, c, d, na, nc, nd, col, col, col)


## A tapered tube along +Y from 0 to `height` (radius r0 at the bottom, r1 at the top), bent by
## `bend` (an offset reached at the top, eased in), with smooth sides and a top cap.
func tube(r0: float, r1: float, height: float, sides: int, xf: Transform3D, color: Color, bend := Vector3.ZERO, rings := 3, top_color := Color(-1, 0, 0)) -> void:
	var nb := xf.basis.inverse().transposed()
	var ring_pts := []
	var ring_nrm := []
	var slope := (r0 - r1) / maxf(0.001, height)
	for k in rings + 1:
		var t := float(k) / rings
		var c := Vector3(0, height * t, 0) + bend * t * t
		var r := lerpf(r0, r1, t)
		var pts := []
		var ns := []
		for s in sides:
			var a := TAU * s / sides
			var d := Vector3(cos(a), 0, sin(a))
			pts.append(xf * (c + d * r))
			ns.append((nb * (d + Vector3(0, slope, 0))).normalized())
		ring_pts.append(pts)
		ring_nrm.append(ns)
	var tc := color if top_color.r < 0.0 else top_color
	for k in rings:
		var ca := color.lerp(tc, float(k) / rings)
		var cb := color.lerp(tc, float(k + 1) / rings)
		for s in sides:
			var s2 := (s + 1) % sides
			tri(ring_pts[k][s], ring_pts[k][s2], ring_pts[k + 1][s2], ring_nrm[k][s], ring_nrm[k][s2], ring_nrm[k + 1][s2], ca, ca, cb)
			tri(ring_pts[k][s], ring_pts[k + 1][s2], ring_pts[k + 1][s], ring_nrm[k][s], ring_nrm[k + 1][s2], ring_nrm[k + 1][s], ca, cb, cb)
	var top: Array = ring_pts[rings]
	var centre := xf * (Vector3(0, height, 0) + bend)
	var up := (nb * Vector3.UP).normalized()
	for s in sides:
		tri(centre, top[s], top[(s + 1) % sides], up, up, up, tc, tc, tc)


## A puffy lump: an icosphere pushed about by noise. Its normals lean toward `crown` (a point the
## whole cluster shades around) by `crown_mix`, so many lumps read as one soft painted mass.
## `color_of(p_world)` gives each vertex its colour. `flat_bottom` < 1 squashes the lower half
## (a mushroom cap, a cushion of moss).
func lump(radius: Vector3, subdiv: int, xf: Transform3D, noise_amp: float, seed: int, color_of: Callable,
		crown := Vector3.INF, crown_mix := 0.0, flat_bottom := 1.0) -> void:
	var ico := _icosphere(subdiv)
	var vs: PackedVector3Array = ico[0]
	var idx: PackedInt32Array = ico[1]
	var nb := xf.basis.inverse().transposed()
	var world := PackedVector3Array()
	var nrm := PackedVector3Array()
	var colors := PackedColorArray()
	var centre := xf.origin
	for v in vs:
		var n := _noise3(v * 1.7 + Vector3(seed * 1.31, seed * 0.77, seed * 2.11))
		var r := radius
		if v.y < 0.0:
			r.y *= flat_bottom
		var p := v * r * (1.0 + (n - 0.5) * 2.0 * noise_amp)
		var wp := xf * p
		var nn := (nb * (v / r)).normalized()
		if crown != Vector3.INF and crown_mix > 0.0:
			nn = nn.lerp((wp - crown).normalized(), crown_mix).normalized()
		world.append(wp)
		nrm.append(nn)
		colors.append(color_of.call(wp, centre))
	for i in range(0, idx.size(), 3):
		var a := idx[i]
		var b := idx[i + 1]
		var c := idx[i + 2]
		tri(world[a], world[b], world[c], nrm[a], nrm[b], nrm[c], colors[a], colors[b], colors[c])


## An extruded triangle (a gable end): `a`, `b`, `c` in the XY plane, `depth` along Z.
func prism(a: Vector2, b: Vector2, c: Vector2, depth: float, xf: Transform3D, color: Color) -> void:
	var nb := xf.basis.inverse().transposed()
	var z := depth * 0.5
	var f := [Vector3(a.x, a.y, z), Vector3(b.x, b.y, z), Vector3(c.x, c.y, z)]
	var k := [Vector3(a.x, a.y, -z), Vector3(b.x, b.y, -z), Vector3(c.x, c.y, -z)]
	var nf := (nb * Vector3.BACK).normalized()
	var nk := (nb * Vector3.FORWARD).normalized()
	tri(xf * f[0], xf * f[1], xf * f[2], nf, nf, nf, color, color, color)
	tri(xf * k[0], xf * k[2], xf * k[1], nk, nk, nk, color, color, color)
	for i in 3:
		var j := (i + 1) % 3
		var e: Vector3 = f[j] - f[i]
		var n := (nb * Vector3(e.y, -e.x, 0).normalized()).normalized()
		var mid: Vector3 = (f[0] + f[1] + f[2]) / 3.0
		if Vector3(e.y, -e.x, 0).dot(f[i] - mid) < 0.0:
			n = -n
		_quad(xf * f[i], xf * f[j], xf * k[j], xf * k[i], n, n, n, n, color)


## Hands the surface to `mesh` with its material and starts afresh.
func commit(mesh: ArrayMesh, material: Material) -> void:
	if verts.is_empty():
		return
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_COLOR] = cols
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	mesh.surface_set_material(mesh.get_surface_count() - 1, material)
	verts = PackedVector3Array()
	norms = PackedVector3Array()
	cols = PackedColorArray()


# ------------------------------------------------------------------ helpers
static var _ico_cache := {}


static func _icosphere(subdiv: int) -> Array:
	if _ico_cache.has(subdiv):
		return _ico_cache[subdiv]
	var t := (1.0 + sqrt(5.0)) / 2.0
	var vs := PackedVector3Array([
		Vector3(-1, t, 0), Vector3(1, t, 0), Vector3(-1, -t, 0), Vector3(1, -t, 0),
		Vector3(0, -1, t), Vector3(0, 1, t), Vector3(0, -1, -t), Vector3(0, 1, -t),
		Vector3(t, 0, -1), Vector3(t, 0, 1), Vector3(-t, 0, -1), Vector3(-t, 0, 1)])
	for i in vs.size():
		vs[i] = vs[i].normalized()
	var idx := PackedInt32Array([0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2,
		10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1])
	for s in subdiv:
		var mids := {}
		var out := PackedInt32Array()
		for i in range(0, idx.size(), 3):
			var a := idx[i]
			var b := idx[i + 1]
			var c := idx[i + 2]
			var ab := _mid(vs, mids, a, b)
			var bc := _mid(vs, mids, b, c)
			var ca := _mid(vs, mids, c, a)
			out.append_array([a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca])
		idx = out
	_ico_cache[subdiv] = [vs, idx]
	return _ico_cache[subdiv]


static func _mid(vs: PackedVector3Array, mids: Dictionary, a: int, b: int) -> int:
	var key := Vector2i(mini(a, b), maxi(a, b))
	if mids.has(key):
		return mids[key]
	vs.append(((vs[a] + vs[b]) * 0.5).normalized())
	mids[key] = vs.size() - 1
	return vs.size() - 1


static func _hash3(p: Vector3) -> float:
	var q := Vector3(fposmod(p.x * 0.3183099 + 0.1, 1.0), fposmod(p.y * 0.3183099 + 0.2, 1.0), fposmod(p.z * 0.3183099 + 0.3, 1.0)) * 17.0
	return fposmod(q.x * q.y * q.z * (q.x + q.y + q.z), 1.0)


## Smooth value noise in 0..1.
static func _noise3(x: Vector3) -> float:
	var i := x.floor()
	var f := x - i
	f = f * f * (Vector3.ONE * 3.0 - f * 2.0)
	var a := lerpf(lerpf(_hash3(i), _hash3(i + Vector3(1, 0, 0)), f.x), lerpf(_hash3(i + Vector3(0, 1, 0)), _hash3(i + Vector3(1, 1, 0)), f.x), f.y)
	var b := lerpf(lerpf(_hash3(i + Vector3(0, 0, 1)), _hash3(i + Vector3(1, 0, 1)), f.x), lerpf(_hash3(i + Vector3(0, 1, 1)), _hash3(i + Vector3(1, 1, 1)), f.x), f.y)
	return lerpf(a, b, f.z)
