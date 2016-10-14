using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GlmNet;
using System.Linq;
using System.Text;
using GLuint = System.UInt32;
using GLfloat = System.Single;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.Shaders;

namespace SharpGLProgram
{

    // this class performs the loading of the shaders, and manages the various different level of details renderings. 
    // Another use of this class is to manage the centroid location at each depth location. 
    // Yet another use of this class is to manage the texture mapping 
    class Scene
    {

        System.IO.StreamWriter objWriter;

        Random rnd;
        vec3[] colorData; // this is just to provide some random color data.  
        int fullDataSize = 200;  // i am intializing it to 200, can change to other values if necessary. 
        int detailLayers = 5; 
        const uint positionAttribute = 0;
        const uint normalAttribute = 1;
        const uint colorAttribute = 2;
        const uint textureAttribute = 3;
        const uint sliceAttribute = 4;

        OpenGL glContainer;  // used as a reference to store the opengGL container 

        // texture information 
        int textureROWS = 16000, textureCOLS = 200;  // number of rows and columns of the texture maps . 16384 seems like the max resolution you can have. 
        byte[] textureImage; // only 1 is required... 
        double[][] textureDepth; // does not refer to the depth, but the tunnel depth that each line of texture is referring to. 
        public uint[] texNameID; // name of the textureUnit 
        bool containTexture = false; // to indicate if this rendering contains textures 
        bool streamingMode = false;
        bool createdTextures = false; 
        int lastTextureImageRowSize;
     
        // running computation of the texture coordinates based on depth. These 2 values are only used internally to compute the value. Used for searching and indexing. 
        int currentTextureIndex = 0;
        int currentTextureRowCounter = 0;

        // these variables are used for the generation of streaming texture maps. 
        int streamTextureIndex = 0;
        int streamTextureRowCounter = 0; 



        vec3[] colorPosition;

        bool orientation = true; // true = upwards orientation 

        public void Initialize(OpenGL gl)
        {

            objWriter = new System.IO.StreamWriter(@"scene_debug.txt"); // used as a means to write deugging data on the screen 

            glContainer = gl; 
            rnd = new Random(); // random generator. I am using this for the sake of generating random colors. Once the color data is here, we can remove this. 

            //  We're going to specify the attribute locations for the position and normal, 
            //  so that we can force both shaders to explicitly have the same locations.
           
            var attributeLocations = new Dictionary<uint, string>
            {
                {positionAttribute, "position"},
                {normalAttribute, "normal"},
                {colorAttribute, "color"},
                {textureAttribute, "TexCoord"},
                {sliceAttribute, "slice"}
            };


            // Create the per pixel shader.
            // if you ever wish to create a new vert or frag file, remember to set the bulid properties to "embedded source" 
            shaderID = new ShaderProgram();
            shaderID.Create(gl,
                ManifestResourceLoader.LoadTextFile(@"Shaders\vertexShader.vert"),
                ManifestResourceLoader.LoadTextFile(@"Shaders\fragmentShader.frag"), attributeLocations);
            shaderID.Bind(gl); // bind the shaders to the program. 


            resolution = new Resolution[detailLayers]; // use 4 layers... 
            for (int a = 0; a < detailLayers; a++)
               resolution[a] = new Resolution();

            // now to manually pre-set the 4 different levels of details 
            resolution[0].Initialize(gl, 1, fullDataSize, 30000); // highest level of detail , 30k of slices for each b, , lock 
            resolution[1].Initialize(gl, 2, fullDataSize, 60000);
            resolution[2].Initialize(gl, 5, fullDataSize, 120000);
            resolution[3].Initialize(gl, 20, fullDataSize, 400000);
            resolution[4].Initialize(gl, 50, fullDataSize, 800000);




            colorPosition = new vec3[256];


            for (int a = 0; a < 256; a++)
                colorPosition[a] = new vec3((float)a / 255, (float) a / 255, (float) a / 255); 
                        
            gl.Uniform3(shaderID.GetUniformLocation(gl, "colorPosition"), 768, colorPosition.SelectMany(v => v.to_array()).ToArray());
            gl.Uniform1(shaderID.GetUniformLocation(gl, "colorCountRange"), 255);
            gl.Uniform1(shaderID.GetUniformLocation(gl, "lowCutoff"), 0.0f);
            gl.Uniform1(shaderID.GetUniformLocation(gl, "highCutoff"), 1.0f); 

            colorData = new vec3[fullDataSize];
        }

