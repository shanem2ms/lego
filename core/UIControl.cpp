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

    void UIControl::Update(const std::shared_ptr<SceneGroup>& group, DrawContext& ctx)
    {
        if (!m_isInit)
        {
            Initialize(group, ctx);
            m_isInit = true;
        }
    }

    bool UIControl::IsHit(float x, float y, int touchId)
    {
        return x >= m_x && x < (m_x + m_width) &&
            y >= m_y && y < (m_y + m_height);
    }


    void UIManager::AddControl(std::shared_ptr<UIControl> ctrl)
    {
        m_controls.push_back(ctrl);
    }

    int g_buttonDown = 0;

    bool UIManager::TouchDown(float x, float y, int touchId)
    {
        m_touchPos = m_touchDown = gmtl::Vec2f(x, y);

        g_buttonDown = m_buttonDown = 1;

        bool handled = false;
        for (auto uictrl : m_controls)
        {
            if (uictrl->IsHit(x, y, touchId) &&
                uictrl->TouchDown(x, y, touchId))
            {
                m_capturedCtrl = uictrl;
                handled = true;
                break;
            }
        }

        return handled;
    }

    bool UIManager::TouchDrag(float x, float y, int touchId)
    {
        m_touchPos = gmtl::Vec2f(x, y);

        if (m_capturedCtrl != nullptr)
        {
            m_capturedCtrl->TouchDrag(x, y, touchId);
            return true;
        }
        return false;
    }

    static int sButtonStates[256];

    void UIManager::Update(Engine& engine, int w, int h, DrawContext& ctx)
    {
        if (m_uiGroup == nullptr)
        {
            memset(sButtonStates, 0, sizeof(sButtonStates));
            m_uiGroup = std::make_shared<SceneGroup>();
            m_uiGroup->SetOffset(Point3f(0, 0, w / 2));
            engine.Root()->AddItem(m_uiGroup);
        }
        for (auto ctrl : m_controls)
        {
            ctrl->Update(m_uiGroup, ctx);
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
        ImGui::SetNextWindowPos(
            ImVec2(w - btnSize * 6, h - btnSize * 3)
            , ImGuiCond_Always
        );

        int buttonsThisFrame[256];
        memset(buttonsThisFrame, 0, sizeof(buttonsThisFrame));
        ImGui::Begin("make window", nullptr,
            ImGuiWindowFlags_NoBackground |
            ImGuiWindowFlags_NoTitleBar |
            ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove);

        ImGui::SetCursorPos(ImVec2(btnSize + btnSpace * 2, 0));
        ImGui::Button(ICON_FA_CHEVRON_UP, ImVec2(btnSize, btnSize));
        buttonsThisFrame['W'] = ImGui::IsItemActive();
        ImGui::SetCursorPos(ImVec2(btnSize + btnSpace * 2, btnSize + btnSpace));
        ImGui::Button(ICON_FA_CHEVRON_DOWN, ImVec2(btnSize, btnSize));
        buttonsThisFrame['S'] = ImGui::IsItemActive();
        ImGui::SetCursorPos(ImVec2(btnSize * 2 + btnSpace * 4, btnSize / 2));
        ImGui::Button(ICON_FA_CHEVRON_RIGHT, ImVec2(btnSize, btnSize));
        buttonsThisFrame['D'] = ImGui::IsItemActive();
        ImGui::SetCursorPos(ImVec2(0, btnSize / 2));
        ImGui::Button(ICON_FA_CHEVRON_LEFT, ImVec2(btnSize, btnSize));
        buttonsThisFrame['A'] = ImGui::IsItemActive();

        ImGui::SetCursorPos(ImVec2(btnSize * 4 + btnSpace * 4, 0));
        ImGui::Button(ICON_FA_CARET_SQUARE_O_UP, ImVec2(btnSize, btnSize));
        buttonsThisFrame[32] = ImGui::IsItemActive();

        ImGui::SetCursorPos(ImVec2(btnSize * 4 + btnSpace * 4, btnSize + btnSpace));
        ImGui::Button(ICON_FA_CARET_SQUARE_O_DOWN, ImVec2(btnSize, btnSize));
        buttonsThisFrame[16] = ImGui::IsItemActive();

        ImGui::End();

        imguiEndFrame();

        for (int i = 0; i < 256; ++i)
        {
            if (buttonsThisFrame[i] != sButtonStates[i])
            {
                if (buttonsThisFrame[i] > 0)
                    ctx.m_pWorld->KeyDown(i);
                else
                    ctx.m_pWorld->KeyUp(i);
            }
        }
        memcpy(sButtonStates, buttonsThisFrame, sizeof(sButtonStates));
    }

    bool UIManager::TouchUp(int touchId)
    {
        g_buttonDown = m_buttonDown = 0;
        if (m_capturedCtrl != nullptr)
        {
            m_capturedCtrl->TouchUp(touchId);
            m_capturedCtrl = nullptr;
            return true;
        }

        return false;
    }

    UIButton::UIButton(const std::string& text, float x, float y, float w, float h) :
        UIControl(x, y, w, h),
        m_text(text)
    {
        m_background = Vec4f(0.6f, 0.6f, 0.6f, 1.0f);
        m_border = Vec4f(0.8f, 0.8f, 0.8f, 1.0f);
        m_pressedColor = Vec4f(0.9f, 0.6f, 0.6f, 1.0f);
    }

    void UIButton::Initialize(const std::shared_ptr<SceneGroup>& group, DrawContext& ctx)
    {
        std::shared_ptr<SceneGroup> grp = std::make_shared<SceneGroup>();
        Vec3f scl = Vec3f(m_width * 0.5f, m_height * 0.5f, 1);
        grp->SetScale(scl);
        grp->SetOffset(Point3f(m_x + scl[0], m_y + scl[1], 0));

        m_rect = std::make_shared<SceneRect>();
        m_rect->SetColor(m_background);
        m_rect->SetStroke(m_border, 3.0f);
        grp->AddItem(m_rect);
        std::shared_ptr<SceneText> text = std::make_shared<SceneText>();
        text->SetOffset(Point3f(0, 0.5f, 0));
        text->SetText(m_text);
        text->SetColor(Vec4f(0, 0, 0, 1));
        text->SetFont("roboto", m_height * 0.6f);
        grp->AddItem(text);

        group->AddItem(grp);
    }

    bool UIButton::TouchDown(float x, float y, int touchId)
    {
        m_rect->SetColor(m_pressedColor);
        if (m_pressed != nullptr)
            m_pressed(touchId);
        return true;
    }

    bool UIButton::TouchDrag(float x, float y, int touchId)
    {
        return true;
    }

    bool UIButton::TouchUp(int touchId)
    {
        m_rect->SetColor(m_background);
        return true;
    }
    void UIButton::OnPressed(const UIButton::PressedFunc& pressed)
    {
        m_pressed = pressed;
    }

}