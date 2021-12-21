#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "LegoBrick.h"
#include "BrickMgr.h"
#include "Physics.h"
#include "ConnectionWidget.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"


namespace sam
{
    LegoBrick::LegoBrick(const PartId &partid, int paletteIdx, bool showConnectors) :
        m_partid(partid),
        m_paletteIdx(paletteIdx),
        m_showConnectors(showConnectors),
        m_connectorPickIdx(-1),
        m_connectorPickWidget(),
        m_rigidBody(nullptr),
        m_initialState(nullptr)
    {

    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    static bgfx::ProgramHandle sShader2(BGFX_INVALID_HANDLE);  
    static bgfx::ProgramHandle sShader3(BGFX_INVALID_HANDLE);
    static bgfx::UniformHandle sPaletteHandle(BGFX_INVALID_HANDLE);
    static bgfxh<bgfx::UniformHandle> sUparams;

    void LegoBrick::Initialize(DrawContext& dc)
    {
        if (!bgfx::isValid(sShader))
        {
            sShader = Engine::Inst().LoadShader("vs_brick.bin", "fs_cubes.bin");
            sShader2 = Engine::Inst().LoadShader("vs_connector.bin", "fs_pickconnector.bin");
            sShader3 = Engine::Inst().LoadShader("vs_connector.bin", "fs_pickbrick.bin");
            sPaletteHandle = bgfx::createUniform("s_brickPalette", bgfx::UniformType::Sampler);
        }
        m_pBrick = BrickManager::Inst().GetBrick(m_partid);
        if (!sUparams.isValid())
            sUparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
        
        SetScale(Vec3f(BrickManager::Scale, BrickManager::Scale, BrickManager::Scale));
        //SetRotate();

        BrickManager::Inst().LoadConnectors(m_pBrick);
        if (m_showConnectors)
        {
            for (auto c : m_pBrick->m_connectors)
            {
                auto connectWidget = std::make_shared<ConnectionWidget>(c.type);
                connectWidget->SetOffset(c.pos);
                connectWidget->SetRotate(c.dir);
                AddItem(connectWidget);
            }
        }

        Matrix44f m = dc.m_mat *
            makeTrans<Matrix44f>(m_offset) *
            makeRot<Matrix44f>(m_rotate) *
            makeScale<Matrix44f>(m_scale);

        btTransform mat4;
        mat4.setFromOpenGLMatrix(m.getData());
        m_initialState = std::make_shared<btDefaultMotionState>(mat4);
        btRigidBody::btRigidBodyConstructionInfo constructInfo((btScalar)0, m_initialState.get(),
            m_pBrick->m_collisionShape.get());
        m_rigidBody = std::make_shared<btRigidBody>(constructInfo);
        dc.m_physics->AddRigidBody(m_rigidBody.get());
    }

    void LegoBrick::SetPickData(float data)
    {
        int connectorIdx = (int)(data + 0.5f) - 1;
        if (connectorIdx != m_connectorPickIdx)
        {
            if (m_connectorPickWidget != nullptr)
                RemoveItem(m_connectorPickWidget);
            if (connectorIdx >= 0 && 
                connectorIdx < m_pBrick->m_connectors.size())
            {

                auto& c = m_pBrick->m_connectors[connectorIdx];
                auto connectWidget = std::make_shared<ConnectionWidget>(c.type);
                connectWidget->SetOffset(c.pos);
                connectWidget->SetRotate(c.dir);
                AddItem(connectWidget);
                m_connectorPickWidget = connectWidget;
            }
        }
        m_connectorPickIdx = connectorIdx;
    }

    void LegoBrick::Draw(DrawContext& ctx)
    {
        static const Quatf rot = make<Quatf>(AxisAnglef(Math::PI, 0.0f, 0.0f, 1.0f));
        SceneGroup::Draw(ctx);
        if (!bgfx::isValid(m_pBrick->m_vbh))
            return;
        if (m_pBrick != nullptr)
            BrickManager::Inst().MruUpdate(m_pBrick);
        PosTexcoordNrmVertex::init();
        Matrix44f m = ctx.m_mat * 
            makeTrans<Matrix44f>(m_offset) *
            makeRot<Matrix44f>(m_rotate) *
            makeScale<Matrix44f>(m_scale);
        // Set render states.l
        
        Vec4f color = Vec4f(m_paletteIdx, 0, 0, 0);

        {
        uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CW
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;
            bgfx::setTransform(m.getData());
            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            bgfx::setUniform(sUparams, &color, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, m_pBrick->m_vbh);
            bgfx::setIndexBuffer(m_pBrick->m_ibh);
            bgfx::submit(DrawViewId::DeferredObjects, sShader);
        }

        if (m_pBrick->m_connectorCL != nullptr)
        {
            int pickItems = ctx.m_pickedCandidates.size();
            ctx.m_pickedCandidates.push_back(ptr());
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CW
                | BGFX_STATE_DEPTH_TEST_LESS;
            m_pBrick->m_connectorCL->Use();
            bgfx::setTransform(m.getData());
            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            Vec4f p(pickItems, 0, 0, 0);
            bgfx::setUniform(sUparams, &p, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, m_pBrick->m_connectorCL->vbh);
            bgfx::setIndexBuffer(m_pBrick->m_connectorCL->ibh);
            bgfx::submit(DrawViewId::PickObjects, sShader2);

            bgfx::setTransform(m.getData());
            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            bgfx::setUniform(sUparams, &p, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, m_pBrick->m_vbh);
            bgfx::setIndexBuffer(m_pBrick->m_ibh);
            bgfx::submit(DrawViewId::PickObjects, sShader3);
        }
    }
    
    void LegoBrick::Decomission(DrawContext& ctx)
    {
        SceneGroup::Decomission(ctx);
        if (m_rigidBody)
        {
            ctx.m_physics->RemoveRigidBody(m_rigidBody.get());
            m_rigidBody = nullptr;
        }
    }
    LegoBrick::~LegoBrick()
    {        
    }

}