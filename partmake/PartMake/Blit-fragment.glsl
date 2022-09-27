#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_EXT_samplerless_texture_functions : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Blit_Transform
{
    mat4 MWP;
    ivec2 texSize;
};


layout(set = 0, binding = 0) uniform utexture2D Texture;

layout(set = 0, binding = 1) uniform t
{
    Vox_Shaders_Blit_Transform field_t;
};


layout(location = 0) in vec2 fsin_0;
layout(location = 0) out vec4 OutColor;

void main()
{
    ivec2 uv = ivec2(fsin_0 * ivec2(field_t.texSize.x,field_t.texSize.y));
    uvec4 v4 = texelFetch(Texture, uv, 0);
    OutColor = vec4(v4) / 4294967295.0;
}
