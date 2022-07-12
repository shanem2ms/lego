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
SAMPLER2D(s_transmittance, 4);
SAMPLER3D(s_scatter, 5);
SAMPLER2D(s_irradiance, 6);

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

static const vec3 betaR = vec3(36.888, 85.86, 210.516);
static const float ISun = 70.0;
static const float fAtmosphereFog = 0;
static const float exposure = 0.4;
static const float EarthRadius = 6371000;
static const float Rt = EarthRadius + 60000;
static const float RtCalc = 60000;
static const int RES_MU = 128;
static const int RES_R = 32;
static const int RES_MU_S = 32;
static const int RES_NU = 8;

vec4 Texture4DAtlas(float r, float mu, float muS, float nu, float d, float s)
{
    float H = sqrt(Rt * Rt - 1);
    float rho = sqrt(r * r - 1);
    float rmu = r * mu;
    float delta = rmu * rmu - r * r + 1;
    vec4 cst = rmu < 0.0 && delta > 0.0 ? 
            vec4(1.0, 0.0, 0.0, 0.5 - 0.5 / float(RES_MU)) : 
            vec4(-1.0, H * H, H, 0.5 + 0.5 / float(RES_MU));
    float uR = 0.5 / float(RES_R) + rho / H * (1.0 - 1.0 / float(RES_R));
    float uMu = cst.w + (rmu * cst.x + sqrt(delta + cst.y)) / (rho + cst.z) * (0.5 - 1.0 / float(RES_MU));

    // better formula
    float uMuS = 0.5 / float(RES_MU_S) + 
            (atan(max(muS, -0.1975) * tan(1.26 * 1.1)) / 1.1 + (1.0 - 0.26)) * 0.5 * (1.0 - 1.0 / float(RES_MU_S));
    float lerp = (nu + 1.0) / 2.0 * (float(RES_NU) - 1.0);
    float uNu = floor(lerp);
    lerp = lerp - uNu;
    return texture3DLod(s_scatter, vec3((uNu + uMuS) / float(RES_NU), uMu, uR * d + s), 0) * (1.0 - lerp) +
           texture3DLod(s_scatter, vec3((uNu + uMuS + 1.0) / float(RES_NU), uMu, uR * d + s), 0) * lerp;
}

vec3 HDR(vec3 L) 
{
    L = L * exposure;
    L.r = L.r < 1.413 ? pow(L.r * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.r);
    L.g = L.g < 1.413 ? pow(L.g * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.g);
    L.b = L.b < 1.413 ? pow(L.b * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.b);
    return L;
}

vec3 IHDR(vec3 L, float pownum)
{
    L.r = L.r < 0.7566 ? pow(L.r, pownum) / 0.38317 : -log(1.00001 - L.r);
    L.g = L.g < 0.7566 ? pow(L.g, pownum) / 0.38317 : -log(1.00001 - L.g);
    L.b = L.b < 0.7566 ? pow(L.b, pownum) / 0.38317 : -log(1.00001 - L.b);
    return L / exposure;
}
// Rayleigh phase function
float PhaseFunctionR(float mu) 
{
    return (3.0 / (16.0 * M_PI)) * (1.0 + mu * mu);
}
// Mie phase function
float PhaseFunctionM2(float mu, float mieGG) 
{
    return 1.5 * 1.0 / (4.0 * M_PI) * (1.0 - mieGG*mieGG) * pow(1.0 + (mieGG*mieGG) - 2.0*mieGG*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + mieGG*mieGG);
}

// approximated single Mie scattering
vec3 GetMie(vec4 rayMie) 
{ // rayMie.rgb=C*, rayMie.w=Cm,r
    return rayMie.rgb * rayMie.w / max(rayMie.r, 1e-4) * (betaR.r / betaR);
}

