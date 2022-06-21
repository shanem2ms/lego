$input v_texcoord0

#include "uniforms.sh"
#include <bgfx_shader.sh>

void main()
{
	float len = ceil(1 - length((vec2(0.5,0.5) - v_texcoord0) * 2));	 

	gl_FragColor = u_params[0] * len;
} 
