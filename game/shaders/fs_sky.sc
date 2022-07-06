$input v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>
SAMPLER2D(s_depthtex, 0);

void main()
{
    vec4 outColor;
    vec4 inViewPos;
    inViewPos.xy = (v_texcoord0.xy - vec2(0.5, 0.5)) * vec2(2, -2);
    vec4 inTexBias = vec4(v_texcoord0.xy,0,0);
    float fZBufferDepth = texture2DLod(s_depthtex, v_texcoord0.xy, 0);
        
    inViewPos.z = fZBufferDepth; 
    inViewPos.w = 1;
    fZBufferDepth = 1 - fZBufferDepth;

    vec4 worldPos = mul(inViewPos, matViewProjection);
    worldPos /= worldPos.w;
    worldPos *= fAtmosphereAmount;
    worldPos.xyz += fvWsEyePosition;
    float x0l = length(worldPos.xyz);

    // This prevents the atmosphere from going insane when atmosphereAmount
    // is turned down and you zoom out.
    if (fZBufferDepth < 1 && (length(worldPos.xyz) * 1) >= (Rt - 0.004f))
    {
        worldPos.xyz = worldPos.xyz / length(worldPos.xyz) * (Rt - 0.004f) / 1;
    }

    // This prevents a strange bug that happens when water dips below
    // sea level.
    if (x0l < 1.0001)
    {
        worldPos.xyz *= (1.0001 / x0l);
        x0l = length(worldPos.xyz);
    }
    
    vec3 s = mul(vec4(0,0,0,1), matMainLightVP);
    vec3 x = fvWsEyePosition * 1;
    vec3 v = (worldPos - fvWsEyePosition);
    float dist = length(v) * 1;
    v = normalize(v);
    s = normalize(s);
    float r, mu;
    vec3 result;
    r = length(x);
    mu = dot(x, v) / r;
    float d = -r * mu - sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    
    if (d > 0.0) 
    { // if x in space and ray intersects atmosphere
        // move x to nearest intersection of ray with top atmosphere boundary
        x += d * v;
        mu = (r * mu + d) / Rt;
        r = Rt;
        dist -= d;
    }
    
    float muS = (dot(x, s) + fDaylightOffset) / r;
    float fNightFogAmount = lerp(0.9, 0.6, fAtmosphereFog);
    float nightAtmosphereAmt = saturate(muS * 25 + fNightFogAmount) + 1 - fNightFogAmount;
    if (r <= Rt) 
    { // if ray intersects atmosphere
        float nu = dot(v, s) * saturate(muS * 25);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            lerp(Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0),
                Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0.5), fAtmosphereFog) , 0.0);
        result = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM, 0.0);
        result *= ISun;
    } 
    else 
    { // x in space and ray looking in space
        result = vec3(0.0, 0.0, 0.0);
    }   

    const int smpSize = 4;
    float width, height;
    fullscreenTex.GetDimensions(width, height);
    const vec2 offsetSize = vec2(1.0f / width, 1.0f / height);
    vec4 inColorCmb = vec4(0,0,0,0);
    for (int ix = -smpSize; ix <= smpSize; ++ix)
    {
        for (int iy = -smpSize; iy <= smpSize; ++iy)
        {
            vec4 fsSmp = fullscreenTex.Sample(linearClampS, v_texcoord0.xy + offsetSize * vec2(ix, iy));
            inColorCmb += max(vec4(0,0,0,0), 
                 fsSmp - vec4(0.5, 0.5, 0.5, 0)) / (abs(ix) + abs(iy) + 1) * fsSmp.a;
        }
    }

    vec4 inColor = fullscreenTex.Sample(linearClampS, v_texcoord0.xy);
    vec4 inColorDrp = drapeRemap.Sample(linearClampS, v_texcoord0.xy);
    //inColor.rgb = pow(inColor.rgb, 1.0 / 2.2);

    vec4 inColorAdj = inColor;
    inColorAdj.rgb = IHDR(inColorAdj.rgb, 2.2);

    // Now calculate backend of the scattering to be subtracted out    
    vec3 result2 = vec3(0,0,0);
    if (fZBufferDepth < 1)
    {
        float nightblur = 1 - saturate (muS * 25);
        inColorAdj += inColorCmb * 2 * nightblur;
        float r2, mu2;
        vec3 x2 = worldPos.xyz * 1;
        r2 = length(x2);
        mu2 = dot (x2, v) / r2;
        float nu = dot (v, s) * saturate(muS * 25);
        float muS = (dot (x2, s) + fDaylightOffset) / r2;
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            lerp(Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0), 
                Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0.5), fAtmosphereFog), 0.0);
        result2 = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM , 0.0);
        inColorAdj.xyz = (inColorAdj.xyz * 1.5 - vec3(1,1,1) * 0.1) * 0.2;
    }
    else
    {
        // remove starfield during daytime
        inColorAdj *= saturate(1.2 - result.g);
    }
    // purple up the night sky
    result = lerp(result, result.grb, 0.65 * saturate(-muS));
    result2 = lerp(result2, result2.grb, 0.65 * saturate(-muS));

    x = fvWsEyePosition * 1 ;

    vec4 outColorHDR = GroundColor(inColorAdj, x, v, s, r, mu, dist, x0l, fZBufferDepth, result, result2, fAtmosphereFog);
    if (fZBufferDepth >= 1)
    {
        outColorHDR.xyz += SunColor(v_texcoord0.xy, x, v, s, r, mu, 0);
    }

    float fDarkenFog = lerp(1, 0.5, fAtmosphereFog);
    outColor = max(vec4(HDR(outColorHDR *  fDarkenFog), 1), vec4(0,0,0,0));
    outColor = lerp(outColor, inColor, inColorDrp.a);
    gl_FragColor = outColor;
} 
