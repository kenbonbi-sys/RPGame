class_name HeroModel
extends Node3D
## The hero's body in the Dauntless direction, from Quaternius CC0 kits: an outfit from Modular
## Character Outfits – Fantasy (the Ranger, a scout, is Hồng Anh's) is the body and the skeleton;
## the head, eyes, brows and hair come from the Universal Base Characters (only the triangles that
## follow the head and neck bones are kept); Hồng Anh's red scarf and a big sword are added; the
## Universal Animation Libraries 1 and 2 are put onto the skeleton. Every kit shares the same bone
## names, so only rotations are copied (plus the hips' height, scaled), which keeps the proportions.
##
## The hero code asks for animations by the old KayKit names; ALIASES maps them to UAL clips.

const OUTFITS := "res://assets/third_party/quaternius_outfits/%s_%s.gltf"   # Male|Female, Ranger|Peasant
const HEADS := {
	"Male": "res://assets/third_party/quaternius_ubc/characters/Superhero_Male_FullBody.gltf",
	"Female": "res://assets/third_party/quaternius_ubc/characters/Superhero_Female_FullBody.gltf",
}
const SKIN_TEXTURES := {
	"Male": ["T_Superhero_Male_Ligh.png", "T_Superhero_Male_Dark.png"],
	"Female": ["T_Superhero_Female_Light_BaseColor.png", "T_Superhero_Female_Dark_BaseColor.png"],
}
const UBC_DIR := "res://assets/third_party/quaternius_ubc/characters/"
const HAIR := "res://assets/third_party/quaternius_ubc/hair/%s.gltf"
const LIBRARIES := [
	["res://assets/third_party/quaternius_ual/UAL1_Standard_UE.fbx", "Rig|"],
	["res://assets/third_party/quaternius_ual/UAL2_Standard.glb", ""],
]
const LOOPING := ["Idle", "Sword_Idle", "Walk", "Jog_Fwd", "Sprint", "NinjaJump_Idle", "Crouch_Idle",
	"Spell_Simple_Idle", "Idle_Shield", "Swim_Idle", "Sitting_Idle"]
const HEAD_BONES := ["Head", "neck_01"]

## KayKit name the hero code uses -> UAL clip.
const ALIASES := {
	"Idle": "Sword_Idle",
	"Walking_A": "Walk",
	"Running_A": "Jog_Fwd",
	"Running_B": "Sprint",
	"Jump_Start": "Jump_Start",
	"Jump_Idle": "NinjaJump_Idle",
	"Jump_Land": "Jump_Land",
	"Dodge_Forward": "Roll",
	"Hit_A": "Hit_Chest",
	"Hit_B": "Hit_Knockback",
	"Lie_Down": "Hit_Knockback",
	"Lie_StandUp": "LayToIdle",
	"Death_A": "Death01",
	"1H_Melee_Attack_Slice_Diagonal": "Sword_Regular_A",
	"1H_Melee_Attack_Slice_Horizontal": "Sword_Regular_B",
	"1H_Melee_Attack_Chop": "Sword_Regular_C",
	"1H_Melee_Attack_Stab": "Sword_Attack",
	"2H_Melee_Attack_Spin": "Sword_Regular_Combo",
	"Spellcast_Shoot": "Spell_Simple_Shoot",
	"Spellcast_Long": "Spell_Simple_Enter",
	"Spellcast_Raise": "Spell_Simple_Enter",
	"Throw": "OverhandThrow",
	"1H_Ranged_Shoot": "Spell_Simple_Shoot",
	"PickUp": "Crouch_Idle",
	"Climb": "ClimbUp_1m_RM",
}

@export var body := "Male"
@export var outfit := "Ranger"
@export var hood := false
@export var hair := "Hair_SimpleParted"
@export var light_skin := true
@export var hair_color := Color(0.26, 0.16, 0.09)
@export var scarf := Color(0.80, 0.12, 0.10)
@export var trim := Color(0.80, 0.60, 0.28)

## Render layer 2 keeps decals off the hero; layer 11 is only the hero, for the HUD's portrait camera.
const PORTRAIT_LAYER := 1 << 10

var skeleton: Skeleton3D
var anim: AnimationPlayer
var body_root: Node3D
## Bone -> its height above the soles in the rest pose, for sole_height().
var _sole_bones := {}
var _to_model := Transform3D.IDENTITY

static var _library: AnimationLibrary
static var _heads := {}


