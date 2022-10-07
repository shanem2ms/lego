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
    float aspectRatio;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;
layout(location = 1) out vec3 fsin_normal;
layout(location = 2) out vec4 fsin_Pos;

void main()
{	
	vec3 pos = Position;
	mat4 wvp = Projection * View * World;
	vec4 c0 = wvp * vec4(0, 0, 0, 1);
	vec4 c1 = wvp * vec4(0.1, 0, 0, 1);
	c0 /= c0.w;
	c1 /= c1.w;
	vec2 d = c1.xy - c0.xy;
	vec2 dir = normalize(d);
	vec2 crossdir = vec2(dir.y, -dir.x);
    vec4 clipPosition = wvp * vec4(pos.x,0,0, 1);
    clipPosition /= clipPosition.w;
    clipPosition.xy += (Position.y * crossdir * vec2(aspectRatio, 1)) * 0.01;
    gl_Position = clipPosition;
    fsin_texCoords = TexCoords;
    fsin_normal = Normal;
    fsin_Pos = clipPosition;
}