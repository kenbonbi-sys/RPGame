shader_type spatial;
// Colossus fur, painted: chunky locks running along the part's length (object-space Y), darker
// roots and sunlit tips, moss settling on whatever faces the sky, the stylised light.
// Climbable surfaces wear this; armour does not.

uniform vec3 base : source_color = vec3(0.23, 0.20, 0.17);
uniform vec3 tip : source_color = vec3(0.42, 0.37, 0.30);
uniform vec3 moss : source_color = vec3(0.34, 0.48, 0.2);
uniform float moss_amount = 0.65;
uniform float strand_density = 3.0;

#include "res://shaders/stylized_light.gdshaderinc"

varying vec3 obj_pos;
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
	return mix(mix(mix(hash3(i + vec3(0, 0, 0)), hash3(i + vec3(1, 0, 0)), f.x),
				   mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
			   mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x),
				   mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	obj_pos = VERTEX;
	world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 p = obj_pos;
	float locks = vnoise3(p * vec3(strand_density, 0.25, strand_density));
	float fine = vnoise3(p * vec3(strand_density * 2.6, 0.6, strand_density * 2.6));
	float clumps = vnoise3(p * vec3(0.5, 0.2, 0.5) + vec3(3.0));
	float h = locks * 0.5 + fine * 0.15 + clumps * 0.35;
	// soft painted streaks, lit from above: pale on the back, deep underneath
	vec3 c = mix(base, tip, smoothstep(0.32, 0.78, h));
	c *= mix(0.86, 1.16, smoothstep(-0.7, 0.8, world_nrm.y));
	float mossy = smoothstep(0.55, 0.85, world_nrm.y + (clumps - 0.5) * 0.5) * moss_amount;
	c = mix(c, moss * (0.8 + 0.4 * fine), mossy);
	ALBEDO = c;
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
	AO = mix(0.6, 1.0, h);
	AO_LIGHT_AFFECT = 0.4;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
