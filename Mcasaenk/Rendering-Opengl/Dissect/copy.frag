#version 430 core

out vec4 FragColor;
in vec4 gl_FragCoord;

uniform ivec2 st;

uniform sampler2D tex;

void main() {
	FragColor = texelFetch(tex, ivec2(gl_FragCoord.xy) + st, 0);
}