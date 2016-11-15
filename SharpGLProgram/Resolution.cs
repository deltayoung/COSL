using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlmNet;
using GLuint = System.UInt32;
using GLfloat = System.Single;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.Shaders;

namespace SharpGLProgram
{
    // this class handles all the rendering (2 blocks + skeleton) at a certain resolution level. 
    class Resolution
    {
        int resolutionLevel = 1; // default is 1 
        int fullDataSize = 200;  // i am intializing it to 200, can change to other values if necessary. 
        int currentBlockIndex; // this changes from 0 to 1 back and fro, to render them in succession. 
        vec3[] colorData; // this is just to provide some random color data.  
        vec3[] pointData; // this is the actual point data
        vec3[] skeletonData; // this is point for the skeleton 
        const uint positionAttribute = 0;
        const uint normalAttribute = 1;
        const uint colorAttribute = 2;
        const uint textureAttribute = 3;
        const uint sliceAttribute = 4;

        OpenGL glContainer;

        bool containTexture = true; 

    
        public void Initialize(OpenGL gl, int rLevel , int fullDataCount, int maxSliceCount)
        {
            resolutionLevel = rLevel; // 1 = full resolution , 4 = one quarter of resolution ... 
            fullDataSize = fullDataCount / resolutionLevel;

            if (fullDataSize < 4)
                fullDataSize = 4; 

            glContainer = gl; 
            currentBlockIndex = 0; // set to zero at the beginning. 

            block = new Sector[2]; // creating 2 new blocks 
            // notice that the texture attribute is not passed to the blocks 
            block[0] = new Sector();
            block[0].generateGeometry(gl, positionAttribute, normalAttribute, colorAttribute, textureAttribute, sliceAttribute, fullDataSize, maxSliceCount);  // initialised the tunnel geometry 
  
           block[1] = new Sector();
           block[1].generateGeometry(gl, positionAttribute, normalAttribute, colorAttribute, textureAttribute, sliceAttribute, fullDataSize, maxSliceCount);  // initialised the tunnel geometry 
           
            skeleton = new Skeleton();
            skeleton.generateGeometry(gl, positionAttribute, fullDataSize);  // initialised the tunnel geometry 

            // these are used to hold 1 contour size of data. 
            colorData = new vec3[fullDataSize];
            pointData = new vec3[fullDataSize];
            skeletonData = new vec3[skeleton.skeletonPtPerSlice]; 
        }

        // the fullDataCount will be always the original total 
        public void reInitialize(OpenGL gl, int fullDataCount, int maxSliceCount)
        {
            fullDataSize = fullDataCount / resolutionLevel;
            if (fullDataSize < 4)
                fullDataSize = 4;

            block[0].generateGeometry(gl, positionAttribute, normalAttribute, colorAttribute, textureAttribute, sliceAttribute, fullDataSize, maxSliceCount);  // initialised the tunnel geometry 
            block[1].generateGeometry(gl, positionAttribute, normalAttribute, colorAttribute, textureAttribute, sliceAttribute, fullDataSize, maxSliceCount);  // initialised the tunnel geometry 
                  
            skeleton.generateGeometry(gl, positionAttribute, fullDataSize);  

            // these are used to hold 1 contour size of data. 
            colorData = new vec3[fullDataSize];
            pointData = new vec3[fullDataSize];
            skeletonData = new vec3[skeleton.skeletonPtPerSlice]; 
        }

        public void setTextureContain(bool containT)
        {
            containTexture = containT;

            block[0].containTexture = containTexture;
            block[1].containTexture = containTexture; 


        }

        public void clearMemory(OpenGL gl)
        {
            block[0].clearAllBuffers(gl);
            block[1].clearAllBuffers(gl);

            skeleton.clearAllBuffers(gl); 

        }




        // currentTextureIndex refers to the index of the textureID (which texture image to call_1), currentTextureCoords refers to the UV coords (just the row) 
        public void createOneSliceTexture(OpenGL gl, GLfloat[] ContourPositions, int currentTextureIndex, double currentTextureCoord)
        {
            pointConversion(ref ContourPositions);

            if (block[currentBlockIndex].createOneSliceTexture(gl, ref pointData, currentTextureIndex, currentTextureCoord) == false)
            {
                // need to shift between the two blocks  
                if (currentBlockIndex == 0)
                    currentBlockIndex = 1;
                else
                    currentBlockIndex = 0;

                block[currentBlockIndex].reset();
                block[currentBlockIndex].createOneSliceTexture(gl, ref pointData, currentTextureIndex, currentTextureCoord);
            }


            skeleton.createOneSlice(gl, ref skeletonData); 

        }

        

