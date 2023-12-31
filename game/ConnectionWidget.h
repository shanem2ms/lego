#pragma once

#include <map>
#include <set>
#include "SceneItem.h"

struct VoxCube;

namespace sam
{
    class BrickManager;
    class Brick;
    class ConnectionWidget : public SceneItem
    {
        bgfxh<bgfx::UniformHandle> m_uparams;
        Vec3f m_color;
    public:
        ConnectionWidget(Vec3f color);
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };
}