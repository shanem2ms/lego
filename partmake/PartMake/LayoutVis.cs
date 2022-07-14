﻿using System;
using AssetPrimitives;
using SampleBase;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

namespace partmake
{
    public class LayoutVis : SampleApplication, IRenderVis
    {
        ResourceFactory _factory;
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _selectedVertexBuffer;
        private DeviceBuffer _selectedIndexBuffer;
        private DeviceBuffer _cubeVertexBuffer;
        private DeviceBuffer _cubeIndexBuffer;
        private DeviceBuffer _planeVertexBuffer;
        private DeviceBuffer _planeIndexBuffer;
        private DeviceBuffer _bspSelVertexBuffer;
        private DeviceBuffer _bspSelIndexBuffer;
        uint _bspSelIndexCount;
        List<int> faceIndices;
        private DeviceBuffer _ldrLoaderVertexBuffer;
        private DeviceBuffer _ldrLoaderIndexBuffer;
        private int _ldrLoaderIndexCount;

        private DeviceBuffer _mbxVertexBuffer;
        private DeviceBuffer _mbxIndexBuffer;
        private int _mbxIndexCount;

        private DeviceBuffer _bspPortalsVertexBuffer;
        private DeviceBuffer _bspPortalsIndexBuffer;
        List<Tuple<uint, uint>> _bspPortalsIndexCounts;
        List<Topology.BSPPortal> _bspPortals;
        List<Topology.PortalFace> _bspPortalFaces;

        private DeviceBuffer _decompVertexBuffer;
        private DeviceBuffer _decompIndexBuffer;
        List<Tuple<uint, uint>> _decompIndexCounts;
        List<Topology.ConvexMesh> _decompMeshes;

        private DeviceBuffer _bspFacesVertexBuffer;
        private DeviceBuffer _bspFacesIndexBuffer;
        uint _bspFacesIndexCount;
        List<Tuple<uint, uint>> _bspFacesIndexCounts;

        uint _cubeIndexCount;
        uint _planeIndexCount;
        private CommandList _cl;
        private Pipeline _pipeline;
        private Pipeline _pipelinePick;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private ResourceSet _raycastSet;
        private Texture _primStgTexture;
        private Texture _pickStgTexture;
        bool _primTexUpdate = false;
        private Texture _primTexture;
        private TextureView _primTextureView;
        private Texture _pickTexture;
        private TextureView _pickTextureView;
        private Framebuffer _pickFB;
        private Sampler _primSampler;
        private int _indexCount;
        private int _triangleCount;
        private int _selectedIndexCount;
        Vector2 lookDir;
        int mouseDown = 0;
        Vector2 lMouseDownPt;
        Vector2 rMouseDownPt;
        Vector2 mouseDownLookDir;
        Vector3 partOffset;
        Vector3 cameraPos = new Vector3(0, 0, -8.5f);
        Vector3 mouseDownCameraPos;
        float partScale;
        float zoom = 2.3f;
        float mouseDownZoom = 0;
        AABB primBbox;
        public event EventHandler<Topology.INode> OnINodeSelected;
        public event EventHandler<Topology.BSPNode> OnBSPNodeSelected;
        public event EventHandler<string> OnLogUpdated;
        private bool onlyShowCoveredPortalFaces = false;
        public bool IsActive { get; set; }
        public LayoutVis(ApplicationWindow window) : base(window)
        {
        }

        bool mouseMoved = false;
        public void MouseDown(int btn, int X, int Y, System.Windows.Forms.Keys keys)
        {
            mouseDown |= 1 << btn;
            mouseDownLookDir = lookDir;
            mouseDownZoom = zoom;
            rMouseDownPt = lMouseDownPt = new Vector2(X, Y);
            mouseDownCameraPos = cameraPos;
            mouseMoved = false;
        }
        public void MouseUp(int btn, int X, int Y)
        {
            mouseDown &= ~(1 << btn);
        }
        public void MouseMove(int X, int Y, System.Windows.Forms.Keys keys)
        {
            mouseMoved = true;
            if ((mouseDown & 1) != 0)
                lookDir = mouseDownLookDir + (new Vector2(X, Y) - lMouseDownPt) * 0.01f;
            if ((mouseDown & 2) != 0)
            {
                if ((keys & System.Windows.Forms.Keys.Shift) != 0)
                {
                    zoom = mouseDownZoom + (Y - rMouseDownPt.Y) * 0.005f;
                }
                else
                {
                    float tscale = 0.01f;
                    cameraPos = mouseDownCameraPos - new Vector3(-X + lMouseDownPt.X, Y - lMouseDownPt.Y, 0) * tscale;
                }
            }
        }

        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            _factory = factory;
            _projectionBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _materialBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            _primTexture = factory.CreateTexture(
                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Sampled, TextureType.Texture2D, TextureSampleCount.Count1));

