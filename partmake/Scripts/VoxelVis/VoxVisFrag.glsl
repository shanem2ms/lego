#version 450

layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec3 fsin_normal;
layout(location = 0) out vec4 fsout_color;

layout(set = 1, binding = 1) uniform MeshColor
{
    vec4 col;
};
layout(set = 1, binding = 2) uniform texture2D VoxTexture;
layout(set = 1, binding = 3) uniform sampler VoxSampler;

void main()
{
    float p = 1.0;
    float light = pow(clamp(dot(normalize(vec3(1, 1, 0)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(-1, 1, 0)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(1, 0, -1)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(0, -1, 1)), fsin_normal),0,1), p);
        
    vec3 c = vec3(1,1,1) * (light * 0.7 + 0.1);
    fsout_color =  vec4(fsin_normal, 1);
}
