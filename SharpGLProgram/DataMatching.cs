using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpGLProgram
{
    // this class includes some functions to help to align some of the data between different types of dataset. 
    class DataMatching
    {

        // not used!
        // when the depth values are read.. it multiples by 100k, and the jumps are very large. Spreading it evenly 
        public void spreadingDepthValues(ref double[] depthValues)
        {
            int startIndex = 0, endIndex;

            while (startIndex < depthValues.Count() - 1) // while not end of array 
            {
                endIndex = startIndex;
                while (depthValues[startIndex] == depthValues[endIndex])
                {
                    if (endIndex == depthValues.Count()-1)
                        return; // reached end of array and still equal.... so nothing to do at this point 
                    endIndex++;
                 }

                // now we have a start and end with different values. 

                double totalDifference = depthValues[endIndex] - depthValues[startIndex];
                double partialDifference = totalDifference / (endIndex - startIndex);

                // can actually start from startIndex + 1 ... does not matter 
                for (int i = startIndex ; i < endIndex; i++)
                    depthValues[i] = depthValues[startIndex] + (i - startIndex) * partialDifference;


                startIndex = endIndex;  // set to next jump 
                
            }


        }

    }
}
