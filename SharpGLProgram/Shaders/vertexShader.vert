#version 130

in vec3 position;
in vec3 normal; 
in vec3 color; 
in vec2 TexCoord; 
in float slice; 

uniform vec3 diffuseLightWorld; 

uniform mat4 ProjectionMatrix; 
uniform mat4 ModelMatrix; 
uniform mat4 fullTransformaMatrix; 

out vec3 outputColor;  
out float sliceValue; 
out vec2 TexCoord0; 

// lighting information 
out vec3 diffuseLight; 
out vec3 theNormal; 
out vec3 thePosition; 



void main()
{
	
	gl_Position =  fullTransformaMatrix  * vec4(position ,1.0f) ;
	
	outputColor = color; 
	sliceValue = slice;  
		
	// passing the texture cordinates of the vertex to the shaders 	
	TexCoord0 = TexCoord;  // passing it onto the fragment shaders 



	theNormal = vec3(ModelMatrix * vec4(normal,0.0f));  // the reason why we put the zero behind is to ignore the translation part of the matrix. 
	thePosition = vec3(ModelMatrix * vec4(position,1.0f));  // transform the position
	diffuseLight =  vec3(ModelMatrix * vec4(diffuseLightWorld,1.0f)); // transform the light source 


}