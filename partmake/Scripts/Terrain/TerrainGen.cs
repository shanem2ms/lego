using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;
using partmake.graphics;

namespace partmake.script
{
    public class TerrainGen
    {
	    RenderTarget []_erosionRT = new RenderTarget[2];
	    private ResourceSet []_erosionResourceSet = new ResourceSet[2];
	    private Pipeline _erosionPipeline;
	    
        private Pipeline _pipeline;

	    private DeviceBuffer _planeVertexBuffer;
	    private DeviceBuffer _planeIndexBuffer;
	    private uint _planeIndexCount;
	    public TextureView TerrainTexView => _erosionRT[0].View;	    
	    public struct TerrainVal
	    {
	    	public float r;
	    	public float g;
	    	public float b;
	    	public float a;
	    }
	    CpuTexture<TerrainVal> _cpuTexture;
	    public CpuTexture<TerrainVal> CpuTex => _cpuTexture;

    	public void Gen()    
    	{
    		ShaderSet terrainSet = new ShaderSet(
    			Path.Combine(Api.ScriptFolder, "Terrain\\VtxBlit.glsl"),
    			Path.Combine(Api.ScriptFolder, "Terrain\\TerrainGen.glsl"));
    			
    		ShaderSet erosionSet = new ShaderSet(
    			Path.Combine(Api.ScriptFolder, "Terrain\\VtxBlit.glsl"),
    			Path.Combine(Api.ScriptFolder, "Terrain\\Erosion.glsl"));
    				               
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;
                                                                                      						        
			ResourceLayout textureLayout = Api.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(                    
                    new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)));     
            for (int i = 0; i < 2; ++i)
			{
				_erosionRT[i] = new RenderTarget(1024, 1024, PixelFormat.R32_G32_B32_A32_Float);
	    		_erosionResourceSet[1 - i] = Api.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
	    			textureLayout,
	    			_erosionRT[i].View));
			}
		
			_erosionPipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
			                BlendStateDescription.SingleDisabled,
			                DepthStencilStateDescription.Disabled,
			                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
			                PrimitiveTopology.TriangleList,
			                erosionSet.Desc,
			                textureLayout,
			                _erosionRT[0].FrameBuffer.OutputDescription));    	  
			                                                
            _pipeline = Api.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleDisabled,
                DepthStencilStateDescription.Disabled,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                terrainSet.Desc,
                new ResourceLayout[] {},
                _erosionRT[0].FrameBuffer.OutputDescription));     
		}
    	
    	public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
    	{
    	 	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(_erosionRT[1].FrameBuffer);
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount); 
            cl.SetPipeline(_erosionPipeline);
            
            for (int iter = 0; iter <200; ++iter)
            {
            for (int i = 0; i < 2; ++i)
            {
	            cl.SetFramebuffer(_erosionRT[i].FrameBuffer);
				cl.SetGraphicsResourceSet(0, _erosionResourceSet[i]);	            
	            cl.SetVertexBuffer(0, _planeVertexBuffer);
	            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
	            cl.DrawIndexed(_planeIndexCount); 
            }
            }
                                        
            _cpuTexture = _erosionRT[1].GetCpuTexture<TerrainVal>(cl);

    	}
    }
}    