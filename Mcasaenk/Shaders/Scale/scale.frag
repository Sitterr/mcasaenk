#version 430 core

out vec4 FragColor;
in vec2 pos;
in vec2 glpos;
layout(pixel_center_integer) in vec4 gl_FragCoord;

uniform sampler2D region0;
uniform float zoom;

uniform vec4 screenshot;
uniform bool screenshot_resizable;
uniform vec3 screenshot_color;

uniform ivec3 reg_regRect;
uniform int reg_isloading[400];
uniform int reg_isqueued[400];

uniform int CHUNKGRID, REGIONGRID, BACKGROUND, MAPGRID;
uniform bool OVERLAYS, UNLOADED, USEMAPPALETTE;

float ctwh(float c){
    return abs(round(c) - c);
}

vec4 queuedpattern(vec2 uv, float scale, vec4 c1, vec4 c2){
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
    vec4 color = diagIndex < 1.0 ? c1 : c2;

    return mix(vec4(0.0), color, stripe);
}
bool pointInRect(vec2 point, vec4 rect) {
    return point.x >= rect.x && point.x <= rect.x + rect.z &&
           point.y >= rect.y && point.y <= rect.y + rect.w;
}
vec4 sqc(vec2 center, float size){
    return vec4(center - size / 2, vec2(size));
}

void main() {
    float insimzoom = zoom > 1 ? 1.0 : zoom;
    float outsimzoom = zoom < 1 ? 1.0 : zoom;
    vec2 npos = vec2(pos.x, 1 - pos.y);

    FragColor = texture(region0, npos);
    if(USEMAPPALETTE) {
        if(pointInRect(glpos, screenshot)) {
            FragColor = ;// todo
        }
    }

    ivec2 glReg = ivec2(floor(glpos / 512));


    if(BACKGROUND == 1){
        vec4 gridcol;
        if(int(gl_FragCoord.y / 32) % 2 == int(gl_FragCoord.x / 32) % 2) gridcol = vec4(10, 10, 10, 255) / 255;
        else gridcol = vec4(15, 15, 15, 255) / 255;
        FragColor = mix(gridcol, FragColor, FragColor.a);
    }

    if(OVERLAYS || UNLOADED) {
        ivec2 rregpos = glReg - reg_regRect.xy;
        int indx = rregpos.y * reg_regRect.z + rregpos.x;
        if(indx < 400 * 32) {
            if(UNLOADED){
                if(((reg_isqueued[indx / 32] >> (indx % 32)) & 1) == 1){
                    vec4 r = queuedpattern(fract(glpos / 512), 4 * max(zoom, 1 / 2.0), vec4(173, 216, 230, 255) / 255, vec4(90, 90, 90, 255) / 255);
                    FragColor = mix(FragColor, r, r.a);
                }
            }
            if(OVERLAYS){
                if(((reg_isloading[indx / 32] >> (indx % 32)) & 1) == 1) {
                    vec2 ac = (abs(0.5 - fract(glpos / 512)) * 2);
                    float a = length(ac) / sqrt(2) * 0.40;
                    FragColor = mix(FragColor, vec4(200, 200, 200, 255) / 255, a);
                }
            }
        }
    }

    if(CHUNKGRID == 1 && zoom >= 1) {
        if(ctwh(fract(glpos.x / 16)) <= 1 * ((1.0 / 16) / zoom) / 2 || ctwh(fract(glpos.y / 16)) <= 1 * ((1.0 / 16) / zoom) / 2){
            FragColor = vec4(0.5, 0.5, 0.5, 1);
        }
    }

    if(MAPGRID > 0) {
        int mapsize = int(128 * pow(2, MAPGRID - 1));
        if(ctwh(fract((glpos.x - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2 || ctwh(fract((glpos.y - mapsize / 2) / mapsize)) <= 1 * ((1.0 / mapsize) / zoom) / 2){
            FragColor = vec4(1, 1, 0, 1);
        }
    }

    if(REGIONGRID == 1 || REGIONGRID == 2) {
        float dashBrPerReg = max(1, 8 * zoom);
        if(ctwh(fract(glpos.x / 512)) < 4 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.y / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec4(1, 1, 1, 1);
        }
        if(ctwh(fract(glpos.y / 512)) < 4 * ((1.0 / 512) / zoom) / 2 && (int(floor(-0.5 + fract(glpos.x / 512) * dashBrPerReg * 2)) % 2 == 0 || REGIONGRID == 1)){
            FragColor = vec4(1, 1, 1, 1);
        }
    }

    
    if(screenshot != vec4(0)) {
        if(pointInRect(glpos, screenshot)){
            FragColor = mix(vec4(1, 1, 1, 1), FragColor, 0.75);
        }

        if(abs(glpos.x - screenshot.x) <= 2 / zoom                  && glpos.y >= screenshot.y && glpos.y <= screenshot.y + screenshot.w) {
            FragColor = vec4(screenshot_color, 1);
        }
        if(abs(glpos.x - (screenshot.x + screenshot.z)) <= 2 / zoom && glpos.y >= screenshot.y && glpos.y <= screenshot.y + screenshot.w) {
            FragColor = vec4(screenshot_color, 1);
        }
        if(abs(glpos.y - screenshot.y) <= 2 / zoom                  && glpos.x >= screenshot.x && glpos.x <= screenshot.x + screenshot.z) {
            FragColor = vec4(screenshot_color, 1);
        }
        if(abs(glpos.y - (screenshot.y + screenshot.w)) <= 2 / zoom && glpos.x >= screenshot.x && glpos.x <= screenshot.x + screenshot.z) {
            FragColor = vec4(screenshot_color, 1);
        }

        if(screenshot_resizable){
            float sqsize = (10 + zoom) / zoom;
            if(pointInRect(glpos, sqc(screenshot.xy, sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(0, screenshot.w), sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(screenshot.z, 0), sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(screenshot.z, screenshot.w), sqsize)) || 

               pointInRect(glpos, sqc(screenshot.xy + vec2(screenshot.z, screenshot.w / 2), sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(0, screenshot.w / 2), sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(screenshot.z / 2, 0), sqsize)) || 
               pointInRect(glpos, sqc(screenshot.xy + vec2(screenshot.z / 2, screenshot.w), sqsize))){
                FragColor = vec4(screenshot_color, 1);
            }
        }
    }

}