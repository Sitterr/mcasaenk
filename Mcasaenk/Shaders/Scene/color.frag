#version 430 core
out vec4 FragColor;
in vec2 pos;
layout(pixel_center_integer) in vec4 gl_FragCoord;

uniform ivec2 resolution;
uniform float zoom;

uniform isampler2DArray region0;

uniform samplerBuffer palette;
uniform samplerBuffer tintpalette;

uniform float CONTRAST, SUN_LIGHT, BLOCK_LIGHT, WATER_TRANSPARENCY, ADEG;
uniform bool WATER_SMART_SHADE, SHADE3D, STATIC_SHADE;

uniform sampler2D blurdata;
uniform int R;
uniform float coeff[128];

uniform sampler2D oceandepth;

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

        vec4 color = vec4(Color(ir, l).rgb, 1);

        float fd = 1;
        // water
        {
            if(IsDepth(ir, l)){
                vec4 terrainColor = ActColor(ir);
                int waterDepth = ir.depth;
                if(waterDepth > ir.height) {
                    terrainColor = color;
                }

                // kawase
                {
                    float q = 1;
                    if(waterDepth < 8) q = 0;
                    else if(waterDepth > 18) q = 1;
                    else q = (waterDepth - 8) / 10.0;

                    waterDepth = int(texelFetch(oceandepth, ivec2(gl_FragCoord.xy) + ivec2(512, 512), 0).r * 65535 * q + waterDepth * (1 - q));
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