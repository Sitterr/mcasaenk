#version 430 core
out vec4 FragColor;
in vec2 pos;
in vec2 glpos;
layout(pixel_center_integer) in vec4 gl_FragCoord;

uniform ivec2 tv_resolution;
uniform ivec2 tv_glR;
uniform ivec2 tv_cam;
uniform float tv_zoom;

uniform isampler2DArray region0;

uniform samplerBuffer palette;
uniform samplerBuffer tintpalette;

uniform float CONTRAST, SUN_LIGHT, BLOCK_LIGHT, WATER_TRANSPARENCY, ADEG;
uniform bool WATER_SMART_SHADE, SHADE3D, STATIC_SHADE;

uniform sampler2DArray blur_tintcolors;
uniform usampler2D blur_meanheight_oceandepth;
uniform int blur_tintcount;
uniform int blur_blendtints[7];
uniform int blur_R;

uniform int MAPAPPROXIMATIONALGO;
uniform vec4 map_screenshot;
uniform uint map_screenshot_mapcolors[64];

// global
int layers;
float depthQ(int depth) {
    const float c = 5, r = 8;

    float q = 1;
    if(depth <= c) q = 0;
    else if(depth >= c + r) q = 1;
    else q = pow(((depth - c) / r), 2);

    return q;
}
ivec2 blurCoord(ivec2 pos){
    float inszoom = tv_zoom > 1 ? 1 : tv_zoom;
    ivec2 gl = tv_glR * 512 + pos;
    ivec2 loc = gl - tv_cam + 512;

    return ivec2(vec2(loc.x, (tv_resolution.y / inszoom + 2 * 512) - loc.y - 1) * inszoom);
}
vec3 contrast(vec3 color) {
    const float z = 0.00;
    float alpha = CONTRAST * z + (1 - z / 2);
    return clamp(alpha * (color - 0.5) + 0.5, 0.0, 1.0);
}
bool pointInRect(vec2 point, vec4 rect) {
    return point.x >= rect.x && point.x <= rect.x + rect.z &&
           point.y >= rect.y && point.y <= rect.y + rect.w;
}
// global

// map palette
vec4 unpackARGB(uint argb) {
    float a = float((argb >> 24) & 0xFF) / 255.0;
    float r = float((argb >> 16) & 0xFF) / 255.0;
    float g = float((argb >> 8) & 0xFF) / 255.0;
    float b = float(argb & 0xFF) / 255.0;
    return vec4(r, g, b, a);
}
vec3 rgb2lab(vec3 c) {
    float rNorm = (c.r > 0.04045) ? pow((c.r + 0.055) / 1.055, 2.4) : (c.r / 12.92);
    float gNorm = (c.g > 0.04045) ? pow((c.g + 0.055) / 1.055, 2.4) : (c.g / 12.92);
    float bNorm = (c.b > 0.04045) ? pow((c.b + 0.055) / 1.055, 2.4) : (c.b / 12.92);

    float x = rNorm * 0.4124 + gNorm * 0.3576 + bNorm * 0.1805;
    float y = rNorm * 0.2126 + gNorm * 0.7152 + bNorm * 0.0722;
    float z = rNorm * 0.0193 + gNorm * 0.1192 + bNorm * 0.9505;

    x /= 0.95047;
    y /= 1.00000;
    z /= 1.08883;

    x = (x > 0.008856) ? pow(x, 1 / 3.0) : (7.787 * x + 16.0 / 116.0);
    y = (y > 0.008856) ? pow(y, 1 / 3.0) : (7.787 * y + 16.0 / 116.0);
    z = (z > 0.008856) ? pow(z, 1 / 3.0) : (7.787 * z + 16.0 / 116.0);

    return vec3(
        (116.0 * y) - 16.0,
        500.0 * (x - y),
        200.0 * (y - z)
    );
}
float cie94(vec3 lab1, vec3 lab2) {
    const float kL = 0.298;
    const float kC = 0.317;
    const float kH = 0.385;

    const float k1 = 0.045;
    const float k2 = 0.015;
    
    float l1 = lab1.x, a1 = lab1.y, b1 = lab1.z;
    float l2 = lab2.x, a2 = lab2.y, b2 = lab2.z;
    
    float deltaL = l1 - l2;
    float deltaA = a1 - a2;
    float deltaB = b1 - b2;
    
    float c1 = sqrt(a1*a1 + b1*b1);
    float c2 = sqrt(a2*a2 + b2*b2);
    float deltaC = c1 - c2;
    
    float deltaH_squared = deltaA*deltaA + deltaB*deltaB - deltaC*deltaC;
    float deltaH = sqrt(max(0.0, deltaH_squared));
    
    float sL = 1.0;
    float sC = 1.0 + k1 * c1;
    float sH = 1.0 + k2 * c1;
    
    float lightness_term = deltaL / (kL * sL);
    float chroma_term = deltaC / (kC * sC);
    float hue_term = deltaH / (kH * sH);
    
    return sqrt(lightness_term*lightness_term + 
                chroma_term*chroma_term + 
                hue_term*hue_term);
}
float colorDiff(vec3 c1, vec3 c2) {
    if(MAPAPPROXIMATIONALGO == 0) return length(c1 - c2);
    if(MAPAPPROXIMATIONALGO == 1) return length(rgb2lab(c1) - rgb2lab(c2));
    if(MAPAPPROXIMATIONALGO == 2) return cie94(rgb2lab(c1), rgb2lab(c2));
}
// map palette


