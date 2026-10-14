shader_type spatial;
render_mode cull_disabled;
// Lush stylised grass (the Dauntless meadows): blades lean in a rolling wind, part around the
// hero, shade from a cool root to a warm sunlit tip, and shrink away at the edge of the patch so
// it never pops. Normals point up so the field lights like one soft surface.

uniform vec3 root_color : source_color = vec3(0.14, 0.30, 0.12);
uniform vec3 tip_color : source_color = vec3(0.58, 0.72, 0.26);
uniform vec3 dry_color : source_color = vec3(0.78, 0.70, 0.36);
uniform float wind_strength = 0.18;
uniform vec3 hero_pos = vec3(0.0, -1000.0, 0.0);
uniform vec3 camera_pos = vec3(0.0);
uniform float fade_start = 34.0;
uniform float fade_end = 46.0;

#include "res://shaders/stylized_light.gdshaderinc"

varying float tip;
varying vec3 world_pos;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	tip = UV.y;
	vec3 base = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float dist = length(base.xz - camera_pos.xz);
	float shrink = 1.0 - smoothstep(fade_start, fade_end, dist);
	VERTEX *= shrink;
	vec4 w = MODEL_MATRIX * vec4(VERTEX, 1.0);
	float gust = vnoise(w.xz * 0.05 + vec2(TIME * 0.35, TIME * 0.2));
	float bend = tip * tip * (wind_strength * (0.4 + gust) + 0.05 * sin(TIME * 2.3 + w.x * 0.8 + w.z * 0.6));
	w.x += bend;
	w.z += bend * 0.6;
	// part around the hero
	vec2 away = w.xz - hero_pos.xz;
	float near = 1.0 - smoothstep(0.3, 1.3, length(away));
	if (abs(w.y - hero_pos.y) < 2.5) {
		w.xz += normalize(away + vec2(0.001)) * near * tip * 0.55;
		w.y -= near * tip * 0.25;
	}
	world_pos = w.xyz;
	VERTEX = (inverse(MODEL_MATRIX) * w).xyz;
	NORMAL = (inverse(MODEL_MATRIX) * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
}

void fragment() {
	float patchy = vnoise(world_pos.xz * 0.04);
	vec3 tipc = mix(tip_color, dry_color, smoothstep(0.55, 0.9, patchy) * 0.6);
	ALBEDO = mix(root_color, tipc, smoothstep(0.0, 1.0, tip));
	ROUGHNESS = 0.9;
	BACKLIGHT = ALBEDO * 0.5 * tip;
	AO = mix(0.5, 1.0, tip);
	AO_LIGHT_AFFECT = 0.4;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
