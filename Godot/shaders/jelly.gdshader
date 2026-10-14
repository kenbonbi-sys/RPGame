shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back;
// A slime's body: see-through green jelly with a darker core, a bright rim, and moss growing on
// its top (object-space up). `hurt` flashes it white when struck.

uniform vec3 jelly : source_color = vec3(0.35, 0.72, 0.35);
uniform vec3 core : source_color = vec3(0.12, 0.35, 0.16);
uniform vec3 moss : source_color = vec3(0.22, 0.36, 0.14);
uniform float moss_line = 0.35;
uniform float hurt = 0.0;
// status colour (frozen, burning, poisoned...) with its strength in alpha
uniform vec4 tint = vec4(0.0);

#include "res://shaders/stylized_light.gdshaderinc"

varying vec3 obj_pos;
varying vec3 obj_nrm;

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
	obj_nrm = NORMAL;
}

void fragment() {
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	vec3 c = mix(core, jelly, 0.35 + 0.65 * fres);
	float n = vnoise3(obj_pos * 7.0);
	float mossy = smoothstep(moss_line - 0.1, moss_line + 0.1, obj_nrm.y + (n - 0.5) * 0.5);
	c = mix(c, moss * (0.7 + 0.6 * n), mossy);
	c = mix(c, tint.rgb, tint.a);
	ALBEDO = mix(c, vec3(1.0), hurt);
	ALPHA = mix(0.72 + 0.25 * fres, 1.0, max(mossy, hurt));
	ROUGHNESS = mix(0.12, 0.9, mossy);
	SPECULAR = 0.7;
	EMISSION = jelly * fres * 0.35 + vec3(hurt) * 0.6;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
	// a wet highlight
	vec3 h = normalize(LIGHT + VIEW);
	SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * smoothstep(0.95, 0.985, dot(NORMAL, h)) * 0.45;
}
