using System;
using System.Windows.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Numerics;

namespace partmake
{
    public class Palette
    {
        public class Item
        {
            public string name;
            public int index;
            public byte r;
            public byte g;
            public byte b;
            public byte a;
            public float h;
            public float s;
            public float l;
            public string mat;

            public string Name => name;
            public int Index => index;
            Brush brush;
            public string HSL => $"{h} {s} {l}";
            public Brush Brush { get { if (brush == null) brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)); return brush; } }
            public Vector4 RGBA;
        }
        static public Dictionary<int, Item> AllItems = new Dictionary<int, Item>();

        // 0\s!COLOUR\s(\w+)\s+CODE\s+(\d+)\s+VALUE\s#([\dA-F]+)\s+EDGE\s+#([\dA-F]+)\s+((ALPHA)\s+(\d+))?(\w+)?
        static public List<Item> SortedItems = new List<Item>();
        static public List<Item> SolidColors = new List<Item>();
        public static void LoadColors(string rootFolder)
        {
            string lDConfigPath = Path.Combine(rootFolder, "LDConfig.ldr");
            string[] lines = File.ReadAllLines(lDConfigPath);
            Regex colorrg = new Regex("0\\s!COLOUR\\s(\\w+)\\s+CODE\\s+(\\d+)\\s+VALUE\\s#([\\dA-F]+)\\s+EDGE\\s+#([\\dA-F]+)\\s*((ALPHA)\\s+(\\d+))?(\\w+)?");
            Regex legoidrg = new Regex("0\\s+\\/\\/\\sLEGOID\\s+(\\d+)\\s-\\s([\\w\\s]+)");
            int legoidCur;
            string legoNameCur;
            foreach (var line in lines)
            {
                Match m = legoidrg.Match(line);
                Match m2 = colorrg.Match(line);
                if (m.Success)
                {
                    legoidCur = int.Parse(m.Groups[1].Value);
                    legoNameCur = m.Groups[2].Value;
                }
                else if (m2.Success)
                {
                    string name = m2.Groups[1].Value;
                    int index = int.Parse(m2.Groups[2].Value);
                    uint fill = uint.Parse(m2.Groups[3].Value, System.Globalization.NumberStyles.HexNumber);
                    uint edge = uint.Parse(m2.Groups[4].Value, System.Globalization.NumberStyles.HexNumber);
                    byte r = (byte)((fill >> 16) & 0xFF);
                    byte g = (byte)((fill >> 8) & 0xFF);
                    byte b = (byte)(fill & 0xFF);
                    byte a = 255;
                    string mat = "";
                    if (m2.Groups[6].Value == "ALPHA")
                    {
                        a = byte.Parse(m2.Groups[7].Value);
                    }
                    else if (m2.Groups[8].Value.Length > 0)
                    {
                        mat = m2.Groups[8].Value;
                    }
                    byte h;
                    byte s;
                    byte l;
                    HSL hsl = Palette.RGBToHSL(new RGB { R = r, G = g, B = b });

                    Item c = new Item()
                    {
                        index = index,
                        name = name,
                        r = r,
                        g = g,
                        b = b,
                        a = a,
                        h = hsl.H,
                        s = hsl.S,
                        l = hsl.L,
                        mat = mat,
                        RGBA = new Vector4((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f, (float)a / 255.0f)
                    };
                    AllItems.Add(index, c);
                }
            }

            SortedItems.AddRange(AllItems.Values.Where(a => a.mat.Length == 0));
            SortedItems.Sort((a, b) => {
                int trn = b.a - a.a;
                if (trn != 0)
                    return trn;
                int isbwl = (a.s < 0.1f) ? 1 : 0;
                int isbwr = (b.s < 0.1f) ? 1 : 0;
                int r = isbwr - isbwl;
                if (r != 0) return r;
                return (int)((b.h - a.h) * 360.0f);
                    });

            SolidColors = AllItems.Values.Where(a => a.mat.Length == 0 && a.a == 255).ToList();
        }

    
        public struct RGB
        {
            public RGB(byte r, byte g, byte b)
            {
                R = r;
                G = g;
                B = b;
            }

            public byte R;
            public byte G;
            public byte B;
        }

        public struct HSL
        {
            public float H;
            public float S;
            public float L;
        }

        public static int GetClosestMatch(RGB rgb)

        {
            return GetClosestMatch(rgb, Vector3.One);
        }
        public static int GetClosestMatch(RGB rgb,
            Vector3 hslWeights)
        {
            HSL hsl = RGBToHSL(rgb);
            float mindist = 1e10f;
            int minitem = -1;            
            foreach (var c in SolidColors)
            {
                float dist = (c.h - hsl.H) * (c.h - hsl.H) * hslWeights.X +
                    (c.s - hsl.S) * (c.s - hsl.S) * hslWeights.Y +
                    (c.l - hsl.L) * (c.l - hsl.L) * hslWeights.Z;
                if (dist < mindist)
                {
                    mindist = dist;
                    minitem = c.index;
                }
            }

            return minitem;
        }
        public static HSL RGBToHSL(RGB rgb)
        {
            HSL hsl = new HSL();

            float r = (rgb.R / 255.0f);
            float g = (rgb.G / 255.0f);
            float b = (rgb.B / 255.0f);

            float min = Math.Min(Math.Min(r, g), b);
            float max = Math.Max(Math.Max(r, g), b);
            float delta = max - min;

            hsl.L = (max + min) / 2;

            if (delta == 0)
            {
                hsl.H = 0;
                hsl.S = 0.0f;
            }
            else
            {
                hsl.S = (hsl.L <= 0.5) ? (delta / (max + min)) : (delta / (2 - max - min));

                float hue;

                if (r == max)
                {
                    hue = ((g - b) / 6) / delta;
                }
                else if (g == max)
                {
                    hue = (1.0f / 3) + ((b - r) / 6) / delta;
                }
                else
                {
                    hue = (2.0f / 3) + ((r - g) / 6) / delta;
                }

                if (hue < 0)
                    hue += 1;
                if (hue > 1)
                    hue -= 1;

                hsl.H = hue;
            }

            return hsl;
        }


    }
}
