shader_type spatial;
// Ground paint from vertex colours (r grass, g earth/road, b rock, a swamp mud) in the Dauntless
// look: lush saturated greens with broad painted patches of yellow-green, warm earthy roads with
// soft edges, warm grey rock on steep slopes with moss on its tops, the steppe's golden grass in the
// north-west, and stylised wrapped lighting.

uniform vec3 grass_deep : source_color = vec3(0.17, 0.36, 0.15);
uniform vec3 grass_sun : source_color = vec3(0.47, 0.62, 0.22);
uniform vec3 grass_steppe : source_color = vec3(0.80, 0.68, 0.34);
uniform vec3 earth : source_color = vec3(0.56, 0.40, 0.25);
uniform vec3 earth_light : source_color = vec3(0.72, 0.56, 0.36);
uniform vec3 rock_warm : source_color = vec3(0.58, 0.52, 0.46);
uniform vec3 rock_cool : source_color = vec3(0.36, 0.38, 0.42);
uniform vec3 moss : source_color = vec3(0.32, 0.46, 0.18);
uniform vec3 mud : source_color = vec3(0.27, 0.24, 0.16);
uniform float low_v = 256.0;

#include "res://shaders/stylized_light.gdshaderinc"

varying vec3 world_pos;
varying vec3 world_nrm;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float s = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		s += vnoise(p) * a;
		p = p * 2.03 + vec2(17.0, 9.0);
		a *= 0.5;
	}
	return s;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec4 w = COLOR;
	vec2 p = world_pos.xz;
	float big = fbm(p * 0.008);
	float mid = fbm(p * 0.05);
	float fine = vnoise(p * 0.9);

	// painted patches: deep shade green to sunny yellow-green
	vec3 grass = mix(grass_deep, grass_sun, smoothstep(0.3, 0.7, big * 0.7 + mid * 0.5));
	float steppe = smoothstep(low_v - 10.0, low_v + 25.0, -world_pos.z) * (1.0 - smoothstep(380.0, 420.0, world_pos.x));
	grass = mix(grass, grass_steppe * (0.9 + 0.2 * mid), steppe);
	grass *= 0.92 + 0.12 * fine;

	vec3 dirt = mix(earth, earth_light, smoothstep(0.35, 0.75, mid)) * (0.92 + 0.12 * fine);

	float strata = vnoise(vec2(world_pos.y * 0.35, (p.x + p.y) * 0.015));
	vec3 rock = mix(rock_cool, rock_warm, smoothstep(0.2, 0.8, strata * 0.6 + big * 0.6));
	// moss on the parts of rock that face the sky
	float mossy = smoothstep(0.55, 0.85, world_nrm.y + (mid - 0.5) * 0.4);
	rock = mix(rock, moss * (0.85 + 0.3 * mid), mossy * 0.75);

	vec3 mudc = mud * (0.85 + 0.3 * mid);

	float slope_rock = smoothstep(0.80, 0.6, world_nrm.y);
	float rock_w = max(w.b, slope_rock);
	// soft, wobbly road edges
	float earth_w = clamp(w.g * 1.15 + (mid - 0.5) * 0.4 * w.g, 0.0, 1.0);

	vec3 c = grass * w.r + dirt * earth_w + mudc * w.a;
	c /= max(w.r + earth_w + w.a, 0.001);
	c = mix(c, rock, clamp(rock_w, 0.0, 1.0));
	float snow = smoothstep(95.0, 120.0, world_pos.y + mid * 12.0) * smoothstep(0.55, 0.8, world_nrm.y);
	c = mix(c, vec3(0.9, 0.93, 0.98), snow);

	ALBEDO = c;
	ROUGHNESS = mix(0.95, 0.4, w.a * smoothstep(1.2, 0.2, world_pos.y));
	SPECULAR = 0.2;
}

void light() {
	stylized_light(NORMAL, VIEW, LIGHT, LIGHT_COLOR, ATTENUATION, ALBEDO, DIFFUSE_LIGHT, SPECULAR_LIGHT);
}
