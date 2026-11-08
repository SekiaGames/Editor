shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_always, cull_disabled, fog_disabled;

uniform sampler2DArray water_texture : filter_linear_mipmap, repeat_enable;
uniform float frame = 0.0;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {

	vec4 ripple = texture(water_texture, vec3(UV, frame));
	ALBEDO = srgb_to_linear(COLOR.rgb * ripple.rgb);
	ALPHA = COLOR.a * ripple.a;
}
