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

    void UIManager::KeyDown(int keyId)
    {

    }

    void UIManager::KeyUp(int keyId)
    {

    }

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
        if (w != m_width || h != m_height)
        {
            m_topctrl = Build(ctx, w, h);
            m_width = w;
            m_height = h;
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
        if (!m_isopen)
            return;

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
        {
            m_onOpenChangedFn(isopen);
            m_isopen = false;
        }

        ImVec2 pos = ImGui::GetWindowPos();
        m_x = pos.x;
        m_y = pos.y;

        ImVec2 size = ImGui::GetWindowSize();
        m_width = size.x;
        m_height = size.y;


        ImGuiWindowFlags window_flags = ImGuiWindowFlags_HorizontalScrollbar;
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 5.0f);
        ImGui::BeginChild("ChildR", ImVec2(0, 0), true, window_flags);
        /*
        if (ImGui::BeginTable("split", 2, ImGuiTableFlags_Resizable | ImGuiTableFlags_NoSavedSettings))
        {
            for (int i = 0; i < 100; i++)
            {
                char buf[32];
                sprintf(buf, "%03d", i);
                if (ImGui::TableNextColumn())
                    ImGui::Button(buf, ImVec2(-FLT_MIN, 0.0f));
            }
            ImGui::EndTable();
        }
        ImGui::EndChild();
        */
        ImGui::Columns(10);
        // Also demonstrate using clipper for large vertical lists
        int ITEMS_COUNT = 2000;
        ImGuiListClipper clipper;
        clipper.Begin(ITEMS_COUNT);
        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                for (int j = 0; j < 10; j++)
                {
                    ImGui::Text("[%d %d]...", i, j);
                    ImGui::NextColumn();
                }
        }
        ImGui::Columns(1);
        ImGui::EndChild();
        ImGui::PopStyleVar();

        m_isopen = isopen;
        for (const auto& control : m_controls)
        {
            control->DrawUI();
        }

        ImGui::End();
    }

}