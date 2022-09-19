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



vec4 FS( Vox_Shaders_DepthDownScale_FragmentInput input_)
{
    float x = field_ss.ddx * 0.5f;
    float y = field_ss.ddy * 0.5f;
    vec4 v0 = texture(sampler2D(Texture, Sampler), input_.fsUV + vec2(-x, -y));
    vec4 v1 = texture(sampler2D(Texture, Sampler), input_.fsUV + vec2(x, -y));
    vec4 v2 = texture(sampler2D(Texture, Sampler), input_.fsUV + vec2(-x, y));
    vec4 v3 = texture(sampler2D(Texture, Sampler), input_.fsUV + vec2(x, y));
    vec3 c0 = Vox_Shaders_DepthDownScale_Decode(v0.z);
    vec3 c1 = Vox_Shaders_DepthDownScale_Decode(v1.z);
    vec3 c2 = Vox_Shaders_DepthDownScale_Decode(v2.z);
    vec3 c3 = Vox_Shaders_DepthDownScale_Decode(v3.z);
    vec3 cavg = (c0 + c1 + c2 + c3) * 0.25f;
    float vzavg = Vox_Shaders_DepthDownScale_Encode(cavg);
    float maxdepth = max(max(max(v0.x, v1.x), v2.x), v3.x);
    float mindepth = min(min(min(v0.y, v1.y), v2.y), v3.y);
    float mask = (v0.w + v1.w + v2.w + v3.w) * 0.25f;
    return vec4(maxdepth, mindepth, vzavg, mask);
}


layout(location = 0) in vec2 fsin_0;
layout(location = 0) out vec4 _outputColor_;

void main()
{
    Vox_Shaders_DepthDownScale_FragmentInput input_;
    input_.Position = gl_FragCoord;
    input_.fsUV = fsin_0;
    vec4 output_ = FS(input_);
    _outputColor_ = output_;
}
