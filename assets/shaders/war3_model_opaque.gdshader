shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque, shadows_disabled;

#include "res://assets/shaders/war3_model.gdshaderinc"

uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 col = war3_sample(UV);
	if (col.a < 0.004) {
		discard;
	}
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cutoff;
}
