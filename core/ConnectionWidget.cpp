#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "ConnectionWidget.h"
#include "BrickMgr.h"


namespace sam
{
    ConnectionWidget::ConnectionWidget(int color) :
        m_color(color)
    {

    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    static bgfx::UniformHandle sPaletteHandle(BGFX_INVALID_HANDLE);

    void ConnectionWidget::Initialize(DrawContext& nvg)
    {
        if (!bgfx::isValid(sShader))
        {
            sShader = Engine::Inst().LoadShader("vs_connector.bin", "fs_forwardshade.bin");
            sPaletteHandle = bgfx::createUniform("s_brickPalette", bgfx::UniformType::Sampler);
        }
        m_uparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
    }

    void ConnectionWidget::Draw(DrawContext& ctx)
    {
        Cube::init();
        Matrix44f m = ctx.m_mat * CalcMat() *
            makeScale<Matrix44f>(Vec3f(3, 5, 3));
        bgfx::setTransform(m.getData());
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_WRITE_Z
            | BGFX_STATE_CULL_CW
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_BLEND_ALPHA
            | BGFX_STATE_MSAA;
        // Set render states.l

        bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
        Vec4f color = Vec4f(m_color, 0.5f, 0.0f, 0);
        bgfx::setUniform(m_uparams, &color, 1);

        bgfx::setState(state);
        bgfx::setVertexBuffer(0, Cube::vbh);
        bgfx::setIndexBuffer(Cube::ibh);
        bgfx::submit(DrawViewId::ForwardRendered, sShader);

    }

}