using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;

namespace partmake.script
{
    public class Script
    {
        Random r = new Random();
    	int []colors = new int[4] { 288, 2, 74, 6 };
		VoxelVis vox = new VoxelVis();
		TerrainGen tg;

        public void Run()
        {                    	
    		Api.CustomDraw = Draw;
    		Api.MouseHandler = MouseHandler;
        }
        
        void MouseHandler(LayoutVis.MouseCommand command, int btn, Vector2 screenPos, 
            Vector3 w0, Vector3 w1, ref Matrix4x4 viewProj)
        {
        	if (command == LayoutVis.MouseCommand.ButtonDown)
        		Api.WriteLine($"{w0}");
        }
        
        bool doTerrainGen = true;
        public void Draw(CommandList cl, ref Matrix4x4 viewmat, ref Matrix4x4 projMat)
        {
        	if (doTerrainGen)
        	{
        	if (tg == null)
        	{
        		tg = new TerrainGen();
        		tg.Gen();
	    		vox.Gen(tg.TerrainTexView);
        		tg.Draw(cl, ref viewmat, ref projMat);
        	}
        	else
        	{
        		var cputex = tg.CpuTex;
        		cputex.Map();
    			Utils u = new Utils();        		
	    		Api.Scene.BeginUpdate(true);
	    		//u.Minifig(Matrix4x4.CreateTranslation(new Vector3(8, 208, 0)));
	    		Terrain(cputex.Data, cputex.Width, cputex.Height);  
	    		Api.Scene.EndUpdate();
        		
        		doTerrainGen = false;	
        	}
        	}
        	//vox.Draw(cl, ref viewmat, ref projMat);
        }
        
        void Rain()
        {
			var flower = Api.GetPart("24866");
    		float range = 500;
    		for (int i = 0; i < 55; ++i)
    		{
    			Api.Scene.AddUnconnected(new PartInst(flower, 
    				Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, r.NextSingle() * MathF.PI) * 
    				Matrix4x4.CreateTranslation(new Vector3((r.NextSingle() - 0.5f) * range, 
    					r.NextSingle() * 350, 
    				(r.NextSingle() - 0.5f) * range)), 22, false));
    		}
    	}
        void Terrain(TerrainGen.TerrainVal[] terrainData, uint w, uint h)
        {
    		var plate4x4 = Api.GetPart("3031");
    		var brick2x2 = Api.GetPart("3003");
    		var plate1x1 = Api.GetPart("3024");
    		var slope1x1 = Api.GetPart("54200");
        	Utils u = new Utils();
        	int div = 8;
        	int cnt = 1024/div;
        	int c = 71;
        	for (int y = 0; y < cnt/1; ++y)
        	{
        		for (int x = 0; x < cnt/1; ++x)
        		{
        			int indexx = ((x + 1) * div - 1) * (int)w + (int)(y * div);
        			int indexy = (x * div) * (int)w + (int)((y + 1) * div - 1);
        			int index = (x * div) * (int)w + (int)(y * div);
        			float height = terrainData[index].a  * 250;
        			float heightx = terrainData[indexx].a  * 250;
        			float heighty = terrainData[indexy].a  * 250;
        			float dx = height - heightx;
        			float dy = height - heighty;
        			float rot = 0;
        			c = 71;
        			var part = slope1x1;
        			if (MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) < 5)
        			{
        				part = plate1x1;
        			}
        			else if (MathF.Abs(dx) > MathF.Abs(dy))
        			{
        				c = 23;
        				if (dx > 0) { 
        					c = 288;
        					rot = MathF.PI;
        				}
        			}
        			else
        			{    
        				c = 450;
        				if (dy > 0)
        				{
        					c = 5;
        				}
        				rot = -MathF.Sign(dy) * MathF.PI * 0.5f;
        			}
        			
        			height = MathF.Round(height / 8.0f) * 8.0f;
					Matrix4x4 mat = Matrix4x4.CreateRotationY(rot) *
						Matrix4x4.CreateTranslation(new Vector3((y - cnt/2)*20-10, height, (x-cnt/2)*20-10));
					var plate1x1part = new PartInst(part,
						mat, c, true );
					Api.Scene.AddUnconnected(plate1x1part);
        		}
        	}
        	/*
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
	        		Api.Scene.AddUnconnected(plate4x4part);
	        		c =Palette.GetClosestMatch(new Palette.RGB((byte)rd, (byte)grn, 50));
					var brick2x2part = new PartInst(brick2x2,
						c, false );
	        		Api.Scene.Connect(brick2x2part, 5, plate4x4part, r.Next(0, 16), false);
	        		
				}
			}*/			
								
        }
    }
}