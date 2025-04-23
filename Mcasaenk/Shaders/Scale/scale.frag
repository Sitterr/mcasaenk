#version 430 core

out vec4 FragColor;
in vec2 pos;

uniform sampler2D screenTexture;
uniform vec2 glPos;
uniform ivec2 size;
uniform float zoom;


float ctwh(float c){
    return abs(floor(c + 0.5) - c);
}

vec4 blend(vec3 c1, vec3 c2, float f){
    return vec4(c1 * f + c2 * (1 - f), 1.0);
}

void main() {
    float insimzoom = zoom > 1 ? 1.0 : zoom;
    float outsimzoom = zoom < 1 ? 1.0 : zoom;
    vec2 npos = vec2(pos.x, 1 - pos.y);

    FragColor = texture(screenTexture, pos);

    // background grid
    vec3 gridcol;
    if(int(((pos.y * size.y + fract(glPos).y - 1) * outsimzoom) / 32) % 2 == int(((npos.x * size.x - fract(glPos).x) * outsimzoom) / 32) % 2) gridcol = vec3(10.0 / 255, 10.0 / 255, 10.0 / 255);
    else gridcol = vec3(15.0 / 255, 15.0 / 255, 15.0 / 255);
    FragColor = blend(FragColor.rgb, gridcol, FragColor.a);

    vec2 glPos_ = glPos - fract(glPos);
    vec2 relPos = npos * size / insimzoom;

    vec2 totPos = glPos_ + relPos;

    // chunk grid
    if(zoom >= 1 && false) {
        if(ctwh(fract(totPos.x / 16)) <= 1 * ((1 / 16.0) / zoom) / 2 || ctwh(fract(totPos.y / 16)) <= 1 * ((1 / 16.0) / zoom) / 2){
            FragColor = vec4(0.5, 0.5, 0.5, 1.0);
        }
    }

    // region grid
    {
        float dashBrPerReg = max(1, 8 * zoom);
        if(ctwh(fract(totPos.x / 512)) <= 3 * ((1 / 512.0) / zoom) / 2 && int(floor(-0.5 + fract(totPos.y / 512) * dashBrPerReg * 2)) % 2 == 0){
            FragColor = vec4(1.0, 1.0, 1.0, 1.0);
        }
        if(ctwh(fract(totPos.y / 512)) <= 3 * ((1 / 512.0) / zoom) / 2 && int(floor(-0.5 + fract(totPos.x / 512) * dashBrPerReg * 2)) % 2 == 0){
            FragColor = vec4(1.0, 1.0, 1.0, 1.0);
        }
    }
    
}