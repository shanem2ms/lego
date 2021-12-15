#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "Frustum.h"
#define NOMINMAX



namespace sam
{
    using namespace gmtl;

    void Frustum::Initialize(DrawContext& nvg)
    {
        m_shader = Engine::Inst().LoadShader("vs_brick.bin", "fs_frustum.bin");
    }

    void Frustum::Draw(DrawContext& ctx)
    {
        return;
        if (ctx.m_curviewIdx == 1)
            return;
        Matrix44f invViewProj = Engine::Inst().ViewCam().PerspectiveMatrix()*
            Engine::Inst().ViewCam().ViewMatrix();
        invert(invViewProj);


        Matrix44f tranform = invViewProj * makeTrans<Matrix44f>(Vec3f(0, 0, 0.5f)) *
            makeScale<Matrix44f>(Vec3f(1, 1, 0.5f));


        bgfx::setTransform(tranform.getData());
        // Set vertex and index buffer.
        bgfx::setVertexBuffer(0, Cube::vbh);
        bgfx::setIndexBuffer(Cube::ibh);
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_DEPTH_TEST_LESS
            | BGFX_STATE_MSAA
            | BGFX_STATE_BLEND_ALPHA
            | BGFX_STATE_CULL_CW;
        // Set render states.l
        bgfx::setState(state);
        bgfx::submit(ctx.m_curviewIdx, m_shader);

    }
}