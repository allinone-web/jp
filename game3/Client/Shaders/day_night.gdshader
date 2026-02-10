shader_type canvas_item;

// ----- 黑夜遮罩 -----
uniform float darkness : hint_range(0.0, 1.0) = 0.0;
uniform float radius_in : hint_range(0.0, 1.0) = 0.25;
uniform float radius_out : hint_range(0.0, 1.0) = 0.65;
uniform float ground_darken : hint_range(0.0, 1.0) = 0.25;
// 黑夜遮罩：純黑，濃度/範圍由 darkness、radius_in/out、ground_darken 控制
uniform vec3 night_color : source_color = vec3(0.0, 0.0, 0.0);

// ----- 白天色彩濾鏡（夕陽/早晨等） -----
uniform vec3 tint_color : source_color = vec3(1.0, 1.0, 1.0);
uniform float tint_strength : hint_range(0.0, 1.0) = 0.0;
uniform float tint_radius_in : hint_range(0.0, 1.0) = 0.2;
uniform float tint_radius_out : hint_range(0.0, 1.0) = 0.7;

void fragment() {
	vec2 center = vec2(0.5, 0.5);
	float d = distance(UV, center);
	
	// 黑夜：徑向暗角 + 右下方較暗、左上方較亮（光源在右、陰影朝左）
	float night_mask = 1.0 - smoothstep(radius_in, radius_out, d);
	float t = (UV.x + (1.0 - UV.y)) * 0.5;
	float height_factor = 1.0 + ground_darken * t;
	float night_alpha = darkness * (1.0 - night_mask) * height_factor;
	
	// 白天：色彩濾鏡暗角（夕陽/早晨色調）
	float tint_mask = 1.0 - smoothstep(tint_radius_in, tint_radius_out, d);
	float tint_alpha = tint_strength * (1.0 - tint_mask);
	
	// 合併：黑夜用 night_color，白天用 tint；alpha 取兩者疊加
	vec3 out_color = mix(tint_color, night_color, min(1.0, darkness * 2.0));
	float out_alpha = clamp(night_alpha + tint_alpha * (1.0 - night_alpha), 0.0, 1.0);
	
	COLOR = vec4(out_color, out_alpha);
}
