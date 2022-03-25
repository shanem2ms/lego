#pragma once
#pragma once
#include <map>
#include <set>
#include "OctTile.h"
#include "OctTileSelection.h"
#include "Level.h"
#include "PartDefs.h"

class btDefaultMotionState;
class btRigidBody;
class btCylinderShape;

namespace sam
{
    class Player
    {
        std::shared_ptr<SceneGroup> m_playerBody;
        std::shared_ptr<SceneGroup> m_playerHead;
        std::shared_ptr<SceneGroup> m_rightHand;
        PartInst m_rightHandPartInst;
        std::shared_ptr<SceneItem> m_rightHandPart;
        bool m_flymode;
        bool m_jump;
        bool m_inspectmode;
        SlotPart m_slots[16];
        Vec3f m_pos;
        Vec2f m_dir;
        Vec3f m_posVel;
        float m_tiltVel;
        std::shared_ptr<btDefaultMotionState> m_initialState;
        std::shared_ptr<btRigidBody> m_rigidBody;
        std::shared_ptr<btCylinderShape> m_btShape;
        int m_currentSlotIdx;

    public:
        
        Vec3f Pos() const {
            return m_pos; }
        Vec3f Dir() const {
            return m_dir;
        }

        void SetRightHandPart(const PartInst& part);
        const PartInst& GetRightHandPart() const
        { return m_rightHandPartInst; }
        std::shared_ptr<SceneItem> GetPlayerGroup()
        { return m_playerBody; }
        void Update(DrawContext& ctx, Level& level);
        Player();
        void Initialize(Level& level);
        const SlotPart* GetSlots() const
        { return m_slots; }
        void SetSlot(int idx, const SlotPart &sp)
        { m_slots[idx] = sp; }
        int GetCurrentSlotIdx() const
        { return m_currentSlotIdx; }
        void SetCurrentSlotIdx(int slotIdx)
        { m_currentSlotIdx = slotIdx; }
        void GetDirs(Vec3f& right, Vec3f& up, Vec3f& forward) const;
        bool FlyMode() const { return m_flymode; }
        bool InspectMode() const { return m_inspectmode; }
        void RawMove(float dx, float dy);
        void MouseDown(float x, float y, int buttonId);
        void MouseDrag(float x, float y, int buttonId);
        void MouseUp(int buttonId);
        void KeyDown(int k);
        void KeyUp(int k);
        void WheelScroll(float delta);
        ~Player();
    };
}

