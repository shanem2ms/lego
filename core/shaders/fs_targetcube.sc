$input v_vtxcolor, v_normal

/*
 * Copyright 2011-2021 Branimir Karadzic. All rights reserved.
 * License: https://github.com/bkaradzic/bgfx#license-bsd-2-clause
 */

#include "uniforms.sh"
#include <bgfx_shader.sh>

void main()
{	
	vec3 col = v_vtxcolor.xyz;
	vec3 lightdir[4] = {
	vec3(1,1,-1),
	vec3(1,-1,.5),
	vec3(-1,.5,0),
	vec3(1,.2,-.5) };

	float lval = 1;
	for (int i = 0; i < 4; ++i)
	{
		normalize(lightdir[i]);
		lval *= 1 - (1 - max(pow(dot(lightdir[i], v_normal),4), 0));
	}
	lval = 1 - lval;

	float ambient = 0.25;
	float lightamt = lval * (1 - ambient) + ambient;
	gl_FragColor.rgb = col * lightamt;
	gl_FragColor.a = 1;
} 
