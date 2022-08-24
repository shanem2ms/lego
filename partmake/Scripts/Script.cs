using System;
using System.Collections.Generic;
using System.Numerics;
namespace partmake.script
{
    public class Script
    {
        Random r = new Random();
    	int []colors = new int[4] { 288, 2, 74, 6 };
        public void Run(List<PartInst> outparts, List<Vector4> locators)
        {            
    		Utils u = new Utils();
    		{
    			var stud1side = LDrawFolders.GetCacheItem("87087");
	    		Matrix4x4 mat = 
	    			Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));        		        	
        		outparts.Add(new PartInst(stud1side,mat, 326));        		
        		Matrix4x4 m2 = stud1side.Connectors[4].IM44 * stud1side.Connectors[0].M44 * mat;
        		outparts.Add(new PartInst(stud1side,m2, 29));
        		Vector3 p = stud1side.Connectors[4].PosY;
        		Vector3 d = stud1side.Connectors[4].DirY * 0;
	    		locators.Add(new Vector4(p.X + d.X,p.Y + d.Y,p.Z + d.Z,1f));
    		}    		 
    		
        }
        
        void Terrain(List<PartInst> outparts)
        {
        	Utils u = new Utils();
			for (int i = -10; i < 10; i++)
        	{
	        	for (int j = -10; j < 10; j++)
	        	{
					Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(j*80-10, 0, i*80-10));        		        	
	        		outparts.Add(new PartInst(LDrawFolders.GetCacheItem("3031"),
						mat, colors[r.Next(0,4)] ));
				}
			}
			
			for (int i = 0; i < 100; ++i)
			{
				int x = r.Next(-40,40);
				int y = r.Next(-40,40);
				Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(x*20, 8, y*20));
				u.MakeFlower(outparts, mat);
			}
			
    		outparts.Add(new PartInst(LDrawFolders.GetCacheItem("4599"),
    			Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 2.25f) *
				Matrix4x4.CreateTranslation(new Vector3(80,8,40)), 1));        }
    }
}