        public void close()
        {
            objWriter.Close();
            clearMemory(); 
        }

        public void setOrientation(bool newo)
        {
            orientation = newo; 
        }


        public void clearMemory()
        {
            // clear vertex and index buffers of all the resolutions 
            for (int a = 0; a < 5; a++)
                resolution[a].clearMemory(glContainer); 

            // clear texture memory
            glContainer.BindTexture(OpenGL.GL_TEXTURE_2D,0); // unbind first 


            if (createdTextures == true)
            {
                for (int a = 0; a <= streamTextureIndex; a++)
                {
                    uint[] ids = new uint[1];
                    ids[0] = texNameID[a];
                    glContainer.DeleteTextures(1, ids);
                }
            }

            //delete shaders 
            shaderID.Delete(glContainer); 


            // reset counters 
            currentTextureIndex = 0;
            currentTextureRowCounter = 0;

            // these variables are used for the generation of streaming texture maps. 
            streamTextureIndex = 0;
           streamTextureRowCounter = 0; 



        }




        public void castColors(OpenGL gl, ref vec3[] cp, int colorCountRange, float lowco, float highco)
        {
            colorPosition = new vec3[256];
            for (int a = 0; a < cp.Count(); a++) // both should be 256 count 
            {
                colorPosition[a] = cp[a];
                colorPosition[a] /= 255.0f; 
            }

            gl.Uniform3(shaderID.GetUniformLocation(gl, "colorPosition"), 768, colorPosition.SelectMany(v => v.to_array()).ToArray());
            gl.Uniform1(shaderID.GetUniformLocation(gl, "colorCountRange"), colorCountRange);
            gl.Uniform1(shaderID.GetUniformLocation(gl, "lowCutoff"), lowco);
            gl.Uniform1(shaderID.GetUniformLocation(gl, "highCutoff"), highco); 

        }





        // reset the tunnel rendering to a new data count. 
        public void reInitialize(OpenGL gl, int fullDataCount)
        {
            fullDataSize = fullDataCount;
            colorData = new vec3[fullDataSize]; // set the size of the colordata to the new size. 

            resolution[0].reInitialize(gl, fullDataSize, 30000);
            resolution[1].reInitialize(gl, fullDataSize, 60000);
            resolution[2].reInitialize(gl, fullDataSize, 120000);
            resolution[3].reInitialize(gl, fullDataSize, 400000);
            resolution[4].reInitialize(gl, fullDataSize, 800000); 
        
        }

        // generate a 1x1 grey Colored textured map 
        public void generatePlainTexture(OpenGL gl)
        {
            containTexture = false; // just to confirm 
            // just a 1 byte texture 
            textureROWS = 1; 
            textureCOLS = 1;

            textureImage = new byte[textureROWS * textureCOLS * 4]; // create a space for a new texture image  
            textureImage[0] = System.Convert.ToByte(128);
            textureImage[1] = System.Convert.ToByte(128);
            textureImage[2] = System.Convert.ToByte(128);
            textureImage[3] = System.Convert.ToByte(128);

            // generate textures 
            texNameID = new uint[1];
            gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
            gl.GenTextures(1, texNameID);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[0]);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureCOLS, textureROWS, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, textureImage);

            createdTextures = true; 

