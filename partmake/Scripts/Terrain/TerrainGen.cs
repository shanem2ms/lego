﻿using System;
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
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
	    private TextureView _terrainTextureView;
	    private Framebuffer _terrainFB;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;

	    private DeviceBuffer _planeVertexBuffer;
	    private DeviceBuffer _planeIndexBuffer;
	    private uint _planeIndexCount;
	    public Texture TerrainTex => _terrainTexture;
	    public TextureView TerrainTexView => _terrainTextureView;

    	public void Gen()    
    	{
            _terrainTexture = Api.ResourceFactory.CreateTexture(
                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_Float, 
                	TextureUsage.RenderTarget | TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
            _terrainTextureView = Api.ResourceFactory.CreateTextureView(_terrainTexture);

			byte []pixShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\TerrainGen.glsl"));    		
    		byte []vtxShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\VtxBlit.glsl"));    		
    		
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
    		
            var planeVertices = primitives.Plane.GetVertices();
            _planeVertexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * planeVertices.Length), BufferUsage.VertexBuffer));
            Api.GraphicsDevice.UpdateBuffer(_planeVertexBuffer, 0, planeVertices);

            var planeIndices = primitives.Plane.GetIndices();
            _planeIndexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)planeIndices.Length, BufferUsage.IndexBuffer));
            _planeIndexCount = (uint)planeIndices.Length;
            Api.GraphicsDevice.UpdateBuffer(_planeIndexBuffer, 0, planeIndices);

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
                            
             Texture offscreenDepth = Api.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                    1024, 1024, 1, 1, PixelFormat.R16_UNorm, TextureUsage.DepthStencil));
             _terrainFB = Api.ResourceFactory.CreateFramebuffer(new FramebufferDescription(offscreenDepth, _terrainTexture));

                                
            _pipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
                _terrainFB.OutputDescription));            
    	}
    	
    	public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
    	{
    	 	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(_terrainFB);
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
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount);            
    	}
    }
}    