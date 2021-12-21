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
        menu->SetLayout(UILayout::Horizontal);

        
        auto typesTable = std::make_shared<UITable>(1);
        int numTypes = BrickManager::Inst().NumTypes();
        typesTable->SetItems(numTypes, [](int start, int count, UITable::TableItem items[])
            {
                for (int r = 0; r < count; r++)
                {
                    std::string typeName = 
                        BrickManager::Inst().TypeName(r + start);
                    items[r].text = typeName;
                }
            });

        typesTable->OnItemSelected([this](int itemIdx)
            { BuildPartsTable(itemIdx); });

        //auto typesTable = std::make_shared<UITable>(numColumns);
        auto typesPanel = std::make_shared<UIPanel>(0, 0, 150, 0);
        typesPanel->AddControl(typesTable);
        menu->AddControl(typesPanel);

        int padding = 20;
        int numColumns = 1280 / (128 + padding);
        m_partsTable = std::make_shared<UITable>(numColumns);
        auto panel = std::make_shared<UIPanel>(0, 0, 0, 0);
        panel->AddControl(m_partsTable);
        menu->AddControl(panel);
        top->AddControl(menu);
        m_mainMenu = menu;

        return top;
    }

    void LegoUI::BuildPartsTable(int itemIdx)
    {
        int padding = 20;
        int numColumns = 1280 / (128 + padding);
        const std::string& t = BrickManager::Inst().TypeName(itemIdx);
        const std::vector<PartId>& partsForType = BrickManager::Inst().PartsForType(t);
        int numRows = partsForType.size();

        m_partsTable->SetItems(numRows, [numColumns, &partsForType](int start, int count, UITable::TableItem items[])
            {
                for (int r = 0; r < count; r++)
                {
                    const PartId& name = partsForType[r + start];
                    Brick* pBrick = BrickManager::Inst().GetBrick(name);
                    items[r].text = std::to_string(r + start) + ": " + name.Name();
                    items[r].image = pBrick->m_icon;
                }
            });

        m_partsTable->OnTooltipText([&partsForType](int itemidx, std::string& text)
            {
                const std::string& name = partsForType[itemidx].Name();
                text = BrickManager::Inst().PartDescription(name);
            });

        m_partsTable->OnItemSelected([this, partsForType](int itemIdx)
            { 
                m_partSelectedFn(partsForType[itemIdx]);
            });
    }

    void LegoUI::ActivateUI(const std::function<void()>& deactivateFn)
    {
        if (m_mainMenu)
            m_mainMenu->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;        
    }

    bool LegoUI::MouseDown(float x, float y, int buttonId)
    {
        bool ret = UIManager::MouseDown(x, y, buttonId);
        return m_isActive;
    }

    bool LegoUI::MouseDrag(float x, float y, int buttonId)
    {
        bool ret = UIManager::MouseDrag(x, y, buttonId);
        return m_isActive;
    }

    bool LegoUI::MouseUp(int buttonId)
    {
        bool ret = UIManager::MouseUp(buttonId);
        return m_isActive;
    }
}