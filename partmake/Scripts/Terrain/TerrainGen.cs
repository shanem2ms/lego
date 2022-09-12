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
	    private Framebuffer _terrainFB;
	    
	    private Framebuffer []_erosionFB = new Framebuffer[2];
	    private Texture []_erosionTex = new Texture[2];
	    private TextureView []_erosionTextureView = new TextureView[2];
	    private ResourceSet []_erosionResourceSet = new ResourceSet[2];
	    private Pipeline _erosionPipeline;
	    
        private Pipeline _pipeline;

	    private DeviceBuffer _planeVertexBuffer;
	    private DeviceBuffer _planeIndexBuffer;
	    private uint _planeIndexCount;
	    public TextureView TerrainTexView => _erosionTextureView[0];

    	public void Gen()    
    	{
			byte []pixShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\TerrainGen.glsl"));    		
    		byte []vtxShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\VtxBlit.glsl"));    		
    		byte []eroShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "Terrain\\Erosion.glsl"));    		
    		
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
                                                                                      
			ShaderSetDescription eroShaderSet = new ShaderSetDescription(
                new[]
                {
                new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                Api.ResourceFactory.CreateFromSpirv(
	                new ShaderDescription(ShaderStages.Vertex, vtxShaderBytes, "main"),
	                new ShaderDescription(ShaderStages.Fragment, eroShaderBytes, "main")));

			        
			ResourceLayout textureLayout = Api.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(                    
                    new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)));     
            for (int i = 0; i < 2; ++i)
			{
				_erosionTex[i] = Api.ResourceFactory.CreateTexture(
	                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_G32_B32_A32_Float, 
                		TextureUsage.RenderTarget | TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
         		_erosionTextureView[i] = 
         			Api.ResourceFactory.CreateTextureView(_erosionTex[i]);
	    		_erosionResourceSet[i] = Api.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
	    			textureLayout,
	    			_erosionTextureView[i]));
    			_erosionFB[1 - i] = Api.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, _erosionTex[i]));
			}
		
			_erosionPipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
			                BlendStateDescription.SingleDisabled,
			                DepthStencilStateDescription.Disabled,
			                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
			                PrimitiveTopology.TriangleList,
			                eroShaderSet,
			                textureLayout,
			                _erosionFB[0].OutputDescription));    	  
			                
            _terrainFB = Api.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, _erosionTex[0]));
                                
            _pipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleDisabled,
                DepthStencilStateDescription.Disabled,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new ResourceLayout[] {},
                _terrainFB.OutputDescription));            			                
		}
    	
    	public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
    	{
    	 	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(_terrainFB);
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount); 
            cl.SetPipeline(_erosionPipeline);
            
            for (int iter = 0; iter <200; ++iter)
            {
            for (int i = 0; i < 2; ++i)
            {
	            cl.SetFramebuffer(_erosionFB[i]);
				cl.SetGraphicsResourceSet(0, _erosionResourceSet[i]);	            
	            cl.SetVertexBuffer(0, _planeVertexBuffer);
	            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
	            cl.DrawIndexed(_planeIndexCount); 
            }
            }
    	}
    }
}    