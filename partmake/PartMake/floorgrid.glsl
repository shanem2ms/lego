#version 450

layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec3 fsin_normal;
layout(location = 0) out vec4 fsout_color;

layout(set = 1, binding = 1) uniform MeshColor
{
    vec4 col;
};

float segs = 10;
void main()
{
    vec2 uv = abs((mod(fsin_texCoords,1/segs) - 0.5/segs)) * segs;
    fsout_color =  vec4(uv,0,1 - ((1 -uv.x) * (1 - uv.y))) * col.w;
}
