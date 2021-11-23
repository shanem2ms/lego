#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "World.h"
#include "imgui.h"
#include <chrono>

namespace sam
{

    UIControl::UIControl(float x, float y, float w, float h) :
        m_x(x),
        m_y(y),
        m_width(w),
        m_height(h),
        m_isInit(false),
        m_background(1.0f, 1.0f, 1.0f, 1.0f), 
        m_touchDown(0, 0),
        m_touchPos(0, 0),
        m_buttonDown(false)
    {

    }

    void UIControl::SetBackgroundColor(const Vec4f& color)
    {
        m_background = color;
    }
    void UIControl::SetBorderColor(const Vec4f& color)
    {
        m_border = color;
    }

    UIControl *UIControl::IsHit(float x, float y, int touchId)
    {
        return (x >= m_x && x < (m_x + m_width) &&
            y >= m_y && y < (m_y + m_height)) ? this : nullptr;
    }

    int g_buttonDown = 0;

    bool UIManager::TouchDown(float x, float y, int touchId)
    {
        m_touchPos = m_touchDown = gmtl::Vec2f(x, y);

        g_buttonDown = m_buttonDown = 1;

        UIControl* pCtrl = m_topctrl->IsHit(x, y, touchId);
        if (pCtrl != nullptr)
        {
            m_capturedCtrl = pCtrl;
            return true;
        }

        return false;
    }

    bool UIManager::TouchDrag(float x, float y, int touchId)
    {
        m_touchPos = gmtl::Vec2f(x, y);

        if (m_capturedCtrl != nullptr)
        {
            return true;
        }
        return true;
    }

