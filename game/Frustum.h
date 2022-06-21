#pragma once

#include <map>
#include <set>
#include "SceneItem.h"

namespace sam
{
    class Frustum : public SceneItem
    {
        bgfx::ProgramHandle m_shader;
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };
}