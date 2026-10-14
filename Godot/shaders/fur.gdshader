shader_type spatial;
// Shaggy colossus fur: strands running along the part's length (object-space Y), clumped
// shading and a soft sheen at the rim. 3D noise, so spheres have no pinched poles.
// Climbable surfaces wear this; armour does not.

uniform vec3 base : source_color = vec3(0.23, 0.20, 0.17);
uniform vec3 tip : source_color = vec3(0.42, 0.37, 0.30);
uniform float strand_density = 5.0;

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
	return mix(mix(mix(hash3(i + vec3(0, 0, 0)), hash3(i + vec3(1, 0, 0)), f.x),
				   mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
			   mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x),
				   mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	obj_pos = VERTEX;
}

void fragment() {
	vec3 p = obj_pos;
	float strands = vnoise3(p * vec3(strand_density, 0.35, strand_density));
	float fine = vnoise3(p * vec3(strand_density * 3.1, 0.9, strand_density * 3.1));
	float clumps = vnoise3(p * vec3(0.7, 0.25, 0.7) + vec3(3.0));
	float h = strands * 0.5 + fine * 0.25 + clumps * 0.35;
	ALBEDO = mix(base * 0.7, tip, smoothstep(0.3, 0.95, h));
	ROUGHNESS = 0.92;
	SPECULAR = 0.3;
	RIM = 0.55;
	RIM_TINT = 0.5;
	AO = mix(0.55, 1.0, h);
	AO_LIGHT_AFFECT = 0.6;
}
