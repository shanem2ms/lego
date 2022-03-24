#include "PlayerView.h"
#include "LegoBrick.h"
#include "Engine.h"

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

        Level::PlayerData playerdata;
        if (level.GetPlayerData(playerdata))
        {
            m_pos = playerdata.pos;
            m_dir = playerdata.dir;
            m_flymode = playerdata.flymode;
            m_inspectmode = playerdata.inspect;
            SetRightHandPart(playerdata.rightHandPart);
            memcpy(m_slots, playerdata.slots, sizeof(m_slots));
        }
        else
        {
            m_pos = Point3f(0.0f, 0.0f, -0.5f);
            m_dir = Vec2f(1.24564195f, -0.455399066f);
        }
    }

    void Player::MouseDown(float x, float y, int buttonId)
    {
        
    }

    constexpr float pi_over_two = 3.14159265358979323846f * 0.5f;
    void Player::RawMove(float dx, float dy)
    {
       
    }

    void Player::MouseDrag(float x, float y, int buttonId)
    {

    }

    void Player::WheelScroll(float delta)
    {

    }


    void Player::MouseUp(int buttonId)
    {
    }

    static int curPartIdx = 0;
    static int prevPartIdx = -1;

    const int LeftShift = 16;
    const int LeftCtrl = 17;
    const int SpaceBar = 32;
    const int AButton = 'A';
    const int DButton = 'D';
    const int WButton = 'W';
    const int SButton = 'S';
    const int FButton = 'F';

    void Player::KeyDown(int k)
    {
        switch (k)
        {

        case FButton:
            m_flymode = !m_flymode;
        break;        case 'I':
            m_inspectmode = !m_inspectmode;
            Engine::Inst().SetDbgCam(m_inspectmode);
            break;
        }
    }

    void Player::KeyUp(int k)
    {     
    }
}
