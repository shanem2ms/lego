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

    bool UIManager::WheelScroll(float delta)
    {
        m_wheelDelta += delta;
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
            , (uint32_t)m_wheelDelta
            , (uint16_t)w
            , (uint16_t)h
        );

        //m_wheelDelta = 0;
        const int btnSize = 150;
        const int btnSpace = 10;
        UIContext uictx;
        m_topctrl->DrawUI(uictx);

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

    void UIStateBtn::DrawUI(UIContext& ctx)
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
        UIControl(x, y, w, h),
        m_layout(UILayout::Vertical)
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

    void UIGroup::DrawUI(UIContext &ctx)
    {
        UILayout oldLayout = ctx.layout;
        ctx.layout = m_layout;
        for (const auto& control : m_controls)
        {
            control->DrawUI(ctx);
        }
        ctx.layout = oldLayout;
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

    void UIWindow::DrawUI(UIContext& ctx)
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

        m_isopen = isopen;
        UIGroup::DrawUI(ctx);
        ImGui::End();
    }

    static int sUICnt = 0;
    UIPanel::UIPanel(float x, float y, float w, float h) :
        UIGroup(x, y, w, h)
    {
        m_name = "UIPanel" + std::to_string(sUICnt++);
    }
       
    void UIPanel::DrawUI(UIContext& ctx)
    {
        if (ctx.layout == UILayout::Horizontal)
            ImGui::SameLine();

        ImGuiWindowFlags window_flags = 0;
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 5.0f);
        float x = ImGui::GetCursorPosX();
        float y = ImGui::GetCursorPosY();
        ImGui::SetCursorPosX(x + (float)m_x);
        ImGui::SetCursorPosY(y + (float)m_y);
        ImGui::BeginChild(m_name.c_str(), ImVec2(m_width, m_height), true, window_flags);
        
        UIGroup::DrawUI(ctx);
        ImGui::EndChild();
        ImGui::PopStyleVar();
    }

    UITable::UITable(int columns) :
        UIControl(0, 0, 0, 0),
        m_columns(columns),
        m_itemcount(0),
        m_selectedIdx(-1)
    {

    }

    void UITable::DrawUI(UIContext& ctx)
    {
        if (m_itemcount == 0)
            return;
        ImGui::Columns(m_columns);
        ImGuiListClipper clipper;
        clipper.Begin(m_itemcount / m_columns);
        while (clipper.Step())
        {
            int size = clipper.DisplayEnd - clipper.DisplayStart;
            size *= m_columns;
            std::vector<TableItem> tableItemsCache(size);
            int startIdx = clipper.DisplayStart * m_columns;
            size = std::min(m_itemcount - startIdx, size);
            m_drawItemsFn(startIdx,
                size, tableItemsCache.data());

            ImGui::PushStyleColor(ImGuiCol_Button, (ImVec4)ImColor(1.0f, 1.0f, 1.0f, 0.0f));
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, (ImVec4)ImColor(0.2f, 0.5f, 0.7f, 0.5f));

            int curIdx = startIdx;
            for (auto& item : tableItemsCache)
            {
                if (m_selectedIdx == curIdx)
                    ImGui::PushStyleColor(ImGuiCol_Button, (ImVec4)ImColor(0.6f, 0.2, 0.4, 0.75f));
                if (bgfx::isValid(item.image))
                    ImGui::ImageButton(item.image, ImVec2(128, 128));
                else
                    ImGui::Button(item.text.c_str());
                if (m_selectedIdx == curIdx)
                    ImGui::PopStyleColor();
                if (ImGui::IsItemActive())
                {
                    if (m_selectedIdx != curIdx)
                    {
                        m_selectedIdx = curIdx;
                        if (m_itemSelectedFn != nullptr)
                            m_itemSelectedFn(curIdx);
                    }
                }
                ImGui::NextColumn();
                curIdx++;
            }
            ImGui::PopStyleColor(2);
        }
        clipper.End();
        ImGui::Columns(1);
    }

    
}