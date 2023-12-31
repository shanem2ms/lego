#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "Frustum.h"
#include "gmtl/Plane.h"
#include "gmtl/PlaneOps.h"
#include "LegoBrick.h"
#include "PartDefs.h"
#include "BrickMgr.h"
#include "Physics.h"
#include "Audio.h"
#include "PlayerView.h"
#include "gmtl/AABoxOps.h"
#include "gmtl/QuatOps.h"
#include "ConnectionLogic.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"
#define NOMINMAX

namespace sam
{
    bool ConnectionLogic::CanConnect(ConnectorType a, ConnectorType b)
    {
        if (a > b)
        {
            ConnectorType tmp = b;
            b = a;
            a = tmp;
        }
        if (a == Stud && b == RStud)
            return true;
        if (a == MFigHipLeg && b == MFigRHipLeg)
            return true;
        if (a == MFigHipStud && b == MFigRHipStud)
            return true;
        if (a == MFigTorsoRArm && b == MFigArmKnob)
            return true;
        if (a == MFigTorsoNeck && b == MFigHeadRNeck)
            return true;
        if (a == MFigRWrist && b == MFigWrist)
            return true;     
        return false;
    }
    float GetMaxRotDist(const AABoxf& aabb, const Vec3f& pos)
    {
        Point3f corners[8];
        getCorners(aabb, corners);
        float distSq = 0;
        for (int i = 0; i < 8; ++i)
        {
            Vec3f p = corners[i] - pos;
            distSq = std::max(lengthSquared(p), distSq);
        }

        return sqrt(distSq);
    }


    struct NearbyConnector
    {
        const PartInst* ppi;
        const Connector* pc;
        float distSq;
        float xDist;
        float zDist;
    };

    Vec3f ColorForType(ConnectorType ctype)
    {
        switch (ctype)
        {
        case Stud:
        case MFigHipLeg:
        case MFigHipStud:
        case MFigTorsoNeck:
        case MFigArmKnob:
            return Vec3f(1, 0, 0);
        case RStud:
        case MFigRHipLeg:
        case MFigTorsoRArm:
        case MFigHeadRNeck:
        case MFigRWrist:
        case MFigRHandGrip:
        case MFigRHipStud:            
            return Vec3f(0, 1, 0);
        default:
            return Vec3f(0.5f, 0.5f, 0.5f);
        }
    }

