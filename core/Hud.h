#pragma once

#include <map>
#include <set>
#include "SceneItem.h"

namespace sam
{
    class Hud : public SceneItem
    {
        bgfx::ProgramHandle m_shader;

    public:
        void Draw(DrawContext& ctx) override;
        void Initialize(DrawContext& nvg) override;
    };
}