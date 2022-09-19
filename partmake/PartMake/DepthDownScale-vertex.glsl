#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_DepthDownScale_Subsample
{
    float ddx;
    float ddy;
    vec2 filler;
};

struct Vox_Shaders_DepthDownScale_VertexInput
{
    vec3 Position;
    vec3 UVW;
};

struct Vox_Shaders_DepthDownScale_FragmentInput
{
    vec4 Position;
    vec2 fsUV;
};


Vox_Shaders_DepthDownScale_FragmentInput VS( Vox_Shaders_DepthDownScale_VertexInput input_)
{
    Vox_Shaders_DepthDownScale_FragmentInput output_;
    output_.Position = vec4((input_.Position - vec3(0.5f, 0.5f, 0)) * 2, 1);
    output_.fsUV = vec2(input_.UVW.x, input_.UVW.y);
    return output_;
}


layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 UVW;
layout(location = 0) out vec2 fsin_0;

void main()
{
    Vox_Shaders_DepthDownScale_VertexInput input_;
    input_.Position = Position;
    input_.UVW = UVW;
    Vox_Shaders_DepthDownScale_FragmentInput output_ = VS(input_);
    fsin_0 = output_.fsUV;
    gl_Position = output_.Position;
        gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}
