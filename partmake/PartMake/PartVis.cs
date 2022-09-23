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
using partmake.graphics;

namespace partmake
{
    public class PartVis : SampleApplication, IRenderVis
    {
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _raycastBuffer;
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
        private Pipeline _pipelineRaycast;
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

        private Pipeline _pipelinesnapshot;
        private Texture _snapshotStgTexture;
        private Texture _snapshotTexture;
        private TextureView _snapshotTextureView;
        private Framebuffer _snapshotFB;

        private DepthCubeMap _depthCubeMap;
        private CubeMapVisualizer _cubeMapVisualizer;

        public List<string> ThumbnailList = new List<string>();
        public List<string> DepthCubeList = new List<string>();
        public bool DoMesh { get; set; } = true;
        public bool DoDecomp { get; set; } = false;
        public bool BSPPortals { get; set; } = false;
        public bool BSPFaces { get; set; } = false;

        public bool ViewCubeMap { get; set; } = false;
        public bool DoRaycast { get; set; } = false;
        public bool ShowEdges { get; set; } = true;

        public bool ShowBisector { get; set; }
        public bool NonManifold { get; set; } = false;
        public bool ShowConnectors { get; set; } = true;

        public bool ShowExteriorPortals { get; set; } = false;
        public bool ShowLdrLoader { get; set; } = true;
        public bool ShowMbx { get; set; } = false;

        Vector4[] edgePalette;
        uint numPrimitives;

        bool showInwardOrOutwardFaces = true;

        List<Connector> connectorVizs = new List<Connector>();
        List<Topology.Edge> edges = new List<Topology.Edge>();
        List<Topology.Edge> nonMFedges = new List<Topology.Edge>();
        List<Vector3> candidateRStuds = new List<Vector3>();
        List<Tuple<Vector3, Vector3>> bisectors = new List<Tuple<Vector3, Vector3>>();
        List<Primitive> primitives = new List<Primitive>();
        LDrawDatNode selectedNode;
        public Topology.BSPNode SelectedBSPNode
        {
            get => selectedBSPNode;
            set { selectedBSPNode = value; OnBSPNodeUpdated(); }
        }

        Topology.BSPNode selectedBSPNode;

        public Topology.PortalFace SelectedPortalFace
        {
            get => selectedPortalFace;
            set { selectedPortalFace = value; OnBSPNodeUpdated(); }
        }

        Topology.PortalFace selectedPortalFace;

        public LDrawDatNode SelectedNode { get => selectedNode; set { selectedNode = value; OnPartUpdated(); } }

        LDrawDatFile _part;
        ResourceFactory _factory;
        Vector3 _testPrimPos = Vector3.Zero;
        Quaternion _testPrimRot = Quaternion.Identity;
        Vector3 _testPrimScl = new Vector3(0.1f, 0.1f, 0.1f);

        int pickX, pickY;
        int pickReady = -1;
        int meshSelectedOffset = -1;
        public LDrawDatFile Part { get => _part; set {                
                    _part = value; OnPartUpdated();                
            } }

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

