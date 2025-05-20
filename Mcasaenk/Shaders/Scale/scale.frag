#version 430 core

out vec3 FragColor;
in vec2 pos;
in vec2 glpos;
layout(pixel_center_integer) in vec4 gl_FragCoord;

uniform sampler2D region0;
uniform float zoom;
uniform vec3 defcolor;

uniform ivec3 reg_regRect;
uniform int reg_isloading[400];
uniform int reg_isqueued[400];

uniform int CHUNKGRID, REGIONGRID, BACKGROUND, MAPGRID;
uniform bool OVERLAYS, UNLOADED;

float ctwh(float c){
    return abs(round(c) - c);
}

vec4 queuedpattern(vec2 uv, float scale, vec3 c1, vec3 c2){
    float thickness = 0.10;
    float length = 0.25;

    vec2 gridUV = uv * scale;
    vec2 cell = floor(gridUV);
    vec2 local = fract(gridUV) - 0.5;

    const float angle = -3.14 / 4;
    mat2 rot = mat2(cos(angle), -sin(angle), sin(angle), cos(angle));
    vec2 p = rot * local;

    float stripe = step(-thickness, p.y) * step(p.y, thickness) *
                   step(-length, p.x) * step(p.x, length);

    float diagIndex = mod(cell.x - cell.y, 2.0);
    vec4 color = diagIndex < 1.0 ? vec4(c1, 1) : vec4(c2, 1);

    return mix(vec4(0.0), color, stripe);
}


void main() {
    float insimzoom = zoom > 1 ? 1.0 : zoom;
    float outsimzoom = zoom < 1 ? 1.0 : zoom;
    vec2 npos = vec2(pos.x, 1 - pos.y);

    vec4 tc = texture(region0, npos);
    FragColor = tc.rgb;

    ivec2 glReg = ivec2(floor(glpos / 512));


    if(BACKGROUND == 1){
        vec3 gridcol;
        if(int(gl_FragCoord.y / 32) % 2 == int(gl_FragCoord.x / 32) % 2) gridcol = vec3(10) / 255;
        else gridcol = vec3(15, 15, 15) / 255;
        FragColor = mix(gridcol, FragColor.rgb, tc.a);
    }

    if(OVERLAYS || UNLOADED) {
        ivec2 rregpos = glReg - reg_regRect.xy;
        int indx = rregpos.y * reg_regRect.z + rregpos.x;
        if(indx < 400 * 32) {
            if(UNLOADED){
                if(((reg_isqueued[indx / 32] >> (indx % 32)) & 1) == 1){
                    vec4 r = queuedpattern(fract(glpos / 512), 4 * max(zoom, 1 / 2.0), vec3(173, 216, 230) / 255, vec3(90) / 255);
                    FragColor = mix(FragColor.rgb, r.rgb, r.a);
                }
            }
            if(OVERLAYS){
                if(((reg_isloading[indx / 32] >> (indx % 32)) & 1) == 1) {
                    vec2 ac = (abs(0.5 - fract(glpos / 512)) * 2);
                    float a = length(ac) / sqrt(2) * 0.40;
                    FragColor = mix(FragColor.rgb, vec3(200) / 255, a);
                }
            }
        }
    }

    if(CHUNKGRID == 1 && zoom >= 1) {
        if(ctwh(fract(glpos.x / 16)) <= 1 * ((1.0 / 16) / zoom) / 2 || ctwh(fract(glpos.y / 16)) <= 1 * ((1.0 / 16) / zoom) / 2){
            FragColor = vec3(0.5, 0.5, 0.5);
        }
    }

    if(MAPGRID > 0) {
        int mapsize = int(128 * pow(2, MAPGRID - 1));
        if(ctwh(fract((glpos.x - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2 || ctwh(fract((glpos.y - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2){
            FragColor = vec3(1, 1, 0);
        }
    }

    if(REGIONGRID == 1 || REGIONGRID == 2) {
        float dashBrPerReg = max(1, 8 * zoom);
        if(ctwh(fract(glpos.x / 512)) < 4 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.y / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec3(1, 1, 1);
        }
        if(ctwh(fract(glpos.y / 512)) < 4 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.x / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec3(1, 1, 1);
        }
    }

    
}