#version 400 core

layout(location = 0) in vec3 position;
out vec2 pos;

void main() {
    pos = (position.xy + 1) / 2;
	gl_Position = vec4(position, 1.0);
}