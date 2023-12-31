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
    ConnectionWidget::ConnectionWidget(Vec3f color) :
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
            | BGFX_STATE_CULL_CCW
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_BLEND_ALPHA
            | BGFX_STATE_MSAA;
        // Set render states.l

        bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
        Vec4f color = Vec4f(m_color, 0.5f);
        bgfx::setUniform(m_uparams, &color, 1);

        bgfx::setState(state);
        bgfx::setVertexBuffer(0, Cube::vbh);
        bgfx::setIndexBuffer(Cube::ibh);
        bgfx::submit(DrawViewId::ForwardRendered, sShader);
        
        const float lsize = 0.25f;
        {
            Matrix44f m = ctx.m_mat * CalcMat() *
                makeScale<Matrix44f>(Vec3f(lsize, lsize, 2.5f)) *
                makeTrans<Matrix44f>(Vec3f(0, 0, -1.0f));

            bgfx::setTransform(m.getData());
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CCW
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_BLEND_ALPHA
                | BGFX_STATE_MSAA;
            // Set render states.l

            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            Vec4f color = Vec4f(0, 0, 1, 1.0f);
            bgfx::setUniform(m_uparams, &color, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, Cube::vbh);
            bgfx::setIndexBuffer(Cube::ibh);
            bgfx::submit(DrawViewId::ForwardRendered, sShader);

        }

        {
            Matrix44f m = ctx.m_mat * CalcMat() *
                makeScale<Matrix44f>(Vec3f(2.5f, lsize, lsize)) *
                makeTrans<Matrix44f>(Vec3f(-1.0f, 0, 0));

            bgfx::setTransform(m.getData());
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CCW
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_BLEND_ALPHA
                | BGFX_STATE_MSAA;
            // Set render states.l

            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            Vec4f color = Vec4f(1, 0, 0, 1.0f);
            bgfx::setUniform(m_uparams, &color, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, Cube::vbh);
            bgfx::setIndexBuffer(Cube::ibh);
            bgfx::submit(DrawViewId::ForwardRendered, sShader);

        }

        {
            Matrix44f m = ctx.m_mat * CalcMat() *
                makeScale<Matrix44f>(Vec3f(lsize, 5, lsize)) *
                makeTrans<Matrix44f>(Vec3f(0, -1.0f, 0));

            bgfx::setTransform(m.getData());
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CCW
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_BLEND_ALPHA
                | BGFX_STATE_MSAA;
            // Set render states.l

            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            Vec4f color = Vec4f(0, 1, 0, 1.0f);
            bgfx::setUniform(m_uparams, &color, 1);

            bgfx::setState(state);
            bgfx::setVertexBuffer(0, Cube::vbh);
            bgfx::setIndexBuffer(Cube::ibh);
            bgfx::submit(DrawViewId::ForwardRendered, sShader);

        }
    }

#if 0

    const float lsize = 0.05f;
    Matrix4x4 cmat = c.Mat.ToM44();
    Vector4 color = axiscolors[2] * (c.IsSelected ? 1.0f : 0.5f);
    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
    {
        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, 0, -0.5f)) *
            Matrix4x4.CreateScale(new Vector3(lsize, lsize, 0.5f)) * cmat * mat;
        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
        _cl.DrawIndexed((uint)_cubeIndexCount);
    }
    color = axiscolors[0] * (c.IsSelected ? 1.0f : 0.5f);
    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
    {
        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(-0.5f, 0, 0)) *
            Matrix4x4.CreateScale(new Vector3(0.5f, lsize, lsize)) * cmat * mat;
        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
        _cl.DrawIndexed((uint)_cubeIndexCount);
    }
    color = axiscolors[1] * (c.IsSelected ? 1.0f : 0.5f);
    _cl.UpdateBuffer(_materialBuffer, 0, ref color);
    {
        Matrix4x4 cm = Matrix4x4.CreateTranslation(new Vector3(0, -0.5f, 0)) *
            Matrix4x4.CreateScale(new Vector3(lsize, 1.0f, lsize)) * cmat * mat;
        _cl.UpdateBuffer(_worldBuffer, 0, ref cm);
        _cl.DrawIndexed((uint)_cubeIndexCount);
    }
#endif
}