class_name ArmourArt
extends RefCounted
## Armour on the hero (HeroModel.set_armour). Nothing is stacked on the bones: every piece is shaped
## from the body itself. The outfit's (or the head's) triangles that follow the piece's bones are
## pushed out along the surface into a shell that keeps their skinning, so the plate bends with the
## body; its open edges get a wall (the plate's thickness) and a raised band, the trim, lit in the
## element's colour. Each slot is a few such plates over each other, so the cloth shows between them:
##   head   a helm over the crown and the back of the head (a hood for Radiant and Umbral sets)
##   torso  a breastplate and, over it, a mantle on the shoulders
##   arms   vambraces on the forearms and a pauldron on each shoulder (the outfit's, mirrored)
##   legs   tassets on the thighs (the under-layer: leather, padding), greaves on the knees and shins
## Krolachi is cloth: the outfit as it is (nothing is added). Recruit is boiled leather with bronze
## trims; every elemental set is metal in its element's tone with a glowing trim.

## Per slot: the plates. mesh: the outfit's mesh (by name suffix) or "Head"; bones: the bones its
## triangles must follow; lift: how far it stands off the body; tone: "plate" or "under";
## above: only above this height (rest pose); cut "cap": only the crown and the back of the head.
const PARTS := {
	"head": [{"mesh": "Head", "bones": ["Head", "neck_01"], "lift": 0.02, "cut": "cap", "tone": "plate"}],
	"hood": [{"mesh": "Head_Hood", "bones": ["Head"], "lift": 0.012, "tone": "plate"}],
	"torso": [{"mesh": "Body", "bones": ["spine_03", "spine_02", "clavicle_l", "clavicle_r"], "lift": 0.018, "tone": "plate"},
		{"mesh": "Body", "bones": ["clavicle_l", "clavicle_r"], "lift": 0.03, "tone": "plate"}],
	"arms": [{"mesh": "Arms", "bones": ["lowerarm_l", "lowerarm_r"], "lift": 0.022, "tone": "plate"},
		{"mesh": "Acc_Pauldron", "bones": ["upperarm_l"], "lift": 0.014, "mirror": true, "tone": "plate"}],
	"legs": [{"mesh": "Legs", "bones": ["thigh_l", "thigh_r"], "lift": 0.01, "tone": "under"},
		{"mesh": "Legs", "bones": ["calf_l", "calf_r"], "lift": 0.02, "tone": "plate"},
		{"mesh": "Feet_Boots", "bones": ["calf_l", "calf_r"], "lift": 0.012, "above": 0.24, "tone": "plate"}],
}
## Element -> plate colour, metallic, roughness; the under-layer is leather in the plate's tone.
const METAL := {
	"neutral": [Color(0.44, 0.29, 0.18), 0.05, 0.72],
	"blaze": [Color(0.34, 0.25, 0.22), 0.6, 0.4],
	"frost": [Color(0.70, 0.78, 0.86), 0.65, 0.32],
	"shock": [Color(0.30, 0.30, 0.40), 0.65, 0.34],
	"terra": [Color(0.44, 0.46, 0.30), 0.55, 0.45],
	"radiant": [Color(0.90, 0.80, 0.56), 0.7, 0.3],
	"umbral": [Color(0.17, 0.13, 0.21), 0.6, 0.36],
}
const LEATHER := Color(0.33, 0.22, 0.15)
const HOODED := ["radiant", "umbral"]
const TRIM_W := 0.014
const TRIM_UP := 0.004

static var _plates := {}
static var _mats := {}


## Whether a set is drawn over the outfit (Krolachi is the outfit's own clothes).
static func drawn(set_id: String) -> bool:
	return Armour.SETS.has(set_id) and set_id != "krolachi"


## Builds the piece of `set_id` for `slot` and returns the new mesh instances (children of the
## skeleton). `sources`: the meshes the plates are shaped from, by name ("Body", "Head"...).
static func dress(skeleton: Skeleton3D, sources: Dictionary, slot: String, set_id: String) -> Array[MeshInstance3D]:
	var out: Array[MeshInstance3D] = []
	if not drawn(set_id) or not Armour.has_piece(set_id, slot):
		return out
	var e := String(Armour.set_of(set_id).element)
	var parts: Array = PARTS.get("hood" if slot == "head" and e in HOODED else slot, [])
	for part in parts:
		var src: MeshInstance3D = sources.get(String(part.mesh))
		if src == null:
			continue
		var meshes := plate(src.mesh, src.skin, part)
		for i in meshes.size():
			if meshes[i].get_surface_count() == 0:
				continue  # a closed piece (the pauldron) has no rim to trim
			var mi := MeshInstance3D.new()
			mi.name = "Armour_%s_%d" % [slot, out.size()]
			mi.mesh = meshes[i]
			mi.skin = src.skin
			mi.material_override = material("%s:%s" % [part.tone, e]) if i % 2 == 0 else material("trim:" + e)
			skeleton.add_child(mi)
			mi.skeleton = mi.get_path_to(skeleton)
			out.append(mi)
	return out


