#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "Frustum.h"
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
                    Vec3f constraintPlane = wsPickedConnectorDir * Vec3f(0, 1, 0);
                    Vec3f lookPlane = camLookDir - dot(camLookDir, constraintPlane) * constraintPlane;

                    normalize(lookPlane);
                    Vec3f rightDir;
                    cross(rightDir, constraintPlane, lookPlane);
                    Matrix33f m33 = makeAxes<Matrix33f>(rightDir, constraintPlane, lookPlane);
                    Quatf qv = make<Quatf>(m33);
                    PartInst pi = player->GetRightHandPart();
                    pi.rot = qv;
                    

                    Vec3f newpos = qv * rhandconnect.pos;
                    Vec3f pos = Vec3f(wsPickedConnectorPos) - (newpos * BrickManager::Scale);
                    pi.pos = pos;
                                 
                    float rotDist = 
                        GetMaxRotDist(pRHandBrick->m_collisionBox, rhandconnect.pos);
                    Spheref s(Vec3f(wsPickedConnectorPos), rotDist * BrickManager::Scale);
                    std::vector<PartInst> nearbyParts;
                    octTileSelection.GetInterectingParts(s, nearbyParts);
                    for (const PartInst& pi : nearbyParts)
                    {

                    }

                    GetAlignmentDomain(pi, rotDist * BrickManager::Scale);
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
}