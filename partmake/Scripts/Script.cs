using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using partmake.graphics;

namespace partmake.script
{
    public class Script
    {
        private DeviceBuffer _planeVertexBuffer;
        private DeviceBuffer _planeIndexBuffer;
        uint _planeIndexCount;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
        Vector3 wposMouse;

        public void Run()
        {                    	
    		Api.MouseHandler = MouseHandler;
    		Api.CustomDraw = Draw;
        }
        
		public void Gen()    
    	{
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;
        
			ShaderSet ss = new ShaderSet(
                    partmake.graphics.Shader.VSDefault,
                    partmake.graphics.Shader.FSDefault, true);
    		
            
			_projectionBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _materialBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));

            
			ResourceLayout projViewLayout = G.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = G.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("MeshColor", ResourceKind.UniformBuffer, ShaderStages.Fragment)));            
                    
            _projViewSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer));
                                
            _pipeline = G.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                ss.Desc,
                new[] { projViewLayout, worldTextureLayout },
                G.Swapchain.Framebuffer.OutputDescription));            
    	}
    	        
    	List<Vector3> mousePts = new List<Vector3>();
        void MouseHandler(LayoutVis.MouseCommand command, int btn, Vector2 screenPos, 
            Vector3 w0, Vector3 w1, ref Matrix4x4 viewProj)
        {
        	if (command == LayoutVis.MouseCommand.ButtonDown)
        	{
        		mousePts.Add(w0);
		    }
    		wposMouse = w0;
        }
        
        public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
        	if (_pipeline == null)
        		Gen();
        		
        	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(G.Swapchain.Framebuffer);
            cl.SetGraphicsResourceSet(0, _projViewSet);
            cl.SetGraphicsResourceSet(1, _worldTextureSet);
    	 	
            cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);
            cl.UpdateBuffer(_viewBuffer, 0, viewmat);
            
            foreach (Vector3 pt in mousePts)
            {
			Matrix4x4 mat =
                Matrix4x4.CreateScale(5f) *
                Matrix4x4.CreateTranslation(pt) *
                Matrix4x4.CreateScale(G.WorldScale);

            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 color = new Vector4(0.5f, 0.5f, 1.0f, 1);
            cl.UpdateBuffer(_materialBuffer, 0, ref color);
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount,
            	1, 0,0,0);            
            }
			{
			Matrix4x4 mat =
                Matrix4x4.CreateScale(2f) *
                Matrix4x4.CreateTranslation(wposMouse) *
                Matrix4x4.CreateScale(G.WorldScale);

            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
            cl.UpdateBuffer(_materialBuffer, 0, ref color);
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount,
            	1, 0,0,0);    
        	}
		}
    }
}