        public void RenderTexture(OpenGL gl, ShaderProgram sp, ref uint[] texNameID)
        {
            // no need to call_1 these 2 functions because these are default ... 
			gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.Uniform1(sp.GetUniformLocation(gl, "gSampler"), 0); 
           

            block[0].Render(gl, sp, ref texNameID);
                        
            block[1].Render(gl, sp, ref texNameID);



            skeleton.VertexBufferArray.Bind(gl);
            skeleton.indexBuffer.Bind(gl);
            gl.DrawElements(SharpGL.OpenGL.GL_LINES, skeleton.totalLines(block[0].numberSlices + block[1].numberSlices), OpenGL.GL_UNSIGNED_INT, IntPtr.Zero); 

        }



        /*
        public void RenderLines(OpenGL gl)
        {


            block[0].RenderLines(gl);

            block[1].RenderLines(gl);





            skeleton.VertexBufferArray.Bind(gl);
            skeleton.indexBuffer.Bind(gl);
            gl.DrawElements(SharpGL.OpenGL.GL_LINES, skeleton.totalLines(block[0].numberSlices + block[1].numberSlices), OpenGL.GL_UNSIGNED_INT, IntPtr.Zero); 
        }

        */ 


           

        // internal function to convert floats[] to vec3[]
        void pointConversion(ref GLfloat[] ContourPositions)
        {
            // size will be 1/3 of the count of contour positions 
            int size = ContourPositions.Count() / 3, a;
            vec3[] originalDataPoint = new vec3[size];
            for (a = 0; a < size; a++)
            {
                originalDataPoint[a].x = ContourPositions[3 * a];
                originalDataPoint[a].y = ContourPositions[3 * a + 1];
                originalDataPoint[a].z = ContourPositions[3 * a + 2];
            }

            // now to resize them to fullDataSize 
            reSizeData(ref originalDataPoint, size, ref pointData, pointData.Count());
            reSizeData(ref originalDataPoint, size, ref skeletonData, skeletonData.Count());


        }

        // function to compute the length of a vec3 point, unfortunately the GLM class hasN not provided this yet. 
        public float length(vec3 point)
        {
            return (float)Math.Sqrt(point.x * point.x + point.y * point.y + point.z * point.z);
        }

        // function to compute the distance between 2 points. 
        public float distance(vec3 start, vec3 end)
        {
            start = start - end;
            return length(start);
        }

        // to resize the data from an original size to a new size, can either downsize or upsize. 
        public void reSizeData(ref vec3[] original, int originalSize, ref vec3[] expand, int expandSize)
        {
            // originalSize is the number of original data
            // expandSize is the number of new data

            int a, b, index;
            float totalLength = 0.0f;
            float intervalLength, distanceLeft, currentDistance;
            vec3 currentPoint, newPoint;

            // compute total length 
            for (a = 0; a < originalSize; a++)
            {
                b = (a + 1) % originalSize;
                totalLength += distance(original[a], original[b]);
            }

            // this is the amount of interval 
            intervalLength = totalLength / expandSize;


            // index will fill in for the expand. 
            expand[0] = original[0];  // first data is always aligned 
            index = 1;
            distanceLeft = intervalLength;  // this is the distance to cover 
            currentPoint = expand[0];  // start from this point 
            for (a = 0; a < originalSize; a++)
            {
                b = (a + 1) % originalSize;
                currentDistance = distance(original[a], original[b]);
                currentPoint = original[a];

                while (currentDistance > distanceLeft)
                {
                    if (index == expandSize)
                        break; // reached end. 
                    newPoint = original[b] - currentPoint;
                    newPoint = glm.normalize(newPoint);
                    newPoint = newPoint * distanceLeft;
                    newPoint = newPoint + currentPoint;
                    currentPoint = newPoint;
                    expand[index++] = currentPoint;
                    currentDistance -= distanceLeft;
                    distanceLeft = intervalLength;

                }

                // if this exit at this point, the distanceLeft is greater thn currentDistance 
                distanceLeft -= currentDistance;

            }
        }

        private Skeleton skeleton; 
       // private Block[] block; // there are 2 blocks, we are rendering them in succession 
        private Sector[] block;

        internal Skeleton has1
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        }

        internal Sector hasN
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        } // there are 2 blocks, we are rendering them in succession

       // private BlockSS[] block;
    }
}
