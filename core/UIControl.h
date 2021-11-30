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
        void Show() { m_isopen = true; }
    };

    class UIManager
    {
    protected:
        std::shared_ptr<UIControl> m_topctrl;
        UIControl *m_capturedCtrl;
        gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;
        int m_width;
        int m_height;
    public:
        UIManager() {}
        virtual bool TouchDown(float x, float y, int touchId);
        virtual bool TouchDrag(float x, float y, int touchId);
        virtual bool TouchUp(int touchId);
        virtual std::shared_ptr<UIControl> Build(DrawContext& ctx, int w, int h) = 0;
        void Update(Engine& engine, int w, int h, DrawContext& ctx);
        void KeyDown(int keyId);
        void KeyUp(int keyId);
    };
   
}