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
	"Spell_Simple_Idle", "Idle_Shield", "Swim_Idle", "Sitting_Idle", "Pistol_Idle"]
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
## The weapon in hand (Weapons.ARMORY) and the idle stance its family wants.
var weapon_id := "w_kiem_hong_anh"
var idle_clip := "Sword_Idle"
var _weapon_nodes: Array[Node] = []
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
	set_weapon(weapon_id)
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
	if anim_name == "Idle":
		return idle_clip
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


## The weapon in hand (Weapons / WeaponArt): every piece is pinned to its hand bone in the hand's
## T-pose frame; dual weapons (chain blades, strikers, repeaters) fill both hands. Its family picks
## the idle stance (fists up for the strikers, pistols ready for the repeaters).
func set_weapon(id: String) -> void:
	for n in _weapon_nodes:
		if is_instance_valid(n):
			n.queue_free()
	_weapon_nodes.clear()
	weapon_id = id
	idle_clip = String(Weapons.family(id).get("idle", "Sword_Idle"))
	for piece in WeaponArt.build(id):
		var bone := "hand_r" if piece.hand == "r" else "hand_l"
		var i := skeleton.find_bone(bone)
		if i < 0:
			continue
		var rest := skeleton.get_bone_global_rest(i)
		var side := 1.0 if piece.hand == "r" else -1.0
		var att := BoneAttachment3D.new()
		att.bone_name = bone
		skeleton.add_child(att)
		_weapon_nodes.append(att)
		var root := Node3D.new()
		root.name = "Weapon"
		att.add_child(root)
		root.transform = rest.affine_inverse() * Transform3D(Basis.IDENTITY, rest.origin + Vector3(-0.07 * side, -0.01, 0.02))
		var glow_mat := WeaponArt.material("glow:" + String(Weapons.element(id)))
		for part in [["metal", WeaponArt.material("metal")], ["glow", glow_mat], ["grip", WeaponArt.material("grip")]]:
			var mesh: Mesh = piece.get(part[0])
			if mesh == null:
				continue
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = part[1]
			mi.layers = 2 | PORTRAIT_LAYER
			root.add_child(mi)
