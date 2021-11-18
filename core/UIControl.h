#pragma once

#include <functional>
#include "SceneItem.h"
namespace sam
{

    class Engine;

    class UIControl
    {
    protected:
        float m_x;
        float m_y;
        float m_width;
        float m_height;
        bool m_isInit;
        Vec4f m_background;
        Vec4f m_border;
        UIControl(float x, float y, float w, float h);    gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;

    public:
        virtual bool IsHit(float x, float y, int touchId);
        virtual bool TouchDown(float x, float y, int touchId) = 0;
        virtual bool TouchDrag(float x, float y, int touchId) = 0;
        virtual bool TouchUp(int touchId) = 0;
        void Update(const std::shared_ptr<SceneGroup>& engine, DrawContext& ctx);
        void SetBackgroundColor(const Vec4f& color);
        void SetBorderColor(const Vec4f& color);
        virtual void Initialize(const std::shared_ptr<SceneGroup>& group, DrawContext& ctx) = 0;
    };


    class UIManager
    {
        std::vector<std::shared_ptr<UIControl>> m_controls;
        std::shared_ptr<UIControl> m_capturedCtrl;
        std::shared_ptr<SceneGroup> m_uiGroup;
        gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;

    public:
        bool TouchDown(float x, float y, int touchId);
        bool TouchDrag(float x, float y, int touchId);
        bool TouchUp(int touchId);
        void AddControl(std::shared_ptr<UIControl> ctrl);
        void Update(Engine& engine, int w, int h, DrawContext& ctx);
    };

    class UIButton : public UIControl
    {
    public:
        typedef std::function<void(int)> PressedFunc;

        UIButton(const std::string& text, float x, float y, float w, float h);
        bool TouchDown(float x, float y, int touchId) override;
        bool TouchDrag(float x, float y, int touchId) override;
        bool TouchUp(int touchId) override;
        void Initialize(const std::shared_ptr<SceneGroup>& group, DrawContext& ctx) override;
        void OnPressed(const PressedFunc& pressed);

    private:
        std::string m_text;
        PressedFunc m_pressed;
        Vec4f m_pressedColor;
        std::shared_ptr<SceneRect> m_rect;
    };

}