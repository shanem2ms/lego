$input v_texcoord0

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

void main()
{
	float xv = v_texcoord0.x * (1 - v_texcoord0.x);
	float yv = v_texcoord0.y * (1 - v_texcoord0.y);
	vec2 cross = abs(v_texcoord0 - vec2(0.5,0.5));	
	vec4 col = vec4(0,0,0,0);
	float ybar = step(cross.y, 0.0015) * step(cross.x, 0.015);
	float xbar = step(cross.x, 0.0015) * step(cross.y, 0.015);
	col.w += saturate(ybar + xbar);
	gl_FragColor = col;
} 
