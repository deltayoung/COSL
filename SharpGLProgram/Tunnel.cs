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

namespace SharpGLProgram
{
    class Tunnel
    {
        private VertexBufferArray vertexBufferArray; // this is the vertex buffer array that stores the STATE of opengl , easier to run the codes 
        VertexBuffer positionBuffer, normalBuffer, colorBuffer, textureBuffer, sliceBuffer; // these are the vertex buffer objects 
        public IndexBuffer indexBuffer; // these holds the index buffers, the indices defining the triangles  
        public IndexBuffer HorindexBuffer; // these holds the index buffers, the indices defining the lines
        public IndexBuffer VertindexBuffer; // these holds the index buffers, the indices defining the lines
        private const int pointPerSlice = 30; // this is the number of points on each slice.  
        private const int maxSlice = 100000; // set to 10 thousand for the time being, see how we can change it in future. 
        int numberSlices = 0;  // the number of slices to date , slice 0 = first slice, initialises to -1 first 

        // for buffering
        //GLuint[] myIndexBufferID;
        //GLuint[] myIndexLineBufferID1;
        //GLuint[] myIndexLineBufferID2;

        // there are 4 main attributes to define: position, normal, color, texurecoordinates 
        // this function just creates the space to put all the various data into. 
        public void generateGeometry(OpenGL gl, uint vertexAttribLocation, uint normalAttribLocation, uint colorAttribLocation, uint textureAttribLocation, uint sliceAttribLocation)
        {
            initializeData(gl, vertexAttribLocation, normalAttribLocation, colorAttribLocation, textureAttribLocation, sliceAttribLocation);
        }


        // this just sets up the vertex attribute pointer stuff, so that they know the nature/type of the data that is coming  
        private void initializeData(OpenGL gl, uint vertexAttribLocation, uint normalAttribLocation, uint colorAttribLocation, uint textureAttribLocation, uint sliceAttribLocation)
        {

            int totalSize3Floats = (int) (maxSlice * pointPerSlice * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 
            int totalSize2Floats = (int) (maxSlice * pointPerSlice * 2 * sizeof(GLfloat)); // 2 floats variable, textures. 
            int totalSize1Floats = (int)(maxSlice * pointPerSlice * 1 * sizeof(GLfloat)); // 1 floats variable, depth Slice Index. 
            int totalIndexSize = (int) ((pointPerSlice * 2 * 3) * ( maxSlice-1) * sizeof(GLuint));  // think about it.... 

            //IntPtr hglobal = Marshal.AllocHGlobal(1000000000);
            //Marshal.FreeHGlobal(hglobal)

            // used as a null pointer... 
            GCHandle handle = GCHandle.Alloc(null, GCHandleType.Pinned); // set to null if you want to send over data at a later timing...  
            IntPtr vertexPtr = handle.AddrOfPinnedObject();

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

                
            /////////// hori line index buffer ////////////////
            HorindexBuffer = new IndexBuffer();
            HorindexBuffer.Create(gl);
            HorindexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalSize2Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);

            /////////// hori line index buffer ////////////////
            VertindexBuffer = new IndexBuffer();
            VertindexBuffer.Create(gl);
            VertindexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalSize2Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);

        }

/*        private void initializeData(OpenGL gl, uint vertexAttribLocation, uint normalAttribLocation, uint colorAttribLocation, uint textureAttribLocation)
        {

            //int totalSize3Floats = (int)(maxSlice * pointPerSlice * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 
            //int totalSize2Floats = (int)(maxSlice * pointPerSlice * 2 * sizeof(GLfloat)); // 2 floats variable, textures. 
            //int totalIndexSize = (int)((pointPerSlice * 2 * 3) * (maxSlice - 1) * sizeof(GLuint));  // think about it.... 

            //IntPtr hglobal = Marshal.AllocHGlobal(1000000000);
            //Marshal.FreeHGlobal(hglobal)

            // used as a null pointer... 
            GCHandle handle = GCHandle.Alloc(null, GCHandleType.Pinned); // set to null if you want to send over data at a later timing...  
            IntPtr vertexPtr = handle.AddrOfPinnedObject();

            // create the vertexBuffer 
            positionBuffer = new VertexBuffer();
            normalBuffer = new VertexBuffer();
            colorBuffer = new VertexBuffer();
            textureBuffer = new VertexBuffer();

            int vertexBytes = Marshal.SizeOf(typeof(GLfloat));
            int totalSize3Floats = (int)(maxSlice * pointPerSlice * 3 * vertexBytes);
            int totalSize2Floats = (int)(maxSlice * pointPerSlice * 2 * vertexBytes); // 2 floats variable, textures. 
            int totalIndexSize = (int)((pointPerSlice * 2 * 3) * (maxSlice - 1) * vertexBytes);  // think about it.... 

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


            //////////// now for the index buffer ////////////////////////// 
            indexBuffer = new IndexBuffer();
            indexBuffer.Create(gl);
            indexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalIndexSize, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);


            /////////// hori line index buffer ////////////////
            HorindexBuffer = new IndexBuffer();
            HorindexBuffer.Create(gl);
            HorindexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalSize2Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);

            /////////// hori line index buffer ////////////////
            VertindexBuffer = new IndexBuffer();
            VertindexBuffer.Create(gl);
            VertindexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalSize2Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);

        }
*/

