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
#include "PlayerView.h"

namespace sam
{
    std::shared_ptr<UIControl> LegoUI::Build(DrawContext& ctx, int w, int h)
    {
        m_topctrl = m_inventory.Build(this, ctx, w, h);
        return m_topctrl;
    }
    std::shared_ptr<UIControl> LegoUI::Inventory::Build(LegoUI*parent, DrawContext &ctx, int w, int h)
    {
        const int btnSize = 150;
        const int btnSpace = 10;

        World* pWorld = ctx.m_pWorld;

        std::shared_ptr<UIWindow> top = std::make_shared<UIWindow>(0, 0, 0, 0, "top", true);
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
        menu->OnOpenChanged([this, parent](bool isopen) {
            if (!isopen) parent->Deactivate(); });
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

        typesTable->OnItemSelected([this, parent](int itemIdx)
            { BuildPartsTable(itemIdx); });

        auto typesPanel = std::make_shared<UIPanel>(0, 0, 150, 0);
        typesPanel->AddControl(typesTable);
        menu->AddControl(typesPanel);

        int padding = 20;
        int numColumns = 1280 / (128 + padding);
        m_partsTable = std::make_shared<UITable>(numColumns);
        auto partsPanel = std::make_shared<UIPanel>(0, 0, -150, 0);
        partsPanel->AddControl(m_partsTable);
        menu->AddControl(partsPanel);

        auto colorsTable = std::make_shared<UITable>(2);
        int numcolors = BrickManager::Inst().NumColors();
        colorsTable->SetItems(numcolors, [](int start, int count, UITable::TableItem items[])
            {
                for (int r = 0; r < count; r++)
                {
                    items[r].colorRect = (uint32_t&)BrickManager::Inst().GetColorFromIdx(r + start).fill;
                    items[r].text = std::to_string(BrickManager::Inst().GetColorFromIdx(r + start).atlasidx);
                }
            });
        colorsTable->OnItemSelected([this](int idx)
            { m_colortSelectedFn(idx); });

        auto colorsPanel = std::make_shared<UIPanel>(0, 0, 150, 0);
        colorsPanel->AddControl(colorsTable);
        menu->AddControl(colorsPanel);

        top->AddControl(menu);
        m_root = menu;

        m_hotbar = std::make_shared<UIWindow>(650, -200, 920, 130, "hotbar", false);
        m_hotbar->SetLayout(UILayout::Horizontal);
        auto hotbarTable = std::make_shared<UITable>(8);
        const SlotPart *pSlots = ctx.m_pPlayer->GetSlots();        
        auto player = ctx.m_pPlayer;
        hotbarTable->SetItems(8, [pSlots, player](int start, int count, UITable::TableItem items[])
            {
                int currentSlockIdx = player->GetCurrentSlotIdx();
                for (int r = 0; r < count; r++)
                {
                    Brick* pBrick = BrickManager::Inst().GetBrick(pSlots[r].id);
                    items[r].image = pBrick->m_icon;
                    items[r].imgTint =
                        (uint32_t&)BrickManager::Inst().GetColorFromCode(pSlots[r].colorCode).fill;
                    items[r].colorRect = ((start + r) == currentSlockIdx ? 0xFF808000 : 0xFF000000);
                }
            });
        hotbarTable->OnItemSelected([player](int idx)
            { player->SetCurrentSlotIdx(idx); 
            });

                   
        auto hotbarPanel = std::make_shared<UIPanel>(0, 0, -100, 0);
        hotbarPanel->AddControl(hotbarTable);
        m_hotbar->AddControl(hotbarPanel);
        m_hotbar->AddControl(std::make_shared<UIStateBtn>(825, 20, 85, 85, ICON_FA_ALIGN_JUSTIFY,
            [pWorld](bool isBtnDown)
            {
                char key = 16;
                if (isBtnDown) pWorld->KeyDown(key);
                else pWorld->KeyUp(key);
            }));

        m_hotbar->Show();
        top->AddControl(m_hotbar);
        
        return top;
    }

    void LegoUI::Inventory::BuildPartsTable(int itemIdx)
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

    void LegoUI::OpenInventory(const std::function<void()>& deactivateFn)
    {
        if (m_inventory.m_root)
            m_inventory.m_root->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;        
    }

    void LegoUI::CloseInventory()
    {
        m_inventory.m_root->Close();
        m_isActive = false;
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
    
    bool LegoUI::WheelScroll(float delta)
    {
        bool ret = UIManager::WheelScroll(delta);
        return m_isActive;
    }
}