func _ready() -> void:
	body_root = (load(OUTFITS % [body, outfit]) as PackedScene).instantiate()
	add_child(body_root)
	skeleton = body_root.find_children("*", "Skeleton3D", true, false)[0]
	_dress_outfit()
	_add_head()
	_add_hair()
	anim = AnimationPlayer.new()
	anim.name = "AnimationPlayer"
	body_root.add_child(anim)
	anim.add_animation_library("", _library_for(skeleton, body_root.get_path_to(skeleton)))
	_scarf()
	_sword()
	for n in find_children("*", "MeshInstance3D", true, false):
		(n as MeshInstance3D).layers = 2 | PORTRAIT_LAYER
	var n: Node = skeleton
	while n != self:
		_to_model = (n as Node3D).transform * _to_model
		n = n.get_parent()
	for b in ["ball_l", "ball_r", "foot_l", "foot_r"]:
		var i := skeleton.find_bone(b)
		if i >= 0:
			_sole_bones[i] = (_to_model * skeleton.get_bone_global_rest(i)).origin.y


## How high the lower sole is above the model's origin in the current pose; some clips (the
## sword idle) dip the feet below where the rest pose stands.
func sole_height() -> float:
	var low := INF
	for i in _sole_bones:
		low = minf(low, (_to_model * skeleton.get_bone_global_pose(i)).origin.y - _sole_bones[i])
	return low if low < INF else 0.0


func resolve(anim_name: String) -> String:
	return ALIASES.get(anim_name, anim_name)


# ------------------------------------------------------------------ animations
## Builds (once) a library of every UAL clip remapped onto a skeleton with the same bone names.
static func _library_for(target: Skeleton3D, skeleton_path: NodePath) -> AnimationLibrary:
	if _library:
		return _library
	_library = AnimationLibrary.new()
	var target_hips := target.get_bone_rest(target.find_bone("pelvis")).origin
	for entry in LIBRARIES:
		var src: Node = (load(entry[0]) as PackedScene).instantiate()
		var ap: AnimationPlayer = src.find_children("*", "AnimationPlayer", true, false)[0]
		var src_sk: Skeleton3D = src.find_children("*", "Skeleton3D", true, false)[0]
		var src_hips := src_sk.get_bone_rest(src_sk.find_bone("pelvis")).origin
		var hip_scale := target_hips.length() / maxf(src_hips.length(), 0.0001)
		for full in ap.get_animation_list():
			var clip := String(full).trim_prefix(entry[1])
			if _library.has_animation(clip):
				continue
			var a := (ap.get_animation(full) as Animation).duplicate(true) as Animation
			for t in range(a.get_track_count() - 1, -1, -1):
				var path := a.track_get_path(t)
				var bone := String(path.get_concatenated_subnames())
				var type := a.track_get_type(t)
				if bone == "" or target.find_bone(bone) < 0 or bone == "root" or type == Animation.TYPE_SCALE_3D:
					a.remove_track(t)
					continue
				if type == Animation.TYPE_POSITION_3D:
					if bone != "pelvis":
						a.remove_track(t)
						continue
					for k in a.track_get_key_count(t):
						var v: Vector3 = a.track_get_key_value(t, k)
						a.track_set_key_value(t, k, target_hips + (v - src_hips) * hip_scale)
				a.track_set_path(t, NodePath(String(skeleton_path) + ":" + bone))
			if clip in LOOPING:
				a.loop_mode = Animation.LOOP_LINEAR
			_library.add_animation(clip, a)
		src.free()
	return _library


# ------------------------------------------------------------------ looks
func _dress_outfit() -> void:
	for n in body_root.find_children("*", "MeshInstance3D", true, false):
		var mi := n as MeshInstance3D
		if mi.name.contains("Hood") and not hood:
			mi.visible = false
			continue
		for s in mi.mesh.get_surface_count():
			var m := mi.mesh.surface_get_material(s)
			if not (m is BaseMaterial3D):
				continue
			var sm := (m as BaseMaterial3D).duplicate() as BaseMaterial3D
			sm.rim_enabled = true
			sm.rim = 0.35
			sm.rim_tint = 0.55
			# the outfit's bare skin (hands, neck) matches the head's skin
			if m.resource_name.begins_with("MI_Regular"):
				sm.albedo_texture = load(UBC_DIR + SKIN_TEXTURES[body][0 if light_skin else 1])
			mi.set_surface_override_material(s, sm)


