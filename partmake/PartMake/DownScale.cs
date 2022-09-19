using partmake.graphics;
using SharpText.Veldrid;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Veldrid;

namespace partmake
{
    class DownScale
    {
        private Plane plane;
        private Pipeline _pipeline;
        private ResourceSet _resourceSet;
        private Texture _color;
        public TextureView _outView;
        public Framebuffer _FB;
        private DeviceBuffer cbufferSubsample;


        public TextureView View => _outView;
        public Texture OutTexture => _color;
        public DownScale()
        { }

        public struct Subsample
        {
            public float ddx;
            public float ddy;
            Vector2 filler;
        }

        public void CreateResources(ResourceFactory factory, TextureView inputView, uint width, uint height)
        {
            plane = new Plane();
            _color = factory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1,
            PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget | TextureUsage.Sampled));
            _outView = factory.CreateTextureView(_color);
            _FB = factory.CreateFramebuffer(new FramebufferDescription(null, _color));

            ResourceLayout layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
              new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
              new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
              new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

            graphics.ShaderSet shaderSet = new ShaderSet("DepthDownScale-vertex", "DepthDownScale-fragment");

            GraphicsPipelineDescription mirrorPD = new GraphicsPipelineDescription(
               BlendStateDescription.SingleOverrideBlend,
               DepthStencilStateDescription.DepthOnlyLessEqual,
               new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
               PrimitiveTopology.TriangleList,
               shaderSet.Desc,
               layout,
               _FB.OutputDescription);

            cbufferSubsample = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Subsample>(),
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            Sampler greateSampler = factory.CreateSampler(new SamplerDescription(SamplerAddressMode.Clamp, SamplerAddressMode.Clamp,
                SamplerAddressMode.Clamp, SamplerFilter.MinPoint_MagPoint_MipPoint,
                ComparisonKind.Greater, 0, 0, 0, 0, SamplerBorderColor.OpaqueBlack));

            _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(layout,
                inputView,
                greateSampler,
                cbufferSubsample));
            _pipeline = factory.CreateGraphicsPipeline(ref mirrorPD);
            Subsample ss = new Subsample() { ddx = 0.5f / width, ddy = 0.5f / height };
            script.Api.GraphicsDevice.UpdateBuffer(cbufferSubsample, 0, ref ss);
        }


        public void Draw(CommandList cl)
        {
            cl.SetFramebuffer(_FB);
            cl.SetFullViewports();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _resourceSet);
            cl.SetVertexBuffer(0, primitives.Plane.VertexBuffer);
            cl.SetIndexBuffer(primitives.Plane.IndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(primitives.Plane.IndexLength, 1, 0, 0, 0);
        }
    }
}
