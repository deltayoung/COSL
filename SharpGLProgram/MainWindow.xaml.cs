using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using GlmNet; 
using GLfloat = System.Single;
using GLuint = System.UInt32;
using SharpGL.SceneGraph;
using SharpGL;

//using System.Printing;
using Microsoft.Win32;


// COSL
using AX.Object;
using AX.Object.Logging;
using AX.Data.Logging;
using TechTao.UDF.Impl;
using TechTao.UDF.Spec;
using System.Windows.Forms;

// For DispatcherTimer
using System.Windows.Threading;
using System.Threading;

using System.ComponentModel;

namespace SharpGLProgram
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // to save the contours

        Random random; 
        mat4 projectionMatrix;
        mat4 modelTransformMatrix, tempmodelTransformMatrix, mgTransformMatrix, compassRotationMatrix;
        mat4 viewingMatrix; 
        vec4 xVector, yVector, zVector;
        vec4 fixedX, fixedY, fixedZ; 
        vec4 translationalVector; 
        float zNear, zFar, fov;
        float translationSpeed, mouseWheelSpeed;
        System.IO.StreamWriter objWriter;
        System.IO.StreamWriter doglegWriter;

        
        bool is_Left_Button_Pressed, is_Right_Button_Pressed, is_Middle_Button_Pressed;
        Point prev_Mouse_Loc, diff_Mouse_Loc, cur_Mouse_Loc, init_Mouse_Loc, picking_Mouse_Loc;

        mat4 fullT;

        int pick_Depth;
        float pick_Depth_f;
        float click_depth; 
        float real_depth;

        vec3 rotationPoint;
        float PI = 3.14159265f; 

             

        // store 1 contour
        GLfloat[] ContourPositions;

        // COSL Portion
        //DispatcherTimer timer;
        public IService m_iService = null;
        public ILoggingManagerNet m_iLoggingManager;
        //Create a thread to update visual
        public delegate void ThreadUpdateVisual(MainWindow wnd);
        Thread m_threadUpdateVisual = new Thread(new ParameterizedThreadStart(OnThreadUpdateVisual));
        bool m_bUpdateVisualThreadTerminat = true;

        // delegate is something like a function pointer. 
        public delegate void ServiceEventResponsor(bool mode);
        public delegate void ServiceEventResponsor2();

        public static int[,] colorPalette;          // the color palette containing all interpolated colors for tunnels (Like's color paletter) 
        public static List<string> rgbValues = new List<string>();       // list of selected colors to be interpolated for coloring tunnels
        public static bool userColorSelection = false;
        public static int userColorLevels;
        public static bool linearColor = false;
        public static int lowCO, highCO; // low and highCutoff values 

        //Wanghj:2016.8.15
        //to get the direction 
        public LoggingMode m_eLoggingMode = LoggingMode._Count;
        public LoggingDir m_eLoggingDir = LoggingDir._Count;

        // MgDec
        public float mgDecValue = 0 ; 


        // first streaming initialization 
        public bool firstStreamInitialization = false;

        // other UI-related flags
        public bool duplicateTimeModeSignal = false;
        public bool isPaused = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            //textBox1.IsEnabled = false;

            InitializeComponent();

            //this.ResizeMode = ResizeMode.NoResize;
            this.ResizeMode = ResizeMode.CanResizeWithGrip;
            this.KeyDown += openGLControl_KeyDown;

            // maximized the application from the start. 
            WindowState = WindowState.Maximized;

          
            // setting the various parameter for screen ratio and mouse controls 
            random = new Random(); 
            zNear = 0.1f;
            zFar = 1000.0f;
            fov = 60.0f / 180.0f * 3.1415f; 
            translationSpeed = 0.5f;
            mouseWheelSpeed = 0.1f;
            is_Left_Button_Pressed = false; // for mouse trigger 
            is_Right_Button_Pressed = false;
            is_Middle_Button_Pressed = false;

            fixedX = new vec4(1.0f, 0.0f, 0.0f, 1.0f);  // original x vector
            fixedY = new vec4(0.0f, 1.0f, 0.0f, 1.0f);  // original y vector
            fixedZ = new vec4(0.0f, 0.0f, 1.0f, 1.0f);  // original z vector

            rotationPoint = new vec3();
            //translationLine = new vec3(); 
            fullT = mat4.identity(); 
            modelTransformMatrix = mat4.identity();
            projectionMatrix = mat4.identity();
            compassRotationMatrix = mat4.identity();

            objWriter = new System.IO.StreamWriter(@"debug.txt"); // used as a means to write debugging data on the screen 
            doglegWriter = new System.IO.StreamWriter(@"dogleg.txt"); // used as a means to write debugging data on the screen 

            // shift the modelview backwards 
            modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(0, 0, 0));
            viewingMatrix = glm.lookAt(new vec3(30, 30, 0), new vec3(0, 0, 0), new vec3(0, 0, 1));
            //viewingMatrix = glm.lookAt(new vec3(0, 30, 0), new vec3(0, 0, 0), new vec3(0, 0, 1));

            pick_Depth = -1;
            pick_Depth_f = -1.0f;
      
        }

        // given the top and bottom of the tunnel so far, compute the point of rotation. 
        vec3 computeRotationPoint(float ztop, float zbottom)
        {
            vec3 planeNorm = new vec3(0, 0, 1);  // this is the normal plane perpendicular to z plane.
            vec3 tunnelTop = new vec3(modelTransformMatrix * new vec4(0, 0, ztop, 1)); // transform the top to world coordinates
            vec3 tunnelBottom = new vec3(modelTransformMatrix * new vec4(0, 0, zbottom, 1)); // transform the top to world coordinates
            vec3 tunnelNormal = tunnelTop - tunnelBottom;

            // length function is not implemented by GLM wpf version, so we have to write our own.
            // have to be negative since i am taking the top...

            float tunnelLength = -(float)Math.Sqrt((double)(tunnelNormal.x * tunnelNormal.x + tunnelNormal.y * tunnelNormal.y + tunnelNormal.z * tunnelNormal.z));

            vec3 intersection;

            if (Math.Abs(tunnelLength) < 0.001f)
                intersection = tunnelTop;
            else
            {
                // normalized tunnel length

                tunnelNormal = glm.normalize(tunnelNormal);

                // test if tunnel is lying on the same plane as the xy plane

                if (Math.Abs(glm.dot(planeNorm, tunnelNormal)) < 0.0001f)
                    return new vec3(0, 0, (ztop + zbottom) / 2.0f); // return the midpoint to rotate

                //vec3 temp = new vec3() - tunnelTop; // take a point lying on the viewplane (which is always on the orgin xy plane) 
                vec3 temp = new vec3(0,0,depthStart) - tunnelTop; // take a point lying on the viewplane (which is always on the orgin xy plane) 
                
                float parameter = glm.dot(planeNorm, temp) / glm.dot(planeNorm, tunnelNormal);


                if (parameter < tunnelLength)
                    parameter = tunnelLength;

                else if (parameter > 0)
                    parameter = 0;

                intersection = (tunnelNormal * parameter) + tunnelTop;
            }

            // transform back
            intersection = new vec3(glm.inverse(modelTransformMatrix) * new vec4(intersection, 1));

            return intersection;
        }
                   
        // getting the 3 on-screen axis reference 
        private void getPositionMatrixAndVector(OpenGL gl)
        {

            // using a temp matrix 
            mat4 tempMatrix = viewingMatrix* modelTransformMatrix;
                 
            // set the translational vector 
            translationalVector = new vec4(tempMatrix[3,0], tempMatrix[3,1], tempMatrix[3,2] , 1.0f ); // must have a cleaner way to do this. 
                    
            tempMatrix = glm.inverse(tempMatrix); // get the inverse 
                       
            // should have a cleaner way to do this. 
            tempMatrix[3, 0] = 0.0f;
            tempMatrix[3, 1] = 0.0f;
            tempMatrix[3, 2] = 0.0f;

            // transforming the fixed axis vector 
            xVector = tempMatrix * fixedX;
            yVector = tempMatrix * fixedY;
            zVector = tempMatrix * fixedZ;

            // transforming the translation vector 
            translationalVector = tempMatrix * translationalVector; 
        }

        void drawDerrick(OpenGL gl)
        {
            GLfloat bottom, top;
            if (m_eLoggingMode == LoggingMode.Time)
            {
                bottom = depthStart;
                top = depthRender;
            }
            else if (m_eLoggingDir == LoggingDir.Up)
            {
                bottom = depthStart;
                top = depthRender;
            }
            else // default: DOWN direction in DEPTH mode
            {
                bottom = depthRender;
                top = depthStart;
            }


            double topSize = 1, baseSize = 3, step = 7, height = step * baseSize;

            /*scene.applyColorTransformation(gl, new vec3(0.5f, 0.5f, 0), 0.7f);
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(-baseSize - 5, -baseSize - 5, top);   //gl.Vertex(-XBoxCoord, -YBoxCoord, top);   //gl.Vertex(-baseSize, -baseSize, top);
            gl.Vertex(baseSize + 5, -baseSize - 5, top);    //gl.Vertex(XBoxCoord, -YBoxCoord, top);    //gl.Vertex(baseSize, -baseSize, top);
            gl.Vertex(baseSize + 5, baseSize + 5, top);     //gl.Vertex(XBoxCoord, YBoxCoord, top);     //gl.Vertex(baseSize, baseSize, top);
            gl.Vertex(-baseSize - 5, -baseSize - 5, top);   //gl.Vertex(-XBoxCoord, -YBoxCoord, top);   //gl.Vertex(-baseSize, -baseSize, top);
            gl.Vertex(baseSize + 5, baseSize + 5, top);     //gl.Vertex(XBoxCoord, YBoxCoord, top);     //gl.Vertex(baseSize, baseSize, top);
            gl.Vertex(-baseSize - 5, baseSize + 5, top);    //gl.Vertex(-XBoxCoord, YBoxCoord, top);    //gl.Vertex(-baseSize, baseSize, top);
            gl.End();*/

            /*gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0, 0, top + 5);
            double step = Math.PI / 6;
            gl.Vertex(5, 0, top);
            for (double i = step; i < Math.PI * 2; i += step)
                gl.Vertex(5 * Math.Cos(i), 5 * Math.Sin(i), top);
            gl.Vertex(5, 0, top);
            gl.End();*/
			
			scene.applyColorTransformation(gl, new vec3(1, 1, 0));

            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(-topSize, -topSize, top + height);
            gl.Vertex(topSize, -topSize, top + height);
            gl.Vertex(topSize, topSize, top + height);
            gl.Vertex(-topSize, -topSize, top + height);
            gl.Vertex(topSize, topSize, top + height);
            gl.Vertex(-topSize, topSize, top + height);
            gl.End();

            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(0, 0, top + height + 2);
            gl.Vertex(-topSize, -topSize, top + height);
            gl.Vertex(0, 0, top + height + 2);
            gl.Vertex(topSize, -topSize, top + height);
            gl.Vertex(0, 0, top + height + 2);
            gl.Vertex(topSize, topSize, top + height);
            gl.Vertex(0, 0, top + height + 2);
            gl.Vertex(-topSize, topSize, top + height);

            gl.End();
            double lowerSize = baseSize, stepSize = (baseSize - topSize) / step, upperSize = baseSize - stepSize, curHeight = top, newHeight;
            for (int i = 0; i < step; i++)
            {
                newHeight = curHeight + height / step;

                gl.Begin(OpenGL.GL_LINE_STRIP);
                gl.Vertex(-lowerSize, -lowerSize, curHeight);
                gl.Vertex(lowerSize, -lowerSize, curHeight);
                gl.Vertex(lowerSize, lowerSize, curHeight);
                gl.Vertex(-lowerSize, lowerSize, curHeight);

                gl.Vertex(-lowerSize, -lowerSize, curHeight);
                gl.Vertex(-upperSize, -upperSize, newHeight);
                gl.Vertex(lowerSize, -lowerSize, curHeight);
                gl.Vertex(upperSize, -upperSize, newHeight);
                gl.Vertex(lowerSize, lowerSize, curHeight);
                gl.Vertex(upperSize, upperSize, newHeight);
                gl.Vertex(-lowerSize, lowerSize, curHeight);
                gl.Vertex(-upperSize, upperSize, newHeight);
                gl.Vertex(-lowerSize, -lowerSize, curHeight);
                gl.End();

                lowerSize = upperSize;
                upperSize -= stepSize;
                curHeight = newHeight;
            }

			gl.Begin(OpenGL.GL_LINES);
			gl.Vertex(0, 0, bottom);
			gl.Vertex(0, 0, curHeight);
			gl.End();
        }

        void drawMarkings(OpenGL gl)
        {
            int interval = m_eLoggingMode == LoggingMode.Time ? timeInterval : depthInterval;
            scene.applyColorTransformation(gl, new vec3(0.5f, 0.5f, 0.5f));
            
            // line marks at intervals of depthInterval or timeInterval
            int effInterval = depthRender < depthStart ? -interval : interval;
            int depthWrite = (int)(depthRender - depthStart) / effInterval;

            gl.Begin(OpenGL.GL_LINES);
            for (int i = 1; i <= depthWrite; i++)
            {
                gl.Vertex(5 - XBoxCoord, -YBoxCoord, depthStart + i * effInterval);
                gl.Vertex(-XBoxCoord, -YBoxCoord, depthStart + i * effInterval);

                gl.Vertex(-XBoxCoord, -YBoxCoord, depthStart + i * effInterval);
                gl.Vertex(-XBoxCoord, 5 - YBoxCoord, depthStart + i * effInterval);
            }
            gl.End();

            // line marks at X and Y intervals
            int XIntervals = (int)(2*XBoxCoord)/interval, YIntervals = (int)(2*YBoxCoord)/interval;
            gl.Begin(OpenGL.GL_LINES);
            for (int i = 0; i <= XIntervals; i++)
            {
                gl.Vertex(XBoxCoord - i*interval, -YBoxCoord, depthRender);
                gl.Vertex(XBoxCoord - i*interval, -YBoxCoord+5, depthRender);
            }
            for (int i = 0; i <= YIntervals; i++)
            {
                gl.Vertex(-XBoxCoord, YBoxCoord - i*interval, depthRender);
                gl.Vertex(-XBoxCoord+5, YBoxCoord - i*interval, depthRender);
            }
            gl.End();
        }

        void drawFlatCircles(OpenGL gl, float radius)
        {
            scene.applyColorTransformation(gl, new vec3((float)65/255, (float)105/255, 1)); // royal (bright) blue
            
            // N-S-W-E
            gl.Begin(OpenGL.GL_LINE_LOOP);
            for (float i = 0; i < 2 * Math.PI; i += 0.1f)
            {
                gl.Vertex(radius * Math.Cos(i), radius * Math.Sin(i), 0);
            }
            gl.End();

            // N-S
            gl.Begin(OpenGL.GL_LINE_LOOP);
            for (float i = 0; i < 2 * Math.PI; i += 0.1f)
            {
                gl.Vertex(0, radius * Math.Cos(i), radius * Math.Sin(i));
            }
            gl.End();

            // E-W
            gl.Begin(OpenGL.GL_LINE_LOOP);
            for (float i = 0; i < 2 * Math.PI; i += 0.1f)
            {
                gl.Vertex(radius * Math.Cos(i), 0, radius * Math.Sin(i));
            }
            gl.End();
        }

        void markDirection(OpenGL gl, vec3 location, float textSize, char direction, mat4 transformMat)
        {
            vec4 worldpos = new vec4(location, 1);
            vec4 projectedPos = new vec4();
            projectedPos = transformMat * worldpos;
            vec3 screenPos = new vec3(projectedPos.x / projectedPos.w, projectedPos.y / projectedPos.w, projectedPos.z / projectedPos.w);


            if ((screenPos.z < 1.0f) && (screenPos.z > -1.0f))
            {
                vec2 onScreen = new vec2((float)((screenPos.x + 1) / 2.0f * openGLControl.ActualWidth), (float)((screenPos.y + 1) / 2.0f * openGLControl.ActualHeight));

                gl.DrawText3D("Times New Roman", textSize, 0f, 0.2f, "");
                // draw depth on the z axis
                gl.DrawText((int)onScreen.x, (int)onScreen.y, 1, 1, 1, "Courier New", textSize, direction.ToString());
            }
        }

        void drawNorthSouthPointer(OpenGL gl, float radius)
        {
            scene.applyColorTransformation(gl, new vec3(1, 0, 0));      // north pointer - red
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(0, radius, 0);
            gl.Vertex(-0.1 * radius, 0, 0);
            gl.Vertex(0.1 * radius, 0, 0);
            gl.End();

            scene.applyColorTransformation(gl, new vec3(1, 1, 1));  // south pointer - white
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(0, -radius, 0);
            gl.Vertex(0.1 * radius, 0, 0);
            gl.Vertex(-0.1 * radius, 0, 0);
            gl.End();
        }

		void drawCompass(OpenGL gl)
		{
			int minRenderWidth = (500 - 200) / 2; // MainWindow.MinimumWidth - UIControl.FixedWidth
			float compassScale = 1.5f, compassRadius = 1.0f, aspectRatio = 1.0f;
			mat4 compassProjectionMatrix = glm.perspective(fov, aspectRatio, zNear, zFar),
					compassViewingMatrix = glm.lookAt(new vec3(2.0f, 2.0f, 0.0f), new vec3(0, 0, 0), new vec3(0, 0, 1)),
					compassTranslationMatrix = mat4.identity(),     //new mat4(new vec4(1,0,0,0), new vec4(0,1,0,0), new vec4(0,0,1,0), new vec4(25,0,-10,1)),
					compassScalingMatrix = new mat4(new vec4(compassScale, 0, 0, 0), new vec4(0, compassScale, 0, 0), new vec4(0, 0, compassScale, 0), new vec4(0, 0, 0, 1)),
					compassTransformMatrix = compassTranslationMatrix * compassRotationMatrix * compassScalingMatrix,
					compassFullTransformMatrix = compassProjectionMatrix * compassViewingMatrix * compassTransformMatrix;

			gl.Viewport(10, 5, minRenderWidth, minRenderWidth);

			// Warning: beyond this point (gl.Disable(OpenGL.GL_DEPTH_TEST)), any 3D objects drawn after the compass will not have the same depth buffer with those objects drawn before the compass
			// Consequence: Any objects (including compass) drawn between this point and the next point (gl.Enable(OpenGL.GL_DEPTH_TEST)) will always overlay the previous rendering, regardless of actual 3D depth
			gl.Disable(OpenGL.GL_DEPTH_TEST);
			scene.applyTransformation(gl, compassProjectionMatrix, compassTransformMatrix, compassFullTransformMatrix);
			drawCompassObject(gl, compassRadius);

			scene.unbinding(gl);

			// text: mark N-S-W-E of compass
			markDirection(gl, new vec3(0, compassRadius, 0), 0.1f * compassRadius, 'N', compassFullTransformMatrix);
			markDirection(gl, new vec3(0, -compassRadius, 0), 0.1f * compassRadius, 'S', compassFullTransformMatrix);
			markDirection(gl, new vec3(compassRadius, 0, 0), 0.1f * compassRadius, 'E', compassFullTransformMatrix);
			markDirection(gl, new vec3(-compassRadius, 0, 0), 0.1f * compassRadius, 'W', compassFullTransformMatrix);

			//restore viewport after drawing compass
			gl.Viewport(0, 0, (int)openGLControl.ActualWidth, (int)openGLControl.ActualHeight);
			gl.Enable(OpenGL.GL_DEPTH_TEST);
		}
					
        void drawCompassObject(OpenGL gl, float radius)
        {
            /*//test
            scene.applyColorTransformation(gl, new vec3(0, 1, 0));
            gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0, 0, 1);
            gl.Vertex(-1, -1, 0);
            gl.Vertex(1, -1, 0);
            gl.Vertex(1, 1, 0);
            gl.Vertex(-1, 1, 0);
            gl.Vertex(-1, -1, 0);
            gl.End();
            */

            gl.Clear(OpenGL.GL_DEPTH_BUFFER_BIT);
            drawFlatCircles(gl, radius); 
            drawNorthSouthPointer(gl, radius);
        }

        void writeTextAtLocation(OpenGL gl, vec3 position, string text, float r, float g, float b, float fontSize)
        {
            //scene.unbinding(gl);
            
            vec4 worldpos = new vec4(position, 1);
            vec4 projectedPos = new vec4();
            projectedPos = fullT * worldpos;
            vec3 screenPos = new vec3(projectedPos.x / projectedPos.w, projectedPos.y / projectedPos.w, projectedPos.z / projectedPos.w);


            if ((screenPos.z < 1.0f) && (screenPos.z > -1.0f) && (screenPos.x < 1.0f) && (screenPos.x > -1.0f) && (screenPos.y < 1.0f) && (screenPos.y > -1.0f))
            {
                vec2 onScreen = new vec2((float)((screenPos.x + 1) / 2.0f * openGLControl.ActualWidth), (float)((screenPos.y + 1) / 2.0f * openGLControl.ActualHeight));

                gl.DrawText3D("Times New Roman", fontSize, 0f, 0.2f, "");
                // draw depth on the z axis
                gl.DrawText((int)onScreen.x, (int)onScreen.y, r, g, b, "Courier New", fontSize, text);
            }
           
        }
        
        //    bool writePicking = false; 
         private void Picking_Operation(OpenGL gl, mat4 ftm)
        {
            // set the binding for both the shaders and the framebuffer 
            picking.binding(gl);

            //  Clear the color and depth buffer for the current framebuffer 
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            // send all the matrices to the current picking shader code. 
            picking.applyTransformation(gl, ftm);

            // rendering the tunnel. This rendering is not displayed, but only rendered to the created framebuffer and onto the texture.  
            scene.Render(gl, translationalVector); // render the tunnel  

            // can change to any other method, as long as the mouse position is provided

			const int controlPanelWidth = 200;	// if the UI control panel width changes, need to change this value
            pick_Depth_f = picking.pickPixel(gl, (int)(picking_Mouse_Loc.X - controlPanelWidth), (int)(openGLControl.ActualHeight - picking_Mouse_Loc.Y - 1), ref click_depth);
            pick_Depth = (int)pick_Depth_f;

			objWriter.WriteLine("picking_Mouse_Loc=" + picking_Mouse_Loc.X.ToString() + ", " + picking_Mouse_Loc.Y.ToString());
			objWriter.WriteLine("pick_Depth=" + pick_Depth);
			objWriter.Flush();

            picking.unbinding(gl);
            
        }

        // helper function to write vertex 
        void glVertex3d(OpenGL gl, vec4 v)
        {
            gl.Vertex(v.x, v.y, v.z);
        }


      

        int depthCounter = 0;
        float depthStart = 0;
        float depthAnchor = 0;  // the starting/anchor depth at the current rendering scene
        int startdraw = 0;
        int XBoxCoord = 100;
        int YBoxCoord = 100;
        int timeInterval = 10;
        int depthInterval = 10;
        /// <summary>
        /// Handles the OpenGLDraw event of the openGLControl1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="SharpGL.SceneGraph.OpenGLEventArgs"/> instance containing the event data.</param>
        private void openGLControl_OpenGLDraw(object sender, OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            //test if depthRender is out of screen, update the depthAnchor to move towards depthRender to adjust the screen view
            if (testPointOutsideScreen(gl, new vec3(0, 0, depthRender)))
                depthAnchor = depthRender + 0.5f*(depthRender-depthAnchor); 

            //viewingMatrix = glm.lookAt(new vec3(30, 30, depthStart), new vec3(0, 0, depthStart), new vec3(0, 0, 1));
            //viewingMatrix = glm.lookAt(new vec3(30, 30, depthRender), new vec3(0, 0, depthRender), new vec3(0, 0, 1));
			//viewingMatrix = glm.lookAt(new vec3(0, 30, depthRender), new vec3(0, 0, depthRender), new vec3(0, 0, 1));
            viewingMatrix = glm.lookAt(new vec3(30, 30, depthAnchor), new vec3(0, 0, depthAnchor), new vec3(0, 0, 1));

            mgTransformMatrix = glm.rotate(modelTransformMatrix, mgDecValue, new vec3(0.0f, 0.0f, 1.0f));

            // get the 3 important vectors 
            getPositionMatrixAndVector(gl);
            //fullT = projectionMatrix * viewingMatrix * modelTransformMatrix;
            fullT = projectionMatrix * viewingMatrix * mgTransformMatrix;

            // picking operation. Can control whether to activate this or not 
            if (pickingFlag == 1)
                Picking_Operation(gl, fullT);
            
			// Clear the color and depth buffer.
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            //gl.Enable(OpenGL.GL_LINE_SMOOTH);
            gl.Enable(OpenGL.GL_POLYGON_SMOOTH); 
            gl.Enable(OpenGL.GL_POLYGON_OFFSET_FILL); 
            gl.PolygonOffset(1.0f, 5);
            //gl.LineWidth(0.1f); 

            // send all the matrices to the shader code. 
            scene.binding(gl);
            scene.applyLights(gl, new vec3(XBoxCoord, YBoxCoord, depthStart), new vec3(30, 30, depthStart));
            scene.applyTransformation(gl, projectionMatrix, mgTransformMatrix, fullT);
            scene.applyPicking(gl, pick_Depth);
            scene.Render(gl, translationalVector); // render the tunnel  

            // rotate back for the boundingbox 
            fullT = projectionMatrix * viewingMatrix * modelTransformMatrix;
            scene.applyTransformation(gl, projectionMatrix, modelTransformMatrix, fullT);

            // here we just start drawing the lines 

            scene.applyStartLines(gl, 1); // +ve means start drawing lines 

            // z- axis
            scene.applyColorTransformation(gl, new vec3(0, 0, 1));
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthRender);
            gl.End();

            // x-axis
            scene.applyColorTransformation(gl, new vec3(1, 0, 0));
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthRender);
            gl.End();

            // y-axis
            scene.applyColorTransformation(gl, new vec3(0, 1, 0));
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthRender);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthRender);
            gl.End();

            // bounding box
            scene.applyColorTransformation(gl, new vec3(1, 1, 1));
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(XBoxCoord, YBoxCoord, depthRender);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthRender);
            gl.Vertex(XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, -YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthStart);
            gl.Vertex(-XBoxCoord, YBoxCoord, depthRender);
            gl.End();

            // derrick
            drawDerrick(gl);

            // interval marking
            drawMarkings(gl);

			// Note: Untextured tunnel is drawn semi-transparent, so it has to be drawn last after all opaque objects to produce a correct depth test 
			// (otherwise, transparency will still hide the derrick line unless depth test is disabled, 
			// but disabling depth test will make untextured tunnel floats in front of opaque objects from every viewpoint, which is inaccurate)
			if (StreamFlag == 1)
				scene.drawUntexturedTunnel(gl, tunnelPhysicalDataHolder); // untextured tunnel for streaming option 

			// draw compass
			drawCompass(gl);

            // text: depth intervals
            writeTextAtLocation(gl, new vec3(-XBoxCoord, -YBoxCoord, depthRender), ((m_eLoggingMode == LoggingMode.Time ? 1.0f : -1.0f) * depthRender).ToString(), 1, 1, 0, 15.0f);
            int interval = m_eLoggingMode == LoggingMode.Time ? timeInterval : depthInterval;
            int effInterval = depthRender < depthStart ? -interval : interval;
            int depthWrite = (int)(depthRender - depthStart) / effInterval;
            //while(i < depthWrite)
            for(int i=1;i<=depthWrite;i++)
            {
                writeTextAtLocation(gl, new vec3(-XBoxCoord, -YBoxCoord, (depthStart + i * effInterval)), ((m_eLoggingMode == LoggingMode.Time ? 1.0f : -1.0f) * (depthStart + i * effInterval)).ToString(), 1, 1, 1, 10.0f);
            }
            writeTextAtLocation(gl, new vec3(-XBoxCoord, -YBoxCoord, depthStart), ((m_eLoggingMode == LoggingMode.Time ? 1.0f : -1.0f) * depthStart).ToString(), 1, 1, 1, 10.0f);

            // text: X & Y intervals
            int XIntervals = (int)(2 * XBoxCoord) / interval, YIntervals = (int)(2 * YBoxCoord) / interval;
            for (int i = 0; i <= XIntervals; i++)
            {
                writeTextAtLocation(gl, new vec3(XBoxCoord-i*interval, -YBoxCoord, depthRender-2), (XBoxCoord-i*interval).ToString(), 1, 1, 1, 10.0f);
            }
            for (int i = 0; i <= YIntervals; i++)
            {
                writeTextAtLocation(gl, new vec3(-XBoxCoord, YBoxCoord-i*interval, depthRender-2), (YBoxCoord-i*interval).ToString(), 1, 1, 1, 10.0f);
            }

            scene.binding(gl);

            scene.applyStartLines(gl, -1); // -ve means stop drawing lines 

            scene.applyColorTransformation(gl, new vec3(0, 0, 1));

        }


        
        /// <summary>
        /// Handles the OpenGLInitialized event of the openGLControl1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="SharpGL.SceneGraph.OpenGLEventArgs"/> instance containing the event data.</param>
        private void openGLControl_OpenGLInitialized(object sender, OpenGLEventArgs args)
        {
            //  TODO: Initialise OpenGL here.

            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;
         

            //  Initialise the scene.
           scene.Initialize(gl);

           // set the picking framebuffer
           picking.Initialize(gl);
           picking.generateBuffers(openGLControl.OpenGL, (int)openGLControl.ActualWidth, (int)openGLControl.ActualHeight); 

            //  Set the clear color.
            gl.ClearColor(0, 0, 0, 0);
                  
        
          }
        
        // when you click Apply/OK on the color dialog this sets up the color to be changed to in the fragment shader. 
        public void applyColorScheme()
        {
            string [] colorvalue; 
            vec3[] rawValues ;
            vec3 highC, lowC; 

            rawValues = new vec3[256]; // this is raw colors from the se

            if (userColorSelection == false) // pre selected palette 
            {

                for (int a = 0; a < rgbValues.Count(); a++)
                {
                    colorvalue = rgbValues[a].Split(',');
                    rawValues[a] = new vec3(int.Parse(colorvalue[0]), int.Parse(colorvalue[1]), int.Parse(colorvalue[2]));
                }
                scene.castColors(openGLControl.OpenGL, ref rawValues, rgbValues.Count() - 1 , 0.0f, 1.0f); 
            }
            else
            {
                // user select 2 colors and then select the number of levels. 

                colorvalue = rgbValues[0].Split(',');
                lowC = new vec3(int.Parse(colorvalue[0]), int.Parse(colorvalue[1]), int.Parse(colorvalue[2]));
                colorvalue = rgbValues[1].Split(',');
                highC = new vec3(int.Parse(colorvalue[0]), int.Parse(colorvalue[1]), int.Parse(colorvalue[2]));

                if (linearColor == true)
                {
                    for (int a = 0; a < userColorLevels; a++)
                        rawValues[a] = highC * ((float)a / (userColorLevels - 1)) + lowC * ((float)(userColorLevels - 1 - a) / (userColorLevels - 1));
                }
                else
                {
                    double logValue;
                    double interval = 9.0f / (userColorLevels - 1); 
                    // index[0] should be 1 , index[userColorLevels] should be 10
                    for (int a = 0; a < userColorLevels; a++)
                    {
                        double b = interval * a + 1.0f ; 
                        // this is the log value. The answer is from zero to 1. 
                        logValue = Math.Log10(b);

                        rawValues[a] = highC * (float)logValue + lowC * (float) (1.0f - logValue); 

                    }


                }

                

                scene.castColors(openGLControl.OpenGL, ref rawValues, userColorLevels - 1 , (float) lowCO / 255.0f  , (float) highCO / 255.0f ); 

            }


          
        }

        
        /// <summary>
        /// Handles the Resized event of the openGLControl1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="SharpGL.SceneGraph.OpenGLEventArgs"/> instance containing the event data.</param>
        private void openGLControl_Resized(object sender, OpenGLEventArgs args)
        {
            // setting the projectionMatrix everytime you change the screen 
             projectionMatrix = glm.perspective(fov, (float)openGLControl.ActualWidth / (float)openGLControl.ActualHeight, zNear, zFar);
            // resizing the picking framebuffer 
            picking.resizeBuffers(openGLControl.OpenGL, (int)openGLControl.ActualWidth, (int)openGLControl.ActualHeight);
        }
        
        private void openGLControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(null);
                
            if (pickingFlag == 1)
            {
                double pickedDepth = doglegResult.dataList[pick_Depth].depth;
                if (m_eLoggingMode != LoggingMode.Time) pickedDepth = -pickedDepth;
                //DgDepth.Text = pickedDepth.ToString("f2");
				DgDepth.Text = pickedDepth.ToString(); 
                pickingFlag = 0;
            }
			else if (startdraw == 1)
			{
				is_Left_Button_Pressed = true;  // set to true 
				prev_Mouse_Loc = p;
				rotationPoint = computeRotationPoint(depthRender, depthStart);
			}
         }

        private void openGLControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            is_Left_Button_Pressed = false;   // set to false 
        }

        private void openGLControl_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(null);
            is_Right_Button_Pressed = true;
            init_Mouse_Loc = p;

            tempmodelTransformMatrix = mat4.identity();
            copyMat4(ref tempmodelTransformMatrix, modelTransformMatrix);

            if (startdraw == 1)
                computeRotationPoint(depthRender, depthStart);
        }

        private void openGLControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            is_Right_Button_Pressed = false; 
        }

        int pickingFlag = 0; // used to enable highlighting the selected contour when a left Ctrl button is pressed. 
        private void openGLControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;
            if (e.Key == Key.LeftCtrl)
            {
                pickingFlag = 1;
            }
        }

        private void openGLControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {  
            Point p = e.GetPosition(null);

            vec4 axisRotation;
            double rotationRate;
            vec3 tran_movement_vec;

            if (pickingFlag == 1)
            {
                picking_Mouse_Loc = p; // for picking 
            }

            if (is_Left_Button_Pressed == true) // for left mouse usage 
            {
                cur_Mouse_Loc = p;
                diff_Mouse_Loc.X = cur_Mouse_Loc.X - prev_Mouse_Loc.X;
                diff_Mouse_Loc.Y = cur_Mouse_Loc.Y - prev_Mouse_Loc.Y;

                axisRotation = (xVector * (float)diff_Mouse_Loc.Y) + (yVector * (float)diff_Mouse_Loc.X);
                axisRotation = glm.normalize(axisRotation);

                rotationRate = Math.Abs(diff_Mouse_Loc.X) + Math.Abs(diff_Mouse_Loc.Y);
                rotationRate /= 5.0f;

                // rotate the compass
                compassRotationMatrix = glm.rotate(compassRotationMatrix, glm.radians((float)rotationRate), new vec3(axisRotation));

                // activate the line above and below if you want a first person view motion 
                modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(rotationPoint));
                modelTransformMatrix = glm.rotate(modelTransformMatrix, glm.radians((float)rotationRate), new vec3(axisRotation));
                modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(-rotationPoint.x, -rotationPoint.y, -rotationPoint.z));

                prev_Mouse_Loc = cur_Mouse_Loc;
            }
            else if (is_Right_Button_Pressed == true)
            {
                cur_Mouse_Loc = p;
                diff_Mouse_Loc.X = cur_Mouse_Loc.X - init_Mouse_Loc.X;
                diff_Mouse_Loc.Y = cur_Mouse_Loc.Y - init_Mouse_Loc.Y;
                
                copyMat4(ref modelTransformMatrix, tempmodelTransformMatrix);
                         
                tran_movement_vec = new vec3(xVector) * translationSpeed ;
                tran_movement_vec *= (float) diff_Mouse_Loc.X;

              //  modelTransformMatrix = glm.translate(modelTransformMatrix, tran_movement_vec); // translate the model transform matrix

                tran_movement_vec = new vec3(yVector) * translationSpeed;
                tran_movement_vec = new vec3(0, 0, 1) * translationSpeed;
                tran_movement_vec *= -(float)diff_Mouse_Loc.Y;

               modelTransformMatrix = glm.translate(modelTransformMatrix, tran_movement_vec); // translate the model transform matrix
	        }
            else if (is_Middle_Button_Pressed == true)
            {
                cur_Mouse_Loc = p;

                diff_Mouse_Loc.X = -(cur_Mouse_Loc.X - init_Mouse_Loc.X);
                diff_Mouse_Loc.Y = -(cur_Mouse_Loc.Y - init_Mouse_Loc.Y);

                if (diff_Mouse_Loc.Y != 0)
                {
                    copyMat4(ref modelTransformMatrix, tempmodelTransformMatrix);

                    float zoomFactor = (float)diff_Mouse_Loc.Y * mouseWheelSpeed * 10.0f;

                    //modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(zVector) * (float)diff_Mouse_Loc.Y / (float)Math.Abs(diff_Mouse_Loc.Y) * mouseWheelSpeed *10.0f  * (float)Math.Abs(diff_Mouse_Loc.Y)); // translate the model transform matrix
                    modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(zVector) * zoomFactor); // translate the model transform matrix
                    
                    float distance = (float)Math.Sqrt(translationalVector.x * translationalVector.x + translationalVector.y * translationalVector.y);
                    int interval = (int)(distance/4);
                    if (interval < 5) interval = 5;
                    else if (interval > 1000) interval = 1000;
                    if (m_eLoggingMode == LoggingMode.Time)
                    {
                        timeInterval = interval;
                        Interval.Text = timeInterval.ToString();
                    }
                    else
                    {
                        depthInterval = interval;
                        Interval.Text = depthInterval.ToString();
                    }

                }
            }
        }
        
         public void copyMat4(ref mat4 newMat, mat4 original)
         {
             int a, b;
             for (a = 0; a < 4; a++)
                 for (b = 0; b < 4; b++)
                     newMat[a, b] = original[a, b]; // revert to original one. 
         }
         
        /// <summary>
        /// The scene we're drawing.
        /// </summary>
        private readonly Scene scene = new Scene();
        private readonly Picking picking = new Picking();  // this is to implement the picking 

        private void openGLControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ( e.Delta != 0 )
                modelTransformMatrix = glm.translate(modelTransformMatrix, new vec3(zVector) * e.Delta/Math.Abs(e.Delta) * mouseWheelSpeed); // translate the model transform matrix
         }
        
        private void openGLControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((e.ChangedButton == MouseButton.Middle) && (e.ButtonState == MouseButtonState.Pressed))
            {

                //writePicking = true; 

                is_Middle_Button_Pressed = true;
                Point p = e.GetPosition(null);
                init_Mouse_Loc = p;

                tempmodelTransformMatrix = mat4.identity();
                copyMat4(ref tempmodelTransformMatrix, modelTransformMatrix);
            }
        }

        private void openGLControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if ((e.ChangedButton == MouseButton.Middle) && (e.ButtonState == MouseButtonState.Released))
            {
                is_Middle_Button_Pressed = false; 
            }
        }
        
        /*********************************** COSL Functions ****************************************/
        public enum DepthToDUCoeff
        {
            FeetToDU = 30480,
            MeterToDU = 100000,
            SecondToMS = 1000, 
        }

        // the default curvesnames are listed here 
        string cDEV = "DEV";
        string cDAZ = "DAZ";
        string cAZW = "AZW";
        string cBHIOP = "BHIOP";
        string cSPRD = "SPRD"; 
        
        // this is a system call, it is called internally 
        // the objective is to collect the current depth and time and convert them to strings. 
        // no one seems to use this function, and no one seems to need anything from here. 
        // this function is not used. 
        void OnTimer(object sender, EventArgs e)
        {
            // "F2" means to only represent them to 2 decimal place. 
            
            string DepthString = (m_iService.CurDepthInDU() / (float)DepthToDUCoeff.MeterToDU).ToString("F2");

            int nTotalSec = (int)(m_iService.CurTimeInMS() / 1000);
            int nHour = (int)(nTotalSec / 3600);
            int nMin = (int)((nTotalSec - nHour * 3600) / 60);
            int nSec = (int)(nTotalSec - nHour * 3600 - nMin * 60);
            string TimeString = nHour.ToString() + " : " + nMin.ToString() + " : " + nSec.ToString();
        }

        // this function is called by "StreamButton_Click", this is the first function to kickstart the streaming operation . 
        // bootstart the various managers. 
        private bool Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_iLoggingManager = ObjectFactory.CreateObject(ObjectType.LoggingManager_Net) as ILoggingManagerNet;
            m_iService = m_iLoggingManager.Service;
            if (m_iService == null)
                return false;

            // Monitor service event like activate, deactivate and state change.
            m_iService.SeviceEvent += new ServiceEventHandler(OnServiceEvent);

            //Connect to server and create two threads to receive and process data.
            bool res = m_iLoggingManager.Init("");
            if (res == false)
            {
                System.Windows.MessageBox.Show("Initializing logging manager failed!");
                return false;
            }
            return true;
        }
        
        // called from the function "Window_Loaded" , 2nd function in the list of streaming operation 
        // various types of service events. 
        // only the activate and deactive functions are done, the rest of the state_change events are not handled. 
        public void OnServiceEvent(SERVICE_EVENT eEventType, object param)
        {
            switch (eEventType)
            {
                case SERVICE_EVENT.SERVICE_EVENT_ACTIVATE:
                    {
                        //activate
                        Dispatcher.Invoke(DispatcherPriority.Normal, new ServiceEventResponsor(OnStart), false);
                    }
                    break;
                case SERVICE_EVENT.SERVICE_EVENT_DEACTIVATE:
                    {
                        // deactivate       
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ServiceEventResponsor2(Close));
                    }
                    break;
                case SERVICE_EVENT.SERVICE_EVENT_STATE_CHANGE:
                    {
                        ServiceWorkStateInfo stateInfo = param as ServiceWorkStateInfo;
                        switch (stateInfo.eWorkMode)
                        {
                            case ServiceWorkMode.Idle: { }
                                break;
                            case ServiceWorkMode.Logging:
                                {
                                    if (stateInfo.eLoggingMode == m_eLoggingMode && stateInfo.eLoggingDir == m_eLoggingDir)   // no change in logging mode & direction
                                        break;

                                    if (firstStreamInitialization && !duplicateTimeModeSignal)
                                    {
                                        System.Windows.MessageBox.Show("A change in logging mode/direction was detected. The streaming will restart.");
                                        if (isPaused) Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ServiceEventResponsor2(handlePauseResume));
                                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ServiceEventResponsor2(refreshCurve));
										Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ServiceEventResponsor2(clearDgInputs));
                                    }
                                    else if (firstStreamInitialization && duplicateTimeModeSignal)
                                    {
                                        duplicateTimeModeSignal = false; // flip back the signal
                                        break;
                                    }
                                    else
                                        firstStreamInitialization = true;

                                    if (stateInfo.eLoggingMode == LoggingMode.Time)
                                    {
                                        //Change your show to Time mode
                                        //Wanghj:2016.8.15
                                        m_eLoggingMode = LoggingMode.Time;
                                        duplicateTimeModeSignal = true; // flip the signal, because the switch to time mode will invoke 2 state change signals and we only need to respond to it once
                                    }
                                    else
                                        if (stateInfo.eLoggingMode == LoggingMode.Depth)
                                        {
                                            //Wanghj:2016.8.15
                                            m_eLoggingMode = LoggingMode.Depth;
                                            if (stateInfo.eLoggingDir == LoggingDir.Down)
                                            {
                                                //Wanghj:2016.8.15
                                                m_eLoggingDir = LoggingDir.Down;
                                                // depth is constantly going upwards ... 
                                            }
                                            else
                                            {
                                                //Wanghj:2016.8.15
                                                m_eLoggingDir = LoggingDir.Up;
                                                // depth is constantly going downwards
                                            }
                                        }
                                }
                                break;
                        }
                    }
                    break;
            }
        }


        // 3rd function in the streaming operation 
        //When receiving activate info from logging software, start update visual thread, because activate means logging software begin to gather data
        //starts the icurveManager to collect the various curveData. 
        //have to find out how does the curve information collection takes place 
        void OnStart(bool bRelogging = false)
        {
            ICurveManager iCurveManager = m_iService.GetCurveManager();
            if (iCurveManager == null)
                return;

            //Wanghj:2016.8.18
            tboxDev.ItemsSource = null;
            tboxDaz.ItemsSource = null;
            tboxAzw.ItemsSource = null;
            tboxCap.ItemsSource = null;
            tboxText.ItemsSource = null; // color 

            //Wanghj:2016.8.18
            List<String> oneDimensionCurveNames = GetCurveNamesForStreamMode(iCurveManager, true);
            tboxDev.ItemsSource = oneDimensionCurveNames;
            tboxDaz.ItemsSource = oneDimensionCurveNames;
            tboxAzw.ItemsSource = oneDimensionCurveNames;

            List<String> allCurveNames = GetCurveNamesForStreamMode(iCurveManager, false);
            tboxCap.ItemsSource = allCurveNames;   // tunnel surface info
            tboxText.ItemsSource = allCurveNames;  // color 

            //Wanghj:2016.8.15
            ICurve curveDevod = null;
            if (cDEV != null)
                curveDevod = iCurveManager.GetCurve(cDEV);
            if (curveDevod != null)
            {
                ICurveData curveData = curveDevod.GetProcessCurveData();
                if (curveData != null)
                    curveData.EnableRecordingDataToFile = true;

                //Wanghj:2016.8.18
                tboxDev.SelectedValue = curveDevod.CurveAliasName;
            }
            //Wanghj:2016.8.15
            ICurve curveDazod = null;
            if (cDAZ != null)
                curveDazod = iCurveManager.GetCurve(cDAZ);
            if (curveDazod != null)
            {
                ICurveData curveData = curveDazod.GetProcessCurveData();
                if (curveData != null)
                    curveData.EnableRecordingDataToFile = true;

                //Wanghj:2016.8.18
                tboxDaz.SelectedValue = curveDazod.CurveAliasName;
            }

            //Wanghj:2016.8.15
            ICurve curveAzw = null;
            if (cAZW != null)
                curveAzw = iCurveManager.GetCurve(cAZW);
            if (curveAzw != null)
            {
                ICurveData curveData = curveAzw.GetProcessCurveData();
                if (curveData != null)
                    curveData.EnableRecordingDataToFile = true;

                //Wanghj:2016.8.18
                tboxAzw.SelectedValue = curveAzw.CurveAliasName;
            }
                        
            //Wanghj:2016.8.15
            ICurve curveSprd = null;
            if (cSPRD != null)
                curveSprd = iCurveManager.GetCurve(cSPRD);
            if (curveSprd != null)
            {
                ICurveData curveData = curveSprd.GetProcessCurveData();
                if (curveSprd != null)
                    curveData.EnableRecordingDataToFile = true;
                //Wanghj:2016.8.18
                tboxCap.SelectedValue = curveSprd.CurveAliasName;
            }
            //Wanghj:2016.8.15
            ICurve curveBhiop = null;
            if (cBHIOP != null)
                curveBhiop = iCurveManager.GetCurve(cBHIOP);
            if (curveBhiop != null)
            {
                ICurveData curveData = curveBhiop.GetProcessCurveData();
                if (curveBhiop != null)
                    curveData.EnableRecordingDataToFile = true;
                //Wanghj:2016.8.18
                tboxText.SelectedValue = curveBhiop.CurveAliasName;
            }



            //Start update visual thread
            m_bUpdateVisualThreadTerminat = false;
            m_threadUpdateVisual = new Thread(new ParameterizedThreadStart(OnThreadUpdateVisual));
            m_threadUpdateVisual.SetApartmentState(ApartmentState.STA);
            m_threadUpdateVisual.IsBackground = true;
            m_threadUpdateVisual.Start(this);
        }


        // this is the main call, to start the onThreadUpdateVisual. Streaming operation uses this to get the streaming data and render the tunnel 
        public static void OnThreadUpdateVisual(object wnd)
        {
            (wnd as MainWindow).UpdateVisual();
        }

        float depthRender = 0.0f;
        double[] position;
        
        int maxDepth = 1000000;       
  
        int DepthIndex = 0;
        int renderIndex = 0;
      
    
        int colorInformationSize;

        // the reason why this variable is placed here is to transfer the information to the renderer. 
        CurveDataMgt curveBHIOPData = new CurveDataMgt(); // stores the color information 
        CurveDataMgt tunnelPhysicalData = new CurveDataMgt(); // stores the tunnel physical information 
        CurveDataMgt tunnelPhysicalDataHolder = new CurveDataMgt();  // this is a placeholder to store those that are still waiting for the texture to come. 
        CurveDataMgt dazStorage = new CurveDataMgt();
        CurveDataMgt devStorage = new CurveDataMgt();
        CurveDataMgt azwStorage = new CurveDataMgt(); 
        CurveDataMgt doglegResult = new CurveDataMgt(); // computes and stores the dogleg result for each contour, so we do not need to store daz and dev values 


        
        void commonDataExtraction( ref ICurve gCurve , ref bool firstData , ref long gDepth, ref ulong gTime,  ref CurveDataMgt cdm, bool changeRadian) 
        {
            //Wanghj:2016.8.15
            if (gCurve != null)
            {
                if (firstData == false)
                {
                    ICurvePointData pointData = gCurve.getNewestCurvePointData();
                    if (pointData != null)
                    {
                        gDepth = -pointData.DepthInDU;
                        gTime = pointData.TimeInMS;
                        firstData = true;
                    }
                }
                else
                {
                    List<ICurvePointData> pointDataList = new List<ICurvePointData>();
                    pointDataList = GetPointListFromCurve(gCurve, -gDepth, gTime);
                    if (pointDataList != null && pointDataList.Count > 0)
                    {
                        for (int j = 0; j < pointDataList.Count; j++)
                        {
                            ICurvePointData pointData = pointDataList[j];
                            if (!IsDepthOrTimeLegal(pointData, -gDepth, gTime))
                                continue;
                            gDepth = -pointData.DepthInDU;
                            gTime = pointData.TimeInMS;

                            double[] data = new double[gCurve.CurvePointElementCount];
                            pointData.GetDataInDouble(ref data);

                            if ( changeRadian == true )
                                data[0] = data[0] / 180.0f * PI; // change to radian mode 

                            if (m_eLoggingMode == LoggingMode.Time)
                                cdm.insertNew((double)gTime / (float)DepthToDUCoeff.SecondToMS, data);
                            else
                                cdm.insertNew((double)gDepth / (float)DepthToDUCoeff.MeterToDU, data);
                        }
                    }

                }
            }                
        }



        //bool escapeStreaming = false; 




        // this seems to be the major code area. all the processing is performed through here. 
        public void UpdateVisual()
        {
            //if (isPaused)
            //    return; // skip update 

            if (m_eLoggingMode == LoggingMode.Depth)
            {

                // assigning the orientation of the data. 
                if (m_eLoggingDir == LoggingDir.Down)
                {

                    //objWriter.WriteLine("[Depth Mode] logging direction is up");
                    tunnelPhysicalDataHolder.setOrientation(false);
                    curveBHIOPData.setOrientation(false);
                    tunnelPhysicalData.setOrientation(false);
                    dazStorage.setOrientation(false);
                    devStorage.setOrientation(false);
                    azwStorage.setOrientation(false);
                    doglegResult.setOrientation(false);
                }
                else
                {
                    //objWriter.WriteLine("[Depth Mode] logging direction is down");
                    tunnelPhysicalDataHolder.setOrientation(true);
                    curveBHIOPData.setOrientation(true);
                    tunnelPhysicalData.setOrientation(true);
                    dazStorage.setOrientation(true);
                    devStorage.setOrientation(true);
                    azwStorage.setOrientation(true);
                    doglegResult.setOrientation(true);
                }
            }
            else // logging mode: Time
            {
                //objWriter.WriteLine("[Time Mode] logging direction is down");
                tunnelPhysicalDataHolder.setOrientation(true);
                curveBHIOPData.setOrientation(true);
                tunnelPhysicalData.setOrientation(true);
                dazStorage.setOrientation(true);
                devStorage.setOrientation(true);
                azwStorage.setOrientation(true);
                doglegResult.setOrientation(true);


            }





            //objWriter.Flush(); 



            vec3 Centroid;
            double BaseDistance;
            vec3[] previousHeightPoint = new vec3[1];
            vec3[] testpts;
            vec3[] rotatedPoints;

            DataParameter colorInformationStorage = new DataParameter();

            ICurveManager iCurveManager = m_iService.GetCurveManager();
            if (iCurveManager == null)
                return;

            //Here is just an example of getting one curve "SPRD", you can get all the curves from CurveManager.
            //Wanghj:2016.8.15
            ICurve curve = cSPRD != null ? iCurveManager.GetCurve(cSPRD) : null;
            bool firstSPRDPointDataCome = false;
            long depthInDUSPRD = 0;
            ulong timeInMSSPRD = 0;

            //Wanghj:2016.8.15
            ICurve curve1 = cDEV != null ? iCurveManager.GetCurve(cDEV) : null;
            bool firstDEVPointDataCome = false;
            long depthInDUDEV = 0;
            ulong timeInMSDEV = 0;

            //Wanghj:2016.8.15
            ICurve curve2 = cDAZ != null ? iCurveManager.GetCurve(cDAZ) : null;
            bool firstDAZPointDataCome = false;
            long depthInDUDAZ = 0;
            ulong timeInMSDAZ = 0;

            //Wanghj:2016.8.15
            ICurve curve3 = cBHIOP != null ? iCurveManager.GetCurve(cBHIOP) : null;
            bool firstBHIOPPointDataCome = false;
            long depthInDUBHIOP = 0;
            ulong timeInMSBHIOP = 0;


            ICurve curve4 = cAZW != null ? iCurveManager.GetCurve(cAZW) : null;
            bool firstAZWPointDataCome = false;
            long depthInDUAZW = 0;
            ulong timeInMSAZW = 0;


            if (curve == null) // sprd curve 
                System.Windows.MessageBox.Show("Missing SPRD curve");
            if (curve1 == null) // dev curve 
                System.Windows.MessageBox.Show("Missing DEV curve");
            if (curve2 == null) // daz curve 
                System.Windows.MessageBox.Show("Missing DAZ curve");
            if (curve3 == null) // color curve
                System.Windows.MessageBox.Show("Missing BHIOP curve");
            if (curve4 == null) // AZW curve 
                System.Windows.MessageBox.Show("Missing AZW curve");
            if (curve == null || curve1 == null || curve2 == null || curve3 == null || curve4 == null)
                return;

            // setting the size of the color data per contour 
            colorInformationSize = (int)curve3.CurvePointElementCount;
            colorInformationStorage.dataP = new double[colorInformationSize]; // size of colored data 

           

            //You can get newest curve data here and then you can use the data to do whatever you want.
            while (m_bUpdateVisualThreadTerminat == false)
            {
            /*    if (escapeStreaming == true)
                {
                    escapeStreaming = false;
                    return; // exit this function
                }
                */

                if (isPaused)
                    continue;

                Thread.Sleep(1); // to provide a slight delay 
                {

                    commonDataExtraction(ref curve1, ref firstDEVPointDataCome, ref depthInDUDEV, ref timeInMSDEV, ref devStorage, true);  // dev curve
                    commonDataExtraction(ref curve2, ref firstDAZPointDataCome, ref depthInDUDAZ, ref timeInMSDAZ, ref dazStorage, true);  // daz curve
                    commonDataExtraction(ref curve3, ref firstBHIOPPointDataCome, ref depthInDUBHIOP, ref timeInMSBHIOP, ref curveBHIOPData, false);  // bhiop curve
                    commonDataExtraction(ref curve4, ref firstAZWPointDataCome, ref depthInDUAZW, ref timeInMSAZW, ref azwStorage, true);  // azw
                                                     
                    if (curve != null)
                    {
                        if (firstSPRDPointDataCome == false)
                        {
                            ICurvePointData pointData = curve.getNewestCurvePointData();
                            if (pointData != null)
                            {
                                depthInDUSPRD = -pointData.DepthInDU;
                                timeInMSSPRD = pointData.TimeInMS;
                                firstSPRDPointDataCome = true;

                            }
                        }
                        else
                        {                          

                            List<ICurvePointData> pointDataList = new List<ICurvePointData>();
                            pointDataList = GetPointListFromCurve(curve, -depthInDUSPRD, timeInMSSPRD);
                            if (pointDataList != null && pointDataList.Count > 0)
                            {
                                                          

                                 for (int k = 0; k < pointDataList.Count; k++)
                                {
                                    ICurvePointData pointData = pointDataList[k];
                                             
                                    // test whether the new time or depth correspond to the mode that is stated 
                                    if (!IsDepthOrTimeLegal(pointData, -depthInDUSPRD, timeInMSSPRD))
                                        continue;
                                    depthInDUSPRD = -pointData.DepthInDU;
                                    timeInMSSPRD = pointData.TimeInMS;

                                    int NumPoints = (int)curve.CurvePointElementCount;
                                    position = new double[curve.CurvePointElementCount];
                                    position = pointData.getDataInDouble();

                                    // push the physical data to the list class 

                                    bool noDuplicate;

                                    if (m_eLoggingMode == LoggingMode.Time)
                                        noDuplicate = tunnelPhysicalData.insertNew(timeInMSSPRD / (float)DepthToDUCoeff.SecondToMS, position);
                                    else
                                    {
                                        noDuplicate = tunnelPhysicalData.insertNew((double)depthInDUSPRD / (float)DepthToDUCoeff.MeterToDU, position);
                                    }

                                     

                                    if (noDuplicate == true) // managed to successfully add one to the list. 
                                    {
                                        bool writeDebug = false;

                                        // debugging purposes. 

                                        /*
                                        objWriter.Write(azwStorage.getCount() + " " + azwStorage.getLastDepth() + " " + azwStorage.getLastDataValue() + " " + 
                                            
                                            devStorage.getCount() + " " + devStorage.getLastDepth() + " " + devStorage.getLastDataValue()
                                            + "             " + dazStorage.getCount() + " " + dazStorage.getLastDepth() + " " + dazStorage.getLastDataValue() +
                                            "               Color Count " + curveBHIOPData.getCount() + " First Depth = " + curveBHIOPData.getFirstDepth() + " Last Depth " + curveBHIOPData.getLastDepth() + "                 "
                                            + DepthIndex + " " + tunnelPhysicalData.getLastDepth() + " :: " + NumPoints + " ");

                                        objWriter.Flush(); */ 


                                        //writeDebug = true;

                                        double[] tempPoints = new double[NumPoints];


                                        DataParameter[] devPair = new DataParameter[2];
                                        DataParameter[] dazPair = new DataParameter[2];
                                        DataParameter azwData = new DataParameter(); 

                                        bool success = getDevDazPair((float)tunnelPhysicalData.getFirstDepth(), ref devPair, ref dazPair, ref azwData);

                                       // if ((success == true) && (curveBHIOPData.withinRange((float)tunnelPhysicalData.getFirstDepth()) == true))
                                        if  (success == true) // we can proceed once we got the dev and daz in range 
                                        {
                                            testpts = new vec3[NumPoints];
                                            rotatedPoints = new vec3[NumPoints];

                                            tunnelPhysicalData.getData(ref tempPoints); // put the data from the first in list into tempPoints. 


                                            if (FirstDepthInput == 0)
                                            {
                                                // first depth, centroid is 0
                                                Centroid.x = 0;
                                                Centroid.y = 0;
                                                Centroid.z = (float)tunnelPhysicalData.getFirstDepth();

                                                FirstDepthInput = 1;
                                                float radianIncrement = (PI * 2.0f) / NumPoints;

                                                for (int j = 0; j < NumPoints; j++)
                                                {
                                                    // calvin_change 4 
                                                    testpts[j].x = (float)(tempPoints[j] * Math.Cos(j * radianIncrement + azwData.dataP[0]) + PrevCentroid.x);
                                                    testpts[j].y = (float)(tempPoints[j] * Math.Sin(j * radianIncrement + azwData.dataP[0]) + PrevCentroid.y);
                                                    testpts[j].z = (float)tunnelPhysicalData.getFirstDepth();


                                                    rotatedPoints[j] = testpts[j];
                                                }


                                                if (writeDebug == true)
                                                {
                                                    objWriter.Write("Success! = " + devPair[0].dataP[0] + " " + dazPair[0].dataP[0] + " ");


                                                    //for (int j = 0; j < NumPoints; j++)
                                                      //  objWriter.Write("(" + rotatedPoints[j].x + "," + rotatedPoints[j].y + "," + rotatedPoints[j].z + ") ");
                                                    objWriter.WriteLine();
                                                    objWriter.Flush(); 

                                                }

                                            }
                                            else
                                            {
                                                BaseDistance = Math.Tan(devPair[0].dataP[0]) * Math.Abs((float)tunnelPhysicalData.getFirstDepth() - PrevDepthInDU);
                                                Centroid.x = (GLfloat)(BaseDistance * Math.Cos(dazPair[0].dataP[0]));
                                                Centroid.y = (GLfloat)(BaseDistance * Math.Sin(dazPair[0].dataP[0]));
                                                Centroid.z = (float)tunnelPhysicalData.getFirstDepth();

                                                vec3 v_dev;
                                                vec3 PreviousPoint;
                                                PreviousPoint.x = PrevCentroid.x;
                                                PreviousPoint.y = PrevCentroid.y;
                                                PreviousPoint.z = PrevCentroid.z;

                                                // vector joining current centroid from previous centroid
                                                v_dev = Centroid - PrevCentroid;

                                                float radianIncrement = (PI * 2.0f) / NumPoints;

                                                // testpts are the contour points on the new depth but with the same x and y values of the previous centroid
                                                for (int j = 0; j < NumPoints; j++)
                                                {
                                                    // calvin_change 5 
                                                    testpts[j].x = (float)(tempPoints[j] * Math.Cos(j * radianIncrement + azwData.dataP[0]) + PrevCentroid.x);
                                                    testpts[j].y = (float)(tempPoints[j] * Math.Sin(j * radianIncrement + azwData.dataP[0]) + PrevCentroid.y);
                                                    testpts[j].z = (float)tunnelPhysicalData.getFirstDepth();


                                                }

                                                // rotate about the previous centroid
                                                // calvin_change 6 
                                                generateTurn((float)(devPair[0].dataP[0]), (float)(dazPair[0].dataP[0]), v_dev, ref rotatedPoints, (float)Math.Abs(tunnelPhysicalData.getFirstDepth() - PrevDepthInDU), PreviousPoint, testpts);

                                                if (writeDebug == true)
                                                {
                                                    objWriter.Write("Success! = " + devPair[0].dataP[0] + " " + dazPair[0].dataP[0] + " ");

                                                    objWriter.Write("PrevDepthInDU = " + PrevDepthInDU + "    ");
                                                    objWriter.Write("base = " + BaseDistance + "    ");
                                                    objWriter.Write("value zero = " + tempPoints[0] + "     ");
                                                    objWriter.Write("Centroid = (" + Centroid.x + "," + Centroid.y + "," + Centroid.z + ")          ");
                                                    objWriter.Write("PrevCentroid = (" + PrevCentroid.x + "," + PrevCentroid.y + "," + PrevCentroid.z + ")          ");



                                                  //  for (int j = 0; j < NumPoints; j++)
                                                    for (int j = 0; j < 1; j++)
                                                    {
                                              //          objWriter.Write("(" + rotatedPoints[j].x + "," + rotatedPoints[j].y + "," + rotatedPoints[j].z + ") ");
                                                        objWriter.Write("Testpoint zero = (" + testpts[j].x + "," + testpts[j].y + "," + testpts[j].z + ") ");
                                                    }
                                                                                                       

                                                    objWriter.WriteLine();
                                                    objWriter.Flush(); 

                                                }

                                            }

                                            // need the depthStart to start the view at the right depth
                                            if (depthCounter == 0)
                                            {
                                                depthRender = (float)tunnelPhysicalData.getFirstDepth();

                                                // calvin_change 8
                                                depthStart = (float)tunnelPhysicalData.getFirstDepth();
                                                depthAnchor = depthStart;
                                                depthCounter = 1;
                                            }



                                            // save the dogleg value 
                                            double[] dgValue = new double[3];
                                            dgValue[0] = computeDogLeg(devPair, dazPair);
                                            dgValue[1] = devPair[0].dataP[0];
                                            dgValue[2] = dazPair[0].dataP[0];
                                            doglegResult.insertNew(tunnelPhysicalData.getFirstDepth(), dgValue);

                                            


                                            tunnelPhysicalData.setVectorData(rotatedPoints);  // set the vector points . 

                                            PrevDepthInDU = (float)tunnelPhysicalData.getFirstDepth();


                                            DataParameter dpTemp = new DataParameter(); // setup a new data parameter
                                            tunnelPhysicalData.getFirstFullElement(ref dpTemp); // extract the first data 
                                            tunnelPhysicalDataHolder.insertNew(dpTemp);  // put it into the tempHolder 
                                            tunnelPhysicalData.deleteFirst(); // remove the first 

                                            


                                            // update Previous positions to add on to the next centroid (relative)
                                            PrevCentroid.x += Centroid.x;
                                            PrevCentroid.y += Centroid.y;
                                            PrevCentroid.z = Centroid.z;

                                            //PrevDepthInDU = (float)SaveContourInfo[renderIndex].depth;
                                            // calvin_change 10
                                        
                                  
                                            
                                        }
                                        else // if success != 0 ... it means cannot find the required daz and dez point. 
                                        {
                                            if (writeDebug == true)
                                            {
                                                objWriter.WriteLine("Failed! ");

                                               

                                            }
                                        }
                                                                                                                  


                                        // this is after testing for the dez and daz
                                        // now we test if the holder class is within range of the color 
                                        //if ( ( tunnelPhysicalDataHolder.getDataCount() > 0 ) && (curveBHIOPData.withinRange((float)tunnelPhysicalDataHolder.getFirstDepth()) == true)) 
                                        if (tunnelPhysicalDataHolder.getDataCount() > 0)
                                        {
                                            if (curveBHIOPData.withinRange((float)tunnelPhysicalDataHolder.getFirstDepth()) == true)
                                            {

                                                // calvin_change 7
                                                //depthRender = (float)tunnelPhysicalDataHolder.getFirstDepth();                                          

                                                // calvin_change 9 
                                                tunnelPhysicalDataHolder.setBoundingBox(ref XBoxCoord, ref YBoxCoord);


                                                // sent event to the textbox to add one contour to the renderer 
                                                DepthTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.TextBox>(ShowText), DepthTextBox);

                                                renderIndex++;

                                            }
                                            
											depthRender = (float)tunnelPhysicalDataHolder.getLastDepth(); // update of depthRender as new contours (without texture) stream in
                                            Action a = delegate { DepthTextBox.Text = (Math.Abs(depthRender)).ToString(); };
                                            DepthTextBox.Dispatcher.Invoke(a);
                                            
                                        }

                                                                          
                                    }
                                    else // if the number of success (levels) exceed the depth
                                    {
                                        //  System.Windows.MessageBox.Show("Too Many Points");
                                        //  return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        //Wanghj:2016.8.18
        List<String> GetCurveNamesForStreamMode(ICurveManager iCurveManager, bool getOneDimensionCurves)
        {
            if (iCurveManager == null)
                return null;
            List<ICurve> allCurves = iCurveManager.GetCurveList();
            List<String> curvesNames = new List<String>();
            if (allCurves == null)
                return null;

            foreach (ICurve curve in allCurves)
            {
                if (getOneDimensionCurves)
                {
                    if (curve.CurvePointElementCount == 1)
                        curvesNames.Add(curve.CurveAliasName);
                }
                else
                    curvesNames.Add(curve.CurveAliasName);
            }
            curvesNames.Sort();
            return curvesNames;
        }  



        //Wanghj:2016.8.15
        public List<ICurvePointData> GetPointListFromCurve(ICurve curve, long newestDepthInDU, ulong newesTimeInMS)
        {
            if (curve == null)
                return null;
            ICurveData iCurveData = curve.GetProcessCurveData();
            if (iCurveData != null)
            {
                if (m_eLoggingMode == LoggingMode.Time)
                    return iCurveData.GetNewGeneratedCurvePointDataFromTimeInMS(newesTimeInMS);
                else
                    return iCurveData.GetNewGeneratedCurvePointDataFromDepthInDU(newestDepthInDU);
            }
            else
                return null;
        }

        //Wanghj:2016.8.15
        public bool IsDepthOrTimeLegal(ICurvePointData iCurvePointData, long newestDepthInDU, ulong newestTimeInMS)
        {
            if (m_eLoggingMode == LoggingMode.Time)
            {
                if (iCurvePointData.TimeInMS <= newestTimeInMS)
                    return false;
            }
            else
            {
                if (m_eLoggingDir == LoggingDir.Up && iCurvePointData.DepthInDU >= newestDepthInDU)
                {
             //       objWriter.WriteLine("Testing: current = " + iCurvePointData.DepthInDU + " , compare = " + newestDepthInDU); 
                    return false;
                }
                if (m_eLoggingDir == LoggingDir.Down && iCurvePointData.DepthInDU <= newestDepthInDU)
                    return false;
            }
            return true;
        }


        // calvin - showText is used as a means to call add one more slice. 
        // go back to UI to add another contour
        private void ShowText(System.Windows.Controls.TextBox p)
        {
            OpenGL gl = openGLControl.OpenGL;

            
            // for load from file option, if there is any texture, it has already been pushed into the scene class. So here we just push the slices to it. 
            if (LoadFileFlag == 1)
            {

                float[] extractPhysicalData = new float[tunnelPhysicalData.getVectorDataSize()];
                tunnelPhysicalData.getData(ref extractPhysicalData);

                if (extractPhysicalData[0] != double.NaN)
                    scene.createOneSlice(gl, extractPhysicalData, tunnelPhysicalData.getFirstDepth());

                // remove the first data 
                tunnelPhysicalData.deleteFirst();

            
            }
            else
            {

                if ((tunnelPhysicalDataHolder.getDataCount() > 0) && (curveBHIOPData.withinRange((float)tunnelPhysicalDataHolder.getFirstDepth()) == true))
                {
                    
                    if (renderIndex == 0) // somehow we cannot initialize this at the main function, maybe the gl is not initialized yet.
                    {
                        // for the first contour that comes in
                        scene.reInitialize(openGLControl.OpenGL, tunnelPhysicalDataHolder.getVectorDataSize() / 3);
                        scene.generatePrepareStreamingTexture(openGLControl.OpenGL, colorInformationSize, tunnelPhysicalData.upwards); // we still use the upwards from the physical data 
                    }

                    float[] extractPhysicalData = new float[tunnelPhysicalDataHolder.getVectorDataSize()];
                    tunnelPhysicalDataHolder.getData(ref extractPhysicalData);

                    if (extractPhysicalData[0] != double.NaN)
                    {
                        // dump all color information
                        scene.dumpAllColorInformation(gl, ref curveBHIOPData);
                        scene.createOneSlice(gl, extractPhysicalData, tunnelPhysicalDataHolder.getFirstDepth());
                    }

                    // remove the first data 
                    tunnelPhysicalDataHolder.deleteFirst();
                }
                
            }
         
        }

        // Calvin check: where did this appear at?  
        private void ShowText1(System.Windows.Controls.TextBox p)
        {
            OpenGL gl = openGLControl.OpenGL;
        }

        //When get deactivate info from logging software, close the window and stop the update thread.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            //if (LoadFileFlag == 1)
            //    return;

            if (StreamFlag == 1)
            {
                m_iLoggingManager.Destroy();
                m_bUpdateVisualThreadTerminat = true;

                m_threadUpdateVisual.Abort();
                if (m_iService != null)
                {
                    m_iService.Deactivate();
                }
            }
            else
                return;
        }


        // both pairs of dev and daz have 2 element count 
        public double computeDogLeg(DataParameter[] devPair, DataParameter[] dazPair)
        {
            double I1 = devPair[0].dataP[0];
            double B1 = dazPair[0].dataP[0]; 
            double I2 = devPair[1].dataP[0];
            double B2 = dazPair[1].dataP[0]; 

            double I = I1 - I2;
            double B = B1 - B2;

            // everything is in radian 
            if (B > PI)
            {
                if (B1 > B2)
                {
                    B = B1 - B2 - PI*2.0f;
                }
                else
                {
                    B = B1 - B2 + PI * 2.0f;
                }
            }
            double IC = (I1 + I2) / 2;
            double S = Math.Abs(devPair[0].depth - devPair[1].depth); // either dev pair or daz pair of depth can be used, both should be the same 

            //double Dogleg = (Math.Sqrt(I * I + B * B * Math.Sin(IC) * Math.Sin(IC)) * Convert.ToDouble(UnitSetting.Text) / S); 
            double Dogleg = (Math.Sqrt(I * I + B * B * Math.Sin(IC) * Math.Sin(IC)) / S);  // the multiplication for the unitsetting would be done later. 

            return Dogleg;
        }

        private void getDogleg()
        {
            double[] Dev = new double[2];
            double[] Daz = new double[2];

            if (pick_Depth == -1 && (DgDepth.Text == "" ))
            {
                DogLegAns.Text = "";
                return;
            }

            double dgc=-1;
            if (pick_Depth != -1) // user clicked on the tunnel, has a pick_depth 
            {
                // uses the actual array index
                dgc = doglegResult.getElementValue(pick_Depth) * Convert.ToDouble(UnitSetting.Text);
                //System.Windows.MessageBox.Show("pick_Depth = " + pick_Depth.ToString() + ", dgc = " + dgc.ToString());

                //if (m_eLoggingMode != LoggingMode.Time)
                //    dgc = -dgc;
            }
            else // read DgDepth.Text user text input
            {
                double userTextDepthInput = double.Parse(DgDepth.Text);
                double firstDepth = doglegResult.getFirstDepth(), lastDepth = doglegResult.getLastDepth();
                if (m_eLoggingMode != LoggingMode.Time)
                {
                    firstDepth = -firstDepth;
                    lastDepth = -lastDepth;
					if (firstDepth > lastDepth)
					{
						double temp = firstDepth;
						firstDepth = lastDepth;
						lastDepth = temp;
					}
                }
               
                if (userTextDepthInput >= firstDepth && userTextDepthInput <= lastDepth) 
                {
                    if (m_eLoggingMode != LoggingMode.Time)
                        userTextDepthInput = -userTextDepthInput;
					int indexCheck = doglegResult.getIndex(userTextDepthInput);
					if (indexCheck >= 0 && indexCheck < doglegResult.dataList.Count())
					{
						dgc = doglegResult.dataList[indexCheck].dataP[0] * Convert.ToDouble(UnitSetting.Text);
						// System.Windows.MessageBox.Show("Pick depth = " + doglegResult.getIndex(userTextDepthInput));

						//if (m_eLoggingMode != LoggingMode.Time)
						//	dgc = -dgc;
					}
                }
                else
                {
                    dgc = -1;
                    System.Windows.MessageBox.Show("Invalid dogleg depth input! Enter valid range: [" + firstDepth + ", " + lastDepth + "]");
                }
            }
            DogLegAns.Text = dgc.ToString("F5");

            pick_Depth = -1;
            pick_Depth_f = -1.0f; // this is not important 
        }

        // click button for dogleg value
        private void DogLeg_Click(object sender, RoutedEventArgs e)
        {
            getDogleg();
        }

        public bool getDevDazPairStaticCall(float real_Depth, ref DataParameter[] devPair, ref DataParameter[] dazPair)
        {
            devPair[0] = new DataParameter();
            devPair[1] = new DataParameter();

            dazPair[0] = new DataParameter();
            dazPair[1] = new DataParameter();


            bool successDev = devStorage.getNearestValueStaticCall(real_Depth, ref devPair[0], ref devPair[1]);
            bool successDaz = dazStorage.getNearestValueStaticCall(real_Depth, ref dazPair[0], ref dazPair[1]);

            return (successDaz & successDev);

        }
        

        public bool getDevDazPair(float real_Depth, ref DataParameter[] devPair , ref DataParameter [] dazPair , ref DataParameter azwData)
        {
            devPair[0] = new DataParameter();
            devPair[1] = new DataParameter();

            dazPair[0] = new DataParameter();
            dazPair[1] = new DataParameter(); 


            bool successDev = devStorage.getNearestValue(real_Depth, ref devPair[0], ref devPair[1]);
            bool successDaz = dazStorage.getNearestValue(real_Depth, ref dazPair[0], ref dazPair[1]);


            // now to extract azw data 
            DataParameter azwTemp = new DataParameter();
            azwStorage.getNearestValue(real_depth, ref azwData, ref azwTemp); 



            return (successDaz & successDev);

        }

        //Create a Delegate that matches the signature of the LoadingProgressBar's SetValue method
        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value);
 
        // load from file
        int LoadFileFlag = 0;
        int StreamFlag = 0;
        int FirstDepthInput = 0;
        float PrevDepthInDU = 0;
        vec3 PrevCentroid;

        string recentOpenFile; 

        // this function follows directly after clicking on the loading file button 
        public void LoadFromFile(object sender, RoutedEventArgs e)
        {
            Pause.IsEnabled = false;    // always disable pause for loading from file

            System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Filter = "CFF files(*.cff)|*.cff";
            string fileName = string.Empty;

            // selection of a file 
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                fileName = dlg.FileName;

            recentOpenFile = fileName;

            LoadFileDetails(fileName, true);

        }

        // helper function to load all the combo boxes 
        void initializedComboBox(ref List<string> cNames, ref  System.Windows.Controls.ComboBox cboxUI, ref string cstring , ref int existFlag )
        {
            cboxUI.ItemsSource = null;
            cboxUI.ItemsSource = cNames;
            for (int i = 0; i < cboxUI.Items.Count; i++)
            {
                if (cboxUI.Items[i].ToString() == cstring)
                {
                    cboxUI.SelectedItem = cboxUI.Items[i];
                    existFlag = 1;
                    break;
                }
            }
        }



        public void LoadFileDetails(string fileName, bool defaultName)
        {
            // start drawing right away
            startdraw = 1;

            ProgressWindow progress = new ProgressWindow();
            progress.Owner = this;
            progress.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            progress.ResizeMode = ResizeMode.NoResize;
            this.IsEnabled = false;
            progress.Show();
            progress.LoadingProgressBar.Minimum = 0;
            progress.LoadingProgressBar.Maximum = 100;
            progress.LoadingProgressBar.Value = 0;

            //Stores the value of the ProgressBar
            double progressValue = 0;

            //Create a new instance of our ProgressBar Delegate that points
            // to the ProgressBar's SetValue method.
            UpdateProgressBarDelegate updatePbDelegate = new UpdateProgressBarDelegate(progress.LoadingProgressBar.SetValue);

            LoadFileFlag = 1; // indicator to detect loading from file. 
            vec3 Centroid;
            double BaseDistance;
            vec3[] previousHeightPoint = new vec3[1];
            vec3[] testpts;
            vec3[] rotatedPoints;
            int existDevFlag = 0;
            int existDazFlag = 0;
            int existAzwFlag = 0;
            int existBhiopFlag = 0;
            int existSPRDFlag = 0;

            float timeMultipler = 1.0f;  // prevents constant testing for time or depth 



            // the filename is retrieved from user input 
            if (fileName != string.Empty)
            {
                IDataManager m_iDataManager = (new DataManager()) as IDataManager;
                if (m_iDataManager != null)
                    m_iDataManager.Load(); 

                IDataFileFormat format = m_iDataManager.FormatManager.FindAppropriateFormat(fileName);

                if (format != null) 
                {
                    bool m_bCanOpenFile = format.CanOpenFile(fileName); // test if the file can be opened. 
                }

                progressValue = 1;
                Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });
                
                IDataFile dataFile = m_iDataManager.OpenFile(fileName, true);
                if (dataFile != null) // file is opened 
                {
                    // this section of code seems to fill up the combo boxes with the appropriate items 
                    // this just get the list of available curves names. 
                    List<string> curves = GetCurveNamesFromDataFile(dataFile, true);
                    curves.Sort(); // get list of curves, sort them 
                    if (curves != null)
                    {
                        initializedComboBox(ref curves, ref tboxDev, ref cDEV, ref existDevFlag);
                        initializedComboBox(ref curves, ref tboxDaz, ref cDAZ, ref existDazFlag);
                        initializedComboBox(ref curves, ref tboxAzw, ref cAZW, ref existAzwFlag);
                        initializedComboBox(ref curves, ref tboxCap, ref cSPRD, ref existSPRDFlag);
                        initializedComboBox(ref curves, ref tboxText, ref cBHIOP, ref existBhiopFlag);

                        progressValue = 5;
                        Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });

                    }
                    else
                    {
                        progress.Close();
                        this.IsEnabled = true;
                        return; // exit 
                    }

                    ICurveData curveData;
                    double[] dblIndexValues ;
                    float[] fCurvePointData ;

                    bool tunnelOrientation = true; // upwards for now 


                    // updated with COSL Code
                    if (existDevFlag == 1) // if the DEVOD curve exist in the list of curves. 
                    {
                       
                        ICurveInUDFFile curveDEVInUDF = ObjectFactory.CreateObject(ObjectType.Curve_InUDFFile) as ICurveInUDFFile;
                        curveDEVInUDF.CurveName = cDEV;
                        curveDEVInUDF.InitFromUDFFile(dataFile);
                                             
                        curveData = curveDEVInUDF.getCurveData();
                        curveData.Init(curveData.DistributeType, (int)curveDEVInUDF.CurvePointElementCount, (int)(curveDEVInUDF.CurvePointElementCount * 4));
                            
                        // these are just initialized, the next function will set them to the appropriate size. 
                        dblIndexValues = new double[1];
                        fCurvePointData = new float[1];

                        if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                        {
                            (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataDepthInDU, curveData.EndDataDepthInDU, out fCurvePointData, out dblIndexValues);
                            tunnelOrientation = false; // depth measurement means it is going downwards 
                            timeMultipler = -1.0f; // negation 
                        }
                        else
                        {
                            (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataTimeInMS, curveData.EndDataTimeInMS, out fCurvePointData, out dblIndexValues);
                            timeMultipler = 1.0f / (float)DepthToDUCoeff.SecondToMS;  // save the need to contantly test 
                        }

                        if (fCurvePointData.Count() > 0)
                        {
                            for (int i = 0; i < dblIndexValues.Count(); i++)
                            {
                                devStorage.insertNew(dblIndexValues[i] * timeMultipler , fCurvePointData[i] / 180.0f * PI);
                            }
                        }
                  
                    }

                    progressValue = 16;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });

                    if (existAzwFlag == 1) // if the DEVOD curve exist in the list of curves. 
                    {
                        ICurveInUDFFile curveAZWInUDF = ObjectFactory.CreateObject(ObjectType.Curve_InUDFFile) as ICurveInUDFFile;
                        curveAZWInUDF.CurveName = cAZW;
                        curveAZWInUDF.InitFromUDFFile(dataFile);


                        curveData = curveAZWInUDF.getCurveData();
                        curveData.Init(curveData.DistributeType, (int)curveAZWInUDF.CurvePointElementCount, (int)(curveAZWInUDF.CurvePointElementCount * 4));

                        // these are just initialized, the next function will set them to the appropriate size. 
                        dblIndexValues = new double[1];
                        fCurvePointData = new float[1];

                        if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                        {
                            (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataDepthInDU, curveData.EndDataDepthInDU, out fCurvePointData, out dblIndexValues);
                            tunnelOrientation = false; // depth measurement means it is going downwards 
                            timeMultipler = -1.0f; // negation 
                        }
                        else
                        {
                            (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataTimeInMS, curveData.EndDataTimeInMS, out fCurvePointData, out dblIndexValues);
                            timeMultipler = 1.0f / (float)DepthToDUCoeff.SecondToMS;  // save the need to contantly test 
                        }


                        if (fCurvePointData.Count() > 0)
                        {
                            for (int i = 0; i < dblIndexValues.Count(); i++)
                            {
                                azwStorage.insertNew(dblIndexValues[i] * timeMultipler, fCurvePointData[i] / 180.0f * PI);
                            }
                        }

                    }

                    progressValue = 32;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });

                    if (existDazFlag == 1)
                    {
                        // updated with COSL Code
                        ICurveInUDFFile curveDAZInUDF = ObjectFactory.CreateObject(ObjectType.Curve_InUDFFile) as ICurveInUDFFile;
                        curveDAZInUDF.CurveName = cDAZ;
                        curveDAZInUDF.InitFromUDFFile(dataFile);
                
                            curveData = curveDAZInUDF.getCurveData();
                            curveData.Init(curveData.DistributeType, (int)curveDAZInUDF.CurvePointElementCount, (int)(curveDAZInUDF.CurvePointElementCount * 4));

                            dblIndexValues = new double[1];
                            fCurvePointData = new float[1];

                            if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                            {
                                (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataDepthInDU, curveData.EndDataDepthInDU, out fCurvePointData, out dblIndexValues);
                                tunnelOrientation = false; // depth measurement means it is going downwards 
                                timeMultipler = -1.0f; // negation 
                            }
                            else
                            {
                                (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataTimeInMS, curveData.EndDataTimeInMS, out fCurvePointData, out dblIndexValues);
                                timeMultipler = 1.0f / (float)DepthToDUCoeff.SecondToMS;  // save the need to contantly test 
                            }

                            if (fCurvePointData.Count() > 0)
                            {
                                for (int i = 0; i < dblIndexValues.Count(); i++)
                                {
                                    dazStorage.insertNew(dblIndexValues[i] * timeMultipler, fCurvePointData[i] / 180.0f * PI);
                                }
                            }
                  
                    }

                    progressValue = 64;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });

                    if (existBhiopFlag == 1) // color related information
                    {
                        ICurveInUDFFile curveBHIOPInUDF = ObjectFactory.CreateObject(ObjectType.Curve_InUDFFile) as ICurveInUDFFile;
                        curveBHIOPInUDF.CurveName = cBHIOP;
                        curveBHIOPInUDF.InitFromUDFFile(dataFile);
                        List<ICurvePointData> listPointsBHIOP = new List<ICurvePointData>();
               
                            curveData = curveBHIOPInUDF.getCurveData();
                            curveData.Init(curveData.DistributeType, (int)curveBHIOPInUDF.CurvePointElementCount, (int)(curveBHIOPInUDF.CurvePointElementCount * 4));
                            if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                                listPointsBHIOP = curveData.GetNewGeneratedCurvePointDataDepthBetween(curveData.FirstDataDepthInDU, curveData.EndDataDepthInDU);
                            else
                                listPointsBHIOP = curveData.GetNewGeneratedCurvePointDataTimeBetween(curveData.FirstDataTimeInMS, curveData.EndDataTimeInMS);



                            if (listPointsBHIOP.Count > 0)
                            {
                                 for (int i = 0; i < listPointsBHIOP.Count; i++)
                                {
                                    ICurvePointData pointData = listPointsBHIOP[i];
                          
                                    if (pointData != null)
                                    {
                                        double depthInDU = (double) pointData.DepthInDU / (float)DepthToDUCoeff.MeterToDU ;
                                        ulong timeInMS = pointData.TimeInMS;

                                        double[] data = new double[curveBHIOPInUDF.CurvePointElementCount];
                                        pointData.GetDataInDouble(ref data);

                                         // calvin change 3 
                                        if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                                            curveBHIOPData.insertNew(-depthInDU, data); 
                                        else
                                            curveBHIOPData.insertNew(timeInMS / (float)DepthToDUCoeff.SecondToMS , data);

                                    }
                                 }
                            }
                  
                    }

                    
                    // set the orientation of the holder 
                    tunnelPhysicalDataHolder.setOrientation(tunnelOrientation);
                    curveBHIOPData.setOrientation(tunnelOrientation);
                    tunnelPhysicalData.setOrientation(tunnelOrientation);
                    dazStorage.setOrientation(tunnelOrientation);
                    devStorage.setOrientation(tunnelOrientation);
                    azwStorage.setOrientation(tunnelOrientation);
                    doglegResult.setOrientation(tunnelOrientation);
                    scene.setOrientation(tunnelOrientation); 
                  
                    

                    progressValue = 80;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });

                    if (existSPRDFlag == 0)
                    {
                        progress.Close();
                        this.IsEnabled = true;
                        System.Windows.MessageBox.Show("SPRD curve invalid");
                        return; 
                    }
                    
                    ICurveInUDFFile curveSPRDInUDF = ObjectFactory.CreateObject(ObjectType.Curve_InUDFFile) as ICurveInUDFFile;
                    curveSPRDInUDF.CurveName = cSPRD;
                    curveSPRDInUDF.InitFromUDFFile(dataFile);
                                 
                    curveData = curveSPRDInUDF.getCurveData();
                    curveData.Init(curveData.DistributeType, (int)curveSPRDInUDF.CurvePointElementCount, (int)(4 * curveSPRDInUDF.CurvePointElementCount));

                    dblIndexValues = new double[1];
                    fCurvePointData = new float[1];
                    if (curveData.DistributeType == CurveDataDistributeType.CurveDataDistributeType_DepthEvenly)
                    {
                        (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataDepthInDU, curveData.EndDataDepthInDU, out fCurvePointData, out dblIndexValues);
                        timeMultipler = -1.0f; 
                    }
                    else
                    {
                        (curveData as ICurveDataInUDFFile).ReadSamples(curveData.FirstDataTimeInMS, curveData.EndDataTimeInMS, out fCurvePointData, out dblIndexValues);
                        timeMultipler = 1.0f / (float)DepthToDUCoeff.SecondToMS;  // save the need to contantly test 
                    }



                    int NumPoints = (int)curveSPRDInUDF.CurvePointElementCount; // number of points in one single contour


                    if (NumPoints < 4) // min 4 point to form a tunnel contour 
                    {
                        progress.Close();
                        this.IsEnabled = true;
                        System.Windows.MessageBox.Show("SPRD curve invalid");
                        return; 
                    }
                    
                    ContourPositions = new GLfloat[NumPoints * 3];  // why need to multiply by 6? 
                    testpts = new vec3[NumPoints];  
                    rotatedPoints = new vec3[NumPoints];

                    // to reinitialize the tunnel size to the file that is loaded 
                    scene.reInitialize(openGLControl.OpenGL, NumPoints);
                    if (existBhiopFlag == 1)
                    {
                        scene.generateTextures(openGLControl.OpenGL, ref curveBHIOPData);
                        curveBHIOPData.clearAll(); // clear all data now, not required. 
                    }
                    else
                        scene.generatePlainTexture(openGLControl.OpenGL); 

                    if (dblIndexValues.Count() > 0)
                    {
                        double depthInDU = 0.0f;
                        //ulong timeInMS = 0;

                        PrevCentroid = new vec3(0, 0, 0); // reset to zero point. 
                            
                        for (int i = 0; i < dblIndexValues.Count(); i++)
                        {

                            depthInDU = dblIndexValues[i] * timeMultipler;

                             
                            double[] data = new double[curveSPRDInUDF.CurvePointElementCount];
                            position = new double[curveSPRDInUDF.CurvePointElementCount];
                            int startIndex = i * (int)curveSPRDInUDF.CurvePointElementCount;
                            int length = (int)(curveSPRDInUDF.CurvePointElementCount);
                            Array.Copy(fCurvePointData, startIndex, position, 0, length); // copy from fcurvePointData at startIndex to position at 0 for size length

                            tunnelPhysicalData.insertNew( (double) depthInDU , position);
                                          
                            DataParameter[] devPair = new DataParameter[2];
                            DataParameter[] dazPair = new DataParameter[2];
                                    
                            bool success = getDevDazPairStaticCall((float)depthInDU, ref devPair, ref dazPair); 

                            // fill up dev and daz values before adding a new contour to buffer
                            if (success == true)
                            {
                                float azwIncrement = (float) azwStorage.getCurrentAZWvalue(); 

                                // calculate the new centroid position
                                if (FirstDepthInput == 0) // a boolean to detect if this is the first depth
                                {
                                    // first depth, centroid is 0
                                    Centroid.x = 0;
                                    Centroid.y = 0;
                                    Centroid.z = (float) depthInDU ; 
                                                                                
                                    FirstDepthInput = 1; // boolean to control. 

                                    float radianIncrement = (PI * 2.0f) / NumPoints;  
                                        
                                    for (int j = 0; j < NumPoints; j++) // 30 points 
                                    {
                                        // cannot use 12.0f ..... change it to something more efficient. 
                                        testpts[j].x = (float)(position[j] * Math.Cos(j * radianIncrement + azwIncrement));
                                        testpts[j].y = (float)(position[j] * Math.Sin(j * radianIncrement + azwIncrement));
                                        testpts[j].z = (float)depthInDU ;

                                        rotatedPoints[j] = testpts[j]; // rotated point is the same. 

                                    }
                                }
                                else
                                {
                                    // baseDistance is the distance in the XY plane.  // if the depth do not change, the centroid will remain zero
                                    BaseDistance = Math.Tan(devPair[0].dataP[0]) * Math.Abs(depthInDU - PrevDepthInDU);
                                    // centroid x & y coordinate space is relative coordinate space. 
                                    Centroid.x = (GLfloat)(BaseDistance * Math.Cos(dazPair[0].dataP[0]));
                                    Centroid.y = (GLfloat)(BaseDistance * Math.Sin(dazPair[0].dataP[0]));
                                    Centroid.z = (float) depthInDU ; // this is the actual depth that is obtained 
                                    vec3 v_dev;
                                    vec3 PreviousPoint;

                                    PreviousPoint = PrevCentroid; // previous point is the previous centroid position 
                                        
                                    // vector joining current centroid from previous centroid
                                    // vector from previous centroid to centroid 
                                    v_dev = Centroid - PrevCentroid;  

                                        float radianIncrement = (PI * 2.0f) / NumPoints;  


                                    // testpts are the contour points on the new depth but with the same x and y values of the previous centroid
                                    for (int j = 0; j < NumPoints; j++)
                                    {
                                        // cannot use 12.0f 
                                        testpts[j].x = (float)(position[j] * Math.Cos(j * radianIncrement + azwIncrement) + PrevCentroid.x);
                                        testpts[j].y = (float)(position[j] * Math.Sin(j * radianIncrement + azwIncrement) + PrevCentroid.y);
                                        testpts[j].z = (float)depthInDU ;

                                        //  rotatedPoints[j].z = testpts[j].z +1; // rotated point is the same. 
                                    }
                                        
                                    // rotate about the previous centroid
                                    //if ( (float)Math.Abs(depthInDU - PrevDepthInDU) / RenderScale > 0.0001f ) // only need to rotate the points if the depth distance is large.  
                                    generateTurn((float)(devPair[0].dataP[0]), (float)(dazPair[0].dataP[0]), v_dev, ref rotatedPoints, (float)Math.Abs(depthInDU - PrevDepthInDU), PreviousPoint, testpts);

                                }

                                depthRender = (float)depthInDU;
                                // need the depthStart to start the view at the right depth
                                if (depthCounter == 0)
                                {
                                    depthStart = (float)depthInDU ;
                                    depthAnchor = depthStart;
                                    depthCounter = 1;
                                }


                                // calvin_change 9 
                                tunnelPhysicalData.setVectorData(rotatedPoints);  // set the vector points . 
                                tunnelPhysicalData.setBoundingBox(ref XBoxCoord, ref YBoxCoord);

                                                                     
                                // update Previous positions to add on to the next centroid (relative)
                                PrevCentroid.x += Centroid.x;
                                PrevCentroid.y += Centroid.y;
                                PrevCentroid.z = Centroid.z;
                                PrevDepthInDU = (float) depthInDU;


                                // save the dogleg value 
                                double[] dgValue = new double[3];
                                dgValue[0] = computeDogLeg(devPair, dazPair);
                                dgValue[1] = devPair[0].dataP[0];
                                dgValue[2] = dazPair[0].dataP[0]; 
                                doglegResult.insertNew((float)depthInDU, dgValue);


                             

                                if (DepthIndex < maxDepth)
                                {
                                    DepthTextBox.Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.TextBox>(ShowText), DepthTextBox);
                                }
                                else
                                {
                                    System.Windows.MessageBox.Show("Too Many Points");
                                    progress.Close();
                                    this.IsEnabled = true;
                                    return;
                                }
                                DepthIndex++;

                                // if (DepthIndex == 10)
                                    //   break; 
                            }

                        }
                    }

                    progressValue = 98;
                    Dispatcher.Invoke(updatePbDelegate, System.Windows.Threading.DispatcherPriority.Background, new object[] { System.Windows.Controls.ProgressBar.ValueProperty, progressValue });
                }
            }

     //       objWriter.WriteLine("Finished loading..................");
     //       objWriter.Flush(); 



            devStorage.clearAll();
            dazStorage.clearAll();
            azwStorage.clearAll(); 


            UpdateVisibilityDogleg();
            //textBox1.Visibility = Visibility;


            progress.LoadingProgressBar.Value = 100;
            progress.Close();
            this.IsEnabled = true;

            System.Windows.MessageBox.Show("File loaded");
        }
         
        
                
        private void UpdateVisibilityDogleg()
        {

            DepthLabel.Visibility = Visibility.Visible;
            DepthTextBox.Visibility = Visibility.Visible;
            DgCheckBox.Visibility = Visibility.Visible;
            stackPanel3.Visibility = Visibility.Visible;
            stackPanel2.Visibility = Visibility.Visible;

        }

        // when clicked upon for the texture map properties. probably used to control how the colors are generated. 
        private void Color_Click(object sender, RoutedEventArgs e)
        {
            var secondWindow = new PropertyWindow();

            secondWindow.applyOkCancel += value => applyColorScheme();

            secondWindow.Closing += new CancelEventHandler(secondWindow_Closing);
            secondWindow.Show();
        }

        void secondWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var secondWindow = sender as PropertyWindow;

            //applyColorScheme(); // use this to call the scene to update the fragment shaders 
            
        }

        public void StreamButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window_Loaded(sender, e))
            {
                Pause.IsEnabled = true;
                StreamFlag = 1;

                // start drawing right away
                startdraw = 1;

                UpdateVisibilityDogleg();
            }
            else
            {
                RefreshCurves.IsEnabled = false;
                DgCheckBox.IsEnabled = false;
                Interval.IsEnabled = false;
                Color.IsEnabled = false;
                Print.IsEnabled = false;
                Export.IsEnabled = false;
                tboxAzw.IsEnabled = false;
                tboxCap.IsEnabled = false;
                tboxDaz.IsEnabled = false;
                tboxDev.IsEnabled = false;
                tboxText.IsEnabled = false;
                MgDec.IsEnabled = false;
                MgDecGoto.IsEnabled = false;
                Pause.IsEnabled = false;
            }
        }

        // dev and daz are all in radians
        // daz is not used at this point, maybe we need to rotate the points. v_dev already encapsulate the information provided by daz 
        private void generateTurn(float dev, float daz, vec3 v_dev, ref vec3[] rotatedPoints, float deltaDist, vec3 PreviousPoint, vec3[] testpts)
        {
            int a;
            vec3 rotatePoint;

            v_dev = glm.normalize(v_dev);
            rotatePoint = glm.cross(new vec3(0, 0, -1), v_dev);
            if ( (rotatePoint.x == 0.0f) && (rotatePoint.y == 0.0f) || (rotatePoint.z == 0.0f))  // not defined 
                rotatePoint = new vec3(0, 1, 0); 
            rotatePoint = glm.normalize(rotatePoint);  // this is the turning vector 
            
            mat4 rotationMatrix = glm.rotate(dev, rotatePoint); // this is the rotationMatrix;
            float shiftDistance = deltaDist / (float)Math.Cos(dev) - deltaDist; 
            vec3 shiftVector = v_dev * shiftDistance;
              
          
            for (a = 0; a < testpts.Count(); a++)
            {
                rotatedPoints[a] = testpts[a];
                rotatedPoints[a] -= PreviousPoint; 
                          
                rotatedPoints[a] = new vec3(rotationMatrix * new vec4(rotatedPoints[a], 1)); // applying the transform
                rotatedPoints[a] += PreviousPoint; 
                
                rotatedPoints[a] += shiftVector;
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.PrintDialog dialog = new System.Windows.Controls.PrintDialog();
            System.Printing.PrintQueue printQ = dialog.PrintQueue;  // to force initialization of the dialog's PrintTicket or PrintQueue

            if (dialog.ShowDialog() != true) return;

            double printableWidth = (double)dialog.PrintQueue.GetPrintCapabilities().PageImageableArea.ExtentWidth, //dialog.PrintTicket.PageMediaSize.Width.Value, //printQ.DefaultPrintTicket.PageMediaSize.Width.Value, //dialog.PrintableAreaWidth, 
                   printableHeight = (double)dialog.PrintQueue.GetPrintCapabilities().PageImageableArea.ExtentHeight; //dialog.PrintTicket.PageMediaSize.Height.Value; //printQ.DefaultPrintTicket.PageMediaSize.Height.Value; //dialog.PrintableAreaHeight; //(double)dialog.PrintTicket.PageMediaSize.Height; 

            //System.Windows.MessageBox.Show("printableWidth=" + printableWidth + ", printableHeight=" + printableHeight + 
            //    ", openGLControl.ActualWidth=" + openGLControl.ActualWidth + ", openGLControl.ActualHeight=" + openGLControl.ActualHeight + 
            //    ", this.ActualWidth=" + this.ActualWidth + ", this.ActualHeight=" + this.ActualHeight);

            double scalingFactor, offsetFactor = 0.8;
            if (openGLControl.ActualWidth > openGLControl.ActualHeight)
            {
                dialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
                scalingFactor = Math.Min(printableWidth / openGLControl.ActualHeight, printableHeight / openGLControl.ActualWidth);
                //scalingFactor = Math.Min(printableWidth / (this.ActualHeight*offsetFactor), printableHeight / (this.ActualWidth*offsetFactor));
            }
            else
            {
                dialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Portrait;
                scalingFactor = Math.Min(printableWidth / openGLControl.ActualWidth, printableHeight / openGLControl.ActualHeight);
                //scalingFactor = Math.Min(printableWidth / (this.ActualWidth*offsetFactor), printableHeight / (this.ActualHeight*offsetFactor)); 
            }
            //System.Windows.MessageBox.Show("scalingFactor=" + scalingFactor);
            dialog.PrintTicket.PageScalingFactor = (int?)(100 * scalingFactor * offsetFactor); // PageScalingFactor is in % - this only affect pdf printer, not effective on actual physical printer!

            // Get original Transform and Size
            Transform OriginalTrans = this.openGLControl.LayoutTransform.Clone();
            Size OldSize = new Size(this.openGLControl.ActualWidth, this.openGLControl.ActualHeight);

            //Transform the Visual to scale
            this.openGLControl.LayoutTransform = new ScaleTransform(scalingFactor, scalingFactor);

            //scale openGLControl to the printing page size
            Size sz = new Size(this.openGLControl.ActualWidth * scalingFactor, this.openGLControl.ActualHeight * scalingFactor);

            //update the layout of the visual to the printing page size.
            this.openGLControl.Measure(sz);
            this.openGLControl.Arrange(new Rect(new Point(0, 0), sz));

            dialog.PrintVisual(this.openGLControl, "Tunnel Visualisation Printout");

            // Restore everything back
            this.openGLControl.LayoutTransform = OriginalTrans;
            this.openGLControl.Measure(OldSize);
            this.openGLControl.Arrange(new Rect(new Point(0, 0), OldSize));

        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "TunnelVisualisation"; // Default file name
            saveFileDialog.DefaultExt = ".jpg"; // Default file extension
            saveFileDialog.Filter = "Image file|*.jpg"; // Filter files by extension

            if (saveFileDialog.ShowDialog() == true)
            {
                using (FileStream filestream = new FileStream(saveFileDialog.FileName.ToString(), FileMode.Create))
                {
                    //BitmapImage objImage = new BitmapImage(new Uri(saveFileDialog.FileName.ToString(), UriKind.RelativeOrAbsolute));

                    double offsetFactor = 1.2;
                    RenderTargetBitmap bitmap =
                            new RenderTargetBitmap((int)(this.openGLControl.ActualWidth * offsetFactor),
                             (int)(this.openGLControl.ActualHeight * offsetFactor),
                              96, 96, PixelFormats.Pbgra32);




                    bitmap.Render(this.openGLControl);

                    /*   new RenderTargetBitmap((int)gl.RenderContextProvider.Width,
                        (int)gl.RenderContextProvider.Height,
                         96, 96, PixelFormats.Pbgra32);
                       bitmap.Render(this.openGLControl);
                       */

                    /*JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = 90;
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(filestream);
                    */
                    // If you want to export the xamDataChart to a PNG 
                    // instead of JPEG, use this code block:
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(filestream);
                }
            }
        }

        private void UpdateColorProperties()
        {
            var secondWindow = new PropertyWindow();
            //string selection = secondWindow.ChoiceLinearLog.ToString();
        }

        private void DgCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DgstackPanel.Visibility = Visibility.Visible;
            DgstackPanel1.Visibility = Visibility.Visible;
            UnitSetting.Visibility = Visibility.Visible;
            UnitSet.Visibility = Visibility.Visible;
            DgBatchstackPanel.Visibility = Visibility.Visible;
            DgBatchstackPanel1.Visibility = Visibility.Visible;
            rectangle1.Visibility = Visibility.Visible;
            BDogleg.Visibility = Visibility.Visible;
        }

        private void DgCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DgstackPanel.Visibility = Visibility.Hidden;
            DgstackPanel1.Visibility = Visibility.Hidden;
            UnitSetting.Visibility = Visibility.Hidden;
            UnitSet.Visibility = Visibility.Hidden;
            DgBatchstackPanel.Visibility = Visibility.Hidden;
            DgBatchstackPanel1.Visibility = Visibility.Hidden;
            rectangle1.Visibility = Visibility.Hidden;
            BDogleg.Visibility = Visibility.Hidden;
        }

        private void UnitSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        public List<string> GetCurveNamesFromDataFile(IDataFile dataFile, bool getOneDimensionCurves)
        {
            List<string> m_CurveNamesFromDataFile = new List<string>();

            if (dataFile != null)
            {
                IUdfStorage storage = dataFile.GetFirstStorage();

                if (storage != null)
                {
                    foreach (IFrameDataObject frameDataObject in storage.FrameDataObjects)
                    {
                        if (frameDataObject.Channels != null && frameDataObject.Channels[0] != null)
                        {
                            int number = frameDataObject.Channels[0].SampleElements;
                            string name = frameDataObject.Name;

                            if (getOneDimensionCurves)
                                if (number == 1)
                                    m_CurveNamesFromDataFile.Add(name);
                                else
                                    m_CurveNamesFromDataFile.Add(name);
                        }

                    }
                }
            }

            return m_CurveNamesFromDataFile;
        }

        private void BDogleg_Click(object sender, RoutedEventArgs e)
        {

            if (textBox1.Text == "" || textBox2.Text == "")
                return;

            double upper = double.Parse(textBox2.Text);
            double lower = double.Parse(textBox1.Text);
            double firstDepth = Math.Abs(doglegResult.getFirstDepth()), lastDepth = Math.Abs(doglegResult.getLastDepth());
            double upperLimit = firstDepth < lastDepth ? lastDepth : firstDepth, lowerLimit = firstDepth < lastDepth ? firstDepth : lastDepth;
            int lowerIndex, upperIndex;


            if (lower < lowerLimit || upper > upperLimit || lower >= upper)
            {
                System.Windows.MessageBox.Show("Invalid lower/upper depth inputs or upper depth value is equal to or lower than the lower depth value! Valid range of depth: [" + lowerLimit + ", " + upperLimit + "]");
                return;
            }

            string outputFileName = "";
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                outputFileName = saveFileDialog.FileName;
            }
            else
                return;

            StreamWriter outputWriter = new System.IO.StreamWriter(outputFileName); // used as a means to write debugging data on the screen 

            //doglegWriter.WriteLine("DogLeg Computation Result");
            //doglegWriter.WriteLine("Depth               DEV             DAZ         Dogleg /25m         Dogleg /30m         Dogleg /100m");
            outputWriter.WriteLine("DogLeg Computation Result");
			outputWriter.WriteLine(String.Format("{0,20} {1,16} {2,16} {3,16} {4,16} {5,16}", "Depth", "DEV", "DAZ", "Dogleg /25m", "Dogleg /30m", "Dogleg /100m"));

            if (lower < lowerLimit) // lower than permitted 
            {
                if (upper < lowerLimit)
                {
                    //doglegWriter.WriteLine("Out of Range!");
                    //doglegWriter.Flush();
                    outputWriter.WriteLine("Out of Range!");
                    outputWriter.Flush();
                    return; //exit 
                }
                else
                {
                    lower = lowerLimit;
                    lowerIndex = 0;
                }
            }
            else
				lowerIndex = doglegResult.getIndex(m_eLoggingMode == LoggingMode.Time ? lower : -lower);



			if (upper > upperLimit)
			{
				if (lower > upperLimit)
				{
					//doglegWriter.WriteLine("Out of Range!");
					//doglegWriter.Flush();
					outputWriter.WriteLine("Out of Range!");
					outputWriter.Flush();
					return; //exit 
				}
				else
				{
					upper = upperLimit;
					upperIndex = doglegResult.getCount();
				}
			}
			else
				upperIndex = doglegResult.getIndex(m_eLoggingMode == LoggingMode.Time ? upper : -upper);
                //upperIndex = doglegResult.getIndex(upper) + 1; // why +1?


            // now we have valid upper and lower dogleg values 
            double b = lower;
            int a = lowerIndex;

            while (b < upper)
            {
				while (Math.Abs(doglegResult.dataList[a].depth) < b)
				{
					//a++;
					if (m_eLoggingMode == LoggingMode.Depth && m_eLoggingDir == LoggingDir.Up)
						a--;
					else
						a++;
				}

                double baseDogleg = doglegResult.dataList[a].dataP[0] * 25.0f;
                double baseDogleg1 = doglegResult.dataList[a].dataP[0] * 30.0f;
                double baseDogleg2 = doglegResult.dataList[a].dataP[0] * 100.0f;


				//Depth               DEV             DAZ         Dogleg /25m         Dogleg /30m         Dogleg /100m
				//============
                //doglegWriter.WriteLine(doglegResult.dataList[a].depth.ToString("F4") + "        " + doglegResult.dataList[a].dataP[1].ToString("F4") + "         " + doglegResult.dataList[a].dataP[2].ToString("F4") + "        "
                //    + baseDogleg.ToString("F4") + "          " + baseDogleg1.ToString("F4") + "       " + baseDogleg2.ToString("F4"));
				outputWriter.WriteLine(String.Format("{0,20} {1,16} {2,16} {3,16} {4,16} {5,16}", 
														Math.Abs(doglegResult.dataList[a].depth).ToString("F4"), 
														doglegResult.dataList[a].dataP[1].ToString("F4"), 
														doglegResult.dataList[a].dataP[2].ToString("F4"), 
														baseDogleg.ToString("F4"), 
														baseDogleg1.ToString("F4"),
														baseDogleg2.ToString("F4")));


                b += 25.0f;

            }


            System.Windows.MessageBox.Show("Dogleg results written to file!");
            //doglegWriter.Flush(); 
            outputWriter.Flush();
            outputWriter.Close();
        }

        private void MgDec_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            double mgvRadian = double.Parse(MgDec.Text) * PI/ 180.0f;

           // mgTransformMatrix = glm.rotate(modelTransformMatrix, (float) ( mgvRadian - mgDecValue), new vec3(0.0f, 0.0f, 1.0f));


            mgDecValue = (float) mgvRadian ; 

            //objWriter.WriteLine("lost focus = " + mgvRadian);
            //objWriter.Flush(); 
        }




        private void refreshCurve()
        {
            switchToTimeMode(m_eLoggingMode == LoggingMode.Time ? true : false);

            if (StreamFlag == 1)
            {
                m_bUpdateVisualThreadTerminat = true;
            }

            curveBHIOPData.clearAll();
            tunnelPhysicalDataHolder.clearAll();
            tunnelPhysicalData.clearAll();
            dazStorage.clearAll();
            devStorage.clearAll();
            azwStorage.clearAll();
            doglegResult.clearAll();


            scene.close();
            picking.close();


            // some resetting
            XBoxCoord = 100;
            YBoxCoord = 100;
            DepthIndex = 0;
            renderIndex = 0;
            PrevDepthInDU = 0;
            FirstDepthInput = 0;
            PrevCentroid = new vec3(0, 0, 0);
            depthStart = depthRender = depthAnchor = 0.0f;
            modelTransformMatrix = mat4.identity();
            compassRotationMatrix = mat4.identity();
            

            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;
            //  Initialise the scene.
            scene.Initialize(gl);
            // set the picking framebuffer
            picking.Initialize(gl);
            picking.generateBuffers(openGLControl.OpenGL, (int)openGLControl.ActualWidth, (int)openGLControl.ActualHeight);

            //  Set the clear color.
            gl.ClearColor(0, 0, 0, 0);

            // get the new curvenames from the comboxes 
            assignStringComboBox(ref tboxDev, ref cDEV);
            assignStringComboBox(ref tboxDaz, ref cDAZ);
            assignStringComboBox(ref tboxAzw, ref cAZW);
            assignStringComboBox(ref tboxText, ref cBHIOP);
            assignStringComboBox(ref tboxCap, ref cSPRD);

            if (StreamFlag == 0) // this only works if this is a load from file originally 
            {
                LoadFileDetails(recentOpenFile, false); // use the combo selection to select the appropriate curves 
            }
            else
            {

                depthCounter = 0;


                System.Windows.MessageBox.Show("Streaming starts!");

                OnStart();
            }
        }


        private void RefreshCurves_Click(object sender, RoutedEventArgs e)
        {
            refreshCurve();
        }


        void assignStringComboBox(ref System.Windows.Controls.ComboBox cboxUI, ref string cstring)
        {
            if (cboxUI.SelectedItem == null)
                cstring = null;
            else
                cstring = cboxUI.SelectedItem.ToString();
        }




        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var firstWindow = new Window1();

            objWriter.Close();
            doglegWriter.Close();
            
            scene.close();
            picking.close(); 

            this.Close();
            firstWindow.Show();
        }

        private void MgDecGoto_Click(object sender, RoutedEventArgs e)
        {
            double mgvRadian = double.Parse(MgDec.Text) * PI / 180.0f;

            mgDecValue = (float)mgvRadian;
        }

        private void DgDepth_LostFocus(object sender, RoutedEventArgs e)
        {
            getDogleg();
        }

        private void DgDepth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Keyboard.FocusedElement != sender)
                getDogleg();
        }

        // if the flag is true, switch the UI text to time mode, else set to depth mode
        private void switchToTimeMode(bool switchFromDepthToTimeFlag)
        {
            if (switchFromDepthToTimeFlag)
            {
                Interval.ToolTip = "Integer input of time interval of range 1-1000";
                Interval.Text = timeInterval.ToString();
                DepthLabel.Content = "Current Time :";
                DgDepthLabel.Content = "Dogleg Time :";
                label3.Content = "Lowerbound Time :";
                label4.Content = "Upperbound Time :";
                LoggingInfo.Content = "Mode : Time";
            }
            else
            {
                Interval.ToolTip = "Integer input of depth interval of range 1-1000";
                Interval.Text = depthInterval.ToString();
                DepthLabel.Content = "Current Depth :";
                DgDepthLabel.Content = "Dogleg Depth :";
                label3.Content = "Lower Depth :";
                label4.Content = "Upper Depth :";
                if (m_eLoggingDir == LoggingDir.Up)
                    LoggingInfo.Content = "Mode : Depth, direction : Up";
                else if (m_eLoggingDir == LoggingDir.Down)
                    LoggingInfo.Content = "Mode : Depth, direction : Down";
                else
                    LoggingInfo.Content = "Mode : Depth";
            }
        }

        private void handlePauseResume()
        {
            isPaused = !isPaused;   // flip the flag between "Pause" and "Resume"

            if (isPaused)   // to pause
            {
                RefreshCurves.IsEnabled = false;
                Pause.Content = "Resume";
            }
            else // to resume
            {
                RefreshCurves.IsEnabled = true;
                Pause.Content = "Pause";
            }
        }

		private void clearDgInputs()
		{
			DgDepth.Text = "";
			DogLegAns.Text = "";
			MgDec.Text = "";
			textBox1.Text = "";
			textBox2.Text = "";
		}

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            handlePauseResume();
        }

        private void Interval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (m_eLoggingMode == LoggingMode.Time)
                timeInterval = int.Parse(Interval.Text);
            else
                depthInterval = int.Parse(Interval.Text);
        }

        private void Interval_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Interval_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tbox = sender as System.Windows.Controls.TextBox;
            if (tbox.Text == "" || double.Parse(tbox.Text) < 1 || double.Parse(tbox.Text) > 1000)
            {
                e.Handled = true;
            }
        }

        private bool testPointOutsideScreen(OpenGL gl, vec3 testPoint)
        {
            bool isOutside = false;

            // project testPoint to the screen
            vec4 worldpos = new vec4(testPoint, 1);
            vec4 projectedPos = new vec4();
            projectedPos = fullT * worldpos;
            vec3 screenPos = new vec3(projectedPos.x / projectedPos.w, projectedPos.y / projectedPos.w, projectedPos.z / projectedPos.w);

            if ((screenPos.y > 1.0f) || (screenPos.y < -1.0f))
            {
                isOutside = true;
            }
            return isOutside;
        }

		private void MgDecGoto_Click(object sender, KeyboardFocusChangedEventArgs e)
		{
			MgDecGoto_Click(sender, (RoutedEventArgs) e);
		}

    }
  
}
