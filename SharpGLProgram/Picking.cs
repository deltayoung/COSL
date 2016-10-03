using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlmNet;
using System.Runtime.InteropServices;
using GLuint = System.UInt32;
using SharpGL;
using SharpGL.Enumerations;
using SharpGL.Shaders;


namespace SharpGLProgram
{
    // this class implements the picking shaders that we use to insert details about the triangles that we draw, so that we are able to retrieve information from the framebuffer
    class Picking
    {

        OpenGL glContainer; 

        public void Initialize(OpenGL gl)
        {
            glContainer = gl; 
            //  We're going to specify the attribute locations for the position and normal, 
            //  so that we can force both shaders to explicitly have the same locations.
            const uint positionAttribute = 0;
            const uint normalAttribute = 1;
            const uint colorAttribute = 2;
            const uint textureAttribute = 3;
            const uint sliceAttribute = 4;

            var attributeLocations = new Dictionary<uint, string>
            {
                {positionAttribute, "position"},
                {normalAttribute, "normal"},
                {colorAttribute, "color"},
                {textureAttribute, "texture"},
                {sliceAttribute, "slice"}
            };

                      
            // Create the per pixel shader.
            // if you ever wish to create a new vert or frag file, remember to set the build properties to "embedded source" 
            shaderID = new ShaderProgram();
            shaderID.Create(gl,
                ManifestResourceLoader.LoadTextFile(@"Shaders\pickingVS.vert"),
                ManifestResourceLoader.LoadTextFile(@"Shaders\pickingFS.frag"), attributeLocations);
      
        }

        // setting the transformation matrix 
        public void applyTransformation(OpenGL gl, mat4 fullTransform)
        {
             shaderID.SetUniformMatrix4(gl, "fullTransformaMatrix", fullTransform.to_array());
        }

        public void close()
        {
            shaderID.Delete(glContainer); 
        }

        
        // used when the window resizes, so we  have to resize the texture too. 
        public bool resizeBuffers(OpenGL gl, int width, int height)
        {
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);

            gl.Viewport(0, 0, width, height); 

           
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, colourRenderBufferID);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB32F, width, height, 0, OpenGL.GL_RGBA, OpenGL.GL_FLOAT, null);
            gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, colourRenderBufferID, 0);


            gl.BindTexture(OpenGL.GL_TEXTURE_2D, depthRenderBufferID);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_DEPTH_COMPONENT, width, height, 0, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, null);
            gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_DEPTH_ATTACHMENT_EXT, OpenGL.GL_TEXTURE_2D, depthRenderBufferID, 0);



            gl.ReadBuffer(OpenGL.GL_COLOR_ATTACHMENT0_EXT); 
            gl.DrawBuffer(OpenGL.GL_COLOR_ATTACHMENT0_EXT); 
          
            // reset the binding to normal 
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);

            // checking the status of the framebuffer creation 
            GLuint status = gl.CheckFramebufferStatusEXT(OpenGL.GL_FRAMEBUFFER_EXT);
            if (status != OpenGL.GL_FRAMEBUFFER_COMPLETE_EXT)
                return false;

            return true; 
        }
        
        // this function generates the framebuffers to store a color buffer as a texture
        public bool generateBuffers(OpenGL gl, int width, int height)
        {
            uint[] ids = new uint[1];
            gl.GenFramebuffersEXT(1, ids);
            frameBufferID = ids[0];
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
            
            gl.GenTextures(1, ids);
            colourRenderBufferID = ids[0];
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, colourRenderBufferID);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB32F, width, height, 0, OpenGL.GL_RGBA, OpenGL.GL_FLOAT, null);
            gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, colourRenderBufferID,  0);


            gl.GenTextures(1, ids);
            depthRenderBufferID = ids[0];
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, depthRenderBufferID);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_DEPTH_COMPONENT, width, height, 0, OpenGL.GL_DEPTH_COMPONENT , OpenGL.GL_FLOAT, null);
            gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_DEPTH_ATTACHMENT_EXT, OpenGL.GL_TEXTURE_2D, depthRenderBufferID, 0);

          

            gl.ReadBuffer(OpenGL.GL_COLOR_ATTACHMENT0_EXT); 
            gl.DrawBuffer(OpenGL.GL_COLOR_ATTACHMENT0_EXT); 
                   
            // checking the status of the framebuffer creation 
            GLuint status =  gl.CheckFramebufferStatusEXT(OpenGL.GL_FRAMEBUFFER_EXT);
            if (status != OpenGL.GL_FRAMEBUFFER_COMPLETE_EXT)
                return false;
            
            // reset the binding to normal 
            gl.BindRenderbufferEXT(OpenGL.GL_RENDERBUFFER_EXT, 0); 
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0); 
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0); 

            return true; 
        }


        public float pickPixel(OpenGL gl, int x, int y, ref float actualDepth)
        {
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
            gl.ReadBuffer(OpenGL.GL_COLOR_ATTACHMENT0_EXT);


            // reading RGBA, each value is 32 bits, so total u need 16 bytes to store all 
            byte[] pixelInfo = new byte[16];
            gl.ReadPixels(x, y, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_FLOAT, pixelInfo);

            // 4 bytes = 1 32-bit float value. 
            float myFloat1 = System.BitConverter.ToSingle(pixelInfo, 0); 
            float myFloat2 = System.BitConverter.ToSingle(pixelInfo, 4);
            float myFloat3 = System.BitConverter.ToSingle(pixelInfo, 8);
            float myFloat4 = System.BitConverter.ToSingle(pixelInfo, 12);
            
            gl.ReadBuffer(OpenGL.GL_NONE);
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);


            actualDepth = myFloat3; 

            // can be changed to get any value from the 4 float values. 
            return myFloat1; 
        }

        
        // this is shader + framebuffer binding 
        public void binding(OpenGL gl)
        {
            shaderID.Bind(gl);
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, frameBufferID);
           
        }

        // this is shader + framebuffer unbinding 
        public void unbinding(OpenGL gl)
        {
            shaderID.Unbind(gl);
            gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0); 
        }


        //  The shaders we use.
        private ShaderProgram shaderID;

        private uint frameBufferID; // for the framebuffer 
        private uint colourRenderBufferID; // for the color texture 2D 
        private uint depthRenderBufferID; 
    }
}
