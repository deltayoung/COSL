#version 130

in vec3 position;
in vec3 normal; 
in vec3 color; 
in vec2 texture; 

uniform mat4 ProjectionMatrix; 
uniform mat4 ModelMatrix; 
uniform mat4 fullTransformaMatrix; 

out vec3 outputColor;  

void main()
{
//	gl_Position =  ProjectionMatrix * ModelMatrix  * vec4(position,1.0f) ;
	gl_Position =  fullTransformaMatrix  * vec4(position,1.0f) ;
	outputColor = vec3(1.0f, 1.0f, 0.0f); 
}