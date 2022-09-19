using System;
using System.Collections.Generic;
using System.Numerics;

namespace partmake.script
{
	public class Utils
	{
    		
		public void Minifig(Matrix4x4 parentMat)
		{
			Matrix4x4 mat = 
				Matrix4x4.CreateTranslation(new Vector3(0,0,0)) * parentMat;
			var rightleg = Api.GetPart("3816");
			var hip = Api.GetPart("3815");
			var leftleg = Api.GetPart("3817");
			var torso = Api.GetPart("3814");
			var rightarm = Api.GetPart("3818");
			var leftarm = Api.GetPart("3819");
			var head = Api.GetPart("3626");
			var hand = Api.GetPart("3820");

			var hippart = new PartInst(hip, mat, 1, false);
			Api.Scene.AddUnconnected(hippart);
    		
    		var leftlegpart = new PartInst(leftleg, 1, false);
    		Api.Scene.Connect(leftlegpart, 1, hippart, 0, true);			

    		var rightlegpart = new PartInst(rightleg, 1, false);
    		Api.Scene.Connect(rightlegpart, 1, hippart, 1, true);			
				
			var torsopart = new PartInst(torso, 4);
    		Api.Scene.Connect(torsopart, 3, hippart, 3, true);		
    		Api.Scene.SetPlayerPart(torsopart);
    		
    		var leftarmpart = new PartInst(leftarm, 4, false);
    		Api.Scene.Connect(leftarmpart, 0, torsopart, 2, true);

    		var lefthandpart = new PartInst(hand, 14, false);    		
    		Api.Scene.Connect(lefthandpart, 0, leftarmpart, 1, true);
    		
			var rightarmpart = new PartInst(rightarm, 4, false);
    		Api.Scene.Connect(rightarmpart, 0, torsopart, 1, true);
    		
    		var righthandpart = new PartInst(hand, 14, false);    		
    		Api.Scene.Connect(righthandpart, 0, rightarmpart, 1, true);

    		var headpart = new PartInst(head, 14, false);    		
    		Api.Scene.Connect(headpart, 0, torsopart, 0, true);
		}
		
		void ConnectorTest()
		{
			var stud1side = Api.GetPart("87087");
			var stud4side = Api.GetPart("4733");
    		Matrix4x4 mat = 
    			Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));        		        	
    		Api.Scene.AddUnconnected(new PartInst(stud4side,mat, 326));       
    		foreach (var stud in stud4side.ConnectorsWithType(ConnectorType.Stud))
    		{
				Matrix4x4 m2 =     
    			stud1side.Connectors[4].IM44 *
    			stud.M44 * mat;
    				Api.Scene.AddUnconnected(new PartInst(stud1side,m2, 29));
			}
    		        		
    		
		}    		 
	}
}