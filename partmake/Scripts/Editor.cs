using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using partmake.graphics;
using System.IO;

namespace partmake.script
{
    public class Script
    {
        private DeviceBuffer _planeVertexBuffer;
        private DeviceBuffer _planeIndexBuffer;
        uint _planeIndexCount;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _materialBuffer;
        Vector3 wposMouse;

        public void Run()
        {                    	
    		Api.MouseHandler = MouseHandler;
    		Api.CustomDraw = Draw;
        }
        
		public void Gen()    
    	{
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;
        
    		ShaderSet ss = new ShaderSet(
    			Path.Combine(Api.ScriptFolder, "Editor\\VtxLine.glsl"),
    			Path.Combine(Api.ScriptFolder, "Editor\\PixLine.glsl"), false);
                    
			_projectionBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(80, BufferUsage.UniformBuffer));
            _materialBuffer = G.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));

            
			ResourceLayout projViewLayout = G.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = G.ResourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("MeshColor", ResourceKind.UniformBuffer, ShaderStages.Fragment)));            
                    
            _projViewSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = G.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _materialBuffer));
                                
            _pipeline = G.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                ss.Desc,
                new[] { projViewLayout, worldTextureLayout },
                G.Swapchain.Framebuffer.OutputDescription));            
    	}
    	        
    	List<Vector3> mousePts = new List<Vector3>();
        void MouseHandler(LayoutVis.MouseCommand command, int btn, Vector2 screenPos, Vector3 p0,
        	Vector3 p1, ref Matrix4x4 viewProj)
        {
        	Vector3 []snapDirs = new Vector3[3]
	        	{ Vector3.UnitX,
	        	Vector3.UnitY,
	        	Vector3.UnitZ };
			if (mousePts.Count > 0)
			{
				Vector3 from = mousePts[mousePts.Count - 1];
				from *= G.WorldScale;
				Vector2 fromWpt;
				{
					Vector4 from4 = new Vector4(from, 1);				
					Vector4 spt = Vector4.Transform(from4, viewProj);
					spt /= spt.W;
					fromWpt = new Vector2(spt.X, spt.Y);
				}
				int idx = 0;
				int minIdx = 0;
				float minDist = 10000;
				Vector2 minPt = Vector2.Zero;
				foreach (Vector3 snapdir in snapDirs)
				{
					Vector4 from4 = new Vector4(from + snapdir, 1);				
					Vector4 spt = Vector4.Transform(from4, viewProj);
					spt /= spt.W;
					Vector2 snapDir = new Vector2(spt.X, spt.Y);
					Vector2 closestPt =
						Utils.ClosestPtOnLine(fromWpt, snapDir, screenPos);
						float dlen = (closestPt - screenPos).Length();
					if (dlen < minDist)
					{
						minIdx = idx;
						minDist = dlen;
						minPt = closestPt;
					}
					idx++;
				}
				if (minDist < 0.01f)
				{
					Vector3 dir = snapDirs[minIdx];

					Matrix4x4 invViewProj;
					Matrix4x4.Invert(viewProj, out invViewProj);
					
					Vector3 lookdir = GetLookDir(ref invViewProj);
					Vector3 up = Vector3.Cross(lookdir, dir);
					Vector3 nrm = Vector3.Cross(-up, dir);
		        	Vector3 worldPos;
		        	Vector3 mray0, mray1;
		        	Utils.GetMouseWsRays(minPt, out mray0, out mray1, ref invViewProj);
		        	Utils.IntersectPlane(mray0, mray1, nrm, mousePts[mousePts.Count - 1], out worldPos);
		        	wposMouse = worldPos;					
				}
				
			}
			else
			{
	        	Vector3 worldPos;
	        	partmake.graphics.Utils.IntersectPlane(p0, p1, Vector3.UnitY, Vector3.Zero, out worldPos);
	        	if (command == LayoutVis.MouseCommand.ButtonDown)
	        	{
	        		mousePts.Add(worldPos);
			    }
			    if (mousePts.Count > 0)
			    {
			    	
			    }
	    		wposMouse = worldPos;
	    	}
        }
                
        Vector3 GetLookDir(ref Matrix4x4 invViewProj)
        {
			Vector4 wspt0 = Vector4.Transform(new Vector4(0,0,0,1), invViewProj);
			wspt0 /= wspt0.W;
			Vector4 wspt1 = Vector4.Transform(new Vector4(0,0,1,1), invViewProj);
			wspt1 /= wspt1.W;
			Vector4 lookDir = wspt1 - wspt0;
			Vector3 lookDir3 = new Vector3(lookDir.X, lookDir.Y, lookDir.Z);
			return Vector3.Normalize(lookDir3);
        }
        
        struct WorldBuffer
        {
        	public Matrix4x4 worldBuf;
        	public float aspect;
        }
        
        public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
        	if (_pipeline == null)
        		Gen();
        		
        	cl.SetPipeline(_pipeline);
            cl.SetFramebuffer(G.Swapchain.Framebuffer);
            cl.SetGraphicsResourceSet(0, _projViewSet);
            cl.SetGraphicsResourceSet(1, _worldTextureSet);
    	 	
            cl.UpdateBuffer(_projectionBuffer, 0, ref projMat);
            cl.UpdateBuffer(_viewBuffer, 0, viewmat);
            
            foreach (Vector3 pt in mousePts)
            {
				Matrix4x4 mat =
	                Matrix4x4.CreateScale(5f) *
	                Matrix4x4.CreateTranslation(pt) *
	                Matrix4x4.CreateScale(G.WorldScale);
	
				WorldBuffer wbuf = new WorldBuffer();
				wbuf.worldBuf = mat;
				wbuf.aspect = G.WindowSize.Y / G.WindowSize.X;
	            cl.UpdateBuffer(_worldBuffer, 0, ref wbuf);
	            Vector4 color = new Vector4(0.5f, 0.5f, 1.0f, 1);
	            cl.UpdateBuffer(_materialBuffer, 0, ref color);
	            cl.SetVertexBuffer(0, _planeVertexBuffer);
	            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
	            cl.DrawIndexed(_planeIndexCount,
	            	1, 0,0,0);            
            }
			{
			Matrix4x4 mat =
                Matrix4x4.CreateScale(2f) *
                Matrix4x4.CreateTranslation(wposMouse) *
                Matrix4x4.CreateScale(G.WorldScale);

            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
            Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
            cl.UpdateBuffer(_materialBuffer, 0, ref color);
            cl.SetVertexBuffer(0, _planeVertexBuffer);
            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(_planeIndexCount,
            	1, 0,0,0);    
        	}
			if (mousePts.Count > 0)
			{
				Vector3 from = mousePts[mousePts.Count - 1];
				Vector3 vec = (wposMouse - from);
				Vector3 mid = (wposMouse + from) * 0.5f;
				Vector3 xdir = Vector3.Normalize(vec);
				Vector3 zdirPrime = Vector3.Cross(xdir, Vector3.UnitY);
				Vector3 ydir = Vector3.Cross(xdir, zdirPrime);
				Vector3 zdir = Vector3.Cross(ydir, xdir);
				
				Matrix4x4 m = Matrix4x4.Identity;
				m.M11 = xdir.X;
				m.M12 = xdir.Y;
				m.M13 = xdir.Z;
				
				m.M21 = ydir.X;
				m.M22 = ydir.Y;
				m.M23 = ydir.Z;

				m.M31 = zdir.X;
				m.M32 = zdir.Y;
				m.M33 = zdir.Z;

				float len = vec.Length() / 2;
							Matrix4x4 mat =
                Matrix4x4.CreateScale(new Vector3(len, 2, 2)) *
                m *
				Matrix4x4.CreateTranslation(mid) *
                Matrix4x4.CreateScale(G.WorldScale);
	            cl.UpdateBuffer(_worldBuffer, 0, ref mat);
	            Vector4 color = new Vector4(1, 0.5f, 0.5f, 1);
	            cl.UpdateBuffer(_materialBuffer, 0, ref color);
	            cl.SetVertexBuffer(0, _planeVertexBuffer);
	            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
	            cl.DrawIndexed(_planeIndexCount,
	            	1, 0,0,0);    
			}
		}
    }
}