using partmake.script;
using SharpText.Veldrid;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using System.Reflection;
using partmake.graphics;

namespace partmake
{
    class DepthCubeMap
    {
        public class Side
        {
            private Texture _color;
            TextureView _view;
            public Framebuffer _FB;
            public Pipeline _pipeline;
            private DeviceBuffer cbufferTransform;
            private ResourceSet[] depthResourceSets;
            public graphics.MMTex _pixelData;
            DownScale[] _mips;
            Texture[] _staging;
            Vector3 _offset;
            Vector3 _scale;

            public TextureView[] View => new TextureView[] { _view }.Concat(_mips.Select(m => m.View)).ToArray();

            int idx;

            public Side(int _idx, ResourceFactory factory, uint width, uint height,
                ShaderSetDescription depthShaders, ResourceLayout depthLayout, DeviceBuffer[] materials,
                Vector3 offset, Vector3 scale)
            {
                idx = _idx;
                _offset = offset;
                _scale = scale;
                bool frontOrBack = (idx & 1) == 0;
                _color = factory.CreateTexture(TextureDescription.Texture2D(
                                width, height, 1, 1,
                                 PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget | TextureUsage.Sampled));
                _view = factory.CreateTextureView(_color);
                Texture offscreenDepth = factory.CreateTexture(TextureDescription.Texture2D(
                    width, height, 1, 1, PixelFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil));
                _FB = factory.CreateFramebuffer(new FramebufferDescription(offscreenDepth, _color));
                GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    frontOrBack ? DepthStencilStateDescription.DepthOnlyLessEqual : DepthStencilStateDescription.DepthOnlyGreaterEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
                    PrimitiveTopology.TriangleList,
                    depthShaders,
                    depthLayout,
                    _FB.OutputDescription);
                _pipeline = factory.CreateGraphicsPipeline(pd);
                cbufferTransform = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Transform>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
                depthResourceSets = new ResourceSet[materials.Length];
                for (int idx = 0; idx < depthResourceSets.Length; ++idx)
                {
                    depthResourceSets[idx] = factory.CreateResourceSet(new ResourceSetDescription(depthLayout, cbufferTransform, materials[idx]));
                }

                int levels = (int)Math.Log2(width) - 2;
                _mips = new DownScale[levels];
                TextureView curView = _view;
                for (int idx = 0; idx < levels; ++idx)
                {
                    _mips[idx] = new DownScale();
                    _mips[idx].CreateResources(factory, curView, width >> (idx + 1), height >> (idx + 1));
                    curView = _mips[idx].View;
                }
            }

            public void UpdateUniformBufferOffscreen()
            {
                Transform ui = new Transform { LightPos = new Vector4(0, 0, 0, 1) };
                ui.Projection = Matrix4x4.CreateScale(1, 1, 0.5f) *
                    Matrix4x4.CreateTranslation(0, 0, 0.5f);

                ui.View = Matrix4x4.Identity;
                ui.Model = Matrix4x4.Identity;


                Matrix4x4[] rots = new Matrix4x4[]
                {
                Matrix4x4.Identity,
                Matrix4x4.CreateRotationX((float)(Math.PI * 0.5)),
                Matrix4x4.CreateRotationY((float)(Math.PI * 0.5)),
                };

                float maxscale = MathF.Max(MathF.Max(_scale.X, _scale.Y), _scale.Z);

                
                ui.Model = Matrix4x4.CreateTranslation(-_offset) *
                    Matrix4x4.CreateScale(2.0f / maxscale) *
                    Matrix4x4.CreateRotationZ((float)(Math.PI)) *
                    rots[idx / 2];
                G.GraphicsDevice.UpdateBuffer(cbufferTransform, 0, ref ui);
            }

