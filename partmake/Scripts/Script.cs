using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;

namespace partmake.script
{
    public class Script
    {
        public void Run()
        {                    	
    		Api.MouseHandler = MouseHandler;
        }
        
        void MouseHandler(LayoutVis.MouseCommand command, int btn, int X, int Y, Vector3 worldPos)
        {
        	if (command == LayoutVis.MouseCommand.ButtonDown)
        		Api.WriteLine($"{worldPos}");
        }
    }
}