#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "PartDefs.h"

struct VoxCube;

namespace sam
{
    class BrickManager;
    class Brick;
    class LegoBrick : public SceneGroup
    {        
        PartId m_partid;
        Brick* m_pBrick;
        int m_paletteIdx;
        bool m_showConnectors;
    public:
        LegoBrick(const PartId& partstr, int paletteIdx, bool showConnectors = false);
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
    };
}