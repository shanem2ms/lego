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

layout(set = 0, binding = 0) uniform t
{
    Vox_Shaders_Depth_Transform field_t;
};


Vox_Shaders_Depth_FragmentInput VS( Vox_Shaders_Depth_VertexInput input_)
{
    Vox_Shaders_Depth_FragmentInput output_;
    vec4 v4Pos = vec4(input_.Position, 1);
    output_.VtxColor = input_.Color;
    output_.Position = (field_t.Projection * field_t.View * field_t.Model) * v4Pos;
    output_.Depth = vec2(output_.Position.z, output_.Position.w);
    return output_;
}


layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 UV;
layout(location = 2) in vec3 Color;
layout(location = 3) in vec3 Normal;
layout(location = 0) out vec2 fsin_0;
layout(location = 1) out vec3 fsin_1;

void main()
{
    Vox_Shaders_Depth_VertexInput input_;
    input_.Position = Position;
    input_.UV = UV;
    input_.Color = Color;
    input_.Normal = Normal;
    Vox_Shaders_Depth_FragmentInput output_ = VS(input_);
    fsin_0 = output_.Depth;
    fsin_1 = output_.VtxColor;
    gl_Position = output_.Position;
        gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}
