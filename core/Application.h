#pragma once

#include <memory>
#include <string>
#include <functional>
#include "gmtl/Vec.h"

namespace sam
{

class World;
class Engine;
class UIManager;
class LegoUI;
struct DrawContext;
class BrickManager;
class Audio;
class Application
{
    std::unique_ptr<World> m_world;
    std::unique_ptr<Engine> m_engine;
    std::unique_ptr<LegoUI> m_legoUI;
    std::unique_ptr<Audio> m_audio;
    std::unique_ptr<BrickManager> m_brickManager;
    int m_width;
    int m_height;
    int m_frameIdx;
    std::string m_documentsPath;
    std::string m_startupPath;
    std::function<bool(bool)> m_hideMouseCursorFn;
    bool m_rawMouseMode;
    static void (*m_dbgFunc)(const char*);

public:    
    UIManager& UIMgr();
    Application(const std::string &startupPath);
    ~Application();
    static Application& Inst();
    int FrameIdx() const { return m_frameIdx; }
    void MouseDown(float x, float y, int buttonId);
    void MouseMove(float x, float y, int buttonId);
    void WheelScroll(float delta);
    void MouseUp(int buttonId);
    void RawMouseMoved(int32_t rx, int32_t ry);
    void KeyDown(int keyId);
    void KeyUp(int keyId);
    void Resize(int w, int h);
    void Tick(float time);
    void Draw();
    Audio &GetAudio() 
    { return *m_audio; }
    void Initialize(const char *folder);
    const std::string &Documents() const
    { return m_documentsPath; }    
    const std::string &StartupPath() const
    { return m_startupPath; }
    void SetHideMouseCursorFn(const std::function<bool(bool)>& fn);
    static void SetDebugMsgFunc(void (*dbgfunc)(const char*));
    static void DebugMsg(const std::string& str);
    void OpenInventory();
    void OpenMainMenu();

    void UIImportMbx(const std::string& name);

    void UIQuit();
};

std::shared_ptr< bgfx::CallbackI> CreateCallback();
}
