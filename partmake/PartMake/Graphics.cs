using partmake.script;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;

namespace partmake.graphics
{
    public static class Layouts
    {
        public static VertexLayoutDescription Standard =
            new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
    }
    public class Shader
    {
        byte[] vtxshaderBytes;
        byte[] pixshaderBytes;
        Veldrid.Shader[] shaders;

        public Veldrid.Shader[] Shaders => shaders;
        public Shader(string vtxpath, string pixpath)
        {
            vtxshaderBytes = File.ReadAllBytes(Path.Combine(vtxpath));
            pixshaderBytes = File.ReadAllBytes(Path.Combine(pixpath));
            shaders = Api.ResourceFactory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, vtxshaderBytes, "main"),
                    new ShaderDescription(ShaderStages.Fragment, pixshaderBytes, "main"));
        }        
    }

    public class ShaderSet
    {
        ShaderSetDescription _shaderSetDescription;
        public ShaderSetDescription Desc => _shaderSetDescription;
        Shader _shader;
        public ShaderSet(string vtxpath, string pixpath)
        {
            _shader = new Shader(vtxpath, pixpath);
            _shaderSetDescription = new ShaderSetDescription(new[] { Layouts.Standard },
                _shader.Shaders);
        }
    }

    public class RenderTarget
    {
        private Framebuffer _frameBuffer;
        private Texture _texture;
        private TextureView _textureView;
        public TextureView View => _textureView;
        public Framebuffer FrameBuffer => _frameBuffer;
        public RenderTarget(uint width, uint height, PixelFormat format)
        {
            _texture = Api.ResourceFactory.CreateTexture(
                                new TextureDescription(width, height, 1, 1, 1, format,
                                    TextureUsage.RenderTarget | TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
            _textureView = Api.ResourceFactory.CreateTextureView(_texture);
            _frameBuffer = Api.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, _texture));
        }
    }
}