## The head (and eyes, brows) of the Universal Base Character, bound to the outfit's skeleton.
func _add_head() -> void:
	var src: Node = (load(HEADS[body]) as PackedScene).instantiate()
	for n in src.find_children("*", "MeshInstance3D", true, false):
		var mi := n as MeshInstance3D
		var copy := MeshInstance3D.new()
		copy.skin = mi.skin
		if mi.name.begins_with("SuperHero"):
			copy.mesh = _head_only(mi.mesh, mi.skin)
			var m := mi.mesh.surface_get_material(0)
			if m is BaseMaterial3D:
				var sm := (m as BaseMaterial3D).duplicate() as BaseMaterial3D
				sm.albedo_texture = load(UBC_DIR + SKIN_TEXTURES[body][0 if light_skin else 1])
				sm.rim_enabled = true
				sm.rim = 0.35
				copy.material_override = sm
		else:
			copy.mesh = mi.mesh
		skeleton.add_child(copy)
		copy.skeleton = copy.get_path_to(skeleton)
	src.free()


## Keeps only the triangles whose corners mostly follow the head and neck bones.
static func _head_only(mesh: Mesh, skin: Skin) -> ArrayMesh:
	if _heads.has(mesh):
		return _heads[mesh]
	var head_binds := {}
	for i in skin.get_bind_count():
		if String(skin.get_bind_name(i)) in HEAD_BONES:
			head_binds[i] = true
	var out := ArrayMesh.new()
	for s in mesh.get_surface_count():
		var arrays := mesh.surface_get_arrays(s)
		var bones: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
		var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
		var per := bones.size() / maxi(1, (arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size())
		var on_head := PackedByteArray()
		on_head.resize((arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size())
		for v in on_head.size():
			var w := 0.0
			for j in per:
				if head_binds.has(bones[v * per + j]):
					w += weights[v * per + j]
			on_head[v] = 1 if w >= 0.5 else 0
		var idx: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		var kept := PackedInt32Array()
		for t in range(0, idx.size(), 3):
			if on_head[idx[t]] == 1 and on_head[idx[t + 1]] == 1 and on_head[idx[t + 2]] == 1:
				kept.append_array([idx[t], idx[t + 1], idx[t + 2]])
		arrays[Mesh.ARRAY_INDEX] = kept
		out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays, [], {},
			Mesh.ARRAY_FLAG_USE_8_BONE_WEIGHTS if per == 8 else 0)
		out.surface_set_material(out.get_surface_count() - 1, mesh.surface_get_material(s))
	_heads[mesh] = out
	return out


## Hair from the pack, bound to this skeleton's head bone.
func _add_hair() -> void:
	if hair == "" or hood:
		return
	var path := HAIR % hair
	if not ResourceLoader.exists(path):
		return
	var h: Node = (load(path) as PackedScene).instantiate()
	for n in h.find_children("*", "MeshInstance3D", true, false):
		var mi := n as MeshInstance3D
		var copy := MeshInstance3D.new()
		copy.mesh = mi.mesh
		copy.skin = mi.skin
		for s in mi.mesh.get_surface_count():
			var m := mi.mesh.surface_get_material(s)
			if m is BaseMaterial3D:
				var hm := (m as BaseMaterial3D).duplicate() as BaseMaterial3D
				hm.albedo_color = hair_color
				hm.rim_enabled = true
				hm.rim = 0.3
				copy.set_surface_override_material(s, hm)
		skeleton.add_child(copy)
		copy.skeleton = copy.get_path_to(skeleton)
	h.free()


func _mat(col: Color, metallic := 0.0, rough := 0.7) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = col
	m.metallic = metallic
	m.roughness = rough
	m.rim_enabled = true
	m.rim = 0.4
	m.rim_tint = 0.5
	return m


## A piece of gear placed in T-pose world space (metres) and pinned to a bone.
func _piece(bone: String, mesh: Mesh, mat: Material, at: Vector3, scale := Vector3.ONE, rot := Vector3.ZERO) -> Node3D:
	var i := skeleton.find_bone(bone)
	if i < 0:
		return null
	var att := BoneAttachment3D.new()
	att.bone_name = bone
	skeleton.add_child(att)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.transform = skeleton.get_bone_global_rest(i).affine_inverse() * Transform3D(Basis.from_euler(rot).scaled(scale), at)
	att.add_child(mi)
	return mi


## Hồng Anh's red scarf: a wrap at the neck and two tails over the back.
func _scarf() -> void:
	var red := _mat(scarf, 0.0, 0.85)
	var neck := skeleton.get_bone_global_rest(skeleton.find_bone("neck_01")).origin
	var wrap := CylinderMesh.new()
	wrap.top_radius = 0.085
	wrap.bottom_radius = 0.125
	wrap.height = 0.11
	_piece("neck_01", wrap, red, neck + Vector3(0, -0.02, 0.0))
	var chest := skeleton.get_bone_global_rest(skeleton.find_bone("spine_03")).origin
	for side in [-1.0, 1.0]:
		var tail := BoxMesh.new()
		tail.size = Vector3(0.1, 0.46, 0.025)
		_piece("spine_03", tail, red, chest + Vector3(side * 0.06, -0.06, -0.2), Vector3.ONE, Vector3(0.22, 0, side * 0.12))


## A broad Dauntless-style sword in the right hand: in T-pose the fist holds it pointing forward
## (+Z of the hand's frame). Built face by face so the bevels catch the light: a blade with a
## diamond section, bright edge bevels and a real point; a swept cross guard with a gem; a wrapped
## grip and a faceted pommel.
func _sword() -> void:
	var hand := skeleton.find_bone("hand_r")
	if hand < 0:
		return
	var rest := skeleton.get_bone_global_rest(hand)
	var root := Node3D.new()
	root.name = "Sword"
	var att := BoneAttachment3D.new()
	att.bone_name = "hand_r"
	skeleton.add_child(att)
	att.add_child(root)
	root.transform = rest.affine_inverse() * Transform3D(Basis.IDENTITY, rest.origin + Vector3(-0.07, -0.01, 0.02))
	var metal := StandardMaterial3D.new()
	metal.vertex_color_use_as_albedo = true
	metal.metallic = 0.65
	metal.roughness = 0.32
	metal.rim_enabled = true
	metal.rim = 0.4
	metal.rim_tint = 0.4
	var leather := _mat(Color(0.24, 0.14, 0.09), 0.0, 0.85)
	var m := MeshInstance3D.new()
	m.mesh = sword_mesh(trim)
	m.material_override = metal
	root.add_child(m)
	var g := MeshInstance3D.new()
	g.mesh = _grip_mesh()
	g.material_override = leather
	root.add_child(g)


## The metal of the sword along +Z: blade from the guard (z 0.1) to the point (z 1.28).
static func sword_mesh(gold: Color) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var flat := Color(0.62, 0.66, 0.7)
	var bevel := Color(0.88, 0.9, 0.93)
	var fuller := Color(0.45, 0.5, 0.56)
	# blade section: x is the thickness, y the width; stations [z, half width, half thickness]
	var stations := [[0.1, 0.082, 0.026], [0.55, 0.09, 0.024], [0.98, 0.078, 0.021], [1.28, 0.0, 0.003]]
	var ring := func(w: float, t: float) -> Array:
		return [Vector2(0, w), Vector2(t * 0.55, w * 0.74), Vector2(t, w * 0.18), Vector2(t, -w * 0.18), Vector2(t * 0.55, -w * 0.74),
			Vector2(0, -w), Vector2(-t * 0.55, -w * 0.74), Vector2(-t, -w * 0.18), Vector2(-t, w * 0.18), Vector2(-t * 0.55, w * 0.74)]
	# faces of the ring: edge bevels bright, flats steel, the middle strip a darker fuller
	var face_col := [bevel, flat, fuller, flat, bevel, bevel, flat, fuller, flat, bevel]
	for i in stations.size() - 1:
		var a: Array = stations[i]
		var b: Array = stations[i + 1]
		var ra: Array = ring.call(a[1], a[2])
		var rb: Array = ring.call(b[1], b[2])
		for k in ra.size():
			var k2 := (k + 1) % ra.size()
			var p0 := Vector3(ra[k].x, ra[k].y, a[0])
			var p1 := Vector3(ra[k2].x, ra[k2].y, a[0])
			var p2 := Vector3(rb[k2].x, rb[k2].y, b[0])
			var p3 := Vector3(rb[k].x, rb[k].y, b[0])
			var c: Color = face_col[k]
			if i == stations.size() - 2 and c == fuller:
				c = flat
			_quad(st, p0, p1, p2, p3, c)
	# cap the blade's root (inside the guard anyway)
	var r0: Array = ring.call(stations[0][1], stations[0][2])
	for k in range(1, r0.size() - 1):
		_tri(st, Vector3(r0[0].x, r0[0].y, 0.1), Vector3(r0[k + 1].x, r0[k + 1].y, 0.1), Vector3(r0[k].x, r0[k].y, 0.1), flat)
	# the cross guard: a bar across the width whose ends sweep toward the point, a gem in the middle
	var dark_gold := gold.darkened(0.35)
	_box(st, Vector3(0, 0, 0.08), Vector3(0.075, 0.2, 0.07), gold, dark_gold)
	for side in [-1.0, 1.0]:
		var base := Vector3(0, side * 0.1, 0.08)
		var tip := Vector3(0, side * 0.2, 0.17)
		_beam(st, base, tip, 0.032, 0.018, gold, dark_gold)
		_ball(st, tip, 0.026, gold)
	_ball(st, Vector3(0.04, 0, 0.08), 0.028, Color(0.2, 0.85, 0.75))
	_ball(st, Vector3(-0.04, 0, 0.08), 0.028, Color(0.2, 0.85, 0.75))
	# pommel
	_ball(st, Vector3(0, 0, -0.2), 0.04, gold)
	st.generate_normals()
	return st.commit()


static func _grip_mesh() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var col := Color.WHITE
	# leather wrapped in bands: radius steps up and down along the grip
	var n := 8
	var zs := []
	for i in 9:
		zs.append(lerpf(0.045, -0.17, i / 8.0))
	for i in zs.size() - 1:
		var r0 := 0.024 if i % 2 == 0 else 0.021
		var r1 := 0.021 if i % 2 == 0 else 0.024
		for k in n:
			var a0 := TAU * k / n
			var a1 := TAU * (k + 1) / n
			var p0 := Vector3(cos(a0) * r0, sin(a0) * r0, zs[i])
			var p1 := Vector3(cos(a1) * r0, sin(a1) * r0, zs[i])
			var p2 := Vector3(cos(a1) * r1, sin(a1) * r1, zs[i + 1])
			var p3 := Vector3(cos(a0) * r1, sin(a0) * r1, zs[i + 1])
			_quad(st, p0, p3, p2, p1, col)
	st.generate_normals()
	return st.commit()


static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color) -> void:
	for p in [a, b, c]:
		st.set_color(col)
		st.add_vertex(p)


