$input v_vtxcolor, v_texcoord0, v_normal

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

float packColor(vec3 c) {
    const float m = 255.999;
    return floor(c.b * m) * (256 * 256) +
        floor(c.g * m) * 256 +
        floor(c.r * m);
}

void main()
{
    gl_FragColor = vec4(0, u_params[0].x, 1, 1);	
} 

