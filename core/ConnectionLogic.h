#pragma once
#include <memory>
#include "Engine.h"

class CubeList;
namespace sam
{
    class LegoBrick;
    class Player;
    class OctTileSelection;
    class PartInst;

    enum ConnectorType
    {
        Unknown = 0,
        Stud = 1,
        Clip = 2,
        StudJ = 3,
        RStud = 4,
        MFigHipLeg = 5,
        MFigRHipLeg = 6,
        MFigHipStud = 7,
        MFigTorsoRArm = 8,
        MFigTorsoNeck = 9,
        MFigHeadRNeck = 10,
        MFigArmKnob = 11,
        MFigRWrist = 12,
        MFigWrist = 13,
        MFigRHandGrip = 14,
        MFigRHipStud = 15,
    };


    inline Vec3f ScaleForType(ConnectorType ctype)
    {
        return Vec3f(5, 5, 5);
    }

    Vec3f ColorForType(ConnectorType ctype);

    struct Connector
    {
        ConnectorType type;
        Vec3f pos;
        Vec3f scl;
        Vec3f dir;
        int pickIdx;

        Quatf GetDirAsQuat()
        {
            if (fabs(dot(Vec3f(0, 1, 0), dir)) > 0.999f)
                return Quatf(QUAT_MULT_IDENTITYF);
            else
            {
                Matrix44f mat = makeRot<Matrix44f>(dir, Vec3f(0, 1, 0));
                return make<Quatf>(mat);
            }
        }        
    };

    inline bool operator < (const Vec3f& lhs, const Vec3f& rhs)
    {
        for (int i = 0; i < 3; ++i)
        {
            if (lhs[i] != rhs[i])
                return lhs[i] < rhs[i];
        }
        return false;
    }
    inline bool operator < (const Quatf& lhs, const Quatf& rhs)
    {
        for (int i = 0; i < 4; ++i)
        {
            if (lhs[i] != rhs[i])
                return lhs[i] < rhs[i];
        }
        return false;
    }

    inline bool operator < (const Connector& lhs, const Connector& rhs)
    {
        if (lhs.type != rhs.type)
            return lhs.type < rhs.type;
        if (lhs.pos != rhs.pos)
            return lhs.pos < rhs.pos;
        return lhs.dir < rhs.dir;
    }
    inline bool operator == (const Connector& lhs, const Connector& rhs)
    {
        if (lhs.type != rhs.type)
            return false;
        return lhs.pos == rhs.pos;
    }
    class ConnectionLogic : public IEngineDraw
    {
        std::shared_ptr<CubeList> m_connnectorsCL;
    public:
        void PlaceBrick(Player* player, std::shared_ptr<LegoBrick> pickedBrick,
            OctTileSelection &octTileSelection, std::shared_ptr<Physics> physics, bool doCollisionCheck);

        void GetAlignmentDomain(const PartInst& pi, float sweepDist);
        void Draw(DrawContext& dc) override;
        static bool CanConnect(ConnectorType a, ConnectorType b);
    };
}