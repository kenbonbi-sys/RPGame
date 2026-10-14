extends SceneTree

func _init() -> void:
	for path in OS.get_cmdline_user_args():
		var ps: PackedScene = load(path)
		var root := ps.instantiate()
		var ap: AnimationPlayer = root.find_children("*", "AnimationPlayer", true, false).front() if root.find_children("*", "AnimationPlayer", true, false).size() > 0 else null
		print("== ", path)
		if ap:
			var names := ap.get_animation_list()
			print("  ", names.size(), " anims: ", ", ".join(names))
		var sk = root.find_children("*", "Skeleton3D", true, false)
		if sk.size() > 0:
			var s: Skeleton3D = sk[0]
			var bones := []
			for i in s.get_bone_count(): bones.append(s.get_bone_name(i))
			print("  bones: ", ", ".join(bones))
		var meshes = root.find_children("*", "MeshInstance3D", true, false)
		print("  meshes: ", ", ".join(meshes.map(func(m): return m.name)))
		root.free()
	quit()
