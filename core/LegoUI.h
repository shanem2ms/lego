#pragma once

#include "UIControl.h"

namespace sam
{
    struct PartId;
    class LegoUI : public UIManager
    {
        std::shared_ptr<UIWindow> m_mainMenu;
        std::function<void()> m_deactivateFn;
        bool m_isActive;
        std::shared_ptr<UITable> m_partsTable;
        std::function<void(const PartId&)> m_partSelectedFn;
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
        void BuildPartsTable(int itemIdx);
        void OnPartSelected(const std::function<void(const PartId&)>& partSelectedFn)
        { m_partSelectedFn = partSelectedFn; }
    };
}