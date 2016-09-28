#version 130

in float sliceValue; 
in float depthValue; 
out vec3 myColor;

void main()
{
	myColor = vec3(sliceValue, sliceValue, depthValue);
}
