#include "StdIncludes.h"
#include "Application.h"
#include <bgfx/bgfx.h>
#include "Engine.h"
#include "UIControl.h"
#include "World.h"
#include "imgui.h"
#include "LegoUI.h"
#include <chrono>
#include "BrickMgr.h"

namespace sam
{
    std::shared_ptr<UIControl> LegoUI::Build(DrawContext &ctx, int w, int h)
    {
        const int btnSize = 150;
        const int btnSpace = 10;

        World* pWorld = ctx.m_pWorld;

        std::shared_ptr<UIWindow> top = std::make_shared<UIWindow>(0, 0, 0, 0, "top", true);
        m_topctrl = top;
        std::shared_ptr<UIWindow> wnd = std::make_shared<UIWindow>(w - btnSize * 6, h - btnSize * 3, 0, 0, "controls", true);        
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

        std::shared_ptr<UIWindow> menu = std::make_shared<UIWindow>(650, 250, 1280, 700, "bricks", false);
        menu->OnOpenChanged([this](bool isopen) {
            if (!isopen) Deactivate(); });
        
        int padding = 10;
        int numColumns = 1280 / (128 + padding);
        m_partsTable = std::make_shared<UITable>(numColumns);
        int numRows = BrickManager::Inst().NumParts() / numColumns;

        m_partsTable->SetItems(numRows, [numColumns](int start, int end)
            {
                for (int r = start; r < end; r++)
                {
                    for (int c = 0; c < numColumns; c++)
                    {
                        int i = r * numColumns + c;
                        const std::string& name = BrickManager::Inst().PartName(i);
                        Brick* pBrick = BrickManager::Inst().GetBrick(name);
                        std::string btntext = std::to_string(i) + ": " + name;
                        if (pBrick->m_icon.isValid())
                            ImGui::ImageButton(pBrick->m_icon, ImVec2(128, 128));
                        else
                            ImGui::Button(btntext.c_str());
                        ImGui::NextColumn();
                    }
                }
            });

        auto panel = std::make_shared<UIPanel>(0, 0, 0, 0);
        panel->AddControl(m_partsTable);
        menu->AddControl(panel);
        top->AddControl(menu);
        m_mainMenu = menu;

        return top;
    }

    void LegoUI::ActivateUI(const std::function<void()>& deactivateFn)
    {
        if (m_mainMenu)
            m_mainMenu->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;        
    }

    bool LegoUI::TouchDown(float x, float y, int touchId)
    {
        bool ret = UIManager::TouchDown(x, y, touchId);
        return m_isActive;
    }

    bool LegoUI::TouchDrag(float x, float y, int touchId)
    {
        bool ret = UIManager::TouchDrag(x, y, touchId);
        return m_isActive;
    }

    bool LegoUI::TouchUp(int touchId)
    {
        bool ret = UIManager::TouchUp(touchId);
        return m_isActive;
    }
}