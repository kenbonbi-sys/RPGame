class_name Props
extends RefCounted
## Places single models from the Kenney Nature Kit (the scattered ones go through Foliage).

const KIT := "res://assets/third_party/kenney_nature/%s.glb"


## Puts a model on the ground at a map point. `solid` adds a convex collider (scaled points,
## so the physics never sees a scaled shape).
static func place(parent: Node, model: String, m: Vector2, yaw := 0.0, scale := 1.0, solid := true,
		sink := 0.0, group := "") -> Node3D:
	var node: Node3D = (load(KIT % model) as PackedScene).instantiate()
	var h := Terrain.height_at_world(Vector3(m.x, 0, -m.y))
	var xf := Transform3D(Basis(Vector3.UP, yaw).scaled(Vector3.ONE * scale), Vector3(m.x, h - sink, -m.y))
	node.transform = xf
	Palette.apply(node)
	parent.add_child(node)
	if solid:
		var body := StaticBody3D.new()
		body.collision_layer = 1
		body.collision_mask = 0
		body.transform = Transform3D(Basis(Vector3.UP, yaw), xf.origin)
		parent.add_child(body)
		for n in node.find_children("*", "MeshInstance3D", true, false):
			var mi := n as MeshInstance3D
			var local := Transform3D(Basis.IDENTITY.scaled(Vector3.ONE * scale), Vector3.ZERO) * _relative(mi, node)
			var pts := PackedVector3Array()
			var faces := mi.mesh.get_faces()
			var step := maxi(1, faces.size() / 200)
			for i in range(0, faces.size(), step):
				pts.append(local * faces[i])
			var shape := ConvexPolygonShape3D.new()
			shape.points = pts
			var cs := CollisionShape3D.new()
			cs.shape = shape
			body.add_child(cs)
		if group != "":
			body.add_to_group(group)
	if group != "":
		node.add_to_group(group)
	return node


static func _relative(n: Node3D, root: Node3D) -> Transform3D:
	var xf := Transform3D.IDENTITY
	var p: Node = n
	while p and p != root:
		xf = (p as Node3D).transform * xf
		p = p.get_parent()
	return xf


static func ground(m: Vector2) -> Vector3:
	return Vector3(m.x, Terrain.height_at_world(Vector3(m.x, 0, -m.y)), -m.y)
