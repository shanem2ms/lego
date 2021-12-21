
$input v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

SAMPLER2D(s_blittex, 0);

void main()
{    
	gl_FragColor = texture2DLod(s_blittex, v_texcoord0.xy, 0);
} 


