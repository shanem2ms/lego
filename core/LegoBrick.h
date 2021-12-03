#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "Loc.h"

struct VoxCube;

namespace sam
{
    class BrickManager;
    class Brick;
    class LegoBrick : public SceneItem
    {
        BrickManager* m_mgr;
        bgfx::VertexBufferHandle m_vbh;
        bgfx::IndexBufferHandle m_ibh;
        bgfxh<bgfx::UniformHandle> m_uparams;
        std::string m_partstr;
        Brick* m_pBrick;
    public:
        LegoBrick(BrickManager *mgr,
            const std::string& partstr);
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;

    };
}