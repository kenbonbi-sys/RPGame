class_name NpcGear
extends RefCounted
## The things that make each townsperson themselves, built in code and pinned to their bones:
## Granny Strega's huge witch hat (feather, and a little frog riding the brim) and her eye-topped
## staff, Wils's cap, the Middleman's faceless visor and the glowing cell in his palm, Dr.
## Priyani's notebook, Janek Zai's eyepiece, Gaius's rolled map. Places are in the model's T-pose
## space (metres, front +Z).

const PAINTED := preload("res://shaders/painted.gdshader")


static func _paint() -> ShaderMaterial:
	return HouseArt.material("trim")


static func _glow(c: Color, energy := 3.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.emission_enabled = true
	m.emission = c
	m.emission_energy_multiplier = energy
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m


static func _color(c: Color) -> Callable:
	return func(_p: Vector3, _q: Vector3) -> Color: return c


static func add(m: HeroModel, what: String) -> void:
	var head := m.bone_rest("Head")
	match what:
		"witch_hat":
			var k := MeshKit.new()
			var orange := Color(0.86, 0.46, 0.14)
			var purple := Color(0.42, 0.18, 0.52)
			k.tube(0.42, 0.4, 0.04, 18, Transform3D(Basis.IDENTITY, Vector3(0, 0, 0)), orange, Vector3.ZERO, 1)
			k.tube(0.17, 0.02, 0.5, 12, Transform3D(Basis.IDENTITY, Vector3(0, 0.03, 0)), orange, Vector3(0.0, 0.0, -0.16), 5)
			k.tube(0.175, 0.17, 0.07, 12, Transform3D(Basis.IDENTITY, Vector3(0, 0.03, 0)), purple, Vector3.ZERO, 1)
			k.rounded_box(Vector3(0.03, 0.34, 0.012), 0.005, Transform3D(Basis(Vector3.BACK, -0.6), Vector3(0.13, 0.2, 0.05)), Color(0.95, 0.93, 0.86))
			# the frog on the brim
			k.lump(Vector3(0.055, 0.04, 0.06), 1, Transform3D(Basis.IDENTITY, Vector3(-0.26, 0.05, 0.12)), 0.05, 3, _color(Color(0.36, 0.7, 0.24)))
			k.lump(Vector3.ONE * 0.016, 1, Transform3D(Basis.IDENTITY, Vector3(-0.27, 0.085, 0.165)), 0.0, 4, _color(Color(0.95, 0.85, 0.3)))
			k.lump(Vector3.ONE * 0.016, 1, Transform3D(Basis.IDENTITY, Vector3(-0.235, 0.085, 0.16)), 0.0, 5, _color(Color(0.95, 0.85, 0.3)))
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			m.attach("Head", mesh, null, head + Vector3(0, 0.19, -0.02), Vector3.ONE * 0.85, Vector3(-0.12, 0, 0.05))
		"staff":
			var k := MeshKit.new()
			k.tube(0.025, 0.02, 1.6, 7, Transform3D(Basis.IDENTITY, Vector3(0, -0.8, 0)), Color(0.36, 0.24, 0.14), Vector3(0.05, 0, 0.02), 6)
			k.lump(Vector3(0.07, 0.07, 0.07), 1, Transform3D(Basis.IDENTITY, Vector3(0.05, 0.84, 0.02)), 0.15, 7, _color(Color(0.36, 0.24, 0.14)))
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			var hand := m.bone_rest("hand_r")
			m.attach("hand_r", mesh, null, hand + Vector3(-0.06, 0, 0.03))
			var eye := SphereMesh.new()
			eye.radius = 0.045
			eye.height = 0.09
			m.attach("hand_r", eye, _glow(Color(0.55, 1.0, 0.6), 2.5), hand + Vector3(-0.01, 0.86, 0.09))
		"cap":
			var k := MeshKit.new()
			var rust := Color(0.55, 0.26, 0.14)
			k.lump(Vector3(0.12, 0.07, 0.13), 2, Transform3D(Basis.IDENTITY, Vector3(0, 0, 0)), 0.03, 11, _color(rust), Vector3.INF, 0.0, 0.2)
			k.rounded_box(Vector3(0.18, 0.015, 0.09), 0.006, Transform3D(Basis(Vector3.RIGHT, 0.15), Vector3(0, -0.01, 0.13)), rust * 0.8)
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			m.attach("Head", mesh, null, head + Vector3(0, 0.17, 0.0))
		"visor":
			var k := MeshKit.new()
			k.rounded_box(Vector3(0.2, 0.13, 0.05), 0.02, Transform3D(Basis.IDENTITY, Vector3.ZERO), Color(0.22, 0.25, 0.16))
			k.rounded_box(Vector3(0.22, 0.03, 0.06), 0.01, Transform3D(Basis.IDENTITY, Vector3(0, 0.07, 0)), Color(0.36, 0.42, 0.5))
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			m.attach("Head", mesh, null, head + Vector3(0, 0.08, 0.11))
		"orb":
			var orb := SphereMesh.new()
			orb.radius = 0.07
			orb.height = 0.14
			var hand := m.bone_rest("hand_l")
			m.attach("hand_l", orb, _glow(Color(0.95, 0.2, 0.85), 4.0), hand + Vector3(0.09, -0.07, 0.05))
		"book":
			var k := MeshKit.new()
			k.rounded_box(Vector3(0.16, 0.035, 0.22), 0.01, Transform3D(Basis.IDENTITY, Vector3.ZERO), Color(0.45, 0.22, 0.4))
			k.rounded_box(Vector3(0.15, 0.03, 0.21), 0.004, Transform3D(Basis.IDENTITY, Vector3(0.006, 0, 0)), Color(0.95, 0.92, 0.82))
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			var hand := m.bone_rest("hand_l")
			m.attach("hand_l", mesh, null, hand + Vector3(0.08, -0.05, 0.04), Vector3.ONE, Vector3(0, 0, 0.3))
		"eyepiece":
			var lens := CylinderMesh.new()
			lens.top_radius = 0.022
			lens.bottom_radius = 0.026
			lens.height = 0.03
			m.attach("Head", lens, _glow(Color(0.3, 0.85, 1.0), 2.0), head + Vector3(-0.034, 0.075, 0.105), Vector3.ONE, Vector3(PI * 0.5, 0, 0))
		"scroll":
			var k := MeshKit.new()
			k.tube(0.035, 0.035, 0.36, 8, Transform3D(Basis(Vector3.BACK, PI * 0.5), Vector3(0.18, 0, 0)), Color(0.9, 0.84, 0.66), Vector3.ZERO, 1)
			var mesh := ArrayMesh.new()
			k.commit(mesh, _paint())
			var hand := m.bone_rest("hand_r")
			m.attach("hand_r", mesh, null, hand + Vector3(-0.04, -0.04, 0.04))
