﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace constrained_delaunay_triangulation
{
    public class pslg_datastructure
    {
        public pslg_datastructure()
        {
            // Empty constructor 
        }

        public void select_and_set_mesh(double x, double y, bool is_create)
        {
            // this is a call to set or delete the mesh
            int surf_index = -1; //variable to store the index
            foreach (surface_store surf in all_surfaces)
            {
                if (surf.pointinsurface(x, y) == true) // find the surface where the point lies
                {
                    surf_index = all_surfaces.FindIndex(obj => obj.surface_id == surf.surface_id); // add the index of the surface to the main index;
                    break;
                }
            }

            List<int> inner_surf_index = new List<int>(); // variable to store inner surface index
            if (surf_index != -1) // if not equal to -1 then surface is found
            {
                foreach (surface_store inner_surf in all_surfaces[surf_index].inner_surfaces)
                {
                    inner_surf_index.Add(all_surfaces.FindIndex(obj => obj.surface_id == inner_surf.surface_id)); // add the index of inner surfaces to the list
                }
            }
            else
            {
                return; //exit the function if no surfaces are found
            }

            if (is_create == true) // create the mesh
            {
                constrained_delaunay_algorithm.create_constrained_mesh(surf_index, inner_surf_index, ref all_surfaces);
            }
            else // delete the mesh
            {
                constrained_delaunay_algorithm.delete_constrained_mesh(surf_index, inner_surf_index, ref all_surfaces);
            }
        }

        public void paint_me(ref Graphics gr1)
        {
            Graphics gr0 = gr1;

            all_surfaces.ForEach(obj => obj.paint_me(ref gr0)); // Paint the surface and its associated mesh
        }

        public List<surface_store> all_surfaces = new List<surface_store>(); // List of all detected surfaces (visible outside)

        public class mesh2d
        {
            List<point2d> _all_points = new List<point2d>(); // List of point object to store all the points in the drawing area
            List<edge2d> _all_edges = new List<edge2d>(); // List of edge object to store all the edges created from Delaunay triangulation
            List<triangle2d> _all_triangles = new List<triangle2d>(); // List of face object to store all the faces created from Delaunay triangulation

            public List<point2d> all_points
            {
                get { return this._all_points; }
            }

            public List<edge2d> all_edges
            {
                get { return this._all_edges; }
            }

            public List<triangle2d> all_triangles
            {
                get { return this._all_triangles; }
            }

            public mesh2d()
            {
                // Empty constructor
            }

            public mesh2d(List<point2d> i_all_pts, List<edge2d> i_all_edg, List<triangle2d> i_all_fcs)
            {
                this._all_points = i_all_pts;
                this._all_edges = i_all_edg;
                this._all_triangles = i_all_fcs;
            }

            public void paint_me(ref Graphics gr0) // this function is used to paint the mesh
            {
                Graphics gr1 = gr0;

                Pen temp_edge_pen = new Pen(Color.DarkOrange, 1);
                all_edges.ForEach(obj => obj.paint_me(ref gr1, ref temp_edge_pen)); // Paint the edges

                Pen temp_node_pen = new Pen(Color.DarkRed, 2);
                all_points.ForEach(obj => obj.paint_me(ref gr1, ref temp_node_pen)); // Paint the nodes

                Pen temp_tri_pen = new Pen(Color.LightGreen, 1);
                all_triangles.ForEach(obj => obj.paint_me(ref gr1, ref temp_tri_pen)); // Paint the faces
            }
        }

        public class surface_store
        {
            // surface variables
            private int _surface_id;
            public List<point2d> surface_nodes = new List<point2d>();
            public List<point2d> inner_nodes = new List<point2d>();

            public List<edge2d> surface_edges = new List<edge2d>();
            private int _encapsulating_surface_id;
            public List<edge2d> encapsulating_seed_edges = new List<edge2d>();
            public List<surface_store> inner_surfaces = new List<surface_store>();

            // mesh variable
            public bool is_meshed;
            public mesh2d my_mesh = new mesh2d();

            // Drawing aid for the surface
            System.Drawing.Drawing2D.HatchBrush tri_brush;
            private System.Drawing.Region surface_region = new System.Drawing.Region();

            // Surface area
            public double surface_area;

            public int surface_id
            {
                get { return this._surface_id; }
            }

            public int encapsulating_surface_id
            {
                get { return this._encapsulating_surface_id; }
            }

            public surface_store(int i_surf_id, List<point2d> i_surface_nodes, List<edge2d> i_surface_edges, int surf_count)
            {

                this._surface_id = i_surf_id; // add surface id
                surface_nodes.AddRange(i_surface_nodes);
                surface_edges.AddRange(i_surface_edges);
                is_meshed = false;
                _encapsulating_surface_id = -1;

                List<PointF> temp_sur_pts = new List<PointF>();

                temp_sur_pts.Add(this.surface_edges[0].start_pt.get_point()); // Add the first point of the surface edge
                foreach (edge2d ed in this.surface_edges)
                {
                    temp_sur_pts.Add(ed.end_pt.get_point()); // since all the surface edges are interconnected only store the end points
                }

                // Set the path of outter surface
                System.Drawing.Drawing2D.GraphicsPath surface_path = new System.Drawing.Drawing2D.GraphicsPath();
                surface_path.StartFigure();
                surface_path.AddPolygon(temp_sur_pts.ToArray());
                surface_path.CloseFigure();

                // set region
                surface_region = new Region(surface_path);

                // set the hatch brush
                Color hatch_color = the_static_class.GetRandomColor(surf_count);
                System.Drawing.Drawing2D.HatchStyle hatch_style = the_static_class.GetRandomHatchStyle(surf_count);
                Color trans_color = Color.FromArgb(0, 10, 10, 10);

                tri_brush = new System.Drawing.Drawing2D.HatchBrush(hatch_style, hatch_color, trans_color);

                //set the area
                surface_area = Math.Abs(this.signedpolygonarea());
            }

            public void reverse_surface_orinetation()
            {
                List<edge2d> temp_edge_list = new List<edge2d>();

                for (int i = this.surface_edges.Count - 1; i >= 0; i--) // reverse the list
                {
                    temp_edge_list.Add(new edge2d(this.surface_edges[i].edge_id, this.surface_edges[i].end_pt, this.surface_edges[i].start_pt));
                }

                // clear the edge list
                this.surface_edges = new List<edge2d>();
                this.surface_edges.AddRange(temp_edge_list);
            }

            public void set_inner_surfaces(surface_store i_inner_surface)
            {

                inner_surfaces.Add(i_inner_surface);
   
                // Set the path of inner surface
                List<PointF> temp_sur_pts = new List<PointF>();
                foreach (edge2d ed in inner_surfaces[inner_surfaces.Count - 1].surface_edges)
                {
                    temp_sur_pts.Add(ed.end_pt.get_point());
                }

                System.Drawing.Drawing2D.GraphicsPath inner_surface = new System.Drawing.Drawing2D.GraphicsPath();
                inner_surface.StartFigure();
                inner_surface.AddPolygon(temp_sur_pts.ToArray());
                inner_surface.CloseFigure();

                // set region
                surface_region.Exclude(inner_surface); // exclude the inner surface region
            }

            public void set_encapsulating_surface(surface_store outter_surface)
            {
                this._encapsulating_surface_id = outter_surface.surface_id;
            }

            public void paint_me(ref Graphics gr0) // this function is used to paint the points
            {
                // gr0.FillRegion(tri_brush, surface_region);// Fill the surface region

                // Paint the surface ID and inner surface ids and surface area
                /*
                String txt = "surface " + this.surface_id.ToString() + " " + ((inner_surfaces.Count > 0) ? "=>" : "");
                foreach (surface_store inner_surf in inner_surfaces)
                {
                    txt = txt + inner_surf.surface_id.ToString() + ",";
                }
                txt = (inner_surfaces.Count > 0) ? txt.Remove(txt.Length - 1) : txt;
                txt = txt + ((encapsulating_surface_id != -1) ? "(" + encapsulating_surface_id.ToString() + ")" : "");


                Font drawFont = new Font("Arial", 16);
                gr0.DrawString(txt, drawFont, new SolidBrush(Color.Black), surface_nodes[0].get_point());
                */

                Graphics gr1 = gr0;

                Pen temp_edge_pen = new Pen(Color.DarkBlue, 2);
                surface_edges.ForEach(obj => obj.paint_me(ref gr1, ref temp_edge_pen)); // Paint the edges

                Pen temp_node_pen = new Pen(Color.BlueViolet, 2);
                surface_nodes.ForEach(obj => obj.paint_me(ref gr1, ref temp_node_pen)); // Paint the faces

                my_mesh.paint_me(ref gr1);
            }

            // Return the polygon's area in "square units."
            // The value will be negative if the polygon is
            // oriented clockwise.
            public double signedpolygonarea()
            {
                //The total calculated area is negative if the polygon is oriented clockwise

                // Extract point
                List<PointF> polygon_pt = new List<PointF>();
                foreach (edge2d ed in this.surface_edges)
                {
                    polygon_pt.Add(ed.end_pt.get_point());
                }

                // Return the result.
                return the_static_class.SignedPolygonArea(polygon_pt.ToArray());
            }

            // Return True if the point is in the polygon (outside edges of the surface).
            // Note this will return if the point is inside the outside edges of the surface (use point in surface to find the points in surface)
            public bool pointinpolygon(double X, double Y)
            {
                // Extract point
                List<PointF> polygon_pt = new List<PointF>();
                //polygon_pt.Add(this.surface_edges[0].start_pt.get_point());
                foreach (edge2d ed in this.surface_edges)
                {
                    polygon_pt.Add(ed.end_pt.get_point());
                }

                PointF test_pt = new PointF((float)X, (float)Y);

                return the_static_class.IsPointInPolygon(polygon_pt.ToArray(), test_pt);
            }

            // Return true if the point is in the surface
            public bool pointinsurface(double X, double Y)
            {
                if (pointinpolygon(X, Y) == true)
                {
                    foreach (surface_store inner_surf in inner_surfaces)
                    {
                        if (inner_surf.pointinpolygon(X, Y) == true) // the point lies in inner surface so not the selected surface
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public class point2d // class to store the points
        {
            int _id;
            double _x;
            double _y;

            public int id
            {
                get { return this._id; }
            }

            public double x
            {
                get { return this._x; }
            }
            public double y
            {
                get { return this._y; }
            }
            public point2d(int i_id, double i_x, double i_y)
            {
                // constructor 1
                this._id = i_id;
                this._x = i_x;
                this._y = i_y;
            }

            public void paint_me(ref Graphics gr0, ref Pen node_pen) // this function is used to paint the points
            {
                gr0.FillEllipse(node_pen.Brush, new RectangleF(get_point_for_ellipse(), new SizeF(4, 4)));

                if (the_static_class.ispaint_label == true)
                {
                    string my_string = (this.id + 1).ToString() + "(" + this._x.ToString("F2") + ", " + this._y.ToString("F2") + ")";
                    SizeF str_size = gr0.MeasureString(my_string, new Font("Cambria", 6)); // Measure string size to position the dimension

                    gr0.DrawString(my_string, new Font("Cambria", 6),
                                                                       new Pen(Color.DarkBlue, 2).Brush,
                                                                       get_point_for_ellipse().X + 3 + the_static_class.to_single(-str_size.Width * 0.5),
                                                                       the_static_class.to_single(str_size.Height * 0.5) + get_point_for_ellipse().Y + 3);
                }
            }

            public PointF get_point_for_ellipse()
            {
                return (new PointF(the_static_class.to_single(this._x) - 2,
               the_static_class.to_single((this._y) - 2))); // return the point as PointF as edge of an ellipse
            }

            public PointF get_point()
            {
                return (new PointF(the_static_class.to_single(this._x),
               the_static_class.to_single(this._y))); // return the point as PointF as edge of an ellipse
            }

            public bool Equals(point2d other)
            {
                return (this._x == other.x && this._y == other.y); // Equal function is used to check the uniqueness of the points added
            }

            public bool is_close_enough(point2d other)
            {
                double eps = 0.001;
                return (Math.Abs(this._x - other.x) < eps && Math.Abs(this._y - other.y) < eps);
            }
        }

        public class points_equality_comparer : IEqualityComparer<point2d>
        {
            public bool Equals(point2d a, point2d b)
            {
                return (a.Equals(b));
            }

            public int GetHashCode(point2d other)
            {
                return (other.x.GetHashCode() * 17 + other.y.GetHashCode() * 19);
                // 17,19 are just ranfom prime numbers
            }
        }

        public class edge2d
        {
            int _edge_id;
            point2d _start_pt;
            point2d _end_pt;
            point2d _mid_pt; // not stored in point list

            public int edge_id
            {
                get { return this._edge_id; }
            }

            public point2d start_pt
            {
                get { return this._start_pt; }
            }

            public point2d end_pt
            {
                get { return this._end_pt; }
            }

            public point2d mid_pt
            {
                get { return this._mid_pt; }
            }

            public double edge_length
            {
                get { return Math.Sqrt(Math.Pow(_start_pt.x - _end_pt.x, 2) + Math.Pow(_start_pt.y - _end_pt.y, 2)); }
            }

            public edge2d(int i_edge_id, point2d i_start_pt, point2d i_end_pt)
            {
                // constructor 1
                this._edge_id = i_edge_id;
                this._start_pt = i_start_pt;
                this._end_pt = i_end_pt;
                this._mid_pt = new point2d(-1, (i_start_pt.x + i_end_pt.x) * 0.5, (i_start_pt.y + i_end_pt.y) * 0.5);
            }

            public void paint_me(ref Graphics gr0, ref Pen edge_pen) // this function is used to paint the points
            {
                gr0.DrawLine(edge_pen, start_pt.get_point(), end_pt.get_point());
                //gr0.DrawLine(new Pen(edge_pen.Color,edge_pen.Width),mid_pt.get_point(), end_pt.get_point());

                //System.Drawing.Drawing2D.AdjustableArrowCap bigArrow = new System.Drawing.Drawing2D.AdjustableArrowCap(3, 3);
                //edge_pen.CustomEndCap = bigArrow;
                //gr0.DrawLine(edge_pen, start_pt.get_point(), mid_pt.get_point());
            }

            public bool Equals(edge2d other)
            {
                return (other.start_pt.Equals(this._start_pt) && other.end_pt.Equals(this._end_pt));
            }

            public bool Equals_without_orientation(edge2d other)
            {
                return (other.start_pt.Equals(this._start_pt) && other.end_pt.Equals(this._end_pt) || other.start_pt.Equals(this._end_pt) && other.end_pt.Equals(this._start_pt));
            }

            public bool vertex_exists(point2d other)
            {

                if (start_pt.Equals(other) == true || end_pt.Equals(other) == true)
                {
                    return true;
                }
                return false;
            }

        }

        public class triangle2d
        {
            int _face_id;
            public point2d[] vertices { get; } = new point2d[3];

            point2d _mid_pt;
            double shrink_factor = 0.6f;


            public int face_id
            {
                get { return this._face_id; }
            }

            public point2d face_mid_pt
            {
                get
                {
                    return this._mid_pt;
                }
            }

            public PointF get_p1
            {
                get
                {
                    return new PointF(the_static_class.to_single(_mid_pt.get_point().X * (1 - shrink_factor) + (vertices[0].get_point().X * shrink_factor)),
                                       the_static_class.to_single(_mid_pt.get_point().Y * (1 - shrink_factor) + (vertices[0].get_point().Y * shrink_factor)));
                }
            }

            public PointF get_p2
            {
                get
                {
                    return new PointF(the_static_class.to_single(_mid_pt.get_point().X * (1 - shrink_factor) + (vertices[1].get_point().X * shrink_factor)),
                                      the_static_class.to_single(_mid_pt.get_point().Y * (1 - shrink_factor) + (vertices[1].get_point().Y * shrink_factor)));
                }
            }

            public PointF get_p3
            {
                get
                {
                    return new PointF(the_static_class.to_single(_mid_pt.get_point().X * (1 - shrink_factor) + (vertices[2].get_point().X * shrink_factor)),
                                      the_static_class.to_single(_mid_pt.get_point().Y * (1 - shrink_factor) + (vertices[2].get_point().Y * shrink_factor)));
                }
            }

            public triangle2d(int i_face_id, point2d i_p1, point2d i_p2, point2d i_p3)
            {
                this._face_id = i_face_id;
                if (!IsCounterClockwise(i_p1, i_p2, i_p3))
                {
                    this.vertices[0] = i_p1;
                    this.vertices[1] = i_p3;
                    this.vertices[2] = i_p2;
                }
                else
                {
                    this.vertices[0] = i_p1;
                    this.vertices[1] = i_p2;
                    this.vertices[2] = i_p3;
                }

                this._mid_pt = new point2d(-1, (i_p1.x + i_p2.x + i_p3.x) / 3.0f, (i_p1.y + i_p2.y + i_p3.y) / 3.0f);
            }

            private bool IsCounterClockwise(point2d point1, point2d point2, point2d point3)
            {
                double result = (point2.x - point1.x) * (point3.y - point1.y) -
                    (point3.x - point1.x) * (point2.y - point1.y);
                return result > 0;
            }

            public bool edge_exists(edge2d other)
            {
                edge2d edge_1 = new edge2d(-1, this.vertices[0], this.vertices[1]);
                edge2d edge_2 = new edge2d(-1, this.vertices[1], this.vertices[2]);
                edge2d edge_3 = new edge2d(-1, this.vertices[2], this.vertices[0]);

                if (edge_1.Equals_without_orientation(other) == true || edge_2.Equals_without_orientation(other) == true || edge_3.Equals_without_orientation(other) == true)
                {
                    return true;
                }
                return false;
            }

            public void paint_me(ref Graphics gr0, ref Pen face_pen) // this function is used to paint the points
            {
                //Pen triangle_pen = new Pen(Color.LightGreen, 1);

                if (the_static_class.is_paint_mesh == true)
                {
                    PointF[] curve_pts = { get_p1, get_p2, get_p3 };
                    gr0.FillPolygon(face_pen.Brush, curve_pts); // Fill the polygon

                    if (the_static_class.ispaint_label == true)
                    {
                        string my_string = this._face_id.ToString();
                        SizeF str_size = gr0.MeasureString(my_string, new Font("Cambria", 6)); // Measure string size to position the dimension

                        gr0.DrawString(my_string, new Font("Cambria", 6), new Pen(Color.DeepPink, 2).Brush, this._mid_pt.get_point());

                    }
                }

                //if (the_static_class.is_paint_incircle == true)
                //{
                //    if (circumradius_shortest_edge_ratio > the_static_class.B_var)
                //    {
                //        gr0.DrawEllipse(new Pen(Color.Black, 2), the_static_class.to_single(this._ellipse_edge.get_point().X + 2 - this._circle_radius),
                //                                  the_static_class.to_single(this._ellipse_edge.get_point().Y + 2 - this._circle_radius),
                //                                     the_static_class.to_single(this._circle_radius * 2),
                //                                     the_static_class.to_single(this._circle_radius * 2));
                //    }
                //}

            }

            //public void paint_circumcenter(ref Graphics gr0, ref Pen face_pen) // this function is used to paint the circumcenter
            //{
            //    if (circumradius_shortest_edge_ratio > the_static_class.B_var)
            //    {
            //        gr0.FillEllipse(face_pen.Brush, this._ellipse_edge.get_point().X,
            //                                     this._ellipse_edge.get_point().Y,
            //                                     the_static_class.to_single(2 * 2),
            //                                     the_static_class.to_single(2 * 2));
            //    }

            //}


        }
    }
    public static class the_static_class
    {
        public static SizeF bounding_box;
        public static PointF bounding_midpt;

        public static bool ispaint_label = false; // static variable to control whether to paint id or not

        public static bool is_animate_checked = false; // static variable to control animation timing;
        public static bool is_paint_incircle = false;
        public static bool is_paint_mesh = true;

        public static int instance_counter_at; // static variable to control instances count
        public static int inpt_timer_interval = 500; // 0.5 seconds, static variable to control the interval of the timer

        public static double B_var = Math.Sqrt(2); // Ruppert's algorithm condition B_var = Math.Sqrt(2) && Paul chew's second algorithm condition (Math.Sqrt(5) * 0.5)

        /// <summary>
                    /// Function to Check the valid of Numerical text from textbox.text
                    /// </summary>
                    /// <param name="tB_txt">Textbox.text value</param>
                    /// <param name="Negative_check">Is negative number Not allowed (True) or allowed (False)</param>
                    /// <param name="zero_check">Is zero Not allowed (True) or allowed (False)</param>
                    /// <returns>Return the validity (True means its valid) </returns>
                    /// <remarks></remarks>
        public static bool test_a_textboxvalue_validity_int(string tb_txt, bool n_chk, bool z_chk)
        {
            bool is_valid = false;
            //This function returns false if the textbox doesn't contains number 
            if (Int32.TryParse(tb_txt, out Int32 number) == true)
            {
                is_valid = true;

                if (n_chk == true) // check for negative number
                {
                    if (Convert.ToInt32(tb_txt) < 0)
                    {
                        is_valid = false;
                    }
                }

                if (z_chk == true) // check for zero number
                {
                    if (Convert.ToInt32(tb_txt) == 0)
                    {
                        is_valid = false;
                    }
                }
            }
            return is_valid;
        }

        /// <summary>
                    /// Function to convert double to single (mostly used in System.Drawing functions)
                    /// </summary>
                    /// <param name="value"></param>
                    /// <returns></returns>
        public static float to_single(double value)
        {
            return (float)value;
        }

        /// <summary>
                    /// Funtion to check NAN or Infinity value
                    /// </summary>
                    /// <param name="chkval"></param>
                    /// <returns></returns>
        public static bool Isval_NAN_or_Infinity(double chkval)
        {
            return (double.IsNaN(chkval) || double.IsInfinity(chkval));
        }

        public static Color GetRandomColor(int hash)
        {
            //Random randonGen = new Random(DateTime.Now.Millisecond.GetHashCode());
            Random randomGen = new Random((hash + 19) * DateTime.Now.Millisecond.GetHashCode());
            Color randomColor = Color.FromArgb(randomGen.Next(0, 256), randomGen.Next(0, 256), randomGen.Next(0, 256));
            return randomColor;
        }

        public static System.Drawing.Drawing2D.HatchStyle GetRandomHatchStyle(int hash)
        {
            //Random randomGen = new Random(hash.GetHashCode());
            //int randomhatchindex = randomGen.Next(0, 6);
            int randomhatchindex = hash > 6 ? 0 : hash;
            System.Drawing.Drawing2D.HatchStyle style = new System.Drawing.Drawing2D.HatchStyle();

            switch (randomhatchindex)
            {
                case 0:
                    style = System.Drawing.Drawing2D.HatchStyle.BackwardDiagonal;
                    break;
                case 1:
                    style = System.Drawing.Drawing2D.HatchStyle.DashedVertical;
                    break;
                case 2:
                    style = System.Drawing.Drawing2D.HatchStyle.Cross;
                    break;
                case 3:
                    style = System.Drawing.Drawing2D.HatchStyle.DiagonalCross;
                    break;
                case 4:
                    style = System.Drawing.Drawing2D.HatchStyle.HorizontalBrick;
                    break;
                case 5:
                    style = System.Drawing.Drawing2D.HatchStyle.LightDownwardDiagonal;
                    break;
                case 6:
                    style = System.Drawing.Drawing2D.HatchStyle.LightUpwardDiagonal;
                    break;
                default:
                    break;
            }

            return style;
        }

        // Return the angle ABC.
        // Return a value between PI and -PI.
        // Note that the value is the opposite of what you might
        // expect because Y coordinates increase downward.
        public static double GetAngle(double Ax, double Ay,
            double Bx, double By, double Cx, double Cy)
        {
            // Get the dot product.
            double dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy);

            // Get the cross product.
            double cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, Cy);

            // Calculate the angle.
            return Math.Atan2(cross_product, dot_product);
        }

        // Return the cross product AB x BC.
        // The cross product is a vector perpendicular to AB
        // and BC having length |AB| * |BC| * Sin(theta) and
        // with direction given by the right-hand rule.
        // For two vectors in the X-Y plane, the result is a
        // vector with X and Y components 0 so the Z component
        // gives the vector's length and direction.
        public static double CrossProductLength(double Ax, double Ay,
            double Bx, double By, double Cx, double Cy)
        {
            // Get the vectors' coordinates.
            double BAx = Ax - Bx;
            double BAy = Ay - By;
            double BCx = Cx - Bx;
            double BCy = Cy - By;

            // Calculate the Z coordinate of the cross product.
            return (BAx * BCy - BAy * BCx);
        }

        // Return the dot product AB · BC.
        // Note that AB · BC = |AB| * |BC| * Cos(theta).
        private static double DotProduct(double Ax, double Ay,
            double Bx, double By, double Cx, double Cy)
        {
            // Get the vectors' coordinates.
            double BAx = Ax - Bx;
            double BAy = Ay - By;
            double BCx = Cx - Bx;
            double BCy = Cy - By;

            // Calculate the dot product.
            return (BAx * BCx + BAy * BCy);
        }

        /// <summary>
        /// Determines if the given point is inside the polygon
        /// </summary>
        /// <param name="polygon">the vertices of polygon</param>
        /// <param name="testPoint">the given point</param>
        /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool IsPointInPolygon_1(PointF[] polygon, PointF testPoint)
        {
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].Y < testPoint.Y && polygon[j].Y >= testPoint.Y || polygon[j].Y < testPoint.Y && polygon[i].Y >= testPoint.Y)
                {
                    if (polygon[i].X + (testPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < testPoint.X)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        // Return True if the point is in the polygon.
        public static bool IsPointInPolygon(PointF[] polygon, PointF testPoint)
        {
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = polygon.Length - 1;
            double total_angle = GetAngle(
                polygon[max_point].X, polygon[max_point].Y,
                testPoint.X, testPoint.Y,
                polygon[0].X, polygon[0].Y);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++)
            {
                total_angle += GetAngle(
                    polygon[i].X, polygon[i].Y,
                    testPoint.X, testPoint.Y,
                    polygon[i + 1].X, polygon[i + 1].Y);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return (Math.Abs(total_angle) > 1);
        }


        public static double SignedPolygonArea(PointF[] polygon)
        {
            //The total calculated area is negative if the polygon is oriented clockwise

            // Add the first point to the end.
            int num_points = polygon.Length;
            PointF[] pts = new PointF[num_points + 1];
            polygon.CopyTo(pts, 0);
            pts[num_points] = polygon[0];

            // Get the areas.
            double area = 0;
            for (int i = 0; i < num_points; i++)
            {
                area +=
                    (pts[i + 1].X - pts[i].X) *
                    (pts[i + 1].Y + pts[i].Y) / 2;
            }

            // Return the result.
            return area;
        }
    }


}
