class_name Townsfolk
extends Node3D
## The townsfolk of the village hub: the Ramsgate cast of Dauntless, by name and trade as the
## Dauntless wiki has them (dauntless.fandom.com), each at a place laid out the way Ramsgate puts
## them: Wils and Moyra at the smithy off the plaza; Kat Sorrel by the stairs left of the smithy at
## the Sparkforge; Janek Zai and the Middleman on the terrace right of it; Granny Strega in the
## plaza; Dr. Priyani on the outskirts; Gaius Copperwheel at his map table; Bulvar Rosk at the
## Training Grounds with practice dummies. Their looks follow their portraits as far as the
## character kit goes; their lines are written here in their voices (not taken from the game).
##
## Places are metres east (x) and north (y) of the village centre; `face` is the point they turn to
## when nobody is near.

const CAST := {
	"kat": {
		"name": "Katerin Sorrel", "title": "Người Hướng Dẫn", "at": Vector2(-17.0, 32.0), "face": Vector2(-4, 31),
		"look": {"body": "Female", "outfit": "Ranger", "texture": "T_Ranger_3_BaseColor", "hair": "Hair_SimpleParted",
			"hair_color": Color(0.2, 0.12, 0.07), "scarf": Color(0.93, 0.91, 0.87), "armour": {"arms": "pangar"},
			"idle": "Idle_FoldArms"},
		"greet": "Đến rồi à. Tốt. Chẳng ai thành thợ săn bằng cách ngồi chờ behemoth mò tới tận cửa.",
		"about": "Ta lớn lên ở vùng biên, mồ côi từ bé. Giờ mỗi lần ta chỉ dạy một người, và người đó lúc này là ngươi. Đừng làm ta mất mặt.",
		"services": [["Tôi nên làm gì tiếp?", "advice"], ["Con đường thợ săn", "path"]],
	},
	"wils": {
		"name": "Wils Bormen", "title": "Thợ Rèn Vũ Khí", "at": Vector2(-8.6, 23.4), "face": Vector2(-8.6, 21.0), "busy": true,
		"look": {"body": "Male", "outfit": "Peasant", "texture": "T_Peasant_2_BaseColor", "hair": "Hair_Buzzed,Hair_Beard",
			"hair_color": Color(0.9, 0.88, 0.84), "weapon": "npc_bua_ren", "extras": ["cap"], "idle": "TreeChopping"},
		"greet": "Thép Ostograd, búa Ostograd, giá cả cũng kiểu Ostograd. Cần vũ khí thì ngươi tới đúng chỗ rồi.",
		"about": "Nhà ta buôn bán khắp Ostia, ta là con thứ. Họ bỏ tiền dựng chốn này, ta bỏ búa. Đừng mặc cả với ta, ta không thích.",
		"services": [["Rèn / đổi vũ khí", "loadout:0"]],
	},
	"moyra": {
		"name": "Moyra Heigsketter", "title": "Thợ Rèn Giáp", "at": Vector2(-13.4, 22.2), "face": Vector2(-4, 12),
		"look": {"body": "Female", "outfit": "Peasant", "texture": "T_Peasant_2_BaseColor", "tint": Color(0.72, 0.72, 0.74),
			"hair": "Hair_SimpleParted", "hair_color": Color(0.74, 0.3, 0.12), "weapon": "npc_bua_ren", "idle": "Sword_Idle"},
		"greet": "Lò này mẹ tôi để lại. Giáp ra khỏi đây thì phải giữ được mạng người mặc nó.",
		"about": "Mẹ tôi là Varya Heigsketter, tôi vẫn rèn theo sổ tay của bà. Bác Wils giúp tôi giữ lò sau khi bà mất. Với thợ săn, tôi lấy giá công bằng.",
		"services": [["Rèn / đổi giáp", "loadout:1"]],
	},
	"middleman": {
		"name": "The Middleman", "title": "Người Buôn Cell", "at": Vector2(10.6, 27.0), "face": Vector2(8, 14),
		"look": {"body": "Male", "outfit": "Ranger", "tint": Color(0.5, 0.55, 0.62), "hair": "", "armour": {"head": "boreus",
			"torso": "boreus", "arms": "boreus", "legs": "boreus"}, "extras": ["visor", "orb"], "idle": "Idle_Lantern"},
		"greet": "Ngươi không thấy ta. Ta cũng không thấy ngươi. Vậy... cần Cell à?",
		"about": "Hàng từ đâu tới à? Từ chỗ không ai hỏi. Mỗi tuần ba viên Cell mới, và ta ghép hai viên thành một viên tốt hơn, cho ai trả đủ.",
		"services": [["Xem Cell", "cells"]],
	},
	"priyani": {
		"name": "Dr. Shaed Priyani", "title": "Nhà Nghiên Cứu Behemoth", "at": Vector2(33.0, 8.2), "face": Vector2(30, 0),
		"look": {"body": "Female", "outfit": "Ranger", "texture": "T_Ranger_3_BaseColor", "tint": Color(0.94, 0.9, 1.0),
			"hair": "Hair_Long", "hair_color": Color(0.07, 0.05, 0.04), "light_skin": false, "extras": ["book"],
			"idle": "Idle_TalkingPhone"},
		"greet": "Mỗi con behemoth là một câu đố. Giải xong thì nó chết. Tôi rất thích câu đố.",
		"about": "Tôi dựng Trường Nghiên Cứu Behemoth của Orrery. Có thời tôi nghiên cứu chúng vì tò mò. Bây giờ thì chỉ để biết cách giết chúng nhanh hơn.",
		"services": [["Hồ sơ behemoth", "beasts"]],
	},
	"gaius": {
		"name": "Gaius Copperwheel", "title": "Nhà Vẽ Bản Đồ", "at": Vector2(3.5, -13.6), "face": Vector2(3.5, -11.0), "busy": true,
		"look": {"body": "Male", "outfit": "Ranger", "texture": "T_Ranger_3_BaseColor", "tint": Color(0.92, 0.46, 0.38),
			"hood": true, "hair": "Hair_Buzzed", "light_skin": false, "armour": {"arms": "pangar"}, "idle": "Idle_Rail"},
		"greet": "Đừng giẫm lên bản đồ. Không, không phải tấm đó, tấm kia. Thôi được, cứ đứng đó.",
		"about": "Tôi học ở St. Avellaine, chuyên theo dấu bão tối. Một lần đắm tàu, tôi sống một mình nhiều tháng, từ đó quen nói chuyện với chính mình. Đúng không, Gaius? Đúng.",
		"services": [["Xem bản đồ", "map"]],
	},
	"janek": {
		"name": "Janek Zai", "title": "Đô Đốc Ostia", "at": Vector2(5.6, 25.6), "face": Vector2(2, 12),
		"look": {"body": "Male", "outfit": "Ranger", "texture": "T_Ranger_3_BaseColor", "tint": Color(0.36, 0.42, 0.6),
			"hair": "Hair_SimpleParted", "hair_color": Color(0.88, 0.89, 0.92), "scarf": Color(0.92, 0.87, 0.74),
			"armour": {"arms": "gnasher"}, "weapon": "npc_sung_janek", "extras": ["eyepiece"], "idle": "Pistol_Idle"},
		"greet": "Hạm Đội Viễn Chinh Thứ Ba không chờ ai cả, thợ săn. Nói nhanh.",
		"about": "Ta chỉ huy chiến hạm Indomitable. Cha ta rèn súng, ta bắn chúng. Kẻ thù lớn nhất của ta không có móng vuốt: đó là Vận May.",
		"services": [["Nhận Súng Ostia", "repeater"], ["Lựu đạn", "grenades"]],
	},
	"strega": {
		"name": "Granny Strega", "title": "Bà Bán Thuốc", "at": Vector2(-6.8, 5.4), "face": Vector2(-5.4, 3.6),
		"look": {"body": "Female", "outfit": "Peasant", "tint": Color(0.62, 0.82, 0.55), "hair": "Hair_Buns",
			"hair_color": Color(0.72, 0.72, 0.74), "scale": 0.88, "extras": ["witch_hat", "staff"], "idle": "Spell_Simple_Idle"},
		"greet": "Thuốc của bà chữa được mọi thứ, trừ tính tò mò. Mua đi cưng.",
		"about": "Bà đi khắp các đảo, còn mấy ông chồng của bà thì... đi khắp các nghĩa trang. Cái huy hiệu này à? Quà của một người bạn. Đừng hỏi nữa.",
		"services": [["Mua thuốc", "shop"]],
	},
	"bulvar": {
		"name": "Bulvar Rosk", "title": "Bậc Thầy Vũ Khí · Crimson Blades", "at": Vector2(32.4, 14.6), "face": Vector2(36, 19),
		"look": {"body": "Male", "outfit": "Ranger", "texture": "T_Ranger_3_BaseColor", "tint": Color(0.8, 0.66, 0.52),
			"hair": "Hair_Buns", "hair_color": Color(0.06, 0.05, 0.04), "light_skin": false, "scale": 1.1,
			"armour": {"torso": "pangar", "arms": "pangar"}, "weapon": "npc_riu_bulvar", "idle": "Sword_Idle"},
		"greet": "Crimson Blades chỉ nhận người giỏi. Ngươi chưa giỏi. Lại đây, đánh cái hình nộm kia cho ta xem.",
		"about": "Kat dạy ta cầm vũ khí, rồi ta dạy cả một đoàn lính đánh thuê. Ta nói ta giỏi nhất không phải để khoe đâu, chỉ là nói thật.",
		"services": [["Các loại vũ khí", "weapons"], ["Tập với hình nộm", "dummies"]],
	},
}

