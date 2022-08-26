using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BulletSharp;
using BM = BulletSharp.Math;
using System.Numerics;
using System.Collections.ObjectModel;

namespace partmake
{
    static class Utils
    {
        public static BM.Matrix FromMat4(Matrix4x4 m4)
        {
            return new BM.Matrix(
                m4.M11, m4.M12, m4.M13, m4.M14,
                m4.M21, m4.M22, m4.M23, m4.M24,
                m4.M31, m4.M32, m4.M33, m4.M34,
                m4.M41, m4.M42, m4.M43, m4.M44);
        }

        public static Matrix4x4 FromMat(BM.Matrix m)
        {
            return new Matrix4x4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }

        public static BM.Vector3 FromVector3(Vector3 v3)
        {
            return new BM.Vector3(v3.X, v3.Y, v3.Z);
        }

        public static Vector3 FromBVector3(BM.Vector3 v3)
        {
            return new Vector3(v3.X, v3.Y, v3.Z);
        }

    }
    class RigidBody
    {
        ConvexTriangleMeshShape shape;
        BulletSharp.RigidBody body;
        Matrix4x4 worldMatrix;

        public RigidBody(string name, Matrix4x4 initialPos, float mass, TriangleMesh tm)
        {
            worldMatrix = initialPos;
            shape = new ConvexTriangleMeshShape(tm);
            BulletSharp.Math.Vector3 inertia;
            shape.CalculateLocalInertia(mass, out inertia);
            RigidBodyConstructionInfo constructInfo =
                new RigidBodyConstructionInfo(mass, new DefaultMotionState(
                    Utils.FromMat4(worldMatrix)), shape, inertia);
            body = new BulletSharp.RigidBody(constructInfo);
            body.SetDamping(0.3f, 0.3f);
        }

        public void AfterWorldAdd()
        {
            //System.Diagnostics.Debug.WriteLine($"{body.BroadphaseProxy.CollisionFilterGroup} .. {body.BroadphaseProxy.CollisionFilterMask}");
            if (CollisionGroup >= 0)
            {
                body.BroadphaseProxy.CollisionFilterGroup = CollisionGroup;
                body.BroadphaseProxy.CollisionFilterMask = (int)CollisionFilterGroups.StaticFilter;
                //body.Gravity = new BulletSharp.Math.Vector3(0);
            }
        }

        public void Refresh()
        {
            worldMatrix = Utils.FromMat(body.WorldTransform);
        }

        public void SetTransform(Matrix4x4 t)
        {
            worldMatrix = t;
            body.WorldTransform =
                Utils.FromMat4(worldMatrix);
        }

        public int CollisionGroup { get; set; } = -1;
        public Matrix4x4 WorldMatrix => worldMatrix;
        public BulletSharp.RigidBody Body => body;
        public object objectInfo;

        public override string ToString()
        {
            return objectInfo != null ? objectInfo.ToString() :
                body.ToString();
        }
    }

    class Constraint
    {
        virtual public TypedConstraint C => null;

        public bool Enabled { get => C.IsEnabled; set { C.IsEnabled = value; } }
    }

    class PointConstraint : Constraint
    {
        Point2PointConstraint p2p;

        RigidBody pinnedBody;
        override public TypedConstraint C => p2p;

        public PointConstraint(RigidBody mesh1, Vector3 m1pivot)
        {
            pinnedBody = mesh1;
            pinnedBody.Body.ActivationState = ActivationState.DisableDeactivation;
            p2p = new Point2PointConstraint(mesh1.Body, Utils.FromVector3(m1pivot));
            p2p.Setting.ImpulseClamp = 30.0f;
            p2p.Setting.Tau = 0.001f;
        }

        public void UpdateWsPos(Vector3 wspos)
        {
            //OpenTK.Vector3 lPos = OpenTK.Vector3.TransformPosition(wspos, pinnedBody.WorldMatrix.Inverted());
            p2p.PivotInB = Utils.FromVector3(wspos);
        }
    }

