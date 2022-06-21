$input a_position
$input a_texcoord0
$output v_texcoord0

#include <bgfx_shader.sh>

void main()
{
	vec2 tx = a_texcoord0;
	v_texcoord0 = tx;
	
	gl_Position =  mul(u_model[0], vec4(a_position.x, a_position.y, a_position.z, 1.0) );
}
 