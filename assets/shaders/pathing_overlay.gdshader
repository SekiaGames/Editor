shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform sampler2D pathing_texture : filter_nearest, repeat_disable;

void fragment() {
	vec4 cell = texture(pathing_texture, UV);
	ALBEDO = cell.rgb;
	ALPHA = cell.a;
}
