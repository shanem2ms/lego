$input a_position
$input a_texcoord0
$output v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */ 


#include <bgfx_shader.sh>

void main()
{
	vec2 tx = a_texcoord0;
	v_texcoord0 = tx;
	gl_Position = vec4(a_position.xy, 0.5,1);
}
 