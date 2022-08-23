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
			var item = LDrawFolders.GetCacheItem("3741");
			outparts.Add(new PartInst(item, mat, 10));
			var flower = LDrawFolders.GetCacheItem("33291");			
			var flowerlstud = flower.Connectors[1];
			foreach (var connector in item.Connectors)
			{			
				if (connector.Type == ConnectorType.Stem)
				{
					outparts.Add(new PartInst(flower, 
					Matrix4x4.CreateScale(1,1,-1) *
					Matrix4x4.CreateTranslation(new Vector3(0,15.0f,0)) * 
						flowerlstud.IM44 *connector.M44 * 
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
			var leftleg = LDrawFolders.GetCacheItem("3817");

			var rltohiprconnector = rightleg.Connectors[1];
			
			var hiprconnector = hip.Connectors[0];
			
			var hiplconnector = hip.Connectors[1];
			var lltohiprconnector = leftleg.Connectors[1];
			outparts.Add(new PartInst(rightleg, mat, 0));
			mat = hiprconnector.IM44 * rltohiprconnector.M44 * mat;
			outparts.Add(new PartInst(hip, mat, 0));
			outparts.Add(new PartInst(leftleg, lltohiprconnector.IM44 * hiplconnector.M44 * mat, 1));
			ScriptEngine.WriteLine("");
			foreach (var connector in hip.Connectors)
			{			
			ScriptEngine.WriteLine($"{connector.Type} {connector.Rotation}");
			}
			
		}
	}
}