## Granny Strega's shelf: the game's three potions at their value in gold.
const SHOP := ["potion_red", "potion_blue", "potion_green"]

var npcs := {}


func _ready() -> void:
	var c := WorldMap.village_center()
	var rng := RandomNumberGenerator.new()
	rng.seed = 41
	_smithy(rng, c + Vector2(-11.0, 26.0))
	_sparkforge(c + Vector2(-19.6, 33.6))
	_terrace(rng, c + Vector2(8.0, 26.6))
	_cauldron(c + Vector2(-5.4, 3.6))
	_research_desk(rng, c + Vector2(33.0, 6.6))
	_map_table(rng, c + Vector2(3.5, -12.0))
	_training_grounds(rng, c + Vector2(35.5, 18.0))
	for id in CAST:
		var d: Dictionary = CAST[id]
		var at: Vector2 = c + d.at
		var to: Vector2 = (c + d.face) - at
		var npc := Npc.new()
		npc.name = String(id).capitalize()
		npc.setup(id, d, atan2(to.x, -to.y))
		npc.position = Props.ground(at)
		add_child(npc)
		npcs[id] = npc


# ------------------------------------------------------------------ the places they work
func _mesh(kits: HouseArt.Kits, m: Vector2, yaw := 0.0, solid_size := Vector3.ZERO) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = kits.mesh()
	mi.position = Props.ground(m)
	mi.rotation.y = yaw
	add_child(mi)
	if solid_size != Vector3.ZERO:
		var body := StaticBody3D.new()
		var cs := CollisionShape3D.new()
		var box := BoxShape3D.new()
		box.size = solid_size
		cs.shape = box
		cs.position = Vector3(0, solid_size.y * 0.5, 0)
		body.add_child(cs)
		mi.add_child(body)
	return mi


