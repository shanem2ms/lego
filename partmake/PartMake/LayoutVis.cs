using System;
using AssetPrimitives;
using SampleBase;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

namespace partmake
{
    public class LayoutVis : SampleApplication, IRenderVis
    {
        public bool IsActive { get; set; }
        public LayoutVis(ApplicationWindow window) : base(window)
        {
        }

        public void MouseDown(int btn, int X, int Y, Keys keys)
        {
        }

        public void MouseMove(int X, int Y, Keys keys)
        {
        }

        public void MouseUp(int btn, int X, int Y)
        {
        }

        protected override void CreateResources(ResourceFactory factory)
        {
        }

        protected override void Draw(float deltaSeconds)
        {
            if (!IsActive)
                return;
        }
    }



}
