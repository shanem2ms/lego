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
using System.Reflection;

namespace partmake
{
    public class PartVis : SampleApplication
    {
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _raycastBuffer;
        private DeviceBuffer _materialBuffer;
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _cubeVertexBuffer;
        private DeviceBuffer _cubeIndexBuffer;
        private DeviceBuffer _planeVertexBuffer;
        private DeviceBuffer _planeIndexBuffer;
        uint _cubeIndexCount;
        uint _planeIndexCount;
        private CommandList _cl;
        private Pipeline _pipeline;
        private Pipeline _pipelineRaycast;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private ResourceSet _raycastSet;
        private int _indexCount;
        Vector2 lookDir;
        int mouseDown = 0;
        Vector2 lMouseDownPt;
        Vector2 rMouseDownPt;
        Vector2 mouseDownLookDir;
        Vector3 partOffset;
        Vector3 cameraPos = new Vector3(0, 0, -8.5f);
        Vector3 mouseDownCameraPos;
        float partScale;
        float zoom = 0;
        float mouseDownZoom = 0;
        public bool ShowBisector { get; set; }
        Vector4[] edgePalette;

        class ConnectorVis
        {
            public LDrawDatFile.ConnectorType type;
            public Matrix4x4 mat;
        }
        List<ConnectorVis> connectorVizs = new List<ConnectorVis>();
        List<Topology.Edge> edges = new List<Topology.Edge>();
        List<Vector3> candidateRStuds = new List<Vector3>();
        List<Tuple<Vector3, Vector3>> bisectors = new List<Tuple<Vector3, Vector3>>();

        LDrawFolders.Entry _part;
        ResourceFactory _factory;
        public LDrawFolders.Entry Part { get => _part; set { _part = value; OnPartUpdated(); } }

        public PartVis(ApplicationWindow window) : base(window)
        {
            edgePalette = new Vector4[100];
            Random r = new Random();
            for (int idx = 0; idx < edgePalette.Length; ++idx)
            {
                edgePalette[idx] = new Vector4((float)r.NextDouble(),
                    (float)r.NextDouble(), (float)r.NextDouble(), 1);
            }
        }

        public void MouseDown(int btn, int X, int Y, System.Windows.Forms.Keys keys)
        {
            mouseDown |= 1 << btn;
            mouseDownLookDir = lookDir;
            mouseDownZoom = zoom;
            rMouseDownPt = lMouseDownPt = new Vector2(X, Y);
            mouseDownCameraPos = cameraPos;
        }
        public void MouseUp(int btn, int X, int Y)
        {
            mouseDown &= ~(1 << btn);
        }
        public void MouseMove(int X, int Y, System.Windows.Forms.Keys keys)
        {
            if ((mouseDown & 1) != 0)
                lookDir = mouseDownLookDir + (new Vector2(X, Y) - lMouseDownPt) * 0.01f;
            if ((mouseDown & 2) != 0)
            {
                if ((keys & System.Windows.Forms.Keys.Shift) != 0)
                {
                    float tscale = 0.01f;
                    cameraPos = mouseDownCameraPos - new Vector3(-X + lMouseDownPt.X, Y - lMouseDownPt.Y, 0) * tscale;
                }
                else
                {
                    zoom = mouseDownZoom + (Y - rMouseDownPt.Y) * 0.005f;
                }
            }
        }

        void OnPartUpdated()
        {
            if (_factory == null)
                return;
            LDrawDatFile part = LDrawFolders.GetPart(_part);
            List<Vtx> vlist = new List<Vtx>();
            part.GetVertices(vlist, false);

            if (vlist.Count == 0)
            {
                return;
            }

            Vtx[] vertices = vlist.ToArray();
            AABB aabb = AABB.CreateFromPoints(vertices.Select(v => v.pos));

            partOffset = (aabb.Min + aabb.Max) * 0.5f;
            Vector3 vecScale = (aabb.Max - aabb.Min);
            partScale = 0.025f;// 1 / MathF.Max(MathF.Max(vecScale.X, vecScale.Y), vecScale.Z);
            uint[] indices = new uint[vlist.Count];
            for (uint i = 0; i < indices.Length; ++i)
            {
                indices[i] = i;
            }

            _vertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices);

