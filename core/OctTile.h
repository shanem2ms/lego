#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "Loc.h"
#include "PartDefs.h"

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
        Brick* m_pBrick;

        int m_lastUsedRawData;
        float m_intersects;
        bool m_isdecommissioned;
        std::vector<PartInst> m_parts;

    public:
        static const int SquarePtsCt = 256;
        float m_nearDist;
        float m_farDist;
        float distFromCam;
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
        void Decomission(DrawContext& ctx);
        void LoadVB();
        bool IsCollided(Point3f &oldpos, Point3f &newpos, AABoxf& bbox, Vec3f& outNormal);
        static Vec3i FindHit(const std::vector<byte> &data, const Vec3i p1, const Vec3i p2);
        int GetReadyState() const
        { return m_readyState; }
    };

    class TargetCube : public SceneItem
    {
        bgfx::ProgramHandle m_shader;
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };
}