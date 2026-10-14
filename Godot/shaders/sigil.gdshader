shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
// A colossus's weak point: a pale glowing rune on its hide that breathes slowly, flares when
// struck and fades as it weakens (health 0..1).

uniform vec4 color : source_color = vec4(0.55, 0.95, 0.85, 1.0);
uniform float health = 1.0;
uniform float flare = 0.0;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float ring = smoothstep(0.08, 0.0, abs(r - 0.72));
	float inner = smoothstep(0.06, 0.0, abs(r - 0.42)) * step(0.5, fract(a * 6.0 / 6.2832 + 0.25));
	float spokes = smoothstep(0.05, 0.0, abs(p.x)) * step(r, 0.62) + smoothstep(0.05, 0.0, abs(p.y)) * step(r, 0.62);
	float core = smoothstep(0.25, 0.0, r);
	float glow = smoothstep(1.0, 0.0, r) * 0.35;
	float pulse = 0.65 + 0.35 * sin(TIME * 2.4);
	float m = (ring + inner * 0.8 + spokes * 0.6 + core) * pulse + glow;
	float k = (0.35 + 0.65 * health) * (1.0 + flare * 2.5);
	ALBEDO = color.rgb * m * k * 2.2;
	ALPHA = clamp(m * k, 0.0, 1.0);
}
