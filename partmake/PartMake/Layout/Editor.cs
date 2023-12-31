using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using partmake.graphics;
using System.IO;

namespace partmake
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

       
		public void Gen()    
    	{
            _planeVertexBuffer = primitives.Plane.VertexBuffer;
            _planeIndexBuffer = primitives.Plane.IndexBuffer;
            _planeIndexCount = primitives.Plane.IndexLength;
        
    		ShaderSet ss = new ShaderSet("partmake.VtxLine.glsl",
    			"partmake.PixLine.glsl", true);
                    
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
    	   	        
        static bool SnapToPoint(Vector2 screenPos, Vector3 []snapPoints,
    		ref Matrix4x4 viewProj, out Vector3 snapPos, out int snapIdx)
    	{
			int idx = 0;
			int minIdx = 0;
			float minDist = 10000;
			Vector2 minPt = Vector2.Zero;
			foreach (Vector3 snappt in snapPoints)
			{
	    		Vector3 from = snappt * G.WorldScale;
				Vector4 from4 = new Vector4(from, 1);				
				Vector4 spt = Vector4.Transform(from4, viewProj);
				Vector2 fromSpt = new Vector2(spt.X / spt.W, spt.Y / spt.W);
				float d = (fromSpt - screenPos).Length();
				if (d < minDist)
				{
					minDist = d;
					minIdx = idx;
				}
				idx++;
			}
			
			if (minDist < 0.02f)
			{
				snapPos = snapPoints[minIdx];
				snapIdx = minIdx;
				return true;				
			}
			
			snapPos = Vector3.Zero;
			snapIdx = -1;
			return false;
    	}
    	
    	static bool SnapToDir(Vector3 fromOrig, Vector2 screenPos, Vector3 []snapDirs,
    		ref Matrix4x4 viewProj, out Vector3 snapPos, out int snapIdx)
    	{
    		Vector3 from = fromOrig * G.WorldScale;
			Vector4 spt;
			Vector2 fromSpt;
			{
				Vector4 from4 = new Vector4(from, 1);				
				spt = Vector4.Transform(from4, viewProj);
				fromSpt = new Vector2(spt.X / spt.W, spt.Y / spt.W);
			}    	
			int idx = 0;
			int minIdx = 0;
			float minDist = 10000;
			Vector2 minPt = Vector2.Zero;
			foreach (Vector3 snapdir in snapDirs)
			{
				Vector4 from4 = new Vector4(from + snapdir, 1);				
				Vector4 spt2 = Vector4.Transform(from4, viewProj);
				spt2 /= spt2.W;
				Vector2 snapDir = new Vector2(spt2.X, spt2.Y);
				Vector2 closestPt =
					Utils.ClosestPtOnLine(fromSpt, snapDir, screenPos);
					float dlen = (closestPt - screenPos).Length();
				if (dlen < minDist)
				{
					minIdx = idx;
					minDist = dlen;
					minPt = closestPt;
				}
				idx++;
			}
			if (minDist < 0.02f)
			{
				Vector3 dir = snapDirs[minIdx];				
				Matrix4x4 invViewProj;
				Matrix4x4.Invert(viewProj, out invViewProj);
				
				Vector3 lookdir = GetLookDir(ref invViewProj);
				Vector3 up = Vector3.Normalize(Vector3.Cross(lookdir, dir));
				Vector3 nrm = Vector3.Normalize(Vector3.Cross(-up, dir));
	        	Vector3 worldPos;
	        	Vector3 mray0, mray1;
	        	Utils.GetMouseWsRays(minPt, out mray0, out mray1, ref invViewProj);
	        	Utils.IntersectPlane(mray0, mray1, nrm, fromOrig, out worldPos);
	        	snapPos = worldPos;	
	        	snapIdx = minIdx;
	        	return true;
	        }
	        
        	snapPos = Vector3.Zero;
        	snapIdx = -1;
        	return false;
		}
		
		static string Pv(Vector3 v)
		{
			return $"[{v.X}, {v.Y}, {v.Z}]";
		}
    	List<Vector3> mousePts = new List<Vector3>();
    	int snapAxis = -1;
        void MouseHandler(LayoutVis.MouseCommand command, int btn, Vector2 screenPos, Vector3 p0,
        	Vector3 p1, ref Matrix4x4 viewProj)
        {
        	Vector3 []snapDirs = new Vector3[3]
	        	{ Vector3.UnitX,
	        	Vector3.UnitY,
	        	Vector3.UnitZ };
	        snapAxis = -1;
			if (mousePts.Count > 0)
			{
				Vector3 from = mousePts[mousePts.Count - 1];
				Vector3 snappos;
				int snapIdx;
				if (SnapToPoint(screenPos, mousePts.ToArray(), ref viewProj, out snappos, out snapIdx))
				{
					wposMouse = snappos;
				}
				else if (SnapToDir(from, screenPos, snapDirs, ref viewProj, out snappos, out snapIdx))
				{
					wposMouse = snappos;
					snapAxis = snapIdx;
				}				
				else
				{
					Vector4 spt = Vector4.Transform(new Vector4(from * G.WorldScale, 1), viewProj);
					Matrix4x4 invViewProj;
					Matrix4x4.Invert(viewProj, out invViewProj);
					Vector4 wptZ = Vector4.Transform(new Vector4(screenPos.X * spt.W, screenPos.Y * spt.W, spt.Z, spt.W), invViewProj);
					wptZ /= wptZ.W;
					wposMouse = new Vector3(wptZ.X, wptZ.Y, wptZ.Z) / G.WorldScale;
				}
				
	        	if (command == LayoutVis.MouseCommand.ButtonDown)
	        	{
	        		mousePts.Add(wposMouse);
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
                
        static Vector3 GetLookDir(ref Matrix4x4 invViewProj)
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
        
        Vector4 []snapColors = new Vector4[3] {
        	new Vector4(1,0,0,1),
        	new Vector4(0,1,0,1),
        	new Vector4(0,0,1,1)
        };
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
			for (int i = 0; i < mousePts.Count - 1; ++i)
			{
				Vector3 from = mousePts[i];
				Vector3 to = mousePts[i+1];
				Vector3 vec = (to - from);
				Vector3 mid = (to + from) * 0.5f;
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
	            Vector4 color = new Vector4(0.8f, 0.8f, 0.8f, 1);
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
	            Vector4 color = snapAxis >= 0 ? snapColors[snapAxis] : 
	            	new Vector4(0.8f, 0.8f, 0.8f, 1);
	            cl.UpdateBuffer(_materialBuffer, 0, ref color);
	            cl.SetVertexBuffer(0, _planeVertexBuffer);
	            cl.SetIndexBuffer(_planeIndexBuffer, IndexFormat.UInt16);
	            cl.DrawIndexed(_planeIndexCount,
	            	1, 0,0,0);    
			}
		}
    }
}