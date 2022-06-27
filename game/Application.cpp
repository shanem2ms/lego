#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "LegoUI.h"
#include "BrickMgr.h"
#include "World.h"
#include "GameController.h"
#include "PlayerView.h"
#include "Audio.h"
#include "imgui.h"
#include "Enet.h"
#include "Server.h"
#include <filesystem>
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
    using namespace std::chrono_literals;
    static std::chrono::milliseconds timeoutVal = 200ms;
    static bool sWatchDogCheckEnabled = false;
    void WatchDogFunc();
#endif

    Application::Application() :
        m_height(0),
        m_width(0),
        m_frameIdx(0),
        m_rawMouseMode(false),
        m_touchMode(true)
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
            if (sWatchDogCheckEnabled && elapsed > timeoutVal)
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

    void Application::Initialize(const char* startFolder, const char* docFolder, const char* server, bool touchMode)
    {
        m_touchMode = touchMode;
        if (m_touchMode)
        {
            m_gameController = std::make_unique<GameController>();
            m_gameController->ConnectPlayer(m_world->GetPlayer(), m_world.get());
        }
        std::string servername("localhost");
        if (server != nullptr && strlen(server) > 0)
            servername = server;
        m_client = std::make_unique<ENetClient>(servername, 8000);
        //m_localsvr->Start()
        m_startupPath = startFolder;
        m_documentsPath = docFolder;
        std::filesystem::path path(m_documentsPath);
        path = path / "testlvl";
        m_localsvr = std::make_unique<Server>();
        m_localsvr->Start(path.string(), "localhost", 8000);
        m_world->Open(m_client.get());
        imguiCreate(32.0f);
        m_brickManager = std::make_unique<BrickManager>();
        m_engine->AddExternalDraw(m_brickManager.get());
        if (m_gameController != nullptr)
            m_engine->AddExternalDraw(m_gameController.get());
        m_legoUI = std::make_unique<LegoUI>();
        m_world->OnShowInventory([this]()
            {
                OpenInventory();
            });
        OpenMainMenu();
    }
    UIManager& Application::UIMgr()
    {
        return *m_legoUI;
    }

    void Application::OpenMainMenu()
    {
        if (m_hideMouseCursorFn != nullptr)
            m_hideMouseCursorFn(false);
        m_rawMouseMode = false;
        m_legoUI->MainMenu().Open([this]()
            {
                if (m_hideMouseCursorFn)
                    m_hideMouseCursorFn(true);
                m_rawMouseMode = true;
            });

    }
    void Application::OpenInventory()
    {
        if (m_hideMouseCursorFn != nullptr)
            m_hideMouseCursorFn(false);
        m_rawMouseMode = false;
        m_legoUI->Inventory().Open([this]()
            {
                if (m_hideMouseCursorFn)
                    m_hideMouseCursorFn(true);
                m_rawMouseMode = true;
            });

        m_legoUI->Inventory().OnPartSelected([this](const PartId& partname)
            {
                auto player = m_world->GetPlayer();
                player->ReplaceCurrentPart(partname);
            });

        m_legoUI->Inventory().OnColorSelected([this](int idx)
            {
                const BrickColor& bc = BrickManager::Inst().GetColorFromIdx(idx);
                auto player = m_world->GetPlayer();
                player->ReplaceCurrentPartColor(bc);
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
            m_world->GetPlayer()->RawMove((float)rx * mScale, (float)-ry * mScale);
        }
    }

    void Application::MouseDown(float x, float y, int buttonId)
    {
        if (!m_legoUI->MouseDown(x, y, buttonId))
        {
            if (!m_rawMouseMode)
                m_rawMouseMode = m_hideMouseCursorFn(true);
            else
                m_world->GetPlayer()->MouseDown(x, y, buttonId);
        }
    }

    void Application::MouseMove(float x, float y, int buttonId)
    {
        if (!m_legoUI->MouseDrag(x, y, buttonId))
            m_world->GetPlayer()->MouseDrag(x, y, buttonId);
    }

    void Application::MouseUp(int buttonId)
    {
        if (!m_legoUI->MouseUp(buttonId))
            m_world->GetPlayer()->MouseUp(buttonId);
    }

    void Application::WheelScroll(float delta)
    {
        if (!m_legoUI->WheelScroll(delta))
            m_world->GetPlayer()->WheelScroll(delta);
    }

    void Application::KeyDown(int keyId)
    {
        if (keyId == 0x1B) // Escape
        {
            if (m_rawMouseMode)
            {
                OpenMainMenu();
            }
            else
            {
                m_legoUI->CloseAll();
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
        if (m_gameController != nullptr)
            m_gameController->SetSize(w, h);
        m_engine->Resize(w, h);
        m_world->Layout(w, h);
    }

    void Application::Tick(float time)
    {
        m_engine->Tick(time);
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
        ctx.m_pPlayer = m_world->GetPlayer().get();
        ctx.m_numGpuCalcs = 0;
        ctx.m_pickedItem = nullptr;
        ctx.debugDraw = false;
        m_engine->UpdatePickData(ctx);
        if (m_gameController != nullptr)
            m_gameController->Update(ctx);
        m_legoUI->Update(*m_engine, m_width, m_height, ctx);
        m_world->Update(*m_engine, ctx);

        bgfx::setViewRect(DrawViewId::MainObjects, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(DrawViewId::DeferredLighting, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(DrawViewId::ForwardRendered, 0, 0, uint16_t(m_width), uint16_t(m_height));
        bgfx::setViewRect(DrawViewId::HUD, 0, 0, uint16_t(m_width), uint16_t(m_height));

        bgfx::setViewRect(DrawViewId::PickObjects, 0, 0, PickBufSize, PickBufSize);
        bgfx::setViewRect(DrawViewId::PickBlit, 0, 0, PickBufSize, PickBufSize);

        ctx.m_pickViewScale[0] = (float)(m_width) / (float)(PickBufSize);
        ctx.m_pickViewScale[1] = (float)(m_height) / (float)(PickBufSize);

        m_engine->Draw(ctx);

        m_frameIdx++;// = bgfx::frame() + 1;
        auto elapsed = std::chrono::high_resolution_clock::now() - sFrameStart;
        long long microseconds = std::chrono::duration_cast<std::chrono::microseconds>(elapsed).count();
        g_Fps = (float)1000000.0f / microseconds;

#if WATCHDOGTHREAD
        if (elapsed < timeoutVal)
        {
            if (counter++ == 100)
                sWatchDogCheckEnabled = true;
        }
        else
            counter = 0;
#endif
    }

    void Application::TouchDown(float x, float y, uint64_t touchId)
    {
        if (!m_legoUI->MouseDown(x, y, 0))
        { 
            m_gameController->TouchDown(x, y, touchId);
        }
    }

    void Application::TouchMove(float x, float y, uint64_t touchId)
    {
        if (!m_legoUI->MouseDrag(x, y, 0))
        {
            m_gameController->TouchMove(x, y, touchId);
        }

    }

    void Application::TouchUp(float x, float y, uint64_t touchId)
    {
        if (!m_legoUI->MouseUp(0))
        {
            m_gameController->TouchUp(x, y, touchId);
        }
    }

    void Application::UIImportMbx(const std::string& name)
    {
        m_world->ImportMbx(name);
    }

    void Application::UIQuit()
    {

    }

    Application::~Application()
    {

    }
}
