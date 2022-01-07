using System.Windows.Controls;
using System.Numerics;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows.Input;

namespace partmake
{
    /// <summary>
    /// Interaction logic for ThreeD.xaml
    /// </summary>
    public partial class ThreeD : Grid
    {

        LDrawFolders.Entry part;
        LDrawDatFile file;
        public LDrawFolders.Entry Part { get => part; set { part = value;  OnPartChange(part); } }
        PerspectiveCamera myPCamera = null;
        Model3DGroup myModel3DGroup;
        public ThreeD()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        public void Refresh()
        {
            OnPartChange(part);
        }

        void OnPartChange(LDrawFolders.Entry part)
        {
            viewport.Children.Clear();
            myModel3DGroup = new Model3DGroup();

            if (myPCamera == null)
            {
                myPCamera = new PerspectiveCamera();
                myPCamera.Position = new Point3D(0, 0, 2);
                myPCamera.LookDirection = new Vector3D(0, 0, -1);
                myPCamera.FieldOfView = 60;
            }
            viewport.Camera = myPCamera;
            // Define the lights cast in the scene. Without light, the 3D object cannot
            // be seen. Note: to illuminate an object from additional directions, create
            // additional lights.
            DirectionalLight myDirectionalLight = new DirectionalLight();
            myDirectionalLight.Color = Colors.White;
            myDirectionalLight.Direction = new Vector3D(-0.61, -0.5, -0.61);

            myModel3DGroup.Children.Add(myDirectionalLight);
            AmbientLight ambientLight = new AmbientLight();
            ambientLight.Color = Colors.DarkGray;
            myModel3DGroup.Children.Add(ambientLight);

            {
                file = LDrawFolders.GetPart(part);
                List<Vtx> vertices = new List<Vtx>();
                file.GetVertices(vertices, false);

                // Add the geometry model to the model group.
                myModel3DGroup.Children.Add(CreateModel(vertices, Color.FromArgb(100, 255, 100, 100)));
            }

            {
                List<Vtx> vertices = new List<Vtx>();
                file.GetVertices(vertices, true);

                // Add the geometry model to the model group.
                myModel3DGroup.Children.Add(CreateModel(vertices, Color.FromArgb(255, 255, 100, 255)));
            }

            ModelVisual3D myModelVisual3D = new ModelVisual3D();
            // Add the group of models to the ModelVisual3d.
            myModelVisual3D.Content = myModel3DGroup;

            //
            viewport.Children.Add(myModelVisual3D);

            Topology.Mesh topoMesh;
            topoMesh = new Topology.Mesh();
            file.GetTopoMesh(topoMesh);
            topoMesh.RemoveDuplicateFaces();
            List<Topology.Loop> loops = topoMesh.FindSquares();
            foreach (var loop in loops)
            {
                List<Vtx> vertices = new List<Vtx>();
                loop.GetVertices(vertices);
                myModel3DGroup.Children.Add(CreateModel(vertices, Color.FromArgb(100, 155, 100, 200)));
            }
        }


        GeometryModel3D CreateModel(List<Vtx> vertices, Color c)
        {

            // The geometry specifes the shape of the 3D plane. In this sample, a flat sheet
            // is created.
            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
            // Create a collection of normal vectors for the MeshGeometry3D.
            Vector3DCollection myNormalCollection = new Vector3DCollection();
            // Create a collection of vertex positions for the MeshGeometry3D.
            Point3DCollection myPositionCollection = new Point3DCollection();
            // Create a collection of texture coordinates for the MeshGeometry3D.
            PointCollection myTextureCoordinatesCollection = new PointCollection();
            // Create a collection of triangle indices for the MeshGeometry3D.
            Int32Collection myTriangleIndicesCollection = new Int32Collection();

            foreach (var v in vertices)
            {
                myPositionCollection.Add(new Point3D(v.pos.X, v.pos.Y, v.pos.Z));
                myNormalCollection.Add(new Vector3D(v.nrm.X, v.nrm.Y, v.nrm.Z));
                myTextureCoordinatesCollection.Add(new System.Windows.Point(v.tx.X, v.tx.Y));
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                myTriangleIndicesCollection.Add(i);
            }


            myMeshGeometry3D.Normals = myNormalCollection;
            myMeshGeometry3D.Positions = myPositionCollection;
            myMeshGeometry3D.TextureCoordinates = myTextureCoordinatesCollection;
            myMeshGeometry3D.TriangleIndices = myTriangleIndicesCollection;

            GeometryModel3D myGeometryModel = new GeometryModel3D();
            // Apply the mesh to the geometry model.
            myGeometryModel.Geometry = myMeshGeometry3D;

            // The material specifies the material applied to the 3D object. In this sample a
            // linear gradient covers the surface of the 3D object.

            // Define material and apply to the mesh geometries.
            DiffuseMaterial myMaterial = new DiffuseMaterial(new SolidColorBrush(c));
            myGeometryModel.Material = myMaterial;

            DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(c));
            myGeometryModel.BackMaterial = material;

            // Apply a transform to the object. In this sample, a rotation transform is applied,
            // rendering the 3D object rotated.
            RotateTransform3D myRotateTransform3D = new RotateTransform3D();
            AxisAngleRotation3D myAxisAngleRotation3d = new AxisAngleRotation3D();
            ScaleTransform3D scaleTransform = new ScaleTransform3D(new Vector3D(0.01, 0.01, 0.01));            
            myAxisAngleRotation3d.Axis = new Vector3D(0, 3, 0);
            myAxisAngleRotation3d.Angle = 40;
            myRotateTransform3D.Rotation = myAxisAngleRotation3d;
            myGeometryModel.Transform = scaleTransform;
            return myGeometryModel;
        }

        System.Windows.Point mouseDnPos;
        bool mouseDn = false;
        Vector2 mouseDnAngle;
        Vector2 curAngle;
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            this.CaptureMouse();
            mouseDnPos = e.GetPosition(this);
            mouseDn = true;
            mouseDnAngle = curAngle;
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            this.ReleaseMouseCapture();
            mouseDn = false;
            base.OnMouseUp(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (mouseDn)
            {
                System.Windows.Vector p = e.GetPosition(this) - mouseDnPos;
                curAngle = mouseDnAngle + new Vector2((float)p.X * 0.01f,
                    (float)p.Y * 0.01f);
                Vector3D vec = new Vector3D(System.Math.Cos(curAngle.X) *
                    -System.Math.Cos(curAngle.Y), System.Math.Sin(curAngle.Y),
                    -System.Math.Sin(curAngle.X) *
                    -System.Math.Cos(curAngle.Y));
                myPCamera.Position = (Point3D)(-vec);
                myPCamera.LookDirection = vec;
            }
            base.OnMouseMove(e);
        }
    }
    
}
