$input a_position, a_texcoord0, a_normal
$output v_vtxcolor, v_normal


/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */ 


#include <bgfx_shader.sh>
#include "uniforms.sh"

SAMPLER2D(s_brickPalette, 0);

void main()
{ 
	if (a_texcoord0.x < 0) a_texcoord0.x = u_params[0];
	float u = fmod(a_texcoord0.x, 16) / 16.0 ;
	float v = (floor(a_texcoord0.x / 16) / 16.0);
	vec4 col = texture2DLod(s_brickPalette, vec2(u,v), 0);
	v_vtxcolor = col;
	v_normal = a_normal;//mul(u_model[0], vec4(a_normal, 0.0));
	gl_Position = mul(u_modelViewProj, vec4(a_position.x, a_position.y, a_position.z, 1.0) );
}
