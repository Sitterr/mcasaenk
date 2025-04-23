#version 430 core
out vec4 FragColor;
in vec2 pos;
layout(origin_upper_left) in vec4 gl_FragCoord;

uniform ivec2 resolution;
uniform float zoom;

const ivec2 regsize = ivec2(512, 512);

uniform samplerBuffer palette;
uniform samplerBuffer tintpalette;

uniform isampler2DArray region_aa;
uniform isampler2DArray region_ab;
uniform isampler2DArray region_ac;
uniform isampler2DArray region_ba;
uniform isampler2DArray region0;
uniform isampler2DArray region_bc;
uniform isampler2DArray region_ca;
uniform isampler2DArray region_cb;
uniform isampler2DArray region_cc;

uniform float coeff[128];

uniform int R;

struct BlockData{
    vec4 basecolor;
    int tint;
};
BlockData blockData(int id){
    vec4 palettedata = texelFetch(palette, id);

    BlockData block;
    block.basecolor.rgb = palettedata.abg;
    block.basecolor.a = (int(palettedata.r * 255) & 0x0F) / 15.0;
    block.tint = ((int(palettedata.r * 255) & 0xF0) >> 4);
    return block;
}
struct RegionData{
    int height, depth;
    int blockid, biomeid;
    float light, shade;
    bool lightfrombottom;
    BlockData block;
};
RegionData regionData(isampler2DArray region, int l, ivec2 pos) {
    ivec4 data = texelFetch(region, ivec3(l, pos), 0);

    RegionData regionData;
//regionData.height = data.r;
    regionData.depth = (data.g >> 1);
//    regionData.lightfrombottom = bool(data.g & 1);
//    regionData.blockid = data.b;
//    regionData.block = blockData(regionData.blockid);
//    int a = data.a;
//    regionData.biomeid = a >> 8;
//    regionData.light = ((a & 0x00F0) >> 4) / 15.0;
//    regionData.shade = (a & 0x000F) / 15.0;
    return regionData;
}

RegionData regionData(int l, ivec2 pos){
    pos = pos + regsize;

    ivec2 reg = pos / regsize;
    ivec2 relpos = pos % regsize;
    int ipos = l * 512 * 512 + relpos.y * 512 + relpos.x;


    if(reg == ivec2(1, 1)) return regionData(region0, l, relpos);

    if(reg == ivec2(0, 0)) return regionData(region_aa, l, relpos);
    if(reg == ivec2(0, 1)) return regionData(region_ab, l, relpos);
    if(reg == ivec2(0, 2)) return regionData(region_ac, l, relpos);

    if(reg == ivec2(1, 0)) return regionData(region_ba, l, relpos);
    if(reg == ivec2(1, 2)) return regionData(region_bc, l, relpos);

    if(reg == ivec2(2, 0)) return regionData(region_ca, l, relpos);
    if(reg == ivec2(2, 1)) return regionData(region_cb, l, relpos);
    if(reg == ivec2(2, 2)) return regionData(region_cc, l, relpos);
}

void main() {
    int gli = int(gl_FragCoord.y) * resolution.x + int(gl_FragCoord.x);
    ivec2 ipos = ivec2(pos * 512);

    float sum = 0, br = 0;
    for(int i = -R; i <= R; i++){
        RegionData d = regionData(0, ipos + ivec2(0, i));    
        if(d.depth > 0) {
//          float s = R / 3.0;
//          float coeffi = R > 0 ? 255 * (1 / sqrt(2 * 3.14 * s * s)) * pow(2.71, -(abs(i) * abs(i)) / (2 * s * s)) : 1;
            //float coeffi = 1;
            float coeffi = coeff[abs(i)];
            sum += d.depth * coeffi;
            br += coeffi;
        }
    }
    
    //firstpass.data[gli] = otg;
    int isum = int(round(sum)), ibr = int(round(br));
    FragColor = vec4((isum >> 0) & 0xFF, (isum >> 8) & 0xFF, (ibr >> 0) & 0xFF, (ibr >> 8) & 0xFF) / 255.0;
}