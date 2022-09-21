#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable


layout(location = 0) in vec2 InDepth;
layout(location = 1) in vec3 InNormal;
layout(location = 0) out vec4 OutColor;

void main()
{
    float v = (InDepth.x / InDepth.y);
    OutColor = vec4(v,v,v,v);
}
