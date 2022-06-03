$input a_position, a_texcoord0, a_normal
$output v_vtxcolor, v_texcoord0, v_normal


/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */ 


#include <bgfx_shader.sh>
#include "uniforms.sh"

SAMPLER2D(s_brickPalette, 0);

void main()
{ 
	float u = fmod(u_params[0].x, 16) / 16.0;
	float v = (floor(u_params[0].x / 16) / 16.0);
	v = 1-v;
	v_texcoord0 = a_texcoord0;
	v_vtxcolor = u_params[0];
	v_normal = a_normal;  
	gl_Position = mul(u_modelViewProj, vec4(a_position.x, a_position.y, a_position.z, 1.0) );
}
