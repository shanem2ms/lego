#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "World.h"
#include "imgui.h"
#include "dear-imgui/ImGuiFileDialog.h"
#include <chrono>

namespace sam
{

    // Iphone 11pro max size as 1.0.
    static float unitWidth = 2778.0f;
    static float unitHeight = 1284.0f;

    UIControl::UIControl(float x, float y, float w, float h) :
        m_x(x),
        m_y(y),
        m_width(w),
        m_height(h),
        m_isInit(false),
        m_background(1.0f, 1.0f, 1.0f, 1.0f), 
        m_touchDown(0, 0),
        m_touchPos(0, 0),
        m_buttonDown(false),
        m_isVisible(true)
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

    UIControl *UIControl::IsHit(float x, float y, int buttonId)
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

    bool UIManager::MouseDown(float x, float y, int buttonId)
    {
        m_touchPos = m_touchDown = gmtl::Vec2f(x, y);

        g_buttonDown = m_buttonDown = 1;

        return false;
    }

    bool UIManager::MouseDrag(float x, float y, int buttonId)
    {
        m_touchPos = gmtl::Vec2f(x, y);

        return true;
    }   

    bool UIManager::WheelScroll(float delta)
    {
        m_wheelDelta += delta;
        return true;
    }

    void UIManager::Update(Engine& engine, int w, int h, DrawContext& ctx)
    {
        if (m_topctrl == nullptr)
        {
            m_topctrl = Build(ctx, unitWidth, unitHeight);
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

        UIContext uictx;
        uictx.width = unitWidth;
        uictx.height = unitHeight;
        // Iphone 11pro max size as 1.0.
        uictx.scaleW = (float)w / unitWidth;
        uictx.scaleH = (float)h / unitHeight;        
        m_topctrl->DrawUI(uictx);

        imguiEndFrame();
    }

    bool UIManager::MouseUp(int buttonId)
    {
        g_buttonDown = m_buttonDown = 0;
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
    inline ImVec2 ToIM(const UIContext &ctx, const float &x, const float &y)
    { return ImVec2(x * ctx.scaleW, y * ctx.scaleH); }

    inline ImVec2 ToIMScl(const UIContext& ctx, const float& x, const float& y)
    {
        return ImVec2(
            (x < 0 ? x + ctx.width : x) * ctx.scaleW,
            (y < 0 ? y + ctx.height : y) * ctx.scaleH);
    }

    inline ImVec2 ToIMPos(const UIContext& ctx, const float& x, const float& y)
    {
        return ImVec2(
            (x < 0 ? x + ctx.width : x) * ctx.scaleW,
            (y < 0 ? y + ctx.height : y) * ctx.scaleH);
    }

    void UIStateBtn::DrawUI(UIContext& ctx)
    {
        ImGui::SetCursorPos(ToIMPos(ctx, m_x, m_y));
        ImGui::Button(m_text.c_str(), ToIM(ctx, m_width, m_height));
        bool isDown = ImGui::IsItemActive();
        if (isDown != m_isDown)
            m_stateChanged(isDown);
        m_isDown = isDown;
    }

    UIGroup::UIGroup(float x, float y, float w, float h) :
        UIControl(x, y, w, h),
        m_layout(UILayout::Vertical)
    {}


    UIControl* UIGroup::IsHit(float x, float y, int buttonId)
    {
        float lx = x - m_x;
        float ly = y - m_y;
        for (const auto& ctrl : m_controls)
        {
            UIControl* pHit = ctrl->IsHit(lx, ly, buttonId);
            if (pHit != nullptr)
                return pHit;
        }
        return nullptr;
    }


    void UIGroup::AddControl(std::shared_ptr<UIControl> ctrl)
    {
        m_controls.push_back(ctrl);
    }

    void UIGroup::RemoveControl(const UIControl* ctrl)
    {
        auto itctrl = std::find_if(m_controls.begin(), m_controls.end(), 
            [ctrl](const std::shared_ptr<UIControl>& item) { return item.get() == ctrl; });
        if (itctrl != m_controls.end()) m_controls.erase(itctrl);
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

    UIWindow::UIWindow(float x, float y, float w, float h, const std::string& name,
        bool invisible, bool titleBar) :
        UIGroup(x, y, w, h),
        m_titleBar(titleBar),
        m_name(name),
        m_isinvisible(invisible)
    {

    }

    UIControl* UIWindow::IsHit(float x, float y, int buttonId)
    {
        UIControl *pHit = UIGroup::IsHit(x, y, buttonId);
        if (pHit != nullptr)
            return pHit;

        if (m_isinvisible)
            return nullptr;

        return (x >= m_x && x < (m_x + m_width) &&
            y >= m_y && y < (m_y + m_height)) ? this : nullptr;
    }

    void UIWindow::DrawUI(UIContext& ctx)
    {
        if (!m_isVisible)
            return;
        
        int x = m_x < 0 ? ctx.width + m_x : m_x;
        int y = m_y < 0 ? ctx.height + m_y : m_y;
        int w = m_width <= 0 ? ctx.width + m_width : m_width;
        int h = m_height <= 0 ? ctx.height + m_height : m_height;
        ImGui::SetNextWindowPos(
            ToIM(ctx, x, y), m_isinvisible ? ImGuiCond_Always : ImGuiCond_Appearing);

        if (w > 0)
        {
            ImGui::SetNextWindowSize(ToIM(ctx, w, h),
                m_isinvisible ? ImGuiCond_Always : ImGuiCond_Appearing
            );
        }
        bool isopen = m_isVisible;
        ImGui::Begin(m_name.c_str(), &isopen,
            m_isinvisible ? (
            ImGuiWindowFlags_NoBackground |
            ImGuiWindowFlags_NoTitleBar |
            ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove) : 
            (m_titleBar ? 0 : ImGuiWindowFlags_NoTitleBar) |
            ImGuiWindowFlags_NoResize);
        if (isopen != m_isVisible && m_onOpenChangedFn != nullptr)
        {
            m_onOpenChangedFn(isopen);
            m_isVisible = false;
        }

        ImGui::SetWindowFontScale(ctx.scaleW);
        ImVec2 pos = ImGui::GetWindowPos();
        m_x = pos.x / ctx.scaleW;
        m_y = pos.y / ctx.scaleH;

        ImVec2 size = ImGui::GetWindowSize();
        int clientW = size.x / ctx.scaleW;
        int clientH = size.y / ctx.scaleH;

        m_isVisible = isopen;
        UIContext subCtx = ctx;
        subCtx.width = clientW;
        subCtx.height = clientH;
        UIGroup::DrawUI(subCtx);
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
        if (!m_isVisible)
            return;

        if (ctx.layout == UILayout::Horizontal)
            ImGui::SameLine();

        ImGuiWindowFlags window_flags = 0;
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 5.0f);
        float x = ImGui::GetCursorPosX();
        float y = ImGui::GetCursorPosY();
        ImGui::SetCursorPosX(x + (float)m_x * ctx.scaleW);
        ImGui::SetCursorPosY(y + (float)m_y * ctx.scaleH);
        ImVec2 size = ToIMScl(ctx, m_width, m_height);
        ImGui::BeginChild(m_name.c_str(), size, true, window_flags);
        float oldW = ctx.width;
        float oldH = ctx.height;
        ctx.width = size.x;
        ctx.height = size.y;
        UIGroup::DrawUI(ctx);
        ctx.width = oldW;
        ctx.height = oldH;
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
        ImGui::Columns(m_columns, nullptr, false);
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
                ImGui::PushID(curIdx);
                if (item.colorRect != 0)
                    ImGui::PushStyleColor(ImGuiCol_Button, (ImVec4)ImColor(item.colorRect));
                else if (m_selectedIdx == curIdx)
                    ImGui::PushStyleColor(ImGuiCol_Button, (ImVec4)ImColor(0.6f, 0.2, 0.4, 0.75f));
                if (bgfx::isValid(item.image))
                    ImGui::ImageButton(item.image, ToIM(ctx, 160, 160), ImVec2(0,0), ImVec2(1,1), -1,
                        ImVec4(0,0,0,0), (ImVec4)ImColor(item.imgTint));
                else
                    ImGui::Button(item.text.c_str(), ImVec2(-1, 0));
                if (item.colorRect != 0)
                    ImGui::PopStyleColor();
                else if (m_selectedIdx == curIdx)
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
                if (m_tooltipFn != nullptr && ImGui::IsItemHovered())
                {
                    ImGui::BeginTooltip();
                    ImGui::PushTextWrapPos(ImGui::GetFontSize() * 35.0f * ctx.scaleW);
                    std::string tooltipstr;
                    m_tooltipFn(curIdx, tooltipstr);
                    ImGui::TextUnformatted(tooltipstr.c_str());
                    ImGui::PopTextWrapPos();
                    ImGui::EndTooltip();
                }
                ImGui::PopID();
                ImGui::NextColumn();
                curIdx++;
            }
            ImGui::PopStyleColor(2);
        }
        clipper.End();
        ImGui::Columns(1);
    }

