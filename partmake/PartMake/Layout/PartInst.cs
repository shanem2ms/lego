using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AssetPrimitives;
using SampleBase;
using System.Numerics;
using Veldrid;
using Veldrid.SPIRV;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using partmake.Topology;

namespace partmake
{

    public class Part : IComparable<Part>
    {
        public Part(string name, string mp, string thumb, LDrawDatFile.Descriptor d)
        {
            Name = name;
            thumbnailPath = thumb;
            meshPath = mp;
            descriptor = d;

            if (descriptor?.dims != null)
            {
                dimsf = new float[3] { 1, 1, 1 };
                for (int i = 0; i < descriptor.dims.Length; ++i)
                {
                    dimsf[i] = descriptor.dims[i];
                }
            }
        }
        float[] dimsf = null;
        public string Name { get; }
        string thumbnailPath;
        string meshPath;
        float[][] collisionPts; 

        public float[][]CollisionPts
        {
            get { if (collisionPts == null) LoadCollision(); return collisionPts; }
        }

        System.Windows.Media.ImageSource thumb = null;
        public System.Windows.Media.ImageSource Thumb
        {
            get
            {
                if (thumb != null) return thumb;
                if (File.Exists(thumbnailPath))
                {
                    thumb = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri(thumbnailPath));
                }

                return thumb;
            }
        }

        LDrawDatFile.Descriptor descriptor;

        public float[] DimsF => dimsf;
        public string MainType => descriptor.type;
        public string Subtype => descriptor.subtype;
        public string Desc => descriptor.desc;
        public string Dims => descriptor.dims != null ? String.Join('x', descriptor.dims) : null;
        public Connector []Connectors => descriptor.Connectors;

        public IEnumerable<Connector> ConnectorsWithType(ConnectorType connectorType)
        { return descriptor.Connectors.Where(c => c.Type == connectorType); }

        public int CompareTo(Part other)
        {
            int c = MainType.CompareTo(other.MainType);
            if (c != 0)
                return c;

            c = (descriptor.dims == null ? 1 : 0) - (other.descriptor.dims == null ? 1 : 0);
            if (c != 0)
                return c;
            if (descriptor.dims != null)
            {
                for (int i = 0; i < 3; ++i)
                {
                    float lsize = i < descriptor.dims.Length ? descriptor.dims[i] : 1;
                    float rsize = i < other.descriptor.dims.Length ? other.descriptor.dims[i] : 1;
                    c = lsize.CompareTo(rsize);
                    if (c != 0)
                        return c;
                }
            }

            c = (Subtype == null ? 1 : 0) - (other.Subtype == null ? 1 : 0);
            if (c != 0)
                return c;
            if (Subtype != null)
                return Subtype.CompareTo(other.Subtype);
            c = (Desc == null ? 1 : 0) - (other.Desc == null ? 1 : 0);
            if (c != 0)
                return c;
            if (Desc != null)
                return Desc.CompareTo(other.Desc);
            return 0;
        }

        public DeviceBuffer ldrLoaderVertexBuffer = null;
        public DeviceBuffer ldrLoaderIndexBuffer = null;
        public int ldrLoaderIndexCount = 0;
        public void LoadLdr(ResourceFactory _factory, GraphicsDevice GraphicsDevice)
        {
            LdrLoader.PosTexcoordNrmVertex[] ldrvertices;
            int[] ldrindices;
            LdrLoader ldrLoader = new LdrLoader();
            ldrLoader.LoadCached(meshPath, out ldrvertices, out ldrindices);
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
                Vtx[] vlvertices = ldrvertices.Select(v => new Vtx(new Vector3(v.m_x, v.m_y, v.m_z), new Vector3(v.m_nx, v.m_ny, v.m_nz),
                    new Vector2(v.m_u, v.m_v))).ToArray();

                uint[] vlindices = ldrindices.Select(i => (uint)i).ToArray();

                ldrLoaderVertexBuffer = _factory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vlvertices.Length), BufferUsage.VertexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderVertexBuffer, 0, vlvertices);

                ldrLoaderIndexBuffer = _factory.CreateBuffer(new BufferDescription(sizeof(uint) * (uint)vlindices.Length, BufferUsage.IndexBuffer));
                GraphicsDevice.UpdateBuffer(ldrLoaderIndexBuffer, 0, vlindices);
                ldrLoaderIndexCount = vlindices.Length;
            }
            LoadCollision();
        }       

        void LoadCollision()
        {
            string colfile = 
                Path.Combine(
                Path.GetDirectoryName(meshPath),
                Path.GetFileNameWithoutExtension(meshPath) + ".col");
            collisionPts = Convex.LoadCollision(colfile);
        }
    }

    public class PartInst
    {
        public Part item;
        //public Vector3 position;
        //public Quaternion rotation;
        public Matrix4x4 mat;
        public int paletteIdx;
        public bool dynamic;

        public PartInst(Part item, Matrix4x4 mat, int paletteIdx) : 
            this(item, mat, paletteIdx, false)
        {

        }

        public PartInst(Part item, Matrix4x4 mat, int paletteIdx, bool isdynamic)
        {
            this.mat = mat;
            this.item = item;
            this.paletteIdx = paletteIdx;
            this.dynamic = isdynamic;
        }

    }


}
