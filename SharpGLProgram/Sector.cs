using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using SharpGL;
using GLfloat = System.Single;
using GLuint = System.UInt32;
using GlmNet;
using SharpGL.VertexBuffers;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.Shaders;

// this class is used to store one "block" of the tunnel. There are multiple blocks, that are used to store and render in successive sequence 
namespace SharpGLProgram
{
    class Sector
    {
        private VertexBufferArray vertexBufferArray; // this is the vertex buffer array that stores the STATE of opengl , easier to run the codes 
        VertexBuffer positionBuffer, normalBuffer, colorBuffer, textureBuffer, sliceBuffer; // these are the vertex buffer objects 
        public IndexBuffer indexBuffer; // these holds the index buffers, the indices defining the triangles  
        public IndexBuffer lineIndexBuffer; // these holds the index buffers for the lines. 
        int pointPerSlice = 200; // this is the number of points on each slice.  
        int maxSlice = 1000; // this is the max number of slice that one block can hold 
        public int numberSlices = 0;  // the number of slices to date , slice 0 = first slice

          
       
        vec3[] standardPoints;  // this is the set of data that will be streaming in. I am using a fixed set for each contour, but will need to change to actual data 
        vec3[] colorPoints;   // color storage, randomized colors for now. 


        // for keeping track of textures 
        int startTextureImage; // the first slice in this hasN is linked to the texture image ID 
        int[] sliceIndexChangeTexture; // at every value of this array, is the slice number where the texture image changes. 


        public bool containTexture = true; // this is slightly misleading, all rendering will have textures (for consistencies). but if the texture is a pure color, then we cannot see the tunnel. so need to draw lines. 
        
        
        // there are 4 main attributes to define: position, normal, color, texurecoordinates 
        // this function just creates the space to put all the various data into. 
        public void generateGeometry(OpenGL gl, uint vertexAttribLocation, uint normalAttribLocation, uint colorAttribLocation, uint textureAttribLocation , uint sliceAttribLocation, int dataSizeCount, int maxSliceCount)
        {
            // these 2 variables are pre-set..  
            pointPerSlice = dataSizeCount;
            maxSlice = maxSliceCount; 

            // delete the buffer if it was defined previously. 
            if (vertexBufferArray != null)
                vertexBufferArray.Delete(gl); 

            vertexBufferArray = new VertexBufferArray();
            vertexBufferArray.Create(gl);
            vertexBufferArray.Bind(gl);
            initializeData(gl, vertexAttribLocation, normalAttribLocation, colorAttribLocation, textureAttribLocation, sliceAttribLocation);
            vertexBufferArray.Unbind(gl);

            generateStandardSlice(); // using this to generate a standard slice contour 
         }

        

        public void generateStandardSlice()
        {
            standardPoints = new vec3[pointPerSlice];
            colorPoints = new vec3[pointPerSlice]; 
        }
        
