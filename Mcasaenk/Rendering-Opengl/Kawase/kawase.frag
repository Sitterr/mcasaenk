#version 430 core

in vec2 pos;
in vec4 gl_FragCoord;

layout(location = 0) out uint outDepth;
layout(location = 1) out vec4 outTint0;
layout(location = 2) out vec4 outTint1;
layout(location = 3) out vec4 outTint2;
layout(location = 4) out vec4 outTint3;
layout(location = 5) out vec4 outTint4;
layout(location = 6) out vec4 outTint5;
layout(location = 7) out vec4 outTint6;
vec4 outTints[7];
uniform int tintcount;


uniform sampler2DArray t_tints;
uniform usampler2D t_meanheight_oceandepth;
uniform int ikernels[8];
uniform int ii;


void main() {
    vec2 FragCoord = gl_FragCoord.xy;
    ivec2 iFragCoord = ivec2(FragCoord);

    vec2 size = textureSize(t_meanheight_oceandepth, 0).xy;

    for(int i = 0; i < 1 + tintcount; i++) {
        float d = ikernels[i];

        if(d == -1) {
            if(i == 0) outDepth = texelFetch(t_meanheight_oceandepth, iFragCoord, 0).x;
            else outTints[i - 1] = texelFetch(t_tints, ivec3(iFragCoord, i - 1), 0);
            continue;
        }

        d += 0.5;

        vec2 s0 = (FragCoord + vec2(d, d)) / size;
        vec2 s1 = (FragCoord + vec2(d, -d)) / size;
        vec2 s2 = (FragCoord + vec2(-d, d)) / size;
        vec2 s3 = (FragCoord + vec2(-d, -d)) / size;

        if(i == 0) {
            uvec4 v[4];
            v[0] = textureGather(t_meanheight_oceandepth, s0, 0);
            v[1] = textureGather(t_meanheight_oceandepth, s1, 0);
            v[2] = textureGather(t_meanheight_oceandepth, s2, 0);
            v[3] = textureGather(t_meanheight_oceandepth, s3, 0);

            uint sb = 0, br = 0;
            for(int j=0;j<4;j++){
                for(int k=0;k<4;k++){
                    uint b = (v[j][k] >> 16) & 1;
                    if(b == 1){
                        sb += (v[j][k] >> 17) * 1;
                        br += 1;
                    }
                }
            }

            uint v0 = texelFetch(t_meanheight_oceandepth, iFragCoord, 0).x;

            if(br == 0) outDepth = ((((sb / br) << 1) + 0) << 16) + (v0 & 0xFFFF);
            else        outDepth = ((((sb / br) << 1) + 1) << 16) + (v0 & 0xFFFF);

        } else {
        	vec4 v0 = textureLod(t_tints, vec3(s0, i - 1), 0);
            vec4 v1 = textureLod(t_tints, vec3(s1, i - 1), 0);
            vec4 v2 = textureLod(t_tints, vec3(s2, i - 1), 0);
            vec4 v3 = textureLod(t_tints, vec3(s3, i - 1), 0);
            vec3 sb = vec3(0);
            float br = 0;
            if(v0.a != 0) { sb += v0.rgb; br += v0.a; }
            if(v1.a != 0) { sb += v1.rgb; br += v1.a; }
            if(v2.a != 0) { sb += v2.rgb; br += v2.a; }
            if(v3.a != 0) { sb += v3.rgb; br += v3.a; }

            vec4 outColor = vec4(0);
            if(br > 0) outColor = vec4(sb / br, 1);

            outTints[i - 1] = outColor;
        }
    }

    outTint0 = outTints[0];
    outTint1 = outTints[1];
    outTint2 = outTints[2];
    outTint3 = outTints[3];
    outTint4 = outTints[4];
    outTint5 = outTints[5];
    outTint6 = outTints[6];
}