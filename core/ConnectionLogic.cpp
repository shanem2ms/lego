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
#include "ConnectionLogic.h"
#define NOMINMAX

namespace sam
{
    AABoxf RotateAABox(const AABoxf& in, const Quatf& q)
    {
        AABoxf outBox;
        Vec3f p[2] = { in.mMin,  in.mMax };
        for (int i = 0; i < 8; ++i)
        {
            Point3f op(p[i / 4][0], p[(i / 2) & 0x1][1], p[i & 0x1][2]);
            xform(op, q, op);
            outBox += op;
        }
        return outBox;
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
    void ConnectionLogic::PlaceBrick(std::shared_ptr<Player> player, std::shared_ptr<LegoBrick> pickedBrick,
        OctTileSelection& octTileSelection, bool doCollisionCheck)
    {
        Engine& e = Engine::Inst();
        Camera::Fly la = e.DrawCam().GetFly();
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
            auto& connector = pBrick->m_connectors[connectorIdx];
            Quatf wsPickedConnectorDir = pickedDir * connector.GetDirAsQuat();

            Vec4f wsPickedConnectorPos;
            xform(wsPickedConnectorPos, wm, Vec4f(connector.pos, 1));

            Brick* pRHandBrick = BrickManager::Inst().GetBrick(player->GetRightHandPart().id);
            BrickManager::Inst().LoadConnectors(pBrick);
            for (auto& rhandconnect : pRHandBrick->m_connectors)
            {
                if (Connector::CanConnect(connector.type, rhandconnect.type))
                {

                    // This part is tricky, first based on the direction we're facing, we're goig to try
                    // to place the brick facing directly forward, we calculate that vector.
                    Vec3f constraintPlaneNrm = wsPickedConnectorDir * Vec3f(0, 1, 0);
                    Vec3f zDir = camLookDir - dot(camLookDir, constraintPlaneNrm) * constraintPlaneNrm;
                    PartInst pi = player->GetRightHandPart();
                    if (true) // Snap
                    {
                        Vec3f snapDirs[4];
                        snapDirs[0] = wsPickedConnectorDir * Vec3f(1, 0, 0);
                        snapDirs[1] = -snapDirs[0];
                        snapDirs[2] = wsPickedConnectorDir * Vec3f(0, 0, 1);
                        snapDirs[3] = -snapDirs[2];
                        float maxdot = 0;
                        Vec3f snappedDir;
                        for (int i = 0; i < 4; ++i)
                        {
                            float d = dot(zDir, snapDirs[i]);
                            if (d > maxdot)
                            {
                                maxdot = d;
                                snappedDir = snapDirs[i];
                            }
                        }
                        Vec3f xDir;
                        cross(xDir, constraintPlaneNrm, snappedDir);
                        Matrix33f m33 = makeAxes<Matrix33f>(xDir, constraintPlaneNrm, snappedDir);
                        Quat snappedRot = make<Quatf>(m33);
                        snappedRot = snappedRot * pi.rot;
                        pi.rot = snappedRot;
                        Vec3f newpos = snappedRot * rhandconnect.pos;
                        Vec3f pos = Vec3f(wsPickedConnectorPos) - (newpos * BrickManager::Scale);
                        pi.pos = pos;
                    }
                    else
                    {
                        Planef constraintPlane(constraintPlaneNrm, Vec3f(wsPickedConnectorPos));
                        normalize(zDir);
                        Vec3f xDir;
                        cross(xDir, constraintPlaneNrm, zDir);
                        Matrix33f m33 = makeAxes<Matrix33f>(xDir, constraintPlaneNrm, zDir);

                        // We now form the direction we would ideally place the brick if we didn't 
                        // have to connect it to any other studs.  That direction is qIdealPlacement;
                        Quatf qIdealPlacement = make<Quatf>(m33);

                        // Now we make an part placement, and set its position so that the connectors are joined together.
                        // Direction is set from above.
                        qIdealPlacement = qIdealPlacement * pi.rot;
                        pi.rot = qIdealPlacement;
                        Vec3f newpos = qIdealPlacement * rhandconnect.pos;
                        Vec3f pos = Vec3f(wsPickedConnectorPos) - (newpos * BrickManager::Scale);
                        pi.pos = pos;

                        float rotDist =
                            GetMaxRotDist(pRHandBrick->m_collisionBox, rhandconnect.pos);
                        Spheref s(Vec3f(wsPickedConnectorPos), rotDist * BrickManager::Scale);
                        std::vector<PartInst> nearbyParts;
                        octTileSelection.GetInterectingParts(s, nearbyParts);
                        std::vector<NearbyConnector> nearbyConnectors;
                        std::vector<Vec3f> connectPts;
                        for (const PartInst& pi : nearbyParts)
                        {
                            Brick* b = BrickManager::Inst().GetBrick(pi.id);
                            for (const Connector& c : b->m_connectors)
                            {
                                if (c.type != ConnectorType::Stud)
                                    continue;
                                Vec3f p = c.pos * BrickManager::Scale;
                                Vec3f connectorWsPos = pi.pos + pi.rot * p;
                                Vec3f scl(5, 5, 5);
                                scl *= BrickManager::Scale;
                                AABoxf cb(p - scl, p + scl);
                                RotateAABox(cb, pi.rot);
                                cb.mMin += pi.pos;
                                cb.mMax += pi.pos;
                                if (intersect(cb, s))
                                {
                                    float d = distance(constraintPlane, Point3f(connectorWsPos));
                                    if (d < 0.001f)
                                    {
                                        connectPts.push_back(connectorWsPos);
                                        Vec3f connectVec = Vec3f(connectorWsPos - Vec3f(wsPickedConnectorPos));
                                        nearbyConnectors.push_back(
                                            NearbyConnector{ &pi, &c, lengthSquared(connectVec), dot(xDir, connectVec), dot(zDir, connectVec) });
                                    }
                                }
                            }
                        }

                        std::sort(nearbyConnectors.begin(), nearbyConnectors.end(), [](const auto& a, const auto& b) {
                            return a.distSq < b.distSq; });

                        Planef rhPlane(rhandconnect.dir, rhandconnect.pos);
                        for (auto& rc : pRHandBrick->m_connectors)
                        {
                            float d = distance(rhPlane, Point3f(rc.pos));
                            if (d < 0.001f)
                            {
                            }
                        }
                        // m_connnectorsCL = std::make_shared<CubeList>();
                        //m_connnectorsCL->Create(connectPts, 5 * BrickManager::Scale);
                        GetAlignmentDomain(pi, rotDist * BrickManager::Scale);
                    }
                    AABoxf cbox = pRHandBrick->m_collisionBox;

                    cbox.mMin = cbox.mMin * BrickManager::Scale;// +pi.pos;
                    cbox.mMax = cbox.mMax * BrickManager::Scale;// +pi.pos;
                    cbox = RotateAABox(cbox, wsPickedConnectorDir);
                    cbox.mMin += pi.pos;
                    cbox.mMax += pi.pos;
                    if (!doCollisionCheck || octTileSelection.CanAddPart(pi, cbox))
                    {
                        octTileSelection.AddPartInst(pi);
                        Application::Inst().GetAudio().PlayOnce("click-7.wav");
                    }
                    break;
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