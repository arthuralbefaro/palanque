// Tingimento das camadas do retrato (GDD 7.4): a arte é em tons de cinza e a cor vem por multiplicação.
shader_type canvas_item;

uniform vec4 tint : source_color = vec4(1.0);
uniform float alpha = 1.0;

void fragment() {
	vec4 base = texture(TEXTURE, UV);
	COLOR = vec4(base.rgb * tint.rgb, base.a * alpha);
}
