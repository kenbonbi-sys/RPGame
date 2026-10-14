extends SceneTree
## Prints each model's size including the transforms inside the file.
func _init() -> void:
	for path in OS.get_cmdline_user_args():
		var root: Node3D = load(path).instantiate()
		var aabb := AABB()
		var first := true
		for m in root.find_children("*", "MeshInstance3D", true, false):
			var mi: MeshInstance3D = m
			var xf := Transform3D.IDENTITY
			var p: Node = mi
			while p and p != root:
				xf = (p as Node3D).transform * xf
				p = p.get_parent()
			var b := xf * mi.get_aabb()
			aabb = b if first else aabb.merge(b)
			first = false
		print(path.get_file(), " root scale=", root.scale, " size=", aabb.size.snapped(Vector3.ONE * 0.01), " pos=", aabb.position.snapped(Vector3.ONE * 0.01))
		root.free()
	quit()