            // setting textures 
            for (int a = 0; a < 5; a++)
                resolution[a].setTextureContain(false); // no texture, should draw grid lines. 

        }




        // Call this to prepare the texture data for streaming 
        // colSize is the size of the color data 
        public void generatePrepareStreamingTexture(OpenGL gl, int colSize, bool upwards)
        {
            // test if data is coherent 
            if (colSize != 0)
            {
                containTexture = true;
                streamingMode = true; 
            }
            else
                return; // exit from this function, no textures for this rendering 

            textureCOLS = colSize;
            int[] textureMaxSize = { 0 };
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, textureMaxSize);
            textureROWS = textureMaxSize[0] - textureMaxSize[0] % 1000; // lets keep it to a 1000s value.. should be 16k 

            // now lets see how many texture maps do I need to contain all 
            // 100 texture is enough for now. will be 1.6 million contours... 
            int noOfStreamingTextures = 100;
            texNameID = new uint[noOfStreamingTextures];
            // this will be the depth counter information .. used for reference. 
            textureDepth = new double[noOfStreamingTextures][];
            
            // dun create the individual double first.... 
            streamTextureIndex = 0;
            streamTextureRowCounter = 0;

            orientation = upwards; // this set the orientation direction of the tunnel . 
        }


        // everytime a new contour is added, dump all the current stack of color information  
        public void dumpAllColorInformation(OpenGL gl, ref CurveDataMgt cdm)
        {
            for (int a = 0; a < cdm.dataList.Count(); a++)
               addOneLayerTexture(gl, cdm.dataList[a]);

            bindCurrentTexure(gl);
            cdm.clearPartial();
        }



        public void addOneLayerTexture(OpenGL gl, DataParameter colorInformation )
        {
            if (streamTextureRowCounter == 0) // start of a new texure 
            {
                textureDepth[streamTextureIndex] = new double[textureROWS];  // create the depth information 
                textureImage = new byte[textureROWS * textureCOLS * 4]; // create a space for a new texture image  

                // sets up all the texture information for the initialization process 
                uint[] ids = new uint[1];
                gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
                gl.GenTextures(1, ids);
                texNameID[streamTextureIndex] = ids[0];
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[streamTextureIndex]);

                uint[] array = new uint[] { OpenGL.GL_REPEAT };
                gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, array);
                gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, array);

                // only GL_NEAREST gives the pixellated effect that is required, the rest will smooth the data, cannot use. 
                array[0] = OpenGL.GL_NEAREST;
                gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, array);
                array[0] = OpenGL.GL_NEAREST;
                gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, array);


                createdTextures = true; 

            }

          //  if ( orientation == true )
           //     textureDepth[streamTextureIndex][streamTextureRowCounter] = colorInformation.depth; // stores the depth 
           // else
             
            textureDepth[streamTextureIndex][streamTextureRowCounter] = colorInformation.depth; // stores the depth , negative 

            setTextureInformation(ref colorInformation.dataP, streamTextureRowCounter); // passes the color information into texureimage , one row of information 
          

            streamTextureRowCounter++;

            if (streamTextureRowCounter == textureROWS) // once u hit the limit, go for the last one. 
            {
                bindCurrentTexure(gl); // if end already, bind to texture.. dun need to wait 
                streamTextureRowCounter = 0;
                streamTextureIndex++; 
            }

        }

        // this will be called everytime a new contour is added 
        public void bindCurrentTexure(OpenGL gl)
        {
            // now we set the texture , this would happen every time the data is streamed in. // we can optimized to only do it once it surpasses the physical information.  
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[streamTextureIndex]);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureCOLS, textureROWS, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, textureImage);

        }




        // this function is used in the event where you have all the colorinformation first. i.e. For loading purposes. 
        // Each element of BHIOP contains 2 information, 1 is depth information, 2 is an 1D array of sensor
        // this function is catered for load from file, meaning that all the color information is known beforehand. 
        public void generateTextures(OpenGL gl, ref CurveDataMgt colorInformation)
        {
            int a , b , c ; 
            // test if data is coherent 
            if (colorInformation.getCount() != 0)
                containTexture = true;
            else
                return; // exit from this function, no textures for this rendering 
            
            // set the number of texture columns, this is fixed throughout 
            textureCOLS = colorInformation.getDataCount(); 
            if (textureCOLS == 0) return; // cannot be zero also. 

            int[] textureMaxSize = { 0 };
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, textureMaxSize);
            textureROWS = textureMaxSize[0] - textureMaxSize[0] % 1000; // lets keep it to a 1000s value.. should be 16k 

            // now lets see how many texture maps do I need to contain all 
            texNameID = new uint[colorInformation.getCount() / textureROWS + 1];

            // this will be the depth counter information .. used for reference. 
            textureDepth = new double[texNameID.Count()][];
            for (a = 0; a < texNameID.Count(); a++)
                textureDepth[a] = new double[textureROWS]; 
 
             // now we fill in all the information. 
            a = 0; // used as the full data counter 
            b = 0; // used as a counter for each texture 
            c = 0; // texureID counter 

            while (a < colorInformation.getCount()) 
            {
                if (b == 0) // create a new texure 
                      textureImage = new byte[textureROWS * textureCOLS * 4]; // create a space for a new texture image  
                
                textureDepth[c][b] = colorInformation.getDepth(a); // stores the depth 
                setTextureInformation(ref colorInformation.dataList[a].dataP, b); // passes the color information into texureimage 

                b++;
                a++;
                if (b == textureROWS) // once u hit the limit, go for the last one. 
                {
                    // time to set the Opengl texture information 
                    loadTextureMaps(gl, c); 
                    b = 0; // set to zero; next texure image follows 
                    c++; 
                }
            }

            lastTextureImageRowSize = b; // this is the limit of the number of the rows in the last texture image. This is the size, the actual index is b-1
                                      
        }

        // used to load the textureMaps 
        private void loadTextureMaps(OpenGL gl, int textureIndex)
        {
            uint[] ids = new uint[1];
            gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
            gl.GenTextures(1, ids);
            texNameID[textureIndex] = ids[0];
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[textureIndex]);

            uint[] array = new uint[] { OpenGL.GL_REPEAT };
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, array);
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, array);

            // only GL_NEAREST gives the pixellated effect that is required, the rest will smooth the data, cannot use. 
            array[0] = OpenGL.GL_NEAREST;
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, array);
             array[0] = OpenGL.GL_NEAREST;
            gl.TexParameterI(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, array);

            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureCOLS, textureROWS, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, textureImage);
             //gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D); // mipmap is not useful as the pixellated effect is required 

            textureImage = new byte[1]; // remove memory usage... 

        }

        // sets one line of texture information 
        // this is where to change if the color dialog box has more information. 
        private void setTextureInformation(ref double[] colorData, int currentTextureRow)
        {
            int baseValue = currentTextureRow * textureCOLS * 4;

            // i am assuming that colorData has the same size as textureCOLS 
            for (int i = 0; i < textureCOLS; i++)
            {

                if (colorData[i] < 0.0f) colorData[i] = 0.0f;
                if (colorData[i] > 255.0f) colorData[i] = 255.0f; 

                // we only have 1 single value to represent a color, so we converting it to a greyscale. 
                textureImage[baseValue + i * 4 + 0] = System.Convert.ToByte(255-colorData[i]);
                textureImage[baseValue + i * 4 + 1] = System.Convert.ToByte(255- colorData[i]);
                textureImage[baseValue + i * 4 + 2] = System.Convert.ToByte(255- colorData[i]);
                textureImage[baseValue + i * 4 + 3] = System.Convert.ToByte(255);
            }
        }


        // based on the current slice depth value, use this to determine what is the texure value. 
        private void computeTextureCoordinates(double currentDepthLevel, ref double textureCoords )
        {

            if (containTexture == false)  // no texture.. just return 0.0f; 
            {
                textureCoords = 0.0f; // reaches the end. 
                return;
            }

            if (currentTextureIndex == textureDepth.Count()) // end of texture index size 
            {
                textureCoords = 1.0f; // reaches the end. 
                return; 
            }


            float multipler = 1.0f ;

            if (orientation == false) // need to negate the currentDepthLevel 
                multipler = -1.0f;



            // some notes: For load from files (cff files), the depth is always in one direction, so we dun have to worry about the orientation for loading version. 
            // since for load from file, the textures is alreaded loaded from the start, we dun have to worry about it exceeding the limit. 
            // i am assuming that the depth value will continue to increase.. if negative value have to adjust accordingly. 
            while (currentDepthLevel * multipler > textureDepth[currentTextureIndex][currentTextureRowCounter] * multipler)
            {
                currentTextureRowCounter++; // searches the next one 
                if (currentTextureIndex != textureDepth.Count() - 1) // not the last one 
                {
                    if (currentTextureRowCounter == textureROWS)
                    {
                        currentTextureRowCounter = 0; // reset to zero 
                        currentTextureIndex++; // go to next texture 
                    }
                }
                else // this is the last one 
                {
                    if (currentTextureRowCounter == lastTextureImageRowSize) // uses the last one. 
                    {
                        currentTextureRowCounter = 0; // reset to zero 
                        currentTextureIndex++; // go to next texture 
                        textureCoords = 1.0f; // reaches the end. 
                        return; 
                    }
                }
            }

            textureCoords = (float) currentTextureRowCounter / textureROWS;      
            return; 
        }

        // given a current Depth level, we can use this to compute the texture index, and the position of the texture value. 
        public void createOneSlice(OpenGL gl, GLfloat[] ContourPositions, double currentDepthLevel)
        {
            double textureCoord = 0.0f ;
            computeTextureCoordinates(currentDepthLevel, ref textureCoord);  // get texture coords  , if there is no texture information, this is always 0  coords 

            for (int a = 0; a < detailLayers; a++)
               resolution[a].createOneSliceTexture(gl, ContourPositions, currentTextureIndex, textureCoord);
        }


        public void Render(OpenGL gl, vec4 translationVector)
        {

       //     applyStartLines(gl, -1);

            // just take the x and y. 
            float distance = (float)Math.Sqrt(translationVector.x * translationVector.x + translationVector.y * translationVector.y);

            if (distance < 200.0f)
                resolution[0].RenderTexture(gl, shaderID, ref texNameID);
            else if (distance < 400.0f)
                resolution[1].RenderTexture(gl, shaderID, ref texNameID);
            else if (distance < 800.0f)
                resolution[2].RenderTexture(gl, shaderID, ref texNameID);
            else if (distance < 1600.0f)
                resolution[3].RenderTexture(gl, shaderID, ref texNameID);
            else
                resolution[4].RenderTexture(gl, shaderID, ref texNameID);



        //    applyStartLines(gl, 1);
          //  applyColorTransformation(gl, new vec3(1, 1, 1));

           /*

            if (distance < 200.0f)
                resolution[0].RenderLines(gl);
            else if (distance < 400.0f)
                resolution[1].RenderLines(gl);
            else if (distance < 800.0f)
                resolution[2].RenderLines(gl);
            else if (distance < 1600.0f)
                resolution[3].RenderLines(gl);
            else
                resolution[4].RenderLines(gl);
            */ 

  
        }


        // while waiting for the texture to come in, the tunnel curve is being filled up
        // the user might wait up to 2-3 mins for the textures to come in, so we are rendering the curves first. 
        public void drawUntexturedTunnel(OpenGL gl, CurveDataMgt tunnelCurve)
        {
			gl.Disable(OpenGL.GL_DEPTH_TEST);
            // apply a color of bright grey
            applyColorTransformation(gl, new vec3(0.8f, 0.8f, 0.8f), 0.6f);

            int base1, base2; 

            gl.Begin(OpenGL.GL_LINES);
            for (int a = 0; a < tunnelCurve.dataList.Count(); a = a+5)
            {
                for (int b = 0; b < tunnelCurve.dataList[a].dataP.Count() / 3 ; b++)
                {
                    int c = (b + 1) % (tunnelCurve.dataList[a].dataP.Count() / 3);

                    base1 = b * 3;
                    base2 = c * 3;
                    
                    gl.Vertex(tunnelCurve.dataList[a].dataP[base1], tunnelCurve.dataList[a].dataP[base1 + 1], tunnelCurve.dataList[a].dataP[base1 + 2]);
                    gl.Vertex(tunnelCurve.dataList[a].dataP[base2], tunnelCurve.dataList[a].dataP[base2 + 1], tunnelCurve.dataList[a].dataP[base2 + 2]);
                }
            }
            gl.End();

			// apply semi-transparent grey color for in-betweens
			applyColorTransformation(gl, new vec3(1.0f, 1.0f, 1.0f), 0.6f);
			int tempCount;
			gl.Begin(OpenGL.GL_TRIANGLE_STRIP);
			for (int a = 0; a < tunnelCurve.dataList.Count()-5; a+=5)
			{
				tempCount = tunnelCurve.dataList[a].dataP.Count() / 3;
				base2 = a + 5;
				for (int b = 0; b < tempCount; b++)
				{
					base1 = b * 3;

					gl.Vertex(tunnelCurve.dataList[a].dataP[base1], tunnelCurve.dataList[a].dataP[base1 + 1], tunnelCurve.dataList[a].dataP[base1 + 2]);
					gl.Vertex(tunnelCurve.dataList[base2].dataP[base1], tunnelCurve.dataList[base2].dataP[base1 + 1], tunnelCurve.dataList[base2].dataP[base1 + 2]);
				}
				gl.Vertex(tunnelCurve.dataList[a].dataP[0], tunnelCurve.dataList[a].dataP[1], tunnelCurve.dataList[a].dataP[2]);
				gl.Vertex(tunnelCurve.dataList[base2].dataP[0], tunnelCurve.dataList[base2].dataP[1], tunnelCurve.dataList[base2].dataP[2]);
			}
			gl.End();

			gl.Enable(OpenGL.GL_DEPTH_TEST);
        }


        public void applyLights(OpenGL gl, vec3 diffuseLights, vec3 eyePosition)
        {
            shaderID.SetUniform3(gl, "diffuseLightWorld", diffuseLights.x, diffuseLights.y, diffuseLights.z);
            shaderID.SetUniform3(gl, "ambientLight", 0.2f, 0.2f, 0.2f);
            shaderID.SetUniform3(gl, "eyePosition", eyePosition.x, eyePosition.y, eyePosition.z);

        }



        public void applyTransformation(OpenGL gl, mat4 projectionMatrix, mat4 ModelMatrix, mat4 fullTransform)
        {
            shaderID.SetUniformMatrix4(gl, "ProjectionMatrix", projectionMatrix.to_array());
            shaderID.SetUniformMatrix4(gl, "ModelMatrix", ModelMatrix.to_array());
            shaderID.SetUniformMatrix4(gl, "fullTransformaMatrix", fullTransform.to_array());
        }

        public void applyColorTransformation(OpenGL gl, vec3 setNewColor, float alpha=1.0f)
        {
            shaderID.SetUniform3(gl, "setColor", setNewColor.x, setNewColor.y, setNewColor.z);
			shaderID.SetUniform1(gl, "alpha", alpha);
        }

        public void applyStartLines(OpenGL gl, float startDraw)
        {
            shaderID.SetUniform1(gl, "drawingLines", startDraw);
        }
        
        // internal function to create a set of random colors, once COSL provide the  
        void generateRandomColor()
        {
             for (int a = 0; a < fullDataSize; a++)
            {
                colorData[a].x = (float)rnd.Next(100) / 100.0f;
                colorData[a].y = (float)rnd.Next(100) / 100.0f;
                colorData[a].z = (float)rnd.Next(100) / 100.0f;
            }
        }
       
        public void applyPicking(OpenGL gl, float depthIndex)
        {
            shaderID.SetUniform1(gl, "depthIndex", (float)depthIndex);
        }

        public void binding(OpenGL gl)
        {
            shaderID.Bind(gl);
        }

        public void unbinding(OpenGL gl)
        {
            shaderID.Unbind(gl);
        }
        
        //  The shaders we use.
        private ShaderProgram shaderID;
        private Resolution[] resolution; 
      
    }
}
