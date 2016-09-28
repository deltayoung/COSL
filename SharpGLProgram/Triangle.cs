using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlmNet; 



namespace SharpGLProgram
{
    class Triangle
    {
        public vec3 pt1, pt2, pt3;
                
        public void generateTriangle(Random r) 
        {
            random = r; 

            pt1 = new vec3();
            pt2 = new vec3();
            pt3 = new vec3(); 


            RandomVec(ref pt1);
            RandomVec(ref pt2);
            RandomVec(ref pt3);

            vec3 offset;
            offset = new vec3(); 
            RandomVec(ref offset);

            offset *= RandomFloat() * 1000.0f ;
            pt1 += offset;
            pt2 += offset;
            pt3 += offset; 
        }
        


        private void RandomVec(ref vec3 v)
        {
            v.x = RandomFloat();
            v.y = RandomFloat();
            v.z = RandomFloat(); 
        }



        private float RandomFloat(float min = 0, float max = 1)
        {
            return (float)random.NextDouble() * (max - min) + min;
        }

        private Random random ;
    }
}
