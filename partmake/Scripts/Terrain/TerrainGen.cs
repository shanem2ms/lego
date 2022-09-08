using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;

namespace partmake.script
{
    public class TerrainGen
    {
	    private Texture _terrainTexture;
	    private DeviceBuffer _cubeVertexBuffer;
	    private DeviceBuffer _cubeIndexBuffer;
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
	    private TextureView _terrainTextureView;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;

	    private uint _cubeIndexCount;
    	public void Gen()    
    	{
            _terrainTexture = Api.ResourceFactory.CreateTexture(
                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_Float, 
                	TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
            _terrainTextureView = Api.ResourceFactory.CreateTextureView(_terrainTexture);
    		_terrainTexture.Dispose();

			byte []pixShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\TerrainGen.glsl"));    		
    		byte []vtxShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "vtxblit.glsl"));    		
    		
            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                Api.ResourceFactory.CreateFromSpirv(
	                new ShaderDescription(ShaderStages.Vertex, vtxShaderBytes, "main"),
	                new ShaderDescription(ShaderStages.Fragment, pixShaderBytes, "main")));
    		
            var cubeVertices = primitives.Cube.GetVertices();
            _cubeVertexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * cubeVertices.Length), BufferUsage.VertexBuffer));
            Api.GraphicsDevice.UpdateBuffer(_cubeVertexBuffer, 0, cubeVertices);

            var cubeIndices = primitives.Cube.GetIndices();
            _cubeIndexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)cubeIndices.Length, BufferUsage.IndexBuffer));
            _cubeIndexCount = (uint)cubeIndices.Length;
            Api.GraphicsDevice.UpdateBuffer(_cubeIndexBuffer, 0, cubeIndices);

			_projectionBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _materialBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));

            
			ResourceLayout projViewLayout = Api.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = Api.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("MeshColor", ResourceKind.UniformBuffer, ShaderStages.Fragment)));            
                    
            _projViewSet = Api.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = Api.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer));
                                
            _pipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
                Api.Swapchain.Framebuffer.OutputDescription));            
    	}
    	
    	public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
    	{
    	 	cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _projViewSet);
            cl.SetGraphicsResourceSet(1, _worldTextureSet);
    	 	
            Matrix4x4 mat =
                Matrix4x4.CreateScale(0.1f) *
                Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));

            cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);
            cl.UpdateBuffer(_viewBuffer, 0, viewmat);
            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
            cl.UpdateBuffer(_materialBuffer, 0, ref color);
            cl.SetVertexBuffer(0, _cubeVertexBuffer);
            cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_cubeIndexCount);            
    	}
    }
}    