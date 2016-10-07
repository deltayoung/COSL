using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlmNet;


// this class stores the individual data that is streamed. It only stores what is required. This prevents the need to allocate a huge storage in the beginning. 
namespace SharpGLProgram
{
    class CurveDataMgt
    {

        public List<DataParameter> dataList = new List<DataParameter>() ;
        DataParameter dpItem;

        int runningSearchCounter = 0;  // set to zero in the beginning  
        int azwCounter = 0; // this is only used for azwCounter during loadfromfile functionality


        // upwards implies that as the index increase, the depth also increases 
        public bool upwards = true; // initializing it to upwards, can be changed. 


        // return the index based on the depth given 
        // assuming a few things. 
        // the depth increment is steady for each index jump 
        // this is not affected by the up/down orientation problem 
        public int getIndex(double cDepth)
        {
			if (dataList.Count() < 2) return 0; // just return zero if there is only 2 values 

			/* // Calvin's version
			// this is the interval 
			double interval = dataList[1].depth - dataList[0].depth;

			int indexLocate = (int) ( (cDepth - dataList[0].depth) / interval ) ; 

			if ( ( indexLocate < 0 ) || ( indexLocate >= dataList.Count()))  
				return -1; 


			// this is just an approximation, due to division errors and large array values, we need to refine the search 
			bool searchfurther = true  ;

			do
			{
				float distanceCurrent = Math.Abs((float)(cDepth - dataList[indexLocate].depth));

				if (indexLocate != 0)
				{
					float distanceSmallerIndex = Math.Abs((float)(cDepth - dataList[indexLocate-1].depth));

					if (distanceCurrent > distanceSmallerIndex)
					{
						indexLocate = indexLocate - 1;
						continue; 
					}
				}

				if (indexLocate != dataList.Count()-1 )
				{
					float distanceHigherIndex = Math.Abs((float)(cDepth - dataList[indexLocate + 1].depth));

					if (distanceCurrent > distanceHigherIndex)
					{
						indexLocate = indexLocate + 1;
						continue; 
					}

				}

				searchfurther = false; 

			} while (searchfurther == true); 
			
			return indexLocate;
			*/

			// Binary search
			int lowerDepthIndex = dataList[0].depth < dataList[dataList.Count() - 1].depth ? 0 : dataList.Count()-1;
			int upperDepthIndex = lowerDepthIndex == 0 ? dataList.Count()-1 : 0;
			return binaryFindIndex(cDepth, lowerDepthIndex, upperDepthIndex);
             
        }

		// find the index in dataList that contains the exact depth value; or the nearest depth value
		// assume input indices are positive, in order, and within dataList.Count()
		public int binaryFindIndex(double value, int lowIndex, int highIndex)
		{
			if (value < dataList[lowIndex].depth || value > dataList[highIndex].depth)	// out of bound
				return -1;
			else if (lowIndex == highIndex)
				return lowIndex;
			else if (highIndex - lowIndex == 1)	// within bound
			{
				if (value - dataList[lowIndex].depth < dataList[highIndex].depth - value)
					return lowIndex;
				else
					return highIndex;
			}
			else
			{
				int a = binaryFindIndex(value, lowIndex, (lowIndex+highIndex)/2), b;
				if (a == -1)
				{
					b = binaryFindIndex(value, (lowIndex+highIndex)/2+1, highIndex);
					if (b == -1)
						return -1;
					else
						return b;
				}
				else
					return a;
			}
		}


                
         // if the depth value is increasing, upwards is true. vice versa. 
        public void setOrientation(bool newO)
        {
            upwards = newO; 
        }


        public void getFirstFullElement(ref DataParameter dptemp)
        {
            dptemp.dataP = new double[getVectorDataSize()];

            dptemp.depth = getFirstDepth();
            getData(ref dptemp.dataP); 
        }




        // given elementIndex, return the first data value 
        public double getElementValue(int elementNum)
        {
            if (elementNum < 0) return 0;
            if (elementNum >= dataList.Count()) return 0;

            return dataList[elementNum].dataP[0]; 
        }


        // get the count number of the data array 
        public int getVectorDataSize()
        {
            if (dataList.Count() == 0)
                return 0; // nothing to get

            return dataList[0].dataP.Count(); 
        }

        // given a vec3 array, populate the data array. flatten the data out to 1D. 
        public void setVectorData(vec3[] vectorD)
        {
            if (dataList.Count() == 0)
                return; // nothing to set

            // set it to 3 times bigger 
            dataList[0].dataP = new double[vectorD.Count() * 3];

            for (int a = 0; a < vectorD.Count(); a++)
            {
                dataList[0].dataP[a * 3] = vectorD[a].x;
                dataList[0].dataP[a * 3+1] = vectorD[a].y;
                dataList[0].dataP[a * 3+2] = vectorD[a].z; 

            }
        }