## Shapes every plate once (they are cached and shared by every hero and set), so putting armour
## on later does not stall a frame.
static func warm(sources: Dictionary) -> void:
	for slot in PARTS:
		for part in PARTS[slot]:
			var src: MeshInstance3D = sources.get(String(part.mesh))
			if src:
				plate(src.mesh, src.skin, part)


## "plate:<element>", "under:<element>" or "trim:<element>".
static func material(kind: String) -> Material:
	if _mats.has(kind):
		return _mats[kind]
	var e := kind.get_slice(":", 1)
	var m := StandardMaterial3D.new()
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.rim_enabled = true
	m.rim = 0.4
	m.rim_tint = 0.5
	if kind.begins_with("plate") or kind.begins_with("under"):
		var spec: Array = METAL.get(e, METAL.neutral)
		var under := kind.begins_with("under")
		m.albedo_color = (spec[0] as Color).lerp(LEATHER, 0.6) if under else spec[0]
		m.metallic = float(spec[1]) * (0.15 if under else 1.0)
		m.roughness = 0.78 if under else float(spec[2])
	elif e == "neutral":
		m.albedo_color = Color(0.78, 0.56, 0.3)
		m.metallic = 0.8
		m.roughness = 0.35
	else:
		var col := Elements.color(e)
		m.albedo_color = col
		m.emission_enabled = true
		m.emission = col
		m.emission_energy_multiplier = 1.8
		m.roughness = 0.3
	_mats[kind] = m
	return m


## The plate (surface 0 of the first mesh) and its trim (the second mesh) for one part; mirrored
## parts add the same two for the other side. Cached per source mesh and part.
static func plate(mesh: Mesh, skin: Skin, part: Dictionary) -> Array[ArrayMesh]:
	var key := "%d:%s" % [mesh.get_instance_id(), str(part)]
	if _plates.has(key):
		return _plates[key]
	var out: Array[ArrayMesh] = []
	var shell := _shell(mesh, skin, part, false)
	if not shell.is_empty():
		out.append_array(shell)
		if part.get("mirror", false):
			out.append_array(_shell(mesh, skin, part, true))
	_plates[key] = out
	return out


