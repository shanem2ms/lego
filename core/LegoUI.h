#pragma once

#include "UIControl.h"

namespace sam
{

    class LegoUI : public UIManager
    {
        std::shared_ptr<UIWindow> m_mainMenu;
        std::function<void()> m_deactivateFn;
        bool m_isActive;
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
        void ActivateUI(const std::function<void()>& deactivateFn);
        bool IsActive() const { return m_isActive; }
        bool TouchDown(float x, float y, int touchId) override;
        bool TouchDrag(float x, float y, int touchId) override;
        bool TouchUp(int touchId) override;
    };
}