#version 450

layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec3 fsin_normal;
layout(location = 0) out vec4 fsout_color;

layout(set = 1, binding = 1) uniform MeshColor
{
    vec4 col;
};

void main()
{
    fsout_color =  col;
}