            _indexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)indices.Length, BufferUsage.IndexBuffer));
            GraphicsDevice.UpdateBuffer(_indexBuffer, 0, indices);
            _indexCount = indices.Length;

            List<LDrawDatFile.Connector> connectors = part.GetConnectors();
            connectorVizs.Clear();
            foreach (var conn in connectors)
            {
                LDrawDatFile.ConnectorType mask = (LDrawDatFile.ConnectorType.Stud | LDrawDatFile.ConnectorType.RStud);
                if ((conn.type & mask) != 0)
                    connectorVizs.Add(new ConnectorVis() { type = conn.type & mask, mat = conn.mat });
            }

            /*
            this.edges.Clear();
            m.GetBottomEdges(edges);
            this.edges.Sort((a, b) => b.len.CompareTo(a.len));
            this.candidateRStuds.Clear();
            this.bisectors.Clear();
            m.GetRStuds(candidateRStuds, bisectors);*/
        }
        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            _factory = factory;
            _projectionBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _materialBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            _raycastBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));

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
                    new ResourceLayoutElementDescription("RaycastInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

            _projViewSet = factory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer));

            _raycastSet = factory.CreateResourceSet(new ResourceSetDescription(
                raycastLayout,
                _raycastBuffer));

            {
                var vsfile = "partmake.vs.glsl";
                var fsfile = "partmake.fs.glsl";

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
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { projViewLayout, worldTextureLayout },
                    MainSwapchain.Framebuffer.OutputDescription));

            }


            {
                var vsfile = "partmake.vsfullscreen.glsl";
                var fsfile = "partmake.raycast.glsl";

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


                _pipelineRaycast = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { raycastLayout },
                    MainSwapchain.Framebuffer.OutputDescription));

            }
            _cl = factory.CreateCommandList();
            if (this._part != null)
                OnPartUpdated();
        }

        protected override void OnDeviceDestroyed()
        {
            base.OnDeviceDestroyed();
        }

        protected override void Draw(float deltaSeconds)
        {
            if (_vertexBuffer == null)
                return;
            _cl.Begin();

            _cl.UpdateBuffer(_projectionBuffer, 0, Matrix4x4.CreatePerspectiveFieldOfView(
                1.0f,
                (float)Window.Width / Window.Height,
                0.5f,
                100f));

            _cl.UpdateBuffer(_viewBuffer, 0, Matrix4x4.CreateTranslation(cameraPos));

            Matrix4x4 mat =
                Matrix4x4.CreateTranslation(-partOffset) *
                Matrix4x4.CreateScale(partScale * System.MathF.Pow(2.0f, zoom)) *
                Matrix4x4.CreateRotationY(lookDir.X) *
                Matrix4x4.CreateRotationX(lookDir.Y);
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 col = new Vector4(1, 1, 0, 1) * 0.5f;
            _cl.UpdateBuffer(_materialBuffer, 0, ref col);

            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _vertexBuffer);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.DrawIndexed((uint)_indexCount);

            _cl.ClearDepthStencil(1f);
            Dictionary<LDrawDatFile.ConnectorType, Vector4> colors = new Dictionary<LDrawDatFile.ConnectorType, Vector4>()
                { { LDrawDatFile.ConnectorType.Stud, new Vector4(0.8f, 0.1f, 0, 1) }, { LDrawDatFile.ConnectorType.RStud, new Vector4(0.1f, 0.1f, 0.8f, 1) }};
            _cl.SetVertexBuffer(0, _cubeVertexBuffer);
            _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
            foreach (var c in connectorVizs)
            {
                Vector4 ccol = colors[c.type];
                const float lsize = 0.05f;
                _cl.UpdateBuffer(_materialBuffer, 0, ref ccol);
                {
                    Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, 0, -0.5f)) *
                        Matrix4x4.CreateScale(new Vector3(lsize, lsize, 0.5f)) * c.mat * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }
                {
                    Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(-0.5f, 0, 0)) *
                        Matrix4x4.CreateScale(new Vector3(0.5f, lsize, lsize)) * c.mat * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }
                {
                    Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, -0.5f, 0)) *
                        Matrix4x4.CreateScale(new Vector3(lsize, 1.0f, lsize)) * c.mat * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }
            }

            int palidx = 0;
            foreach (var edge in edges)
            {
                _cl.UpdateBuffer(_materialBuffer, 0, ref edgePalette[palidx++]);
                palidx = palidx % 100;
                Vector3 pt0 = edge.v0.pt;
                Vector3 pt1 = edge.v1.pt;
                float len = (pt1 - pt0).Length();
                Vector3 dir = Vector3.Normalize(pt1 - pt0);
                Vector3 a = Vector3.Cross(Vector3.UnitZ, dir);
                float w = 1 + Vector3.Dot(Vector3.UnitZ, dir);
                Quaternion q = a.LengthSquared() != 0 ? new Quaternion(a, w) : Quaternion.Identity;
                q = Quaternion.Normalize(q);
                Vector3 offset = (pt0 + pt1) * 0.5f;
                Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(0.2f, 0.2f, len)) *
                    Matrix4x4.CreateFromQuaternion(q) *
                    Matrix4x4.CreateTranslation(offset);

                Matrix4x4 cm = m * mat;
                _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                _cl.DrawIndexed((uint)_cubeIndexCount);
            }

            if (ShowBisector)
            {
                Vector4 bisectorColor = new Vector4(1.0f, 0f, 0f, 1.0f);
                _cl.UpdateBuffer(_materialBuffer, 0, ref bisectorColor);
                foreach (var edge in bisectors)
                {
                    Vector3 pt0 = edge.Item1;
                    Vector3 pt1 = edge.Item2;
                    float len = (pt1 - pt0).Length();
                    Vector3 dir = Vector3.Normalize(pt1 - pt0);
                    Vector3 a = Vector3.Cross(Vector3.UnitZ, dir);
                    float w = 1 + Vector3.Dot(Vector3.UnitZ, dir);
                    Quaternion q = a.LengthSquared() != 0 ? new Quaternion(a, w) : Quaternion.Identity;
                    q = Quaternion.Normalize(q);
                    Vector3 offset = (pt0 + pt1) * 0.5f;
                    Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(0.2f, 0.2f, len)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(offset);

                    Matrix4x4 cm = m * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }

            }
            Vector4 candidateColor = new Vector4(0f, 0.5f, 1.0f, 1.0f);
            _cl.UpdateBuffer(_materialBuffer, 0, ref candidateColor);
            foreach (var cnd in candidateRStuds)
            {
                Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(1.0f, 1.0f, 1.0f)) *
                    Matrix4x4.CreateTranslation(cnd);
                Matrix4x4 cm = m * mat;
                _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                _cl.DrawIndexed((uint)_cubeIndexCount);
            }

            Vector4 v4 = new Vector4( Window.Width, Window.Height, 0, 0 );
            _cl.UpdateBuffer(_raycastBuffer, 0, ref v4);
            _cl.SetPipeline(_pipelineRaycast);
            _cl.SetGraphicsResourceSet(0, _raycastSet);
            _cl.SetVertexBuffer(0, _planeVertexBuffer);
            _cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
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