static func _xf(p: Vector3, b := Basis.IDENTITY) -> Transform3D:
	return Transform3D(b, p)


static func _glow_mat(c: Color, energy: float) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = HouseArt.PAINTED
	m.set_shader_parameter("glow", c)
	m.set_shader_parameter("glow_energy", energy)
	m.set_shader_parameter("paint", 0.05)
	return m


func _light(at: Vector3, c: Color, energy: float, reach: float) -> OmniLight3D:
	var l := OmniLight3D.new()
	l.light_color = c
	l.light_energy = energy
	l.omni_range = reach
	l.position = at
	add_child(l)
	return l


## The smithy off the plaza: an open timber shelter under a tiled roof, the forge glowing at the
## back with its chimney, Wils's anvil out front, Moyra's armour stand, a rack and a quench barrel.
func _smithy(rng: RandomNumberGenerator, m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	var w := 9.0
	var d := 5.6
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			kits.k("wood").rounded_box(Vector3(0.32, 3.6, 0.32), 0.06, _xf(Vector3(sx * w * 0.5, 1.8, sz * d * 0.5)), HouseArt.TIMBER)
	kits.k("wood").rounded_box(Vector3(w + 0.6, 0.3, 0.36), 0.05, _xf(Vector3(0, 3.6, d * 0.5)), HouseArt.TIMBER)
	kits.k("wood").rounded_box(Vector3(w + 0.6, 0.3, 0.36), 0.05, _xf(Vector3(0, 3.6, -d * 0.5)), HouseArt.TIMBER)
	# a lean-to roof of tiles, sloping toward the plaza
	var tiles := 9
	for i in tiles:
		var z := -d * 0.5 - 0.5 + (d + 1.0) * (i + 0.5) / tiles
		kits.k("roof").rounded_box(Vector3(w + 1.2, 0.12, (d + 1.0) / tiles + 0.05), 0.04,
			_xf(Vector3(0, 4.3 - (z + d * 0.5) * 0.22, z), Basis(Vector3.RIGHT, 0.22)),
			HouseArt._shade(Color(0.62, 0.3, 0.2), rng, 0.08))
	# the forge: a stone hearth at the back with a chimney
	kits.k("stone").rounded_box(Vector3(2.6, 1.1, 1.6), 0.12, _xf(Vector3(-2.2, 0.55, -1.8)), Color(0.5, 0.48, 0.46), 0.03, rng)
	kits.k("stone").rounded_box(Vector3(1.2, 4.8, 1.0), 0.1, _xf(Vector3(-2.2, 2.6, -2.3)), Color(0.46, 0.44, 0.43), 0.03, rng)
	# Moyra's armour stand: a post with a breastplate
	kits.k("wood").rounded_box(Vector3(0.14, 1.5, 0.14), 0.03, _xf(Vector3(-4.2, 0.75, 3.3)), HouseArt.TIMBER)
	kits.k("wood").rounded_box(Vector3(0.9, 0.12, 0.12), 0.03, _xf(Vector3(-4.2, 1.45, 3.3)), HouseArt.TIMBER)
	kits.k("trim").rounded_box(Vector3(0.62, 0.7, 0.32), 0.12, _xf(Vector3(-4.2, 1.15, 3.36)), Color(0.55, 0.6, 0.66))
	# a rack of blades along the side
	kits.k("wood").rounded_box(Vector3(0.12, 1.6, 2.4), 0.03, _xf(Vector3(4.3, 0.8, -0.6)), HouseArt.TIMBER)
	for i in 4:
		kits.k("trim").rounded_box(Vector3(0.06, 1.2, 0.12), 0.02, _xf(Vector3(4.15, 0.9, -1.5 + i * 0.6)), Color(0.72, 0.74, 0.78))
	var root := _mesh(kits, m, 0.0, Vector3.ZERO)
	# solid parts: the hearth and the posts
	var body := StaticBody3D.new()
	for spot in [[Vector3(-2.2, 0.55, -1.8), Vector3(2.6, 1.1, 1.6)], [Vector3(-2.2, 2.4, -2.3), Vector3(1.2, 4.8, 1.0)],
			[Vector3(4.3, 0.8, -0.6), Vector3(0.3, 1.6, 2.4)]]:
		var cs := CollisionShape3D.new()
		var box := BoxShape3D.new()
		box.size = spot[1]
		cs.shape = box
		cs.position = spot[0]
		body.add_child(cs)
	root.add_child(body)
	# the coals
	var coals := MeshInstance3D.new()
	var ck := MeshKit.new()
	for i in 9:
		var cc := Color(1.0, 0.45 + 0.2 * rng.randf(), 0.12)
		ck.lump(Vector3.ONE * rng.randf_range(0.13, 0.2), 1, _xf(Vector3(-2.9 + (i % 3) * 0.6, 1.12, -2.2 + (i / 3) * 0.4)), 0.2, i,
			func(_p: Vector3, _q: Vector3) -> Color: return cc)
	var cm := ArrayMesh.new()
	ck.commit(cm, _glow_mat(Color(1.0, 0.42, 0.1), 3.0))
	coals.mesh = cm
	root.add_child(coals)
	var fire := _light(root.position + Vector3(-2.2, 1.6, -1.6), Color(1.0, 0.5, 0.2), 2.6, 7.0)
	fire.shadow_enabled = false
	# Wils's anvil in front of him
	var anvil := HouseArt.Kits.new()
	anvil.k("wood").tube(0.32, 0.36, 0.55, 8, _xf(Vector3.ZERO), HouseArt.TIMBER * 0.9, Vector3.ZERO, 1)
	anvil.k("trim").rounded_box(Vector3(0.36, 0.3, 0.26), 0.04, _xf(Vector3(0, 0.68, 0)), Color(0.25, 0.26, 0.28))
	anvil.k("trim").rounded_box(Vector3(0.82, 0.16, 0.34), 0.04, _xf(Vector3(0.04, 0.88, 0)), Color(0.3, 0.31, 0.33))
	anvil.k("trim").prism(Vector2(0.45, 0.08), Vector2(0.45, -0.08), Vector2(0.75, 0.0), 0.18, _xf(Vector3(0, 0.88, 0)), Color(0.3, 0.31, 0.33))
	_mesh(anvil, WorldMap.village_center() + Vector2(-8.6, 22.2), PI * 0.5, Vector3(0.9, 1.0, 0.5))
	var barrel := MeshInstance3D.new()
	barrel.mesh = HouseArt.barrel_mesh(rng)
	barrel.position = Props.ground(m + Vector2(3.6, -3.4))
	add_child(barrel)


## Kat's Sparkforge: a stone plinth with an aether crystal pulsing cyan.
func _sparkforge(m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	kits.k("stone").rounded_box(Vector3(1.4, 0.9, 1.4), 0.14, _xf(Vector3(0, 0.45, 0)), Color(0.52, 0.5, 0.48))
	kits.k("trim").rounded_box(Vector3(1.0, 0.16, 1.0), 0.05, _xf(Vector3(0, 0.98, 0)), Color(0.72, 0.58, 0.3))
	var root := _mesh(kits, m, 0.4, Vector3(1.4, 1.0, 1.4))
	var crystal := MeshInstance3D.new()
	var k := MeshKit.new()
	k.prism(Vector2(-0.2, 0), Vector2(0.2, 0), Vector2(0, 0.9), 0.24, _xf(Vector3(0, 1.05, 0)), Color(0.4, 0.95, 1.0))
	k.prism(Vector2(-0.12, 0), Vector2(0.12, 0), Vector2(0, 0.55), 0.16, _xf(Vector3(0.18, 1.05, 0.1), Basis(Vector3.BACK, -0.35)), Color(0.4, 0.95, 1.0))
	var cm := ArrayMesh.new()
	k.commit(cm, _glow_mat(Color(0.35, 0.9, 1.0), 2.4))
	crystal.mesh = cm
	root.add_child(crystal)
	_light(root.position + Vector3.UP * 1.6, Color(0.4, 0.85, 1.0), 1.4, 5.0)


## The terrace right of the smithy: a plank deck with a rail, Janek's crate of grenades, the
## Middleman's pedestal of cells.
func _terrace(rng: RandomNumberGenerator, m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	var w := 9.0
	var d := 5.0
	for i in 9:
		kits.k("wood").rounded_box(Vector3(w, 0.12, d / 9.0 - 0.04), 0.02, _xf(Vector3(0, 0.08, -d * 0.5 + d * (i + 0.5) / 9.0)),
			HouseArt._shade(HouseArt.TIMBER * 1.25, rng, 0.08))
	for i in 7:
		var x := -w * 0.5 + w * i / 6.0
		kits.k("wood").rounded_box(Vector3(0.14, 1.0, 0.14), 0.03, _xf(Vector3(x, 0.55, -d * 0.5)), HouseArt.TIMBER)
	kits.k("wood").rounded_box(Vector3(w + 0.2, 0.1, 0.12), 0.03, _xf(Vector3(0, 1.0, -d * 0.5)), HouseArt.TIMBER)
	# Janek's crate, grenades in it
	kits.k("wood").rounded_box(Vector3(1.0, 0.6, 0.7), 0.05, _xf(Vector3(-3.3, 0.45, 0.6)), HouseArt.TIMBER * 1.3)
	for i in 4:
		kits.k("trim").lump(Vector3.ONE * 0.13, 1, _xf(Vector3(-3.6 + i * 0.2, 0.82, 0.6 + (i % 2) * 0.12)), 0.05, i,
			func(_p: Vector3, _q: Vector3) -> Color: return Color(0.3, 0.33, 0.3))
	# the Middleman's pedestal
	kits.k("stone").rounded_box(Vector3(0.7, 1.0, 0.7), 0.08, _xf(Vector3(2.6, 0.6, -1.0)), Color(0.36, 0.38, 0.44))
	_mesh(kits, m, 0.0)
	var cells := MeshKit.new()
	var colors := [Color(0.73, 0.0, 1.0), Color(0.0, 0.79, 0.24), Color(0.0, 0.45, 0.92)]
	for i in 3:
		var cc: Color = colors[i]
		cells.lump(Vector3.ONE * 0.11, 1, _xf(Vector3(2.45 + i * 0.16, 1.2, -1.0 + (i % 2) * 0.1)), 0.15, i,
			func(_p: Vector3, _q: Vector3) -> Color: return cc)
	var cm := ArrayMesh.new()
	cells.commit(cm, _glow_mat(Color(0.8, 0.4, 1.0), 1.6))
	var mi := MeshInstance3D.new()
	mi.mesh = cm
	mi.position = Props.ground(m)
	add_child(mi)


## Granny Strega's cauldron in the plaza: black iron, a green brew bubbling, a little fire under.
func _cauldron(m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	kits.k("trim").lump(Vector3(0.62, 0.48, 0.62), 2, _xf(Vector3(0, 0.55, 0)), 0.02, 2,
		func(_p: Vector3, _q: Vector3) -> Color: return Color(0.16, 0.16, 0.18), Vector3.INF, 0.0, 1.0)
	kits.k("trim").tube(0.58, 0.6, 0.12, 14, _xf(Vector3(0, 0.88, 0)), Color(0.2, 0.2, 0.22), Vector3.ZERO, 1)
	for a in 3:
		var p := Vector3(cos(TAU * a / 3.0), 0, sin(TAU * a / 3.0)) * 0.42
		kits.k("trim").rounded_box(Vector3(0.08, 0.3, 0.08), 0.02, _xf(p + Vector3(0, 0.12, 0)), Color(0.16, 0.16, 0.18))
	var root := _mesh(kits, m, 0.0, Vector3(1.2, 1.0, 1.2))
	var brew := MeshInstance3D.new()
	var disc := CylinderMesh.new()
	disc.top_radius = 0.54
	disc.bottom_radius = 0.54
	disc.height = 0.04
	brew.mesh = disc
	var bm := StandardMaterial3D.new()
	bm.albedo_color = Color(0.4, 0.95, 0.35)
	bm.emission_enabled = true
	bm.emission = Color(0.35, 0.9, 0.3)
	bm.emission_energy_multiplier = 1.8
	brew.material_override = bm
	brew.position = Vector3(0, 0.86, 0)
	root.add_child(brew)
	var bubbles := GPUParticles3D.new()
	bubbles.amount = 16
	bubbles.lifetime = 1.4
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3.UP
	pm.spread = 10.0
	pm.initial_velocity_min = 0.3
	pm.initial_velocity_max = 0.7
	pm.gravity = Vector3(0, 0.2, 0)
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 0.4
	pm.color = Color(0.55, 1.0, 0.5, 0.7)
	bubbles.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(0.18, 0.18)
	q.material = Fx._quad_material(true)
	bubbles.draw_pass_1 = q
	bubbles.position = Vector3(0, 0.95, 0)
	root.add_child(bubbles)
	var l := _light(root.position + Vector3(0, 1.2, 0), Color(0.45, 1.0, 0.4), 1.2, 4.5)
	l.shadow_enabled = false


## Dr. Priyani's desk on the outskirts: notes, books, a specimen jar glowing faintly, a horn.
func _research_desk(rng: RandomNumberGenerator, m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	_table(kits, Vector3(2.0, 0.95, 1.0))
	for i in 3:
		kits.k("trim").rounded_box(Vector3(0.3, 0.06, 0.22), 0.01, _xf(Vector3(-0.6 + i * 0.05, 1.0 + i * 0.06, -0.15)),
			[Color(0.45, 0.22, 0.4), Color(0.25, 0.32, 0.5), Color(0.5, 0.36, 0.2)][i])
	kits.k("trim").rounded_box(Vector3(0.5, 0.01, 0.36), 0.002, _xf(Vector3(0.1, 0.98, 0.1), Basis(Vector3.UP, 0.2)), Color(0.95, 0.92, 0.82))
	kits.k("trim").tube(0.05, 0.01, 0.5, 8, _xf(Vector3(0.65, 0.98, 0.25), Basis(Vector3.BACK, 1.2)), Color(0.92, 0.88, 0.78), Vector3(0.0, 0.1, 0.05), 4)
	var root := _mesh(kits, m, 0.0, Vector3(2.0, 1.0, 1.0))
	var jar := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.13
	cyl.bottom_radius = 0.14
	cyl.height = 0.36
	jar.mesh = cyl
	var gm := StandardMaterial3D.new()
	gm.albedo_color = Color(0.5, 0.95, 0.9, 0.35)
	gm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	gm.emission_enabled = true
	gm.emission = Color(0.3, 0.8, 0.75)
	gm.emission_energy_multiplier = 0.8
	jar.material_override = gm
	jar.position = Vector3(0.6, 1.16, -0.2)
	root.add_child(jar)


## Gaius's map table: the world map spread out, rolled maps beside it.
func _map_table(rng: RandomNumberGenerator, m: Vector2) -> void:
	var kits := HouseArt.Kits.new()
	_table(kits, Vector3(2.2, 1.0, 1.4))
	for i in 3:
		kits.k("trim").tube(0.05, 0.05, 0.7, 8, _xf(Vector3(0.75, 1.07, -0.45 + i * 0.12), Basis(Vector3.BACK, PI * 0.5)),
			Color(0.9, 0.84, 0.66).darkened(i * 0.08), Vector3.ZERO, 1)
	var root := _mesh(kits, m, 0.0, Vector3(2.2, 1.0, 1.4))
	var map := MeshInstance3D.new()
	var quad := PlaneMesh.new()
	quad.size = Vector2(1.5, 0.96)
	map.mesh = quad
	var mm := StandardMaterial3D.new()
	mm.albedo_texture = load("res://world/map_preview.png")
	mm.albedo_color = Color(1.0, 0.94, 0.8)
	mm.roughness = 0.9
	map.material_override = mm
	map.position = Vector3(-0.25, 1.03, 0)
	root.add_child(map)


## A plain wooden table of `size`, top at size.y.
func _table(kits: HouseArt.Kits, size: Vector3) -> void:
	kits.k("wood").rounded_box(Vector3(size.x, 0.08, size.z), 0.03, _xf(Vector3(0, size.y - 0.04, 0)), HouseArt.TIMBER * 1.3)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			kits.k("wood").rounded_box(Vector3(0.1, size.y - 0.08, 0.1), 0.02,
				_xf(Vector3(sx * (size.x * 0.5 - 0.12), (size.y - 0.08) * 0.5, sz * (size.z * 0.5 - 0.12))), HouseArt.TIMBER)


## Bulvar's Training Grounds: a fenced yard, the Crimson Blades' banner, three practice dummies.
func _training_grounds(rng: RandomNumberGenerator, m: Vector2) -> void:
	var fence := HouseArt.fence_mesh(rng, 12.0)
	for side in [[Vector2(0, 5.5), 0.0], [Vector2(6.0, 0), PI * 0.5]]:
		var mi := MeshInstance3D.new()
		mi.mesh = fence
		mi.position = Props.ground(m + side[0])
		mi.rotation.y = side[1]
		add_child(mi)
	var banner := MeshInstance3D.new()
	banner.mesh = HouseArt.banner_mesh(Color(0.62, 0.08, 0.1), Color(0.95, 0.76, 0.34))
	banner.position = Props.ground(m + Vector2(-4.8, 3.2))
	add_child(banner)
	for spot in [Vector2(-1.8, 1.6), Vector2(1.4, 2.4), Vector2(3.8, 0.2)]:
		var dummy := TrainingDummy.new()
		dummy.position = Props.ground(m + spot)
		var to: Vector2 = (m + Vector2(-3.0, -3.4)) - (m + spot)
		dummy.rotation.y = atan2(to.x, -to.y)
		add_child(dummy)
