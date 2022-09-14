#version 450

layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec3 fsin_normal;
layout(location = 2) in vec4 fsin_Pos;
layout(location = 0) out uvec4 fsout_color;

layout(set = 1, binding = 1) uniform MeshColor
{
    vec4 col;
};

void main()
{
    uvec3 icol = uvec3(uint(col.x * 255 + 0.1), uint(col.y * 255 + 0.1), uint(col.z * 255 + 0.1));

    uint ix = uint(((fsin_Pos.x / fsin_Pos.w) + 1) * 1000000000);
    uint iy = uint(((fsin_Pos.y / fsin_Pos.w) + 1) * 1000000000);
    uint iz = uint(((fsin_Pos.z / fsin_Pos.w)) * 1000000000);

    fsout_color =  uvec4(icol.x + icol.y * 256 + icol.z * 65536,iz,iz,iz);
}
