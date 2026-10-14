shader_type spatial;
// Kenney foliage leaves in the game palette, swaying gently: the higher up the model, the more
// it moves; neighbouring trees move out of step.

uniform vec3 albedo : source_color = vec3(0.31, 0.43, 0.22);
uniform float sway = 0.035;

varying float shade;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = world.x * 0.07 + world.z * 0.05;
	float h = max(VERTEX.y, 0.0);
	VERTEX.x += sin(TIME * 1.3 + phase) * sway * h;
	VERTEX.z += cos(TIME * 1.1 + phase * 1.3) * sway * 0.6 * h;
	shade = clamp(VERTEX.y * 0.6 + 0.45, 0.55, 1.15);
}

void fragment() {
	ALBEDO = albedo * shade;
	ROUGHNESS = 0.9;
	SPECULAR = 0.2;
	BACKLIGHT = albedo * 0.35;
}
