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
using BulletSharp.SoftBody;
using BulletSharp;

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
        private DeviceBuffer _isoVertexBuffer;
        private DeviceBuffer _isoIndexBuffer;

        uint _cubeIndexCount;
        uint _planeIndexCount;
        uint _isoIndexCount;
        private CommandList _cl;
        private Pipeline _pipeline;
        private Pipeline _pipelineConnectors;
        private Pipeline _pipelinePick;
        private Pipeline _pipelinePickConnectors;
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
        Vector2 lookDir = new Vector2(0, 0.4f);
        int mouseDown = 0;
        Vector2 lMouseDownPt;
        Vector2 rMouseDownPt;
        Vector2 mouseDownLookDir;
        Vector3 cameraPos = new Vector3(0, 0, -8.5f);
        Vector3 mouseDownCameraPos;
        float zoom = 3.5f;
        float mouseDownZoom = 0;
        int pickX, pickY;
        int pickReady = -1;
        int pickIdx = -1;
        int selectedPart = -1;
        int selectedConnectorIdx = -1;
        int meshSelectedOffset = -1;

        public class PartPickEvent
        {
            public PartInst part;
            public int connectorIdx;
        }

        public event EventHandler<PartPickEvent> OnPartPicked;
        public event EventHandler<PartPickEvent> OnConnectorPicked;
        public event EventHandler<Topology.BSPNode> OnBSPNodeSelected;
        public event EventHandler<string> OnLogUpdated;

        public Scene scene;
        public bool IsActive { get; set; }
        public LayoutVis(ApplicationWindow window) : base(window)
        {
        }

        public delegate void DrawDebugDel();
        public DrawDebugDel DrawDebug = null;

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
            if ((mouseDown & 2) != 0)
            {
                if ((keys & System.Windows.Forms.Keys.Shift) != 0)
                {
                    zoom = mouseDownZoom + (Y - rMouseDownPt.Y) * 0.005f;
                }
                else
                {
                    lookDir = mouseDownLookDir + (new Vector2(X, Y) - lMouseDownPt) * 0.01f;
                }
            }
            else if ((mouseDown & 1) != 0)
            {
                float tscale = 0.01f;
                cameraPos = mouseDownCameraPos - new Vector3(-X + lMouseDownPt.X, Y - lMouseDownPt.Y, 0) * tscale;
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
            {
                var vectors = new List<Vector3>();
                var indices = new List<int>();
                GeometryProvider.Icosahedron(vectors, indices);
                var isoVertices = vectors.Select(v => new Vtx(v, Vector3.Normalize(v), new Vector2(v.X, v.Y))).ToArray();
                _isoVertexBuffer = factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * isoVertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(_isoVertexBuffer, 0, isoVertices);

                var isoIndices = indices.ToArray();
                _isoIndexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)isoIndices.Length, BufferUsage.IndexBuffer));
                _isoIndexCount = (uint)isoIndices.Length;
                GraphicsDevice.UpdateBuffer(_isoIndexBuffer, 0, isoIndices);
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

                _pipelineConnectors = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleAlphaBlend,
                    DepthStencilStateDescription.Disabled,
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

                _pipelinePickConnectors = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.Disabled,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { projViewLayout, worldTextureLayout },
                    _pickFB.OutputDescription));                
            }


            _cl = factory.CreateCommandList();
        }

        float worldScale = 0.0025f;
        protected override void Draw(float deltaSeconds)
        {
            scene.StepSimulation();
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
            _cl.SetVertexBuffer(0, _cubeVertexBuffer);
            _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

            float gridSize = 100;
            for (int z = -100; z < 100; ++z)
            {
                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
                _cl.UpdateBuffer(_materialBuffer, 0, new Vector4(1, 1, 1, 0.5f));

                Matrix4x4 cmz =
                    Matrix4x4.CreateScale(100 * gridSize, 1, 1) *
                    Matrix4x4.CreateTranslation(0, 0, z * gridSize) *
                    Matrix4x4.CreateScale(worldScale);

                _cl.UpdateBuffer(_worldBuffer, 0, ref cmz);
                _cl.DrawIndexed(_cubeIndexCount);

                Matrix4x4 cmx =
                    Matrix4x4.CreateScale(1, 1, 100 * gridSize) *
                    Matrix4x4.CreateTranslation(z * gridSize, 0, 0) *
                    Matrix4x4.CreateScale(worldScale);

                _cl.UpdateBuffer(_worldBuffer, 0, ref cmx);
                _cl.DrawIndexed(_cubeIndexCount);
            }
            foreach (var part in scene.PartList)
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
                    part.mat *
                    Matrix4x4.CreateScale(worldScale);
                _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                _cl.UpdateBuffer(_materialBuffer, 0, ref Palette.AllItems[part.paletteIdx].RGBA);
                _cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
            }

            if (selectedPart >= 0 && selectedPart < scene.PartList.Count)
            {
                _cl.SetPipeline(_pipelineConnectors);
                var part = scene.PartList[selectedPart];
                Matrix4x4 cm =
                            part.mat *
                            Matrix4x4.CreateScale(worldScale);
                foreach (var connector in part.item.Connectors)
                {
                    if (connector.Type == ConnectorType.StudJ || connector.Type == ConnectorType.Clip)
                        continue;
                    Vector3 pos = connector.Pos;
                    Quaternion q = connector.Dir;
                    _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                    _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                    Matrix4x4 cmat2 =
                        Matrix4x4.CreateTranslation(new Vector3(0, 0.5f, 0)) *
                        Matrix4x4.CreateScale(new Vector3(1, 8, 1)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(pos) * cm;

                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmat2);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref Connector.ColorsForType[(int)connector.Type]);
                    _cl.DrawIndexed(_cubeIndexCount);

                    Matrix4x4 cmatX =
                        Matrix4x4.CreateTranslation(new Vector3(0.5f, 0, 0)) *
                        Matrix4x4.CreateScale(new Vector3(8, 1, 1)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(pos) * cm;

                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmatX);
                    Vector4 col = new Vector4(1, 0, 0, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed(_cubeIndexCount);

                    Matrix4x4 cmatZ =
                        Matrix4x4.CreateTranslation(new Vector3(0, 0, 0.5f)) *
                        Matrix4x4.CreateScale(new Vector3(1, 1, 8)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(pos) * cm;

                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmatZ);
                    col = new Vector4(0, 0, 1, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed(_cubeIndexCount);

                    /*
                    Matrix4x4 cmat3 =
                        Matrix4x4.CreateTranslation(new Vector3(0, 2.0f, 0)) *
                        Matrix4x4.CreateScale(new Vector3(3, 3, 3)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(pos) * cm;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmat3);
                    Vector4 col = new Vector4(1, 1, 0, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.SetVertexBuffer(0, _isoVertexBuffer);
                    _cl.SetIndexBuffer(_isoIndexBuffer, IndexFormat.UInt32);
                    _cl.DrawIndexed(_isoIndexCount);*/

                }
            }
            if (scene.DebugLocators.Count > 0)
            {
                _cl.SetPipeline(_pipelineConnectors);
                foreach (Vector4 loc in scene.DebugLocators)
                {
                    Matrix4x4 mat =
                        Matrix4x4.CreateScale(loc.W) *
                        Matrix4x4.CreateTranslation(new Vector3(loc.X, loc.Y, loc.Z)) *
                        Matrix4x4.CreateScale(worldScale);

                    _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                    Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    _cl.SetVertexBuffer(0, _isoVertexBuffer);
                    _cl.SetIndexBuffer(_isoIndexBuffer, IndexFormat.UInt32);
                    _cl.DrawIndexed(_isoIndexCount);
                }
            }

            if (DrawDebug != null)
                DrawDebug();
            DrawPicking(ref viewmat, ref projMat);
            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();
            HandlePick();
        }

        public void BulletDebugDrawLine(Vector3 from, Vector3 to, Vector3 color)
        {
            float len = (from - to).Length();
            Vector3 dx = Vector3.Normalize(to - from);
            Vector3 dir2 = MathF.Abs(dx.X) > MathF.Abs(dx.Y) ? Vector3.UnitY : Vector3.UnitX;
            Vector3 dy = Vector3.Cross(dx, dir2);
            Vector3 dz = Vector3.Cross(dy, dx);

            Matrix4x4 rotmat = new Matrix4x4(dx.X, dx.Y, dx.Z, 0,
                dy.X, dy.Y, dy.Z, 0,
                dz.X, dz.Y, dz.Z, 0,
                0, 0, 0, 1);

            Matrix4x4 mat =
                    Matrix4x4.CreateScale(len, 1, 1) *
                    rotmat *
                    Matrix4x4.CreateTranslation((from + to) * 0.5f) *
                    Matrix4x4.CreateScale(worldScale);
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 col = new Vector4(color.X, color.Y, color.Z, 1);
            _cl.UpdateBuffer(_materialBuffer, 0, ref col);
            _cl.SetVertexBuffer(0, _cubeVertexBuffer);
            _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
            _cl.DrawIndexed(_cubeIndexCount);
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


                for (int partIdx = 0; partIdx < scene.PartList.Count; ++partIdx)
                {
                    var part = scene.PartList[partIdx];
                    if (part.item == null)
                        continue;
                    if (part.item.ldrLoaderVertexBuffer == null)
                        part.item.LoadLdr(_factory, GraphicsDevice);
                    if (part.item.ldrLoaderVertexBuffer == null)
                        continue;
                    _cl.SetVertexBuffer(0, part.item.ldrLoaderVertexBuffer);
                    _cl.SetIndexBuffer(part.item.ldrLoaderIndexBuffer, IndexFormat.UInt32);
                    Matrix4x4 cm =
                        part.mat *
                        Matrix4x4.CreateScale(worldScale);
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    int pickIdx = partIdx + 1024;
                    int r = pickIdx & 0xFF;
                    int g = (pickIdx >> 8) & 0xFF;
                    int b = (pickIdx >> 16) & 0xFF;
                    Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                    _cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
                }

                if (selectedPart >= 0 && selectedPart < scene.PartList.Count)
                {
                    _cl.SetPipeline(_pipelinePickConnectors);
                    var part = scene.PartList[selectedPart];
                    Matrix4x4 cm =
                                part.mat *
                                Matrix4x4.CreateScale(worldScale);
                    for (int connectorIdx = 0; connectorIdx < part.item.Connectors.Length; ++connectorIdx)
                    {
                        int pickIdx = connectorIdx + 128;
                        int r = pickIdx & 0xFF;
                        int g = (pickIdx >> 8) & 0xFF;
                        int b = (pickIdx >> 16) & 0xFF;
                        Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);

                        var connector = part.item.Connectors[connectorIdx];
                        if (connector.Type == ConnectorType.StudJ || connector.Type == ConnectorType.Clip)
                            continue;

                        Vector3 pos, scl;
                        Quaternion q;
                        Matrix4x4.Decompose(connector.Mat.ToM44(), out scl, out q, out pos);
                        Matrix4x4 m = connector.Mat.ToM44();
                        Matrix4x4 cmat = connector.Mat.ToM44() * cm;
                        Matrix4x4 cmat2 =
                            Matrix4x4.CreateTranslation(new Vector3(0, -0.5f, 0)) *
                            Matrix4x4.CreateScale(new Vector3(2, 8, 2)) *
                            Matrix4x4.CreateFromQuaternion(q) *
                            Matrix4x4.CreateTranslation(pos) * cm;

                        _cl.UpdateBuffer(_worldBuffer, 0, ref cmat2);
                        _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                        _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                        _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
                        _cl.DrawIndexed(_cubeIndexCount);

                        Matrix4x4 cmat3 =
                            Matrix4x4.CreateTranslation(new Vector3(0, -2, 0)) *
                            Matrix4x4.CreateScale(new Vector3(3, 3, 3)) *
                            Matrix4x4.CreateFromQuaternion(q) *
                            Matrix4x4.CreateTranslation(pos) * cm;
                        _cl.UpdateBuffer(_worldBuffer, 0, ref cmat3);

                        _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                        _cl.SetVertexBuffer(0, _isoVertexBuffer);
                        _cl.SetIndexBuffer(_isoIndexBuffer, IndexFormat.UInt32);
                        _cl.DrawIndexed(_isoIndexCount);

                    }
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
                selectedConnectorIdx = -1;
                if (pickIdx >= 1024)
                {
                    selectedPart = pickIdx - 1024;
                    OnPartPicked?.Invoke(this, new PartPickEvent() { part = scene.PartList[selectedPart], connectorIdx = -1 });
                }
                else if (pickIdx >= 128)
                {
                    selectedConnectorIdx = pickIdx - 128;
                    OnConnectorPicked?.Invoke(this, new PartPickEvent() { part = scene.PartList[selectedPart], connectorIdx = selectedConnectorIdx });
                }
                else
                {
                    selectedPart = -1;
                }
                GraphicsDevice.Unmap(_pickStgTexture);
                pickReady = -1;
            }
        }

        private Vtx[] GetCubeVertices()
        {
            Vtx[] vertices = new Vtx[]
            {
                // Top
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector3(0,-1,0), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector3(0,-1,0), new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f), new Vector3(0,-1,0), new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector3(0,-1,0), new Vector2(0, 1)),
                // Bottom                                                             
                new Vtx(new Vector3(-0.5f,-0.5f, +0.5f),  new Vector3(0, 1,0), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f,-0.5f, +0.5f),  new Vector3(0, 1,0), new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f,-0.5f, -0.5f),  new Vector3(0, 1,0), new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f,-0.5f, -0.5f),  new Vector3(0, 1,0), new Vector2(0, 1)),
                // Left                                                               
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector3(-1,0,0), new Vector2(0, 0)),
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector3(-1,0,0),new Vector2(1, 0)),
                new Vtx(new Vector3(-0.5f, -0.5f, +0.5f), new Vector3(-1,0,0),new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-1,0,0),new Vector2(0, 1)),
                // Right                                                              
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f),new Vector3(1,0,0), new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector3(1,0,0),new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, -0.5f, -0.5f),new Vector3(1,0,0), new Vector2(1, 1)),
                new Vtx(new Vector3(+0.5f, -0.5f, +0.5f),new Vector3(1,0,0), new Vector2(0, 1)),
                // Back                                                               
                new Vtx(new Vector3(+0.5f, +0.5f, -0.5f), new Vector3(0,0,-1), new Vector2(0, 0)),
                new Vtx(new Vector3(-0.5f, +0.5f, -0.5f), new Vector3(0,0,-1),new Vector2(1, 0)),
                new Vtx(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0,0,-1),new Vector2(1, 1)),
                new Vtx(new Vector3(+0.5f, -0.5f, -0.5f), new Vector3(0,0,-1),new Vector2(0, 1)),
                // Front                                                              
                new Vtx(new Vector3(-0.5f, +0.5f, +0.5f), new Vector3(0,0,1),new Vector2(0, 0)),
                new Vtx(new Vector3(+0.5f, +0.5f, +0.5f), new Vector3(0,0,1),new Vector2(1, 0)),
                new Vtx(new Vector3(+0.5f, -0.5f, +0.5f), new Vector3(0,0,1),new Vector2(1, 1)),
                new Vtx(new Vector3(-0.5f, -0.5f, +0.5f), new Vector3(0,0,1),new Vector2(0, 1)),
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

    public static class GeometryProvider
    {
        private static int GetMidpointIndex(Dictionary<string, int> midpointIndices, List<Vector3> vertices, int i0, int i1)
        {

            var edgeKey = string.Format("{0}_{1}", Math.Min(i0, i1), Math.Max(i0, i1));

            var midpointIndex = -1;

            if (!midpointIndices.TryGetValue(edgeKey, out midpointIndex))
            {
                var v0 = vertices[i0];
                var v1 = vertices[i1];

                var midpoint = (v0 + v1) / 2f;

                if (vertices.Contains(midpoint))
                    midpointIndex = vertices.IndexOf(midpoint);
                else
                {
                    midpointIndex = vertices.Count;
                    vertices.Add(midpoint);
                    midpointIndices.Add(edgeKey, midpointIndex);
                }
            }


            return midpointIndex;

        }


        public static void Subdivide(List<Vector3> vectors, List<int> indices, bool removeSourceTriangles)
        {
            var midpointIndices = new Dictionary<string, int>();

            var newIndices = new List<int>(indices.Count * 4);

            if (!removeSourceTriangles)
                newIndices.AddRange(indices);

            for (var i = 0; i < indices.Count - 2; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpointIndex(midpointIndices, vectors, i0, i1);
                var m12 = GetMidpointIndex(midpointIndices, vectors, i1, i2);
                var m02 = GetMidpointIndex(midpointIndices, vectors, i2, i0);

                newIndices.AddRange(
                    new[] {
                    i0,m01,m02
                    ,
                    i1,m12,m01
                    ,
                    i2,m02,m12
                    ,
                    m02,m01,m12
                    }
                    );

            }

            indices.Clear();
            indices.AddRange(newIndices);
        }

        /// <summary>
        /// create a regular icosahedron (20-sided polyhedron)
        /// </summary>
        /// <param name="primitiveType"></param>
        /// <param name="size"></param>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <remarks>
        /// You can create this programmatically instead of using the given vertex 
        /// and index list, but it's kind of a pain and rather pointless beyond a 
        /// learning exercise.
        /// </remarks>

        /// note: icosahedron definition may have come from the OpenGL red book. I don't recall where I found it. 
        public static void Icosahedron(List<Vector3> vertices, List<int> indices)
        {

            indices.AddRange(
                new int[]
                {
                0,4,1,
                0,9,4,
                9,5,4,
                4,5,8,
                4,8,1,
                8,10,1,
                8,3,10,
                5,3,8,
                5,2,3,
                2,7,3,
                7,10,3,
                7,6,10,
                7,11,6,
                11,0,6,
                0,1,6,
                6,1,10,
                9,0,11,
                9,11,2,
                9,2,5,
                7,2,11
                }
                .Select(i => i + vertices.Count)
            );

            var X = 0.525731112119133606f;
            var Z = 0.850650808352039932f;

            vertices.AddRange(
                new[]
                {
                new Vector3(-X, 0f, Z),
                new Vector3(X, 0f, Z),
                new Vector3(-X, 0f, -Z),
                new Vector3(X, 0f, -Z),
                new Vector3(0f, Z, X),
                new Vector3(0f, Z, -X),
                new Vector3(0f, -Z, X),
                new Vector3(0f, -Z, -X),
                new Vector3(Z, X, 0f),
                new Vector3(-Z, X, 0f),
                new Vector3(Z, -X, 0f),
                new Vector3(-Z, -X, 0f)
                }
            );


        }
    }

}
