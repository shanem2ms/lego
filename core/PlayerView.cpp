#include "PlayerView.h"
#include "LegoBrick.h"
#include "Engine.h"
#include "Physics.h"
#include "BrickMgr.h"
#include "World.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"

namespace sam
{     

    Player::Player(World *pWorld) :
        m_flymode(true),
        m_inspectmode(false),
        m_jump(false),
        m_currentSlotIdx(0),
        m_pWorld(pWorld)
    {
        m_playerBody = std::make_shared<SceneGroup>();
        m_rightHandPartInst.canBeDestroyed = true;
    }

    void Player::Initialize(Level& level)
    {
        for (int i = 0; i < 16; ++i)
        {
            m_slots[i].id = PartId("3001");
        }

        m_rightHand = std::make_shared<SceneGroup>();
        m_rightHand->SetOffset(Vec3f(-1.3f, -1.65f, 1.005f));
        m_rightHand->SetRotate(
            make<Quatf>(AxisAnglef(gmtl::Math::PI, 1.0f, 0.0f, 0.0f)) *
            make<Quatf>(AxisAnglef(gmtl::Math::PI, 0.0f, 0.0f, 1.0f)));
            //make<Quatf>(AxisAnglef(gmtl::Math::PI / 8.0f, 1.0f, 0.0f, 0.0f)));
        PartInst pi;
        pi.id = "3820";
        m_rightHand->AddItem(std::make_shared<LegoBrick>(pi, 14, true));
        m_playerBody->AddItem(m_rightHand);
        m_playerHead = std::make_shared<SceneGroup>();
        m_playerHead->SetOffset(Vec3f(0, 2.4f, 0));
        m_playerBody->AddItem(m_playerHead);

        Level::PlayerData playerdata;
        if (level.GetPlayerData(playerdata))
        {
            m_pos = playerdata.pos;
            m_dir = playerdata.dir;
            m_flymode = playerdata.flymode;
            m_inspectmode = false;// playerdata.inspect;
            SetRightHandPart(playerdata.rightHandPart);
            memcpy(m_slots, playerdata.slots, sizeof(m_slots));
        }
        else
        {
            m_pos = Point3f(0.0f, -10.0f, 0.0f);
            m_dir = Vec2f(1.24564195f, -0.455399066f);
        }
    }
    constexpr float pi = 3.14159265358979323846f;
    constexpr float pi_over_two = pi * 0.5f;
    constexpr float pi_over_two_thresh = 3.14159265358979323846f * 0.5f - 0.001f;

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
            m_rightHandPart = std::make_shared<LegoBrick>(part, part.atlasidx, true, LegoBrick::Physics::None, true);
            m_rightHandPart->SetOffset(part.pos + Vec3f(0,0,-1.2f));
            m_rightHandPart->SetRotate(part.rot * gmtl::make<Quatf>((AxisAnglef(pi, Vec3f(0,1,0)))));
            float s = 0.25f;
            m_rightHandPart->SetScale(Vec3f(s, s, s));
            m_rightHand->AddItem(m_rightHandPart);
        }
    }
    inline btVector3 bt(const Vec3f &v)
    { return btVector3(v[0], v[1], v[2]); }

    void Player::GetDirs(Vec3f& right, Vec3f& up, Vec3f& forward) const
    {
        forward = make<gmtl::Quatf>(AxisAnglef(m_dir[1], -1.0f, 0.0f, 0.0f)) * Vec3f(0, 0, 1);
        forward = make<gmtl::Quatf>(AxisAnglef(m_dir[0], 0.0f, -1.0f, 0.0f)) * forward;
        normalize(forward);

        cross(right, forward, Vec3f(0, 1, 0));
        normalize(right);
        cross(up, forward, right);
        normalize(up);
    }


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
            btRigidBody::btRigidBodyConstructionInfo constructInfo(1, m_initialState.get(),
                m_btShape.get());
            m_rigidBody = std::make_shared<btRigidBody>(constructInfo);
            ctx.m_physics->AddRigidBody(m_rigidBody.get());
            m_rigidBody->setFriction(0.0f);
            m_rigidBody->setGravity(btVector3(0,m_flymode ? 0 : 10,0));
        }

        if (!m_inspectmode)
        {
            auto& cam = Engine::Inst().ViewCam();
            Vec3f right, up, forward;
            Vec3f upworld(0, 1, 0);
            GetDirs(right, up, forward);
            Vec3f fwWorld;
            cross(fwWorld, upworld, right);

            Vec3f fwdVel = m_posVel[0] * right +
                (m_posVel[1]) * upworld +
                m_posVel[2] * fwWorld;

            btVector3 linearVel = m_rigidBody->getLinearVelocity();
            btVector3 btimp = bt(fwdVel) - linearVel;
            if (!m_flymode)
                btimp[1] = m_jump ? -7 : 0;
            m_jump = false;
            m_rigidBody->applyCentralImpulse(btimp);
            m_rigidBody->activate();
            m_rigidBody->setFriction(0.0f);

            btVector3 p = m_rigidBody->getCenterOfMassPosition();
            p[1] = std::max(p[1], -32.0f);
            m_pos = Vec3f(p[0], p[1], p[2]);
            m_playerBody->SetOffset(m_pos);

            m_playerBody->SetRotate(make<gmtl::Quatf>(AxisAnglef(m_dir[0], 0.0f, -1.0f, 0.0f)));
            Camera::Fly fly = cam.GetFly();
            fly.pos = m_pos + Vec3f(0, -55 * BrickManager::Scale, 0);
            fly.dir = m_dir;
            cam.SetFly(fly);
        }
        else
        {
            auto& dcam = Engine::Inst().DrawCam();
            Camera::Fly dfly = dcam.GetFly();
            Vec3f right, up, forward;
            dfly.GetDirs(right, up, forward);
            float speedFactor = 0.5f;
            Vec3f fwdVel = m_posVel[0] * right +
                (-m_posVel[1]) * up +
                m_posVel[2] * forward;
            dfly.pos += fwdVel * speedFactor;            
            dcam.SetFly(dfly);
        }
        if ((ctx.m_frameIdx % 60) == 0)
        {
            Camera::Fly fly = Engine::Inst().ViewCam().GetFly();
            Camera::Fly dfly = Engine::Inst().DrawCam().GetFly();
            Level::PlayerData playerdata;
            playerdata.pos = fly.pos;
            playerdata.dir = fly.dir;
            playerdata.flymode = FlyMode();
            playerdata.inspect = InspectMode();
            playerdata.inspectpos = dfly.pos;
            playerdata.inspectdir = dfly.dir;
            playerdata.rightHandPart = GetRightHandPart();
            memcpy(playerdata.slots, m_slots, sizeof(playerdata.slots));

            level.WritePlayerData(playerdata);
        }
    }

    void Player::MouseDown(float x, float y, int buttonId)
    {
        if (buttonId == 1)
        {
            m_pWorld->PlaceBrick(this);
        }
        else if (buttonId == 0)
        {
            m_pWorld->DestroyBrick(this);
        }
        else if (buttonId == 3)
        {
            /*
            const PartId& id = m_pPickedBrick->GetPartInst().id;
            this->
                ReplaceCurrentPart(id.Name());

            this->ReplaceCurrentPartColor(
                BrickManager::Inst().GetColorFromIdx(m_pPickedBrick->GetPartInst().atlasidx));
                */
        }

    }
    void Player::RawMove(float dx, float dy)
    {
        Engine& e = Engine::Inst();
        if (!m_inspectmode)
        {
            m_dir[0] += dx;
            m_dir[1] -= dy;
            m_dir[1] = std::min(m_dir[1], pi_over_two_thresh);
            m_dir[1] = std::max(m_dir[1], -pi_over_two_thresh);
        }
        else
        {
            Camera &c = e.DrawCam();
            Camera::Fly fly = c.GetFly();
            fly.dir[0] += dx;
            fly.dir[1] -= dy;
            //fly.dir[1] = std::min(m_dir[1], pi_over_two_thresh);
            //fly.dir[1] = std::max(m_dir[1], -pi_over_two_thresh);
            c.SetFly(fly);
        }
    }

    void Player::MouseDrag(float x, float y, int buttonId)
    {

    }

    void Player::WheelScroll(float delta)
    {
        SetCurrentSlotIdx(std::max(0, std::min(7, m_currentSlotIdx - (int)delta)));
    }

    void Player::SetCurrentSlotIdx(int slotIdx)
    {
        m_currentSlotIdx = slotIdx;
        PartInst pi;
        pi.canBeDestroyed = true;
        pi.id = m_slots[m_currentSlotIdx].id;
        pi.atlasidx = m_slots[m_currentSlotIdx].colorCode;
        SetRightHandPart(pi);
    }


    void Player::MouseUp(int buttonId)
    {
    }

    static int curPartIdx = 0;
    static int prevPartIdx = -1;

    const int LeftShift = 16;
    const int LeftCtrl = 17;
    const int SpaceBar = 32;
    const int Home = 0x24;
    const int AButton = 'A';
    const int DButton = 'D';
    const int WButton = 'W';
    const int SButton = 'S';
    const int FButton = 'F';

    void Player::MovePadXY(float dx, float dy)
    {
        float speed = 3.0f;
        m_posVel[2] = speed * dy;
        m_posVel[0] = speed * dx;
    }

    void Player::MovePadZ(float dz)
    {
        float speed = 3.0f;
        m_posVel[1] = speed * dz;
    }

    void Player::Jump()
    {
        m_jump = true;
    }
    void Player::KeyDown(int k)
    {
        float speed = 3.0f;
        switch (k)
        {
        case LeftShift:
            if (m_flymode)
                m_posVel[1] += speed;
            break;
        case SpaceBar:
            if (m_flymode)
                m_posVel[1] -= speed;
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
        case Home:
        {
            m_pos = Point3f(0.0f, -10.0f, 0.0f);
            btTransform t = m_rigidBody->getCenterOfMassTransform();
            t.setOrigin(btVector3(m_pos[0], m_pos[1], m_pos[2]));
            m_rigidBody->setCenterOfMassTransform(t);
        }
            break;
        case FButton:
            m_flymode = !m_flymode;
            m_rigidBody->setGravity(btVector3(0, m_flymode ? 0 : 10, 0));
            break;
        case '1':
            m_inspectmode = false;
            Engine::Inst().SetDbgCam(m_inspectmode);
            break;
        case '2':
            m_inspectmode = true;
            Engine::Inst().SetDbgCam(m_inspectmode);
            break;
        case 'Q':
        {
            PartInst part = GetRightHandPart();
            part.rot *= make<Quatf>(AxisAnglef(
                -Math::PI_OVER_2, Vec3f(1, 0, 0)));
            SetRightHandPart(part);
            break;
        }
        case 'R':
        {
            PartInst part = GetRightHandPart();
            part.rot *= make<Quatf>(AxisAnglef(
                -Math::PI_OVER_2, Vec3f(0, 1, 0)));
            SetRightHandPart(part);
            break;
        }

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

    void Player::ReplaceCurrentPart(const PartId& partname)
    {
        int slotIdx = GetCurrentSlotIdx();
        SlotPart sp = GetSlots()[slotIdx];
        sp.id = partname;
        SetSlot(slotIdx, sp);

        PartInst pi = GetRightHandPart();
        pi.id = partname;
        SetRightHandPart(pi);
    }

    void Player::ReplaceCurrentPartColor(const BrickColor& bc)
    {
        int slotIdx = GetCurrentSlotIdx();
        SlotPart sp = GetSlots()[slotIdx];
        sp.colorCode = bc.code;
        SetSlot(slotIdx, sp);

        PartInst pi = GetRightHandPart();
        pi.atlasidx = bc.atlasidx;
        SetRightHandPart(pi);
    }

    Player::~Player()
    {

    }
}
