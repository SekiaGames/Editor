uniform bool use_base_texture = true;
uniform sampler2D base_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 base_color : source_color = vec4(1.0);

uniform bool use_team_color = false;
uniform vec4 team_color : source_color = vec4(1.0);

uniform bool use_detail = false;
uniform sampler2D detail_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 detail_color : source_color = vec4(1.0);

uniform float layer_alpha = 1.0;
uniform float color_scale = 1.0;

vec4 war3_sample(vec2 uv) {
	vec4 col = use_base_texture ? texture(base_texture, uv) : base_color;

	if (use_team_color) {
		col.rgb = team_color.rgb;
	}

	if (use_detail) {
		vec4 detail = texture(detail_texture, uv) * detail_color;

		col.rgb = mix(col.rgb, detail.rgb, detail.a);
		col.a = max(col.a, detail.a);
	}

	col.rgb *= color_scale;
	col.a *= layer_alpha;
	return col;
}