    void ConnectionLogic::PlaceBrick(Player* player, std::shared_ptr<LegoBrick> pickedBrick,
        OctTileSelection& octTileSelection, std::shared_ptr<Physics> physics, bool doCollisionCheck)
    {
        Engine& e = Engine::Inst();
        Camera::Fly la = e.ViewCam().GetFly();
        Vec3f r, u, camLookDir;
        la.GetDirs(r, u, camLookDir);

        Matrix44f mat = pickedBrick->GetWorldMatrix();
        Brick* pBrick = pickedBrick->GetBrick();
        Matrix44f wm = pickedBrick->GetWorldMatrix();
        Vec3f c0 = Vec3f(&wm.mData[0]);
        normalize(c0);
        Vec3f c1 = Vec3f(&wm.mData[4]);
        normalize(c1);
        Vec3f c2 = Vec3f(&wm.mData[8]);
        normalize(c2);
        Matrix33f rotmat = makeAxes<Matrix33f>(c0, c1, c2);
        Quatf pickedDir = make<Quatf>(rotmat);
        normalize(pickedDir);
        int connectorIdx = pickedBrick->GetHighlightedConnector();
        if (connectorIdx >= 0)
        {
            auto& pickedConnector = pBrick->m_connectors[connectorIdx];
            Quatf wsPickedConnectorDir = pickedDir * pickedConnector.GetDirAsQuat();

            Vec4f wsPickedConnectorPos;
            xform(wsPickedConnectorPos, wm, Vec4f(pickedConnector.pos, 1));

            std::shared_ptr<Brick> pRHandBrick = BrickManager::Inst().GetBrick(player->GetRightHandPart().id);
            BrickManager::Inst().LoadConnectors(pBrick);
            for (auto& rhandconnect : pRHandBrick->m_connectors)
            {
                if (ConnectionLogic::CanConnect(pickedConnector.type, rhandconnect.type))
                {

                    // This part is tricky, first based on the direction we're facing, we're goig to try
                    // to place the brick facing directly forward, we calculate that vector.
                    Quatf invPart = rhandconnect.GetDirAsQuat();
                    invert(invPart);
                    wsPickedConnectorDir = wsPickedConnectorDir * invPart;
                    Vec3f constraintPlaneNrm = wsPickedConnectorDir * Vec3f(0, 1, 0);
                    Vec3f zDir = camLookDir - dot(camLookDir, constraintPlaneNrm) * constraintPlaneNrm;
                    PartInst pi = player->GetRightHandPart();

                    Quatf rhq = rhandconnect.GetDirAsQuat();
                    rhq = invert(rhq);
                    Quatf snappedDir;                    
                    if (true) // Snap
                    {
                        float maxdot = 0;
                        for (int i = 0; i < 4; ++i)
                        {
                            Quatf rq = makeRot<Quatf>(AxisAnglef(gmtl::Math::PI_OVER_2 * i, Vec3f(0, 1, 0)));
                            Quatf q = pickedConnector.GetDirAsQuat() * rq * rhq;
                            Vec3f testDir = q * Vec3f(1, 0, 0);
                            float d = dot(zDir, testDir);
                            if (d > maxdot)
                            {
                                maxdot = d;
                                snappedDir = rq;
                            }
                        }
                    }

                    Quatf rq = makeRot<Quatf>(AxisAnglef(gmtl::Math::PI_OVER_2, Vec3f(0, 1, 0)));
                    Quatf q = pickedConnector.GetDirAsQuat() * snappedDir * rhq;
                    pi.rot = pickedDir * q;
                    Vec3f newpos = pi.rot * rhandconnect.pos;
                    Vec3f pos = Vec3f(wsPickedConnectorPos) - (newpos * BrickManager::Scale);
                    pi.pos = pos;

                    AABoxf cbox = pRHandBrick->m_collisionBox;


                    cbox.mMin = cbox.mMin * BrickManager::Scale;// +pi.pos;
                    cbox.mMax = cbox.mMax * BrickManager::Scale;// +pi.pos;
                    cbox = RotateAABox(cbox, wsPickedConnectorDir * pi.rot);
                    cbox.mMin += pi.pos;
                    cbox.mMax += pi.pos;
                    {
                        static std::shared_ptr<btRigidBody> sRigidBody;
                        std::shared_ptr<Brick> pBrick = BrickManager::Inst().GetBrick(pi.id);
                        if (BrickManager::Inst().LoadCollision(pBrick.get()))
                        {                            
                            Matrix44f m =
                                makeTrans<Matrix44f>(pi.pos) *
                                makeRot<Matrix44f>(pi.rot);

                            btTransform mat4;
                            mat4.setFromOpenGLMatrix(m.getData());
                            auto initialState = std::make_shared<btDefaultMotionState>(mat4);
                            btScalar mass = 0;
                            btRigidBody::btRigidBodyConstructionInfo constructInfo(mass, initialState.get(),
                                pBrick->m_collisionShape.get());
                            sRigidBody = std::make_shared<btRigidBody>(constructInfo);
                            if (!doCollisionCheck || !physics->TestCollision(sRigidBody.get()))
                            {
                                octTileSelection.AddPartInst(pi);
                                Application::Inst().GetAudio().PlayOnce("click-7.wav");
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    void ConnectionLogic::GetAlignmentDomain(const PartInst& pi, float sweepDist)
    {

    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    static bgfxh<bgfx::UniformHandle> sUparams;

    void ConnectionLogic::Draw(DrawContext& ctx)
    {
        if (!bgfx::isValid(sShader))
            sShader = Engine::Inst().LoadShader("vs_cubes.bin", "fs_bbox.bin");
        if (!sUparams.isValid())
            sUparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);

        if (m_connnectorsCL == nullptr)
            return;

        Vec4f color = Vec4f(0, 1, 0, 1);

        int viewId = Engine::Inst().GetNextView();
        bgfx::setViewName(viewId, "connectors");
        bgfx::setViewFrameBuffer(viewId, BGFX_INVALID_HANDLE);
        float near = ctx.m_nearfar[0];
        float far = ctx.m_nearfar[2];
        gmtl::Matrix44f view = Engine::Inst().DrawCam().ViewMatrix();
        gmtl::Matrix44f proj0 = Engine::Inst().DrawCam().GetPerspectiveMatrix(near, far);

        bgfx::setUniform(sUparams, &color, 1);
        bgfx::setViewRect(viewId, 0, 0, bgfx::BackbufferRatio::Equal);
        bgfx::setViewTransform(viewId, view.getData(), proj0.getData());

        Matrix44f worldMatrix;
        identity(worldMatrix);
        bgfx::setTransform(worldMatrix.getData());
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_WRITE_Z
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_MSAA
            | BGFX_STATE_BLEND_ALPHA;
        // Set render states.l
        m_connnectorsCL->Use();
        bgfx::setState(state);
        bgfx::setVertexBuffer(0, m_connnectorsCL->vbh);
        bgfx::setIndexBuffer(m_connnectorsCL->ibh);
        bgfx::submit(viewId, sShader);
    }
}