    class G6DOFConstraint : Constraint
    {
        Generic6DofConstraint dof;

        override public TypedConstraint C => dof;

        public G6DOFConstraint(RigidBody mesh1, Matrix4x4 m1matrix,
                    RigidBody mesh2, Matrix4x4 m2matrix,
                    Vector3 AngleLower,
                    Vector3 AngleUpper)
        {
            dof = new Generic6DofConstraint(mesh1.Body, mesh2.Body,
                Utils.FromMat4(m1matrix), Utils.FromMat4(m2matrix), true);
            dof.AngularUpperLimit = Utils.FromVector3(AngleUpper);
            dof.AngularLowerLimit = Utils.FromVector3(AngleLower);
            dof.BreakingImpulseThreshold = 1e+5f;
            //p2p = new Point2PointConstraint(mesh1.Body, mesh2.Body, Utils.FromVector3(m1pivot), Utils.FromVector3(m2pivot));
            //p2p.BreakingImpulseThreshold = 10.0f;
        }

    }

    class BulletSimulation
    {
        DefaultCollisionConfiguration colConfiguration = new DefaultCollisionConfiguration();
        CollisionDispatcher colDispatcher;
        DbvtBroadphase broadphase;
        DiscreteDynamicsWorld colWorld;
        ConstraintSolver solver;

        DebugDrawModes debugDraw;
        public DebugDrawModes DebugDraw
        {
            get => debugDraw; set
            {
                debugDraw = value;

            }
        }

        List<RigidBody> bodies = new List<RigidBody>();
        List<Constraint> constraints = new List<Constraint>();

        public delegate void DebugDrawLineDel(ref Matrix4x4 viewProj, Vector3 from, Vector3 to, Vector3 color);
        public DebugDrawLineDel DebugDrawLine = null;
        Matrix4x4 viewProjDbg;

        public BulletSimulation()
        {
            colDispatcher = new CollisionDispatcher(colConfiguration);
            broadphase = new DbvtBroadphase();
            solver = new NncgConstraintSolver();
            colWorld = new DiscreteDynamicsWorld(colDispatcher, broadphase, solver, colConfiguration);
            colWorld.DebugDrawer = new DbgRenderer(this);
        }

        public void DrawLine(ref BM.Vector3 from, ref BM.Vector3 to, ref BM.Vector3 color)
        {
            if (DebugDrawLine != null)
                DebugDrawLine(ref viewProjDbg, Utils.FromBVector3(from), Utils.FromBVector3(to), Utils.FromBVector3(color));
        }

        public void Init()
        {
        }

        public void AddObj(RigidBody obj)
        {
            bodies.Add(obj);
            //colWorld.AddCollisionObject(obj.Body);
            //obj.AfterWorldAdd();
        }

        public void AddConst(Constraint constraint)
        {
            constraints.Add(constraint);
            colWorld.AddConstraint(constraint.C, true);
        }


        public void DrawDebug(Matrix4x4 viewProj)
        {
            viewProjDbg = viewProj;
            colWorld.DebugDrawWorld();
        }
        public void Step()
        {
            var simulationTimestep = 1f / 60f;
            colWorld.StepSimulation(simulationTimestep, 10);
            foreach (var body in bodies)
            {
                body.Refresh();
            }
        }
    }

    class DbgRenderer : DebugDraw
    {

        BulletSimulation pthis;
        public DbgRenderer(BulletSimulation bs)
        {
            pthis = bs;
        }
        public override DebugDrawModes DebugMode { get => pthis.DebugDraw; set => pthis.DebugDraw = value; }

        public override void Draw3DText(ref BM.Vector3 location, string textString)
        {
        }

        public override void DrawLine(ref BM.Vector3 from, ref BM.Vector3 to, ref BM.Vector3 color)
        {
            pthis.DrawLine(ref from, ref to, ref color);
        }

        public override void ReportErrorWarning(string warningString)
        {
            System.Diagnostics.Debug.WriteLine(warningString);
        }
    }
}
