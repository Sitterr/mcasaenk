#version 430 core

out vec3 FragColor;
in vec2 pos;
in vec2 glpos;
layout(pixel_center_integer) in vec4 gl_FragCoord;

uniform sampler2D region0;
uniform float zoom;
uniform vec3 defcolor;

uniform int CHUNKGRID, REGIONGRID, BACKGROUND, MAPGRID;

float ctwh(float c){
    return abs(floor(c + 0.5) - c);
}

vec3 blend(vec3 c1, vec3 c2, float f){
    return vec3(c1 * f + c2 * (1 - f));
}

void main() {
    float insimzoom = zoom > 1 ? 1.0 : zoom;
    float outsimzoom = zoom < 1 ? 1.0 : zoom;
    vec2 npos = vec2(pos.x, 1 - pos.y);

    vec4 tc = texture(region0, npos);
    FragColor = tc.rgb;




    if(BACKGROUND == 1){
        vec3 gridcol;
        if(int(gl_FragCoord.y / 32) % 2 == int(gl_FragCoord.x / 32) % 2) gridcol = vec3(10.0 / 255, 10.0 / 255, 10.0 / 255);
        else gridcol = vec3(15.0 / 255, 15.0 / 255, 15.0 / 255);
        FragColor = blend(FragColor.rgb, gridcol, tc.a);
    }

    if(CHUNKGRID == 1 && zoom >= 1) {
        if(ctwh(fract(glpos.x / 16)) <= 1 * ((1.0 / 16) / zoom) / 2 || ctwh(fract(glpos.y / 16)) <= 1 * ((1.0 / 16) / zoom) / 2){
            FragColor = vec3(0.5, 0.5, 0.5);
        }
    }

    if(REGIONGRID == 1 || REGIONGRID == 2) {
        float dashBrPerReg = max(1, 8 * zoom);
        if(ctwh(fract(glpos.x / 512)) <= 3 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.y / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec3(1, 1, 1);
        }
        if(ctwh(fract(glpos.y / 512)) <= 3 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.x / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec3(1, 1, 1);
        }
    }

    if(MAPGRID > 0) {
        int mapsize = int(128 * pow(2, MAPGRID - 1));
        if(ctwh(fract((glpos.x - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2 || ctwh(fract((glpos.y - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2){
            FragColor = vec3(1, 1, 0);
        }
    }
    
}