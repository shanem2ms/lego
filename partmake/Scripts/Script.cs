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
    		u.Minifig(Matrix4x4.CreateTranslation(new Vector3(8, 68, 0)));
    		Terrain();    		 
        }
        
        void Rain()
        {
			var flower = Api.GetPart("24866");
    		float range = 500;
    		for (int i = 0; i < 55; ++i)
    		{
    			Api.AddUnconnected(new PartInst(flower, 
    				Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, r.NextSingle() * MathF.PI) * 
    				Matrix4x4.CreateTranslation(new Vector3((r.NextSingle() - 0.5f) * range, 
    					r.NextSingle() * 350, 
    				(r.NextSingle() - 0.5f) * range)), 22, true));
    		}
    	}
        void Terrain()
        {
        	Utils u = new Utils();
			for (int i = -10; i < 10; i++)
        	{
	        	for (int j = -10; j < 10; j++)
	        	{
					Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(j*80-10, 0, i*80-10));        		        	
	        		Api.AddUnconnected(new PartInst(Api.GetPart("3031"),
						mat, colors[r.Next(0,4)], true ));
				}
			}
			
			for (int i = 0; i < 100; ++i)
			{
				int x = r.Next(-40,40);
				int y = r.Next(-40,40);
				Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(x*20, 8, y*20));
			}
			
        }
    }
}