## [plate, trim] for the triangles of `mesh` (its cloth surfaces, not bare skin) whose corners
## follow `part.bones`, or [] when none do.
static func _shell(mesh: Mesh, skin: Skin, part: Dictionary, mirror: bool) -> Array[ArrayMesh]:
	var want := {}
	var swap := {}
	for i in skin.get_bind_count():
		var bn := String(skin.get_bind_name(i))
		if bn in part.bones:
			want[i] = true
	if mirror:
		var by_name := {}
		for i in skin.get_bind_count():
			by_name[String(skin.get_bind_name(i))] = i
		for i in skin.get_bind_count():
			var bn := String(skin.get_bind_name(i))
			var other := bn.trim_suffix("_l") + "_r" if bn.ends_with("_l") else (bn.trim_suffix("_r") + "_l" if bn.ends_with("_r") else bn)
			swap[i] = by_name.get(other, i)
	var pos := PackedVector3Array()
	var nor := PackedVector3Array()
	var bon := PackedInt32Array()
	var wei := PackedFloat32Array()
	var idx := PackedInt32Array()
	var per := 4
	for s in mesh.get_surface_count():
		var m := mesh.surface_get_material(s)
		if m and m.resource_name.begins_with("MI_Regular"):
			continue
		var arrays := mesh.surface_get_arrays(s)
		var v: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var n: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var b: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
		var w: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
		per = b.size() / maxi(1, v.size())
		var ix: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		var keep := PackedByteArray()
		keep.resize(v.size())
		var lo := Vector3.INF
		var hi := -Vector3.INF
		for i in v.size():
			var f := 0.0
			for j in per:
				if want.has(b[i * per + j]):
					f += w[i * per + j]
			keep[i] = 1 if f >= 0.5 and v[i].y >= float(part.get("above", -INF)) else 0
			if keep[i] == 1:
				lo = lo.min(v[i])
				hi = hi.max(v[i])
		# a cap: just above the brows at the front, sloping down to the nape at the back; the
		# triangles across the line are kept and their lower corners pulled up onto it, so the
		# helm's rim is a clean line
		var cap: bool = part.get("cut", "") == "cap"
		var brow := lo.y + (hi.y - lo.y) * 0.72
		var front := hi.z - 0.015
		var remap := {}
		for t in range(0, ix.size(), 3):
			if keep[ix[t]] == 0 or keep[ix[t + 1]] == 0 or keep[ix[t + 2]] == 0:
				continue
			if cap and maxf(v[ix[t]].y - _cap_y(brow, front, v[ix[t]].z), maxf(v[ix[t + 1]].y - _cap_y(brow, front, v[ix[t + 1]].z),
					v[ix[t + 2]].y - _cap_y(brow, front, v[ix[t + 2]].z))) < 0.0:
				continue
			var tri := [ix[t], ix[t + 1], ix[t + 2]]
			if mirror:
				tri = [ix[t], ix[t + 2], ix[t + 1]]
			for k in tri:
				if not remap.has(k):
					remap[k] = pos.size()
					var p := v[k]
					var nn := n[k]
					if cap:
						p.y = maxf(p.y, _cap_y(brow, front, p.z))
					if mirror:
						p.x = -p.x
						nn.x = -nn.x
					pos.append(p)
					nor.append(nn)
					for j in per:
						var bi := b[k * per + j]
						bon.append(swap.get(bi, bi) if mirror else bi)
						wei.append(w[k * per + j])
				idx.append(remap[k])
	if idx.is_empty():
		return []
	# weld by position: one outward direction per point, so the seams of the cloth stay closed
	var group := PackedInt32Array()
	group.resize(pos.size())
	var at := {}
	var gsum: Array[Vector3] = []
	for i in pos.size():
		var k := pos[i].snapped(Vector3.ONE * 0.001)
		if not at.has(k):
			at[k] = gsum.size()
			gsum.append(Vector3.ZERO)
		group[i] = at[k]
	for t in range(0, idx.size(), 3):
		var a := pos[idx[t]]
		var fn := (pos[idx[t + 1]] - a).cross(pos[idx[t + 2]] - a)
		for k in 3:
			gsum[group[idx[t + k]]] += fn
	idx = _big_islands(idx, group, pos)
	var gdir: Array[Vector3] = []
	for g in gsum:
		gdir.append(g.normalized() if g.length() > 1e-9 else Vector3.UP)
	# the face normals' winding may point in or out: follow the cloth's own normals
	var agree := 0.0
	for i in pos.size():
		agree += gdir[group[i]].dot(nor[i])
	var sign := 1.0 if agree >= 0.0 else -1.0
	var lift := float(part.lift)
	var plate_pos := PackedVector3Array()
	plate_pos.resize(pos.size())
	for i in pos.size():
		plate_pos[i] = pos[i] + gdir[group[i]] * sign * lift
	# open edges: an edge of the welded points used by one triangle only
	var edges := {}
	for t in range(0, idx.size(), 3):
		for k in 3:
			var i0 := idx[t + k]
			var i1 := idx[t + (k + 1) % 3]
			var ga := group[i0]
			var gb := group[i1]
			var ek := Vector2i(mini(ga, gb), maxi(ga, gb))
			if edges.has(ek):
				edges[ek].count += 1
			else:
				edges[ek] = {"count": 1, "a": i0, "b": i1, "c": idx[t + (k + 2) % 3]}
	# the rims follow the cloth's triangles and zigzag: smooth each rim along its loop (every
	# point a little toward its two neighbours on the rim), then give every rim point one inward
	# direction, the average of its two rim edges'
	var rim := []
	var next := {}
	for ek in edges:
		if edges[ek].count == 1:
			rim.append(edges[ek])
			next[ek.x] = (next.get(ek.x, []) as Array) + [ek.y]
			next[ek.y] = (next.get(ek.y, []) as Array) + [ek.x]
	var gpos := {}
	for i in pos.size():
		if next.has(group[i]):
			gpos[group[i]] = plate_pos[i]
	for pass_i in 5:
		var moved := {}
		for g in gpos:
			var nb: Array = next[g]
			moved[g] = gpos[g] * 0.5 + (gpos[nb[0]] + gpos[nb[1]]) * 0.25 if nb.size() == 2 else gpos[g]
		gpos = moved
	for i in pos.size():
		if gpos.has(group[i]):
			var delta: Vector3 = gpos[group[i]] - plate_pos[i]
			plate_pos[i] += delta
			pos[i] += delta
	var gin := {}
	for e in rim:
		var a := plate_pos[e.a]
		var b := plate_pos[e.b]
		var along := (b - a).normalized()
		var to_c := plate_pos[e.c] - (a + b) * 0.5
		var inward := (to_c - along * to_c.dot(along)).normalized()
		for g in [group[e.a], group[e.b]]:
			gin[g] = (gin.get(g, Vector3.ZERO) as Vector3) + inward
	var wall := _Builder.new(per)
	var trim := _Builder.new(per)
	# holes in the cloth (where its straps and buckles sat) are closed over rather than trimmed:
	# a rim loop much shorter than the longest one is a hole
	var holes := _small_loops(next, gpos, 0.25)
	var rep := {}
	for i in pos.size():
		rep[group[i]] = i
	for loop in holes:
		var c := Vector3.ZERO
		var n := Vector3.ZERO
		for g in loop:
			c += gpos[g]
			n += gdir[g] * sign
		c /= loop.size()
		n = n.normalized()
		for k in loop.size():
			var ga: int = loop[k]
			var gb: int = loop[(k + 1) % loop.size()]
			wall.tri([gpos[ga], gpos[gb], c], n, [rep[ga], rep[gb], rep[ga]], bon, wei)
	var hole_edges := {}
	for loop in holes:
		for k in loop.size():
			var ga: int = loop[k]
			var gb: int = loop[(k + 1) % loop.size()]
			hole_edges[Vector2i(mini(ga, gb), maxi(ga, gb))] = true
	for e in rim:
		var ia: int = e.a
		if hole_edges.has(Vector2i(mini(group[ia], group[e.b]), maxi(group[ia], group[e.b]))):
			continue
		var ib: int = e.b
		var na: Vector3 = gdir[group[ia]] * sign
		var nb: Vector3 = gdir[group[ib]] * sign
		var pa := plate_pos[ia]
		var pb := plate_pos[ib]
		var in_a: Vector3 = (gin[group[ia]] as Vector3).normalized()
		var in_b: Vector3 = (gin[group[ib]] as Vector3).normalized()
		var out_n := -(in_a + in_b).normalized()
		# the plate's thickness, from just above the cloth to the plate
		var ua := pos[ia] + na * lift * 0.25
		var ub := pos[ib] + nb * lift * 0.25
		wall.quad([ua, ub, pb, pa], out_n, [ia, ib, ib, ia], bon, wei)
		# the trim: a band over the edge, a little proud of the plate
		var ta := pa + na * TRIM_UP
		var tb := pb + nb * TRIM_UP
		var ra := pa + na * TRIM_UP + in_a * TRIM_W
		var rb := pb + nb * TRIM_UP + in_b * TRIM_W
		trim.quad([ta, tb, rb, ra], (na + nb).normalized(), [ia, ib, ib, ia], bon, wei)
		# and its outer lip, so the band reads from the side
		trim.quad([ta - na * (TRIM_UP + lift * 0.25), tb - nb * (TRIM_UP + lift * 0.25), tb, ta], out_n, [ia, ib, ib, ia], bon, wei)
	var plate_mesh := ArrayMesh.new()
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = plate_pos
	arrays[Mesh.ARRAY_NORMAL] = nor
	arrays[Mesh.ARRAY_BONES] = bon
	arrays[Mesh.ARRAY_WEIGHTS] = wei
	arrays[Mesh.ARRAY_INDEX] = idx
	var flags := Mesh.ARRAY_FLAG_USE_8_BONE_WEIGHTS if per == 8 else 0
	plate_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays, [], {}, flags)
	wall.commit(plate_mesh, flags)
	var trim_mesh := ArrayMesh.new()
	trim.commit(trim_mesh, flags)
	return [plate_mesh, trim_mesh]


