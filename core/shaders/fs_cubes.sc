$input v_texcoord0, v_normal

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
    vec3 nrm = normalize(v_normal);
    nrm = (nrm + vec3(1,1,1)) * 0.5;
	gl_FragColor.r = packColor(nrm);
    gl_FragColor.g = 0;
    gl_FragColor.b = packColor(u_params[0].rgb);
	gl_FragColor.a = 1;
} 

