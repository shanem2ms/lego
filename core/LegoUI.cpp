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
        auto topWnd = std::make_shared<UIWindow>(0, 0, 0, 0, "top", true, false);
        m_topctrl = topWnd;
        topWnd->AddControl(m_mainMenu.Build(this, ctx, w, h));
        topWnd->AddControl(m_inventory.Build(this, ctx, w, h));
        topWnd->AddControl(BuildHotbar(ctx, w, h));
        return m_topctrl;
    }
    std::shared_ptr<UIControl> LegoUI::Inventory::Build(LegoUI* parent, DrawContext& ctx, int w, int h)
    {
        const int btnSize = 150;
        const int btnSpace = 10;        
        
        std::shared_ptr<UIWindow> inventoryWnd = std::make_shared<UIWindow>(100, 100, -200, -200, "bricks", false, true);
        inventoryWnd->OnOpenChanged([this, parent](bool isopen) {
            if (!isopen) Deactivate(); });
        inventoryWnd->SetLayout(UILayout::Horizontal);


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
        inventoryWnd->AddControl(typesPanel);

        int padding = 20;
        int numColumns = 1280 / (128 + padding);
        m_partsTable = std::make_shared<UITable>(numColumns);
        auto partsPanel = std::make_shared<UIPanel>(0, 0, -300, 0);
        partsPanel->AddControl(m_partsTable);
        inventoryWnd->AddControl(partsPanel);

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
        inventoryWnd->AddControl(colorsPanel);
        m_root = inventoryWnd;
        m_root->Close();
        return inventoryWnd;
    }

    std::shared_ptr<UIControl> LegoUI::BuildHotbar(DrawContext& ctx, int w, int h)
    {
        m_hotbar = std::make_shared<UIWindow>(250, -300, 1620, 230, "hotbar", false, false);
        m_hotbar->SetLayout(UILayout::Horizontal);
        auto hotbarTable = std::make_shared<UITable>(8);
        const SlotPart* pSlots = ctx.m_pPlayer->GetSlots();
        auto player = ctx.m_pPlayer;
        hotbarTable->SetItems(8, [pSlots, player](int start, int count, UITable::TableItem items[])
            {
                int currentSlockIdx = player->GetCurrentSlotIdx();
                for (int r = 0; r < count; r++)
                {
                    items[r].image = BrickManager::Inst().GetBrickThumbnail(pSlots[r].id);
                    items[r].imgTint =
                        (uint32_t&)BrickManager::Inst().GetColorFromCode(pSlots[r].colorCode).fill;
                    items[r].colorRect = ((start + r) == currentSlockIdx ? 0xFF808000 : 0xFF000000);
                }
            });
        hotbarTable->OnItemSelected([player](int idx)
            { player->SetCurrentSlotIdx(idx);
            });

        World* pWorld = ctx.m_pWorld;
        auto hotbarPanel = std::make_shared<UIPanel>(0, 0, -100, 0);
        hotbarPanel->AddControl(hotbarTable);
        m_hotbar->AddControl(hotbarPanel);
        m_hotbar->AddControl(std::make_shared<UIStateBtn>(845, 20, 85, 85, ICON_FA_ALIGN_JUSTIFY,
            [pWorld](bool isBtnDown)
            {
                Application::Inst().OpenInventory();
            }));

        m_hotbar->Show();
        return m_hotbar;
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
                    items[r].text = std::to_string(r + start) + ": " + name.Name();
                    items[r].image = BrickManager::Inst().GetBrickThumbnail(name);
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

    void LegoUI::Inventory::Open(const std::function<void()>& deactivateFn)
    {
        if (m_root)
            m_root->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;
    }

    void LegoUI::Inventory::Close()
    {
        m_root->Close();
        m_isActive = false;
    }

    void LegoUI::CloseAll()
    {
        if (m_inventory.m_isActive)
            m_inventory.Close();
        if (m_mainMenu.m_isActive)
            m_mainMenu.Close();
    }

    bool LegoUI::MouseDown(float x, float y, int buttonId)
    {
        bool ret = UIManager::MouseDown(x, y, buttonId);
        return IsActive();
    }

    bool LegoUI::MouseDrag(float x, float y, int buttonId)
    {
        bool ret = UIManager::MouseDrag(x, y, buttonId);
        return IsActive();
    }

    bool LegoUI::MouseUp(int buttonId)
    {
        bool ret = UIManager::MouseUp(buttonId);
        return IsActive();
    }

    bool LegoUI::WheelScroll(float delta)
    {
        bool ret = UIManager::WheelScroll(delta);
        return IsActive();
    }


    std::shared_ptr<UIControl> LegoUI::MainMenu::Build(LegoUI* parent, DrawContext& ctx, int w, int h)
    {
        std::shared_ptr<UIWindow> menu = std::make_shared<UIWindow>(650, 250, 1280, 700, "mainmenu", false, true);
        menu->OnOpenChanged([this, parent](bool isopen) {
            if (!isopen) Deactivate(); });
        menu->SetLayout(UILayout::Horizontal);
        auto panel = std::make_shared<UIPanel>(0, 0, -30, -30);
        menu->AddControl(panel);

        std::shared_ptr<UIFileDialog> importFileBrowser;
        importFileBrowser = std::make_shared<UIFileDialog>(0, 0, -30, -30, Application::Inst().Documents() + "/Import/.",
            [menu, panel](bool isOk, const std::string& filepath) 
            {
                if (isOk)
                {
                    Application::Inst().UIImportMbx(filepath);
                }
                panel->SetVisible(true);                
            });
        importFileBrowser->SetVisible(false);
        menu->AddControl(importFileBrowser);

        panel->AddControl(std::make_shared<UIStateBtn>(20, 100, 165, 85, "Import",
            [menu, panel, importFileBrowser](bool isBtnDown)
            {
                if (isBtnDown) {
                    panel->SetVisible(false);
                    importFileBrowser->SetVisible(true);
                }
            }));

        

        panel->AddControl(std::make_shared<UIStateBtn>(20, -105, 165, 85, "Quit",
            [](bool isBtnDown)
            {
                Application::Inst().UIQuit();
            }));
        
        panel->AddControl(std::make_shared<UIStateBtn>(-120, -105, 165, 85, "Close",
            [menu](bool isBtnDown)
            {
                menu->Close();
            }));

        m_root = menu;
        return menu;
    }

    void LegoUI::MainMenu::Open(const std::function<void()>& deactivateFn)
    {
        if (m_root)
            m_root->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;
    }

    void LegoUI::MainMenu::Close()
    {
        m_root->Close();
        m_isActive = false;
    }


    std::shared_ptr<UIControl> LegoUI::ImportWindow::Build(LegoUI* parent, DrawContext& ctx, int w, int h)
    {
        std::shared_ptr<UIWindow> menu = std::make_shared<UIWindow>(650, 250, 1280, 700, "ImportWindow", false, true);
        menu->OnOpenChanged([this, parent](bool isopen) {
            if (!isopen) Deactivate(); });
        menu->SetLayout(UILayout::Horizontal);


        menu->AddControl(std::make_shared<UIStateBtn>(20, -105, 165, 85, "Import",
            [](bool isBtnDown)
            {
            }));

        menu->AddControl(std::make_shared<UIStateBtn>(220, -105, 165, 85, "Cancel",
            [](bool isBtnDown)
            {
            }));
        m_root = menu;
        return menu;
    }

    void LegoUI::ImportWindow::Open(const std::function<void()>& deactivateFn)
    {
        if (m_root)
            m_root->Show();
        m_isActive = true;
        m_deactivateFn = deactivateFn;
    }

    void LegoUI::ImportWindow::Close()
    {
        m_root->Close();
        m_isActive = false;
    }

}