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
        int m_color;
    public:
        ConnectionWidget(int color);
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };
}