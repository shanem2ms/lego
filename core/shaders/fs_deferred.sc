$input v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include <bgfx_shader.sh>
#include "shaderlib.sh"
#include "uniforms.sh"

SAMPLER2D(s_depthtex, 0);
SAMPLER2D(s_gbuftex, 1);
SAMPLERCUBE(s_texCube, 2);
SAMPLERCUBE(s_texCubeIrr, 3);
uniform vec4 u_texelSize;
uniform mat4 u_deferredViewProj;
uniform vec4 u_eyePos;

vec3 calcFresnel(vec3 _cspec, float _dot, float _strength)
{
	return _cspec + (1.0 - _cspec)*pow(1.0 - _dot, 5.0) * _strength;
}

vec3 calcLambert(vec3 _cdiff, float _ndotl)
{
	return _cdiff*_ndotl;
}

vec3 calcBlinn(vec3 _cspec, float _ndoth, float _ndotl, float _specPwr)
{
	float norm = (_specPwr+8.0)*0.125;
	float brdf = pow(_ndoth, _specPwr)*_ndotl*norm;
	return _cspec*brdf;
}

float specPwr(float _gloss)
{
	return exp2(10.0*_gloss+2.0);
}

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
    vec3 shane = normal;
    normal = (normal * 2.0) - vec3(1,1,1);
    vec3 lightdir = vec3(-.1,1,-.5);
    normalize(lightdir);
    vec3 viewDir = normalize(u_eyePos - wpos.xyz);

    vec3 ld     = lightdir;
	vec3 clight = vec3(1,1,1) * ao;

	// Input.
	vec3 nn = normalize(normal);
	vec3 vv = viewDir;
	vec3 hh = normalize(vv + ld);

	float ndotv = clamp(dot(nn, vv), 0.0, 1.0);
	float ndotl = clamp(dot(nn, ld), 0.0, 1.0);
	float ndoth = clamp(dot(nn, hh), 0.0, 1.0);
	float hdotv = clamp(dot(hh, vv), 0.0, 1.0);
    vec3 vr = 2.0*ndotv*nn - vv; // Same as: -reflect(vv, nn);
	vec3 cubeR = vr;
	vec3 cubeN = nn;

    float inReflectivity = 0.43;
    vec3 inAlbedo = color;
    vec3 refl = mix(vec3_splat(0.04), inAlbedo, inReflectivity);
    vec3 albedo = inAlbedo * (1.0 - inReflectivity);

    float inGloss = 0.25;
	vec3 dirFresnel = calcFresnel(refl, hdotv, inGloss);
	vec3 envFresnel = calcFresnel(refl, ndotv, inGloss);

	vec3 lambert = calcLambert(albedo * (1.0 - dirFresnel), ndotl);
	vec3 blinn   = calcBlinn(dirFresnel, ndoth, ndotl, specPwr(inGloss));
	vec3 direct  = (lambert + blinn)*clight;

    float mip = 1;//1.0 + 5.0*(1.0 - inGloss); // Use mip levels [1..6] for radiance.
    vec3 radiance    = toLinear(textureCubeLod(s_texCube, cubeR, mip).xyz);
	vec3 irradiance  = toLinear(textureCube(s_texCubeIrr, cubeN).xyz);
    vec3 envDiffuse  = albedo     * irradiance;
	vec3 envSpecular = envFresnel * radiance;
	vec3 indirect    = envDiffuse + envSpecular;

	// Color.
	vec3 outColor = direct + indirect;
	outColor = outColor * exp2(1);
	gl_FragColor.xyz = outColor;
	gl_FragColor.w = 1.0;
} 


