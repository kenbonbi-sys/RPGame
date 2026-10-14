shader_type spatial;
// Tree crowns and bushes built by TreeArt: puffy lumps whose normals already lean out from the
// whole crown. The mesh colour carries data: R = height in the crown (deep cool green underneath
// to sunny yellow-green on top), G = how far out from the crown's heart (the inside is darker),
// B = a random shade per lump. Light wraps far round and glows through from behind; the crown
// sways above `crown_base`.

uniform vec3 top_color : source_color = vec3(0.72, 0.84, 0.32);
uniform vec3 mid_color : source_color = vec3(0.33, 0.58, 0.22);
uniform vec3 under_color : source_color = vec3(0.07, 0.26, 0.22);
uniform float sway = 0.05;
uniform float crown_base = 3.0;
uniform float translucency = 0.35;

#include "res://shaders/stylized_light.gdshaderinc"

varying vec3 obj_pos;

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
	vec3 world = MODEL_MATRIX[3].xyz;
	float phase = world.x * 0.07 + world.z * 0.05;
	float h = max(VERTEX.y - crown_base, 0.0) * 0.2;
	VERTEX.x += sin(TIME * 1.3 + phase + VERTEX.y * 0.25) * sway * h;
	VERTEX.z += cos(TIME * 1.1 + phase * 1.3) * sway * 0.6 * h;
}

void fragment() {
	float h = COLOR.r;
	float out_ = COLOR.g;
	float shade = COLOR.b;
	vec3 c = mix(under_color, mid_color, smoothstep(0.0, 0.55, h));
	c = mix(c, top_color, smoothstep(0.5, 1.0, h) * 0.85);
	c *= 0.86 + 0.28 * shade;
	c *= mix(0.5, 1.0, out_);
	float n = vnoise3(obj_pos * 1.4);
	float dapple = vnoise3(obj_pos * 4.5 + 3.0);
	c *= 0.88 + 0.16 * n + 0.08 * dapple;
	ALBEDO = c;
	ROUGHNESS = 0.95;
	SPECULAR = 0.15;
	AO = mix(0.45, 1.0, out_);
	AO_LIGHT_AFFECT = 0.25;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
	float back = clamp(dot(-NORMAL, LIGHT), 0.0, 1.0);
	DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * back * translucency * vec3(0.9, 1.0, 0.55) / 3.14159;
}
