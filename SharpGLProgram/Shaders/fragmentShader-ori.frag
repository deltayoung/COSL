#version 430

in vec3 outputColor;  

uniform float drawingLines; // +ve means draw lines, -ve means stop drawing lines
uniform vec3 setColor; 

out vec4 myColor;

void main()
{
	if ( drawingLines > 0 ) 
		myColor = vec4(setColor,1); 
	else 
		myColor = vec4(1,1,1,1);
}
