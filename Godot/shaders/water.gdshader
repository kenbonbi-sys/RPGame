shader_type spatial;
render_mode cull_disabled;
// Still, murky swamp water: dark green-brown, a slow ripple in the normals, mirror-like at
// grazing angles.

uniform vec3 deep : source_color = vec3(0.10, 0.14, 0.11);
uniform vec3 shallow : source_color = vec3(0.24, 0.29, 0.22);
uniform float ripple_scale = 0.18;

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
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_pos.xz * ripple_scale;
	float t = TIME * 0.25;
	float n1 = vnoise(p + vec2(t, t * 0.7));
	float n2 = vnoise(p * 1.9 - vec2(t * 0.8, t * 0.3));
	vec3 n = normalize(vec3((n1 - 0.5) * 0.25, 1.0, (n2 - 0.5) * 0.25));
	NORMAL = (VIEW_MATRIX * vec4(n, 0.0)).xyz;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALBEDO = mix(deep, shallow, 0.4 + 0.3 * n1);
	ROUGHNESS = 0.08;
	METALLIC = 0.0;
	SPECULAR = 0.6;
	ALPHA = 1.0;
	EMISSION = vec3(0.0);
	ALBEDO = mix(ALBEDO, ALBEDO * 0.6, fres);
}
