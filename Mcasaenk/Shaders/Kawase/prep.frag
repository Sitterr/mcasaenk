#version 430 core

in vec2 pos;

layout(location = 0) out vec2 outColor0;
layout(location = 1) out vec4 outColor1;
layout(location = 2) out vec4 outColor2;
layout(location = 3) out vec4 outColor3;
layout(location = 4) out vec4 outColor4;

uniform samplerBuffer palette;
uniform samplerBuffer tintpalette;

uniform isampler2DArray region0;

// global
int layers;
vec4 mult(vec4 v1, vec4 v2){
    return vec4(v1.r * v2.r, v1.g * v2.g, v1.b * v2.b, v1.a * v2.a);
}
vec4 blend(vec4 fg, vec4 bg) {
    float outAlpha = fg.a + bg.a * (1.0 - fg.a);
    
    // Prevent division by zero if outAlpha is 0
    vec3 outColor = vec3(0.0);
    if (outAlpha > 0.0) {
        outColor = (fg.rgb * fg.a + bg.rgb * bg.a * (1.0 - fg.a)) / outAlpha;
    }
    
    return vec4(outColor, outAlpha);
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
    block.basecolor.rgb = palettedata.abg;
    block.basecolor.a = (int(palettedata.r * 255) & 0x0F) / 15.0;
    block.tint = ((int(palettedata.r * 255) & 0xF0) >> 4);
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

int TerrHeight(RegionData d){
    return d.height - d.depth;
}

bool IsDepth(RegionData d, int l) { return l == layers - 1 && d.depth != 0; }

vec4 ActColor(RegionData d){
    return mult(d.block.basecolor, TintColorFor(d.block.tint, d.biomeid, d.height));
}
vec4 Color(RegionData d, int l) {
    if(IsDepth(d, l)) return mult(depth.basecolor, TintColorFor(depth.tint, d.biomeid, d.height));
    else return ActColor(d);
}
bool ContainsInfo(RegionData d) {
    return d.blockid != 0 || d.height != 0;
}
// reg




void main() {
    int layers = textureSize(region0, 0).z;
	ivec2 ipos = ivec2(pos * 512);
    RegionData wl = regionData(region0, layers - 1, ipos);

    outColor0 = vec2(wl.depth / 65535.0, wl.depth > 0 ? 1 : 0);
}