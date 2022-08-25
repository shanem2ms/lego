using System;
using System.Collections.Generic;
using System.Numerics;

namespace partmake.script
{
	public class Utils
	{
    	int []flowercolors = new int[4] { 29, 15, 31, 4 };
        Random r = new Random();

		public void MakeFlower(Matrix4x4 mat)
		{
			var item = Api.GetPart("3741");
			Api.Parts.Add(new PartInst(item, mat, 10));
			var flower = Api.GetPart("33291");			
			var flowerlstud = flower.Connectors[1];
			foreach (var connector in item.Connectors)
			{			
				if (connector.Type == ConnectorType.Stem)
				{
					Api.Parts.Add(new PartInst(flower, 
					Matrix4x4.CreateScale(1,1,-1) *
					Matrix4x4.CreateTranslation(new Vector3(0,15.0f,0)) * 
						flowerlstud.IM44 *connector.M44 * 
						mat, flowercolors[ r.Next(0,4)] ));
					
				}
			}
		}
		

		public void Minifig(Matrix4x4 parentMat)
		{
			Matrix4x4 mat = 
				Matrix4x4.CreateTranslation(new Vector3(0,0,0)) * parentMat;
			var rightleg = Api.GetPart("3816");
			var hip = Api.GetPart("3815");
			var leftleg = Api.GetPart("3817");

			var rltohiprconnector = rightleg.Connectors[1];			
			var hiprconnector = hip.Connectors[1];			
			var hiplconnector = hip.Connectors[0];
			var lltohiprconnector = leftleg.Connectors[1];
			
			Api.Parts.Add(new PartInst(hip, mat, 1));
			//Api.Locators.Add(new Vector4(-10, 30, 0, 2));
			
			Matrix4x4 m2 =     
    			lltohiprconnector.IM44 *
    			hiplconnector.M44 * mat;
			Api.Parts.Add(new PartInst(leftleg, m2, 1));    			
    						
			Matrix4x4 m3 =     
    			rltohiprconnector.IM44 *
    			hiprconnector.M44 * mat;
			Api.Parts.Add(new PartInst(rightleg, m3, 1));    			
			Vector3 pt =
				Vector3.Transform(Vector3.Zero, hiprconnector.M44 * mat);
		}
		
		void ConnectorTest()
		{
			var stud1side = Api.GetPart("87087");
			var stud4side = Api.GetPart("4733");
    		Matrix4x4 mat = 
    			Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));        		        	
    		Api.Parts.Add(new PartInst(stud4side,mat, 326));       
    		foreach (var stud in stud4side.ConnectorsWithType(ConnectorType.Stud))
    		{
				Matrix4x4 m2 =     
    			stud1side.Connectors[4].IM44 *
    			stud.M44 * mat;
    				Api.Parts.Add(new PartInst(stud1side,m2, 29));
			}
    		        		
    		
		}    		 
	}
}