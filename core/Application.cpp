#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "World.h"
#include "imgui.h"
#include <chrono>

#define WATCHDOGTHREAD 0

namespace sam
{
    void (*Application::m_dbgFunc)(const char*) = nullptr;
    void Application::SetDebugMsgFunc(void (*dbgfunc)(const char*))
    {
        m_dbgFunc = dbgfunc;
    }
    void Application::DebugMsg(const std::string& str)
    {
        if (m_dbgFunc != nullptr)
            m_dbgFunc(str.c_str());
    }

#ifdef SAM_COROUTINES
    co::static_thread_pool g_threadPool(8);
#endif
    static Application* s_pInst = nullptr;
    static std::thread sWatchdogThread;
    static std::chrono::steady_clock::time_point sFrameStart;

#if WATCHDOGTHREAD
    static bool sWatchDogCheckEnabled = false;
    void WatchDogFunc();
    using namespace std::chrono_literals;
#endif

    Application::Application() :
        m_height(0),
        m_width(0),
        m_frameIdx(0),
        m_rawMouseMode(false)
    {
        s_pInst = this;
        m_engine = std::make_unique<Engine>();
        m_world = std::make_unique<World>();
        m_uiMgr = std::make_unique<UIManager>();
#if WATCHDOGTHREAD
        sWatchdogThread = std::thread(WatchDogFunc);
#endif
    }

#if WATCHDOGTHREAD
    void WatchDogFunc()
    {
        while (true)
        {
            std::this_thread::sleep_for(1ms);
            auto elapsed = std::chrono::high_resolution_clock::now()
                - sFrameStart;
            if (sWatchDogCheckEnabled && elapsed > 20ms)
            {
                __debugbreak();
            }
        }
    }
#endif

    Application& Application::Inst()
    {
        return *s_pInst;
    }


    UIManager& Application::UIMgr()
    {
        return *m_uiMgr;
    }

    void Application::SetHideMouseCursorFn(const std::function<bool(bool)>& fn)
    {
        m_hideMouseCursorFn = fn;
    }
    
    void Application::RawMouseMoved(int32_t rx, int32_t ry)
    {
        static const float mScale = 1.0f / 256.0f;
        if (m_rawMouseMode)
        {
            m_world->RawMove((float)rx * mScale, (float)-ry * mScale);
        }
    }

    void Application::TouchDown(float x, float y, int touchId)
    {
        if (!m_uiMgr->TouchDown(x, y, touchId))
        {
            if (!m_rawMouseMode)
                m_rawMouseMode = m_hideMouseCursorFn(true);
            else
                m_world->TouchDown(x, y, touchId);
        }
    }

    void Application::TouchMove(float x, float y, int touchId)
    {
        if (!m_uiMgr->TouchDrag(x, y, touchId))
            m_world->TouchDrag(x, y, touchId);
    }

    void Application::TouchUp(int touchId)
    {
        if (!m_uiMgr->TouchUp(touchId))
            m_world->TouchUp(touchId);
    }

    void Application::KeyDown(int keyId)
    {
        if (keyId == 0x1B) // Escape
        {
            m_hideMouseCursorFn(false);
            m_rawMouseMode = false;
        }
        else
            m_world->KeyDown(keyId);
    }

    void Application::KeyUp(int keyId)
    {
        m_world->KeyUp(keyId);
    }

    void Application::Resize(int w, int h)
    {
        m_width = w;
        m_height = h;
        m_engine->Resize(w, h);
        m_world->Layout(w, h);
    }

    void Application::Tick(float time)
    {
        m_engine->Tick(time);
    }

   
    void Application::Initialize(const char *folder)
    {
        m_documentsPath = folder;
        std::string dbPath = m_documentsPath + "/testlvl";
        m_world->Open(dbPath);
        imguiCreate();
    }

    const float Pi = 3.1415297;
    float g_Fps = 0;
    int counter = 0;
    void Application::Draw()
    {
        sFrameStart = std::chrono::high_resolution_clock::now();
        sam::DrawContext ctx;
        ctx.m_nearfar[0] = 0.1f;
        ctx.m_nearfar[1] = 25.0f;
        ctx.m_nearfar[2] = 100.0f;
        ctx.m_frameIdx = m_frameIdx;
        ctx.m_pWorld = m_world.get();
        ctx.m_numGpuCalcs = 0;
        m_uiMgr->Update(*m_engine, m_width, m_height, ctx);
        m_world->Update(*m_engine, ctx);

        bgfx::setViewRect(0, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(1, 0, 0, uint16_t(m_width), uint16_t(m_height));
        m_engine->Draw(ctx);

        m_frameIdx = bgfx::frame() + 1;
        auto elapsed = std::chrono::high_resolution_clock::now() - sFrameStart;
        long long microseconds = std::chrono::duration_cast<std::chrono::microseconds>(elapsed).count();
        g_Fps = (float)1000000.0f / microseconds;

#if WATCHDOGTHREAD
        if (elapsed < 20ms)
        {
            if (counter++ == 100)
                sWatchDogCheckEnabled = true;
        }
        else
            counter = 0;
#endif
    }
    Application::~Application()
    {

    }
}
