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
    // this would be used to render the "Skeleton" of the tunnel at various levels of details 
    // ideally, this skeleton would never be seen. 
    // the only time where it would be visible is when you are scrolling upwards, but not outwards for a very long time. 
    class Skeleton
    {
        private VertexBufferArray vertexBufferArray; // this is the vertex buffer array that stores the STATE of opengl , easier to run the codes 
        VertexBuffer positionBuffer; // these are the vertex buffer objects 
        public IndexBuffer indexBuffer; // these holds the index buffers, the indices defining the triangles  
        public int skeletonPtPerSlice = 6 ; // initialize to 6 first. 
        private const int maxSlice = 1600000; // 1.6 million, that's the highest number of slices that we catered for..   
        int numberSlices = 0;  // the number of slices to date , slice 0 = first slice

        // dataSizeCount tells you how much the associated blocks have in each contour 
        public void generateGeometry(OpenGL gl, uint vertexAttribLocation,   int dataSizeCount)
        {
           
            // lets keep the skeleton to a manageable level, between 8 and 4.... nothing outside this range. 
            skeletonPtPerSlice = dataSizeCount/4;
            if (skeletonPtPerSlice > 8)
                skeletonPtPerSlice = 8;
            if (skeletonPtPerSlice < 4)
                skeletonPtPerSlice = 4; 
             
            // delete the buffer if it was defined previously. 
            if (vertexBufferArray != null)
                vertexBufferArray.Delete(gl);

            vertexBufferArray = new VertexBufferArray();
            vertexBufferArray.Create(gl);
            vertexBufferArray.Bind(gl);
            initializeData(gl, vertexAttribLocation );
            vertexBufferArray.Unbind(gl);
        
        }

        // this just sets up the vertex attribute pointer stuff, so that they know the nature/type of the data that is coming  
        private void initializeData(OpenGL gl, uint vertexAttribLocation)
        {
            // each segment only has skeletonPtPerSlice of number of point per slice. 
            int totalPossiblePoints = skeletonPtPerSlice * maxSlice ;

            int totalSize3Floats = (int)(totalPossiblePoints * sizeof(GLfloat) * 3);  // location, normal , color  attribute 
            int totalIndexSize = (int)(totalPossiblePoints * 2 * sizeof(GLuint)) - (int)(skeletonPtPerSlice * 2 * sizeof(GLuint));  // one slice dun need to attach to the other slice. 

            // used as a null pointer... 
            GCHandle handle = GCHandle.Alloc(null, GCHandleType.Pinned); // set to null if you want to send over data at a later timing...  
            IntPtr vertexPtr = handle.AddrOfPinnedObject();

            // used to clear all buffers. 
            clearAllBuffers(gl);

            // create the vertexBuffer 
            positionBuffer = new VertexBuffer();
   
            // setting the vertex buffers for the position 
            positionBuffer.Create(gl);
            positionBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferData(SharpGL.OpenGL.GL_ARRAY_BUFFER, totalSize3Floats, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(vertexAttribLocation, 3, OpenGL.GL_FLOAT, false, 0, IntPtr.Zero);
            gl.EnableVertexAttribArray(vertexAttribLocation);
        
            //////////// now for the index buffer ////////////////////////// 
            indexBuffer = new IndexBuffer();
            indexBuffer.Create(gl);
            indexBuffer.Bind(gl);
            gl.BufferData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, totalIndexSize, vertexPtr, SharpGL.OpenGL.GL_STATIC_DRAW);
        }



        // 2 information regarding this slice , only data points and color 
        // the current convention is this, the master slice 0 will contain no color info, master slice 0 will contain color information for triangles between slice 0 and 1 
        // i am assuming that the array for both are already in the final 200 points set
        public bool createOneSlice(OpenGL gl, ref vec3[] slicePoints )
        {
            if (numberSlices == maxSlice) return false;  // reached max slides ... exit 

            // size/space for 1 slice 
            int size3Floats = (int)(skeletonPtPerSlice * 3 * sizeof(GLfloat)); // for variabls that have 3 floats 

            // offset so far 
            int offsetSize3Floats = (int)( numberSlices  * size3Floats);  // for variabls that have 3 floats 
      
            // initializing all the required variables 
            vec3[] vertices = new vec3[skeletonPtPerSlice];  // the vertices for the new slice 
       

            int a, b;

            // standardPoints is always the "previous" slice  , slicePoints is the "current" slice 
            for (a = 0; a < skeletonPtPerSlice; a++)
                vertices[a] = slicePoints[a];
          
            // now to sub the data into the gl_array_buffer     
            GCHandle handle = GCHandle.Alloc(vertices.SelectMany(v => v.to_array()).ToArray(), GCHandleType.Pinned);
            IntPtr vertexPtr = handle.AddrOfPinnedObject();
            positionBuffer.Bind(gl); // this binds to the gl_array_buffer
            gl.BufferSubData(SharpGL.OpenGL.GL_ARRAY_BUFFER, offsetSize3Floats, size3Floats, vertexPtr);
            handle.Free();

            
            // now we create the element arrays 
            GLuint currentStartSliceIndex = (GLuint)(numberSlices * skeletonPtPerSlice); // this is where the first point of this contour starts
            GLuint[] elementIndex ; // there is this number of indices required for this 
            int indexOffset ; // = (int)totalPointPerSlice * sizeof(GLuint);
            

            if (numberSlices == 0) // this is the first slice.  
            {
                elementIndex = new GLuint[skeletonPtPerSlice * 2];
                indexOffset = 0; 
            }
            else
            {
                elementIndex = new GLuint[skeletonPtPerSlice * 2 * 2];  // lines across this contour and to the previous one. 
                indexOffset = (numberSlices - 1) * skeletonPtPerSlice * 4 * sizeof(GLuint) + skeletonPtPerSlice * 2 * sizeof(GLuint); 
            }


            // lines across this contour
            int indexCount = 0;
            for (a = 0; a < skeletonPtPerSlice; a++)
            {
                b = (a + 1) % skeletonPtPerSlice;

                elementIndex[indexCount++] = (GLuint)(currentStartSliceIndex + a);
                elementIndex[indexCount++] = (GLuint)(currentStartSliceIndex + b);

            }


            // establishing lines to the previous contour
            if (numberSlices != 0) // this is NOT the first slice.  
            {
                for (a = 0; a < skeletonPtPerSlice; a++)
                {
                    elementIndex[indexCount++] = (GLuint)(currentStartSliceIndex + a);
                    elementIndex[indexCount++] = (GLuint)(currentStartSliceIndex + a - skeletonPtPerSlice);

                }

            }


           

            handle = GCHandle.Alloc(elementIndex, GCHandleType.Pinned);
            vertexPtr = handle.AddrOfPinnedObject();
            indexBuffer.Bind(gl);
            gl.BufferSubData(SharpGL.OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexOffset, elementIndex.Count() * sizeof(GLuint), vertexPtr);
            handle.Free();

            // here we increase the number of slices. 
            numberSlices++;

            if (numberSlices == maxSlice)
                return false;  // reached max slides ... exit 

            return true;
        }




        // clear all the used openGL buffers. 
        public void clearAllBuffers(OpenGL gl)
        {
            if (positionBuffer != null)
                deleteBuffer(gl, positionBuffer.VertexBufferObject);
                    
            if (indexBuffer != null)
                deleteBuffer(gl, indexBuffer.IndexBufferObject);
        }

        // used as a helper class to delete the buffer. 
        private void deleteBuffer(OpenGL gl, uint indexID)
        {
            uint[] ids = new uint[1];
            ids[0] = indexID;
            gl.DeleteBuffers(1, ids);
        }

        // get the total line count for rendering 
        // the renderedSlices belongs to the blocks, if there are rendered, then no point rendering this. 
         public int totalLines( int renderedSlices )
         {
             if (renderedSlices >= numberSlices)
                 return 0; 
             
            if (numberSlices == 0)
                return 0; 
            else  if (numberSlices == 1)
                return numberSlices * 2 * skeletonPtPerSlice; 
            else
                return 2 * skeletonPtPerSlice + (numberSlices-1 - renderedSlices) * 4 * skeletonPtPerSlice; ;    
         
        }

        // Gets the vertex buffer array.
        public VertexBufferArray VertexBufferArray
        {
            get { return vertexBufferArray; }
        }
       

    }
}
