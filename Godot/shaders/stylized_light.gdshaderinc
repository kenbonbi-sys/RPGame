// Stylised lighting shared by the world shaders (the Dauntless look): a soft, wrapped light
// terminator instead of a hard Lambert falloff, shadows tinted cool blue rather than grey, and a
// warm rim where the light grazes. Include after the uniforms, then call stylized_light() from
// light().

uniform vec3 shadow_tint : source_color = vec3(0.55, 0.68, 0.95);
uniform float wrap_amount = 0.35;
uniform float rim_amount = 0.25;

void stylized_light(vec3 normal, vec3 view, vec3 light, vec3 light_color, float attenuation, vec3 base_color,
		inout vec3 diffuse, inout vec3 specular) {
	float ndl = dot(normal, light);
	float wrapped = clamp((ndl + wrap_amount) / (1.0 + wrap_amount), 0.0, 1.0);
	// a gentle band at the terminator reads as painted
	wrapped = smoothstep(0.0, 1.0, wrapped);
	float lit = wrapped * attenuation;
	vec3 shade = mix(shadow_tint * 0.35, vec3(1.0), lit);
	diffuse += light_color * shade * lit / 3.14159;
	// cool fill on the shadowed side so it never goes muddy
	diffuse += light_color * shadow_tint * 0.06 * (1.0 - lit);
	float rim = pow(1.0 - clamp(dot(normal, view), 0.0, 1.0), 3.0) * smoothstep(-0.2, 0.5, ndl);
	specular += light_color * rim * rim_amount * attenuation * base_color;
}
