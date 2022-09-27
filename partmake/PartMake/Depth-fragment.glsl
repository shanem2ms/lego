#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable


layout(location = 0) in vec2 InDepth;
layout(location = 1) in vec3 InNormal;
layout(location = 0) out uvec4 OutColor;

uint ftou(float f)
{
    const uint div = 4294967294;
    return uint((f * div) + 1);
}
void main()
{
    
    float v = (InDepth.x / InDepth.y);
    vec2 nrm = (InNormal.xy + vec2(1,1)) * 0.5;
    OutColor = uvec4(ftou(v),ftou(v),ftou(nrm.x), ftou(nrm.y));
}
