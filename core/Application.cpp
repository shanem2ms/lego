#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "LegoUI.h"
#include "BrickMgr.h"
#include "World.h"
#include "PlayerView.h"
#include "Audio.h"
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

    Application::Application(const std::string& startupPath) :
        m_height(0),
        m_width(0),
        m_frameIdx(0),
        m_startupPath(startupPath),
        m_rawMouseMode(false)
    {
        s_pInst = this;
        m_engine = std::make_unique<Engine>();
        m_world = std::make_unique<World>();
        m_audio = std::make_unique<Audio>();
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
        return *m_legoUI;
    }

    void Application::ActivateUI()
    {
        if (m_hideMouseCursorFn != nullptr)
            m_hideMouseCursorFn(false);
        m_rawMouseMode = false;
        m_legoUI->OpenInventory([this]()
            {
                if (m_hideMouseCursorFn)
                    m_hideMouseCursorFn(true);
                m_rawMouseMode = true;
            });

        m_legoUI->OnPartSelected([this](const PartId& partname)
            {
                PartInst pi = m_world->GetPlayer()->GetRightHandPart();
                pi.id = partname;
                m_world->GetPlayer()->SetRightHandPart(pi);
            });

        m_legoUI->OnColorSelected([this](int idx)
            {
                PartInst pi = m_world->GetPlayer()->GetRightHandPart();
                pi.paletteIdx = idx;
                m_world->GetPlayer()->SetRightHandPart(pi);
            });
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

    void Application::MouseDown(float x, float y, int buttonId)
    {
        if (!m_legoUI->MouseDown(x, y, buttonId))
        {
            if (!m_rawMouseMode)
                m_rawMouseMode = m_hideMouseCursorFn(true);
            else
                m_world->MouseDown(x, y, buttonId);
        }
    }

    void Application::MouseMove(float x, float y, int buttonId)
    {
        if (!m_legoUI->MouseDrag(x, y, buttonId))
            m_world->MouseDrag(x, y, buttonId);
    }

    void Application::MouseUp(int buttonId)
    {
        if (!m_legoUI->MouseUp(buttonId))
            m_world->MouseUp(buttonId);
    }

    void Application::WheelScroll(float delta)
    {
        if (!m_legoUI->WheelScroll(delta))
            m_world->WheelScroll(delta);
    }

    void Application::KeyDown(int keyId)
    {
        if (keyId == 0x1B) // Escape
        {
            if (m_rawMouseMode)
            {
                ActivateUI();
            }
            else
            {
                m_legoUI->CloseInventory();
            }
        }
        else if (m_legoUI->IsActive())
            m_legoUI->KeyDown(keyId);
        else
            m_world->KeyDown(keyId);
    }

    void Application::KeyUp(int keyId)
    {
        if (m_legoUI->IsActive())
            m_legoUI->KeyUp(keyId);
        else
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
        imguiCreate(32.0f);
        m_brickManager = std::make_unique<BrickManager>("c:\\ldraw");
        m_engine->AddExternalDraw(m_brickManager.get());
        m_legoUI = std::make_unique<LegoUI>();
        ActivateUI();
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
        ctx.m_pickedItem = nullptr;
        ctx.debugDraw = false;
        m_engine->UpdatePickData(ctx);
        m_legoUI->Update(*m_engine, m_width, m_height, ctx);
        m_world->Update(*m_engine, ctx);

        bgfx::setViewRect(DrawViewId::DeferredObjects, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(DrawViewId::DeferredLighting, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(DrawViewId::ForwardRendered, 0, 0, uint16_t(m_width), uint16_t(m_height));

        bgfx::setViewRect(DrawViewId::PickObjects, 0, 0, PickBufSize, PickBufSize);
        bgfx::setViewRect(DrawViewId::PickBlit, 0, 0, PickBufSize, PickBufSize);

        ctx.m_pickViewScale[0] = (float)(m_width) / (float)(PickBufSize);
        ctx.m_pickViewScale[1] = (float)(m_height) / (float)(PickBufSize);

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
