class_name Village
extends Node3D
## Làng Lá Xanh in the Dauntless style: a ring of timber-framed houses (HouseArt) with tiled roofs
## around a campfire green, the great oak, a market stall, barrels and crates, banners, the
## village's Đá Truyền Tống, a wheat field behind a fence.

const HOUSE_ANGLES := [40.0, 140.0, 175.0, 212.0, 250.0, 292.0, 328.0]
const BANNER := Color(0.20, 0.46, 0.36)
const BANNER_TRIM := Color(0.95, 0.76, 0.34)

var _fire_light: OmniLight3D


func _ready() -> void:
	var c := WorldMap.village_center()
	var rng := RandomNumberGenerator.new()
	rng.seed = 18
	for i in HOUSE_ANGLES.size():
		var a := deg_to_rad(HOUSE_ANGLES[i])
		var r := rng.randf_range(24.0, 31.0)
		var m := c + Vector2(cos(a), sin(a)) * r
		# face the green: the door looks back toward the centre
		var to_center := c - m
		var yaw := atan2(to_center.x, -to_center.y)
		_house(rng, m, yaw, rng.randf_range(6.5, 8.5), rng.randf_range(5.5, 6.8), 2 if i % 3 != 1 else 1)
	_campfire(c)
	_waystone(c + Vector2(9, -7))
	_field(rng, c + Vector2(-34, -30))
	_great_oak(c + Vector2(-8, 10))
	_stall(rng, c + Vector2(14, 16), Color(0.85, 0.28, 0.24), Color(0.98, 0.92, 0.8))
	_stall(rng, c + Vector2(-15, -12), Color(0.24, 0.5, 0.62), Color(0.98, 0.92, 0.8))
	for i in 4:
		var a := TAU * (i + 0.5) / 4.0
		_mesh(HouseArt.banner_mesh(BANNER, BANNER_TRIM), c + Vector2(cos(a), sin(a)) * 13.0, a, true, 0.12)
	_clutter(rng, c)
	Props.place(self, "log_stack", c + Vector2(-12, -6), 1.0, 3.0)
	Props.place(self, "sign", c + Vector2(40, -2), -PI * 0.5, 3.0)


func _house(rng: RandomNumberGenerator, m: Vector2, yaw: float, w: float, d: float, storeys: int) -> void:
	var built := HouseArt.house(rng, w, d, storeys)
	var root: Node3D = built.node
	root.position = Props.ground(m) + Vector3.DOWN * 0.15
	root.rotation.y = yaw
	add_child(root)
	var size: Vector3 = built.size
	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	var bs := BoxShape3D.new()
	bs.size = size
	cs.shape = bs
	cs.position = Vector3(0, size.y * 0.5, 0)
	body.add_child(cs)
	root.add_child(body)
	# the walls hide what stands behind the house (kept inside the walls and under the roof)
	var occ := OccluderInstance3D.new()
	var box := BoxOccluder3D.new()
	box.size = Vector3(size.x * 0.85, size.y * 0.55, size.z * 0.85)
	occ.occluder = box
	occ.position = Vector3(0, size.y * 0.3, 0)
	root.add_child(occ)


## Places a built mesh on the ground; `solid` gives it a small box (or cylinder) collider.
func _mesh(mesh: Mesh, m: Vector2, yaw := 0.0, solid := false, radius := 0.5) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = Props.ground(m)
	mi.rotation.y = yaw
	add_child(mi)
	if solid:
		var body := StaticBody3D.new()
		body.collision_layer = 1
		body.collision_mask = 0
		var cs := CollisionShape3D.new()
		var cyl := CylinderShape3D.new()
		cyl.radius = radius
		cyl.height = 2.0
		cs.shape = cyl
		cs.position = Vector3(0, 1.0, 0)
		body.add_child(cs)
		mi.add_child(body)
	return mi


func _great_oak(m: Vector2) -> void:
	var tree := TreeArt.instance("village_0")
	tree.position = Props.ground(m) + Vector3.DOWN * 0.2
	tree.rotation.y = 0.4
	add_child(tree)
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var cyl := CylinderShape3D.new()
	cyl.radius = TreeArt.trunk_radius("village_0")
	cyl.height = 8.0
	cs.shape = cyl
	cs.position = Vector3(0, 4.0, 0)
	body.add_child(cs)
	tree.add_child(body)


