#pragma once
#pragma once
#include <map>
#include <set>
#include "OctTile.h"
#include "OctTileSelection.h"
#include "Level.h"
#include "PartDefs.h"

namespace sam
{
    class Player
    {
        std::shared_ptr<SceneGroup> m_playerGroup;
        std::shared_ptr<SceneGroup> m_rightHand;
        PartInst m_rightHandPartInst;
        std::shared_ptr<SceneItem> m_rightHandPart;
        bool m_flymode;
        bool m_inspectmode;
        SlotPart m_slots[16];

    public:
        
        void SetRightHandPart(const PartInst& part);
        const PartInst& GetRightHandPart() const
        { return m_rightHandPartInst; }
        std::shared_ptr<SceneItem> GetPlayerGroup()
        { return m_playerGroup; }
        void Update(DrawContext& ctx);
        Player();
        void Initialize(Level& level);
    };
}

