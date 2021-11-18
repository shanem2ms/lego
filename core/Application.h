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
struct DrawContext;
class Application
{
    std::unique_ptr<World> m_world;
    std::unique_ptr<Engine> m_engine;
    std::unique_ptr<UIManager> m_uiMgr;
    int m_width;
    int m_height;
    int m_frameIdx;
    std::string m_documentsPath;
    std::function<bool(bool)> m_hideMouseCursorFn;

public:    
    UIManager& UIMgr();
    Application();
    ~Application();
    static Application& Inst();
    int FrameIdx() const { return m_frameIdx; }
    void TouchDown(float x, float y, int touchId);
    void TouchMove(float x, float y, int touchId);
    void TouchUp(int touchId);
    void RawMouseMoved(uint32_t rx, uint32_t ry);
    void KeyDown(int keyId);
    void KeyUp(int keyId);
    void Resize(int w, int h);
    void Tick(float time);
    void Draw();
    void Initialize(const char *folder);
    const std::string &Documents() const
    { return m_documentsPath; }    
    void SetHideMouseCursorFn(const std::function<bool(bool)>& fn);
};

}
