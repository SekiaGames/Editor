shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_disabled, fog_disabled;

uniform sampler2DArray cliff_texture : filter_linear_mipmap, repeat_disable;

uniform bool lighting_enabled = true;

varying flat float layer_index;
varying float shade_value;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	layer_index = CUSTOM0.x;
	shade_value = CUSTOM0.y;
}

void fragment() {
	vec3 color = texture(cliff_texture, vec3(UV, layer_index)).rgb;

	float shade = lighting_enabled ? shade_value : 1.0;
	ALBEDO = srgb_to_linear(color * shade);
}
