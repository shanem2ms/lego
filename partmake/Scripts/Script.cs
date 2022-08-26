using System;
using System.Collections.Generic;
using System.Numerics;
namespace partmake.script
{
    public class Script
    {
        Random r = new Random();
    	int []colors = new int[4] { 288, 2, 74, 6 };
        public void Run()
        {            
    		Utils u = new Utils();
    		u.Minifig(Matrix4x4.CreateTranslation(new Vector3(0, 20, 0)));
    		
        }
        
        void Terrain()
        {
        	Utils u = new Utils();
			for (int i = -10; i < 10; i++)
        	{
	        	for (int j = -10; j < 10; j++)
	        	{
					Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(j*80-10, 0, i*80-10));        		        	
	        		Api.Parts.Add(new PartInst(Api.GetPart("3031"),
						mat, colors[r.Next(0,4)] ));
				}
			}
			
			for (int i = 0; i < 100; ++i)
			{
				int x = r.Next(-40,40);
				int y = r.Next(-40,40);
				Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(x*20, 8, y*20));
			}
			
    		Api.Parts.Add(new PartInst(Api.GetPart("4599"),
    			Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 2.25f) *
				Matrix4x4.CreateTranslation(new Vector3(80,8,40)), 1));        }
    }
}