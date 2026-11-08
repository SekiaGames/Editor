shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_disabled, fog_disabled;

uniform vec4 shallow : source_color = vec4(0.20, 0.36, 0.62, 1.0);
uniform vec4 deep : source_color = vec4(0.08, 0.20, 0.42, 1.0);

void fragment() {
	float fres = clamp(1.0 - dot(NORMAL, VIEW), 0.0, 1.0);
	ALBEDO = mix(deep.rgb, shallow.rgb, fres);
}