// palette
struct BlockData{
    vec4 basecolor;
    int tint;
    bool noshade;
};
BlockData blockData(int id){
    vec4 palettedata = texelFetch(palette, id);

    BlockData block;
    block.basecolor.rgb = palettedata.abg;
    int r = int(palettedata.r * 255);
    block.noshade = (r & 1) == 1;
    block.basecolor.a = ((r >> 1) & 7) / 7.0;
    block.tint = ((r & 0xF0) >> 4);
    return block;
}
BlockData depth;
int biomecount;
vec4 TintColorFor(ivec2 pos, int tint, int biome, int height) {
    for(int i=0;i<blur_tintcount;i++){
        if(tint == blur_blendtints[i]){
            vec4 c = texelFetch(blur_tintcolors, ivec3(blurCoord(pos), i), 0);
            c.a = 1;
            return c; 
        }
    }

    {
        vec4 hdata = texelFetch(tintpalette, tint);
        int offset = (int(hdata.a * 255) << 16) + (int(hdata.b * 255) << 8) + (int(hdata.g * 255));
        int type = int(hdata.r * 255) >> 4;

        if(type == 1) {
            return texelFetch(tintpalette, offset).bgra;
        } else if(type == 2){
            return texelFetch(tintpalette, offset + biome).bgra;
        } else if(type == 3){
            return texelFetch(tintpalette, offset + height).bgra;
        } else if(type == 4){
            return texelFetch(tintpalette, offset + height * biomecount + biome).bgra;
        } else return vec4(1, 1, 1, 1);

    }
}
// palette


// reg
struct RegionData{
    int height, depth;
    int blockid, biomeid;
    float light, shade;
    bool lightfrombottom;
    BlockData block;
};
RegionData regionData(isampler2DArray region, int l, ivec2 pos) {
    ivec4 data = texelFetch(region, ivec3(pos, l), 0);

    RegionData regionData;
    regionData.height = data.r;
    regionData.depth = (data.g >> 1);
    regionData.lightfrombottom = bool(data.g & 1);
    regionData.blockid = data.b;
    regionData.block = blockData(regionData.blockid);
    int a = data.a;
    regionData.biomeid = a >> 8;
    regionData.light = ((a & 0x00F0) >> 4) / 15.0;
    regionData.shade = (a & 0x000F) / 15.0;

    if(l == layers - 1) {
        float q = depthQ(regionData.depth);
        uint dv = (texelFetch(blur_meanheight_oceandepth, blurCoord(pos), 0).r >> 16) >> 1;
        regionData.depth = int(dv * q + regionData.depth * (1 - q));
    }

    return regionData;
}

bool IsDepth(RegionData d, int l) { return l == layers - 1 && d.depth != 0; }

vec4 ActColor(RegionData d, ivec2 pos){
    return d.block.basecolor * TintColorFor(pos, d.block.tint, d.biomeid, d.height);
}
vec4 Color(RegionData d, int l, ivec2 pos) {
    if(IsDepth(d, l)) return depth.basecolor * TintColorFor(pos, depth.tint, d.biomeid, d.height);
    else return ActColor(d, pos);
}
bool ContainsInfo(RegionData d) {
    return d.blockid != 0 || d.height != 0;
}
// reg



ivec2 ipos;
RegionData irs[5];
int irx, irnx, irz, irnz;

