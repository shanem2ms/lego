#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
struct Vox_Shaders_Blit_Transform
{
    mat4 MWP;
    ivec2 texSize;
};

layout(set = 0, binding = 1) uniform t
{
    Vox_Shaders_Blit_Transform field_t;
};


layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_0;

void main()
{
    gl_Position = field_t.MWP * vec4(Position, 1);
    fsin_0 = vec2(TexCoords.x, 1 - TexCoords.y);
    gl_Position.y = -gl_Position.y; // Correct for Vulkan clip coordinates
}
