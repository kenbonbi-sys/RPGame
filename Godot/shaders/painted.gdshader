shader_type spatial;
// Painted surfaces for code-built things (houses, props, bark, creatures): a base colour times the
// mesh's vertex colour (sRGB), soft painterly variation, optional moss on faces that look up,
// optional glow, the stylised light, and the creature hooks (`hurt` flashes white, `tint` shows a
// status colour).

uniform vec3 albedo : source_color = vec3(1.0);
uniform float vertex_tint = 1.0;
uniform float paint = 0.16;
uniform float paint_scale = 1.2;
// a second layer of variation in world space, for big scaled things (rocks) seen up close
uniform float world_paint = 0.0;
uniform float world_paint_scale = 0.9;
uniform vec3 moss : source_color = vec3(0.36, 0.52, 0.2);
uniform float moss_amount = 0.0;
uniform float roughness = 0.85;
uniform float specular_amount = 0.3;
uniform vec3 glow : source_color = vec3(0.0);
uniform float glow_energy = 0.0;
uniform float hurt = 0.0;
uniform vec4 tint = vec4(0.0);

#include "res://shaders/stylized_light.gdshaderinc"

varying vec3 obj_pos;
varying vec3 world_pos;
varying vec3 world_nrm;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float vnoise3(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
			   mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	obj_pos = VERTEX;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float n = vnoise3(obj_pos * paint_scale);
	float n2 = vnoise3(obj_pos * paint_scale * 3.7 + 5.0);
	vec3 vc = pow(max(COLOR.rgb, vec3(0.0)), vec3(2.2));
	vec3 c = albedo * mix(vec3(1.0), vc, vertex_tint);
	c *= 1.0 - paint + paint * 2.0 * (0.65 * n + 0.35 * n2);
	if (world_paint > 0.0) {
		float w = vnoise3(world_pos * world_paint_scale) * 0.6 + vnoise3(world_pos * world_paint_scale * 3.3 + 2.0) * 0.4;
		c *= 1.0 - world_paint + world_paint * 2.0 * w;
	}
	float mossy = smoothstep(0.55, 0.85, world_nrm.y + (n - 0.5) * 0.6) * moss_amount;
	c = mix(c, moss * (0.85 + 0.3 * n2), mossy);
	c = mix(c, tint.rgb, tint.a);
	ALBEDO = mix(c, vec3(1.0), hurt);
	ROUGHNESS = roughness;
	SPECULAR = specular_amount;
	EMISSION = glow * glow_energy + vec3(hurt) * 0.5;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
