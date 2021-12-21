$input v_vtxcolor

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>


void main()
{
    gl_FragColor = vec4(v_vtxcolor);	
} 

