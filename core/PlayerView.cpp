#include "PlayerView.h"
#include "LegoBrick.h"
#include "Engine.h"
#include "Physics.h"
#include "BrickMgr.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"

namespace sam
{     

    Player::Player() :
        m_flymode(true),
        m_inspectmode(false),
        m_jump(false)
    {
        m_playerGroup = std::make_shared<SceneGroup>();
        m_rightHandPartInst.canBeDestroyed = true;
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
            m_rightHandPart = std::make_shared<LegoBrick>(part, part.paletteIdx, LegoBrick::Physics::None, true);
            m_rightHandPart->SetOffset(part.pos + Vec3f(0,0,-1.2f));
            m_rightHandPart->SetRotate(part.rot);
            float s = 0.25f;
            m_rightHandPart->SetScale(Vec3f(s, s, s));
            m_rightHand->AddItem(m_rightHandPart);
        }
    }
    inline btVector3 bt(const Vec3f &v)
    { return btVector3(v[0], v[1], v[2]); }

    void Player::Update(DrawContext& ctx, Level& level)
    {
        if (m_rigidBody == nullptr)
        {
            Matrix44f m = 
                makeTrans<Matrix44f>(m_pos);

            btTransform mat4;
            mat4.setFromOpenGLMatrix(m.getData());
            m_initialState = std::make_shared<btDefaultMotionState>(mat4);
            btScalar mass = 1;
            m_btShape = std::make_shared<btCylinderShape>(btVector3(20, 25, 5) * BrickManager::Scale);
            btRigidBody::btRigidBodyConstructionInfo constructInfo(mass, m_initialState.get(),
                m_btShape.get());
            m_rigidBody = std::make_shared<btRigidBody>(constructInfo);
            ctx.m_physics->AddRigidBody(m_rigidBody.get());
        }
        auto& dcam = Engine::Inst().DrawCam();
        auto& cam = Engine::Inst().ViewCam();
        Camera::Fly fly = cam.GetFly();
        Vec3f right, up, forward;
        Vec3f upworld(0, 1, 0);
        auto dfly = dcam.GetFly();
        dfly.GetDirs(right, up, forward);
        Vec3f fwWorld;
        cross(fwWorld, right, upworld);

        Vec3f fwdVel = m_posVel[0] * right +
            (m_posVel[1]) * upworld +
            m_posVel[2] * fwWorld;
       
        btVector3 linearVel = m_rigidBody->getLinearVelocity();
        btVector3 btimp = bt(fwdVel) - linearVel;
        btimp[1] = m_jump ? 5 : 0;
        m_jump = false;
        m_rigidBody->applyCentralImpulse(btimp);
        m_rigidBody->activate();
        m_rigidBody->setFriction(2.0f);
        
        btVector3 p = m_rigidBody->getCenterOfMassPosition();
        m_pos = Vec3f(p[0], p[1], p[2]);
        m_playerGroup->SetOffset(m_pos);
        m_playerGroup->SetRotate(fly.Quat());
        dfly.pos = m_pos + Vec3f(0,14*BrickManager::Scale,0);
        dcam.SetFly(dfly);

        if ((ctx.m_frameIdx % 60) == 0)
        {
            Level::PlayerData playerdata;
            playerdata.pos = fly.pos;
            playerdata.dir = fly.dir;
            playerdata.flymode = FlyMode();
            playerdata.inspect = InspectMode();
            playerdata.inspectpos = dfly.pos;
            playerdata.inspectdir = dfly.dir;
            playerdata.rightHandPart = GetRightHandPart();

            level.WritePlayerData(playerdata);
        }
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
        PartInst pi;
        pi.id = "3820";
        m_rightHand->AddItem(std::make_shared<LegoBrick>(pi, 14));
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
            m_pos = Point3f(0.0f, 10.0f, 0.0f);
            m_dir = Vec2f(1.24564195f, -0.455399066f);
        }
    }

    void Player::MouseDown(float x, float y, int buttonId)
    {
        
    }

    constexpr float pi_over_two = 3.14159265358979323846f * 0.5f;
    void Player::RawMove(float dx, float dy)
    {
        Engine& e = Engine::Inst();
        Camera::Fly la = e.DrawCam().GetFly();
        la.dir[0] += dx;
        la.dir[1] -= dy;
        la.dir[1] = std::max(la.dir[1], -pi_over_two);
        e.DrawCam().SetFly(la);
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
        float speed = 5.0f;
        switch (k)
        {
        case LeftShift:
            if (m_flymode)
                m_posVel[1] -= speed;
            break;
        case SpaceBar:
            if (m_flymode)
                m_posVel[1] += speed;
            else
                m_jump = true;
            break;
        case AButton:
            m_posVel[0] -= speed;
            break;
        case DButton:
            m_posVel[0] += speed;
            break;
        case WButton:
            m_posVel[2] += speed;
            break;
        case SButton:
            m_posVel[2] -= speed;
            break;

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
        switch (k)
        {
        case LeftShift:
        case SpaceBar:
            m_posVel[1] = 0;
            break;
        case AButton:
        case DButton:
            m_posVel[0] = 0;
            break;
        case WButton:
        case SButton:
            m_posVel[2] = 0;
            break;
        }
    }

    Player::~Player()
    {

    }
}