        // for the time being, we will ignore the texture and color data. 
        public void createOneSlice(OpenGL gl, GLfloat[] ContourPositions)
        {
            if (numberSlices > maxSlice - 2) return;  // reached max slides ... exit 

            int offsetSize3Floats = (int)(numberSlices * pointPerSlice * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 
            int offsetSize2Floats = (int)(numberSlices * pointPerSlice * 2 * sizeof(GLfloat)); // 2 floats variable, textures. 
            int offsetSize1Floats = (int)(numberSlices * pointPerSlice * sizeof(GLfloat)); //1 floats variable, slice number. 

            int size3Floats = (int)(pointPerSlice * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 
            int size2Floats = (int)(pointPerSlice * 2 * sizeof(GLfloat)); // 2 floats variable, textures. 
            int size1Floats = (int)(pointPerSlice * sizeof(GLfloat)); // 1 floats variable, slice number. 

            vec3[] vertices = new vec3[pointPerSlice];  // the vertices for the new slice 
            vec3[] normals = new vec3[pointPerSlice];
            vec3[] colors = new vec3[pointPerSlice];
            vec2[] textures = new vec2[pointPerSlice];
            float[] sliceIndex = new float[pointPerSlice]; 

            int a , b ; 
                   
            for (a = 0; a < pointPerSlice; a++)
            {
                vertices[a].x = (float)ContourPositions[3*a];
                vertices[a].y = (float)ContourPositions[3 * a + 1];
                vertices[a].z = (float)ContourPositions[3 * a + 2];

                normals[a] = vertices[a];
                normals[a].z = 0;
                glm.normalize(normals[a]);  // this is the normal at that point 

                colors[a] = new vec3(0, 1, 1);  // some random colors 
                textures[a] = new vec2(0, 0);    // might have to rethink how we define textures 
                sliceIndex[a] = (float)numberSlices; // set to the slice number 
            }

            
            // now to sub the data into the gl_array_buffer     
            GCHandle handle = GCHandle.Alloc(vertices.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            IntPtr vertexPtr = handle.AddrOfPinnedObject();
            positionBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);

            handle.Free();

            handle = GCHandle.Alloc(normals.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            normalBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);

            handle.Free();

            handle = GCHandle.Alloc(colors.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            colorBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);

            handle.Free();

            handle = GCHandle.Alloc(textures.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            textureBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize2Floats, size2Floats, vertexPtr);

            handle.Free();

            handle = GCHandle.Alloc(sliceIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            sliceBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize1Floats, size1Floats, vertexPtr);
            handle.Free(); 

            numberSlices++;
            if (numberSlices == 1) return; // exit since for slice 1 there is no triangles 

            // now we create the element arrays 

            GLuint startPreviousSliceIndex =(GLuint)((numberSlices - 2) * pointPerSlice) ;
            GLuint[] elementIndex = new GLuint[(int)pointPerSlice*2*3]; // go figure  

            int indexCount = 0; 
            for (a = 0; a < pointPerSlice; a++)
            {
                //b = (a + 1) % pointPerSlice;
                b = a + 1;
                if (b == pointPerSlice) b = 0; 

                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + pointPerSlice + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + b);

                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + pointPerSlice + a);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + b);
                elementIndex[indexCount++] = (GLuint)(startPreviousSliceIndex + pointPerSlice + b);
            }

