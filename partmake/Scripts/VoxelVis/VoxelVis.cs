using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;
using partmake.graphics;

namespace partmake.script
{
    public class VoxelVis
    {
	    private TextureView _terrainTextureView;
	    private Sampler _terrainSampler;
	    private DeviceBuffer _cubeVertexBuffer;
	    private DeviceBuffer _cubeIndexBuffer;
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
        private DeviceBuffer _instanceVB;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;

	    private uint _cubeIndexCount = 0;
	    const uint dimcnt = 1024;
	    uint _instanceCnt = dimcnt*dimcnt;
	    
    	public void Gen(TextureView terrainTextureView)    
    	{
            _terrainTextureView = terrainTextureView;

            _terrainSampler = G.ResourceFactory.CreateSampler(new SamplerDescription(SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerFilter.MinPoint_MagPoint_MipPoint,
                ComparisonKind.Always, 0, 0, 0, 0, SamplerBorderColor.OpaqueBlack));

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
    	
    	public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
    	{
    		if (_cubeIndexCount == 0)
    			return;
    	 	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(G.Swapchain.Framebuffer);
            cl.SetGraphicsResourceSet(0, _projViewSet);
            cl.SetGraphicsResourceSet(1, _worldTextureSet);
    	 	
            Matrix4x4 mat =
                Matrix4x4.CreateScale(0.01f) *
                Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));

            cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);
            cl.UpdateBuffer(_viewBuffer, 0, viewmat);
            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
            cl.UpdateBuffer(_materialBuffer, 0, ref color);
            cl.SetVertexBuffer(0, _cubeVertexBuffer);
            cl.SetVertexBuffer(1, _instanceVB);
            cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_cubeIndexCount,
            	_instanceCnt, 0,0,0);            
    	}
	    Vector2 []GetInstanceBuf()
	    {
	    	Vector2[] vtxs = new Vector2[dimcnt *dimcnt];
	    	int xoff = 0;
	    	int yoff = 0;
	    	float scale = 1.0f;
	    	for (int y = 0; y < dimcnt; ++y)
	    	{
		    	for (int x = 0; x < dimcnt; ++x)
		    	{
		    		vtxs[y * dimcnt + x] = new Vector2(
			    		(float)(x + xoff)/ (dimcnt * scale - 1),
			    		(float)(y + yoff)/ (dimcnt * scale - 1));
		    	}
	    	}
	    	return vtxs;
	    }
    }      
}    