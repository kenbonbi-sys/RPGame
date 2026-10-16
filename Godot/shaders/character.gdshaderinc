// Characters in the Dauntless look (the hero, the townsfolk, their armour): the outfit's own
// texture with its colours pushed richer toward a hand-painted read,
// lit by the same stylised light as the world (soft terminator, blue shadows) with a strong rim
// so silhouettes pop, and a tight painted highlight on metal.

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool has_texture = false;
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool has_normal = false;
uniform float normal_strength = 0.55;
// mip bias: how much fine texture noise is smoothed away (0: outfit sheets are atlases, a blur
// would bleed one piece's colour into the next)
uniform float soften = 0.0;
uniform float saturation = 1.15;
uniform float brightness = 1.05;
uniform float roughness_value = 0.75;
uniform float metallic_value = 0.0;
uniform bool use_alpha = false;
uniform float alpha_cut = 0.5;
uniform bool use_vertex_color = false;
uniform bool vertex_srgb = true;
uniform vec3 emission_color : source_color = vec3(0.0);
uniform float emission_energy = 0.0;

#include "res://shaders/stylized_light.gdshaderinc"

void fragment() {
	vec4 t = has_texture ? texture(albedo_texture, UV, soften) : vec4(1.0);
	vec3 c = t.rgb * albedo.rgb;
	if (use_vertex_color) {
		c *= vertex_srgb ? pow(max(COLOR.rgb, vec3(0.0)), vec3(2.2)) : COLOR.rgb;
	}
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = max(mix(vec3(l), c, saturation), vec3(0.0)) * brightness;
	ALBEDO = c;
	if (has_normal) {
		NORMAL_MAP = texture(normal_texture, UV).rgb;
		NORMAL_MAP_DEPTH = normal_strength;
	}
	ROUGHNESS = roughness_value;
	METALLIC = metallic_value;
	SPECULAR = 0.2;
	EMISSION = emission_color * emission_energy;
	if (use_alpha) {
		ALPHA = t.a * albedo.a;
		ALPHA_SCISSOR_THRESHOLD = alpha_cut;
	}
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
	// a small, bright painted highlight: strong on metal, a sheen on leather and cloth
	vec3 h = normalize(LIGHT + VIEW);
	float shine = mix(18.0, 70.0, metallic_value) * (1.2 - ROUGHNESS);
	float hl = pow(max(dot(NORMAL, h), 0.0), shine);
	hl = smoothstep(0.35, 0.6, hl);
	SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * hl * mix(0.06, 0.55, metallic_value) * mix(vec3(1.0), ALBEDO * 1.6 + 0.2, metallic_value);
}
