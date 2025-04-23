#version 430 core

in vec2 pos;
//layout(pixel_center_integer) in vec4 gl_FragCoord;
in vec4 gl_FragCoord;

layout(location = 0) out vec2 outColor0;
layout(location = 1) out vec4 outColor1;
layout(location = 2) out vec4 outColor2;
layout(location = 3) out vec4 outColor3;
layout(location = 4) out vec4 outColor4;


uniform sampler2DArray t_tints;
uniform sampler2D t_oceandepth;
uniform int ikernels[5];
uniform int ii;

vec2 excludeNulls(vec2 v) {
    if(v.g == 0) return vec2(0, 0);
    else return vec2(v.r / v.g, v.g);
}

void main() {
    vec2 FragCoord = gl_FragCoord.xy;
    ivec2 iFragCoord = ivec2(FragCoord);

    vec2 size = textureSize(t_oceandepth, 0).xy;

    for(int i = 0; i < 1; i++) {
        float d = ikernels[i];

        if(d == -1) {
            if(i == 0)      outColor0 = texelFetch(t_oceandepth, iFragCoord, 0).xy;
            else if(i == 1) outColor1 = texelFetch(t_tints, ivec3(FragCoord, 0), 0);
            else if(i == 2) outColor2 = texelFetch(t_tints, ivec3(FragCoord, 1), 0);
            else if(i == 3) outColor3 = texelFetch(t_tints, ivec3(FragCoord, 2), 0);
            else if(i == 4) outColor4 = texelFetch(t_tints, ivec3(FragCoord, 3), 0);

            continue;
        }

        d += 0.5 + ii;
        //d += 0.5;
        //vec2 s = FragCoord / size;

        vec2 s0 = (FragCoord + vec2(d, d)) / size;
        vec2 s1 = (FragCoord + vec2(d, -d)) / size;
        vec2 s2 = (FragCoord + vec2(-d, d)) / size;
        vec2 s3 = (FragCoord + vec2(-d, -d)) / size;

        if(i == 0) {
	        vec2 v0 = excludeNulls(textureLod(t_oceandepth, s0, 0).rg);
            vec2 v1 = excludeNulls(textureLod(t_oceandepth, s1, 0).rg);
            vec2 v2 = excludeNulls(textureLod(t_oceandepth, s2, 0).rg);
            vec2 v3 = excludeNulls(textureLod(t_oceandepth, s3, 0).rg);
            float sb = 0, br = 0;
            if(v0.g != 0) { sb += v0.r * v0.g; br += v0.g; }
            if(v1.g != 0) { sb += v1.r * v1.g; br += v1.g; }
            if(v2.g != 0) { sb += v2.r * v2.g; br += v2.g; }
            if(v3.g != 0) { sb += v3.r * v3.g; br += v3.g; }

            if(br == 0) outColor0 = vec2(0, 0);
            else outColor0 = vec2(sb / br, 1);

        } else {
        
        }
    }

}