        int mode = 0;
        public void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            float move = 0.05f;
            float scl = 1.1f;
            float rot = 0.1f;
            switch (e.Key)
            {
                case System.Windows.Input.Key.D1:
                    mode = 0;
                    break;
                case System.Windows.Input.Key.D2:
                    mode = 1;
                    break;
                case System.Windows.Input.Key.D3:
                    mode = 2;
                    break;
            }
            if (mode == 0)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.D:
                        _testPrimPos.X += move;
                        break;
                    case System.Windows.Input.Key.A:
                        _testPrimPos.X -= move;
                        break;
                    case System.Windows.Input.Key.Q:
                        _testPrimPos.Y += move;
                        break;
                    case System.Windows.Input.Key.Z:
                        _testPrimPos.Y -= move;
                        break;
                    case System.Windows.Input.Key.W:
                        _testPrimPos.Z += move;
                        break;
                    case System.Windows.Input.Key.S:
                        _testPrimPos.Z -= move;
                        break;
                    case System.Windows.Input.Key.E:
                        ShowExteriorPortals = !ShowExteriorPortals;
                        break;
                    case System.Windows.Input.Key.V:
                        if (selectedBSPNode != null && selectedBSPNode.Portal != null)
                            selectedBSPNode.Portal.Visible = !selectedBSPNode.Portal.Visible;
                        LoadPortalsMesh();
                        break;
                    case System.Windows.Input.Key.PageDown:
                        {
                            var portals = _part.GetTopoMesh().bSPTree.GetLeafPortals();
                            foreach (var portal in portals)
                            {
                                portal.Visible = false;
                            }
                            LoadPortalsMesh();
                        }
                        break;
                    case System.Windows.Input.Key.PageUp:
                        {
                            var portals = _part.GetTopoMesh().bSPTree.GetLeafPortals();
                            foreach (var portal in portals)
                            {
                                portal.Visible = true;
                            }
                            LoadPortalsMesh();
                        }
                        break;
                    case System.Windows.Input.Key.Space:
                        if (selectedBSPNode != null && selectedBSPNode.Portal != null)
                        {
                            selectedBSPNode.Portal.SetConnectedInvisible();
                        }
                        LoadPortalsMesh();
                        break;
                    case System.Windows.Input.Key.B:
                        if (selectedBSPNode != null && selectedBSPNode.Portal != null)
                        {
                            selectedBSPNode.Portal.SetExterior();
                        }
                        LoadPortalsMesh();
                        break;
                    case System.Windows.Input.Key.P:
                        onlyShowCoveredPortalFaces = !onlyShowCoveredPortalFaces;
                        LoadPortalsMesh();
                        break;
                    case System.Windows.Input.Key.I:
                        showInwardOrOutwardFaces = !showInwardOrOutwardFaces;
                        LoadPortalsMesh();
                        break;
                    case System.Windows.Input.Key.M:
                        DoMesh = !DoMesh;
                        BSPPortals = !DoMesh;
                        break;
                    case System.Windows.Input.Key.K:
                        {
                            var portals = _part.GetTopoMesh().bSPTree.GetLeafPortals();
                            foreach (var portal in portals)
                            {
                                portal.Visible = false;
                            }
                            _part.GetTopoMesh().bSPTree.TraceToPortal(selectedBSPNode.Portal);
                            LoadPortalsMesh();
                        }
                        break;

                }
            }
            else if (mode == 1)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.D:
                        _testPrimScl.X *= scl;
                        break;
                    case System.Windows.Input.Key.A:
                        _testPrimScl.X /= scl;
                        break;
                    case System.Windows.Input.Key.Q:
                        _testPrimScl.Y *= scl;
                        break;
                    case System.Windows.Input.Key.Z:
                        _testPrimScl.Y /= scl;
                        break;
                    case System.Windows.Input.Key.W:
                        _testPrimScl.Z *= scl;
                        break;
                    case System.Windows.Input.Key.S:
                        _testPrimScl.Z /= scl;
                        break;
                }
            }
            else if (mode == 2)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.D:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(rot, 0, 0);
                        break;
                    case System.Windows.Input.Key.A:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(-rot, 0, 0);
                        break;
                    case System.Windows.Input.Key.Q:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(0, rot, 0);
                        break;
                    case System.Windows.Input.Key.Z:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(0, -rot, 0);
                        break;
                    case System.Windows.Input.Key.W:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(0, 0, rot);
                        break;
                    case System.Windows.Input.Key.S:
                        _testPrimRot *= Quaternion.CreateFromYawPitchRoll(0, 0, -rot);
                        break;
                }
            }
            UpdatePrimForTest();
        }

        public void OnKeyUp(System.Windows.Input.KeyEventArgs e)
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
            if (!mouseMoved)
            {
                pickX = (int)((float)X / (float)Window.Width * 1024.0f);
                pickY = (int)((float)Y / (float)Window.Height * 1024.0f);
                pickReady = 0;
            }
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
                    float tscale = 0.01f;
                    cameraPos = mouseDownCameraPos - new Vector3(-X + lMouseDownPt.X, Y - lMouseDownPt.Y, 0) * tscale;
                }
                else
                {
                    zoom = mouseDownZoom + (Y - rMouseDownPt.Y) * 0.005f;
                }
            }
        }
        struct Rgba128
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        struct Rgba
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        void AddBboxTransformed(List<Vector3> vec, Matrix4x4 m)
        {
            vec.Add(Vector3.Transform(new Vector3(-1, -1, -1), m));
            vec.Add(Vector3.Transform(new Vector3(-1, -1, 1), m));
            vec.Add(Vector3.Transform(new Vector3(-1, 1, -1), m));
            vec.Add(Vector3.Transform(new Vector3(-1, 1, 1), m));
            vec.Add(Vector3.Transform(new Vector3(1, -1, -1), m));
            vec.Add(Vector3.Transform(new Vector3(1, -1, 1), m));
            vec.Add(Vector3.Transform(new Vector3(1, 1, -1), m));
            vec.Add(Vector3.Transform(new Vector3(1, 1, 1), m));
        }

        System.DoubleNumerics.Vector3 FTD(Vector3 d)
        {
            return new System.DoubleNumerics.Vector3((double)d.X, (double)d.Y, (double)d.Z);
        }

        Vector3 DTF(System.DoubleNumerics.Vector3 d)
        {
            return new Vector3((float)d.X, (float)d.Y, (float)d.Z);
        }

        
        [DllImport("kernel32.dll", EntryPoint = "RtlCopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        void OnPartUpdated()
        {
            if (_factory == null)
                return;

            meshSelectedOffset = -1;
            List<Vtx> vlist = new List<Vtx>();
            faceIndices = new List<int>();
            bool doCandidatePlanes = false;
            if (doCandidatePlanes)
            {
                Dictionary<int, Topology.Plane> planes =
                    Topology.ConnectorUtils.GetCandidatePlanes(_part.GetTopoMesh());
                _part.GetTopoMesh().GetVertices(vlist, faceIndices, true,
                    (Topology.Face f) =>
                {
                    return (planes.ContainsKey(f.Plane.idx));
                });
            }
            else
            {
                _part.GetTopoMesh().GetVertices(vlist, faceIndices, true, null);
            }
            if (vlist.Count == 0)
            {
                return;
            }

            Vtx[] vertices = vlist.ToArray();
            AABB aabb = AABB.CreateFromPoints(vertices.Select(v =>
                new System.DoubleNumerics.Vector3(v.pos.X, v.pos.Y, v.pos.Z)));



            partOffset = DTF((aabb.Min + aabb.Max) * 0.5);
            Vector3 vecScale = DTF(aabb.Max - aabb.Min);
            
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

            _triangleCount = _indexCount / 3;

            _decompMeshes = _part.GetTopoMesh().convexDecomp;
            _decompIndexCounts = null;
            _decompVertexBuffer = null;
            _decompIndexBuffer = null;
            if (_decompMeshes.Count > 0)
            {
                List<Vtx> dcmpPts = new List<Vtx>();
                _decompIndexCounts = new List<Tuple<uint, uint>>();
                foreach (var conv in _decompMeshes)
                {
                    uint startIdx = (uint)dcmpPts.Count;
                    dcmpPts.AddRange(conv.points.Select(v => new Vtx(v, new System.DoubleNumerics.Vector2(0, 0))));
                    _decompIndexCounts.Add(new Tuple<uint, uint>(startIdx,
                        (uint)dcmpPts.Count - startIdx));
                }
                _decompVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * dcmpPts.Count), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(_decompVertexBuffer, 0, dcmpPts.ToArray());

                uint[] decompIndices = new uint[dcmpPts.Count];
                for (uint i = 0; i < decompIndices.Length; i++)
                {
                    decompIndices[i] = i;
                }
                _decompIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)decompIndices.Length, BufferUsage.IndexBuffer));
                GraphicsDevice.UpdateBuffer(_decompIndexBuffer, 0, decompIndices);
            }

            List<Vtx> vlistSel = new List<Vtx>();

            _part.GetVertices(vlistSel, true, true);
            if (vlistSel.Count > 0)
            {
                Vtx[] verticesSel = vlistSel.ToArray();
                uint[] indicesSel = new uint[vlistSel.Count];
                for (uint i = 0; i < indicesSel.Length; ++i)
                {
                    indicesSel[i] = i;
                }

                _selectedVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * verticesSel.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(_selectedVertexBuffer, 0, verticesSel);

                _selectedIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)indicesSel.Length, BufferUsage.IndexBuffer));
                GraphicsDevice.UpdateBuffer(_selectedIndexBuffer, 0, indicesSel);
                _selectedIndexCount = indicesSel.Length;
            }

            List<Tuple<System.DoubleNumerics.Vector3, System.DoubleNumerics.Vector3>> bs =
                _part.Connectors.Bisectors;
            List<Connector> connectors = _part.Connectors.Items.ToList();
            this.bisectors = bs.Select(tp => new Tuple<Vector3, Vector3>(
                new Vector3((float)tp.Item1.X, (float)tp.Item1.Y, (float)tp.Item1.Z),
                new Vector3((float)tp.Item2.X, (float)tp.Item2.Y, (float)tp.Item2.Z))).ToList();
            connectorVizs = connectors;

            this.edges.Clear();
            _part.GetTopoMesh().GetEdges(this.edges);
            this.nonMFedges.Clear();
            _part.GetTopoMesh().GetNonManifold(this.nonMFedges);
            this.edges.Sort((a, b) => b.len.CompareTo(a.len));
            this.nonMFedges.Sort((a, b) => b.len.CompareTo(a.len));

            this.primitives.Clear();
            List<Primitive> outPrims = new List<Primitive>(); ;
            _part.GetPrimitives(outPrims);
            this.primitives = outPrims;// outPrims.Where(p => !p.inverted).ToList();
            Matrix4x4 wm =
                Matrix4x4.CreateScale(partScale);

            MappedResourceView<Rgba128> rView = GraphicsDevice.Map<Rgba128>(_primStgTexture, MapMode.Write);
            int matsize = Marshal.SizeOf<Matrix4x4>();
            IntPtr ptr = Marshal.AllocHGlobal(matsize * primitives.Count);
            IntPtr curptr = ptr;
            List<Vector3> primpts = new List<Vector3>();

            for (int p = 0; p < primitives.Count; ++p)
            {
                Matrix4x4 t;
                Matrix4x4 wwt = primitives[p].transform.ToM44() * wm;

                Vector3 opt0 = new Vector3(0, 0, 0), opt1 = new Vector3(1, 1, 1);
                Vector3 pt0 = Vector3.Transform(opt0, wwt);
                Vector3 pt1 = Vector3.Transform(opt1, wwt);
                float scale0 = (pt1 - pt0).Length() / (opt1 - opt0).Length();
                AddBboxTransformed(primpts, wwt);
                Matrix4x4.Invert(wwt, out t);
                t.M34 = primitives[p].inverted ? -1 : 1;
                t.M44 = scale0;
                Marshal.StructureToPtr<Matrix4x4>(t, curptr, false);
                curptr = IntPtr.Add(curptr, matsize);
            }

            this.primBbox = AABB.CreateFromPoints(primpts.Select(v => FTD(v)));
            this.primBbox.Grow(0.05f);
            CopyMemory(rView.MappedResource.Data, ptr, (uint)(matsize * primitives.Count));
            Marshal.FreeHGlobal(ptr);
            GraphicsDevice.Unmap(_primStgTexture);
            this.numPrimitives = (uint)primitives.Count;
            this._primTexUpdate = true;

            LoadPortalsMesh();

            if (ShowLdrLoader)
            {
                LdrLoader.PosTexcoordNrmVertex[] ldrvertices;
                int[] ldrindices;
                _part.GetLDrLoaderMesh(out ldrvertices, out ldrindices);

                if (ldrvertices.Length > 0)
                {
                    Vtx[] vlvertices = ldrvertices.Select(v => new Vtx(new Vector3(v.m_x, v.m_y, v.m_z), new Vector3(v.m_nx, v.m_ny, v.m_nz),
                        new Vector2(v.m_u, v.m_v))).ToArray();
                    uint[] vlindices = ldrindices.Select(i => (uint)i).ToArray();

                    _ldrLoaderVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vlvertices.Length), BufferUsage.VertexBuffer));
                    GraphicsDevice.UpdateBuffer(_ldrLoaderVertexBuffer, 0, vlvertices);

                    _ldrLoaderIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)vlindices.Length, BufferUsage.IndexBuffer));
                    GraphicsDevice.UpdateBuffer(_ldrLoaderIndexBuffer, 0, vlindices);
                    _ldrLoaderIndexCount = vlindices.Length;

                    _depthCubeMap = new DepthCubeMap(partOffset, vecScale, _ldrLoaderVertexBuffer, _ldrLoaderIndexBuffer,
                        (uint)_ldrLoaderIndexCount);

                    _cubeMapVisualizer = new CubeMapVisualizer(_depthCubeMap);
                }
            }

            if (ShowMbx)
            {
                MbxImport.Mesh mesh = _part.LoadMbx();
                if (mesh != null)
                {
                    Vtx[] vlvertices = mesh.vertices.Select(v => new Vtx(new Vector3(v.X, v.Y, v.Z) * 2.5f, new Vector3(1, 0, 0),
      new Vector2(0, 0))).ToArray();
                    uint[] vlindices = mesh.indices.Select(i => (uint)i).ToArray();
                    _mbxVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vlvertices.Length), BufferUsage.VertexBuffer));
                    GraphicsDevice.UpdateBuffer(_mbxVertexBuffer, 0, vlvertices);

                    _mbxIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)vlindices.Length, BufferUsage.IndexBuffer));
                    GraphicsDevice.UpdateBuffer(_mbxIndexBuffer, 0, vlindices);
                    _mbxIndexCount = vlindices.Length;
                }
                else
                    _mbxIndexCount = 0;
            }
            string logstr = Topology.PolygonClip.GetLog();
            OnLogUpdated?.Invoke(this, logstr);

        }

        void LoadPortalsMesh()
        {
            if (_part.GetTopoMesh().bSPTree == null)
                return;
            {
                _bspPortals =
                     _part.GetTopoMesh().bSPTree.GetLeafPortals();
                List<Vtx> vlist = new List<Vtx>();
                _bspPortalsIndexCounts = new List<Tuple<uint, uint>>();
                foreach (var portal in _bspPortals)
                {
                    uint startIdx = (uint)vlist.Count;
                    foreach (var face in portal.Faces)
                    {
                        if (onlyShowCoveredPortalFaces && !face.IsCovered)
                            continue;
                        var triangles = Topology.Face.GetTriangles(face.points);
                        foreach (var tri in triangles)
                        {
                            vlist.AddRange(
                            tri.Select(v =>
                                new Vtx(v, face.Normal, new System.DoubleNumerics.Vector2(0, 0))));
                        }
                    }
                    _bspPortalsIndexCounts.Add(new Tuple<uint, uint>(startIdx,
                        (uint)vlist.Count - startIdx));
                }

                if (vlist.Count > 0)
                {
                    Vtx[] vertices = vlist.ToArray();
                    uint[] indices = new uint[vlist.Count];
                    for (uint i = 0; i < indices.Length; i++)
                    {
                        indices[i] = i;
                    }
                    _bspPortalsVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
                    GraphicsDevice.UpdateBuffer(_bspPortalsVertexBuffer, 0, vertices);

                    _bspPortalsIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)indices.Length, BufferUsage.IndexBuffer));
                    GraphicsDevice.UpdateBuffer(_bspPortalsIndexBuffer, 0, indices);
                }
            }
            {
                _bspPortalFaces = new List<Topology.PortalFace>();
                _bspFacesIndexCounts = new List<Tuple<uint, uint>>();
                List<Vtx> vlist = new List<Vtx>();
                foreach (var portal in _bspPortals)
                {
                    foreach (var face in portal.Faces)
                    {
                        if (face.isFinal)
                        {
                            uint startIdx = (uint)vlist.Count;
                            var triangles = Topology.Face.GetTriangles(face.points);
                            foreach (var tri in triangles)
                            {
                                vlist.AddRange(
                                tri.Select(v =>
                                    new Vtx(v, face.Normal, new System.DoubleNumerics.Vector2(0, 0))));
                            }
                            _bspPortalFaces.Add(face);
                            _bspFacesIndexCounts.Add(new Tuple<uint, uint>(startIdx,
                                (uint)vlist.Count - startIdx));
                        }
                    }

                }
                if (vlist.Count > 0)
                {
                    Vtx[] vertices = vlist.ToArray();
                    uint[] indices = new uint[vlist.Count];
                    for (uint i = 0; i < indices.Length; i++)
                    {
                        indices[i] = i;
                    }
                    _bspFacesVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
                    GraphicsDevice.UpdateBuffer(_bspFacesVertexBuffer, 0, vertices);

                    _bspFacesIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)indices.Length, BufferUsage.IndexBuffer));
                    GraphicsDevice.UpdateBuffer(_bspFacesIndexBuffer, 0, indices);
                }
            }
        }
        void OnBSPNodeUpdated()
        {
            _bspSelVertexBuffer = null;
            _bspSelIndexBuffer = null;
            _bspSelIndexCount = 0;
            if (selectedBSPNode == null)
                return;
            List<Vtx> vlist = new List<Vtx>();
            foreach (var face in selectedBSPNode.Portal.Faces)
            {
                var tris = Topology.Face.GetTriangles(face.points);
                foreach (var tri in tris)
                {
                    vlist.AddRange(
                        tri.Select(v => new Vtx(v, face.Normal, new System.DoubleNumerics.Vector2(0, 0))));
                }
            }

            Vtx[] vertices = vlist.ToArray();
            uint[] indices = new uint[vlist.Count];
            for (uint i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }
            _bspSelVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_bspSelVertexBuffer, 0, vertices);

            _bspSelIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)indices.Length, BufferUsage.IndexBuffer));
            GraphicsDevice.UpdateBuffer(_bspSelIndexBuffer, 0, indices);
            _bspSelIndexCount = (uint)indices.Length;
        }
        void UpdatePrimForTest()
        {
            int pcount = 1;
            Matrix4x4 wm = Matrix4x4.CreateScale(_testPrimScl) *
                Matrix4x4.CreateFromQuaternion(_testPrimRot) *
                Matrix4x4.CreateTranslation(_testPrimPos);

            MappedResourceView<Rgba128> rView = GraphicsDevice.Map<Rgba128>(_primStgTexture, MapMode.Write);
            int matsize = Marshal.SizeOf<Matrix4x4>();
            IntPtr ptr = Marshal.AllocHGlobal(matsize * pcount);
            IntPtr curptr = ptr;
            for (int p = 0; p < pcount; ++p)
            {
                Matrix4x4 t;
                Matrix4x4 wwt = wm;
                float scale = new Vector3(wwt.M11, wwt.M22, wwt.M33).Length();
                Matrix4x4.Invert(wwt, out t);
                Marshal.StructureToPtr<Matrix4x4>(t, curptr, false);
                curptr = IntPtr.Add(curptr, matsize);
            }
            CopyMemory(rView.MappedResource.Data, ptr, (uint)(matsize * pcount));
            Marshal.FreeHGlobal(ptr);
            GraphicsDevice.Unmap(_primStgTexture);
            this.numPrimitives = (uint)pcount;
            this._primTexUpdate = true;
        }
        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            graphics.G.ResourceFactory = factory;
            G.Swapchain = MainSwapchain;
            G.GraphicsDevice = GraphicsDevice;
            _factory = factory;
            _projectionBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _materialBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            _raycastBuffer = factory.CreateBuffer(new BufferDescription(128, BufferUsage.UniformBuffer));
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

            _raycastSet = factory.CreateResourceSet(new ResourceSetDescription(
                raycastLayout,
                _raycastBuffer,
                _primTextureView,
                _primSampler));

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
                    BlendStateDescription.SingleAlphaBlend,
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

                var vertex = new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main");
                var fragment = new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main");
                var shader = factory.CreateFromSpirv(
                        vertex,
                        fragment);
                ShaderSetDescription shaderSet = new ShaderSetDescription(
                    new[]
                    {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                    }, shader);


                _pipelineRaycast = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { raycastLayout },
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

                uint snapSize = 64;

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

                _snapshotTexture = factory.CreateTexture(TextureDescription.Texture2D(
                              snapSize, snapSize, 1, 1,
                               PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled));
                _snapshotStgTexture = factory.CreateTexture(TextureDescription.Texture2D(
                    snapSize,
                    snapSize,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Staging));

                _snapshotTextureView = factory.CreateTextureView(_snapshotTexture);
                Texture offscreenDepth = factory.CreateTexture(TextureDescription.Texture2D(
                    snapSize, snapSize, 1, 1, PixelFormat.R16_UNorm, TextureUsage.DepthStencil));
                _snapshotFB = factory.CreateFramebuffer(new FramebufferDescription(offscreenDepth, _snapshotTexture));


                _pipelinesnapshot = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                    PrimitiveTopology.TriangleList,
                    shaderSet,
                    new[] { projViewLayout, worldTextureLayout },
                    _snapshotFB.OutputDescription));

            }
            _cl = factory.CreateCommandList();
            if (this._part != null && this._part.IsInitialized)
                OnPartUpdated();
        }

        protected override void OnDeviceDestroyed()
        {
            base.OnDeviceDestroyed();
        }
        static Matrix4x4 MatrixForLine(Vector3 pt0, Vector3 pt1, ref Matrix4x4 mat, ref Matrix4x4 viewPrj)
        {
            float len = (pt1 - pt0).Length();
            Vector3 dir = Vector3.Normalize(pt1 - pt0);
            Vector3 a = Vector3.Cross(Vector3.UnitZ, dir);
            float w = 1 + Vector3.Dot(Vector3.UnitZ, dir);
            Quaternion q = a.LengthSquared() != 0 ? new Quaternion(a, w) : Quaternion.Identity;
            q = Quaternion.Normalize(q);
            Vector3 offset = (pt0 + pt1) * 0.5f;
            Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(1, 1, len)) *
                Matrix4x4.CreateFromQuaternion(q) *
                Matrix4x4.CreateTranslation(offset);
            Matrix4x4 cm = m * mat;
            Matrix4x4 matViewProj = cm * viewPrj;
            Vector4 tp1 = Vector4.Transform(new Vector4(-0.5f, -0.5f, 0, 1), matViewProj);
            Vector4 tp2 = Vector4.Transform(new Vector4(0.5f, 0.5f, 0, 1), matViewProj);
            tp1 /= tp1.W;
            tp2 /= tp2.W;
            float lineLen = (tp1 - tp2).Length();

            float s = 0.002f;
            m = Matrix4x4.CreateScale(new Vector3(s / lineLen, s / lineLen, len)) *
                Matrix4x4.CreateFromQuaternion(q) *
                Matrix4x4.CreateTranslation(offset);
            cm = m * mat;
            return cm;
        }

        void DrawThumbnail()
        {
            string path = ThumbnailList[0];
            ThumbnailList.RemoveAt(0);

            LdrLoader.PosTexcoordNrmVertex[] ldrvertices;
            int[] ldrindices;
            LdrLoader ldrLoader = new LdrLoader();
            ldrLoader.LoadCached(path, out ldrvertices, out ldrindices);
            if (ldrvertices.Length > 0 && ldrindices.Length > 0)
            {
                Vector3 vmax = new Vector3(
                    ldrvertices.Select(v => v.m_x).Max(),
                    ldrvertices.Select(v => v.m_y).Max(),
                    ldrvertices.Select(v => v.m_z).Max());

                Vector3 vmin = new Vector3(
                    ldrvertices.Select(v => v.m_x).Min(),
                    ldrvertices.Select(v => v.m_y).Min(),
                    ldrvertices.Select(v => v.m_z).Min());

                Vector3 vecscale = vmax - vmin;
                partScale = 1.0f / Math.Max(Math.Max(vecscale.X, vecscale.Y), vecscale.Z);
                Vtx[] vlvertices = ldrvertices.Select(v => new Vtx(new Vector3(v.m_x, v.m_y, v.m_z), new Vector3(v.m_nx, v.m_ny, v.m_nz),
                    new Vector2(v.m_u, v.m_v))).ToArray();

                Vector3 vmid = (vmax + vmin) * 0.5f;
                partOffset = vmid;

                uint[] vlindices = ldrindices.Select(i => (uint)i).ToArray();

                DeviceBuffer ldrLoaderVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vlvertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderVertexBuffer, 0, vlvertices);

                DeviceBuffer ldrLoaderIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)vlindices.Length, BufferUsage.IndexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderIndexBuffer, 0, vlindices);
                int ldrLoaderIndexCount = vlindices.Length;

                _cl.Begin();

                _cl.SetPipeline(_pipelinesnapshot);
                _cl.SetFramebuffer(_snapshotFB);

                Matrix4x4 projMat = Matrix4x4.CreatePerspectiveFieldOfView(
                                1.0f,
                                (float)Window.Width / Window.Height,
                                0.05f,
                                20f);
                _cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);

                Matrix4x4 viewmat = Matrix4x4.CreateRotationY(-0.75f) *
                    Matrix4x4.CreateRotationX(0.5f) *
                    Matrix4x4.CreateTranslation(cameraPos / System.MathF.Pow(2.0f, zoom));
                _cl.UpdateBuffer(_viewBuffer, 0, viewmat);

                Matrix4x4 mat =
                    Matrix4x4.CreateTranslation(-partOffset) *
                    Matrix4x4.CreateScale(partScale);
                _cl.ClearColorTarget(0, RgbaFloat.Black);
                _cl.ClearDepthStencil(1f);

                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                Vector4 col = new Vector4(1, 1, 1, 1);
                _cl.UpdateBuffer(_materialBuffer, 0, ref col);


                _cl.SetVertexBuffer(0, ldrLoaderVertexBuffer);
                _cl.SetIndexBuffer(ldrLoaderIndexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);
                _cl.DrawIndexed((uint)ldrLoaderIndexCount);

                _cl.CopyTexture(this._snapshotTexture, this._snapshotStgTexture);
                _cl.End();
                GraphicsDevice.SubmitCommands(_cl);
                GraphicsDevice.SwapBuffers(MainSwapchain);
                GraphicsDevice.WaitForIdle();
                string pngpath = Path.Combine(Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path) + ".png");
                HandleSnapshot(pngpath);
            }
        }
        void DrawDepthCube()
        {
            string path = DepthCubeList[0];
            DepthCubeList.RemoveAt(0);

            LdrLoader.PosTexcoordNrmVertex[] ldrvertices;
            int[] ldrindices;
            LdrLoader ldrLoader = new LdrLoader();
            ldrLoader.LoadCached(path, out ldrvertices, out ldrindices);
            if (ldrvertices.Length > 0 && ldrindices.Length > 0)
            {
                var vertices = ldrvertices.Where(v => 
                    !float.IsNaN(v.m_x) &&
                    !float.IsNaN(v.m_y) &&
                    !float.IsNaN(v.m_z));

                Vector3 vmax = new Vector3(
                    vertices.Select(v => v.m_x).Max(),
                    vertices.Select(v => v.m_y).Max(),
                    vertices.Select(v => v.m_z).Max());

                Vector3 vmin = new Vector3(
                    vertices.Select(v => v.m_x).Min(),
                    vertices.Select(v => v.m_y).Min(),
                    vertices.Select(v => v.m_z).Min());

                Vector3 vecscale = vmax - vmin;
                partScale = 1.0f / Math.Max(Math.Max(vecscale.X, vecscale.Y), vecscale.Z);
                Vtx[] vlvertices = ldrvertices.Select(v => new Vtx(new Vector3(v.m_x, v.m_y, v.m_z), new Vector3(v.m_nx, v.m_ny, v.m_nz),
                    new Vector2(v.m_u, v.m_v))).ToArray();

                Vector3 vmid = (vmax + vmin) * 0.5f;
                partOffset = vmid;

                uint[] vlindices = ldrindices.Select(i => (uint)i).ToArray();

                DeviceBuffer ldrLoaderVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vlvertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderVertexBuffer, 0, vlvertices);

                DeviceBuffer ldrLoaderIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)vlindices.Length, BufferUsage.IndexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderIndexBuffer, 0, vlindices);
                int ldrLoaderIndexCount = vlindices.Length;

                DepthCubeMap depthCubeMap = new DepthCubeMap(partOffset, vecscale, _ldrLoaderVertexBuffer, _ldrLoaderIndexBuffer,
                    (uint)_ldrLoaderIndexCount);

                _cl.Begin();
                depthCubeMap.DrawToFile(_cl);
                _cl.End();
                GraphicsDevice.SubmitCommands(_cl);
                GraphicsDevice.SwapBuffers(MainSwapchain);
                GraphicsDevice.WaitForIdle();
                string depthfilepath = Path.Combine(Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path) + ".zcube");
                depthCubeMap.SaveToFile(depthfilepath);
            }
        }

        protected override void Draw(float deltaSeconds)
        {

            if (ThumbnailList.Count > 0)
            {
                DrawThumbnail();
                return;
            }
            else if (DepthCubeList.Count > 0)
            {
                DrawDepthCube();
                return;
            }

            if (_vertexBuffer == null)
                return;
            _cl.Begin();

            _depthCubeMap.DrawOffscreen(_cl);

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
            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            if (ViewCubeMap)
            {
                _cubeMapVisualizer.DrawMain(_cl);
            }
            else if (DoRaycast)
                DrawRaycast(ref viewmat);
            else
            {
                DrawMesh(ref mat);
                if (ShowLdrLoader && _ldrLoaderVertexBuffer != null)
                    DrawLdrLoaderMesh(ref mat);
                DrawMbxMesh(ref mat);
                DrawDecomp(ref mat, ref viewmat, ref projMat);

                DrawBSPPortals(ref mat);
                DrawBSPFaces(ref mat);

                DrawEdges(ref mat, ref viewmat, ref projMat);

                DrawNonManifold(ref mat, ref viewmat, ref projMat);

                if (selectedBSPNode != null)
                    DrawSelectedBSP(ref mat, ref viewmat, ref projMat);

                DrawBisectors(ref mat);
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
            }
            if (BSPPortals)
                DrawPickingBSP(ref mat, ref viewmat, ref projMat);
            else
                DrawPicking(ref mat, ref viewmat, ref projMat);
            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();

            HandlePick();
        }

        void DrawSnapshot(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            Matrix4x4 viewPrj = viewmat * projMat;

            _cl.SetPipeline(_pipelinesnapshot);
            _cl.SetFramebuffer(_snapshotFB);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            Matrix4x4 wmat =
                Matrix4x4.CreateTranslation(-partOffset) *
                Matrix4x4.CreateScale(partScale);
            _cl.UpdateBuffer(_worldBuffer, 0, ref wmat);
            _cl.SetVertexBuffer(0, _vertexBuffer);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);

            _cl.DrawIndexed((uint)_indexCount);

            _cl.CopyTexture(this._snapshotTexture, this._snapshotStgTexture);

        }

        void DrawPicking(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
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
                Matrix4x4 wmat =
                    Matrix4x4.CreateTranslation(-partOffset) *
                    Matrix4x4.CreateScale(partScale);
                _cl.UpdateBuffer(_worldBuffer, 0, ref wmat);
                _cl.SetVertexBuffer(0, _vertexBuffer);
                _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);

                for (int i = 0; i < _triangleCount; i++)
                {
                    int fidx = i;
                    int pidx = fidx + 1;
                    int r = pidx & 0xFF;
                    int g = (pidx >> 8) & 0xFF;
                    int b = (pidx >> 16) & 0xFF;
                    Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                    _cl.DrawIndexed(3, 1, (uint)(i * 3), 0, 0);
                }
                int pickIdx = _triangleCount + 1;
                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
                foreach (var edge in this.edges)
                {
                    int r = pickIdx & 0xFF;
                    int g = (pickIdx >> 8) & 0xFF;
                    int b = (pickIdx >> 16) & 0xFF;
                    Vector4 edgeColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref edgeColor);
                    Vector3 pt0 = DTF(edge.v0.pt);
                    Vector3 pt1 = DTF(edge.v1.pt);
                    float len = (pt1 - pt0).Length();
                    Vector3 dir = Vector3.Normalize(pt1 - pt0);
                    Vector3 a = Vector3.Cross(Vector3.UnitZ, dir);
                    float w = 1 + Vector3.Dot(Vector3.UnitZ, dir);
                    Quaternion q = a.LengthSquared() != 0 ? new Quaternion(a, w) : Quaternion.Identity;
                    q = Quaternion.Normalize(q);
                    Vector3 offset = (pt0 + pt1) * 0.5f;
                    Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(1, 1, len)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(offset);
                    Matrix4x4 cm = m * mat;
                    Matrix4x4 matViewProj = cm * viewPrj;
                    Vector4 tp1 = Vector4.Transform(new Vector4(-0.5f, -0.5f, 0, 1), matViewProj);
                    Vector4 tp2 = Vector4.Transform(new Vector4(0.5f, 0.5f, 0, 1), matViewProj);
                    tp1 /= tp1.W;
                    tp2 /= tp2.W;
                    float lineLen = (tp1 - tp2).Length();

                    float s = 0.005f;
                    m = Matrix4x4.CreateScale(new Vector3(s / lineLen, s / lineLen, len)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(offset);
                    cm = m * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                    pickIdx++;
                }
                _cl.CopyTexture(this._pickTexture, this._pickStgTexture);
                pickReady = 1;
            }

        }
        void DrawPickingBSP(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
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
                Matrix4x4 wmat =
                    Matrix4x4.CreateTranslation(-partOffset) *
                    Matrix4x4.CreateScale(partScale);
                _cl.UpdateBuffer(_worldBuffer, 0, ref wmat);
                _cl.SetVertexBuffer(0, _bspPortalsVertexBuffer);
                _cl.SetIndexBuffer(_bspPortalsIndexBuffer, IndexFormat.UInt32);

                for (int i = 0; i < _bspPortalsIndexCounts.Count; i++)
                {
                    var portal = _bspPortals[i];
                    if (portal.Visible && (ShowExteriorPortals || !portal.IsExterior))
                    {
                        int pidx = i + 1;
                        int r = pidx & 0xFF;
                        int g = (pidx >> 8) & 0xFF;
                        int b = (pidx >> 16) & 0xFF;
                        Vector4 meshColor = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1);
                        var offsets = _bspPortalsIndexCounts[i];

                        _cl.UpdateBuffer(_materialBuffer, 0, ref meshColor);
                        _cl.DrawIndexed(offsets.Item2, 1, offsets.Item1, 0, 0);
                    }
                }

                _cl.CopyTexture(this._pickTexture, this._pickStgTexture);
                pickReady = 1;
            }

        }

        public void SelectFace(Topology.Face f)
        {
            int fIdx = _part.GetTopoMesh().faces.IndexOf(f);
            meshSelectedOffset = faceIndices.IndexOf(fIdx);
        }

        void HandleSnapshot(string outpath)
        {
            meshSelectedOffset = -1;
            MappedResourceView<Rgba> rView = GraphicsDevice.Map<Rgba>(_snapshotStgTexture, MapMode.Read);
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap((int)_snapshotStgTexture.Width,
                (int)_snapshotStgTexture.Height, (int)rView.MappedResource.RowPitch, System.Drawing.Imaging.PixelFormat.Format32bppArgb,
                rView.MappedResource.Data);
            GraphicsDevice.Unmap(_snapshotStgTexture);
            bitmap.Save(outpath);
        }

        void HandlePick()
        {
            if (pickReady == 1)
            {
                meshSelectedOffset = -1;
                MappedResourceView<Rgba> rView = GraphicsDevice.Map<Rgba>(_pickStgTexture, MapMode.Read);
                Rgba r = rView[pickX, pickY];
                int pickIdx = r.r + (r.g << 8) + (r.b << 16);
                GraphicsDevice.Unmap(_pickStgTexture);
                if (BSPPortals)
                {
                    if (pickIdx > 0)
                        OnBSPNodeSelected?.Invoke(this, _bspPortals[pickIdx - 1].parentNode);
                }
                else
                {
                    if (pickIdx > _triangleCount)
                    {
                        Topology.Edge e = this.edges[pickIdx - (_triangleCount + 1)];
                        OnINodeSelected?.Invoke(this, e);
                    }
                    else if (pickIdx > 0)
                    {
                        var face = _part.GetTopoMesh().faces[faceIndices[pickIdx - 1]];
                        OnINodeSelected?.Invoke(this, face);
                        meshSelectedOffset = pickIdx - 1;
                    }
                }
                pickReady = -1;
            }
        }
        void DrawNonManifold(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (NonManifold)
            {            //_cl.ClearDepthStencil(1f);
                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                Matrix4x4 viewPrj = viewmat * projMat;
                _cl.ClearDepthStencil(1f);

                int palidx = 0;
                foreach (var edge in this.nonMFedges)
                {
                    _cl.UpdateBuffer(_materialBuffer, 0, ref edgePalette[palidx++]);
                    palidx = palidx % 100;
                    Vector3 pt0 = DTF(edge.v0.pt);
                    Vector3 pt1 = DTF(edge.v1.pt);
                    float len = (pt1 - pt0).Length();
                    Vector3 dir = Vector3.Normalize(pt1 - pt0);
                    Vector3 a = Vector3.Cross(Vector3.UnitZ, dir);
                    float w = 1 + Vector3.Dot(Vector3.UnitZ, dir);
                    Quaternion q = a.LengthSquared() != 0 ? new Quaternion(a, w) : Quaternion.Identity;
                    q = Quaternion.Normalize(q);
                    Vector3 offset = (pt0 + pt1) * 0.5f;
                    Matrix4x4 m = Matrix4x4.CreateScale(new Vector3(1, 1, len)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(offset);

                    Matrix4x4 cm = m * mat;
                    Matrix4x4 matViewProj = cm * viewPrj;
                    Vector4 tp1 = Vector4.Transform(new Vector4(-0.5f, -0.5f, 0, 1), matViewProj);
                    Vector4 tp2 = Vector4.Transform(new Vector4(0.5f, 0.5f, 0, 1), matViewProj);
                    tp1 /= tp1.W;
                    tp2 /= tp2.W;
                    float lineLen = (tp1 - tp2).Length();

                    float s = 0.002f;
                    m = Matrix4x4.CreateScale(new Vector3(s / lineLen, s / lineLen, len)) *
                        Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(offset);
                    cm = m * mat;
                    _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }
            }

        }
        void DrawEdges(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (this.ShowEdges)
            {
                _cl.SetPipeline(_pipeline);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);

                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                Matrix4x4 viewPrj = viewmat * projMat;

                Vector4 edgeColor = new Vector4(1, 1, 1, 1);
                Vector4 edgeFlag1 = new Vector4(1, 0, 1, 1);
                Vector4 edgeFlag2 = new Vector4(0, 1, 1, 1);
                Vector4 edgeSelected = new Vector4(1, 0, 0, 1);
                bool showErrors = true;
                foreach (var edge in this.edges)
                {
                    if (edge.IsSelected)
                        _cl.UpdateBuffer(_materialBuffer, 0, ref edgeSelected);
                    else if (showErrors && (edge.errorFlags & 2) != 0)
                        _cl.UpdateBuffer(_materialBuffer, 0, ref edgeFlag2);
                    else if (showErrors && ((edge.v0.errorFlags & 1) != 0 ||
                        (edge.v1.errorFlags & 1) != 0))
                        _cl.UpdateBuffer(_materialBuffer, 0, ref edgeFlag1);
                    else
                        _cl.UpdateBuffer(_materialBuffer, 0, ref edgeColor);
                    Vector3 pt0 = DTF(edge.v0.pt);
                    Vector3 pt1 = DTF(edge.v1.pt);

                    Matrix4x4 wm = MatrixForLine(pt0, pt1, ref mat, ref viewPrj);

                    _cl.UpdateBuffer(_worldBuffer, 0, ref wm);
                    _cl.DrawIndexed((uint)_cubeIndexCount);
                }
            }
        }

        void DrawMesh(ref Matrix4x4 mat)
        {
            if (DoMesh)
            {
                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                Vector4 col = new Vector4(1, 1, 0, 1);
                _cl.UpdateBuffer(_materialBuffer, 0, ref col);

                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _vertexBuffer);
                _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);
                if (meshSelectedOffset < 0)
                {
                    _cl.DrawIndexed((uint)_indexCount);
                }
                else
                {
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed((uint)meshSelectedOffset * 3, 1, (uint)(0), 0, 0);

                    Vector4 selectedCol = new Vector4(1, 0, 0, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref selectedCol);
                    _cl.DrawIndexed(3, 1, (uint)(meshSelectedOffset) * 3, 0, 0);

                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    uint remainingCount = (uint)_indexCount - (uint)(meshSelectedOffset - 1) * 3;
                    _cl.DrawIndexed(remainingCount, 1, (uint)(meshSelectedOffset + 1) * 3, 0, 0);
                }

                if (_selectedVertexBuffer != null)
                {
                    _cl.ClearDepthStencil(1f);
                    Vector4 co2l = new Vector4(1, 0, 0, 1) * 0.5f;
                    _cl.UpdateBuffer(_materialBuffer, 0, ref co2l);
                    _cl.SetVertexBuffer(0, _selectedVertexBuffer);
                    _cl.SetIndexBuffer(_selectedIndexBuffer, IndexFormat.UInt32);
                    _cl.DrawIndexed((uint)_selectedIndexCount);
                }
            }
        }
        void DrawLdrLoaderMesh(ref Matrix4x4 mat)
        {
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 col = new Vector4(0.2f, 0.6f, 0.6f, 1);
            _cl.UpdateBuffer(_materialBuffer, 0, ref col);

            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _ldrLoaderVertexBuffer);
            _cl.SetIndexBuffer(_ldrLoaderIndexBuffer, IndexFormat.UInt32);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.DrawIndexed((uint)_ldrLoaderIndexCount);
        }
        void DrawMbxMesh(ref Matrix4x4 mat)
        {
            if (ShowMbx && _mbxIndexCount > 0)
            {
                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                Vector4 col = new Vector4(0.2f, 0.6f, 0.6f, 1);
                _cl.UpdateBuffer(_materialBuffer, 0, ref col);

                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _mbxVertexBuffer);
                _cl.SetIndexBuffer(_mbxIndexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);
                _cl.DrawIndexed((uint)_mbxIndexCount);
            }
        }

        void DrawDecomp(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            if (DoDecomp && _decompMeshes.Count > 0)
            {
                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _decompVertexBuffer);
                _cl.SetIndexBuffer(_decompIndexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);
                for (int idx = 0; idx < _decompIndexCounts.Count; idx++)
                {
                    Vector4 col = new Vector4(_decompMeshes[idx].color, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed((uint)_decompIndexCounts[idx].Item2, 1, (uint)_decompIndexCounts[idx].Item1, 0, 0);
                }

                _cl.SetPipeline(_pipeline);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);

                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                Matrix4x4 viewPrj = viewmat * projMat;

                Vector4 edgeColor = new Vector4(1, 1, 1, 1);
                foreach (var mesh in this._decompMeshes)
                {
                    for (int i = 0; i < mesh.points.Count; i += 3)
                    {
                        for (int t = 0; t < 3; t++)
                        {
                            _cl.UpdateBuffer(_materialBuffer, 0, ref edgeColor);
                            var v0 = mesh.points[i + t];
                            var v1 = mesh.points[i + ((t + 1) % 3)];
                            Vector3 pt0 = new Vector3((float)v0.X, (float)v0.Y, (float)v0.Z);
                            Vector3 pt1 = new Vector3((float)v1.X, (float)v1.Y, (float)v1.Z);

                            Matrix4x4 wm = MatrixForLine(pt0, pt1, ref mat, ref viewPrj);

                            _cl.UpdateBuffer(_worldBuffer, 0, ref wm);
                            _cl.DrawIndexed((uint)_cubeIndexCount);
                        }
                    }
                }
            }
        }

        void DrawBSPPortals(ref Matrix4x4 mat)
        {
            if (BSPPortals && _bspPortalsVertexBuffer != null)
            {
                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _bspPortalsVertexBuffer);
                _cl.SetIndexBuffer(_bspPortalsIndexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);

                int portalIdx = 0;
                foreach (var offsets in _bspPortalsIndexCounts)
                {
                    var portal = _bspPortals[portalIdx];
                    if (portal.Visible && (ShowExteriorPortals || !portal.IsExterior))
                    {
                        Vector4 col = new Vector4(portal.color, 1.0f);
                        _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                        _cl.DrawIndexed(offsets.Item2, 1, offsets.Item1, 0, 0);
                    }
                    portalIdx++;
                }
            }
        }

        void DrawBSPFaces(ref Matrix4x4 mat)
        {
            if (BSPFaces && _bspFacesVertexBuffer != null)
            {
                _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _bspFacesVertexBuffer);
                _cl.SetIndexBuffer(_bspFacesIndexBuffer, IndexFormat.UInt32);
                _cl.SetGraphicsResourceSet(0, _projViewSet);
                _cl.SetGraphicsResourceSet(1, _worldTextureSet);

                int portalIdx = 0;
                foreach (var offsets in _bspFacesIndexCounts)
                {
                    Vector4 col = new Vector4(_bspPortalFaces[portalIdx].color, 1);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref col);
                    _cl.DrawIndexed(offsets.Item2, 1, offsets.Item1, 0, 0);
                    portalIdx++;
                }
            }
        }

        void DrawSelectedBSP(ref Matrix4x4 mat, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
            //_cl.ClearDepthStencil(1f);
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 col = new Vector4(selectedBSPNode.Portal.color, 1);
            _cl.UpdateBuffer(_materialBuffer, 0, ref col);

            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _bspSelVertexBuffer);
            _cl.SetIndexBuffer(_bspSelIndexBuffer, IndexFormat.UInt32);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.DrawIndexed((uint)_bspSelIndexCount);

            _cl.SetVertexBuffer(0, _cubeVertexBuffer);
            _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

            Matrix4x4 viewPrj = viewmat * projMat;
            Vector4 edgeColor = new Vector4(1, 1, 1, 1);
            foreach (var face in selectedBSPNode.Portal.Faces)
            {
                if (this.selectedPortalFace == face)
                {
                    var pts = face.points;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        _cl.UpdateBuffer(_materialBuffer, 0, ref edgeColor);
                        Vector3 pt0 = DTF(pts[i]);
                        Vector3 pt1 = DTF(pts[(i + 1) % pts.Count]);

                        Matrix4x4 wm = MatrixForLine(pt0, pt1, ref mat, ref viewPrj);

                        _cl.UpdateBuffer(_worldBuffer, 0, ref wm);
                        _cl.DrawIndexed((uint)_cubeIndexCount);
                    }
                }
            }
        }
        void DrawRaycast(ref Matrix4x4 viewmat)
        {
            if (this._primTexUpdate)
            {
                _cl.CopyTexture(this._primStgTexture, this._primTexture);
                this._primTexUpdate = false;
            }
            Matrix4x4.Invert(viewmat, out viewmat);
            Vector4 v4 = new Vector4(Window.Width, Window.Height, 0, 0);
            _cl.UpdateBuffer(_raycastBuffer, 0, ref v4);
            _cl.UpdateBuffer(_raycastBuffer, 16, ref viewmat);
            _cl.UpdateBuffer(_raycastBuffer, 80, ref this.numPrimitives);
            Vector4 bboxmin = new Vector4(DTF(this.primBbox.Min), 0);
            Vector4 bboxsize = new Vector4(DTF(this.primBbox.Max - this.primBbox.Min), 0);
            _cl.UpdateBuffer(_raycastBuffer, 96, ref bboxmin);
            _cl.UpdateBuffer(_raycastBuffer, 112, ref bboxsize);
            _cl.SetPipeline(_pipelineRaycast);
            _cl.SetGraphicsResourceSet(0, _raycastSet);
            _cl.SetVertexBuffer(0, _planeVertexBuffer);
            _cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            _cl.DrawIndexed((uint)_planeIndexCount);
        }
        void DrawBisectors(ref Matrix4x4 mat)
        {
            Dictionary<ConnectorType, Vector4> colors = new Dictionary<ConnectorType, Vector4>()
                { { ConnectorType.Stud, new Vector4(0.8f, 0.1f, 0, 1) }, 
                { ConnectorType.RStud, new Vector4(0.1f, 0.1f, 0.8f, 1) },
                { ConnectorType.MFigHipLeg, new Vector4(0.3f, 0.8f, 0, 1) },
                { ConnectorType.MFigRHipLeg, new Vector4(0.3f, 0.8f, 0, 1) },
                { ConnectorType.MFigHipStud, new Vector4(0.3f, 0.2f, 0.9f, 1)}};

            if (ShowConnectors)
            {
                Vector4[] axiscolors = new Vector4[3]
                {
                        new Vector4(1,0,0,1),
                        new Vector4(0,1,0,1),
                        new Vector4(0,0,1,1)
                };

                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);

                foreach (var c in connectorVizs)
                {
                    const float lsize = 0.05f;
                    Matrix4x4 cmat = c.Mat.ToM44();
                    Vector4 color = axiscolors[2] * (c.IsSelected ? 1.0f : 0.5f);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    {
                        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, 0, -0.5f)) *
                            Matrix4x4.CreateScale(new Vector3(lsize, lsize, 0.5f)) * cmat * mat;
                        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                        _cl.DrawIndexed((uint)_cubeIndexCount);
                    }
                    color = axiscolors[0] * (c.IsSelected ? 1.0f : 0.5f);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    {
                        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(-0.5f, 0, 0)) *
                            Matrix4x4.CreateScale(new Vector3(0.5f, lsize, lsize)) * cmat * mat;
                        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                        _cl.DrawIndexed((uint)_cubeIndexCount);
                    }
                    color = axiscolors[1] * (c.IsSelected ? 1.0f : 0.5f);
                    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
                    {
                        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, -0.5f, 0)) *
                            Matrix4x4.CreateScale(new Vector3(lsize, 1.0f, lsize)) * cmat * mat;
                        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
                        _cl.DrawIndexed((uint)_cubeIndexCount);
                    }
                }
            }
            if (ShowBisector)
            {
                _cl.SetPipeline(_pipeline);
                _cl.SetVertexBuffer(0, _cubeVertexBuffer);
                _cl.SetIndexBuffer(_cubeIndexBuffer, IndexFormat.UInt16);
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
