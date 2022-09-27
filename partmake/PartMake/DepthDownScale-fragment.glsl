#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_EXT_samplerless_texture_functions : enable
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

layout(set = 0, binding = 0) uniform utexture2D Texture;
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
layout(location = 0) out uvec4 OutColor;

void main()
{
    ivec2 iv = ivec2(int(fsUV.x * field_ss.ddx), int(fsUV.y * field_ss.ddy));
    uvec4 v0 = texelFetch(Texture, iv + ivec2(0, 0), 0);
    uvec4 v1 = texelFetch(Texture, iv + ivec2(0, 1), 0);
    uvec4 v2 = texelFetch(Texture, iv + ivec2(1, 0), 0);
    uvec4 v3 = texelFetch(Texture, iv + ivec2(1, 1), 0);
    uint avgz = (v0.z + v1.z + v2.z + v3.z) / 4;
    uint avgw = (v0.w + v1.w + v2.w + v3.w) / 4;
    uint maxdepth = max(max(max(v0.x, v1.x), v2.x), v3.x);
    uint mindepth = min(min(min(v0.y, v1.y), v2.y), v3.y);
    OutColor = uvec4(maxdepth, mindepth, avgz, avgw);
}