int meanheight(ivec2 pos){
    return int((texelFetch(blur_meanheight_oceandepth, blurCoord(pos), 0).r & 0xFFFF));
}

float staticShade(float fd) {
	int xdiff = 0;
    xdiff += irx;
    xdiff -= irnx;
    int zdiff = 0;
    zdiff += irz;
    zdiff -= irnz;

    float shade = clamp(-(cos(radians(ADEG)) * xdiff + -sin(radians(ADEG)) * zdiff), -5, 5);

    if(SHADE3D) return (shade * 8 * CONTRAST * fd) / 255.0;
    else return (shade * 16 * CONTRAST * fd) / 255.0;
}

void setup(){
    layers = textureSize(region0, 0).z;
	ipos = ivec2(pos * 512);

    for(int l=0;l<layers;l++) {
        irs[l] = regionData(region0, l, ipos);
    }
    
    if(STATIC_SHADE){
        irx = meanheight(ipos + ivec2( 1,  0));
        irnx = meanheight(ipos + ivec2(-1,  0));
        irz = meanheight(ipos + ivec2( 0,  1));
        irnz = meanheight(ipos + ivec2( 0, -1));
    }

    depth = blockData(3); //depthid

    vec4 f = texelFetch(tintpalette, 16).abgr;
    biomecount = int(f.a * 255);
}
void main() {
    setup();

    float relvis[5];
    {
        float ostatuk = 1;
        for(int l = 0; l < layers; l++) {
            if(ContainsInfo(irs[l]) == false) continue;

            float a = (ostatuk * (1 - pow(1 - irs[l].block.basecolor.a, max(1, irs[l].depth))));
            relvis[l] = a;
            ostatuk -= a;
        }
        for(int l = 0; l < layers; l++) {
            relvis[l] += ((relvis[l] / (1 - ostatuk)) * ostatuk);
        }
    }

    for(int l = 0; l < layers; l++){
        RegionData ir = irs[l];
        if(ContainsInfo(ir) == false) continue;

        vec4 color = vec4(Color(ir, l, ipos).rgb, 1);

        float fd = 1;
        // water
        {
            if(IsDepth(ir, l)){
                vec4 terrainColor = ActColor(ir, ipos);
                int waterDepth = ir.depth;
                if(waterDepth > ir.height) {
                    terrainColor = color;
                }

                fd = pow(2, -4 * (1 - pow(WATER_TRANSPARENCY, 0.1)) * (waterDepth + 3));
                color = terrainColor * fd + color * (1 - fd);

                float multintensity = pow(fd, 0.75) * CONTRAST + 1 * (1 - CONTRAST);
                color = vec4(color.rgb * multintensity, color.a);  
            }
        }

        // static shades
        if(STATIC_SHADE && ir.block.noshade == false) {
            float stShade = staticShade(fd);
            color = color + vec4(stShade, stShade, stShade, 0);
        }

        // shadows & light
        {
            float sh = 0;

            sh += SUN_LIGHT;

            if(ir.shade > 0){
                sh = clamp(sh * max((1 - (CONTRAST * ir.shade * (WATER_SMART_SHADE ? fd : 1))), ((color.r + color.g + color.b) / 3 - (CONTRAST * 150)) / 255), 0, 1);
            }

            float _fd = ir.lightfrombottom ? fd : 1;
            sh = clamp(sh + clamp((ir.light - 1) / _fd + BLOCK_LIGHT, 0, 1), 0, 1);

            color = vec4(color.rgb * sh, color.a);
        }


        //if(color.a > 0) color.a = 1;
        FragColor += color * relvis[l];
    }


    if(map_screenshot.z > 0 && map_screenshot.w > 0) {
        if(pointInRect(glpos, map_screenshot)) {
            float closest = 10000.0;
            vec3 closestCol = vec3(0);
            const int mults[] = {255, 220, 180, 135};
            for(int i=0;i<64;i++) { 
                vec4 mapcol = unpackARGB(map_screenshot_mapcolors[i]);
                if(mapcol.a == 0) continue;
                for(int k=0;k<mults.length;k++) {
                    vec3 mapcol_ = mapcol.rgb * (float(mults[k]) / 255.0);
                    float res = colorDiff(FragColor.rgb, mapcol_);
                    if(res < closest) {
                        closest = res;
                        closestCol = mapcol_;
                    }
                }
            }

            FragColor = vec4(closestCol, FragColor.a);
        }
    }

}