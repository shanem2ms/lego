#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "Loc.h"
#include "PartDefs.h"
#include "gmtl/Sphere.h"

struct VoxCube;

namespace sam
{
    class TerrainTile;
    class Brick;

    struct OctPart
    {
        int partIdx;
    };
    class OctTile : public SceneGroup
    {
        
        int m_image;
        Vec2f m_vals;
        Loc m_l;
        Vec2f m_maxdh;
        Vec2f m_mindh;
        int m_texpingpong;
        int m_buildFrame;
        int m_readyState;
        bgfxh<bgfx::UniformHandle> m_uparams;

        int m_lastUsedRawData;
        float m_intersects;
        bool m_isdecommissioned;
        std::vector<PartInst> m_parts;
        std::vector<Brick*> m_bricks;
        bool m_needsPersist;
        bool m_needsRefresh;

    public:
        static const int SquarePtsCt = 256;
        float m_nearDist;
        float m_farDist;
        float distFromCam;
    protected:
        void Refresh();
    public:
        void Draw(DrawContext& ctx) override;
        OctTile(const Loc& l);
        ~OctTile();

        void BackgroundLoad(World *pWorld);
        bool IsEmpty() const;

        void SetIntersects(float i)
        { m_intersects = i; }

        void SetImage(int image)
        {
            m_image = image;
        }
        gmtl::AABoxf GetBounds() const override;
        void SetVals(const Vec2f& v)
        {
            m_vals = v;
        }
        void Decomission(DrawContext& ctx) override;
        void LoadVB();
        bool IsCollided(Point3f &oldpos, Point3f &newpos, AABoxf& bbox, Vec3f& outNormal);
        static Vec3i FindHit(const std::vector<byte> &data, const Vec3i p1, const Vec3i p2);
        int GetReadyState() const
        { return m_readyState; }
        void AddPartInst(const PartInst& pi);
        bool CanAddPart(const PartInst& pi, const AABoxf& bbox);
        void RemovePart(const PartInst& pi);
        void GetInterectingParts(const Spheref& sphere, std::vector<PartInst>& piList);        
    };

    class TargetCube : public SceneItem
    {
        bgfx::ProgramHandle m_shader;
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };

    inline AABoxf RotateAABox(const AABoxf& in, const Quatf& q)
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
}