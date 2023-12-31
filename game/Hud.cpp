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
    extern size_t g_brickCacheCnt;
    extern float g_Fps;
    extern Loc g_hitLoc;;
    extern float g_hitDist;
    extern int g_numLod9;
    extern int g_behindViewer;
    extern std::string g_partName;
    extern Loc g_inLoc;
    extern float g_overlap;

    bool g_showStats = true;
extern int g_buttonDown;

	void Hud::Draw(DrawContext& ctx)
	{        
        if (g_showStats)
        {
            bgfx::dbgTextClear();
            /*
            bgfx::dbgTextPrintf(0, 8, 0x0f, "Fps [%.2f]", g_Fps);
            ///bgfx::dbgTextPrintf(0, 10, 0x0f, "VB %d MB", sVBBytes.load() >> 20);
            bgfx::dbgTextPrintf(0, 10, 0x0f, "Tile [%d {%d %d %d}]", g_inLoc.m_l, g_inLoc.m_x, g_inLoc.m_y, g_inLoc.m_z);
            bgfx::dbgTextPrintf(0, 12, 0x0f, "Brick Cache [%d]", g_brickCacheCnt);
            */ 

            Engine& e = Engine::Inst();
            Camera::Fly la = e.ViewCam().GetFly();
            Vec3f r, u, f;
            la.GetDirs(r, u, f);
            bgfx::dbgTextPrintf(0, 1, 0x0f, "Pos [%f %f %f]", la.pos[0], la.pos[1], la.pos[2]);
            bgfx::dbgTextPrintf(0, 2, 0x0f, "Dir [%f %f]", la.dir[0], la.dir[1]);
            bgfx::dbgTextPrintf(0, 3, 0x0f, "Fwd [%f %f %f]", f[0], f[1], f[2]);
        }
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
        bgfx::submit(DrawViewId::HUD, m_shader);
    }
}
