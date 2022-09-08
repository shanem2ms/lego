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
	    private TextureView _terrainTextureView;
    	public void Gen()    
    	{
    		byte []pixShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "TerrainGen.glsl"));    		
    		byte []vtxShaderBytes = File.ReadAllBytes(Path.Combine(Api.ScriptFolder, "vtxblit.glsl"));    		
    		try
    		{
            Api.ResourceFactory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, vtxShaderBytes, "main"),
                new ShaderDescription(ShaderStages.Fragment, pixShaderBytes, "main"));
            }
            catch (Exception e)
            {
	            Api.WriteLine(e.ToString());
            }
    		
            _terrainTexture = Api.ResourceFactory.CreateTexture(
                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_Float, 
                	TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
            _terrainTextureView = Api.ResourceFactory.CreateTextureView(_terrainTexture);
    		_terrainTexture.Dispose();
    		//Api.WriteLine(alltext);
    	}
    }
}    