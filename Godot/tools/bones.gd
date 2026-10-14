extends SceneTree
## Prints where a model's main bones sit at rest (global), to fit gear and check facing.
##   Godot_console.exe --headless --path . -s tools/bones.gd -- res://path/model.gltf
func _init() -> void:
	for path in OS.get_cmdline_user_args():
		var root: Node = load(path).instantiate()
		var sk: Skeleton3D = root.find_children("*", "Skeleton3D", true, false)[0]
		var xf := Transform3D.IDENTITY
		var p: Node = sk
		while p and p != root:
			xf = (p as Node3D).transform * xf
			p = p.get_parent()
		print("== ", path.get_file(), "  skeleton path: ", root.get_path_to(sk), "  skeleton xf: ", xf)
		for b in ["root", "pelvis", "spine_03", "neck_01", "Head", "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "hand_r", "thigh_l", "calf_l", "foot_l", "ball_l"]:
			var i := sk.find_bone(b)
			if i >= 0:
				var g := xf * sk.get_bone_global_rest(i)
				print("  %-12s pos %s  y-axis %s" % [b, str(g.origin.snapped(Vector3.ONE * 0.01)), str(g.basis.y.normalized().snapped(Vector3.ONE * 0.01))])
		var ap = root.find_children("*", "AnimationPlayer", true, false)
		print("  anim players: ", ap.size(), "  children: ", root.get_children().map(func(c): return c.name))
		root.free()
	quit()
