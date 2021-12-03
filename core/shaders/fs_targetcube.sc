$input v_texcoord0, v_normal

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

void main()
{	
	gl_FragColor.rgb = vec3((v_normal + vec3(1,1,1)) * 0.5f);
	gl_FragColor.a = 1;
} 
