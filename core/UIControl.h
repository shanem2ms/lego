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
        virtual UIControl *IsHit(float x, float y, int touchId);
        void SetBackgroundColor(const Vec4f& color);
        void SetBorderColor(const Vec4f& color);
        virtual void DrawUI() = 0;
    };


    class UIStateBtn : public UIControl
    {
        std::string m_text;
        bool m_isDown;
        std::function<void(bool)> m_stateChanged;
    public:
        bool IsDown() const
        { return m_isDown; }
        UIStateBtn(float x, float y, float w, float h, const std::string& text,
            std::function<void (bool)> stateChanged);
        void DrawUI() override;
    };

    class UIGroup : public UIControl
    {
    protected:
        std::vector<std::shared_ptr<UIControl>> m_controls;
        UIGroup(float x, float y, float w, float h);
        UIControl *IsHit(float x, float y, int touchId) override;

    public:
        void AddControl(std::shared_ptr<UIControl> ctrl);
    };

    class UIWindow : public UIGroup
    {
        std::string m_name;
        bool m_isopen;
        bool m_isinvisible;
        std::function<void(bool)> m_onOpenChangedFn;
    public:
        void OnOpenChanged(const std::function<void(bool)> &fn)
        { m_onOpenChangedFn = fn; }
        UIWindow(float x, float y, float w, float h, const std::string& name,
            bool invisible);
        UIControl* IsHit(float x, float y, int touchId) override;
        void DrawUI() override;
    };

    class UIManager
    {
        std::shared_ptr<UIControl> m_topctrl;
        UIControl *m_capturedCtrl;
        gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;

    public:
        bool TouchDown(float x, float y, int touchId);
        bool TouchDrag(float x, float y, int touchId);
        bool TouchUp(int touchId);
        void Update(Engine& engine, int w, int h, DrawContext& ctx);
    };
   
}