            int sizePerSlice = (int)pointPerSlice * 2 * 3 * sizeof(GLuint); 


            handle = GCHandle.Alloc(elementIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            indexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizePerSlice * (numberSlices-2), sizePerSlice, vertexPtr);

            handle.Free();

            int ContourCounter = numberSlices-1;
            GLuint[] Index = new GLuint[pointPerSlice * 2 * 3]; // 30 points, 2 triangles each and 3 points each
            GLuint[] IndexHori1 = new GLuint[pointPerSlice * 2];
            GLuint[] IndexVert1 = new GLuint[pointPerSlice * 2];  
            // create the index list to render the contours
            for (int VerIndex = 1; VerIndex <= pointPerSlice; VerIndex++)
            {
                // for last point of the contour
                if (VerIndex == pointPerSlice)
                {
                    Index[6 * (VerIndex - 1)] = (GLuint)(ContourCounter * pointPerSlice) - 1;
                    Index[6 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter - 1) * pointPerSlice + 1) - 1;
                    Index[6 * (VerIndex - 1) + 2] = (GLuint)((ContourCounter + 1) * pointPerSlice) - 1;
                    Index[6 * (VerIndex - 1) + 3] = (GLuint)((ContourCounter - 1) * pointPerSlice + 1) - 1;
                    Index[6 * (VerIndex - 1) + 4] = (GLuint)((ContourCounter) * pointPerSlice + 1) - 1;
                    Index[6 * (VerIndex - 1) + 5] = (GLuint)((ContourCounter + 1) * pointPerSlice) - 1;

                    // create the index list to draw lines for the quads
                    IndexHori1[2 * (VerIndex - 1)] = (GLuint)(ContourCounter * pointPerSlice - 1);
                    IndexHori1[2 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter - 1) * pointPerSlice);

                    IndexVert1[2 * (VerIndex - 1)] = (GLuint)((ContourCounter) * pointPerSlice - 1);
                    IndexVert1[2 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter + 1) * pointPerSlice - 1);
                }

                else
                {
                    Index[6 * (VerIndex - 1)] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex) - 1;
                    Index[6 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex + 1) - 1;
                    Index[6 * (VerIndex - 1) + 2] = (GLuint)((ContourCounter) * pointPerSlice + VerIndex) - 1;
                    Index[6 * (VerIndex - 1) + 3] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex + 1) - 1;
                    Index[6 * (VerIndex - 1) + 4] = (GLuint)((ContourCounter) * pointPerSlice + VerIndex + 1) - 1;
                    Index[6 * (VerIndex - 1) + 5] = (GLuint)((ContourCounter) * pointPerSlice + VerIndex) - 1;

                    // create the index list to draw lines for the quads
                    IndexHori1[2 * (VerIndex - 1)] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex - 1);
                    IndexHori1[2 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex);

                    IndexVert1[2 * (VerIndex - 1)] = (GLuint)((ContourCounter - 1) * pointPerSlice + VerIndex - 1);
                    IndexVert1[2 * (VerIndex - 1) + 1] = (GLuint)((ContourCounter) * pointPerSlice + VerIndex - 1);
                    //line = IndexVert1[2 * (VerIndex - 1)].ToString() + " " + IndexVert1[2 * (VerIndex - 1) + 1].ToString();
                }

                //objWriter.WriteLine(line);
            }

            int linePerSlice = (int)pointPerSlice * 2 * sizeof(GLuint);

            handle = GCHandle.Alloc(IndexHori1, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            HorindexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, linePerSlice * (numberSlices - 2), linePerSlice, vertexPtr);

            handle.Free();

            handle = GCHandle.Alloc(IndexVert1, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            VertindexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, linePerSlice * (numberSlices - 2), linePerSlice, vertexPtr);

            handle.Free();
        }


        /// <summary>
        /// Gets the vertex buffer array.
        /// </summary>
        public VertexBufferArray VertexBufferArray
        {
            get { return vertexBufferArray; }
        }

        public int totalTriangles()
        {
            return (numberSlices -1) * (pointPerSlice * 2) * 3;
        }

        public int totalLines()
        {
            return (numberSlices - 1) * (pointPerSlice * 2);
        }

    }
}
