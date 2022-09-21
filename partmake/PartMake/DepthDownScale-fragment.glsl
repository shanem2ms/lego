#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_DepthDownScale_Subsample
{
    float ddx;
    float ddy;
    vec2 filler;
};

struct Vox_Shaders_DepthDownScale_FragmentInput
{
    vec4 Position;
    vec2 fsUV;
};

layout(set = 0, binding = 0) uniform texture2D Texture;
layout(set = 0, binding = 1) uniform sampler Sampler;
layout(set = 0, binding = 2) uniform ss
{
    Vox_Shaders_DepthDownScale_Subsample field_ss;
};

vec3 Vox_Shaders_DepthDownScale_Decode( float c)
{
    return vec3(mod(c, 1), mod(c / 256, 1), (c / (256 * 256)));
}


float Vox_Shaders_DepthDownScale_Encode( vec3 c)
{
    return c.x + floor(c.y * 256) + floor(c.z * 256) * 256;
}


layout(location = 0) in vec2 fsUV;
layout(location = 0) out vec4 OutColor;

void main()
{
    float x = field_ss.ddx * 0.5f;
    float y = field_ss.ddy * 0.5f;
    vec4 v0 = texture(sampler2D(Texture, Sampler), fsUV + vec2(-x, -y));
    vec4 v1 = texture(sampler2D(Texture, Sampler), fsUV + vec2(x, -y));
    vec4 v2 = texture(sampler2D(Texture, Sampler), fsUV + vec2(-x, y));
    vec4 v3 = texture(sampler2D(Texture, Sampler), fsUV + vec2(x, y));
    float avgz = (v0.z + v1.z + v2.z + v3.z) * 0.25;
    float avgw = (v0.w + v1.w + v2.w + v3.w) * 0.25;
    float maxdepth = max(max(max(v0.x, v1.x), v2.x), v3.x);
    float mindepth = min(min(min(v0.y, v1.y), v2.y), v3.y);
    OutColor = vec4(maxdepth, mindepth, avgz, avgw);
}
