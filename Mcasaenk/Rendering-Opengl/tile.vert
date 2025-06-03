#version 400 core

layout (location = 0) in vec3 position;
uniform float tv_zoom;
uniform ivec2 tv_glR;
uniform ivec2 tv_resolution;
uniform ivec2 tv_cam;
uniform ivec2 tv_regSize;

out vec2 pos;
out vec2 glpos;

void main() {
    pos = vec2((position.x + 1) / 2, 1 - (position.y + 1) / 2);
    glpos = (tv_glR + pos) * tv_regSize;

    float glX = (tv_glR.x + pos.x) * tv_regSize.x;
    float glZ = (tv_glR.y + pos.y) * tv_regSize.y;

    float Xg = -(tv_resolution.x - (glX - tv_cam.x) * tv_zoom * 2) / tv_resolution.x;
    float Yg = -(tv_resolution.y - (glZ - tv_cam.y) * tv_zoom * 2) / tv_resolution.y;

    gl_Position = vec4(Xg, -Yg, 0, 1); 
}