using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Veldrid;
using partmake.script;
using partmake.Topology;

namespace partmake.primitives
{
    public static class Cube
    {
        public static Vtx[] GetVertices()
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

        public static ushort[] GetIndices()
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

        static DeviceBuffer _vertexBuffer = null;
        public static DeviceBuffer VertexBuffer
        {
            get
            {
                if (_vertexBuffer == null)
                {
                    var vertices = GetVertices();
                    _vertexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
                    Api.GraphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices);
                }
                return _vertexBuffer;
            }
        }

        static DeviceBuffer _indexBuffer = null;
        public static DeviceBuffer IndexBuffer
        {
            get
            {
                if (_indexBuffer == null)
                {
                    var indices = GetIndices();
                    _indexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)indices.Length, BufferUsage.IndexBuffer));
                    Api.GraphicsDevice.UpdateBuffer(_indexBuffer, 0, indices);
                }
                return _indexBuffer;
            }
        }

        static uint _indexLength = 0;
        public static uint IndexLength
        {
            get { if (_indexLength == 0) _indexLength = (uint)GetIndices().Length; return _indexLength; }
        }
    }

    public static class Plane
    {
        public static Vtx[] GetVertices()
        {
            Vtx[] vertices = new Vtx[]
            {
                // Back                                                               
                new Vtx(new Vector3(+1.0f, +1.0f, 0.0f), new Vector2(1, 0)),
                new Vtx(new Vector3(-1.0f, +1.0f, 0.0f), new Vector2(0, 0)),
                new Vtx(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0, 1)),
                new Vtx(new Vector3(+1.0f, -1.0f, 0.0f), new Vector2(1, 1)),
            };

            return vertices;
        }

        public static ushort[] GetIndices()
        {
            ushort[] indices =
            {
                0,1,2, 0,2,3
            };

            return indices;
        }

        static DeviceBuffer _vertexBuffer = null;
        public static DeviceBuffer VertexBuffer
        {
            get
            {
                if (_vertexBuffer == null)
                {
                    var vertices = GetVertices();
                    _vertexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Vtx.SizeInBytes * vertices.Length), BufferUsage.VertexBuffer));
                    Api.GraphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices);
                }
                return _vertexBuffer;
            }
        }

        static DeviceBuffer _indexBuffer = null;
        public static DeviceBuffer IndexBuffer
        {
            get
            {
                if (_indexBuffer == null)
                {
                    var indices = GetIndices();
                    _indexBuffer = Api.ResourceFactory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)indices.Length, BufferUsage.IndexBuffer));
                    Api.GraphicsDevice.UpdateBuffer(_indexBuffer, 0, indices);
                }
                return _indexBuffer;
            }
        }

        static uint _indexLength = 0;
        public static uint IndexLength
        {
            get { if (_indexLength == 0) _indexLength = (uint)GetIndices().Length; return _indexLength; }
        }
    }
}

