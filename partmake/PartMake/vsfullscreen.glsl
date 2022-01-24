#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;
layout(location = 1) out vec3 fsin_normal;

void main()
{
    gl_Position = vec4(Position, 1);
    fsin_texCoords = vec2(TexCoords.x, TexCoords.y);
    fsin_normal = Normal;
}