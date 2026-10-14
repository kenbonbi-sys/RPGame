shader_type spatial;
// Shared skin for the code-built creatures: a base colour with soft noise, optional round spots
// (mushroom caps), a rim light, and a white flash while `hurt` is up.

uniform vec3 albedo : source_color = vec3(0.6, 0.5, 0.4);
uniform vec3 spot_color : source_color = vec3(0.95, 0.92, 0.82);
uniform float spots = 0.0;          // 0 = none, else spot density
uniform float roughness = 0.85;
uniform float rim = 0.25;
uniform float hurt = 0.0;
// status colour (frozen, burning, poisoned...) with its strength in alpha
uniform vec4 tint = vec4(0.0);

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
}

void fragment() {
	float n = vnoise3(obj_pos * 4.0);
	vec3 c = albedo * (0.85 + 0.3 * n);
	if (spots > 0.0) {
		vec3 cell = floor(obj_pos * spots);
		vec3 f = fract(obj_pos * spots) - 0.5;
		float r = 0.22 + 0.18 * hash3(cell);
		float on = step(0.45, hash3(cell + 7.0));
		c = mix(c, spot_color, on * smoothstep(r, r - 0.06, length(f)));
	}
	c = mix(c, tint.rgb, tint.a);
	ALBEDO = mix(c, vec3(1.0), hurt);
	ROUGHNESS = roughness;
	RIM = rim;
	RIM_TINT = 0.4;
	EMISSION = vec3(hurt) * 0.5;
}
