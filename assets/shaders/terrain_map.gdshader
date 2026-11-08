shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_back, fog_disabled;

uniform sampler2DArray ground_texture : filter_linear_mipmap, repeat_disable;

uniform bool lighting_enabled = true;

varying flat vec4 layer_index;

const vec3 LIGHT_DIRECTION = vec3(-0.6095, 0.5079, -0.6095);

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	layer_index = CUSTOM0;
}

void fragment() {

	vec4 color = texture(ground_texture, vec3(UV, layer_index.x));
	vec4 layer1 = texture(ground_texture, vec3(UV, layer_index.y));
	vec4 layer2 = texture(ground_texture, vec3(UV, layer_index.z));
	vec4 layer3 = texture(ground_texture, vec3(UV, layer_index.w));
	color.rgb = mix(color.rgb, layer1.rgb, layer1.a);
	color.rgb = mix(color.rgb, layer2.rgb, layer2.a);
	color.rgb = mix(color.rgb, layer3.rgb, layer3.a);

	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float shade = lighting_enabled ? clamp(dot(world_normal, LIGHT_DIRECTION) + 0.45, 0.0, 1.0) : 1.0;
	ALBEDO = srgb_to_linear(color.rgb * shade);
}
