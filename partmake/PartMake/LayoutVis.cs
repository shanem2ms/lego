using System;
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
        private DeviceBuffer _cubeVertexBuffer;
        private DeviceBuffer _cubeIndexBuffer;
        private DeviceBuffer _planeVertexBuffer;
        private DeviceBuffer _planeIndexBuffer;


        uint _cubeIndexCount;
        uint _planeIndexCount;
        private CommandList _cl;
        private Pipeline _pipeline;
        private Pipeline _pipelinePick;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private Texture _primStgTexture;
        private Texture _pickStgTexture;
        private Texture _primTexture;
        private TextureView _primTextureView;
        private Texture _pickTexture;
        private TextureView _pickTextureView;
        private Framebuffer _pickFB;
        private Sampler _primSampler;
        Vector2 lookDir;
        int mouseDown = 0;
        Vector2 lMouseDownPt;
        Vector2 rMouseDownPt;
        Vector2 mouseDownLookDir;
        Vector3 cameraPos = new Vector3(0, 0, -8.5f);
        Vector3 mouseDownCameraPos;
        float zoom = 2.3f;
        float mouseDownZoom = 0;
        int pickX, pickY;
        int pickReady = -1;
        int pickIdx = -1;
        int meshSelectedOffset = -1;

        public event EventHandler<int> OnPartPicked;
        public event EventHandler<Topology.BSPNode> OnBSPNodeSelected;
        public event EventHandler<string> OnLogUpdated;
        public List<PartInst> PartList { get; set; } = new List<PartInst>();

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
            if (!mouseMoved)
            {
                pickX = (int)((float)X / (float)Window.Width * 1024.0f);
                pickY = (int)((float)Y / (float)Window.Height * 1024.0f);
                pickReady = 0;
            }
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
                //var floorgridfile = "partmake.floorgrid.glsl";

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
            _cl.SetPipeline(_pipeline);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            foreach (var part in PartList)
            {
                if (part.item == null)
                    continue;
                if (part.item.ldrLoaderVertexBuffer == null)
                    part.item.LoadLdr(_factory, GraphicsDevice);
                if (part.item.ldrLoaderVertexBuffer == null)
                    continue;
                _cl.SetVertexBuffer(0, part.item.ldrLoaderVertexBuffer);
                _cl.SetIndexBuffer(part.item.ldrLoaderIndexBuffer, IndexFormat.UInt32);
                Matrix4x4 cm =
                    Matrix4x4.CreateFromQuaternion(part.rotation) *
                    Matrix4x4.CreateTranslation(part.position) *
                    Matrix4x4.CreateScale(0.0025f);
                _cl.UpdateBuffer(_worldBuffer, 0, ref cm);               
                _cl.UpdateBuffer(_materialBuffer, 0, ref Palette.AllItems[part.paletteIdx].RGBA);
                _cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
            }
            
            if (pickIdx >= 0  && pickIdx < PartList.Count)
            {
                var part = PartList[pickIdx];
                Matrix4x4 cm =
                            Matrix4x4.CreateFromQuaternion(part.rotation) *
                            Matrix4x4.CreateTranslation(part.position) *
                            Matrix4x4.CreateScale(0.0025f);
                foreach (var connector in part.item.Connectors)
                {
                    Matrix4x4 cmat = connector.Mat.ToM44() * cm;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmat);
                    Vector4 color = new Vector4(1, 1, 0, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                    _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
                    _cl.DrawIndexed(_cubeIndexCount);
                }
            }
            DrawPicking(ref viewmat, ref projMat);
            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();
            HandlePick();
        }

        void DrawPicking(ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (pickReady == 0)
            {
                Matrix4x4 viewPrj = viewmat * projMat;

                _cl.SetPipeline(_pipelinePick);
                _cl.SetFramebuffer(_pickFB);
                _cl.ClearColorTarget(0, RgbaFloat.Black);
                _cl.ClearDepthStencil(1f);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);

                
                for (int partIdx = 0; partIdx < PartList.Count; ++partIdx)
                {
                    var part = PartList[partIdx];
                    if (part.item == null)
                        continue;
                    if (part.item.ldrLoaderVertexBuffer == null)
                        part.item.LoadLdr(_factory, GraphicsDevice);
                    if (part.item.ldrLoaderVertexBuffer == null)
                        continue;
                    _cl.SetVertexBuffer(0, part.item.ldrLoaderVertexBuffer);
                    _cl.SetIndexBuffer(part.item.ldrLoaderIndexBuffer, IndexFormat.UInt32);
                    Matrix4x4 cm =
                        Matrix4x4.CreateFromQuaternion(part.rotation) *
                        Matrix4x4.CreateTranslation(part.position) *
                        Matrix4x4.CreateScale(0.0025f);
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    int r = partIdx & 0xFF;
                    int g = (partIdx >> 8) & 0xFF;
                    int b = (partIdx >> 16) & 0xFF;
                    Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                    _cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
                }

                _cl.CopyTexture(this._pickTexture, this._pickStgTexture);
                pickReady = 1;
            }

        }
        struct Rgba
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        void HandlePick()
        {
            if (pickReady == 1)
            {
                meshSelectedOffset = -1;
                MappedResourceView<Rgba> rView = GraphicsDevice.Map<Rgba>(_pickStgTexture, MapMode.Read);
                Rgba r = rView[pickX, pickY];
                pickIdx = r.r + (r.g << 8) + (r.b << 16);
                OnPartPicked?.Invoke(this, pickIdx);
                GraphicsDevice.Unmap(_pickStgTexture);
                pickReady = -1;
            }
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