        // this function is called once the vector Data are set... use this to expand the bounding box 
        public void setBoundingBox(ref int xCoord, ref int yCoord)
        {
            if (dataList.Count() == 0)
                return; // nothing to set

            for (int a = 0; a < dataList[0].dataP.Count() / 3 ; a++)
            {
                if (Math.Abs(dataList[0].dataP[a * 3]) > xCoord)
                {
                    xCoord = (int)Math.Abs(dataList[0].dataP[a * 3]) + 100;
                }
                if (Math.Abs(dataList[0].dataP[a * 3 + 1]) > yCoord)
                {
                    yCoord = (int)Math.Abs(dataList[0].dataP[a * 3 + 1]) + 100;
                }

            }
        }

        // delete the first element. (but this function should not be used when the list is huge) 
        // deletion function is very slow for list when count is very large. 
        public void deleteFirst()
        {
            if (dataList.Count() == 0)
                return; // nothing to set
            dataList.RemoveAt(0);  // remove it from list 
        }

        
        // only always get the data from the first item. 
        public void getData(ref double[] extractedData)
        {
            if (dataList.Count() == 0)
                return; // nothing to retrieve 

            for (int a = 0; a < dataList[0].dataP.Count(); a++)
                extractedData[a] = dataList[0].dataP[a]; 
        }


        // only always get the data from the first item. 
        public void getData(ref float[] extractedData)
        {
            if (dataList.Count() == 0)
                return; // nothing to retrieve 

            for (int a = 0; a < dataList[0].dataP.Count(); a++)
                extractedData[a] = (float) dataList[0].dataP[a];

        }
        
        // add a new data points. Method 3
        public bool insertNew(double nDepth, double nDataP)
        {
            double[] temp = new double[1]; 
            temp[0] = nDataP; 

            return insertNew(nDepth, temp); 
        }

        
        // add a new data points. Method 1 
        public bool insertNew(double nDepth, double[] nDataP)
        {
            if (dataList.Count() != 0) // test for similarity 
            {
                if (dataList.Last().depth == nDepth)
                    return false; // do not add 
            }

             // create a new item, else it seems to be adding a pointer to it. 
            dpItem = new DataParameter();
            dpItem.depth = nDepth;
            dpItem.dataP = new double[nDataP.Count()];

            for (int a = 0; a < nDataP.Count(); a++)
                dpItem.dataP[a] = nDataP[a]; 

            dataList.Add(dpItem);

            return true; 
        }

        //add a new data point. Method 2  
        public bool insertNew(DataParameter dp)
        {

            if (dataList.Count() != 0) // test for similarity 
            {
                if (dataList.Last().depth == dp.depth)
                    return false; // do not add 
            }
            
            // create a new item, else it seems to be adding a pointer to it. 
            dpItem = new DataParameter();
            dpItem.depth = dp.depth;
            dpItem.dataP = new double[dp.dataP.Count()];


            for (int a = 0; a < dp.dataP.Count(); a++)
                dpItem.dataP[a] = dp.dataP[a]; 

            dataList.Add(dpItem);

            return true; 
        }


        // clears the data
        public void clearPartial()
        {
            dataList.Clear();

            runningSearchCounter = 0;  // set to zero in the beginning  
            azwCounter = 0; // this is only used for azwCounter during loadfromfile functionality
        }


        // clears the data
        public void clearAll()
        {
            dataList.Clear(); 

            runningSearchCounter = 0;  // set to zero in the beginning  
            azwCounter = 0; // this is only used for azwCounter during loadfromfile functionality
            upwards = true; // initializing it to upwards, can be changed. 
        }

        // given an index, return the depth 
        public double getDepth(int index)
        {
            if ( (index < 0) || ( index >= dataList.Count() ) ) 
                return  0; 

            return dataList[index].depth ; 
        }


        // return the first depth still in data 
        public double getFirstDepth()
        {
            if (dataList.Count() == 0)
                return 0; 
            return dataList[0].depth; 
        }

        // get the last depth value 
        public double getLastDepth()
        {
            if (dataList.Count() == 0)
                return 0; 

            return dataList[dataList.Count() - 1].depth;
        }

        // get the first data element of the last list element
        public double getLastDataValue()
        {
            if (dataList.Count() == 0)
                return 0;

            return dataList[dataList.Count() - 1].dataP[0];

        }

        
        // return number of elements 
        public int getCount()
        {
            return dataList.Count(); 
        }

        // get the data count of the first element / which should be the same for all the elements 
        public int getDataCount()
        {
           if (dataList.Count() == 0)
                return 0;

           return dataList[0].dataP.Count();  
        }


        // test if the contour data has reach the range of this parameter set 
        public bool withinRange(double cDepth)
        {
            if (dataList.Count() == 0) return false;


            if (upwards == true) // depth is increasing mode 
            {
                if (dataList.Last().depth > cDepth)  // the current depth is still lesser than what is currently available in this data list. 
                    return true;
          }
            else // depth is decreaing 
            {
                if (dataList.Last().depth < cDepth)  // the current depth is still lesser than what is currently available in this data list. 
                    return true;
            }

            return false;  // within range... the data can be found.  
        }

        // test if the contour data has reach the range of this parameter set 
        public bool belowRange(double cDepth)
        {
            if (dataList.Count() == 0) return false;

            if (upwards == true) // depth is increasing 
            {
                if (dataList.First().depth < cDepth)
                    return true;
            }
            else // depth is decreasing 
            {
                if (dataList.First().depth > cDepth)
                    return true;
            }
            
            return false;  // within range... the data can be found.  
        }


