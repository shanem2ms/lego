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
    		TerrainGen tg = new TerrainGen();
    		tg.Gen();
    		Api.CustomDraw = tg.Draw;
    		//u.Minifig(Matrix4x4.CreateTranslation(new Vector3(8, 68, 0)));
    		//Terrain();  
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
    				(r.NextSingle() - 0.5f) * range)), 22, false));
    		}
    	}
        void Terrain()
        {
    		var plate4x4 = Api.GetPart("3031");
    		var brick2x2 = Api.GetPart("3003");
        	Utils u = new Utils();
			for (int i = -10; i < 10; i++)
        	{   
	        	for (int j = -10; j < 10; j++)
	        	{
	        		int rd = (i + 10) * 12;
	        		int grn = (j + 10) * 12;
					Matrix4x4 mat = Matrix4x4.CreateTranslation(new Vector3(j*80-10, 0, i*80-10));      
	        		int c =Palette.GetClosestMatch(new Palette.RGB((byte)rd, (byte)grn, 10));
					var plate4x4part = new PartInst(plate4x4,
						mat, c, true );
	        		Api.AddUnconnected(plate4x4part);
	        		c =Palette.GetClosestMatch(new Palette.RGB((byte)rd, (byte)grn, 50));
					var brick2x2part = new PartInst(brick2x2,
						c, false );
	        		Api.Connect(brick2x2part, 5, plate4x4part, r.Next(0, 16), false);
	        		
				}
			}
								
        }
    }
}