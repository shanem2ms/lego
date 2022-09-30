using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;

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

        public void Run()
        {                    	
    		Api.MouseHandler = MouseHandler;
    		Api.CustomDraw = Draw;
        }
        
public void Gen(TextureView terrainTextureView)    
    	{
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;
        
			byte []pixShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "VoxelVis\\VoxVisFrag.glsl"));    		
    		byte []vtxShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "VoxelVis\\VoxVisVtx.glsl"));    		
			var vtxlayout = new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
			var vertexLayoutPerInstance = new VertexLayoutDescription(
                new VertexElementDescription("InstanceCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
                vertexLayoutPerInstance.InstanceStepRate = 1;
            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                	vtxlayout,
                	vertexLayoutPerInstance
                },
                G.ResourceFactory.CreateFromSpirv(
	                new ShaderDescription(ShaderStages.Vertex, vtxShaderBytes, "main"),
	                new ShaderDescription(ShaderStages.Fragment, pixShaderBytes, "main")));
    		
            vertexLayoutPerInstance.InstanceStepRate = 1;
            
            _instanceVB = G.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(float)*2*_instanceCnt, BufferUsage.VertexBuffer));
            G.GraphicsDevice.UpdateBuffer(_instanceVB, 0, GetInstanceBuf());
            
            var cubeVertices = primitives.Plane.GetVertices();
            _cubeVertexBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * cubeVertices.Length), BufferUsage.VertexBuffer));
            G.GraphicsDevice.UpdateBuffer(_cubeVertexBuffer, 0, cubeVertices);

            var cubeIndices = primitives.Plane.GetIndices();
            _cubeIndexBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)cubeIndices.Length, BufferUsage.IndexBuffer));
            _cubeIndexCount = (uint)cubeIndices.Length;
            G.GraphicsDevice.UpdateBuffer(_cubeIndexBuffer, 0, cubeIndices);

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
                    new ResourceLayoutElementDescription("MeshColor", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("VoxTexture", ResourceKind.TextureReadOnly, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("VoxSampler", ResourceKind.Sampler, ShaderStages.Vertex)));            
                    
            _projViewSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer,
                _terrainTextureView,
                _terrainSampler));
                                
            _pipeline = G.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
                G.Swapchain.Framebuffer.OutputDescription));            
    	}
    	        
        void MouseHandler(LayoutVis.MouseCommand command, int btn, int X, int Y, Vector3 worldPos)
        {
        	if (command == LayoutVis.MouseCommand.ButtonDown)
        		Api.WriteLine($"{worldPos}");
        }
        
        public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
		}
    }
}