func _stall(rng: RandomNumberGenerator, m: Vector2, a: Color, b: Color) -> void:
	var stall := HouseArt.stall(rng, a, b)
	var to_center := WorldMap.village_center() - m
	stall.position = Props.ground(m)
	stall.rotation.y = atan2(to_center.x, -to_center.y)
	add_child(stall)
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var bs := BoxShape3D.new()
	bs.size = Vector3(3.6, 1.0, 1.0)
	cs.shape = bs
	cs.position = Vector3(0, 0.5, 0.8)
	body.add_child(cs)
	stall.add_child(body)


## Barrels and crates by the houses and stalls.
func _clutter(rng: RandomNumberGenerator, c: Vector2) -> void:
	var barrel := HouseArt.barrel_mesh(rng)
	var crate := HouseArt.crate_mesh(rng)
	var spots := [Vector2(16.5, 13.5), Vector2(17.3, 14.6), Vector2(11.5, 18.5), Vector2(-17.5, -10.0),
		Vector2(-12.6, -14.2), Vector2(-13.4, -15.1), Vector2(6, 20), Vector2(-20, 6)]
	for i in spots.size():
		var s: Vector2 = spots[i]
		var yaw := rng.randf() * TAU
		if i % 3 == 2:
			_mesh(crate, c + s, yaw, true, 0.55)
			if rng.randf() < 0.5:
				var top := _mesh(crate, c + s, yaw + 0.4)
				top.position.y += 0.9
				top.scale = Vector3.ONE * 0.8
		else:
			_mesh(barrel, c + s, yaw, true, 0.5)


func _campfire(m: Vector2) -> void:
	Props.place(self, "campfire_stones", m, 0.0, 3.0, false)
	Props.place(self, "campfire_logs", m, 0.6, 3.0, false)
	_fire_light = OmniLight3D.new()
	_fire_light.light_color = Color(1.0, 0.62, 0.3)
	_fire_light.light_energy = 3.0
	_fire_light.omni_range = 14.0
	_fire_light.shadow_enabled = true
	_fire_light.position = Props.ground(m) + Vector3.UP * 1.4
	add_child(_fire_light)
	var fire := GPUParticles3D.new()
	fire.amount = 40
	fire.lifetime = 0.9
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3.UP
	pm.spread = 12.0
	pm.initial_velocity_min = 1.2
	pm.initial_velocity_max = 2.4
	pm.gravity = Vector3(0, 1.5, 0)
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 0.5
	var grad := Gradient.new()
	grad.set_color(0, Color(1.0, 0.75, 0.3, 1.0))
	grad.set_color(1, Color(0.9, 0.2, 0.05, 0.0))
	var gt := GradientTexture1D.new()
	gt.gradient = grad
	pm.color_ramp = gt
	fire.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(0.9, 0.9)
	q.material = Fx._quad_material(true)
	fire.draw_pass_1 = q
	fire.position = Props.ground(m) + Vector3.UP * 0.5
	add_child(fire)


func _waystone(m: Vector2) -> void:
	Props.place(self, "statue_obelisk", m, 0.3, 4.0)
	var l := OmniLight3D.new()
	l.light_color = Color(0.5, 0.75, 1.0)
	l.light_energy = 1.6
	l.omni_range = 7.0
	l.position = Props.ground(m) + Vector3.UP * 2.5
	add_child(l)


func _field(rng: RandomNumberGenerator, m: Vector2) -> void:
	for i in 6:
		for j in 8:
			Props.place(self, "crops_wheatStageB", m + Vector2(i * 2.6, j * 2.6), 0.0, 2.6, false, 0.1)
	var fence := HouseArt.fence_mesh(rng, 19.8)
	_mesh(fence, m + Vector2(6.8, -2.5), 0.0)
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var bs := BoxShape3D.new()
	bs.size = Vector3(20.0, 1.2, 0.3)
	cs.shape = bs
	cs.position = Props.ground(m + Vector2(6.8, -2.5)) + Vector3.UP * 0.6
	body.add_child(cs)
	add_child(body)


func _process(_delta: float) -> void:
	if _fire_light:
		var t := Game.now()
		_fire_light.light_energy = 2.6 + sin(t * 9.0) * 0.3 + sin(t * 23.0) * 0.2
