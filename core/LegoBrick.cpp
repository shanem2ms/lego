#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "LegoBrick.h"
#include "BrickMgr.h"


namespace sam
{
    LegoBrick::LegoBrick(std::shared_ptr<BrickManager> mgr, const std::string &partstr) :
        m_mgr(mgr),
        m_partstr(partstr)
    {

    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    void LegoBrick::Initialize(DrawContext& nvg)
    {
        if (!bgfx::isValid(sShader))
            sShader = Engine::Inst().LoadShader("vs_cubes.bin", "fs_cubes.bin");

        const BrickManager::Brick &b = m_mgr->GetBrick(m_partstr.c_str());
        m_vbh = b.m_vbh;
        m_ibh = b.m_ibh;
        m_uparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
        
        SetScale(Vec3f(BrickManager::Scale, BrickManager::Scale, BrickManager::Scale));
        SetOffset(Point3f(0, 0, 1.0f));
                
        SetRotate(make<gmtl::Quatf>(AxisAnglef(Math::PI, 1.0f, 0.0f, 0.0f)));
    }

    void LegoBrick::Draw(DrawContext& ctx)
    {
        if (!bgfx::isValid(m_vbh))
            return;
        PosTexcoordNrmVertex::init();
        Matrix44f m = CalcMat();
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
        
        Vec4f color = Vec4f(1.0f, 1.0f, 0.0f, 1.0f);
        bgfx::setUniform(m_uparams, &color, 1);

        bgfx::setState(state);
        bgfx::setVertexBuffer(0, m_vbh);
        bgfx::setIndexBuffer(m_ibh);
        bgfx::submit(ctx.m_curviewIdx, sShader);

    }

}