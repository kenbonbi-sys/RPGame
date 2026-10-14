class_name Village
extends Node3D
## Làng Lá Xanh: a ring of timber houses with thatched roofs around a campfire green, the
## village's Đá Truyền Tống, a wheat field and fences. Built from code until real models come.

const HOUSE_ANGLES := [40.0, 140.0, 175.0, 212.0, 250.0, 292.0, 328.0]

var _plaster := StandardMaterial3D.new()
var _timber := StandardMaterial3D.new()
var _thatch := StandardMaterial3D.new()
var _window := StandardMaterial3D.new()
var _fire_light: OmniLight3D


func _ready() -> void:
	_plaster.albedo_color = Color(0.80, 0.76, 0.66)
	_plaster.roughness = 0.95
	_timber.albedo_color = Color(0.30, 0.22, 0.15)
	_timber.roughness = 0.9
	_thatch.albedo_color = Color(0.50, 0.40, 0.25)
	_thatch.roughness = 1.0
	_window.albedo_color = Color(1.0, 0.75, 0.4)
	_window.emission_enabled = true
	_window.emission = Color(1.0, 0.65, 0.3)
	_window.emission_energy_multiplier = 1.5
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
		_house(m, yaw, rng.randf_range(6.5, 8.5), rng.randf_range(5.5, 7.0), rng.randf_range(3.6, 4.4))
	_campfire(c)
	_waystone(c + Vector2(9, -7))
	_field(c + Vector2(-34, -30))
	Props.place(self, "tree_oak", c + Vector2(-8, 10), 0.4, 16.0, false)
	Props.place(self, "tent_detailedOpen", c + Vector2(14, 16), 2.2, 4.0)
	Props.place(self, "log_stack", c + Vector2(-12, -6), 1.0, 3.0)
	Props.place(self, "log_stackLarge", c + Vector2(18, -14), 0.3, 3.0)
	Props.place(self, "sign", c + Vector2(40, -2), -PI * 0.5, 3.0)


func _house(m: Vector2, yaw: float, w: float, d: float, h: float) -> void:
	var root := Node3D.new()
	root.name = "House"
	root.position = Props.ground(m) + Vector3.DOWN * 0.3
	root.rotation.y = yaw
	add_child(root)
	_box(root, Vector3(w, h, d), Vector3(0, h * 0.5, 0), _plaster)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(root, Vector3(0.35, h + 0.1, 0.35), Vector3(sx * w * 0.5, h * 0.5, sz * d * 0.5), _timber)
	_box(root, Vector3(w + 0.1, 0.3, d + 0.1), Vector3(0, h, 0), _timber)
	var roof := MeshInstance3D.new()
	var pm := PrismMesh.new()
	pm.size = Vector3(w + 1.4, 2.8, d + 1.2)
	roof.mesh = pm
	roof.material_override = _thatch
	roof.position = Vector3(0, h + 1.4, 0)
	roof.rotation.y = PI * 0.5
	root.add_child(roof)
	_box(root, Vector3(1.3, 2.3, 0.15), Vector3(0, 1.15, d * 0.5 + 0.05), _timber)
	for sx in [-1.0, 1.0]:
		_box(root, Vector3(0.9, 0.8, 0.12), Vector3(sx * w * 0.28, h * 0.6, d * 0.5 + 0.05), _window)
	var body := StaticBody3D.new()
	body.collision_layer = 1
	var cs := CollisionShape3D.new()
	var bs := BoxShape3D.new()
	bs.size = Vector3(w + 0.3, h + 2.5, d + 0.3)
	cs.shape = bs
	cs.position = Vector3(0, (h + 2.5) * 0.5, 0)
	body.add_child(cs)
	root.add_child(body)


func _box(parent: Node3D, size: Vector3, pos: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mi.mesh = bm
	mi.material_override = mat
	mi.position = pos
	parent.add_child(mi)
	return mi


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


func _field(m: Vector2) -> void:
	for i in 6:
		for j in 8:
			Props.place(self, "crops_wheatStageB", m + Vector2(i * 2.6, j * 2.6), 0.0, 2.6, false, 0.1)
	for i in 9:
		Props.place(self, "fence_simple", m + Vector2(-2.0 + i * 2.2, -2.5), 0.0, 2.2, true)


func _process(_delta: float) -> void:
	if _fire_light:
		var t := Game.now()
		_fire_light.light_energy = 2.6 + sin(t * 9.0) * 0.3 + sin(t * 23.0) * 0.2
