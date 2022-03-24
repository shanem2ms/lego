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
        Vec3f m_pos;
        Vec2f m_dir;
    public:
        
        void SetRightHandPart(const PartInst& part);
        const PartInst& GetRightHandPart() const
        { return m_rightHandPartInst; }
        std::shared_ptr<SceneItem> GetPlayerGroup()
        { return m_playerGroup; }
        void Update(DrawContext& ctx);
        Player();
        void Initialize(Level& level);
        const SlotPart* GetSlots() const
        {
            return m_slots;
        }

        bool FlyMode() const { return m_flymode; }
        bool InspectMode() const { return m_inspectmode; }
        void RawMove(float dx, float dy);
        void MouseDown(float x, float y, int buttonId);
        void MouseDrag(float x, float y, int buttonId);
        void MouseUp(int buttonId);
        void KeyDown(int k);
        void KeyUp(int k);
        void WheelScroll(float delta);
    };
}

