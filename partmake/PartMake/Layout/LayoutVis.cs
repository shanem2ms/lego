using System;
using AssetPrimitives;
using VeldridBase;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;
using SharpText.Veldrid;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using BulletSharp.SoftBody;
using BulletSharp;
using System.Windows.Input;
using SharpText.Core;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Diagnostics;
using partmake.graphics;
using System.Threading;
using Typography.OpenFont.Tables;

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
        private CommandList _partCl;
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
        Vector3 cameraPos = new Vector3(-3.5f, 0.8f, 0.18f);
        Vector3 mouseDownCameraPos;
        float zoom = 3.5f;
        float mouseDownZoom = 0;
        int pickReady = -1;
        CommandList pickCommandList;
        int pickIdx = -1;
        int selectedPart = -1;
        int selectedConnectorIdx = -1;
        int meshSelectedOffset = -1;
        bool needPickBufferRefresh = true;
        int[] pickBuffer = new int[1024 * 1024 * 2];
        Matrix4x4 invViewProjPick;
        Matrix4x4 viewProjPick;
        VeldridTextRenderer textRenderer;

        Matrix4x4 ViewMat =>
                GetLookMatrix() *
                Matrix4x4.CreateTranslation(cameraPos);

        Matrix4x4 ProjMat => Matrix4x4.CreatePerspectiveFieldOfView(
                1.0f,
                (float)Window.Width / Window.Height,
                0.05f,
                20f);
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

        Thread pickThread;
        AutoResetEvent pickEvent = new AutoResetEvent(false);
        bool terminatePickThread = false;
        public LayoutVis(ApplicationWindow window) : base(window)
        {
            pickThread = new Thread(PickThreadProc);
            pickThread.Start();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.ShutdownStarted += CurrentDispatcher_ShutdownStarted;
        }

        private void CurrentDispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            this.terminatePickThread = true;
            pickEvent.Set();
        }

        public delegate void DrawDebugDel();
        public DrawDebugDel DrawDebug = null;
        public event EventHandler<bool> AfterDeviceCreated;
        public delegate void CustomDrawDel(CommandList cl, ref Matrix4x4 viewMat, ref Matrix4x4 projMat);
        
        public enum MouseCommand
        {
            ButtonDown,
            ButtonUp,
            Moved
        }
        public delegate void MouseDel(MouseCommand command, int btn, Vector2 screenPos, 
            Vector3 w0, Vector3 w1, ref Matrix4x4 viewProj);

        bool mouseMoved = false;

        bool[] moveDirs = new bool[6];
        public bool OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.W:
                    moveDirs[0] = true;
                    break;
                case System.Windows.Input.Key.S:
                    moveDirs[1] = true;
                    break;
                case System.Windows.Input.Key.A:
                    moveDirs[2] = true;
                    break;
                case System.Windows.Input.Key.D:
                    moveDirs[3] = true;
                    break;
                case System.Windows.Input.Key.Space:
                    moveDirs[4] = true;
                    break;
                case System.Windows.Input.Key.LeftShift:
                case System.Windows.Input.Key.RightShift:
                    moveDirs[5] = true;
                    break;
            }
            return true;
        }
        public bool OnKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.W:
                    moveDirs[0] = false;
                    break;
                case System.Windows.Input.Key.S:
                    moveDirs[1] = false;
                    break;
                case System.Windows.Input.Key.A:
                    moveDirs[2] = false;
                    break;
                case System.Windows.Input.Key.D:
                    moveDirs[3] = false;
                    break;
                case System.Windows.Input.Key.Space:
                    moveDirs[4] = false;
                    break;
                case System.Windows.Input.Key.LeftShift:
                case System.Windows.Input.Key.RightShift:
                    moveDirs[5] = false;
                    break;
            }
            return true;
        }

        void InvalidateView()
        {
            needPickBufferRefresh = true;
        }
        public void MouseDown(int btn, int X, int Y, Keys keys)
        {
            mouseDown |= 1 << btn;
            mouseDownLookDir = lookDir;
            mouseDownZoom = zoom;
            rMouseDownPt = lMouseDownPt = new Vector2(X, Y);
            mouseDownCameraPos = cameraPos;
            mouseMoved = false;
            if (btn == 0)
            {
                if (needPickBufferRefresh && pickReady == -1)
                {
                    
                }
                else
                {
                    int partIdx, connectorIdx;
                    Vector3 worldPos;
                    PickFromBuffer(X, Y, out worldPos, out partIdx, out connectorIdx);

                    if (partIdx >= 0)
                    {
                        selectedPart = partIdx;
                        OnPartPicked?.Invoke(this, new PartPickEvent() { part = scene.PartList[selectedPart], connectorIdx = -1 });
                    }
                    else if (connectorIdx >= 0)
                    {
                        selectedConnectorIdx = connectorIdx;
                        OnConnectorPicked?.Invoke(this, new PartPickEvent() { part = scene.PartList[selectedPart], connectorIdx = selectedConnectorIdx });
                    }
                    else
                        selectedPart = -1;

                    Vector2 screenPos = new Vector2(
                        (X / (float)Window.Width) * 2 - 1.0f,
                        1.0f - (Y / (float)Window.Height) * 2);

                    Vector3 l0, l1;
                    Utils.GetMouseWsRays(screenPos, out l0, out l1, ref invViewProjPick);
                    if (script.Api.MouseHandler != null)
                        script.Api.MouseHandler(MouseCommand.ButtonDown, btn, screenPos, l0, l1, ref viewProjPick);
                }
            }
        }


        void PickThreadProc()
        {
            while (!terminatePickThread)
            {
                pickEvent.WaitOne();
                if (terminatePickThread)
                    break;
                Matrix4x4 projMat = Matrix4x4.CreatePerspectiveFieldOfView(
                                                    1.0f,
                                                    (float)Window.Width / Window.Height,
                                                    0.05f,
                                                    20f);
                Matrix4x4 viewmat =
                    GetLookMatrix() *
                    Matrix4x4.CreateTranslation(cameraPos);
                Matrix4x4.Invert(viewmat, out viewmat);
                CommandList clPick = DrawPicking(ref viewmat, ref projMat);
                this.pickCommandList = clPick;
                this.pickReady = 1;
            }
        }

        public void MouseUp(int btn, int X, int Y)
        {
            mouseDown &= ~(1 << btn);
            if (!mouseMoved)
            {
            }

            Vector2 screenPos = new Vector2(
                (X / (float)Window.Width) * 2 - 1.0f,
                1.0f - (Y / (float)Window.Height) * 2);

            if (script.Api.MouseHandler != null)
                script.Api.MouseHandler(MouseCommand.ButtonUp, btn, screenPos, Vector3.Zero, Vector3.One, ref viewProjPick);
        }
        public void MouseMove(int X, int Y, System.Windows.Forms.Keys keys)
        {
            textRenderer.Update();
            mouseMoved = true;
            if ((mouseDown & 2) != 0)
            {
                if ((keys & System.Windows.Forms.Keys.Shift) != 0)
                {
                    zoom = mouseDownZoom + (Y - rMouseDownPt.Y) * 0.005f;
                }
                else
                {
                    lookDir = mouseDownLookDir + (lMouseDownPt - new Vector2(X, Y)) * 0.01f;
                }

                InvalidateView();
            }
            else if ((mouseDown & 4) != 0)
            {
                float tscale = 0.01f;
                cameraPos = mouseDownCameraPos - new Vector3(-X + lMouseDownPt.X, Y - lMouseDownPt.Y, 0) * tscale;
                InvalidateView();
            }
            else
            {
                {
                    int partIdx, connectorIdx;
                    Vector3 worldPos;
                    PickFromBuffer(X, Y, out worldPos, out partIdx, out connectorIdx);
                    //textRenderer.DrawText($"{worldPos}", new Vector2(X, Y), new SharpText.Core.Color(1, 1, 0, 1), 1);

                    Vector2 screenPos = new Vector2(
                        (X / (float)Window.Width) * 2 - 1.0f,
                        1.0f - (Y / (float)Window.Height) * 2);

                    Vector3 l0, l1;
                    Utils.GetMouseWsRays(screenPos, out l0, out l1, ref invViewProjPick);
                    if (script.Api.MouseHandler != null)
                        script.Api.MouseHandler(MouseCommand.Moved, -1, screenPos, l0, l1, ref viewProjPick);
                }
            }
        }

        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            G.ResourceFactory = factory;
            G.Swapchain = MainSwapchain;
            G.GraphicsDevice = GraphicsDevice;
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


            _cubeVertexBuffer = primitives.Cube.VertexBuffer;
            _cubeIndexBuffer = primitives.Cube.IndexBuffer;
            _cubeIndexCount = primitives.Cube.IndexLength;
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;

            {
                var vectors = new List<Vector3>();
                var indices = new List<int>();
                primitives.GeometryProvider.Icosahedron(vectors, indices);
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
                ShaderSet ss = new ShaderSet(
                    partmake.graphics.Shader.VSDefault,
                    partmake.graphics.Shader.FSDefault, true);


                _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleAlphaBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    ss.Desc,
                    new[] { projViewLayout, worldTextureLayout },
                    MainSwapchain.Framebuffer.OutputDescription));

                _pipelineConnectors = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleAlphaBlend,
                    DepthStencilStateDescription.Disabled,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    ss.Desc,
                    new[] { projViewLayout, worldTextureLayout },
                    MainSwapchain.Framebuffer.OutputDescription));
            }

            {
                var vsfile = "partmake.vs.glsl";
                var fsfile = "partmake.fs.glsl";
                ShaderSet ss = new ShaderSet(vsfile, fsfile, true);
                
                _pickTexture = factory.CreateTexture(TextureDescription.Texture2D(
                              1024, 1024, 1, 1,
                               PixelFormat.R32_G32_UInt, TextureUsage.RenderTarget | TextureUsage.Sampled));
                _pickStgTexture = factory.CreateTexture(TextureDescription.Texture2D(
                    1024,
                    1024,
                    1,
                    1,
                    PixelFormat.R32_G32_UInt,
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
                    ss.Desc,
                    new[] { projViewLayout, worldTextureLayout },
                    _pickFB.OutputDescription));

                _pipelinePickConnectors = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.Disabled,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    ss.Desc,
                    new[] { projViewLayout, worldTextureLayout },
                    _pickFB.OutputDescription));
            }

            _cl = factory.CreateCommandList();
            _partCl = factory.CreateCommandList();
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("partmake.unispace.ttf"))
            {
                Font font = new Font(s, 20);
                textRenderer = new VeldridTextRenderer(GraphicsDevice, _cl, font);
            }
            AfterDeviceCreated?.Invoke(this, true);
        }

        public void OnResize(uint width, uint height)
        {
            G.WindowSize = new Vector2(Window.Width, Window.Height);
            textRenderer.ResizeToSwapchain();
        }
        
        Matrix4x4 GetLookMatrix()
        {
            return Matrix4x4.CreateRotationX(lookDir.Y) *
                Matrix4x4.CreateRotationY(lookDir.X);
        }

        void UpdatePosition()
        {
            Matrix4x4 m = GetLookMatrix();
            Vector3 zdir = Vector3.TransformNormal(Vector3.UnitZ, m);
            Vector3 xdir = Vector3.TransformNormal(Vector3.UnitX, m);
            Vector3 zfwd = Vector3.Cross(xdir, Vector3.UnitY);
            Vector3 xside = Vector3.Cross(Vector3.UnitY, zfwd);
            float speed = 0.025f;
            if (moveDirs[0])
            {
                cameraPos -= zfwd * speed;
                InvalidateView();
            }
            if (moveDirs[1])
            {
                cameraPos += zfwd * speed;
                InvalidateView();
            }
            if (moveDirs[2])
            {
                InvalidateView();
                cameraPos -= xside * speed;
            }
            if (moveDirs[3])
            {
                InvalidateView();
                cameraPos += xside * speed;
            }
            if (moveDirs[4])
            {
                InvalidateView();
                cameraPos += Vector3.UnitY * speed;
            }
            if (moveDirs[5])
            {
                InvalidateView();
                cameraPos -= Vector3.UnitY * speed;
            }
        }
        protected override void Draw(float deltaSeconds)
        {
            if (needPickBufferRefresh && pickReady == -1)
            {
                needPickBufferRefresh = false;
                pickReady = 0;
                pickEvent.Set();
            }

            scene.GameLoop(lookDir, cameraPos);
            UpdatePosition();
            _cl.Begin();

            Matrix4x4 projMat = ProjMat;
            _cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);

            Matrix4x4 viewmat = ViewMat;
            Matrix4x4.Invert(viewmat, out viewmat);
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
                    Matrix4x4.CreateScale(G.WorldScale);

                _cl.UpdateBuffer(_worldBuffer, 0, ref cmz);
                _cl.DrawIndexed(_cubeIndexCount);

                Matrix4x4 cmx =
                    Matrix4x4.CreateScale(1, 1, 100 * gridSize) *
                    Matrix4x4.CreateTranslation(z * gridSize, 0, 0) *
                    Matrix4x4.CreateScale(G.WorldScale);

                _cl.UpdateBuffer(_worldBuffer, 0, ref cmx);
                _cl.DrawIndexed(_cubeIndexCount);
            }
            _partCl.Begin();
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
                    Matrix4x4.CreateScale(G.WorldScale);
                _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                //Vector4 v = new Vector4(part.mainTile.tileColor, 1);
                _cl.UpdateBuffer(_materialBuffer, 0, Palette.AllItems[part.paletteIdx].RGBA);
                _cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
            }
            textRenderer.Draw();

            if (selectedPart >= 0 && selectedPart < scene.PartList.Count)
            {
                _cl.SetPipeline(_pipelineConnectors);
                var part = scene.PartList[selectedPart];
                Matrix4x4 cm =
                            part.mat *
                            Matrix4x4.CreateScale(G.WorldScale);
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
                }

                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                //for (int idx = 0; idx < part.item.CollisionPts.Length; ++idx)
                {
                    //Vector3 vmin = part.item.CollisionPts[idx].min;
                    //Vector3 vmax = part.item.CollisionPts[idx].max;
                    Vector3 vmin = part.item.MinBounds;
                    Vector3 vmax = part.item.MaxBounds;
                    Vector3 midpt = (vmin + vmax) * 0.5f;
                    Vector3 scl = (vmax - vmin);
                    Matrix4x4 cmatBounds = Matrix4x4.CreateScale(scl) *
                        Matrix4x4.CreateTranslation(midpt) * cm;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cmatBounds);
                    Vector4 col = new Vector4(0, 1, 1, 0.5f);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed(_cubeIndexCount);
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
                        Matrix4x4.CreateScale(G.WorldScale);

                    _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                    Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    _cl.SetVertexBuffer(0, _isoVertexBuffer);
                    _cl.SetIndexBuffer(_isoIndexBuffer, IndexFormat.UInt32);
                    _cl.DrawIndexed(_isoIndexCount);
                }
            }

            if (script.Api.CustomDraw != null)
            {
                try
                {
                    script.Api.CustomDraw(_cl, ref viewmat, ref projMat);
                }
                catch (Exception e)
                {
                    script.Api.WriteLine(e.ToString());
                    script.Api.CustomDraw = null;
                }
            }
            if (DrawDebug != null)
                DrawDebug();
            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);

            if (pickReady == 1)
            {
                GraphicsDevice.SubmitCommands(pickCommandList);
                pickCommandList = null;
                pickReady = 2;
            }
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();
            HandlePick(ref viewmat, ref projMat);
        }

        public void BulletDebugDrawLine(Vector3 from, Vector3 to, Vector3 color)
        {
            float lineWidth = 0.5f;
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
                    Matrix4x4.CreateScale(len, lineWidth, lineWidth) *
                    rotmat *
                    Matrix4x4.CreateTranslation((from + to) * 0.5f) *
                    Matrix4x4.CreateScale(G.WorldScale);
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 col = new Vector4(color.X, color.Y, color.Z, 1);
            _cl.UpdateBuffer(_materialBuffer, 0, ref col);
            _cl.SetVertexBuffer(0, _cubeVertexBuffer);
            _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
            _cl.DrawIndexed(_cubeIndexCount);
        }

        CommandList DrawPicking(ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (pickReady == 0)
            {
                CommandList cl = G.ResourceFactory.CreateCommandList();
                cl.Begin();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                Matrix4x4 viewPrj = viewmat * projMat;

                cl.SetPipeline(_pipelinePick);
                cl.SetFramebuffer(_pickFB);
                cl.ClearColorTarget(0, RgbaFloat.Black);
                cl.ClearDepthStencil(1f);
                cl.SetGraphicsResourceSet(0, _projViewSet);
                cl.SetGraphicsResourceSet(1, _worldTextureSet);


                for (int partIdx = 0; partIdx < scene.PartList.Count; ++partIdx)
                {
                    if (needPickBufferRefresh)
                    {
                        pickReady = -1;
                        return null;
                    }
                    var part = scene.PartList[partIdx];
                    if (part.item == null)
                        continue;
                    if (part.item.ldrLoaderVertexBuffer == null)
                        part.item.LoadLdr(_factory, GraphicsDevice);
                    if (part.item.ldrLoaderVertexBuffer == null)
                        continue;
                    cl.SetVertexBuffer(0, part.item.ldrLoaderVertexBuffer);
                    cl.SetIndexBuffer(part.item.ldrLoaderIndexBuffer, IndexFormat.UInt32);
                    Matrix4x4 cm =
                        part.mat *
                        Matrix4x4.CreateScale(G.WorldScale);
                    cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    int pickIdx = partIdx + 1024;
                    int r = pickIdx & 0xFF;
                    int g = (pickIdx >> 8) & 0xFF;
                    int b = (pickIdx >> 16) & 0xFF;
                    Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                    cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                    cl.DrawIndexed((uint)part.item.ldrLoaderIndexCount);
                }

                if (selectedPart >= 0 && selectedPart < scene.PartList.Count)
                {
                    cl.SetPipeline(_pipelinePickConnectors);
                    var part = scene.PartList[selectedPart];
                    Matrix4x4 cm =
                                part.mat *
                                Matrix4x4.CreateScale(G.WorldScale);
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

                        cl.UpdateBuffer(_worldBuffer, 0, ref cmat2);
                        cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                        cl.SetVertexBuffer(0, _cubeVertexBuffer);
                        cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
                        cl.DrawIndexed(_cubeIndexCount);

                        Matrix4x4 cmat3 =
                            Matrix4x4.CreateTranslation(new Vector3(0, -2, 0)) *
                            Matrix4x4.CreateScale(new Vector3(3, 3, 3)) *
                            Matrix4x4.CreateFromQuaternion(q) *
                            Matrix4x4.CreateTranslation(pos) * cm;
                        cl.UpdateBuffer(_worldBuffer, 0, ref cmat3);

                        cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                        cl.SetVertexBuffer(0, _isoVertexBuffer);
                        cl.SetIndexBuffer(_isoIndexBuffer, IndexFormat.UInt32);
                        cl.DrawIndexed(_isoIndexCount);

                    }
                }

                cl.CopyTexture(this._pickTexture, this._pickStgTexture);
                sw.Stop();
                //script.Api.WriteLine($"pick {sw.Elapsed}");
                cl.End();
                return cl;
            }
            return null;
        }
        struct Rgba
        {
            public uint r;
            public uint g;
        }
        
        void HandlePick(ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (pickReady == 2)
            {
                meshSelectedOffset = -1;                
                MappedResourceView<Rgba> rView = GraphicsDevice.Map<Rgba>(_pickStgTexture, MapMode.Read);
                
                Marshal.Copy(rView.MappedResource.Data, pickBuffer, 0, pickBuffer.Length);
                GraphicsDevice.Unmap(_pickStgTexture);
                viewProjPick = viewmat * projMat;
                Matrix4x4.Invert(viewProjPick, out invViewProjPick);
                pickReady = -1;
            }
        }

        void PickFromBuffer(int x, int y, out Vector3 worldPos, 
            out int partIdx,
            out int connectorIdx)
        {
            int mx = (int)((float)x / (float)Window.Width * 1024.0f);
            int my = (int)((float)y / (float)Window.Height * 1024.0f);

            int pr = pickBuffer[(my * 1024 + mx) * 2];
            int pg = pickBuffer[(my * 1024 + mx) * 2 + 1];

            float xcoord = (mx / 1024.0f) * 2 - 1.0f;
            float ycoord = 1.0f - (my / 1024.0f) * 2;
            float zcoord = (pg / 1000000000.0f);

            if (pg > 0)
            {
                Vector4 wpos = Vector4.Transform(new Vector4(xcoord, ycoord, zcoord, 1), invViewProjPick);
                wpos /= (wpos.W * G.WorldScale);
                worldPos = new Vector3(wpos.X, wpos.Y, wpos.Z);
            }
            else
            {
                worldPos = Vector3.Zero;
            }
            pickIdx = (int)pr;
            selectedConnectorIdx = -1;
            partIdx = -1;
            connectorIdx = -1;
            if (pickIdx >= 1024)
            {
                partIdx = pickIdx - 1024;
            }
            else if (pickIdx >= 128)
            {
                connectorIdx = pickIdx - 128;

            }
        }
    }

}
