#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Depth_Transform
{
    mat4 Projection;
    mat4 View;
    mat4 Model;
    vec4 LightPos;
};

struct Vox_Shaders_Depth_Material
{
    vec4 DiffuseColor;
};

struct Vox_Shaders_Depth_VertexInput
{
    vec3 Position;
    vec2 UV;
    vec3 Color;
    vec3 Normal;
};

struct Vox_Shaders_Depth_FragmentInput
{
    vec4 Position;
    vec2 Depth;
    vec3 VtxColor;
};

layout(set = 0, binding = 1) uniform m
{
    Vox_Shaders_Depth_Material field_m;
};


vec4 FS( Vox_Shaders_Depth_FragmentInput input_)
{
    float v = (input_.Depth.x / input_.Depth.y);
    float c = field_m.DiffuseColor.x + floor(field_m.DiffuseColor.y * 256) + floor(field_m.DiffuseColor.z * 256) * 256;
    return vec4(v, v, c, 1);
}


layout(location = 0) in vec2 fsin_0;
layout(location = 1) in vec3 fsin_1;
layout(location = 0) out vec4 _outputColor_;

void main()
{
    Vox_Shaders_Depth_FragmentInput input_;
    input_.Position = gl_FragCoord;
    input_.Depth = fsin_0;
    input_.VtxColor = fsin_1;
    vec4 output_ = FS(input_);
    _outputColor_ = output_;
}