            _primTextureView = factory.CreateTextureView(_primTexture);
            _primSampler = factory.CreateSampler(new SamplerDescription(SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, SamplerFilter.MinPoint_MagPoint_MipPoint,
                ComparisonKind.Always, 0, 0, 0, 0, SamplerBorderColor.OpaqueBlack));
            _primStgTexture = factory.CreateTexture(
                new TextureDescription(1024, 1024, 1, 1, 1, PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging, TextureType.Texture2D, TextureSampleCount.Count1));


            {
                var cubeVertices = GetCubeVertices();
                _cubeVertexBuffer = factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * cubeVertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(_cubeVertexBuffer, 0, cubeVertices);

                var cubeIndices = GetCubeIndices();
                _cubeIndexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)cubeIndices.Length, BufferUsage.IndexBuffer));
                _cubeIndexCount = (uint)cubeIndices.Length;
                GraphicsDevice.UpdateBuffer(_cubeIndexBuffer, 0, cubeIndices);
            }
            {
                var planeVertices = GetPlaneVertices();
                _planeVertexBuffer = factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * planeVertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(_planeVertexBuffer, 0, planeVertices);

                var planeIndices = GetPlaneIndices();
                _planeIndexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)planeIndices.Length, BufferUsage.IndexBuffer));
                _planeIndexCount = (uint)planeIndices.Length;
                GraphicsDevice.UpdateBuffer(_planeIndexBuffer, 0, planeIndices);
            }

            var assembly = Assembly.GetExecutingAssembly();


            ResourceLayout projViewLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("MeshColor", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

            ResourceLayout raycastLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("RaycastInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("PrimitiveTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("PrimitiveSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _projViewSet = factory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer));
          

            {
                var vsfile = "partmake.vs.glsl";
                var fsfile = "partmake.fs.glsl";
                var floorgridfile = "partmake.floorgrid.glsl";

                string VertexCode;
                string FragmentCode;
                using (Stream stream = assembly.GetManifestResourceStream(vsfile))
                using (StreamReader reader = new StreamReader(stream))
                {
                    VertexCode = reader.ReadToEnd();
                }
                using (Stream stream = assembly.GetManifestResourceStream(floorgridfile))
                using (StreamReader reader = new StreamReader(stream))
                {
                    FragmentCode = reader.ReadToEnd();
                }

                ShaderSetDescription shaderSet = new ShaderSetDescription(
                    new[]
                    {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                    },
                    factory.CreateFromSpirv(
                        new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                        new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main")));



                _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleAlphaBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { projViewLayout, worldTextureLayout },
                    MainSwapchain.Framebuffer.OutputDescription));

            }            

            {
                var vsfile = "partmake.vs.glsl";
                var fsfile = "partmake.pick.glsl";

                string VertexCode;
                string FragmentCode;
                using (Stream stream = assembly.GetManifestResourceStream(vsfile))
                using (StreamReader reader = new StreamReader(stream))
                {
                    VertexCode = reader.ReadToEnd();
                }
                using (Stream stream = assembly.GetManifestResourceStream(fsfile))
                using (StreamReader reader = new StreamReader(stream))
                {
                    FragmentCode = reader.ReadToEnd();
                }

                ShaderSetDescription shaderSet = new ShaderSetDescription(
                    new[]
                    {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                    },
                    factory.CreateFromSpirv(
                        new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                        new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main")));

                _pickTexture = factory.CreateTexture(TextureDescription.Texture2D(
                              1024, 1024, 1, 1,
                               PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled));
                _pickStgTexture = factory.CreateTexture(TextureDescription.Texture2D(
                    1024,
                    1024,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Staging));

                _pickTextureView = factory.CreateTextureView(_pickTexture);
                Texture offscreenDepth = factory.CreateTexture(TextureDescription.Texture2D(
                    1024, 1024, 1, 1, PixelFormat.R16_UNorm, TextureUsage.DepthStencil));
                _pickFB = factory.CreateFramebuffer(new FramebufferDescription(offscreenDepth, _pickTexture));


                _pipelinePick = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { projViewLayout, worldTextureLayout },
                    _pickFB.OutputDescription));

            }
            _cl = factory.CreateCommandList();
        }

        protected override void Draw(float deltaSeconds)
        {
            if (!IsActive)
                return;

            _cl.Begin();

            Matrix4x4 projMat = Matrix4x4.CreatePerspectiveFieldOfView(
                1.0f,
                (float)Window.Width / Window.Height,
                0.05f,
                20f);
            _cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);

            Matrix4x4 viewmat = Matrix4x4.CreateRotationY(lookDir.X) *
                Matrix4x4.CreateRotationX(lookDir.Y) *
                Matrix4x4.CreateTranslation(cameraPos / System.MathF.Pow(2.0f, zoom));
            _cl.UpdateBuffer(_viewBuffer, 0, viewmat);
            Matrix4x4 mat =
                Matrix4x4.CreateTranslation(-partOffset) *
                Matrix4x4.CreateScale(partScale);
            _cl.SetPipeline(_pipeline);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            Vector4 candidateColor = new Vector4(0f, 0.5f, 1.0f, 1.0f);
            _cl.UpdateBuffer(_materialBuffer, 0, ref candidateColor);
            _cl.SetVertexBuffer(0, _planeVertexBuffer);
            _cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            Matrix4x4 cm = Matrix4x4.CreateRotationX(MathF.PI * 0.5f);
            _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
            _cl.DrawIndexed((uint)_planeIndexCount);
            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();
        }

        private Vtx[] GetCubeVertices()
        {
            Vtx[] vertices = new Vtx[]
            {
                // Top
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(0, 1)),
                // Bottom                                                             
                new Vtx(new Vector3(-0.5f,-0.5f, +0.5f),  new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f,-0.5f, +0.5f),  new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f,-0.5f, -0.5f),  new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f,-0.5f, -0.5f),  new Vector2(0, 1)),
                // Left                                                               
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(1, 0)),
                new Vtx(new Vector3(-0.5f, -0.5f, +0.5f), new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, -0.5f, -0.5f), new Vector2(0, 1)),
                // Right                                                              
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, -0.5f, -0.5f), new Vector2(1, 1)),
                new Vtx(new Vector3(+0.5f, -0.5f, +0.5f), new Vector2(0, 1)),
                // Back                                                               
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector2(0, 0)),
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector2(1, 0)),
                new Vtx(new Vector3(-0.5f, -0.5f, -0.5f), new Vector2(1, 1)),
                new Vtx(new Vector3(+0.5f, -0.5f, -0.5f), new Vector2(0, 1)),
                // Front                                                              
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f), new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, -0.5f, +0.5f), new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, -0.5f, +0.5f), new Vector2(0, 1)),
            };

            return vertices;
        }

        private static ushort[] GetCubeIndices()
        {
            ushort[] indices =
            {
                0,1,2, 0,2,3,
                4,5,6, 4,6,7,
                8,9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23,
            };

            return indices;
        }

        private Vtx[] GetPlaneVertices()
        {
            Vtx[] vertices = new Vtx[]
            {
                // Back                                                               
                new Vtx(new Vector3(+1.0f, +1.0f, 0.0f), new Vector2(0, 0)),
                new Vtx(new Vector3(-1.0f, +1.0f, 0.0f), new Vector2(1, 0)),
                new Vtx(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(1, 1)),
                new Vtx(new Vector3(+1.0f, -1.0f, 0.0f), new Vector2(0, 1)),
            };

            return vertices;
        }

        private static ushort[] GetPlaneIndices()
        {
            ushort[] indices =
            {
                0,1,2, 0,2,3
            };

            return indices;
        }



    }



}
