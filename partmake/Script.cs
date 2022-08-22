using System;
using System.Collections.Generic;
using System.Numerics;
namespace partmake.script
{
    public class Script
    {
        Random r = new Random();
    	int []colors = new int[4] { 10, 510, 17, 74 };
    	int []flowercolors = new int[4] { 29, 15, 31, 4 };
    	string []blocks = new string[2] { "3070", "3024" };
        public void Run(List<PartInst> outparts, ScriptEngine engine)
        {            
        	for (int i = -10; i < 10; i++)
        	{
        	for (int j = -10; j < 10; j++)
        	{
        	Matrix4x4 mat = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY,(float)(r.NextDouble() * 3.14)) *
        		Matrix4x4.CreateTranslation(new Vector3(j*20, 8, i*20));        	
			outparts.Add(new PartInst("3741", mat, 10));
			var item = LDrawFolders.GetCacheItem("3741");
			foreach (var connector in item.Connectors)
			{			
				if (connector.Type == ConnectorType.Stem)
				{
					outparts.Add(new PartInst("33291", 
					Matrix4x4.CreateScale(1,1,-1) *
					Matrix4x4.CreateTranslation(new Vector3(0,-0.2f,0)) * 
						connector.Mat.ToM44() *
						mat, flowercolors[ r.Next(0,4)] ));
					
				}
			}
			}
			}
        }
    }
}