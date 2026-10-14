shader_type spatial;
render_mode cull_disabled;
// Tree canopies in the Dauntless style: the faceted Kenney leaves get normals that point out from
// the canopy's centre, so a whole crown shades like one soft painted blob; colour runs from a deep
// cool green underneath to a sunny yellow-green on top; leaves sway in the wind.

uniform vec3 albedo : source_color = vec3(0.31, 0.43, 0.22);
uniform vec3 top_color : source_color = vec3(0.62, 0.74, 0.28);
uniform vec3 under_color : source_color = vec3(0.10, 0.26, 0.22);
uniform float sway = 0.035;
uniform float canopy_y = 0.75;     // canopy centre height in the model (Kenney trees ~1.2 tall)

#include "res://shaders/stylized_light.gdshaderinc"

varying float height01;
varying float variation;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = world.x * 0.07 + world.z * 0.05;
	float h = max(VERTEX.y, 0.0);
	vec3 centre = vec3(0.0, canopy_y, 0.0);
	vec3 blob = normalize(VERTEX - centre + vec3(0.0, 0.15, 0.0));
	NORMAL = normalize(mix(NORMAL, blob, 0.85));
	VERTEX.x += sin(TIME * 1.3 + phase) * sway * h;
	VERTEX.z += cos(TIME * 1.1 + phase * 1.3) * sway * 0.6 * h;
	height01 = clamp((VERTEX.y - canopy_y * 0.55) / (canopy_y * 0.9), 0.0, 1.0);
	variation = hash(world.xz);
}

void fragment() {
	vec3 base = albedo * (0.85 + 0.3 * variation);
	vec3 c = mix(under_color * (albedo / vec3(0.31, 0.43, 0.22)), base, smoothstep(0.0, 0.5, height01));
	c = mix(c, top_color * (0.9 + 0.2 * variation), smoothstep(0.55, 1.0, height01) * 0.7);
	ALBEDO = c;
	ROUGHNESS = 0.95;
	BACKLIGHT = c * 0.4;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
