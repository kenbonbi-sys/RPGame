shader_type spatial;
// Stylised rock: warm grey with painted variation, cool in the hollows, moss creeping over every
// face that looks up at the sky, wrapped lighting.

uniform vec3 albedo : source_color = vec3(0.56, 0.53, 0.48);
uniform vec3 moss : source_color = vec3(0.33, 0.47, 0.18);
uniform float moss_amount = 0.8;

#include "res://shaders/stylized_light.gdshaderinc"

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
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float n = vnoise3(world_pos * 0.6);
	float n2 = vnoise3(world_pos * 2.3);
	vec3 c = albedo * (0.8 + 0.3 * n) * (0.92 + 0.1 * n2);
	c = mix(c * vec3(0.85, 0.9, 1.05), c, smoothstep(-0.3, 0.4, world_nrm.y));
	float mossy = smoothstep(0.45, 0.8, world_nrm.y + (n - 0.5) * 0.5) * moss_amount;
	c = mix(c, moss * (0.8 + 0.35 * n2), mossy);
	ALBEDO = c;
	ROUGHNESS = 0.9;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
