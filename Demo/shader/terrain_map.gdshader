// ===== Demo 专用（地表）=====
// 和运行时那套（assets/shaders）分开：这里吃的是竖向拼接的图集（sampler2D + *_layers），
// 因为 Texture2DArray 无法序列化进场景，而 Demo 要求「不运行也能静态观察」。
// 由 tools 里那条 --export-scene 生成，改这里不影响编辑器渲染。

shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_back, fog_disabled;

// War3 地表：每个 tile 的四角各有一张地表贴图 + 一个变体，tile 内按遮罩叠加。
// 贴图是 32 层一组的 64x64 小块（每张地表 BLP 切成 32 个变体）竖向拼成一张图集，UV 在同一 tile 内是 0..1。
// CUSTOM0 = 四角的地表层号（a 为底层，b/g/r 依次往上盖），NORMAL = 角点法线。
uniform sampler2D ground_texture : filter_linear_mipmap, repeat_disable;

// 图集层数：贴图是 N 层 64x64 竖向拼成的一张图，层号换算成 uv.y 偏移
uniform float ground_layers = 1.0;

// 对应 HiveWE 的 map->render_lighting（Ribbon 的「光照」开关）
uniform bool lighting_enabled = true;

varying flat vec4 layer_index;

// HiveWE 的平行光方向：它那套坐标是 z 向上、取 (-0.3, -0.3, 0.25) 归一化，
// 换到 Godot 的 Y 向上就是下面这个。
const vec3 LIGHT_DIRECTION = vec3(-0.6095, 0.5079, -0.6095);

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	layer_index = CUSTOM0;
}

vec4 ground_sample(vec2 uv, float layer) {
	return texture(ground_texture, vec2(uv.x, (uv.y + layer) / ground_layers));
}

void fragment() {
	// 底层是「纯色块」（alpha=1），上面几层是「遮罩块」（alpha 是渐变遮罩）。
	// 所以要用**上层**的 alpha 做混合比例，而不是下层的——HiveWE 那个式子拿底层 alpha
	// 当比例，底层恒为 1，上面几层永远透不出来，格子之间就只剩硬边。
	vec4 color = ground_sample(UV, layer_index.x);
	vec4 layer1 = ground_sample(UV, layer_index.y);
	vec4 layer2 = ground_sample(UV, layer_index.z);
	vec4 layer3 = ground_sample(UV, layer_index.w);
	color.rgb = mix(color.rgb, layer1.rgb, layer1.a);
	color.rgb = mix(color.rgb, layer2.rgb, layer2.a);
	color.rgb = mix(color.rgb, layer3.rgb, layer3.a);

	// HiveWE 在片元里用插值后的法线算 clamp(dot(n, L) + 0.45)，再乘在 sRGB 数值上；
	// Godot 最后会做一次 linear→sRGB，所以这里先手动把 sRGB 转回线性。
	// 片元里的 NORMAL 是视图空间，转回世界空间再和世界空间的平行光点乘
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float shade = lighting_enabled ? clamp(dot(world_normal, LIGHT_DIRECTION) + 0.45, 0.0, 1.0) : 1.0;
	ALBEDO = srgb_to_linear(color.rgb * shade);
}
