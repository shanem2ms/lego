$input a_position, a_texcoord0, a_normal
$output v_texcoord0, v_normal


/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */ 


#include <bgfx_shader.sh>

void main()
{ 
	vec2 tx = a_texcoord0;
	v_texcoord0 = tx; 
	v_normal = a_normal;  
	gl_Position = mul(u_modelViewProj, vec4(a_position.x, a_position.y, a_position.z, 1.0) );
}
