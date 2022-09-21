#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;

layout(location = 0) out vec2 fsUV;

void main()
{
    gl_Position = vec4(Position, 1);
    fsUV = vec2(TexCoords.x, TexCoords.y);
}
