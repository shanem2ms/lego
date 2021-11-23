#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "Hud.h"
#define NOMINMAX

extern std::atomic<size_t> sVBBytes;
namespace sam
{
    void Hud::Initialize(DrawContext& nvg)
    {
        m_shader = Engine::Inst().LoadShader("vs_hud.bin", "fs_hud.bin");
    }

    extern int g_nearTiles;
    extern int g_farTiles;
    extern int nOctTilesTotal;
    extern int nOctTilesDrawn;
    extern float g_Fps;
    extern Loc g_hitLoc;;
    extern float g_hitDist;
    extern int g_numLod9;
    extern int g_behindViewer;
    extern std::string g_partName;
extern int g_buttonDown;

	void Hud::Draw(DrawContext& ctx)
	{        
        Matrix44f m =
            ctx.m_mat * CalcMat();
        bgfx::dbgTextClear();
        bgfx::dbgTextPrintf(0, 8, 0x0f, "Fps [%.2f]", g_Fps);
        bgfx::dbgTextPrintf(0, 8, 0x0f, "Name [%s]", g_partName.c_str());
        bgfx::dbgTextPrintf(0, 10, 0x0f, "VB %d MB", sVBBytes.load() >> 20);

        Engine& e = Engine::Inst();
        Camera::Fly la = e.ViewCam().GetFly();
        bgfx::dbgTextPrintf(0, 4, 0x0f, "Cam [%f %f %f]", la.pos[0], la.pos[1], la.pos[2]);

        //bgfx::setTransform(m.getData());
        Quad::init();

        // Set vertex and index buffer.
        bgfx::setVertexBuffer(0, Quad::vbh);
        bgfx::setIndexBuffer(Quad::ibh);
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_MSAA
            | BGFX_STATE_BLEND_ALPHA;
        // Set render states.l
        bgfx::setState(state);
        bgfx::submit(0, m_shader);
    }
}