            public void CopyMips(CommandList cl)
            {
                if (_pixelData == null)
                {
                    _pixelData = new graphics.MMTex(2, _mips.Length + 1);
                    _pixelData.baseLod = 2;
                    _staging = new Texture[_mips.Length + 1];
                    for (int idx = 0; idx < _pixelData.Length; ++idx)
                    {
                        Texture tex = idx == 0 ? _color : _mips[idx - 1].OutTexture;
                        _staging[idx] = G.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                            tex.Width, tex.Height, 1, 1,
                             PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));
                        _pixelData[_pixelData.Length - idx - 1] = new graphics.Rgba32[tex.Width * tex.Height];
                    }
                }
                for (int idx = 0; idx < _pixelData.Length; ++idx)
                {
                    Texture tex = idx == 0 ? _color : _mips[idx - 1].OutTexture;
                    cl.CopyTexture(tex, _staging[idx]);
                    CopyTexture(_staging[idx], _pixelData[_pixelData.Length - idx - 1]);
                }
            }

            public int LodCnt => _mips.Length + 1;
            public Texture TexAtLod(int lod)
            {
                return lod == 0 ? _color : _mips[lod - 1].OutTexture;
            }

            void CopyTexture(Texture tex, graphics.Rgba32[] pixelData)
            {
                MappedResourceView<graphics.Rgba32> map = G.GraphicsDevice.Map<graphics.Rgba32>(tex, MapMode.Read);

                for (int y = 0; y < tex.Height; y++)
                {
                    for (int x = 0; x < tex.Width; x++)
                    {
                        int index = (int)(y * tex.Width + x);
                        pixelData[index] = map[x, y];
                    }
                }
                G.GraphicsDevice.Unmap(tex);
            }

            public void Prepare(CommandList cl)
            {
                bool frontOrBack = (idx & 1) == 0;
                cl.SetFramebuffer(_FB);
                cl.SetFullViewports();
                cl.ClearColorTarget(0, RgbaFloat.Black);
                cl.ClearDepthStencil(frontOrBack ? 1f : 0f);
                UpdateUniformBufferOffscreen();
                cl.SetPipeline(_pipeline);
            }

            public void Draw(CommandList cl, int idx)
            {
               cl.SetGraphicsResourceSet(0, depthResourceSets[idx]);
            }

            public void BuildMips(CommandList cl)
            {
                for (int idx = 0; idx < _mips.Length; ++idx)
                {
                    _mips[idx].Draw(cl);
                }
            }

            public void WriteBmp(string path)
            {
                _pixelData.SaveTo(path);
            }
        };

        Side[] sides;

        TextureView[] views = null;
        public TextureView[] View => views;
        static uint Size = 128;
        DeviceBuffer[] materialCbs;
        Vector3 _offset;
        Vector3 _scale;
        DeviceBuffer _vertexBuffer;
        DeviceBuffer _indexBuffer;
        uint _indexCount;
        public struct Transform
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 Model;
            public Vector4 LightPos;
        }

        public struct Material
        {
            public Vector4 DiffuseColor;
        }

        public DepthCubeMap(Vector3 offset, Vector3 scale, DeviceBuffer vertexBuffer,
            DeviceBuffer indexBuffer,
            uint indexCount)
        {
            _offset = offset;
            _scale = scale;
            _vertexBuffer = vertexBuffer;
            _indexBuffer = indexBuffer;
            _indexCount = indexCount;
            CreateResource(G.ResourceFactory, DepthCubeMap.Size, DepthCubeMap.Size);
        }

        void CreateResource(ResourceFactory factory, uint width, uint height)
        {
            ResourceLayout depthLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Fragment) }));

            graphics.ShaderSet shader = new graphics.ShaderSet(
                "partmake.Depth-vertex.glsl", "partmake.Depth-fragment.glsl", true);

            materialCbs = new DeviceBuffer[1];
            for (int idx = 0; idx < 1; ++idx)
            {
                materialCbs[idx] = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Material>(), BufferUsage.UniformBuffer));
                Material m = new Material();
                m.DiffuseColor = new Vector4(1, 1, 1, 1);
                G.GraphicsDevice.UpdateBuffer(materialCbs[idx], 0, ref m);
            }

            sides = new Side[6];
            views = new TextureView[6];
            for (int i = 0; i < 6; ++i)
            {
                sides[i] = new Side(i, factory, width, height, shader.Desc, depthLayout, materialCbs,
                    _offset, _scale);
                views[i] = sides[i].View[0];
            }
        }

        public void DrawOffscreen(            
            CommandList cl)
        {
            for (int idx = 0; idx < 6; ++idx)
            {
                sides[idx].Prepare(cl);
                cl.SetVertexBuffer(0, _vertexBuffer);
                cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
                for (int pIdx = 0; pIdx < 1; ++pIdx)
                {
                    sides[idx].Draw(cl, pIdx);
                    cl.DrawIndexed(_indexCount);
                }

                sides[idx].BuildMips(cl);
            }
        }
    }
}
