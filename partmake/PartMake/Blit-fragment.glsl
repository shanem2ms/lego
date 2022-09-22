#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout(set = 0, binding = 0) uniform texture2D Texture;
layout(set = 0, binding = 1) uniform sampler Sampler;


layout(location = 0) in vec2 fsin_0;
layout(location = 0) out vec4 OutColor;

void main()
{
    OutColor = texture(sampler2D(Texture, Sampler), fsin_0);
}
