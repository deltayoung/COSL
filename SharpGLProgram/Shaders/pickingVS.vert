#version 130

in vec3 position;
in vec3 normal; 
in vec3 color; 
in vec2 texture; 
in float slice; 

uniform mat4 fullTransformaMatrix; 
out float sliceValue; 
out float depthValue; 

void main()
{
	
	gl_Position =  fullTransformaMatrix  * vec4(position,1.0f) ;



	
	sliceValue = slice;  
	depthValue = position.z; 

}