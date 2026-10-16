extends RefCounted
## Drawing only what is seen: the land's occluder never rises above the land it covers, the
## foliage's near batches are small squares that cast the shadows (from their light version), far
## batches cast none, and the view distance scales every range. Run by test_runner.

var t: Node


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("render: the land's occluder stays under the land", occluder)
	await t._run("render: foliage batches, shadows and view distance", foliage)


func occluder() -> void:
	var land := Terrain.new()
	t.add_child(land)
	var oi := land.get_node("LandOccluder") as OccluderInstance3D
	check(oi != null and oi.occluder is ArrayOccluder3D, "the land has an occluder")
	if oi == null:
		land.queue_free()
		return
	var occ := oi.occluder as ArrayOccluder3D
	var verts := occ.vertices
	var d := land.data
	var k := Terrain.OCCLUDER_STEP
	var nu := (d.width - 1) / k
	var step := k * d.spacing
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var worst := -INF
	for i in 4000:
		var u := rng.randf_range(0.0, nu * step - 0.01)
		var v := rng.randf_range(0.0, ((d.depth - 1) / k) * step - 0.01)
		var fu := u / step
		var fv := v / step
		var cu := int(fu)
		var cv := int(fv)
		var tu := fu - cu
		var tv := fv - cv
		var row := nu + 1
		var a := verts[cv * row + cu].y
		var b := verts[cv * row + cu + 1].y
		var c := verts[(cv + 1) * row + cu].y
		var e := verts[(cv + 1) * row + cu + 1].y
		var h := a + tu * (b - a) + tv * (c - a) if tu + tv <= 1.0 else e + (1.0 - tu) * (c - e) + (1.0 - tv) * (b - e)
		worst = maxf(worst, h - minf(d.mesh_height_at(u, v), d.height_at(u, v)))
	check(worst < 0.0, "4000 points: the occluder is always below the drawn land (worst %.2f m)" % worst)
	check(verts.size() < 20000, "and light (%d corners)" % verts.size())
	check(ProjectSettings.get_setting("rendering/occlusion_culling/use_occlusion_culling", false), "occlusion culling is on")
	land.queue_free()
	await t._frames(1)
	Terrain.current = null


func foliage() -> void:
	var f := Foliage.new()
	t.add_child(f)
	var near_ok := true
	var far_shadowless := true
	var near_trees_shadowless := true
	var proxies := 0
	var near_trees := 0
	for n in f.get_children():
		var mmi := n as MultiMeshInstance3D
		if mmi == null:
			continue
		var kind: String = mmi.get_meta("kind")
		var far: bool = mmi.get_meta("far")
		if far:
			far_shadowless = far_shadowless and mmi.cast_shadow == GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			continue
		var box := mmi.multimesh.get_aabb()
		if kind != "cliff" and (box.size.x > Foliage.NEAR_CELL + 40.0 or box.size.z > Foliage.NEAR_CELL + 40.0):
			near_ok = false
		if kind == "tree":
			if mmi.cast_shadow == GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY:
				proxies += 1
			else:
				near_trees += 1
				near_trees_shadowless = near_trees_shadowless and mmi.cast_shadow == GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	check(near_ok, "near batches are small squares, so a turn of the camera drops the ones behind")
	check(far_shadowless, "far versions cast no shadow")
	check(near_trees > 0 and proxies == near_trees and near_trees_shadowless, "near trees cast their shadow from the light version (%d / %d)" % [proxies, near_trees])
	f.set_view_distance(0.5)
	var scaled := true
	for n in f.get_children():
		var mmi := n as MultiMeshInstance3D
		if mmi and mmi.get_meta("kind") == "tree":
			var want: float = Foliage.RANGE.tree * 0.5 if mmi.get_meta("far") else Foliage.NEAR.tree * 0.5
			scaled = scaled and is_equal_approx(mmi.visibility_range_end, want)
	check(scaled, "half the view distance draws trees half as far")
	f.queue_free()
	await t._frames(1)