    UIFileDialog::UIFileDialog(float x, float y, float w, float h,
        const std::string &dir,
        std::function<void(bool, const std::string& file)> resultFunc) :
        UIControl(x, y, w, h),
        m_isOpen(false),
        m_dir(dir),
        m_resultFunc(resultFunc)
    {

    }

    void UIFileDialog::DrawUI(UIContext& ctx)
    {
        if (!m_isVisible)
            return;
        if (!m_isOpen)
        {
            ImGuiFileDialog::Instance()->OpenDialog("ChooseFileDlgKey", "Import File", ".zmbx", m_dir.c_str(),
                1, nullptr, ImGuiFileDialogFlags_NoDialog);
        }
        ImVec2 size = ToIMScl(ctx, m_width, m_height);
        if (ImGuiFileDialog::Instance()->Display("ChooseFileDlgKey",
            ImGuiWindowFlags_NoCollapse, size, size))
        {
            bool isOk = false;
            std::string filename;
            if (ImGuiFileDialog::Instance()->IsOk())
            {
                isOk = true;
                filename = ImGuiFileDialog::Instance()->GetFilePathName();
            }
            ImGuiFileDialog::Instance()->Close();
            m_isOpen = false;
            m_isVisible = false;
            m_resultFunc(isOk, filename);
        }
    }

    UIControl* UIFileDialog::IsHit(float x, float y, int buttonId)
    {
        return nullptr;
    }

}