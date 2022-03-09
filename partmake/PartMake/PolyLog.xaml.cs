using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.Text.RegularExpressions;

namespace partmake
{
    /// <summary>
    /// Interaction logic for PolyLog.xaml
    /// </summary>
    public partial class PolyLog : UserControl
    {
        public PolyLog()
        {
            InitializeComponent();
        }

        Timer t;
        private void PointsTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (t == null)
            {
                t = new Timer();
                t.Elapsed += T_Elapsed;
                t.Interval = 1000;
                t.AutoReset = false;
            }
            t.Stop();
            t.Start();
        }

        public string LogText { get => PointsTb.Text; set { PointsTb.Text = value; ProcessText();  } }


        public struct Point
        {
            public double x;
            public double y;

            public override string ToString()
            {
                return $"{x} {y}";
            }
        }

        public class Poly
        {
            public Color color;
            public List<Point> points = new List<Point>();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            DrawPolys();
            base.OnRenderSizeChanged(sizeInfo);
        }

        List<Poly> polys;
        void DrawPolys()
        {
            PolyCanvs.Children.Clear();
            if (polys.Count == 0)
                return;
            Point minpt = polys[0].points[0];
            Point maxpt = minpt;
            foreach (Poly p in polys)
            {
                foreach (Point pt in p.points)
                {
                    if (pt.x > maxpt.x) maxpt.x = pt.x;
                    if (pt.y > maxpt.y) maxpt.y = pt.y;
                    if (pt.x < minpt.x) minpt.x = pt.x;
                    if (pt.y < minpt.y) minpt.y = pt.y;
                }
            }

            double w = PolyCanvs.ActualWidth;
            double h = PolyCanvs.ActualHeight;

            double scalew = w / (maxpt.x - minpt.x);
            double scaleh = h / (maxpt.y - minpt.y);
            double scale = Math.Min(scalew, scaleh);
            foreach (Poly p in polys)
            {
                System.Windows.Shapes.Polygon poly = new System.Windows.Shapes.Polygon();
                for (int i = 0; i < p.points.Count; i++)
                {
                    poly.Points.Add(new System.Windows.Point(
                        (p.points[i].x - minpt.x) * scale,
                        (p.points[i].y - minpt.y) * scale));
                }
                poly.Fill = new SolidColorBrush(p.color);
                poly.StrokeThickness = 3;
                PolyCanvs.Children.Add(poly);
            }
            foreach (Poly p in polys)
            {
                for (int i = 0; i < p.points.Count; i++)
                {
                    Line line = new Line();

                    line.Stroke = SystemColors.WindowFrameBrush;
                    line.X1 = (p.points[i].x - minpt.x) * scale;
                    line.Y1 = (p.points[i].y - minpt.y) * scale;
                    int j = (i + 1) % p.points.Count;
                    line.X2 = (p.points[j].x - minpt.x) * scale;
                    line.Y2 = (p.points[j].y - minpt.y) * scale;

                    PolyCanvs.Children.Add(line);
                }
            }
        }

        Random r = new Random();
        Color? currentColor = null;
        byte alpha = 128;
        Color GetColor()
        {
            if (currentColor != null)
            {
                Color c = currentColor.Value;
                c.A = alpha;
                return c;
            }
            else
            {
                byte[] rgbvals = new byte[3];
                r.NextBytes(rgbvals);
                return Color.FromRgb(rgbvals[0],
                                   rgbvals[1],
                                   rgbvals[2]);
            }
        }
        Regex colorreg = new Regex(@"\[color=(\d+),(\d+),(\d+)\]");
        Regex alphareg = new Regex(@"\[alpha=(\d+)\]");
        void ProcessText()
        {
            try
            {
                string[] lines = PointsTb.Text.Split("\n");
                polys = new List<Poly>();
                Poly poly = null;
                foreach (string line in lines)
                {
                    if (line.Trim().Length == 0)
                    {
                        if (poly != null)
                        {
                            polys.Add(poly);
                            poly = null;
                        }
                    }
                    else if (line[0] == '[')
                    {
                        if (poly != null)
                        {
                            polys.Add(poly);
                            poly = null;
                        }
                        Match m = colorreg.Match(line);
                        if (m.Success)
                        {
                            currentColor = Color.FromArgb(255,
                                byte.Parse(m.Groups[1].Value),
                                byte.Parse(m.Groups[2].Value),
                                byte.Parse(m.Groups[3].Value));
                        }
                        m = alphareg.Match(line);
                        if (m.Success)
                        {
                            alpha = byte.Parse(m.Groups[1].Value);
                        }
                    }
                    else
                    {
                        if (poly == null)
                        {
                            poly = new Poly();
                            poly.color = GetColor();
                        }
                        string[] vals = line.Split(',');
                        poly.points.Add(
                             new Point()
                             {
                                 x = double.Parse(vals[0]),
                                 y = double.Parse(vals[1])
                             });
                    }
                }

                if (poly != null)
                    polys.Add(poly);
                DrawPolys();
            }
            catch (Exception ex)
            {
            }
        }
        private void T_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                ProcessText()));
        }
    }

}