## The rim loops (lists of welded points) whose length is under `share` of the longest one's.
static func _small_loops(next: Dictionary, gpos: Dictionary, share: float) -> Array:
	var loops := []
	var seen := {}
	for start in next:
		if seen.has(start) or (next[start] as Array).size() != 2:
			continue
		var loop := [start]
		seen[start] = true
		var prev: int = start
		var cur: int = next[start][0]
		var closed := false
		while true:
			if cur == start:
				closed = true
				break
			if seen.has(cur) or (next[cur] as Array).size() != 2:
				break
			seen[cur] = true
			loop.append(cur)
			var nb: Array = next[cur]
			var nxt: int = nb[0] if nb[0] != prev else nb[1]
			prev = cur
			cur = nxt
		if closed and loop.size() >= 3:
			loops.append(loop)
	var lengths := []
	var longest := 0.0
	for loop in loops:
		var l := 0.0
		for k in loop.size():
			l += (gpos[loop[k]] as Vector3).distance_to(gpos[loop[(k + 1) % loop.size()]])
		lengths.append(l)
		longest = maxf(longest, l)
	var out := []
	for k in loops.size():
		if lengths[k] < longest * share:
			out.append(loops[k])
	return out


static func _cap_y(brow: float, front: float, z: float) -> float:
	return brow - (front - z) * 0.6


