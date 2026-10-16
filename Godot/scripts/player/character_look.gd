class_name CharacterLook
extends RefCounted
## Puts characters (the hero, the townsfolk) and the armour drawn on them into the Dauntless
## character look (shaders/character.gdshader): every standard material on their meshes becomes
## the stylised one, keeping its texture, colour, normal map, metal and roughness. Materials are
## converted once and shared.

const SHADER := preload("res://shaders/character.gdshader")
const SHADER_DOUBLE := preload("res://shaders/character_double.gdshader")
## How strong the rim is on characters (the world uses less).
const RIM := 0.55
const WRAP := 0.45

static var _done := {}


static func stylize(root: Node) -> void:
	for n in root.find_children("*", "MeshInstance3D", true, false):
		stylize_mesh(n as MeshInstance3D)


static func stylize_mesh(mi: MeshInstance3D) -> void:
	if mi.material_override is BaseMaterial3D:
		mi.material_override = stylized_material(mi.material_override)
	elif mi.mesh:
		for s in mi.mesh.get_surface_count():
			var m := mi.get_surface_override_material(s)
			if m == null:
				m = mi.mesh.surface_get_material(s)
			if m is BaseMaterial3D:
				mi.set_surface_override_material(s, stylized_material(m))


static func stylized_material(base: BaseMaterial3D) -> Material:
	if _done.has(base):
		return _done[base]
	# glowing things (weapon accents, the cell in the Middleman's hand) keep their own material
	if base.emission_enabled and base.emission_energy_multiplier > 1.0 or base.shading_mode == BaseMaterial3D.SHADING_MODE_UNSHADED:
		_done[base] = base
		return base
	var m := ShaderMaterial.new()
	m.shader = SHADER_DOUBLE if base.cull_mode == BaseMaterial3D.CULL_DISABLED else SHADER
	m.set_shader_parameter("albedo", base.albedo_color)
	if base.albedo_texture:
		m.set_shader_parameter("albedo_texture", base.albedo_texture)
		m.set_shader_parameter("has_texture", true)
	if base.normal_enabled and base.normal_texture:
		m.set_shader_parameter("normal_texture", base.normal_texture)
		m.set_shader_parameter("has_normal", true)
	if base.vertex_color_use_as_albedo:
		m.set_shader_parameter("use_vertex_color", true)
		m.set_shader_parameter("vertex_srgb", base.vertex_color_is_srgb)
	m.set_shader_parameter("roughness_value", clampf(base.roughness, 0.3, 1.0))
	# a metal map (the outfits' ORM sheets) marks a few buckles, not the whole cloth: treat those
	# materials as cloth and leather; plain metal (armour, blades) keeps its value
	var textured_metal := base is ORMMaterial3D or base.metallic_texture != null
	m.set_shader_parameter("metallic_value", 0.0 if textured_metal else base.metallic)
	m.set_shader_parameter("rim_amount", RIM)
	m.set_shader_parameter("wrap_amount", WRAP)
	if base.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
		m.set_shader_parameter("use_alpha", true)
		m.set_shader_parameter("alpha_cut", base.alpha_scissor_threshold if base.transparency == BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR else 0.4)
	if base.emission_enabled:
		m.set_shader_parameter("emission_color", base.emission)
		m.set_shader_parameter("emission_energy", base.emission_energy_multiplier)
	_done[base] = m
	return m
