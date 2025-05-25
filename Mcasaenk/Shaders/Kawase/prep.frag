#version 430 core

in vec2 pos;

layout(location = 0) out uint outMeanheights_Oceandepth;
layout(location = 1) out vec4 outTint0;
layout(location = 2) out vec4 outTint1;
layout(location = 3) out vec4 outTint2;
layout(location = 4) out vec4 outTint3;
layout(location = 5) out vec4 outTint4;
layout(location = 6) out vec4 outTint5;
layout(location = 7) out vec4 outTint6;
uniform int tintcount;
uniform int blendtints[7];

uniform samplerBuffer palette;
uniform samplerBuffer tintpalette;

uniform isampler2DArray region0;

// global
int layers;
// global

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
vec4 TintColorFor(int tint, int biome, int height){
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
    return regionData;
}
bool IsDepth(RegionData d, int l) { return l == layers - 1 && d.depth != 0; }
int TerrHeight(RegionData d, int l){
    if(IsDepth(d, l)) return d.height - d.depth;
    return d.height + d.depth / 2;
}
// reg

RegionData irs[5];
ivec2 ipos;
void setup(){
    layers = textureSize(region0, 0).z;
    ipos = ivec2(pos * 512);
    for(int l=0;l<layers;l++) {
        irs[l] = regionData(region0, l, ipos);
    }

    vec4 f = texelFetch(tintpalette, 16).abgr;
    biomecount = int(f.a * 255);
}

void main() {
    setup();

    outTint0 = vec4(0);
    outTint1 = vec4(0);
    outTint2 = vec4(0);
    outTint3 = vec4(0);
    outTint4 = vec4(0);
    outTint5 = vec4(0);
    outTint6 = vec4(0);

    float relvis[5];
    float ostatuk = 1;
    for(int l=0;l<layers;l++){
        if(irs[l].block.noshade == false) {
            float a = (ostatuk * (1 - pow(1 - irs[l].block.basecolor.a, max(1, irs[l].depth))));
            relvis[l] = a;
            ostatuk -= a;
        }

        RegionData wl = irs[l];
        if(IsDepth(wl, l)) wl.block = blockData(3);
        else wl.block = blockData(wl.blockid);

             if(wl.block.tint == blendtints[0] && tintcount > 0) outTint0 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1);
        else if(wl.block.tint == blendtints[1] && tintcount > 1) outTint1 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1);
        else if(wl.block.tint == blendtints[2] && tintcount > 2) outTint2 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1); 
        else if(wl.block.tint == blendtints[3] && tintcount > 3) outTint3 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1); 
        else if(wl.block.tint == blendtints[4] && tintcount > 4) outTint4 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1); 
        else if(wl.block.tint == blendtints[5] && tintcount > 5) outTint5 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1); 
        else if(wl.block.tint == blendtints[6] && tintcount > 6) outTint6 += vec4(TintColorFor(wl.block.tint, wl.biomeid, wl.height).rgb, 1); 
    }
    if(ostatuk < 1){
        for(int l = 0; l < layers; l++) {
            relvis[l] += ((relvis[l] / (1 - ostatuk)) * ostatuk);
        }
    } else {
        relvis[layers - 1] = 1;
    }
    float mh = 0;
    for(int l = 0; l < layers; l++) {
        mh += relvis[l] * TerrHeight(irs[l], l);
    }
    outMeanheights_Oceandepth = (((irs[layers - 1].depth << 1) + (irs[layers - 1].depth > 0 ? 1 : 0)) << 16) | uint(mh);


    if(outTint0.a > 0) outTint0 = vec4(outTint0.rgb / outTint0.a, 1);
    if(outTint1.a > 0) outTint1 = vec4(outTint1.rgb / outTint1.a, 1);
    if(outTint2.a > 0) outTint2 = vec4(outTint2.rgb / outTint2.a, 1);
    if(outTint3.a > 0) outTint3 = vec4(outTint3.rgb / outTint3.a, 1);
    if(outTint4.a > 0) outTint4 = vec4(outTint4.rgb / outTint4.a, 1);
    if(outTint5.a > 0) outTint5 = vec4(outTint5.rgb / outTint5.a, 1);
    if(outTint6.a > 0) outTint6 = vec4(outTint6.rgb / outTint6.a, 1);

}