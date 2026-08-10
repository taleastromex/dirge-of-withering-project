shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, unshaded, shadows_disabled;

uniform sampler2D blood_tex : source_color, hint_default_transparent;
uniform vec4 tint : source_color = vec4(1.35, 0.22, 0.28, 1.0);
uniform float emission_strength : hint_range(0.0, 3.0, 0.05) = 0.85;
uniform float alpha_boost : hint_range(0.5, 3.0, 0.05) = 1.35;
uniform float edge_softness : hint_range(0.5, 4.0, 0.05) = 1.65;
uniform float core_opacity : hint_range(0.4, 1.0, 0.01) = 0.88;

void fragment() {
	// Soft multi-tap sample — smears hard silhouette without portal animation.
	vec2 px = 1.5 / vec2(textureSize(blood_tex, 0));
	vec4 blood = texture(blood_tex, UV) * 0.36;
	blood += texture(blood_tex, UV + vec2(px.x, 0.0)) * 0.12;
	blood += texture(blood_tex, UV - vec2(px.x, 0.0)) * 0.12;
	blood += texture(blood_tex, UV + vec2(0.0, px.y)) * 0.12;
	blood += texture(blood_tex, UV - vec2(0.0, px.y)) * 0.12;
	blood += texture(blood_tex, UV + px) * 0.08;
	blood += texture(blood_tex, UV - px) * 0.08;

	float mask = blood.a;
	if (mask < 0.02) {
		discard;
	}

	vec3 col = max(blood.rgb, vec3(0.22, 0.03, 0.05)) * tint.rgb;
	col = clamp(col, vec3(0.0), vec3(1.5));

	float soft = pow(clamp(mask * alpha_boost, 0.0, 1.0), edge_softness);
	float alpha = soft * mix(0.55, core_opacity, smoothstep(0.15, 0.7, mask));

	ALBEDO = col;
	ALPHA = clamp(alpha, 0.0, 1.0);
	EMISSION = col * emission_strength * soft;
}