    void UIManager::Update(Engine& engine, int w, int h, DrawContext& ctx)
    {
        if (m_topctrl == nullptr)
        {
            
            const int btnSize = 150;
            const int btnSpace = 10;

            World* pWorld = ctx.m_pWorld;

            std::shared_ptr<UIWindow> top = std::make_shared<UIWindow>(0, 0, 0, 0, "top", true);
            std::shared_ptr<UIWindow> wnd = std::make_shared<UIWindow>(w - btnSize * 6, h - btnSize * 3, 0, 0, "controls", true);
            m_topctrl = top;
            top->AddControl(wnd);
            wnd->AddControl(std::make_shared<UIStateBtn>(btnSize + btnSpace * 2, 0, btnSize, btnSize, ICON_FA_CHEVRON_UP,
                [pWorld](bool isBtnDown)
                {
                    char key = 'W';
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));
            wnd->AddControl(std::make_shared<UIStateBtn>(btnSize + btnSpace * 2, btnSize + btnSpace, btnSize, btnSize, ICON_FA_CHEVRON_DOWN,
                [pWorld](bool isBtnDown)
                {
                    char key = 'S';
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));
            wnd->AddControl(std::make_shared<UIStateBtn>(btnSize * 2 + btnSpace * 4, btnSize / 2, btnSize, btnSize, ICON_FA_CHEVRON_RIGHT,
                [pWorld](bool isBtnDown)
                {
                    char key = 'D';
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));
            wnd->AddControl(std::make_shared<UIStateBtn>(0, btnSize / 2, btnSize, btnSize, ICON_FA_CHEVRON_RIGHT,
                [pWorld](bool isBtnDown)
                {
                    char key = 'A';
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));
            wnd->AddControl(std::make_shared<UIStateBtn>(btnSize * 4 + btnSpace * 4, 0, btnSize, btnSize, ICON_FA_CARET_SQUARE_O_UP,
                [pWorld](bool isBtnDown)
                {
                    char key = 32;
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));
            wnd->AddControl(std::make_shared<UIStateBtn>(btnSize * 4 + btnSpace * 4, btnSize + btnSpace, btnSize, btnSize, ICON_FA_CARET_SQUARE_O_DOWN,
                [pWorld](bool isBtnDown)
                {
                    char key = 16;
                    if (isBtnDown) pWorld->KeyDown(key);
                    else pWorld->KeyUp(key);
                }));


            std::shared_ptr<UIWindow> menu = std::make_shared<UIWindow>(100, 100, 200, 200, "bricks", false);
            top->AddControl(menu);

        }

        imguiBeginFrame(m_touchPos[0]
            , m_touchPos[1]
            , m_buttonDown
            , 0
            , uint16_t(w)
            , uint16_t(h)
        );

        const int btnSize = 150;
        const int btnSpace = 10;

        m_topctrl->DrawUI();

        imguiEndFrame();
    }

    bool UIManager::TouchUp(int touchId)
    {
        g_buttonDown = m_buttonDown = 0;
        if (m_capturedCtrl != nullptr)
        {
            m_capturedCtrl = nullptr;
            return true;
        }

        return true;
    }    


    UIStateBtn::UIStateBtn(float x, float y, float w, float h, const std::string& str,
        std::function<void(bool)> stateChanged) :
        UIControl(x, y, w, h),
        m_text(str),
        m_isDown(false),
        m_stateChanged(stateChanged)
    {

    }

    void UIStateBtn::DrawUI()
    {
        ImGui::SetCursorPos(ImVec2(m_x, m_y));
        ImGui::Button(ICON_FA_CHEVRON_UP, ImVec2(m_width, m_height));
        bool isDown = ImGui::IsItemActive();
        if (isDown != m_isDown)
            m_stateChanged(isDown);
        m_isDown = isDown;
    }


    UIWindow::UIWindow(float x, float y, float w, float h, const std::string& name,
            bool invisible) :
        UIGroup(x, y, w, h),
        m_name(name),
        m_isopen(true),
        m_isinvisible(invisible)
    {

    }

    UIGroup::UIGroup(float x, float y, float w, float h) :
        UIControl(x, y, w, h)
    {}


    UIControl* UIGroup::IsHit(float x, float y, int touchId)
    {
        float lx = x - m_x;
        float ly = y - m_y;
        for (const auto& ctrl : m_controls)
        {
            UIControl* pHit = ctrl->IsHit(lx, ly, touchId);
            if (pHit != nullptr)
                return pHit;
        }
        return nullptr;
    }


    void UIGroup::AddControl(std::shared_ptr<UIControl> ctrl)
    {
        m_controls.push_back(ctrl);
    }

    UIControl* UIWindow::IsHit(float x, float y, int touchId)
    {
        UIControl *pHit = UIGroup::IsHit(x, y, touchId);
        if (pHit != nullptr)
            return pHit;

        if (m_isinvisible)
            return nullptr;

        return (x >= m_x && x < (m_x + m_width) &&
            y >= m_y && y < (m_y + m_height)) ? this : nullptr;
    }

    void UIWindow::DrawUI()
    {
        ImGui::SetNextWindowPos(
            ImVec2(m_x, m_y), m_isinvisible ? ImGuiCond_Always : ImGuiCond_Appearing);

        if (m_width > 0)
        {
            ImGui::SetNextWindowSize(ImVec2(m_width, m_height),
                m_isinvisible ? ImGuiCond_Always : ImGuiCond_Appearing
            );
        }
        bool isopen = m_isopen;
        ImGui::Begin(m_name.c_str(), &isopen,
            m_isinvisible ? (
            ImGuiWindowFlags_NoBackground |
            ImGuiWindowFlags_NoTitleBar |
            ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove) : 0);
        if (isopen != m_isopen && m_onOpenChangedFn != nullptr)
            m_onOpenChangedFn(isopen);

        ImVec2 pos = ImGui::GetWindowPos();
        m_x = pos.x;
        m_y = pos.y;

        ImVec2 size = ImGui::GetWindowSize();
        m_width = size.x;
        m_height = size.y;

        m_isopen = isopen;
        for (const auto& control : m_controls)
        {
            control->DrawUI();
        }

        ImGui::End();
    }

}