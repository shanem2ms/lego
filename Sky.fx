//--------------------------------------------------------------------------------------------------
// Atmosphere/Sky Shader Fragment File
//
// This file defines the shader fragments which are used to render the surface
// of the earth.
//--------------------------------------------------------------------------------------------------

global
{

// View direction used for skymap.
Texture2D transmittanceTexture;
Texture2D deltaETexture;
Texture3D deltaSRTexture;
Texture3D deltaSMTexture;
Texture3D deltaSTexture;
Texture3D deltaJTexture;

Texture2D reflectanceTexture; //ground reflectance texture
Texture2D irradianceTexture;  //precomputed skylight irradiance (E table)
Texture3D inscatterTexture;   //precomputed inscattered light (S table)

// Rayleigh

static const vec3 betaR = vec3(36.888, 85.86, 210.516);
//static const float Rt = 1.009434;
static const float ISun = 70.0;
static const float exposure = 0.4;
static const int TRANSMITTANCE_INTEGRAL_SAMPLES = 500;
static const int INSCATTER_INTEGRAL_SAMPLES = 50;
static const int IRRADIANCE_INTEGRAL_SAMPLES = 32;
static const int INSCATTER_SPHERICAL_INTEGRAL_SAMPLES = 16;
static const int RES_R = 32;
static const int RES_MU = 128;
static const int RES_MU_S = 32;
static const int RES_NU = 8;
static const float RtCalc = 1.009434f;


// nearest intersection of ray r, mu with ground or top atmosphere boundary
// mu=cos(ray zenith angle at ray origin)
float Limit(float r, float mu) 
{
    float dout = -r * mu + sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    float delta2 = r * r * (mu * mu - 1.0) + 1;
    if (delta2 >= 0.0) 
    {
        float din = -r * mu - sqrt(delta2);
        if (din >= 0.0) 
        {
            dout = min(dout, din);
        }
    }
    return dout;
}

void GetTransmittanceRMu(in vec2 texUV, out float r, out float muS) 
{
    r = texUV.y;
    muS = texUV.x;
    r = 1 + (r * r) * (Rt - 1);
    muS = -0.15 + tan(1.5 * muS) / tan(1.5) * (1.0 + 0.15);
}

vec2 GetTransmittanceUV(float r, float mu) 
{
    float uR, uMu;
    uR = sqrt((r - 1) / (RtCalc - 1));
    uMu = atan((mu + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
    return vec2(uMu, 1 - uR);
}

float OpticalDepth(float H, float r, float mu) 
{
    float result = 0.0;
    float dx = Limit(r, mu) / float(TRANSMITTANCE_INTEGRAL_SAMPLES);
    float xi = 0.0;
    float yi = exp(-(r - 1) / H);
    for (int i = 1; i <= TRANSMITTANCE_INTEGRAL_SAMPLES; ++i) 
    {
        float xj = float(i) * dx;
        float yj = exp(-(sqrt(r * r + xj * xj + 2.0 * xj * r * mu) - 1) / H);
        result += (yi + yj) / 2.0 * dx;
        xi = xj;
        yi = yj;
    }
    return mu < -sqrt(1.0 - (1 / r) * (1 / r)) ? 1e9 : result;
}

void GetIrradianceRMuS(in vec2 texUV, out float r, out float muS)
 {
    r = 1 + texUV.y * (Rt - 1);
    muS = -0.2 + texUV.x * (1.0 + 0.2);
}

vec3 Transmittance(float r, float mu) 
{
    vec2 uv = GetTransmittanceUV(r, mu);
    uv.y = 1.0 - uv.y;
    return transmittanceTexture.SampleLevel(linearClampS, uv, 0.0f).rgb;
}

vec3 TransmittanceAtlas(float r, float mu, float i) 
{
    vec2 uv = GetTransmittanceUV(r, mu);
    uv.y = saturate(1.0 - uv.y);
    return transmittanceTexture.SampleLevel(linearClampS, uv * vec2(1, 0.495) + vec2(0, i), 0.0f).rgb;
}

// Transmittance(=transparency) of atmosphere between x and x0
// assume segment x,x0 not intersecting ground
// r=||x||, mu=cos(zenith angle of [x,x0) ray at x), v=unit direction floattor of [x,x0) ray
vec3 Transmittance(float r, float mu, vec3 v, vec3 x0) 
{
    vec3 result;
    float r1 = length(x0);
    float mu1 = dot(x0, v) / r;
    if (mu > 0.0) 
    {
        result = min(Transmittance(r, mu) / Transmittance(r1, mu1), 1.0);
    } 
    else 
    {
        result = min(Transmittance(r1, -mu1) / Transmittance(r, -mu), 1.0);
    }
    return result;
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

// Transmittance(=transparency) of atmosphere between x and x0
// assume segment x,x0 not intersecting ground
// d = distance between x and x0, mu=cos(zenith angle of [x,x0) ray at x)
vec3 Transmittance(float r, float mu, float d) 
{
    vec3 result;
    float r1 = sqrt(r * r + d * d + 2.0 * r * mu * d);
    float mu1 = (r * mu + d) / r1;
    if (mu > 0.0) 
    {
        result = min(Transmittance(r, mu) / Transmittance(r1, mu1), 1.0);
    } 
    else 
    {
        result = min(Transmittance(r1, -mu1) / Transmittance(r, -mu), 1.0);
    }
    return result;
}


void Integrand(float r, float mu, float muS, float nu, float t, out vec3 ray, out vec3 mie) 
{
    ray = vec3(0.0, 0.0, 0.0);
    mie = vec3(0.0, 0.0, 0.0);
    float ri = sqrt(r * r + t * t + 2.0 * r * mu * t);
    float muSi = (nu * t + muS * r) / ri;
    ri = max(1, ri);
    if (muSi >= -sqrt(1.0 - 1 / (ri * ri))) 
    {
        vec3 ti = Transmittance(r, mu, t) * Transmittance(ri, muSi);
        ray = exp(-(ri - 1) / HR) * ti;
        mie = exp(-(ri - 1) / HM) * ti;
    }
}

void Inscatter(float r, float mu, float muS, float nu, out vec3 ray, out vec3 mie) 
{
    ray = vec3(0.0, 0.0, 0.0);
    mie = vec3(0.0, 0.0, 0.0);
    float dx = Limit(r, mu) / float(INSCATTER_INTEGRAL_SAMPLES);
    float xi = 0.0;
    vec3 rayi;
    vec3 miei;
    Integrand(r, mu, muS, nu, 0.0, rayi, miei);
    for (int i = 1; i <= INSCATTER_INTEGRAL_SAMPLES; ++i) 
    {
        float xj = float(i) * dx;
        vec3 rayj;
        vec3 miej;
        Integrand(r, mu, muS, nu, xj, rayj, miej);
        ray += (rayi + rayj) / 2.0 * dx;
        mie += (miei + miej) / 2.0 * dx;
        xi = xj;
        rayi = rayj;
        miei = miej;
    }
    ray *= betaR;
    mie *= betaMSca;
}

void GetMuMuSNu(vec2 sp, float r, vec4 dhdH, out float mu, out float muS, out float nu) 
{
    float x = sp.x - 0.5;
    float y = sp.y - 0.5;
    if (y < float(RES_MU) / 2.0)
    {
        float d = 1.0 - y / (float(RES_MU) / 2.0 - 1.0);
        d = min(max(dhdH.z, d * dhdH.w), dhdH.w * 0.999);
        mu = (1 - r * r - d * d) / (2.0 * r * d);
        mu = min(mu, -sqrt(1.0 - (1 / r) * (1 / r)) - 0.001);
    } 
    else 
    {
        float d = (y - float(RES_MU) / 2.0) / (float(RES_MU) / 2.0 - 1.0);
        d = min(max(dhdH.x, d * dhdH.y), dhdH.y * 0.999);
        mu = (Rt * Rt - r * r - d * d) / (2.0 * r * d);
    }
    muS = fmod(x, float(RES_MU_S)) / (float(RES_MU_S) - 1.0);
    // paper formula
    //muS = -(0.6 + log(1.0 - muS * (1.0 -  exp(-3.6)))) / 3.0;
    // better formula
    muS = tan((2.0 * muS - 1.0 + 0.26) * 1.1) / tan(1.26 * 1.1);
    nu = -1.0 + floor(x / float(RES_MU_S)) / (float(RES_NU) - 1.0) * 2.0;
}


static const float dphi = M_PI / float(INSCATTER_SPHERICAL_INTEGRAL_SAMPLES);
static const float dtheta = M_PI / float(INSCATTER_SPHERICAL_INTEGRAL_SAMPLES);
static const float AVERAGE_GROUND_REFLECTANCE = 0.1;
static const float dphiI = M_PI / float(IRRADIANCE_INTEGRAL_SAMPLES);
static const float dthetaI = M_PI / float(IRRADIANCE_INTEGRAL_SAMPLES);

// Rayleigh phase function
float PhaseFunctionR(float mu) 
{
    return (3.0 / (16.0 * M_PI)) * (1.0 + mu * mu);
}

// Mie phase function
float PhaseFunctionM(float mu) 
{
    return 1.5 * 1.0 / (4.0 * M_PI) * (1.0 - mieG*mieG) * pow(1.0 + (mieG*mieG) - 2.0*mieG*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + mieG*mieG);
}

// Mie phase function
float PhaseFunctionM2(float mu, float mieGG) 
{
    return 1.5 * 1.0 / (4.0 * M_PI) * (1.0 - mieGG*mieGG) * pow(1.0 + (mieGG*mieGG) - 2.0*mieGG*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + mieGG*mieGG);
}

vec2 GetIrradianceUV(float r, float muS) 
{
    float uR = (r - 1) / (Rt - 1);
    float uMuS = (muS + 0.2) / (1.0 + 0.2);
    return vec2(uMuS, uR);
}

vec3 Irradiance(Texture2D irtex, float r, float muS) 
{
    vec2 uv = GetIrradianceUV(r, muS);
    return irtex.Sample(linearClampS, uv).rgb;
}

vec3 IrradianceAtlas(Texture2D irtex, float r, float muS, float i) 
{
    vec2 uv = GetIrradianceUV(r, muS);
    uv.y = saturate(uv.y);
    return irtex.Sample(linearClampS, uv * vec2(1, 0.5) + vec2(0, i)).rgb;
}

vec4 Texture4DAtlas(Texture3D table, float r, float mu, float muS, float nu, float d, float s)
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
    float mix = (nu + 1.0) / 2.0 * (float(RES_NU) - 1.0);
    float uNu = floor(mix);
    mix = mix - uNu;
    return table.Sample(linearClampS, vec3((uNu + uMuS) / float(RES_NU), uMu, uR * d + s)) * (1.0 - mix) +
           table.Sample(linearClampS, vec3((uNu + uMuS + 1.0) / float(RES_NU), uMu, uR * d + s)) * mix;
}

vec4 Texture4D(Texture3D table, float r, float mu, float muS, float nu)
{
    return Texture4DAtlas(table, r, mu, muS, nu, 1, 0);
}

void InscatterS(float r, float mu, float muS, float nu, out vec3 raymie) 
{
    r = clamp(r, 1, Rt);
    mu = clamp(mu, -1.0, 1.0);
    muS = clamp(muS, -1.0, 1.0);
    float var = sqrt(1.0 - mu * mu) * sqrt(1.0 - muS * muS);
    nu = clamp(nu, muS * mu - var, muS * mu + var);

    float cthetamin = -sqrt(1.0 - (1 / r) * (1 / r));

    vec3 v = vec3(sqrt(1.0 - mu * mu), 0.0, mu);
    float sx = v.x == 0.0 ? 0.0 : (nu - muS * mu) / v.x;
    vec3 s = vec3(sx, sqrt(max(0.0, 1.0 - sx * sx - muS * muS)), muS);

    raymie = vec3(0.0, 0.0, 0.0);

    // integral over 4.PI around x with two nested loops over w directions (theta,phi) -- Eq (7)
    for (int itheta = 0; itheta < INSCATTER_SPHERICAL_INTEGRAL_SAMPLES; ++itheta)
    {
        float theta = (float(itheta) + 0.5) * dtheta;
        float ctheta = cos(theta);

        float greflectance = 0.0;
        float dground = 0.0;
        vec3 gtransp = vec3(0.0, 0.0, 0.0);
        if (ctheta < cthetamin) 
        { // if ground visible in direction w
            // compute transparency gtransp between x and ground
            greflectance = AVERAGE_GROUND_REFLECTANCE / M_PI;
            dground = -r * ctheta - sqrt(r * r * (ctheta * ctheta - 1.0) + 1);
            gtransp = Transmittance(1, -(r * ctheta + dground) / 1, dground);
        }

        for (int iphi = 0; iphi < 2 * INSCATTER_SPHERICAL_INTEGRAL_SAMPLES; ++iphi) 
        {
            float phi = (float(iphi) + 0.5) * dphi;
            float dw = dtheta * dphi * sin(theta);
            vec3 w = vec3(cos(phi) * sin(theta), sin(phi) * sin(theta), ctheta);

            float nu1 = dot(s, w);
            float nu2 = dot(v, w);
            float pr2 = PhaseFunctionR(nu2);
            float pm2 = PhaseFunctionM(nu2);

            // compute irradiance received at ground in direction w (if ground visible) =deltaE
            vec3 gnormal = (vec3(0.0, 0.0, r) + dground * w) / 1;
            vec3 girradiance = Irradiance(deltaETexture, 1, dot(gnormal, s));

            vec3 raymie1; // light arriving at x from direction w

            // first term = light reflected from the ground and attenuated before reaching x, =T.alpha/PI.deltaE
            raymie1 = greflectance * girradiance * gtransp;

            // second term = inscattered light, =deltaS
            if (first == 1.0) 
            {
                // first iteration is special because Rayleigh and Mie were stored separately,
                // without the phase functions factors; they must be reintroduced here
                float pr1 = PhaseFunctionR(nu1);
                float pm1 = PhaseFunctionM(nu1);
                vec3 ray1 = Texture4D(deltaSRTexture, r, w.z, muS, nu1).rgb;
                vec3 mie1 = Texture4D(deltaSMTexture, r, w.z, muS, nu1).rgb;
                raymie1 += ray1 * pr1 + mie1 * pm1;
            } 
            else 
            {
                raymie1 += Texture4D(deltaSRTexture, r, w.z, muS, nu1).rgb;
            }

            // light coming from direction w and scattered in direction v
            // = light arriving at x from direction w (raymie1) * SUM(scattering coefficient * phaseFunction)
            // see Eq (7)
            raymie += raymie1 * (betaR * exp(-(r - 1) / HR) * pr2 + betaMSca * exp(-(r - 1) / HM) * pm2) * dw;
        }
    }

    // output raymie = J[T.alpha/PI.deltaE + deltaS] (line 7 in algorithm 4.1)
}

vec3 Integrand(float r, float mu, float muS, float nu, float t) 
{
    float ri = sqrt(r * r + t * t + 2.0 * r * mu * t);
    float mui = (r * mu + t) / ri;
    float muSi = (nu * t + muS * r) / ri;
    return Texture4D(deltaJTexture, ri, mui, muSi, nu).rgb * Transmittance(r, mu, t);
}

vec3 Inscatter(float r, float mu, float muS, float nu) 
{
    vec3 raymie = vec3(0.0, 0.0, 0.0);
    float dx = Limit(r, mu) / float(INSCATTER_INTEGRAL_SAMPLES);
    float xi = 0.0;
    vec3 raymiei = Integrand(r, mu, muS, nu, 0.0);
    for (int i = 1; i <= INSCATTER_INTEGRAL_SAMPLES; ++i) 
    {
        float xj = float(i) * dx;
        vec3 raymiej = Integrand(r, mu, muS, nu, xj);
        raymie += (raymiei + raymiej) / 2.0 * dx;
        xi = xj;
        raymiei = raymiej;
    }
    return raymie;
}

// Transmittance(=transparency) of atmosphere for infinite ray (r,mu)
// (mu=cos(view zenith angle)), or zero if ray intersects ground
vec3 TransmittanceWithShadow(float r, float mu, float i) 
{
    return mu < -sqrt(1.0 - (1 / r) * (1 / r)) ? vec3(0.0, 0.0, 0.0) : TransmittanceAtlas(r, mu, i);
}

// approximated single Mie scattering
vec3 GetMie(vec4 rayMie) 
{ // rayMie.rgb=C*, rayMie.w=Cm,r
    return rayMie.rgb * rayMie.w / max(rayMie.r, 1e-4) * (betaR.r / betaR);
}

//inscattered light along ray x+tv, when sun in direction s (=S[L])
vec3 Inscatter(inout vec3 x, vec3 v, vec3 s, out float r, out float mu) 
{
    vec3 result;
    r = length(x);
    mu = dot(x, v) / r;
    float d = -r * mu - sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    if (d > 0.0) 
    { 
        // if x in space and ray intersects atmosphere
        // move x to nearest intersection of ray with top atmosphere boundary
        x += d * v;
        mu = (r * mu + d) / Rt;
        r = Rt;
    }
    if (r <= Rt) 
    { 
        // if ray intersects atmosphere
        float nu = dot(v, s);
        float muS = dot(x, s) / r;
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM(nu);
        vec4 inscatter = max(Texture4D(inscatterTexture, r, mu, muS, nu), 0.0);
        result = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM, 0.0);
    } 
    else 
    { 
        // x in space and ray looking in space
        result = vec3(0.0, 0.0, 0.0);
    }
    return result * ISun;
}

// Ground radiance at end of ray x+tv, when sun in direction s.
// Attenuated bewteen ground and viewer (=R[L0]+R[L*]).
vec4 GroundColor(
        vec4 inColor,
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
        vec4 reflectance = inColor;

        // direct sun light (radiance) reaching x0
        float muS = dot(n, s);
        vec3 sunLight = mix(
            TransmittanceWithShadow(1, muS, 0),
            TransmittanceWithShadow(1, muS, 0.5),
            fogAmt);

        // precomputed sky light (irradiance) (=E[L*]) at x0
        vec3 groundSkyLight = mix(
            IrradianceAtlas(irradianceTexture, 1, muS, 0),
            IrradianceAtlas(irradianceTexture, 1, muS, 0.5),
            fogAmt);

        // light reflected at x0 (=(R[L0]+R[L*])/T(x,x0))
        vec3 groundReflect = (max(muS, 0.0) * sunLight + groundSkyLight) * ISun / M_PI;
        vec3 groundColor = reflectance.rgb * max(groundReflect, 1);
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

// Ground radiance at end of ray x+tv, when sun in direction s.
// Attenuated bewteen ground and viewer (=R[L0]+R[L*]).
vec4 GroundColor2(
        vec4 color, 
        vec3 x, 
        vec3 v, 
        vec3 s, 
        float r, 
        float mu, 
        float xp, 
        float x0l, 
        vec3 sky, 
        vec3 sky2,
        float fogAmt,
        float nightAmt)
{
    vec3 result;
    float daylight = 1;
    float d = xp;

    // ground reflectance at end of ray, x0
    vec3 x0 = x + d * v;
    vec3 n = normalize(x0);
    vec4 reflectance = color;

    // direct sun light (radiance) reaching x0
    float muS = dot(n, s);
    vec3 sunLight = mix(
        TransmittanceWithShadow(1, muS, 0),
        TransmittanceWithShadow(1, muS, 0.5),
        fogAmt);

    // precomputed sky light (irradiance) (=E[L*]) at x0
    vec3 groundSkyLight = mix(
        IrradianceAtlas(irradianceTexture, 1, muS, 0),
        IrradianceAtlas(irradianceTexture, 1, muS, 0.5),
        fogAmt);

    // light reflected at x0 (=(R[L0]+R[L*])/T(x,x0))
    vec3 groundReflect = (max(muS, 0.0) * sunLight + groundSkyLight) * ISun / M_PI;
    vec3 groundColor = reflectance.rgb * max(groundReflect, 1);
    daylight = groundReflect.r;

    // attenuation of light to the viewer, T(x,x0)
    vec3 attenuation = mix(
        TransmittanceAtlas(r, mu, v, x0, 0),
        TransmittanceAtlas(r, mu, v, x0, 0.5),
        fogAmt);

    attenuation = mix(attenuation.ggg, attenuation.rgb, nightAmt);

    result = attenuation * groundColor; //=R[L0]+R[L*]
    if (x0l > 1.000001)
    {
        result -= attenuation * sky2 * ISun;
    }
    
    result += sky;
    return vec4(result, daylight);
}


// direct sun light for ray x+tv, when sun in direction s (=L0)
vec3 SunColor(vec2 uv, vec3 x, vec3 v, vec3 s, float r, float mu, float i)
{
    vec3 transmittance = r <= Rt ? TransmittanceWithShadow(r, mu, i) : vec3 (1.0 ,1.0 ,1.0);
    if (dot ( v , s ) > 0)
    {
        float isun = pow(dot ( v , s ), 2000 * length(transmittance))  * ISun;
        return transmittance * isun ;
    }
    else
    {
        return 0;
    }
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

}

//--------------------------------------------------------------------------------------------------
fragment FPixAtmTransmittance
{
function
{
void PixAtmTransmittance(
        in vec3 inTexCoord : TEXCOORD0,
        out vec4 outColor : SV_Target0)
{
    float r, muS;
    GetTransmittanceRMu(inTexCoord.xy, r, muS);
    vec3 depth = betaR * OpticalDepth(HR, r, muS) + betaMEx * OpticalDepth(HM, r, muS);
    outColor = vec4(exp(-depth), 1.0);
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixAtmIrradiance
{
function
{
void PixAtmIrradiance(
        in vec3 inTexCoord : TEXCOORD0,
        out vec4 outColor : SV_Target0)
{
    float r, muS;
    GetIrradianceRMuS(inTexCoord.xy, r, muS);
    outColor = vec4(Transmittance(r, muS) * max(muS, 0.0), 0.0);
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmInScatter1
{
function
{
void PixAtmInScatter1(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0,
        out vec4 outColor1       : SV_Target1)
{
    vec3 ray;
    vec3 mie;
    float mu, muS, nu;
    GetMuMuSNu(inScreenPosition.xy, r, dhdH, mu, muS, nu);
    Inscatter(r, mu, muS, nu, ray, mie);
    // store separately Rayleigh and Mie contributions, WITHOUT the phase function factor
    outColor = vec4(ray,1);
    outColor1 = vec4(mie,1);
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmCopyIrradiance
{
function
{
void PixAtmCopyIrradiance(
        in vec3 inTexCoord : TEXCOORD0,
        out vec4 outColor : SV_Target0)
{
    outColor = k * deltaETexture.SampleLevel(linearClampS, inTexCoord.xy, 0.0f);
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmCopyAtlasTransmittance
{
function
{
void FPixAtmCopyAtlas(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{	
    int2 inPos = inScreenPosition.xy;
    inPos.y -= layer * 64;

    if (inPos.y >= 0 && inPos.y < 64)
    {
        outColor = transmittanceTexture.Load(int3(inPos, 0.0f));
    }
    else
        discard;
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmCopyAtlasIrradiance
{
function
{
void FPixAtmCopyAtlas(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{	
    int2 inPos = inScreenPosition.xy;
    inPos.y -= layer * 16;

    if (inPos.y >= 0 && inPos.y < 16)
    {
        outColor = irradianceTexture.Load(int3(inPos, 0.0f));
    }
    else
        discard;
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmCopyInScatter1
{
function
{
void PixAtmCopyInScatter1(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{
    vec3 uvw = vec3(inScreenPosition.xy, float(layer) + 0.5) / vec3(int3(RES_MU_S * RES_NU, RES_MU, RES_R));
    vec4 ray = deltaSRTexture.Sample(linearClampS, uvw);
    vec4 mie = deltaSMTexture.Sample(linearClampS, uvw);
    outColor = vec4(ray.rgb, mie.r); // store only red component of single Mie scattering
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmInScatterS
{
function
{
void PixAtmInScatterS(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{
    float mu, muS, nu;
    vec3 raymie;
    GetMuMuSNu(inScreenPosition.xy, r, dhdH, mu, muS, nu);
    InscatterS(r, mu, muS, nu, raymie);
    outColor = vec4(raymie, 1);
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmIrradianceN
{
function
{
void PixAtmIrradianceN(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{
    float r, muS;
    GetIrradianceRMuS(inTexCoord.xy, r, muS);
    vec3 s = vec3(max(sqrt(1.0 - muS * muS), 0.0), 0.0, muS);

    vec3 result = vec3(0.0, 0.0, 0.0);
    // integral over 2.PI around x with two nested loops over w directions (theta,phi) -- Eq (15)
    for (int iphi = 0; iphi < 2 * IRRADIANCE_INTEGRAL_SAMPLES; ++iphi) 
    {
        float phi = (float(iphi) + 0.5) * dphiI;
        for (int itheta = 0; itheta < IRRADIANCE_INTEGRAL_SAMPLES / 2; ++itheta) 
        {
            float theta = (float(itheta) + 0.5) * dthetaI;
            float dw = dthetaI * dphiI * sin(theta);
            vec3 w = vec3(cos(phi) * sin(theta), sin(phi) * sin(theta), cos(theta));
            float nu = dot(s, w);
            if (first == 1.0) {
                // first iteration is special because Rayleigh and Mie were stored separately,
                // without the phase functions factors; they must be reintroduced here
                float pr1 = PhaseFunctionR(nu);
                float pm1 = PhaseFunctionM(nu);
                vec3 ray1 = Texture4D(deltaSRTexture, r, w.z, muS, nu).rgb;
                vec3 mie1 = Texture4D(deltaSRTexture, r, w.z, muS, nu).rgb;
                result += (ray1 * pr1 + mie1 * pm1) * w.z * dw;
            } 
            else 
            {
                result += Texture4D(deltaSRTexture, r, w.z, muS, nu).rgb * w.z * dw;
            }
        }
    }
    outColor = vec4(result, 0.0);
}
}
}


//--------------------------------------------------------------------------------------------------

fragment FPixAtmInScatterN
{
function
{
void PixAtmInScatterN(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{
    float mu, muS, nu;
    GetMuMuSNu(inScreenPosition.xy, r, dhdH, mu, muS, nu);
    outColor = vec4(Inscatter(r, mu, muS, nu), 1);
}
}
}


//--------------------------------------------------------------------------------------------------

fragment FPixAtmCopyInScatterN
{
function
{
void PixAtmCopyInScatterN(
        in vec4 inScreenPosition : SV_Position,
        in vec3 inTexCoord       : TEXCOORD0,
        out vec4 outColor        : SV_Target0)
{
    float mu, muS, nu;
    GetMuMuSNu(inScreenPosition.xy, r, dhdH, mu, muS, nu);
    vec3 uvw = vec3(inTexCoord.x, inTexCoord.y, (float(layer) + 0.5) / float(RES_R));
    outColor = vec4(deltaSTexture.Sample(linearClampS, uvw).rgb / PhaseFunctionR(nu), 0.0);
}
}
}

//--------------------------------------------------------------------------------------------------

fragment FPixAtmosphere
{
function
{
void PixAtmosphere(
        in vec3 inTexCoord : TEXCOORD0,
        out vec4 outColor  : SV_Target0)
{
    vec4 inViewPos;
    inViewPos.xy = (inTexCoord.xy - vec2(0.5, 0.5)) * vec2(2, -2);
    vec4 inTexBias = vec4(inTexCoord.xy,0,0);
    float fZBufferDepth = depthMap.Sample(linearClampS, inTexBias);
        
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
    float fNightFogAmount = mix(0.9, 0.6, fAtmosphereFog);
    float nightAtmosphereAmt = saturate(muS * 25 + fNightFogAmount) + 1 - fNightFogAmount;
    if (r <= Rt) 
    { // if ray intersects atmosphere
        float nu = dot(v, s) * saturate(muS * 25);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            mix(Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0),
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
            vec4 fsSmp = fullscreenTex.Sample(linearClampS, inTexCoord.xy + offsetSize * vec2(ix, iy));
            inColorCmb += max(vec4(0,0,0,0), 
                 fsSmp - vec4(0.5, 0.5, 0.5, 0)) / (abs(ix) + abs(iy) + 1) * fsSmp.a;
        }
    }

    vec4 inColor = fullscreenTex.Sample(linearClampS, inTexCoord.xy);
    vec4 inColorDrp = drapeRemap.Sample(linearClampS, inTexCoord.xy);
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
            mix(Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0), 
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
    result = mix(result, result.grb, 0.65 * saturate(-muS));
    result2 = mix(result2, result2.grb, 0.65 * saturate(-muS));

    x = fvWsEyePosition * 1 ;

    vec4 outColorHDR = GroundColor(inColorAdj, x, v, s, r, mu, dist, x0l, fZBufferDepth, result, result2, fAtmosphereFog);
    if (fZBufferDepth >= 1)
    {
        outColorHDR.xyz += SunColor(inTexCoord.xy, x, v, s, r, mu, 0);
    }

    float fDarkenFog = mix(1, 0.5, fAtmosphereFog);
    outColor = max(vec4(HDR(outColorHDR *  fDarkenFog), 1), vec4(0,0,0,0));
    outColor = mix(outColor, inColor, inColorDrp.a);
    //outColor.rgb = pow(outColor.rgb, 2.2);
    //outColor = mix(outColor, inColor, saturate(muS * -5));
}
}
}


//--------------------------------------------------------------------------------------------------
fragment FPixAtmospherePost
{
function
{
void PixAtmospherePost(
        in vec4 inColor          : r_shadedColor,
        in vec3 inWsPosition     : t_wsPosition,
        in vec4 inScreenPosition : SV_Position,
        out vec4 outColor        : r_shadedColor)
{
    //inColor.rgb = pow(inColor.rgb, 1.0 / 2.2);
    vec4 inColorAdj = inColor;
    vec3 worldPos = inWsPosition;
    float x0l = length(worldPos.xyz);   
    vec3 s = fvMainLightDirection;
    vec3 x = fvWsEyePosition * 1;
    vec3 v = (worldPos - fvWsEyePosition) * fAtmosphereAmount;
    worldPos = v + fvWsEyePosition;
    float dist = length(v) * 1;
    v = normalize(v);
    s = normalize(s);
    float r, mu;
    vec3 result;
    r = length(x);
    mu = dot(x, v) / r;
    float muS = (dot(x, s) + fDaylightOffset) / r;
    float d = -r * mu - sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    float fNightFogAmount = mix(0.9, 0.6, fAtmosphereFog);
    float nightAtmosphereAmt = saturate(muS * 25 + fNightFogAmount) + 1 - fNightFogAmount;
    float nightBrightAdjust = saturate(saturate(muS * 2) + 0.25);
    
    inColorAdj.rgb /= inColorAdj.a;
    inColorAdj.rgb = IHDR(inColorAdj.rgb * 0.5, 2.2) * mix(7, 2, pow(1 - fAtmosphereFog, .25) * nightBrightAdjust);

    if (d > 0.0) 
    { // if x in space and ray intersects atmosphere
        // move x to nearest intersection of ray with top atmosphere boundary
        x += d * v;
        mu = (r * mu + d) / Rt;
        r = Rt;
        dist -= d;
    }
    
    float grayness = 1 - inColor.g;
    if (r <= Rt) 
    { // if ray intersects atmosphere
        float nu = dot(v, s) * saturate(muS * 25);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            mix(Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0),
                Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0.5), fAtmosphereFog) , 0.0);
        result = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM, 0.0);
        result = mix(result.rgb, result.ggg, grayness);
    } 
    else 
    { // x in space and ray looking in space
        result = vec3(0.0, 0.0, 0.0);
    }   

    // Now calculate backend of the scattering to be subtracted out    
    vec3 result2 = vec3(0,0,0);
    {
        float r2, mu2;
        vec3 x2 = worldPos.xyz * 1;
        r2 = length(x2);
        mu2 = dot (x2, v) / r2;
        float nu = dot (v, s) * saturate(muS * 25);
        float muS = (dot (x2, s) + fDaylightOffset) / r2;
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            mix(Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0), 
                Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0.5), fAtmosphereFog), 0.0);
        result2 = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM , 0.0);
        result2 = mix(result2.rgb, result2.ggg, grayness);
    }

    x = fvWsEyePosition * 1 ;
    result *= ISun;
    vec3 outColorHDR = GroundColor2(inColorAdj * 0.5, x, v, s, r, mu, dist, x0l + 0.000001, result, result2, fAtmosphereFog, nightAtmosphereAmt);

    float fDarkenFog = mix(1, 0.5, fAtmosphereFog);
    outColor = max(vec4(HDR(outColorHDR * fDarkenFog), inColor.a), vec4(0,0,0,0));
    outColor.rgb *= outColor.a;
    outColor = mix(inColor, outColor, fMaterialAtmosphereAmount);
    //outColor.rgb = pow(outColor.rgb, 2.2);
    //outColor = mix(outColor, inColor, saturate(muS * -5));
}
}
}


//--------------------------------------------------------------------------------------------------
fragment FPixCloudsAtmospherePost
{
function
{
void PixCloudsAtmospherePost(
        in vec4 inColor          : r_shadedColor,
        in vec3 inWsPosition     : t_wsPosition,
        in vec4 inEdgeHardness   : t_edgeHardness,
        in vec4 inScreenPosition : SV_Position,
        out vec4 outColor        : r_shadedColor)
{
    vec4 inColorAdj = inColor;
    vec3 worldPos = inWsPosition;
    float x0l = length(worldPos.xyz);   
    vec3 s = fvMainLightDirection;
    vec3 x = fvWsEyePosition * 1;
    vec3 v = (worldPos - fvWsEyePosition) * mix(fAtmosphereAmount, 1.0, inEdgeHardness.w);
    worldPos = v + fvWsEyePosition;
    float dist = length(v) * 1;
    v = normalize(v);
    s = normalize(s);
    float r, mu;
    vec3 result;
    r = length(x);
    mu = dot(x, v) / r;
    float muS = dot(x, s) / r;
    float d = -r * mu - sqrt(r * r * (mu * mu - 1.0) + Rt * Rt);
    float fNightFogAmount = mix(0.9, 0.6, fAtmosphereFog);
    float nightAtmosphereAmt = saturate(muS * 25 + fNightFogAmount) + 1 - fNightFogAmount;
    float nightBrightAdjust = saturate(saturate(muS * 2) + 0.25);
    
    inColorAdj.rgb /= inColorAdj.a;
    inColorAdj.rgb = IHDR(inColorAdj.rgb * 0.7, 5.2) * mix(7, 2, pow(1 - fAtmosphereFog, .25) * nightBrightAdjust);

    if (d > 0.0) 
    { // if x in space and ray intersects atmosphere
        // move x to nearest intersection of ray with top atmosphere boundary
        x += d * v;
        mu = (r * mu + d) / Rt;
        r = Rt;
        dist -= d;
    }
    
    float sunsetAmt = saturate(muS * 2 + 1.0) - saturate(muS * 2);
    float grayness = 0.5f * (1 - sunsetAmt);
    vec3 sunset =  mix(vec3(1,1,1), vec3(1.5,0.8,.2) * 1.5, sunsetAmt);
    inColorAdj.rgb *= sunset;
    
        if (r <= Rt) 
    { // if ray intersects atmosphere
        float nu = dot(v, s) * saturate(muS * 25);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            mix(Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0),
                Texture4DAtlas(inscatterTexture, r, mu, abs(muS), nu, 0.5, 0.5), fAtmosphereFog) , 0.0);
        result = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM, 0.0);
        result = mix(result.rgb, result.ggg, grayness);
    } 
    else 
    { // x in space and ray looking in space
        result = vec3(0.0, 0.0, 0.0);
    }   

    // Now calculate backend of the scattering to be subtracted out    
    vec3 result2 = vec3(0,0,0);
    {
        float r2, mu2;
        vec3 x2 = worldPos.xyz * 1;
        r2 = length(x2);
        mu2 = dot (x2, v) / r2;
        float nu = dot (v, s) * saturate(muS * 25);
        float muS = dot (x2, s) / r2;
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM2(nu, 0.1);
        vec4 inscatter = nightAtmosphereAmt * max(
            mix(Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0), 
                Texture4DAtlas(inscatterTexture, r2, mu2, abs(muS), nu, 0.5, 0.5), fAtmosphereFog), 0.0);
        result2 = max(inscatter.rgb * phaseR + GetMie(inscatter) * phaseM , 0.0);
        result2 = mix(result2.rgb, result2.ggg, grayness);
    }

    x = fvWsEyePosition * 1 ;
    result *= ISun;
    vec3 outColorHDR = GroundColor2(inColorAdj * 0.5, x, v, s, r, mu, dist, x0l, result, result2, fAtmosphereFog, nightAtmosphereAmt);

    float fDarkenFog = mix(1, 0.5, fAtmosphereFog);
    outColor = max(vec4(HDR(outColorHDR * fDarkenFog), inColor.a), vec4(0,0,0,0));
    outColor.rgb *= outColor.a;
    outColor = mix(inColor, outColor, fMaterialAtmosphereAmount);
    //outColor = mix(outColor, inColor, saturate(muS * -5));
}
}
}

//--------------------------------------------------------------------------------------------------
// Name: FPixTextureMap
// Type: Pixel Shader Fragment
// Desc: Looks up color from a texture and multiplies by the material color.
//--------------------------------------------------------------------------------------------------
fragment FPix3dCityNightTexMap
{
function
{
void Pix3dCityNightTexMap(
        in vec4 inTexCoord    : TEXCOORD0,
        in float  inVertexAlpha : t_vertexAlpha,
        in vec4 inBaseColor   : r_baseColor,
        in vec3 inNormal      : t_normal,
        out vec4 outBaseColor : r_baseColor,
        out vec3 outNormal : r_normal)
{
    // NOTE: Color in textures is pre-multipled by alpha.  Color in inBaseColor
    // is not pre-multiplied by alpha.  MRK.
    outBaseColor = inBaseColor;
    outBaseColor.rgb *= outBaseColor.a;

    if (bTextureEnabled)
    {
        vec4 dayColor = objectTexture.Sample(objectTextureS, inTexCoord.xy);
        vec4 nightColor = normalMap.Sample(anisotropicClampS, inTexCoord.xy);

        //dayColor.rgb = pow(dayColor.rgb, 1.0f / 2.2f);
        //nightColor.rgb = pow(nightColor.rgb, 1.0f /2.2f);

        //vec4 nightColor2 = dayColor * length(nightColor.rgb) * 4;
        //float nightColorAvg = (nightColor2.r + nightColor2.g + nightColor2.b)/3;
        //vec3 nightColorDiff = nightColor2.rgb - vec3(nightColorAvg, nightColorAvg, nightColorAvg);

        outBaseColor.rgb *= mix(dayColor, nightColor * 1.5 + dayColor / 1.5 /* + nightColorDiff */, fEmissivity);            
        outBaseColor.a = nightColor > 0.25 ? 0.25 : 0;
    }

    outBaseColor = outBaseColor * inVertexAlpha;
    outNormal.xyz = normalize(inNormal.xyz);
}
}
}


global
{
Texture3D<vec2> cloudVolumeTex;
Texture2D rqtCloudL0Tex;
Texture2D rqtCloudL1Tex;
Texture2D rqtCloudL2Tex;

float random(vec3 p)
{
    // We need irrationals for pseudo randomness.
    // Most (all?) known transcendental numbers will (generally) work.
    const vec3 r = vec3(
    23.1406926327792690,  // e^pi (Gelfond's constant)
        2.6651441426902251,
        3.14159265359);//2^sqrt(2) (Gelfond - Schneider constant)
    return frac(cos(fmod(123456789.0, 1e-7 + 256.0 * dot(p,r))));  
}

//-----------------------------------------------------------------------------
// Maths utils
//-----------------------------------------------------------------------------
static const vec3x3 cloudMat = vec3x3(0.00,  0.80,  0.60,
              -0.80,  0.36, -0.48,
              -0.60, -0.48,  0.64);

float cloudHash(float n)
{
    return frac(sin(n)*43758.5453 * randSeed.x);
}

float cloudNoise(in vec3 x)
{
    vec3 p = floor(x);
    vec3 f = frac(x);

    f = f*f*(3.0-2.0*f);

    float n = p.x + p.y*57.0 + 113.0*p.z;

    float res = mix(mix(mix(cloudHash(n+  0.0), cloudHash(n+  1.0),f.x),
                        mix(cloudHash(n+ 57.0), cloudHash(n+ 58.0),f.x),f.y),
                    mix(mix(cloudHash(n+113.0), cloudHash(n+114.0),f.x),
                        mix(cloudHash(n+170.0), cloudHash(n+171.0),f.x),f.y),f.z);
    return res;
}

float cloudFbm(vec3 p)
{
    float f;
    f  = 0.5000 * cloudNoise(p); p = mul(cloudMat, p) * 3.02;
    f += 0.2500 * cloudNoise(p); p = mul(cloudMat, p) * 3.03;
    f += 0.1250 * cloudNoise(p);
    return f;
}

float cloudFbmHighDetail(vec3 p)
{
    float f;
    f  = 0.5000 * cloudNoise(p); p = mul(cloudMat, p) * 3.02;
    f += 0.2500 * cloudNoise(p); p = mul(cloudMat, p) * 3.03;
    f += 0.1250 * cloudNoise(p); p = mul(cloudMat, p) * 3.04;
    f += 0.0625 * cloudNoise(p); p = mul(cloudMat, p) * 3.05;
    f += 0.0312 * cloudNoise(p);
    return f;
}

//-----------------------------------------------------------------------------
// Ball
//-----------------------------------------------------------------------------
float cloudGen(vec3 p)
{	
    return 0.1 - length(p) * 0.7 + cloudFbm(p * noiseSize);
}

//-----------------------------------------------------------------------------
// Box / scattered
//-----------------------------------------------------------------------------
float cloudGen2(vec3 p)
{	
    static const float sv = 0.8;
    static const float mul = 1.0f / (1 - sv);
    vec3 p2 = abs(p);
    float boxFade = min((1 - max(max(p2.x, p2.y), p2.z)) * mul, 1);

    return boxFade * (cloudFbmHighDetail(p * noiseSize) - noiseThreshold);
}

}

//--------------------------------------------------------------------------------------------------
fragment FPixEarthVolCloudGenerate
{
function
{
void PixEarthVolCloudGenerate(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out float outValue : SV_Target0)
{
    vec3 msPos = vec3(inScreenPos.xy,layer) / vec3(targetSizeX,targetSizeY,targetSizeZ);

    // Get viewed pixel and convert from a height to color.
    // Add 1/512 to shift color index into middle of color cell.
    if (msPos.z < 0.5)
    {
        outValue = mix(rqtCloudL0Tex.SampleLevel(pointClampS, vec2(msPos.xy), 0),
            rqtCloudL1Tex.SampleLevel(pointClampS, vec2(msPos.xy), 0), msPos.z * 2);
    }
    else
    {
        outValue = mix(rqtCloudL1Tex.SampleLevel(pointClampS, vec2(msPos.xy), 0),
            rqtCloudL2Tex.SampleLevel(pointClampS, vec2(msPos.xy), 0), (msPos.z - 0.5) * 2);
    }
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudGenerate
{
function
{
void PixVolumeCloudGenerate(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out float outValue : SV_Target0)
{
    vec3 msPos = vec3(inScreenPos.xy,layer) / vec3(targetSizeX,targetSizeY,targetSizeZ);
    msPos = msPos * 2 - vec3(1,1,1);
    float genValue = 0;
    if (noiseType == 0)
    {
        genValue = cloudGen(msPos);
    }
    else
    {
        genValue = cloudGen2(msPos);
    }

    outValue = max(genValue * cloudDensity, 0);
}
}
}


//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudLighting
{
function
{
void PixVolumeCloudLighting(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out vec2 outValue : SV_Target0)
{
    float v = cloudVolumeTex.Load(int4(inScreenPos.x, inScreenPos.y, layer, 0));
    vec3 pixelSize = vec3(1/(float)targetSizeX, 1/(float)targetSizeY,  1/(float)targetSizeZ);
    
    vec3 vecList[7] = { 
        normalize(msLightPos),
        vec3(1, 0, -1),
        vec3(0, 1, -1),
        vec3(1, 1, -1),
        vec3(-1, 0, -1),
        vec3(0, -1, -1),
        vec3(-1, -1, -1)
    };

    float totalLightAmount = 0;
    float curWeight = 1;
    float totalWeight = 0;

    for (int i = 0; i < 1; ++i)
    {
        float lightAmount = 1;
        vec3 vec = vecList[i] * pixelSize;
        vec3 curPos = vec3(inScreenPos.x/targetSizeX, inScreenPos.y/targetSizeY, (float)layer/targetSizeZ);
        int j = 0;
        int loopCount = 70 / (i + 1);
        while (j < loopCount && curPos.x >= 0 && curPos.y >= 0 && curPos.z >= 0 &&
            curPos.x <= 1 && curPos.y <= 1 && curPos.z <= 1)
        {
            float v2 = cloudVolumeTex.SampleLevel(pointClampS, vec3(curPos), 0);
            lightAmount *= 1 - saturate(v2) * shading;
            curPos += vec;
            j++;
        }
        totalLightAmount += lightAmount * curWeight;
        totalWeight += curWeight;
        curWeight += 1;
    }
    totalLightAmount /= totalWeight;
    outValue = vec2(v, totalLightAmount);
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudEvolve
{
function
{
void PixVolumeCloudEvolve(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out vec2 outValue : SV_Target0)
{
    vec3 pixelSize = vec3(1/(float)targetSizeX, 1/(float)targetSizeY,  1/(float)targetSizeZ);
    vec3 curPos = vec3(inScreenPos.x/targetSizeX, inScreenPos.y/targetSizeY, (float)layer/targetSizeZ);
    vec3 offsets[6] = { vec3(-pixelSize.x, 0, 0), vec3(pixelSize.x, 0, 0), vec3(0, -pixelSize.y, 0), 
        vec3(0, pixelSize.y, 0), vec3(0, 0, -pixelSize.z), vec3(0, 0, pixelSize.z) };
    float v = cloudVolumeTex.SampleLevel(pointClampS, curPos, 0);

//	float weight = cloudHash(curPos.x * targetSizeY * targetSizeZ + curPos.y * targetSizeZ + curPos.z);
//	v = cloudVolumeTex.SampleLevel(pointClampS, curPos + offsets[(int)(weight * 6)], 0);
    
    /*
    for (int i = 0; i < 6; ++i)
    {
        vec3 pos = curPos + offsets[i];
        float weight = cloudHash(pos.x * targetSizeY * targetSizeZ + pos.y * targetSizeZ + pos.z);
        float v2 = cloudVolumeTex.SampleLevel(pointClampS, pos, 0);
        v += v2 * weight * .333333;
    }*/
        
    outValue = v;
}
}
}


//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudDiffuse
{
function
{
void PixVolumeCloudDiffuse(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out float outValue : SV_Target0)
{
    const int spread = 1;
    const int numrow = (spread * 2 + 1);
    const int totalSize = numrow * numrow * numrow;
    //vec2 vSmp[totalSize];
    float vTotal = 0;
    float totalPower = 0;

    for (int ix = -spread; ix < spread; ++ix)
    {
        for (int iy = -spread; iy < spread; ++iy)
        {
            for (int iz = -spread; iz < spread; ++iz)
            {
                float power = 1 / (1 + sqrt(ix * ix + iy * iy + iz * iz));

                vTotal += 
                    cloudVolumeTex.Load(int4(inScreenPos.x + ix, inScreenPos.y + iy, layer + iz, 0)) * power;
                totalPower += power;
            }
        }
    }

    outValue = vTotal / totalPower;
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudCombine
{
function
{
void PixVolumeCloudCombine(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out float outValue : SV_Target0)
{	
    vec4 msPos;
    msPos.xyz = vec3(inScreenPos.xy,layer) / vec3(targetSizeX,targetSizeY,targetSizeZ);
    msPos.w = 1;
    vec3 samplePos = mul(msPos, matCloudTransform);
    if (samplePos.x < 0 || samplePos.x > 1 || samplePos.y < 0 || samplePos.y > 1 ||
        samplePos.z < 0 || samplePos.z > 1)
    {
        outValue = 0;
    }
    else
    {
        outValue = cloudVolumeTex.SampleLevel(linearClampS, samplePos, 0);
    }
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudClear
{
function
{
void PixVolumeCloudClear(
        in vec4 inScreenPos : SV_Position,
        in uint layer : SV_RenderTargetArrayIndex,
        out float outValue : SV_Target0)
{	
    outValue = 0;
}
}
}

//--------------------------------------------------------------------------------------------------
// Name: GmtVolumeCloudEvolve
// Type: Geometry Shader Fragment
// Desc: Outputs a quad per layer for rendering to a volume texture
//--------------------------------------------------------------------------------------------------
fragment FGmtVolumeCloudLayers
{
gmtvertexargs
{
InVtx(vec4 psPos   : SV_Position,
    in vec3 tex : TEXCOORD0);

OutVtx(vec4 psPos : SV_Position,
    uint layer : SV_RenderTargetArrayIndex);

MaxVertexCount = 192;
}
gmtfunction
{
void GmtVolumeCloudLayers(
    triangle InVtx inPoint[3], inout TriangleStream<OutVtx> output)
{
    for (uint layer = 0; layer < 64; ++layer)
    {
        OutVtx ov[3];
        ov[0].psPos = inPoint[0].psPos;
        ov[0].layer = layer + layerStart;
        output.Append(ov[0]);	

        ov[1].psPos = inPoint[1].psPos;
        ov[1].layer = layer + layerStart;
        output.Append(ov[1]);	

        ov[2].psPos = inPoint[2].psPos;
        ov[2].layer = layer + layerStart;
        output.Append(ov[2]);	

        output.RestartStrip();
    }
}
}
}

//--------------------------------------------------------------------------------------------------
// Name: FVtxFinalCalcVolCloud
// Type: Vertex Shader Fragment
// Desc: Outputs final values for vertex shader
//--------------------------------------------------------------------------------------------------
fragment FVtxFinalCalcVolCloud
{
function
{
void VtxFinalCalcVolCloud(
        vec4 inPsPosition           : r_psPosition,
        vec3 inWsViewDirection      : r_vtxViewDir,
        vec3 inMsPosition           : r_msPosition,
        out vec4 outPsPosition      : SV_Position,
        out float  outDepth           : t_depth,
        out vec3 outWsViewDirection : t_wsViewDirection,
        out vec3 outMsPosition      : t_msPosition,
        out vec4 outPsPositionTx    : t_psPosition,
        out float  outClipDistance    : SV_ClipDistance0)
{
    outPsPositionTx = outPsPosition = inPsPosition;
    outDepth = inPsPosition.z / inPsPosition.w;
#ifndef PSM_SHADOWS
    outDepth *= step(0.0f, outDepth);
#endif

    outMsPosition = inMsPosition;
    outWsViewDirection = inWsViewDirection;
    outClipDistance = dot(inPsPosition, fvClipPlane);
}
}
}


//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudRenderToBlocks
{
function
{
void PixVolumeCloudRenderToBlocks(
        in vec4 inScreenPos : SV_Position,
        out vec4 outValue : SV_Target0)
{
    const int nBlocksZ = cloudBlockZCount;
    const int tilePixels = cloudBlockPixels;
    int blockXS = (int)(inScreenPos.x) / tilePixels;
    int offsetPixelsX = (int)(inScreenPos.x) % tilePixels;
    int blockX = blockXS / 6;
    int blockSide = blockXS % 6;
    int blockYZ = (int)(inScreenPos.y) / tilePixels;
    int offsetPixelsY = (int)(inScreenPos.y) % tilePixels;
    int blockY = blockYZ / nBlocksZ;
    int blockZ = blockYZ % nBlocksZ;

    int pixelX = blockXS % tilePixels;
    int pixelY = blockYZ % tilePixels;
    
    int3 vecList[18] = { 
        int3(1, 0, 0),
        int3(0, 1, 0),
        int3(0, 0, 1),

        int3(1, 0, 0),
        int3(0, 1, 0),
        int3(0, 0, -1),

        int3(1, 0, 0),
        int3(0, 0, 1),
        int3(0, 1, 0),

        int3(1, 0, 0),
        int3(0, 0, 1),
        int3(0, -1, 0),

        int3(0, 1, 0),
        int3(0, 0, 1),
        int3(1, 0, 0),

        int3(0, 1, 0),
        int3(0, 0, 1),
        int3(-1, 0, 0)
    };

    int3 xDir = vecList[blockSide * 3];
    int3 yDir = vecList[blockSide * 3 + 1];
    int3 rayDir = vecList[blockSide * 3 + 2];

    float totalLightAmount = 1;
    float curWeight = 1;
    float totalWeight = 0;

    int3 startPos = int3(blockX * tilePixels, blockY * tilePixels, blockZ * tilePixels);
    startPos = startPos + xDir * offsetPixelsX + yDir *  offsetPixelsY;
    if ((rayDir.x + rayDir.y + rayDir.z) < 0)
    {
        startPos += -rayDir * (tilePixels - 1); 
    }

    float remainingVisbility = 1;
    float absorption = 1;

    vec4 outColor = 0;
    for (int i = 0; i < tilePixels; ++i)
    {
        int3 curPos = startPos + i * rayDir;
        vec2 v = cloudVolumeTex.Load(int4(curPos, 0));
        float density = saturate(v.r * 2);
        float cloudColor = v.g * 6 + 0.25;
        outColor += vec4(cloudColor,cloudColor,cloudColor,1) * density * remainingVisbility;
        remainingVisbility *= 1.0 - density * absorption;
    }
    outValue = outColor;
}
}
}

//--------------------------------------------------------------------------------------------------
fragment FPixVolumeCloudRaycast
{
function
{
void PixVolumeCloudRaycast(
        in vec3 inMsPosition : t_msPosition,
        in vec4 inPsPosition : t_psPosition,
        out vec4 outColor : r_shadedColor)
{
    outColor = 0;
    
    inPsPosition /= inPsPosition.w;
    vec4 invMsPos1 = mul(vec4(inPsPosition.x, inPsPosition.y, 1, 1), matInvProj);
    vec4 invMsPos2 = mul(vec4(inPsPosition.x, inPsPosition.y, 0.84, 1), matInvProj);
    vec4 invMsPosCur = mul(inPsPosition, matInvProj);
    invMsPos1 /= invMsPos1.w;
    invMsPos2 /= invMsPos2.w;
    invMsPosCur /= invMsPosCur.w;
    vec3 msRayVec = invMsPos2.xyz - invMsPos1.xyz;

    int i = 0;
    vec3 absMs = abs(invMsPosCur);
    float remainingVisbility = 1;
    float absorption = 1;
    
	msRayVec = normalize(msRayVec) * 0.02;
	while (i < 1000 && absMs.x <= 1.01 &&
        absMs.y <= 1.01 &&
        absMs.z <= 1.01 &&
        remainingVisbility > 0.01)
    {
        invMsPosCur.xyz += msRayVec;
           
        vec3 samplePos = (invMsPosCur.xyz + vec3(1, 1, 1)) * 0.5f;
        vec2 v = cloudVolumeTex.SampleLevel(linearClampS, samplePos, 0);
        float density = saturate(v.r * 4);

        float cloudColor = v.g * 6 + 0.25;

        absMs = abs(invMsPosCur);
        outColor += vec4(cloudColor,cloudColor,cloudColor,1) * density * remainingVisbility;

        remainingVisbility *= 1.0 - density * absorption;
        i++;
    }

    outColor.rgb *= 0.20;
}
}
}

//--------------------------------------------------------------------------------------------------
// Name: FPix3dCityShading
// Type: Pixel Shader Fragment
// Desc: Render an object with Phong shading for a main directional light with some simplifying assumptions.
//--------------------------------------------------------------------------------------------------
fragment FPix3dCityShading
{
function
{
void Pix3dCityShading(
        in vec4 inColor           : r_baseColor,
        in vec3 inNormal          : r_normal,
        in float  inMainShadow      : r_mainShadow,
        in vec3 inWsViewDirection : t_wsViewDirection,
        out vec4 outColor         : r_shadedColor)
{
    // Calculate emissive component.
    float fAdjustedEmissivity = max(fEmissivity, 0.4);
    vec4 fvEmissiveColor = inColor * fAdjustedEmissivity;

    // Calculate common values.
    vec3 fvInNormal = normalize(inNormal.xyz);

    // Calculate diffuse and specular components.
    float  fDiffuseComponent = 1.0f - fAdjustedEmissivity;

    vec3 fvInViewDirection = normalize(inWsViewDirection);
    vec3 fvMainHalfway = normalize(fvInViewDirection + fvMainLightDirection);

    float dotNL = dot(fvInNormal, fvMainLightDirection);
    float dotNH = dot(fvInNormal, fvMainHalfway);

    // If we are doing two sided lighting, then we want the abs of the dot product.  This table
    // avoids the if test.
    dotNL *= negDotTable[bTwoSidedLightingEnabled][((int)sign(dotNL))+1];
    dotNH *= negDotTable[bTwoSidedLightingEnabled][((int)sign(dotNH))+1];

    vec4 mainLight = lit(dotNL, dotNH, fSpecularPower);

    // Calculate diffuse contribution from main light.
    vec4 fvMainDiffuse = inColor * fvMainLightDiffuse * mainLight.y * (inMainShadow * 0.5f + 0.5f);

    // Calculate the final color from the main light.
    outColor   = saturate(fvEmissiveColor + fvMainDiffuse * fDiffuseComponent);
    outColor.a = inColor.a;
    //outColor.rgb = pow(outColor.rgb, 2.2f);
}
}
}
