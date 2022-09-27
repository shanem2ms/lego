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
    uvec4 v[4];
    v[0] = texelFetch(Texture, iv + ivec2(0, 0), 0);
    v[1] = texelFetch(Texture, iv + ivec2(0, -1), 0);
    v[2] = texelFetch(Texture, iv + ivec2(-1, 0), 0);
    v[3] = texelFetch(Texture, iv + ivec2(-1, -1), 0);    
    int cnt = 0;
    uint maxdepth = 0;
    uint mindepth = 4294967295;
    for (int i = 0; i < 4; ++i)
    {
        if (v[i].x > 0)
        {   
            cnt++;
        }
    }
    uint avgz = 0;
    uint avgw = 0;
    if (cnt > 0)
    {
        for (int i = 0; i < 4; ++i)
        {
            if (v[i].x == 0)
                continue;
            avgz += v[i].z / cnt;
            avgw += v[i].w / cnt;
            maxdepth = max(maxdepth, v[i].x);
            mindepth = min(mindepth, v[i].y);
        }
        OutColor = uvec4(maxdepth, mindepth, avgz, avgw);
    }
    else
    {
        OutColor = uvec4(0, 0, 0, 0);
    }
}
