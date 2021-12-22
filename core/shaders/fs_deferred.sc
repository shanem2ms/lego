$input v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

SAMPLER2D(s_depthtex, 0);
SAMPLER2D(s_gbuftex, 1);
uniform vec4 u_texelSize;
uniform mat4 u_deferredViewProj;
uniform vec4 u_eyePos;

vec3 unpackColor(float f) {
    vec3 color;
    color.b = floor(f / 256.0 / 256.0);
    color.g = floor((f - color.b * 256.0 * 256.0) / 256.0);
    color.r = floor(f - color.b * 256.0 * 256.0 - color.g * 256.0);
    // now we have a vec3 with the 3 components in range [0..255]. Let's normalize it!
    return color / 255.0;
}


void main()
{    
	vec4 gbuf = texture2DLod(s_gbuftex, v_texcoord0.xy, 0);
    float dr = texture2DLod(s_depthtex, v_texcoord0.xy, 0);
    const int sd = 5;
    float ao = 0;
    float nr = u_texelSize.z;
    float fr = u_texelSize.w;
    float zr = (fr * nr) / (-dr * fr + dr * nr + fr);

    //z = (fr * nr) / (-d * fr + d * nr + fr)
    for (int ix = -sd; ix <= sd; ++ix)
    {
        for (int iy = -sd; iy <= sd; ++iy)
        {
            float d = texture2DLod(s_depthtex, v_texcoord0.xy + (vec2(ix, iy) * u_texelSize.xy * 4), 0);
            float z = (fr * nr) / (-d * fr + d * nr + fr);
            ao += clamp(zr - z, 0, 1);
        }
    }
    ao = clamp(1 - ao / (sd * sd) * 1, 0, 1);
    vec4 spos; 
    spos.xy = v_texcoord0.xy * 2 - vec2(1,1);
    spos.y = -spos.y;
    spos.zw = vec2(zr, 1);    
    vec4 wpos = mul(u_deferredViewProj, spos);
    wpos = wpos / wpos.w;
    vec3 color = unpackColor(gbuf.z);
    vec3 normal = unpackColor(gbuf.r);
    normal = (normal * 2.0) - vec3(1,1,1);
    vec3 lightdir = vec3(.1,-1,.5);
    normalize(lightdir);
    float diff = clamp(dot(lightdir, normal), 0, 1);

    float specularStrength = 0.4;
    vec3 viewDir = normalize(u_eyePos - wpos.xyz);
    vec3 reflectDir = reflect(-lightdir, normal);  
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 15);
    vec3 specular = specularStrength * spec * 1;  

    gl_FragColor.rgb = color * ao * (diff * 0.5 + 0.5 + specular);
	gl_FragColor.a = 1;
} 


