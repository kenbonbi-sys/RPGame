shader_type spatial;
// Ground paint from vertex colours (r grass, g earth/road, b rock, a swamp mud), with rock on
// steep slopes, the steppe's dry grass in the north-west and soft procedural detail. Muted,
// slightly washed colours in the spirit of Shadow of the Colossus.

uniform vec3 grass_forest : source_color = vec3(0.30, 0.42, 0.22);
uniform vec3 grass_light : source_color = vec3(0.45, 0.55, 0.30);
uniform vec3 grass_steppe : source_color = vec3(0.66, 0.60, 0.36);
uniform vec3 earth : source_color = vec3(0.47, 0.39, 0.29);
uniform vec3 rock_a : source_color = vec3(0.50, 0.49, 0.46);
uniform vec3 rock_b : source_color = vec3(0.36, 0.36, 0.35);
uniform vec3 mud : source_color = vec3(0.24, 0.23, 0.17);
uniform float low_v = 256.0;

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
	float big = fbm(p * 0.012);
	float mid = fbm(p * 0.09);
	float fine = vnoise(p * 1.7);

	// grass: forest green, sunlit patches, and dry steppe grass up on the plateau
	vec3 grass = mix(grass_forest, grass_light, smoothstep(0.35, 0.75, big) * 0.8);
	float steppe = smoothstep(low_v - 10.0, low_v + 25.0, -world_pos.z) * (1.0 - smoothstep(380.0, 420.0, world_pos.x));
	grass = mix(grass, grass_steppe, steppe);
	grass *= 0.85 + 0.3 * mid;

	vec3 dirt = earth * (0.8 + 0.35 * mid) * (0.9 + 0.2 * fine);

	float strata = vnoise(vec2(world_pos.y * 0.6, (p.x + p.y) * 0.02));
	vec3 rock = mix(rock_b, rock_a, smoothstep(0.25, 0.8, strata * 0.7 + mid * 0.5));
	rock *= 0.85 + 0.25 * fine;

	vec3 mudc = mud * (0.8 + 0.4 * mid);

	float slope_rock = smoothstep(0.82, 0.62, world_nrm.y);
	float rock_w = max(w.b, slope_rock);
	// break the road edges up a little
	float earth_w = clamp(w.g + (mid - 0.5) * 0.35 * w.g, 0.0, 1.0);

	vec3 c = grass * w.r + dirt * earth_w + mudc * w.a;
	c /= max(w.r + earth_w + w.a, 0.001);
	c = mix(c, rock, clamp(rock_w, 0.0, 1.0));
	// snow-dusted peaks far up in the mountains
	float snow = smoothstep(95.0, 120.0, world_pos.y + mid * 12.0) * smoothstep(0.55, 0.8, world_nrm.y);
	c = mix(c, vec3(0.86, 0.88, 0.9), snow);

	ALBEDO = c;
	ROUGHNESS = mix(0.95, 0.75, w.a);
	SPECULAR = 0.25;
	// wet mud near the water
	ROUGHNESS = mix(ROUGHNESS, 0.35, w.a * smoothstep(1.2, 0.2, world_pos.y));
}
