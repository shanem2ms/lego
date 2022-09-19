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
    float ival = textureLod(sampler2D(VoxTexture, VoxSampler), InstanceCoord, 0).w;
    float ivaldy = textureLod(sampler2D(VoxTexture, VoxSampler), InstanceCoord - vec2(0, 1/1024.0), 0).w;
    float ivaldx = textureLod(sampler2D(VoxTexture, VoxSampler), InstanceCoord - vec2(1/1024.0, 0), 0).w;
    vec2 lookup = ((Position.xy - vec2(1,1)) * 0.5) / 1024.0;  
    vec2 vtxpos = InstanceCoord + lookup;
    float ipos = textureLod(sampler2D(VoxTexture, VoxSampler), vtxpos, 0).w;
    
    vec3 nrm = normalize(vec3(ivaldx - ival, ivaldy - ival, 0.01));
    vec4 worldPosition = World * vec4(
    	vec3(vtxpos.x - 0.5, ipos * 0.1, vtxpos.y - 0.5) * 635, 1);
    vec4 viewPosition = View * worldPosition;
    vec4 clipPosition = Projection * viewPosition;
    gl_Position = clipPosition;
    fsin_texCoords = TexCoords;
    fsin_normal = nrm;
}