#version 430 core

layout (location = 0) in vec3 position;
uniform float zoom;
uniform ivec2 glR;
uniform ivec2 resolution;
uniform ivec2 cam;
out vec2 pos;

void main() {
    pos = vec2((position.x + 1) / 2, 1 - (position.y + 1) / 2);

    float glX = (glR.x + pos.x) * 512.0;
    float glZ = (glR.y + pos.y) * 512.0;

    float Xg = -(resolution.x - (glX - cam.x) * zoom * 2) / resolution.x;
    float Yg = -(resolution.y - (glZ - cam.y) * zoom * 2) / resolution.y;

    gl_Position = vec4(Xg, -Yg, 0, 1); 
}