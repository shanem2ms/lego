using partmake.script;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;
using static partmake.Palette;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using System.Runtime.ConstrainedExecution;

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

        public CpuTexture<T> GetCpuTexture<T>(CommandList _cl) where T: struct
        {
            var tex = new CpuTexture<T>(this._texture);
            tex.Update(_cl);
            return tex;
        }
    }

    public class CpuTexture<T> where T : struct
    {
        Texture _gpuTexture;
        Texture _stagingTexture;
        T[] data;

        public T[] Data => data;
        public CpuTexture(Texture _textureIn)
        {
            _gpuTexture = _textureIn;
            _stagingTexture = Api.ResourceFactory.CreateTexture(new TextureDescription(_textureIn.Width, _textureIn.Height, _textureIn.Depth,
                _textureIn.MipLevels, _textureIn.ArrayLayers, _textureIn.Format, TextureUsage.Staging, _textureIn.Type,
                _textureIn.SampleCount));
        }

        public void Update(CommandList cl)
        {
            cl.CopyTexture(this._gpuTexture, this._stagingTexture);
        }

        public void Map()
        {
            MappedResourceView<T> rView = Api.GraphicsDevice.Map<T>(this._stagingTexture, MapMode.Read);
            
            IntPtr startPtr = rView.MappedResource.Data;
            uint rowPitch = rView.MappedResource.RowPitch;
            uint width = this._stagingTexture.Width;
            uint height = this._stagingTexture.Height;
            int elemSize = Marshal.SizeOf<T>();
            data = new T[width * height];
            for (uint y = 0; y < this._stagingTexture.Height; ++y)
            {
                uint cy = y * rowPitch;
                IntPtr curPtr = IntPtr.Add(startPtr, (int)cy);
                for (uint x = 0; x < width; ++x)
                {
                    data[y * width + x] = Marshal.PtrToStructure<T>(curPtr);
                    curPtr = IntPtr.Add(curPtr, elemSize);
                }
            }
            Api.GraphicsDevice.Unmap(this._stagingTexture);
        }

    }
}
