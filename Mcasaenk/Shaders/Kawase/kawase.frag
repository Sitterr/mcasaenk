#version 430 core

in vec2 pos;
//layout(pixel_center_integer) in vec4 gl_FragCoord;
in vec4 gl_FragCoord;

layout(location = 0) out vec2 outDepth;
layout(location = 1) out vec4 outTint0;
layout(location = 2) out vec4 outTint1;
layout(location = 3) out vec4 outTint2;
layout(location = 4) out vec4 outTint3;
layout(location = 5) out vec4 outTint4;
layout(location = 6) out vec4 outTint5;
layout(location = 7) out vec4 outTint6;
uniform int tintcount;


uniform sampler2DArray t_tints;
uniform sampler2D t_oceandepth;
uniform int ikernels[8];
uniform int ii;


void main() {
    vec2 FragCoord = gl_FragCoord.xy;
    ivec2 iFragCoord = ivec2(FragCoord);

    vec2 size = textureSize(t_oceandepth, 0).xy;

    for(int i = 0; i < 1 + tintcount; i++) {
        float d = ikernels[i];

        if(d == -1) {
            if(i == 0)      outDepth = texelFetch(t_oceandepth, iFragCoord, 0).xy;
            else if(i == 1) outTint0 = texelFetch(t_tints, ivec3(iFragCoord, 0), 0);
            else if(i == 2) outTint1 = texelFetch(t_tints, ivec3(iFragCoord, 1), 0);
            else if(i == 3) outTint2 = texelFetch(t_tints, ivec3(iFragCoord, 2), 0);
            else if(i == 4) outTint3 = texelFetch(t_tints, ivec3(iFragCoord, 3), 0);
            else if(i == 5) outTint4 = texelFetch(t_tints, ivec3(iFragCoord, 4), 0);
            else if(i == 6) outTint5 = texelFetch(t_tints, ivec3(iFragCoord, 5), 0);
            else if(i == 7) outTint6 = texelFetch(t_tints, ivec3(iFragCoord, 6), 0);

            continue;
        }

        d += 0.5;
        //d += 0.5;
        //vec2 s = FragCoord / size;

        vec2 s0 = (FragCoord + vec2(d, d)) / size;
        vec2 s1 = (FragCoord + vec2(d, -d)) / size;
        vec2 s2 = (FragCoord + vec2(-d, d)) / size;
        vec2 s3 = (FragCoord + vec2(-d, -d)) / size;

        if(i == 0) {
	        vec2 v0 = textureLod(t_oceandepth, s0, 0).rg;
            vec2 v1 = textureLod(t_oceandepth, s1, 0).rg;
            vec2 v2 = textureLod(t_oceandepth, s2, 0).rg;
            vec2 v3 = textureLod(t_oceandepth, s3, 0).rg;
            float sb = 0, br = 0;
            if(v0.g != 0) { sb += v0.r; br += v0.g; }
            if(v1.g != 0) { sb += v1.r; br += v1.g; }
            if(v2.g != 0) { sb += v2.r; br += v2.g; }
            if(v3.g != 0) { sb += v3.r; br += v3.g; }

            if(br == 0) outDepth = vec2(0, 0);
            else outDepth = vec2(sb / br, 1);

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


            if(i == 1) outTint0 = outColor;
            else if(i == 2) outTint1 = outColor;
            else if(i == 3) outTint2 = outColor;
            else if(i == 4) outTint3 = outColor;
            else if(i == 5) outTint4 = outColor;
            else if(i == 6) outTint5 = outColor;
            else if(i == 7) outTint6 = outColor;
        }
    }
}