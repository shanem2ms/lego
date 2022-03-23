#include "PlayerView.h"
#include "LegoBrick.h"

namespace sam
{     

    Player::Player() :
        m_flymode(true),
        m_inspectmode(false)
    {
        m_playerGroup = std::make_shared<SceneGroup>();
    }

    void Player::SetRightHandPart(const PartInst& part)
    {
        if (m_rightHandPart != nullptr)
        {
            m_rightHand->RemoveItem(m_rightHandPart);
            m_rightHandPart = nullptr;
        }
        m_rightHandPartInst = part;
        if (!part.id.IsNull())
        {
            m_rightHandPart = std::make_shared<LegoBrick>(part.id, part.paletteIdx, LegoBrick::Physics::None, true);
            m_rightHandPart->SetOffset(part.pos + Vec3f(0,0,-1.2f));
            m_rightHandPart->SetRotate(part.rot);
            float s = 0.25f;
            m_rightHandPart->SetScale(Vec3f(s, s, s));
            m_rightHand->AddItem(m_rightHandPart);
        }
    }

    void Player::Update(DrawContext& ctx)
    {

    }

    void Player::Initialize(Level& level)
    {
        Level::PlayerData playerdata;
        PartInst righthandpart;
        if (level.GetPlayerData(playerdata))
        {
            fly.pos = playerdata.pos;
            fly.dir = playerdata.dir;
            m_flymode = playerdata.flymode;
            m_inspectmode = playerdata.inspect;
            Engine::Inst().SetDbgCam(m_inspectmode);
            dfly.pos = playerdata.inspectpos;
            dfly.dir = playerdata.inspectdir;
            righthandpart = playerdata.rightHandPart;
            memcpy(m_slots, playerdata.slots, sizeof(m_slots));
        }
        else
        {
            fly.pos = Point3f(0.0f, 0.0f, -0.5f);
            fly.dir = Vec2f(1.24564195f, -0.455399066f);
        }
        for (int i = 0; i < 16; ++i)
        {
            m_slots[i].id = PartId("3001");
        }

        m_rightHand = std::make_shared<SceneGroup>();
        m_rightHand->SetOffset(Vec3f(1.3f, -0.65f, 1.005f));
        m_rightHand->SetRotate(make<Quatf>(AxisAnglef(gmtl::Math::PI, 0.0f, 1.0f, 0.0f)) *
            make<Quatf>(AxisAnglef(-gmtl::Math::PI / 8.0f, 0.0f, 0.0f, 1.0f)) *
            make<Quatf>(AxisAnglef(gmtl::Math::PI / 8.0f, 1.0f, 0.0f, 0.0f)));
        m_rightHand->AddItem(std::make_shared<LegoBrick>("3820", 14));
        m_playerGroup->AddItem(m_rightHand);
    }
}
