shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_mix;

uniform sampler2D particle_texture : source_color, filter_linear_mipmap;
uniform vec3 color_start : source_color = vec3(1.0, 0.9, 0.3);
uniform vec3 color_middle : source_color = vec3(1.0, 0.3, 0.0);
uniform vec3 color_end : source_color = vec3(0.6, 0.0, 0.0);
uniform float alpha_start = 1.0;
uniform float alpha_middle = 1.0;
uniform float alpha_end = 0.0;
uniform float scale_start = 1.0;
uniform float scale_middle = 1.0;
uniform float scale_end = 0.2;
uniform vec2 atlas_columns_rows = vec2(1.0, 1.0);

uniform vec2 interval_first = vec2(0.0, 0.0);
uniform vec2 interval_middle = vec2(0.0, 0.0);
uniform float alpha_cutoff = 0.003;

uniform float tail_stretch = 1.0;

varying float v_life;

vec3 segment_color(float t) {
	return t < 0.5 ? mix(color_start, color_middle, t * 2.0)
	               : mix(color_middle, color_end, (t - 0.5) * 2.0);
}

float segment_alpha(float t) {
	return t < 0.5 ? mix(alpha_start, alpha_middle, t * 2.0)
	               : mix(alpha_middle, alpha_end, (t - 0.5) * 2.0);
}

float segment_scale(float t) {
	return t < 0.5 ? mix(scale_start, scale_middle, t * 2.0)
	               : mix(scale_middle, scale_end, (t - 0.5) * 2.0);
}

void vertex() {
	v_life = INSTANCE_CUSTOM.y;
	vec2 local = VERTEX.xy * segment_scale(v_life);

	if (tail_stretch > 1.0) {

		vec3 up_view = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
		vec2 dir = normalize(up_view.xy + vec2(0.0, 1e-5));
		vec2 perp = vec2(-dir.y, dir.x);
		local = perp * local.x + dir * local.y * tail_stretch;
	}
	VERTEX.xy = local;

	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
		INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	vec2 uv = UV;
	if (interval_middle.y > interval_middle.x || interval_first.y > interval_first.x) {
		float columns = max(1.0, atlas_columns_rows.x);
		float rows = max(1.0, atlas_columns_rows.y);
		bool second = v_life >= 0.5;
		float local = second ? (v_life - 0.5) * 2.0 : v_life * 2.0;
		float from = second ? interval_middle.x : interval_first.x;
		float to = second ? interval_middle.y : interval_first.y;
		float frame = floor(mix(from, to, clamp(local, 0.0, 1.0)));
		float column = mod(frame, columns);
		float row = floor(frame / columns);
		uv = (UV + vec2(column, row)) / vec2(columns, rows);
	}
	vec4 tex = texture(particle_texture, uv);
	float alpha = tex.a * segment_alpha(v_life);
	if (alpha < alpha_cutoff) {
		discard;
	}
	ALBEDO = tex.rgb * segment_color(v_life);
	ALPHA = alpha;
}
