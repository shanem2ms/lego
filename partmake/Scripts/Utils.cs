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

			var rltohiprconnector = rightleg.Connectors[1];			
			var hiprconnector = hip.Connectors[1];			
			var hiplconnector = hip.Connectors[0];
			var lltohiprconnector = leftleg.Connectors[1];
			
			Api.Parts.Add(new PartInst(hip, mat, 1, true));
			//Api.Locators.Add(new Vector4(-10, 30, 0, 2));
			
			Matrix4x4 m2 =     
    			lltohiprconnector.IM44 *
    			hiplconnector.M44 * mat;
			Api.Parts.Add(new PartInst(leftleg, m2, 1, true));    			
    						
			Matrix4x4 m3 =     
    			rltohiprconnector.IM44 *
    			hiprconnector.M44 * mat;
			Api.Parts.Add(new PartInst(rightleg, m3, 1, true));    			
			Vector3 pt =
				Vector3.Transform(Vector3.Zero, hiprconnector.M44 * mat);
				
			var hiptorsorconnector = hip.Connectors[3];
			var torsorhipconnector = torso.Connectors[3];
			Matrix4x4 mtorso =
				torsorhipconnector.IM44 *
    			hiptorsorconnector.M44 * mat;
			Api.Parts.Add(new PartInst(torso, mtorso, 4));    
			var torsoleftarmconnector = torso.Connectors[2];
			var leftarmtorsoconnector = leftarm.Connectors[0];
			
			Matrix4x4 mleftarm =
				leftarmtorsoconnector.IM44 * 
    			torsoleftarmconnector.M44 * mtorso;
			Api.Parts.Add(new PartInst(leftarm, mleftarm, 4, true));    
			
			var leftarmwristconnector = leftarm.Connectors[1];
			var handwristconnector = hand.Connectors[0];
			Matrix4x4 mlefthand =
				handwristconnector.IM44 *
				leftarmwristconnector.M44 * mleftarm;			
			Api.Parts.Add(new PartInst(hand, mlefthand, 14, true));    
			
			var torsorightarmconnector = torso.Connectors[1];
			var rightarmtorsoconnector = rightarm.Connectors[0];
			Matrix4x4 mrightarm =
				rightarmtorsoconnector.IM44 * 
    			torsorightarmconnector.M44 * mtorso;
			Api.Parts.Add(new PartInst(rightarm, mrightarm, 4, true));  
			
			var torsoheadconnector = torso.Connectors[0];
			var headtorsoconnector = head.Connectors[0];
					Matrix4x4 mhead =
			headtorsoconnector.IM44 * 
			torsoheadconnector.M44 * mtorso;

			Api.Parts.Add(new PartInst(head, mhead, 14, true));  
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