## Keeps the big connected pieces of cloth (the tunic, a sleeve, a trouser leg) and drops the
## small ones riding on them (straps, buckles, laces), which would make lumps and stray trims.
static func _big_islands(idx: PackedInt32Array, group: PackedInt32Array, pos: PackedVector3Array) -> PackedInt32Array:
	var parent := {}
	var root_of := func(x: int) -> int:
		var r := x
		while parent.get(r, r) != r:
			r = parent[r]
		var c := x
		while parent.get(c, c) != r:
			var nx: int = parent[c]
			parent[c] = r
			c = nx
		return r
	for t in range(0, idx.size(), 3):
		var a: int = root_of.call(group[idx[t]])
		for k in [1, 2]:
			var b: int = root_of.call(group[idx[t + k]])
			if a != b:
				parent[b] = a
	var area := {}
	var biggest := 0.0
	for t in range(0, idx.size(), 3):
		var r: int = root_of.call(group[idx[t]])
		var a := (pos[idx[t + 1]] - pos[idx[t]]).cross(pos[idx[t + 2]] - pos[idx[t]]).length() * 0.5
		area[r] = float(area.get(r, 0.0)) + a
		biggest = maxf(biggest, area[r])
	var out := PackedInt32Array()
	for t in range(0, idx.size(), 3):
		if float(area[root_of.call(group[idx[t]])]) >= biggest * 0.35:
			out.append_array([idx[t], idx[t + 1], idx[t + 2]])
	return out


## Quads that copy the skinning of the plate's corners they stand on.
class _Builder:
	var per := 4
	var pos := PackedVector3Array()
	var nor := PackedVector3Array()
	var bon := PackedInt32Array()
	var wei := PackedFloat32Array()

	func _init(bones_per_vertex: int) -> void:
		per = bones_per_vertex

	func quad(p: Array, n: Vector3, from: Array, bones: PackedInt32Array, weights: PackedFloat32Array) -> void:
		# two triangles, wound so they face `n` (Godot's front faces are clockwise)
		for tri in [[0, 1, 2], [0, 2, 3]]:
			var a: Vector3 = p[tri[0]]
			var b: Vector3 = p[tri[1]]
			var c: Vector3 = p[tri[2]]
			var order: Array = tri if (b - a).cross(c - a).dot(n) < 0.0 else [tri[0], tri[2], tri[1]]
			for k in order:
				pos.append(p[k])
				nor.append(n)
				var src: int = from[k]
				for j in per:
					bon.append(bones[src * per + j])
					wei.append(weights[src * per + j])

	func tri(p: Array, n: Vector3, from: Array, bones: PackedInt32Array, weights: PackedFloat32Array) -> void:
		var a: Vector3 = p[0]
		var b: Vector3 = p[1]
		var c: Vector3 = p[2]
		var order := [0, 1, 2] if (b - a).cross(c - a).dot(n) < 0.0 else [0, 2, 1]
		for k in order:
			pos.append(p[k])
			nor.append(n)
			var src: int = from[k]
			for j in per:
				bon.append(bones[src * per + j])
				wei.append(weights[src * per + j])

	func commit(mesh: ArrayMesh, flags: int) -> void:
		if pos.is_empty():
			return
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = pos
		arrays[Mesh.ARRAY_NORMAL] = nor
		arrays[Mesh.ARRAY_BONES] = bon
		arrays[Mesh.ARRAY_WEIGHTS] = wei
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays, [], {}, flags)
