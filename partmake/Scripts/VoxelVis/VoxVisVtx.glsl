#version 450
layout(set = 0, binding = 0) uniform ProjectionBuffer
{
    mat4 Projection;
};
layout(set = 0, binding = 1) uniform ViewBuffer
{
    mat4 View;
};

layout(set = 1, binding = 0) uniform WorldBuffer
{
    mat4 World;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;
layout(location = 3) in vec2 InstanceCoord;
layout(location = 0) out vec2 fsin_texCoords;
layout(location = 1) out vec3 fsin_normal;

layout(set = 1, binding = 2) uniform texture2D VoxTexture;
layout(set = 1, binding = 3) uniform sampler VoxSampler;

void main()
{
    float ival = textureLod(sampler2D(VoxTexture, VoxSampler), InstanceCoord, 0).r;
    vec4 worldPosition = World * vec4(Position +
    	vec3(InstanceCoord.x - 0.5, ival * 0.1, InstanceCoord.y - 0.5) * 1024, 1);
    vec4 viewPosition = View * worldPosition;
    vec4 clipPosition = Projection * viewPosition;
    gl_Position = clipPosition;
    fsin_texCoords = TexCoords;
    fsin_normal = Normal;
}