class_name HealOrb
extends Node3D
## Terra's proc: a green orb of life that bobs on the ground; a hero who walks into it heals a share
## of their life. It fades after 15 s.

var share := 0.12
var _age := 0.0
var _mat: StandardMaterial3D

const LIFE := 15.0
const PICKUP := 1.3


static func spawn(at: Vector3, heal_share: float) -> void:
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null or tree.current_scene == null:
		return
	var orb := HealOrb.new()
	orb.share = heal_share
	tree.current_scene.add_child(orb)
	at.y = Terrain.height_at_world(at) if Terrain.current else at.y
	orb.global_position = at + Vector3.UP * 0.6
	orb.add_to_group("heal_orbs")


func _ready() -> void:
	var mi := MeshInstance3D.new()
	mi.mesh = CreatureArt.sphere(0.22, 12)
	_mat = StandardMaterial3D.new()
	_mat.albedo_color = Color(0.55, 1.0, 0.45)
	_mat.emission_enabled = true
	_mat.emission = Color(0.4, 1.0, 0.35)
	_mat.emission_energy_multiplier = 2.5
	_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mi.material_override = _mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	var l := OmniLight3D.new()
	l.light_color = Color(0.5, 1.0, 0.4)
	l.light_energy = 1.2
	l.omni_range = 3.0
	add_child(l)


func _physics_process(delta: float) -> void:
	_age += delta
	position.y += sin(_age * 3.0) * 0.004
	rotate_y(delta * 2.0)
	_mat.albedo_color.a = clampf((LIFE - _age) / 2.0, 0.0, 1.0)
	if _age >= LIFE:
		queue_free()
		return
	for h in get_tree().get_nodes_in_group("heroes"):
		if is_instance_valid(h) and not h.is_dead() and h.global_position.distance_to(global_position) < PICKUP + 0.6:
			var hh: Health = h.get("health")
			if hh and hh.hp < hh.max_hp:
				hh.heal(roundf(hh.max_hp * share))
				Fx.burst(global_position, Vector3.UP, Color(0.5, 1.0, 0.4, 0.9), 20, 3.0, 0.25, 0.6, 60.0, 1.0, true)
				queue_free()
				return
