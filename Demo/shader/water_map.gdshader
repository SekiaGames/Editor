// ===== Demo 专用（水面）=====
// 和运行时那套（assets/shaders）分开：这里吃的是竖向拼接的图集（sampler2D + *_layers），
// 因为 Texture2DArray 无法序列化进场景，而 Demo 要求「不运行也能静态观察」。
// 由 tools 里那条 --export-scene 生成，改这里不影响编辑器渲染。

shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_always, cull_disabled, fog_disabled;

// 真实地图的水面：逐格四角水位 + 按水深查出来的颜色（顶点色），再乘一张波纹贴图。
// War3 的水面是透明的：浅滩几乎看不见，深水接近不透明，所以 alpha 也来自顶点色。
uniform sampler2D water_texture : filter_linear_mipmap, repeat_disable;

// 图集层数 = 波纹帧数；UV 原来靠 sampler 的 repeat，现在改成手动 fract
uniform float water_layers = 1.0;
uniform float frame = 0.0;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	// HiveWE 是 outColor = texture(...) * Color，也就是贴图的 alpha 也参与乘法。
	// 只乘 rgb 会让水面比参考更「实」，浅滩的覆盖范围大一圈。
	vec2 tiled = fract(UV);
	vec4 ripple = texture(water_texture, vec2(tiled.x, (tiled.y + frame) / water_layers));
	ALBEDO = srgb_to_linear(COLOR.rgb * ripple.rgb);
	ALPHA = COLOR.a * ripple.a;
}
