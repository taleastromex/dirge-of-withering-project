shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix, shadows_disabled;

uniform vec4 albedo : source_color = vec4(0.32, 0.05, 0.08, 0.18);
uniform float soft_power : hint_range(0.8, 6.0, 0.1) = 2.8;
uniform float emission_strength : hint_range(0.0, 2.0, 0.05) = 0.45;
uniform float density : hint_range(0.05, 1.0, 0.01) = 0.55;

void fragment() {
	// Facing falloff: soft blob, no hard silhouette.
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float soft = pow(facing, soft_power) * density;

	ALBEDO = albedo.rgb;
	ALPHA = clamp(albedo.a * soft, 0.0, 1.0);
	EMISSION = albedo.rgb * emission_strength * soft;
}