        public double getCurrentAZWvalue()
        {
            if (dataList.Count() == 0)
                return 0.0f; 


            if ( azwCounter < dataList.Count() )
                return 0.0f; 

            if ( azwCounter >= dataList.Count() )
                azwCounter = dataList.Count()-1;

            return dataList[azwCounter++].dataP[0];  
        }





        
        // given a new contour depth, is the current data up to date with it? 
        public bool compareAndDelete(double cDepth, ref DataParameter dp)
        {

            if (dataList.Count() <= 2) return false; 

            // when it reaches here, it means that the last element in the list is higher than cDepth 
            // now we have the cDepth has caught up. 
            // we only delete [0] if cDepth is above [1] 

            if (upwards == true) // depth is increasing 
            {
                if (dataList[1].depth < cDepth)  // cDepth has exceeded the [1] data... can delete away the [0] data 
                {
                    dp = dataList[0]; // set the first data to dp. 
                    dataList.RemoveAt(0);  // remove it from list 
                    return true;
                }
            }
            else // depth is decreasing 
            {
                if (dataList[1].depth > cDepth)  // cDepth has exceeded the [1] data... can delete away the [0] data 
                {
                    dp = dataList[0]; // set the first data to dp. 
                    dataList.RemoveAt(0);  // remove it from list 
                    return true;
                }
            }


            return false; // the dDepth is still lodged within [0] and [1] 
        }


        // this function is only used for DAZ and DEV type of data 
        // based on the cDepth value, extract the nearest pair of de
        public bool getNearestValue(double cDepth, ref DataParameter prev , ref DataParameter next )
        {


            // the first data must be below cDepth
            // this happens when the dev or daz data has not caught up with the physical tunnel data
            if ((belowRange(cDepth) == false) || ( withinRange(cDepth) == false ))
            {
                // provide neutral data
                prev.depth = cDepth-1.0f;
                prev.dataP = new double[1];
                prev.dataP[0] = 0.0f;

                next.depth = cDepth + 1.0f;
                next.dataP = new double[1];
                next.dataP[0] = 0.0f;

                return true;

            }
            

            // minimum must have at least 2 values. 
            if (dataList.Count() < 2)
                return false; 

            // the last element has a depth that has exceeded the cDepth
           

            // it must at least clear the first hurdle. 
            DataParameter tempDp = new DataParameter(); 
            tempDp.dataP = new double[1]; 

            // keep removing the [0], till you reach one which u cannot overcome 
            while ( compareAndDelete(cDepth, ref tempDp) == true )
            {}

            prev.depth = dataList[0].depth;
            prev.dataP = new double[1];
            prev.dataP[0] = dataList[0].dataP[0];

            next.depth = dataList[1].depth;
            next.dataP = new double[1];
            next.dataP[0] = dataList[1].dataP[0]; 
            
            return true; 
        }


        bool withinIndex(double cDepth)
        {
            if ( runningSearchCounter + 1 == dataList.Count()-1 ) 
                return true; // reach end already 

            if (upwards == true) // depth is increasing 
            {
                if ((cDepth > dataList[runningSearchCounter].depth) && (cDepth < dataList[runningSearchCounter + 1].depth))
                    return true;
            }
            else // depth is decreasing 
            {
                if ((cDepth < dataList[runningSearchCounter].depth) && (cDepth > dataList[runningSearchCounter + 1].depth))
                    return true;
            }

            runningSearchCounter++;
            return false; 

        }


        // the reason why a static call is required is because in load from file, all the data is already present in the DEV and DAZ, 
        // and if we delete the first node, it will take a very long time. so we cannot do it same as the streaming mode. 
        // this function is only used for DAZ and DEV type of data 
        // based on the cDepth value, extract the nearest pair of de
        public bool getNearestValueStaticCall(double cDepth, ref DataParameter prev, ref DataParameter next)
        {
            // the lastest data has already exceeded the given cDepth
            if (withinRange(cDepth) == false)
                return false;

            // the first data must be below cDepth
            // this happens when the dev or daz data has not caught up with the physical tunnel data
            if (belowRange(cDepth) == false)
            {
                // provide neutral data
                prev.depth = cDepth-1.0f;
                prev.dataP = new double[1];
                prev.dataP[0] = 0.0f;

                next.depth = cDepth + 1.0f;
                next.dataP = new double[1];
                next.dataP[0] = 0.0f;

                return true;

            }

            
            // the runningSearchCounter will be used to move the index each time this is called  
            // in the first iteration, runningSearchcounter = 0 and the cDepth is already above it. 

            while (withinIndex(cDepth) == false)
            {}



            prev.depth = dataList[runningSearchCounter].depth;
            prev.dataP = new double[1];
            prev.dataP[0] = dataList[runningSearchCounter].dataP[0];

            next.depth = dataList[runningSearchCounter+1].depth;
            next.dataP = new double[1];
            next.dataP[0] = dataList[runningSearchCounter+1].dataP[0];

            return true;
        }

        


    }
}
