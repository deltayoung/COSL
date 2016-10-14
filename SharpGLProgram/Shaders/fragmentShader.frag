#version 130

in vec3 outputColor;  
in float sliceValue; 
in vec2 TexCoord0; 

in vec3 theNormal; 
in vec3 thePosition; 
in vec3 diffuseLight; 

uniform vec3 ambientLight; 
uniform vec3 eyePosition; 

uniform float depthIndex ; 
uniform float drawingLines; // +ve means draw lines, -ve means stop drawing lines
uniform vec3 setColor; 
uniform float alpha; 

out vec4 myColor;

uniform sampler2D gSampler; // this should be the texure map 

uniform vec3 colorPosition[256]; // this is the color choice from user 
uniform int colorCountRange; 
uniform float lowCutoff; 
uniform float highCutoff; 


void main()
{
	if ( drawingLines > 0 ) 
		myColor = vec4(setColor,alpha); 
	else 
	{
		if (( sliceValue >= depthIndex) && ( sliceValue < depthIndex+1))
		{
			myColor = vec4(1,1,0,alpha); 
		}
		else
		{
						
			myColor = texture2D(gSampler, TexCoord0.xy) ; // get the texture value , shades of grey all x y z are equal  

			int indexColor; 

			if ( myColor.x < lowCutoff ) 
				indexColor = 0; 
			else if ( myColor.x > highCutoff ) 
				indexColor = colorCountRange ; 
			else 
			{
				float colorRescale = ( myColor.x - lowCutoff ) / ( highCutoff - lowCutoff ); 					

				indexColor = int( colorRescale * ( colorCountRange + 1 ) ) ; 
				if ( indexColor > colorCountRange ) indexColor = colorCountRange; 
			}

		    myColor = vec4( colorPosition[indexColor] , alpha);  // use any of them to select the colorPosition 






			// end here 


			//vec4 finalTexture = vec4( colorPosition[indexColor] , alpha); 

			//myColor =  finalTexture ; 




			// for shading effects 

		//	vec3 diffuseIntensity = normalize(diffuseLight - thePosition);   
		//	float brightness = dot(diffuseIntensity, theNormal) ;  // if you just want the black and white color intensity
		//	vec4 diffuseColor =  vec4(brightness,brightness,brightness, alpha);  // diffuse colors 

			// specular light computation 
		//	vec3 reflectedLight = reflect(-diffuseIntensity, theNormal); 
		//	vec3 eyeVectorWorld = normalize(eyePosition - thePosition); 
		//   float specularity = clamp(dot(reflectedLight , eyeVectorWorld),0,1); 
	//		specularity = pow(specularity,500); 
		//	vec4 specularLight = vec4(specularity,specularity, 0 , alpha); 

			// the reason why we clamp is because the diffuseColor has negative values due to the dot product. Even after adding ab
		//myColor = ((clamp(diffuseColor,0,1) * 0.5f  + clamp(specularLight,0,1) * 0.5f)  + ambientLight ) * myColor   ;


		//	myColor = clamp(diffuseColor,0,1) * 0.5f  + clamp(specularLight,0,1) * 0.2f + vec4(ambientLight,alpha)  ;
			
		//	myColor = myColor * finalTexture ; 




			
		}
	}
}
