shader_type spatial;
render_mode cull_disabled, diffuse_burley;

uniform vec3 low_color : source_color = vec3(0.176, 0.247, 0.129);
uniform vec3 high_color : source_color = vec3(0.365, 0.412, 0.220);
uniform vec3 rock_color : source_color = vec3(0.309, 0.290, 0.251);
uniform float cell = 128.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float h = world_pos.y;
	vec3 base = mix(low_color, high_color, clamp((h - 10.0) / 45.0, 0.0, 1.0));
	base = mix(base, rock_color, clamp((h - 55.0) / 35.0, 0.0, 1.0));

	vec2 p = world_pos.xz / cell;
	vec2 w = fwidth(p) * 1.4;
	vec2 g = abs(fract(p - 0.5) - 0.5) / max(w, vec2(0.0001));
	float line = 1.0 - min(min(g.x, g.y), 1.0);
	base = mix(base, base * 1.30 + vec3(0.03), line * 0.55);

	ALBEDO = base;
	ROUGHNESS = 0.95;
	SPECULAR = 0.03;
}
