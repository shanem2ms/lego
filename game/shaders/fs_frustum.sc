$input v_texcoord0, v_normal

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

void main()
{
	gl_FragColor = vec4(v_texcoord0.xy, 1, 1);
	//gl_FragColor *= 0.75;
} 