## A quad a-b-c-d, wound so its front faces out of the solid it bounds.
static func _quad(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3, col: Color) -> void:
	_tri(st, a, c, b, col)
	_tri(st, a, d, c, col)


## A box with flat faces; the faces looking along ±Y (the bar's ends) get the darker colour.
static func _box(st: SurfaceTool, at: Vector3, size: Vector3, col: Color, end_col: Color) -> void:
	var h := size * 0.5
	var c := [Vector3(-h.x, -h.y, -h.z), Vector3(h.x, -h.y, -h.z), Vector3(h.x, h.y, -h.z), Vector3(-h.x, h.y, -h.z),
		Vector3(-h.x, -h.y, h.z), Vector3(h.x, -h.y, h.z), Vector3(h.x, h.y, h.z), Vector3(-h.x, h.y, h.z)]
	var faces := [[0, 3, 2, 1, col], [4, 5, 6, 7, col], [0, 1, 5, 4, end_col], [3, 7, 6, 2, end_col], [0, 4, 7, 3, col], [1, 2, 6, 5, col]]
	for f in faces:
		_quad(st, at + c[f[0]], at + c[f[1]], at + c[f[2]], at + c[f[3]], f[4])


## A square beam from a to b, tapering from r0 to r1.
static func _beam(st: SurfaceTool, a: Vector3, b: Vector3, r0: float, r1: float, col: Color, side_col: Color) -> void:
	var axis := (b - a).normalized()
	var u := axis.cross(Vector3.RIGHT).normalized()
	var v := axis.cross(u).normalized()
	var corners := [u + v, u - v, -u - v, -u + v]
	for k in 4:
		var k2 := (k + 1) % 4
		var c: Color = col if k % 2 == 0 else side_col
		_quad(st, a + corners[k] * r0, a + corners[k2] * r0, b + corners[k2] * r1, b + corners[k] * r1, c)
	_quad(st, b + corners[0] * r1, b + corners[1] * r1, b + corners[2] * r1, b + corners[3] * r1, col)


## A faceted ball (an octahedron split once), for gems, knobs and the pommel.
static func _ball(st: SurfaceTool, at: Vector3, r: float, col: Color) -> void:
	var dirs := [Vector3.RIGHT, Vector3.LEFT, Vector3.UP, Vector3.DOWN, Vector3.BACK, Vector3.FORWARD]
	var faces := [[0, 2, 4], [2, 1, 4], [1, 3, 4], [3, 0, 4], [2, 0, 5], [1, 2, 5], [3, 1, 5], [0, 3, 5]]
	for f in faces:
		var a: Vector3 = dirs[f[0]]
		var b: Vector3 = dirs[f[1]]
		var c: Vector3 = dirs[f[2]]
		var ab := (a + b).normalized()
		var bc := (b + c).normalized()
		var ca := (c + a).normalized()
		for t in [[a, ab, ca], [ab, b, bc], [ca, bc, c], [ab, bc, ca]]:
			_tri(st, at + t[0] * r, at + t[2] * r, at + t[1] * r, col)
