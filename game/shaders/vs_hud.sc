$input a_position
$input a_texcoord0
$output v_texcoord0

#include <bgfx_shader.sh>

void main()
{
	vec2 tx = a_texcoord0;
	v_texcoord0 = tx;
	gl_Position = vec4(a_position.xy, 0.5,1);
}
 