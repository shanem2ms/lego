#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "Loc.h"

struct VoxCube;

namespace sam
{
    class BrickManager;
    class LegoBrick : public SceneItem
    {
        std::shared_ptr<BrickManager> m_mgr;
        bgfx::VertexBufferHandle m_vbh;
        bgfx::IndexBufferHandle m_ibh;
        bgfxh<bgfx::UniformHandle> m_uparams;

    public:
        LegoBrick(std::shared_ptr<BrickManager> mgr,
            const std::string& partstr);
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;

    };
}