        // this just sets up the vertex attribute pointer stuff, so that they know the nature/type of the data that is coming  
        private void initializeData(OpenGL gl, uint vertexAttribLocation, uint normalAttribLocation, uint colorAttribLocation,  uint textureAttribLocation ,  uint sliceAttribLocation)
        {
            
            // simple, x number of points per slice, y number of slices. Total points required = xy 
            // the +1 point is for the last point, which is a duplicate. This is due to the texture coord, cannot reuse the first point. 
            int totalPossiblePoints = (pointPerSlice+1) * maxSlice;  

            int totalSize3Floats = (int) ( totalPossiblePoints *  sizeof(GLfloat) * 3 ) ;  // location, normal , color  attribute 
            int totalSize2Floats = (int)(totalPossiblePoints * sizeof(GLfloat) * 2);  // depth attribute 
            int totalSize1Floats = (int) ( totalPossiblePoints *  sizeof(GLfloat) * 1 ) ;  // depth attribute 
            int totalIndexSize = (int) (totalPossiblePoints * 6 * sizeof(GLuint));  // for every point on the slice, u need to create 2 triangles. Each triangle = 3 points, so mulitple by 6  
            int totalLineIndexSize = (int)(totalPossiblePoints * 4 * sizeof(GLuint));  // for every point on the slice, u need to create 2 lines. Each line = 2 points, so mulitple by 4  


            // used as a null pointer... 
            GCHandle handle = GCHandle.Alloc(null, GCHandleType.Pinned); // set to null if you want to send over data at a later timing...  
            IntPtr vertexPtr = handle.AddrOfPinnedObject();

            // used to clear all buffers. 
            clearAllBuffers(gl); 

            // create the vertexBuffer 
            positionBuffer = new VertexBuffer();
            normalBuffer = new VertexBuffer();
            colorBuffer = new VertexBuffer();
            textureBuffer = new VertexBuffer(); 
            sliceBuffer = new VertexBuffer(); 

              // setting the vertex buffers for the position 
            positionBuffer.Create(gl);
            positionBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize3Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(vertexAttribLocation, 3, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(vertexAttribLocation);

            // setting the vertex buffers for the normals 
            normalBuffer.Create(gl);
            normalBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize3Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(normalAttribLocation, 3, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(normalAttribLocation);
            
            // setting the vertex buffers for the color 
            colorBuffer.Create(gl);
            colorBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize3Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(colorAttribLocation, 3, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(colorAttribLocation);

            // setting the vertex buffers for the texture 
            textureBuffer.Create(gl);
            textureBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize2Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(textureAttribLocation, 2, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(textureAttribLocation);
            
            // setting the vertex buffers for the slice depth index 
            sliceBuffer.Create(gl);
            sliceBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize1Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(sliceAttribLocation, 1, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(sliceAttribLocation);
             

            //////////// now for the index buffer ////////////////////////// 
            indexBuffer = new IndexBuffer();
            indexBuffer.Create(gl);
            indexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalIndexSize , vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);

            lineIndexBuffer = new IndexBuffer();
            lineIndexBuffer.Create(gl);
            lineIndexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalLineIndexSize, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
        }

        // increase the array of sliceIndexChangeTexture by +1
        private void increaseSliceIndexCounter()
        {
            int[] temp = new int[sliceIndexChangeTexture.Count()];
            for (int a = 0; a < temp.Count(); a++)
                temp[a] = sliceIndexChangeTexture[a];

            sliceIndexChangeTexture = new int[temp.Count() + 1];
            for (int a = 0; a < temp.Count(); a++)
                sliceIndexChangeTexture[a] = temp[a];

            sliceIndexChangeTexture[temp.Count()] = numberSlices; // set the last one to indicate the current number slices. 

        }


        // this is where the resolution class calls the add one slice.. 
        public bool createOneSliceTexture(OpenGL gl, ref vec3[] slicePoints, int currentTextureIndex, double currentTextureCoord)
        {

            if ( numberSlices != 0) // the first slice dun need to test for texture change  
            {
                if (currentTextureIndex != startTextureImage + sliceIndexChangeTexture.Count() - 1) // new texture  
                     createOneSliceTextureMain(gl, ref slicePoints, currentTextureIndex - 1, 1.0f); // add one additional slice at the end with texture coords of 1.0f; 
            }

            return createOneSliceTextureMain(gl, ref slicePoints, currentTextureIndex, currentTextureCoord); //this is the current one that should be added 

        }



        // this version is for the texture Maps 
        // 2 information regarding this slice , only data points and color 
        // the current convention is this, the master slice 0 will contain no color info, master slice 0 will contain color information for triangles between slice 0 and 1 
        // i am assuming that the array for both are already in the final 200 points set
        public bool createOneSliceTextureMain(OpenGL gl, ref vec3[] slicePoints, int currentTextureIndex, double currentTextureCoord)
        {
            // for texture mapping indicators. 
            if (numberSlices == 0)
            {
                startTextureImage = currentTextureIndex;  // if this is the first.. set the startTextureImage 
                sliceIndexChangeTexture = new int[1]; 
                sliceIndexChangeTexture[0] = 0;  // the first one is always zero 
            }

            if (currentTextureIndex != startTextureImage + sliceIndexChangeTexture.Count() - 1) // new texture  
                 increaseSliceIndexCounter(); // increase the sliceIndexChangeTexture array 
            // end of texture mapping indicators 

            

            if (pointPerSlice != slicePoints.Count()) return false; // this cannot be the case.  
            if (numberSlices == maxSlice) return false;  // reached max slides ... exit 

            // all +1 is due to the texture coordinates, need to duplicate the last point 

            // size/space for 1 slice 
            int size3Floats = (int)((pointPerSlice + 1) * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 
            int size2Floats = (int)((pointPerSlice + 1) * 2 * sizeof(GLfloat)); // for variabls that have 3 floats 
            int size1Floats = (int)((pointPerSlice + 1) * sizeof(GLfloat)); // 1 floats variable, slice number. 

            // offset so far 
            int offsetSize3Floats = (int)((numberSlices) * size3Floats);  // for variabls that have 3 floats 
            int offsetSize2Floats = (int)((numberSlices) * size2Floats);  // for variabls that have 3 floats 
            int offsetSize1Floats = (int)((numberSlices) * size1Floats);       //1 floats variable, slice number. 

            // initializing all the required variables 
            vec3[] vertices = new vec3[(pointPerSlice + 1)];  // the vertices for the new slice 
            vec3[] normals = new vec3[(pointPerSlice + 1)];
            vec3[] colors = new vec3[(pointPerSlice + 1)];
            vec2[] textures = new vec2[(pointPerSlice + 1)];
            float[] sliceIndex = new float[(pointPerSlice + 1)];


            int a, b;
            vec3 centroid = new vec3(0, 0, 0);
            // standardPoints is always the "previous" slice  , slicePoints is the "current" slice 
            for (a = 0; a <= pointPerSlice; a++)
            {
                vertices[a] = slicePoints[a % pointPerSlice];  // last point will be equal to the first 
                sliceIndex[a] = numberSlices;
                colors[a] = new vec3(255,0,0); // set to red.... not going to be used, will be removed 

                centroid += vertices[a];  // computing the centroid, so can compute the normal, double counting the first... 

                textures[a].x = ((float)a) / pointPerSlice;
                textures[a].y = (float) currentTextureCoord; // passing in the texture coords.. 
            }

            /*
            vertices[0] = new vec3(-1.0f, 0.0f,1980.0f  );
            vertices[7] = new vec3(1.0f, 0.0f, 1980.0f);
            vertices[14] = new vec3(1.0f, 0.0f, 2060.0f);
            vertices[21] = new vec3(-1.0f, 0.0f, 2060.0f); 
                     
            textures[0] = new vec2(0, 1);
            textures[7] = new vec2(1, 1);
            textures[14] = new vec2(1, 0); 
            textures[21] = new vec2(0, 0); 
         */


            centroid /= (pointPerSlice + 1); // this is the centroid of this slice. 

            // now for normals 
            for (a = 0; a <= pointPerSlice; a++) // normal computation is the same for every single point ( actually this is wrong, will correct in future) 
            {
                normals[a] = vertices[a];
                normals[a] -= centroid;
                glm.normalize(normals[a]);
            }

            // now to sub the data into the gl_array_buffer     
            GCHandle handle = GCHandle.Alloc(vertices.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            IntPtr vertexPtr = handle.AddrOfPinnedObject();

            // physical position 
            positionBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);
            handle.Free();

            // normals 
            handle = GCHandle.Alloc(normals.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            normalBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);
            handle.Free();

            // colors 
            handle = GCHandle.Alloc(colors.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            colorBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);
            handle.Free();

            // textures 
            handle = GCHandle.Alloc(textures.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            textureBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize2Floats, size2Floats, vertexPtr);
            handle.Free();

            // slice index 
            handle = GCHandle.Alloc(sliceIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            sliceBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize1Floats, size1Floats, vertexPtr);
            handle.Free();


            numberSlices++;
            if (numberSlices == 1) return true; // exit since for slice 1 there is no triangles 

            // now we create the element arrays 
            
          
           /*
            GLuint[] elementIndex = new GLuint[6]; // go figure  
            elementIndex[0] = (GLuint)(0);
            elementIndex[1] = (GLuint)(7);
            elementIndex[2] = (GLuint)(14);

            elementIndex[3] = (GLuint)(14);
            elementIndex[4] = (GLuint)(0);
            elementIndex[5] = (GLuint)(21);

            handle = GCHandle.Alloc(elementIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            indexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0, 6 * sizeof(GLuint), vertexPtr);
            handle.Free();
        */ 

           
          
            // +1 because of texture mapping, duplicating the last point
            GLuint startPreviousSliceIndex = (GLuint)((numberSlices - 2) * (pointPerSlice + 1));
            GLuint[] elementIndex = new GLuint[(int)pointPerSlice * 2 * 3]; // go figure  , dun need to +1, because the number of indices remain the same 
            GLuint[] lineElementIndex = new GLuint[(int)pointPerSlice * 4]; // go figure  , dun need to +1, because the number of indices remain the same 


            int indexCount = 0;
            for (a = 0; a < pointPerSlice; a++)
            {
                b = a + 1;

                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + (pointPerSlice + 1) + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + b);

                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + (pointPerSlice + 1) + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + b);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + (pointPerSlice + 1) + b);
            }

            int sizePerSlice = (int)pointPerSlice * 2 * 3 * sizeof(GLuint);


            handle = GCHandle.Alloc(elementIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            indexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizePerSlice * (numberSlices - 2), sizePerSlice, vertexPtr);
            handle.Free();


            // is this for the line drawing

            int lineIndexCount = 0;
            for (a = 0; a < pointPerSlice; a++)
            {
                b = a + 1;

                lineElementIndex[lineIndexCount++] = (GLuint)(startPreviousSliceIndex + a);
                lineElementIndex[lineIndexCount++] = (GLuint)(startPreviousSliceIndex + pointPerSlice+1 + a);


                lineElementIndex[lineIndexCount++] = (GLuint)(startPreviousSliceIndex + a);

                // too many horizontal lines being drawn... reducing it 
                if ( numberSlices % 20 == 0 ) 
                    lineElementIndex[lineIndexCount++] = (GLuint)(startPreviousSliceIndex + b);
                else
                    lineElementIndex[lineIndexCount++] = (GLuint)(startPreviousSliceIndex + a);

                
            }

            int lineSizePerSlice = (int)pointPerSlice * 4  * sizeof(GLuint);


            handle = GCHandle.Alloc(lineElementIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            lineIndexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, lineSizePerSlice * (numberSlices - 2), lineSizePerSlice, vertexPtr);
            handle.Free();




            if (numberSlices == maxSlice)
                return false;  // reached max slides ... exit 
         
            return true;
        }



        
        // this just set the previous slice. 
        public void createPreviousSlice(OpenGL gl, ref vec3[] slicePoints, ref vec3[] colorData)
        {
            for (int a = 0; a < pointPerSlice; a++)
            {
                standardPoints[a] = slicePoints[a];
                colorPoints[a] = colorData[a];
            }

        }

        // set the number of slices = 0 
        public void reset()
        {
            numberSlices = 0; // reset to zero 
        }

        // clear all the used openGL buffers. 
        public void clearAllBuffers(OpenGL gl)
        {
            if (positionBuffer != null)
                deleteBuffer(gl, positionBuffer.VertexBufferObject);

            if (normalBuffer != null)
                deleteBuffer(gl, normalBuffer.VertexBufferObject);

            if (colorBuffer != null)
                deleteBuffer(gl, colorBuffer.VertexBufferObject);

            if (sliceBuffer != null)
                deleteBuffer(gl, sliceBuffer.VertexBufferObject);

            if (indexBuffer != null)
                deleteBuffer(gl, indexBuffer.IndexBufferObject);
            
            if (lineIndexBuffer != null)
                deleteBuffer(gl, lineIndexBuffer.IndexBufferObject);


            reset(); 


        }

        // used as a helper class to delete the buffer. 
        private void deleteBuffer(OpenGL gl, uint indexID)
        {
            uint[] ids = new uint[1];
            ids[0] = indexID;
            gl.DeleteBuffers(1, ids);
        }
               
        // Gets the vertex buffer array.
        public VertexBufferArray VertexBufferArray
        {
            get { return vertexBufferArray; }
        }

        // get the total triangle count for rendering 
        public int totalTriangles()
        {
            return (numberSlices -1) * (pointPerSlice * 2) * 3;
        }

        public int totalLines()
        {
            return (numberSlices - 1) * (pointPerSlice * 2) * 2;
        }



        // si = slice index 
        int getTriangleDraw(int si )
        {
            return si * pointPerSlice * 6; 
        }

        
        public void Render(OpenGL gl, ShaderProgram sp, ref uint[] texNameID)
        {

            if (numberSlices == 0) return; 

            // bind both buffers 
            VertexBufferArray.Bind(gl);
            indexBuffer.Bind(gl);
            
            IntPtr ptr; 
      
            for (int a = 0; a < sliceIndexChangeTexture.Count(); a++)
            {
                if ( startTextureImage+a >= texNameID.Count() )
                    gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[texNameID.Count()-1]); // this is the texture ID  
                else
                    gl.BindTexture(OpenGL.GL_TEXTURE_2D, texNameID[startTextureImage+a]); // this is the texture ID  


                int b = a + 1;

                // this is the offset value 
                // 1. set the ptr 
                ptr = new IntPtr(getTriangleDraw(sliceIndexChangeTexture[a]) * sizeof(GLuint));


                // ptr is the offset start point, glDrawElement determine how many more triangles to draw after that offset point. 
                if (b == sliceIndexChangeTexture.Count()) // reaches the end, from here to end, call_1 the current texture
                      gl.DrawElements(SharpGL.OpenGL.GL_TRIANGLES, getTriangleDraw(numberSlices - sliceIndexChangeTexture[a]), OpenGL.GL_UNSIGNED_INT, ptr);
                else
                   // draw from SLICE sliceIndexChangeTexture[a] to SLICE sliceIndexChangeTexture[b]-1  
                    gl.DrawElements(SharpGL.OpenGL.GL_TRIANGLES, getTriangleDraw(sliceIndexChangeTexture[b]-sliceIndexChangeTexture[a]), OpenGL.GL_UNSIGNED_INT, ptr);
            }
      
            
            lineIndexBuffer.Bind(gl);

            if (containTexture == false)
            {
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0); // this is the texture ID  
                gl.DrawElements(SharpGL.OpenGL.GL_LINES, totalLines(), OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
            }
           


             
          
        }



        /*
        public void RenderLines(OpenGL gl)
        {
            lineIndexBuffer.Bind(gl);

            if (containTexture == false)
            {
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0); // this is the texture ID  
                gl.DrawElements(SharpGL.OpenGL.GL_LINES, totalLines(), OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
            }
        }
        */ 


    }
}