vec2 GetTransmittanceUV(float r, float mu) 
{
    float uR, uMu;
    uR = sqrt((r - 1) / RtCalc);
    uMu = atan((mu + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
    return vec2(uMu, 1 - uR);
} 

vec3 TransmittanceAtlas(float r, float mu, float i) 
{
    vec2 uv = GetTransmittanceUV(r, mu);
    uv.y = saturate(1.0 - uv.y);
    return texture2DLod(s_transmittance, uv * vec2(1, 0.495) + vec2(0, i), 0.0f).rgb;
}

vec3 TransmittanceAtlas(float r, float mu, vec3 v, vec3 x0, float i) 
{
    vec3 result;
    float r1 = length(x0);
    float mu1 = dot(x0, v) / r;
    if (mu > 0.0) 
    {
        result = min(TransmittanceAtlas(r, mu, i) / TransmittanceAtlas(r1, mu1, i), 1.0);
    } 
    else 
    {
        result = min(TransmittanceAtlas(r1, -mu1, i) / TransmittanceAtlas(r, -mu, i), 1.0);
    }
    return result;
}


// Transmittance(=transparency) of atmosphere for infinite ray (r,mu)
// (mu=cos(view zenith angle)), or zero if ray intersects ground
vec3 TransmittanceWithShadow(float r, float mu, float i) 
{
    return mu < -sqrt(1.0 - (1 / r) * (1 / r)) ? vec3(0.0, 0.0, 0.0) : TransmittanceAtlas(r, mu, i);
}

vec2 GetIrradianceUV(float r, float muS) 
{
    float uR = (r - 1) / (Rt - 1);
    float uMuS = (muS + 0.2) / (1.0 + 0.2);
    return vec2(uMuS, uR);
}

vec3 IrradianceAtlas(float r, float muS, float i) 
{
    vec2 uv = GetIrradianceUV(r, muS);
    uv.y = saturate(uv.y);
    return texture2DLod(s_irradiance, uv * vec2(1, 0.5) + vec2(0, i), 0).rgb;
}

// Ground radiance at end of ray x+tv, when sun in direction s.
// Attenuated bewteen ground and viewer (=R[L0]+R[L*]).
vec4 GroundColor(
        vec3 inColor,
        vec3 x, 
        vec3 v, 
        vec3 s, 
        float r, 
        float mu, 
        float xp, 
        float x0l, 
        float zbuff, 
        vec3 sky, 
        vec3 sky2,
        float fogAmt)
{
    vec3 result;
    float daylight = 1;
    float d = xp;
    if ( zbuff < 1) 
    {
        // ground reflectance at end of ray, x0
        vec3 x0 = x + d * v;
        vec3 n = normalize(x0);
        vec3 reflectance = inColor;

        // direct sun light (radiance) reaching x0
        float muS = dot(n, s);
        vec3 sunLight = mix(
            TransmittanceWithShadow(1, muS, 0),
            TransmittanceWithShadow(1, muS, 0.5),
            fogAmt);

        // precomputed sky light (irradiance) (=E[L*]) at x0
        vec3 groundSkyLight = mix(
            IrradianceAtlas(1, muS, 0),
            IrradianceAtlas(1, muS, 0.5),
            fogAmt);

        // light reflected at x0 (=(R[L0]+R[L*])/T(x,x0))
        vec3 groundReflect = (max(muS, 0.0) * sunLight + groundSkyLight) * ISun / M_PI;
        vec3 groundColor = reflectance * max(groundReflect, 1);
        daylight = groundReflect.r;

        // attenuation of light to the viewer, T(x,x0)
        vec3 attenuation = mix(
            TransmittanceAtlas(r, mu, v, x0, 0),
            TransmittanceAtlas(r, mu, v, x0, 0.5),
            fogAmt);

        // water specular color due to sunLight
        /*
        if (reflectance.w > 0.0) 
        {
            vec3 h = normalize(s - v);
            float fresnel = 0.02 + 0.098 * pow(1.0 - dot(-v, h), 5.0);
            float waterBrdf = fresnel * pow(max(dot(h, n), 0.0), 25.0);
            groundColor += saturate(reflectance.w) * max(waterBrdf, 0.0) * sunLight * ISun;
        }
        */

        result = attenuation * groundColor; //=R[L0]+R[L*]
        if (x0l > 1.000001)
        {
            result -= attenuation * sky2 * ISun; 
        }
    } 
    else 
    { 
        // ray looking at the sky
        result = inColor.xyz * min(pow(3 - length(sky) / 3, 0.1), 1);
    }

    result += sky;
    return vec4(result, daylight);
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
    vec3 lightdir = vec3(-.1,1,-.5);
    normalize(lightdir);
    vec3 viewDir = normalize(u_eyePos - wpos.xyz);
    vec3 ld = lightdir;
	vec3 clight = vec3(1,1,1) * ao;

    vec3 s = vec3(0,-1,0);
    vec3 x = u_eyePos;
    vec3 v = (wpos.xyz - u_eyePos);
    float dist = length(v) * 1;
    v = normalize(v);
    s = normalize(s);
    float r, mu;
    vec3 result;
    r = abs(u_eyePos.y);
    mu = dot(x, v) / r;
    float d = -r * mu - sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    float muS = (dot(x, s)) / r;
    float nu = dot(v, s) * saturate(muS * 25);
    float phaseR = PhaseFunctionR(nu);
    float phaseM = PhaseFunctionM2(nu, 0.1);
    vec4 inscatter = max(
        mix(Texture4DAtlas(r, mu, abs(muS), nu, 0.5, 0),
            Texture4DAtlas(r, mu, abs(muS), nu, 0.5, 0.5), fAtmosphereFog) , 0.0);
    result = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM, 0.0);
    result *= ISun;

    vec3 result2 = vec3(0,0,0);
    if (dr < 1)
    {
        float nightblur = 1 - saturate (muS * 25);
        float r2, mu2;
        vec3 x2 = wpos;
        r2 = abs(x2.y);
        mu2 = dot (x2, v) / r2;
        float nu = dot (v, s) * saturate(muS * 25);
        float muS = (dot (x2, s)) / r2;
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = max( 
            mix(Texture4DAtlas(r2, mu2, abs(muS), nu, 0.5, 0), 
                Texture4DAtlas(r2, mu2, abs(muS), nu, 0.5, 0.5), fAtmosphereFog), 0.0);
        result2 = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM , 0.0);
    }

    float x0l = abs(wpos.y);
    vec4 outColorHDR = GroundColor(color, x, v, s, r, mu, dist, x0l, dr, result, result2, fAtmosphereFog);
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

    float inReflectivity = 0.13;
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
	gl_FragColor.xyz = mix(outColor,HDR(outColorHDR),0);
	gl_FragColor.w = 1.0;
} 


