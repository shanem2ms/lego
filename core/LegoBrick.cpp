#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "LegoBrick.h"
#include "BrickMgr.h"
#include "ConnectionWidget.h"


namespace sam
{
    LegoBrick::LegoBrick(const PartId &partid, int paletteIdx, bool showConnectors) :
        m_partid(partid),
        m_paletteIdx(paletteIdx),
        m_showConnectors(showConnectors)
    {

    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    static bgfx::UniformHandle sPaletteHandle(BGFX_INVALID_HANDLE);
    static bgfxh<bgfx::UniformHandle> sUparams;

    void LegoBrick::Initialize(DrawContext& nvg)
    {
        if (!bgfx::isValid(sShader))
        {
            sShader = Engine::Inst().LoadShader("vs_brick.bin", "fs_cubes.bin");
            sPaletteHandle = bgfx::createUniform("s_brickPalette", bgfx::UniformType::Sampler);
        }
        m_pBrick = BrickManager::Inst().GetBrick(m_partid);
        if (!sUparams.isValid())
            sUparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
        
        SetScale(Vec3f(BrickManager::Scale, BrickManager::Scale, BrickManager::Scale));
        SetRotate(make<gmtl::Quatf>(AxisAnglef(Math::PI, 0.0f, 0.0f, 1.0f)));

        if (m_showConnectors)
        {
            BrickManager::Inst().LoadConnectors(m_pBrick);
            for (auto c : m_pBrick->m_connectors)
            {
                auto connectWidget = std::make_shared<ConnectionWidget>(c.type);
                connectWidget->SetOffset(c.pos);
                connectWidget->SetRotate(c.dir);
                AddItem(connectWidget);
            }
        }
    }

    void LegoBrick::Draw(DrawContext& ctx)
    {
        SceneGroup::Draw(ctx);
        if (!bgfx::isValid(m_pBrick->m_vbh))
            return;
        if (m_pBrick != nullptr)
            BrickManager::Inst().MruUpdate(m_pBrick);
        PosTexcoordNrmVertex::init();
        Matrix44f m = ctx.m_mat * CalcMat();
        bgfx::setTransform(m.getData());
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB 
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_WRITE_Z
            | BGFX_STATE_CULL_CCW
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_MSAA
            | BGFX_STATE_BLEND_ALPHA;
        // Set render states.l
        
        bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
        Vec4f color = Vec4f(m_paletteIdx, 0, 0, 0);
        bgfx::setUniform(sUparams, &color, 1);

        bgfx::setState(state);
        bgfx::setVertexBuffer(0, m_pBrick->m_vbh);
        bgfx::setIndexBuffer(m_pBrick->m_ibh);
        bgfx::submit(ctx.m_curviewIdx, sShader);

    }

}