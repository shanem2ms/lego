using partmake.script;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;
using static partmake.Palette;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;

namespace partmake.graphics
{
    public static class G
    {
        public static ResourceFactory ResourceFactory;
        public static Swapchain Swapchain;
        public static GraphicsDevice GraphicsDevice;
        public static float WorldScale = 0.0025f;
        public static Vector2 WindowSize;
    }
    public static class Layouts
    {
        public static VertexLayoutDescription Standard =
            new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));

        public static VertexLayoutDescription DownScale = 
            new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("UVW", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

    }

    public class Shader
    {
        public static string VSDefault = "partmake.vs.glsl";
        public static string FSDefault = "partmake.fs.glsl";

        static Dictionary<string, Shader> shaderCache = new Dictionary<string, Shader>();

        static byte []ComputeMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return md5.ComputeHash(stream);
                }
            }
        }
        static bool IsEqual(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }
        public static Shader GetShaderFromFile(string vtxpath, string pixpath)
        {
            string key = vtxpath + pixpath;
            Shader outshader = null;
            if (shaderCache.TryGetValue(key, out outshader))
            {
                byte[] vtxmd5 = ComputeMD5(vtxpath);
                byte[] pixmd5 = ComputeMD5(pixpath);
                if (!IsEqual(vtxmd5, outshader.vtxmd5) ||
                    !IsEqual(pixmd5, outshader.pixmd5))
                {
                    shaderCache.Remove(key);
                    outshader = null;
                }
            }
            if (outshader == null)
            {
                outshader = new Shader(vtxpath, pixpath);
                shaderCache.Add(key, outshader);
            }
            return outshader;
        }

        public static Shader GetShaderFromResource(string vtxpath, string pixpath)
        {
            string key = vtxpath + pixpath;
            Shader outshader;
            if (!shaderCache.TryGetValue(key, out outshader))
            {
                outshader = new Shader(vtxpath, pixpath, Assembly.GetExecutingAssembly());
                shaderCache.Add(key, outshader);
            }
            return outshader;
        }

        byte[] vtxshaderBytes;
        byte[] pixshaderBytes;
        Veldrid.Shader[] shaders;
        byte[] vtxmd5;
        byte[] pixmd5;

        public Veldrid.Shader[] Shaders => shaders;
        Shader(string vtxpath, string pixpath)
        {
            vtxmd5 = ComputeMD5(vtxpath);
            pixmd5 = ComputeMD5(pixpath);            

            vtxshaderBytes = File.ReadAllBytes(Path.Combine(vtxpath));
            pixshaderBytes = File.ReadAllBytes(Path.Combine(pixpath));
            CrossCompileOptions cc = new CrossCompileOptions();
            shaders = G.ResourceFactory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, vtxshaderBytes, "main"),
                    new ShaderDescription(ShaderStages.Fragment, pixshaderBytes, "main"));
        }       
        
        Shader(string vtxrsrc, string pixrsrc, Assembly assembly)
        {
            string VertexCode;
            string FragmentCode;
            using (Stream stream = assembly.GetManifestResourceStream(vtxrsrc))
            using (StreamReader reader = new StreamReader(stream))
            {
                VertexCode = reader.ReadToEnd();
            }
            using (Stream stream = assembly.GetManifestResourceStream(pixrsrc))
            using (StreamReader reader = new StreamReader(stream))
            {
                FragmentCode = reader.ReadToEnd();
            }

            shaders = G.ResourceFactory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main"));
        }
    }

    public class ShaderSet
    {
        ShaderSetDescription _shaderSetDescription;
        public ShaderSetDescription Desc => _shaderSetDescription;
        Shader _shader;
        public ShaderSet(string vtxpath, string pixpath, bool fromResource)
        {
            _shader = fromResource ? Shader.GetShaderFromResource(vtxpath, pixpath) :
                Shader.GetShaderFromFile(vtxpath, pixpath);
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
            _texture = G.ResourceFactory.CreateTexture(
                                new TextureDescription(width, height, 1, 1, 1, format,
                                    TextureUsage.RenderTarget | TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));
            _textureView = G.ResourceFactory.CreateTextureView(_texture);
            _frameBuffer = G.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, _texture));
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

        public uint Width => _gpuTexture.Width;
        public uint Height => _gpuTexture.Height;
        public T[] Data => data;
        public CpuTexture(Texture _textureIn)
        {
            _gpuTexture = _textureIn;
            _stagingTexture = G.ResourceFactory.CreateTexture(new TextureDescription(_textureIn.Width, _textureIn.Height, _textureIn.Depth,
                _textureIn.MipLevels, _textureIn.ArrayLayers, _textureIn.Format, TextureUsage.Staging, _textureIn.Type,
                _textureIn.SampleCount));
        }

        public void Update(CommandList cl)
        {
            cl.CopyTexture(this._gpuTexture, this._stagingTexture);
        }

        public void Map()
        {
            MappedResourceView<T> rView = G.GraphicsDevice.Map<T>(this._stagingTexture, MapMode.Read);
            
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
            G.GraphicsDevice.Unmap(this._stagingTexture);
        }

    }
    struct RgbaUI32
    {
        public uint r;
        public uint g;
        public uint b;
        public uint a;

        public override string ToString()
        {
            return $"{r} {g} {b} {a}";
        }
    }
    class MMTex
    {
        public RgbaUI32[][] data;

        public MMTex(int levels)
        {
            data = new RgbaUI32[levels][];
        }

        public MMTex(int levels, string file, ZipArchive za) :
            this(levels)
        {
            for (int i = 0; i < data.Length; i++)
            {
                string mipname = $"{file}_{i}.dat";
                ZipArchiveEntry ent = za.GetEntry(mipname);                
                Stream s = ent.Open();
                byte[] bytes = new byte[ent.Length];
                s.Read(bytes, 0, bytes.Length);
                data[i] = FromBytes<RgbaUI32>(bytes);
            }
        }

        public RgbaUI32[] this[int i]
        {
            get => data[i];
            set { data[i] = value; }
        }
        public int Length => data.Length;

        public void SaveTo(string file)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                FileStream f = File.Create($"{file}_{i}.dat");
                byte[] bytes = getBytes(data[i]);
                f.Write(bytes, 0, bytes.Length);
                f.Close();
            }
        }

    
        byte[] getBytes<T>(T[] data)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] arr = new byte[size * data.Length];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            for (int i = 0; i < data.Length; ++i)
            {
                Marshal.StructureToPtr(data[i], ptr, true);
                Marshal.Copy(ptr, arr, i * size, size);
            }
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        T[] FromBytes<T>(byte[] data)
        {
            
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            int size = Marshal.SizeOf(typeof(T));
            T[] arr = new T[data.Length / size];
            IntPtr curptr = ptr;
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = Marshal.PtrToStructure<T>(curptr);
                curptr = IntPtr.Add(curptr, size);
            }
            Marshal.FreeHGlobal(ptr);
            return arr;
        }
    }

    public static class Utils
    {
        public static bool IntersectPlane(Vector3 l0, Vector3 l1,
            Vector3 planeNrm, Vector3 planePt, out Vector3 iPos)
        {
            Vector3 l = Vector3.Normalize(l1 - l0);
            float denom = Vector3.Dot(planeNrm, l);

            Vector3 p0l0 = planePt - l0;
            float t = Vector3.Dot(p0l0, planeNrm) / denom;
            iPos = l0 + l * t;
            return (t >= 0 && t <= 1);
        }
      
        //Compute the distance from AB to C
        public static Vector2 ClosestPtOnLine(Vector2 pointA, Vector2 pointB, Vector2 pointC)
        {
            Vector2 dir = Vector2.Normalize(pointB - pointA);
            float distAlongLine = Vector2.Dot(pointC - pointA, dir);
            return pointA + dir * distAlongLine;
        }
        public static void GetMouseWsRays(Vector2 screenPos, out Vector3 l0, out Vector3 l1, 
            ref Matrix4x4 invViewProjPick)
        {
            Vector4 wpos0 = Vector4.Transform(new Vector4(screenPos.X, screenPos.Y, 0.0f, 1), invViewProjPick);
            wpos0 /= (wpos0.W * G.WorldScale);
            l0 = new Vector3(wpos0.X, wpos0.Y, wpos0.Z);

            Vector4 wpos1 = Vector4.Transform(new Vector4(screenPos.X, screenPos.Y, 1.0f, 1), invViewProjPick);
            wpos1 /= (wpos1.W * G.WorldScale);
            l1 = new Vector3(wpos1.X, wpos1.Y, wpos1.Z);
        }

    }

}
