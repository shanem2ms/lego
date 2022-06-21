#pragma once

#include "UIControl.h"

namespace sam
{
    struct PartId;
    class LegoUI : public UIManager
    {
    public:
        class Inventory
        {
        public:
            std::shared_ptr<UIWindow> m_root;
            std::shared_ptr<UITable> m_partsTable;
            std::function<void(const PartId&)> m_partSelectedFn;
            std::function<void(int)> m_colortSelectedFn;

            Inventory() :
                m_isActive(false) {}

            std::shared_ptr<UIControl> Build(LegoUI* parent, DrawContext& ctx, int w, int h);
            void BuildPartsTable(int itemIdx);

            void Open(const std::function<void()>& deactivateFn);
            void Close();

            void OnPartSelected(const std::function<void(const PartId&)>& partSelectedFn)
            {
                m_partSelectedFn = partSelectedFn;
            }
            void OnColorSelected(const std::function<void(int)>& colorSelectedFn)
            {
                m_colortSelectedFn = colorSelectedFn;
            }

            void Deactivate() {
                m_isActive = false;
                if (m_deactivateFn != nullptr)
                {
                    m_deactivateFn();
                }
            }
            std::function<void()> m_deactivateFn;
            bool m_isActive;
        };

        Inventory m_inventory;
        std::shared_ptr<UIWindow> m_hotbar;
        std::shared_ptr<UIControl> BuildHotbar(DrawContext& ctx, int w, int h);

        class MainMenu
        {
        public:
            std::shared_ptr<UIWindow> m_root;


            MainMenu() :
                m_isActive(false) {}

            std::shared_ptr<UIControl> Build(LegoUI* parent, DrawContext& ctx, int w, int h);

            void Open(const std::function<void()>& deactivateFn);
            void Close();

            void Deactivate() {
                m_isActive = false;
                if (m_deactivateFn != nullptr)
                {
                    m_deactivateFn();
                }
            }
            std::function<void()> m_deactivateFn;
            bool m_isActive;
        };

        class ImportWindow
        {
        public:
            std::shared_ptr<UIWindow> m_root;


            ImportWindow() :
                m_isActive(false) {}

            std::shared_ptr<UIControl> Build(LegoUI* parent, DrawContext& ctx, int w, int h);

            void Open(const std::function<void()>& deactivateFn);
            void Close();

            void Deactivate() {
                m_isActive = false;
                if (m_deactivateFn != nullptr)
                {
                    m_deactivateFn();
                }
            }
            std::function<void()> m_deactivateFn;
            bool m_isActive;
        };

        MainMenu m_mainMenu;
        LegoUI() {}
        std::shared_ptr<UIControl> Build(DrawContext& ctx, int w, int h) override;
        
        Inventory& Inventory() { return m_inventory; }
        MainMenu& MainMenu() { return m_mainMenu; }
        bool IsActive() const { return 
            m_inventory.m_isActive ||
            m_mainMenu.m_isActive; }
        bool MouseDown(float x, float y, int buttonId) override;
        bool MouseDrag(float x, float y, int buttonId) override;
        bool MouseUp(int buttonId) override;
        bool WheelScroll(float delta) override;

        void CloseAll();
    };
}