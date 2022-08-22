using System;
using System.Collections.Generic;
using System.Numerics;

namespace partmake.script
{
	public class Utils
	{
    	int []flowercolors = new int[4] { 29, 15, 31, 4 };
        Random r = new Random();

		public void MakeFlower(List<PartInst> outparts, Matrix4x4 mat)
		{
			outparts.Add(new PartInst("3741", mat, 10));
			var item = LDrawFolders.GetCacheItem("3741");
			foreach (var connector in item.Connectors)
			{			
				if (connector.Type == ConnectorType.Stem)
				{
					outparts.Add(new PartInst("33291", 
					Matrix4x4.CreateScale(1,1,-1) *
					Matrix4x4.CreateTranslation(new Vector3(0,-0.2f,0)) * 
						connector.M44 *
						mat, flowercolors[ r.Next(0,4)] ));
					
				}
			}
		}
		

		public void Minifig(List<PartInst> outparts, Matrix4x4 parentMat)
		{
			Matrix4x4 mat = 
				Matrix4x4.CreateTranslation(new Vector3(0,30.2f,0)) * parentMat;
			var rightleg = LDrawFolders.GetCacheItem("3816");
			var hip = LDrawFolders.GetCacheItem("3815");

			var rltohiprconnector = rightleg.Connectors[1];
			var hipconnector = hip.Connectors[0];
			outparts.Add(new PartInst("3816", mat, 10));
			outparts.Add(new PartInst("3815", hipconnector.IM44 * rltohiprconnector.M44 *  mat, 10));
			ScriptEngine.WriteLine("");
			foreach (var connector in hip.Connectors)
			{			
			ScriptEngine.WriteLine(connector.ToString());				
			}
			
		}
	}
}