using AssetPrimitives;
using SampleBase;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;
using System.Collections.Generic;

namespace partmake
{
    public class PartVis : SampleApplication
    {
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        private CommandList _cl;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private int _indexCount;
        Vector2 lookDir;
        int mouseDown = 0;
        Vector2 lMouseDownPt;
        Vector2 mouseDownLookDir;
        Vector3 partOffset;
        float partScale;

        LDrawFolders.Entry _part;
        ResourceFactory _factory;
        public LDrawFolders.Entry Part { get => _part; set { _part = value; OnPartUpdated(); } }

        public PartVis(ApplicationWindow window) : base(window)
        {
        }


        public void MouseDown(int btn, int X, int Y)
        {
            mouseDown |= 1 << btn;
            mouseDownLookDir = lookDir;
            lMouseDownPt = new Vector2(X, Y);
        }
        public void MouseUp(int btn, int X, int Y)
        {
            mouseDown &= ~(1 << btn);
        }
        public void MouseMove(int X, int Y)
        {
            if ((mouseDown & 1) != 0)
                lookDir = mouseDownLookDir + (new Vector2(X, Y) - lMouseDownPt) * 0.01f;
        }

        void OnPartUpdated()
        {
            if (_factory == null)
                return;
            LDrawDatFile part = LDrawFolders.GetPart(_part);
            List<Vtx> vlist = new List<Vtx>();
            part.GetVertices(vlist, false);
            Vtx[] vertices = vlist.ToArray();
            Vector3 min = vertices[0].pos, max = vertices[0].pos;
            foreach (Vtx v in vertices)
            {
                if (v.pos.X < min.X) min.X = v.pos.X;
                if (v.pos.Y < min.Y) min.Y = v.pos.Y;
                if (v.pos.Z < min.Z) min.Z = v.pos.Z;
                if (v.pos.X > max.X) max.X = v.pos.X;
                if (v.pos.Y > max.Y) max.Y = v.pos.Y;
                if (v.pos.Z > max.Z) max.Z = v.pos.Z;
            }

            partOffset = (min + max) * 0.5f;
            Vector3 vecScale = (max - min);
            partScale = 1 / System.MathF.Max(System.MathF.Max(vecScale.X, vecScale.Y), vecScale.Z);
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

        }
        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            _factory = factory;
           _projectionBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

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

            ResourceLayout projViewLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, true, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
                MainSwapchain.Framebuffer.OutputDescription));

            _projViewSet = factory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer));

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

            _cl.UpdateBuffer(_viewBuffer, 0, Matrix4x4.CreateLookAt(Vector3.UnitZ * 2.5f - Vector3.UnitY * 2.5f, Vector3.Zero, Vector3.UnitY));

            Matrix4x4 mat = 
                Matrix4x4.CreateTranslation(-partOffset) *
                Matrix4x4.CreateScale(partScale) *
                Matrix4x4.CreateRotationY(lookDir.X) *
                Matrix4x4.CreateRotationX(lookDir.Y);
            _cl.UpdateBuffer(_worldBuffer, 0, ref mat);

            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _vertexBuffer);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.DrawIndexed((uint)_indexCount);

            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            GraphicsDevice.WaitForIdle();
        }

    
        private const string VertexCode = @"
#version 450
layout(set = 0, binding = 0) uniform ProjectionBuffer
{
    mat4 Projection;
};
layout(set = 0, binding = 1) uniform ViewBuffer
{
    mat4 View;
};

layout(set = 1, binding = 0) uniform WorldBuffer
{
    mat4 World;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;
layout(location = 1) out vec3 fsin_normal;

void main()
{
    vec4 worldPosition = World * vec4(Position, 1);
    vec4 viewPosition = View * worldPosition;
    vec4 clipPosition = Projection * viewPosition;
    gl_Position = clipPosition;
    fsin_texCoords = TexCoords;
    fsin_normal = Normal;
}";

        private const string FragmentCode = @"
#version 450

layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec3 fsin_normal;
layout(location = 0) out vec4 fsout_color;


void main()
{
    float p = 1.0;
    float light = pow(clamp(dot(normalize(vec3(1, 1, 0)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(-1, 1, 0)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(1, 0, -1)), fsin_normal),0,1), p) +
        pow(clamp(dot(normalize(vec3(0, -1, 1)), fsin_normal),0,1), p);
    vec3 c = vec3(1,1,0) * (light * 0.7 + 0.1);
    fsout_color =  vec4(c,1);
}";
    }

    public struct VertexPositionTexture
    {
        public const uint SizeInBytes = 20;

        public float PosX;
        public float PosY;
        public float PosZ;

        public float TexU;
        public float TexV;

        public VertexPositionTexture(Vector3 pos, Vector2 uv)
        {
            PosX = pos.X;
            PosY = pos.Y;
            PosZ = pos.Z;
            TexU = uv.X;
            TexV = uv.Y;
        }
    }
}
