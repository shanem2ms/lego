#pragma once

#include "UIControl.h"

namespace sam
{
    struct PartId;
    class LegoUI : public UIManager
    {
        std::function<void()> m_deactivateFn;
        bool m_isActive;
        class Inventory
        {
        public:
            std::shared_ptr<UIWindow> m_root;
            std::shared_ptr<UITable> m_partsTable;
            std::function<void(const PartId&)> m_partSelectedFn;
            std::function<void(int)> m_colortSelectedFn;
            std::shared_ptr<UIWindow> m_hotbar;

            std::shared_ptr<UIControl> Build(LegoUI* parent, DrawContext& ctx, int w, int h);
            void BuildPartsTable(int itemIdx);
        };

        Inventory m_inventory;
    public:
        LegoUI() :
            m_isActive(false) {}
        std::shared_ptr<UIControl> Build(DrawContext& ctx, int w, int h) override;
        
        void Deactivate() {
            m_isActive = false;
            if (m_deactivateFn != nullptr)
            {
                m_deactivateFn();
            }
        }

        void OpenInventory(const std::function<void()>& deactivateFn);
        void CloseInventory();
        bool IsActive() const { return m_isActive; }
        bool MouseDown(float x, float y, int buttonId) override;
        bool MouseDrag(float x, float y, int buttonId) override;
        bool MouseUp(int buttonId) override;
        bool WheelScroll(float delta) override;
        
        void OnPartSelected(const std::function<void(const PartId&)>& partSelectedFn)
        { m_inventory.m_partSelectedFn = partSelectedFn; }
        void OnColorSelected(const std::function<void(int)>& colorSelectedFn)
        { m_inventory.m_colortSelectedFn = colorSelectedFn; }
    };
}