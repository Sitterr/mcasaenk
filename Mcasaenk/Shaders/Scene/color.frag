#version 430 core
out vec4 FragColor;
in vec2 pos;
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
uniform sampler2D blur_oceandepth;
uniform int blur_tintcount;
uniform int blur_blendtints[7];
uniform int blur_R;

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
// global



// palette
struct BlockData{
    vec4 basecolor;
    int tint;
};
BlockData blockData(int id){
    vec4 palettedata = texelFetch(palette, id);

    BlockData block;
    block.basecolor.rgb = contrast(palettedata.abg);
    block.basecolor.a = (int(palettedata.r * 255) & 0x0F) / 15.0;
    block.tint = ((int(palettedata.r * 255) & 0xF0) >> 4);
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
        regionData.depth = int(round(texelFetch(blur_oceandepth, blurCoord(pos), 0).r * 65535) * q + regionData.depth * (1 - q));
    }

    return regionData;
}

int TerrHeight(RegionData d){
    return d.height - d.depth;
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
RegionData irx, irnx, irz, irnz;


float staticShade(float fd) {
	int xdiff = 0;
    xdiff += TerrHeight(irx);
    xdiff -= TerrHeight(irnx);
    int zdiff = 0;
    zdiff += TerrHeight(irz);
    zdiff -= TerrHeight(irnz);

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
    

    irx = ipos.x < 511 ? regionData(region0, 0, ipos + ivec2( 1,  0)) : irs[layers - 1];
    irnx = ipos.x >= 1 ? regionData(region0, 0, ipos + ivec2(-1,  0)) : irs[layers - 1];
    irz = ipos.y < 511 ? regionData(region0, 0, ipos + ivec2( 0,  1)) : irs[layers - 1];
    irnz = ipos.y >= 1 ? regionData(region0, 0, ipos + ivec2( 0, -1)) : irs[layers - 1];

    depth = blockData(3); //depthid

    vec4 f = texelFetch(tintpalette, 16).abgr;
    biomecount = int(f.a * 255);
}
void main() {
    setup();

    float relvis[5];
    {
        float ostatuk = 1;
        for(int l = 0; l < layers - 1; l++) {
            if(ContainsInfo(irs[l]) == false) continue;

            float a = (ostatuk * (1 - pow(1 - irs[l].block.basecolor.a, max(1, irs[l].depth))));
            relvis[l] = a;
            ostatuk -= a;
        }
        {
            if(ContainsInfo(irs[layers - 1])) {
                float a = (ostatuk * (1 - pow(1 - irs[layers - 1].block.basecolor.a, max(1, irs[layers - 1].depth))));
                relvis[layers - 1] = a;
                ostatuk -= a;
            }
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
        if(STATIC_SHADE) {
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

    //FragColor = vec4(textureSize(region0, 0).z, 1, 0, 1);
    //if(FragColor.a > 0) FragColor.a = 1;
}