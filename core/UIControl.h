#pragma once

#include <functional>
#include "SceneItem.h"
namespace sam
{

    class Engine;
    
    enum UILayout
    {
        Horizontal,
        Vertical
    };

    struct UIContext
    {
        UILayout layout; 
        int width;
        int height;
        float scaleW;
        float scaleH;
    };

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
        UIControl(float x, float y, float w, float h);    
        gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;

    public:
        virtual UIControl *IsHit(float x, float y, int buttonId);
        void SetBackgroundColor(const Vec4f& color);
        void SetBorderColor(const Vec4f& color);
        virtual void DrawUI(UIContext &ctx) = 0;
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
        void DrawUI(UIContext& ctx) override;
    };

    class UIGroup : public UIControl
    {
    protected:
        std::vector<std::shared_ptr<UIControl>> m_controls;
        UILayout m_layout;
        UIGroup(float x, float y, float w, float h);
        UIControl *IsHit(float x, float y, int buttonId) override;
        void DrawUI(UIContext& ctx) override;
    public:
        void SetLayout(UILayout layout) { m_layout = layout; }
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
        UIControl* IsHit(float x, float y, int buttonId) override;
        void DrawUI(UIContext& ctx) override;
        void Show() { m_isopen = true; }
        void Close() { 
            if (!m_isopen)
                return;
            if (m_onOpenChangedFn != nullptr)
                m_onOpenChangedFn(false);
            m_isopen = false; }
    };

    class UIPanel : public UIGroup
    {
        std::string m_name;
    public:
        UIPanel(float x = 0, float y = 0, float w = 0, float h = 0);
        void DrawUI(UIContext& ctx) override;
    };

    class UITable : public UIControl
    {
    public:
        struct TableItem
        {
            std::string text;
            bgfx::TextureHandle image;
            uint32_t imgTint;
            uint32_t colorRect;
            TableItem() : image({ bgfx::kInvalidHandle }),
                colorRect(0),
                imgTint(0xFFFFFFFF)
                {}
        };
    
    protected:
        int m_itemcount;
        int m_columns;  
        int m_selectedIdx;
        std::function<void(int, int, TableItem items[])> m_drawItemsFn;        
        std::function<void(int)> m_itemSelectedFn;
        std::function<void(int, std::string&)> m_tooltipFn;
    public:
        UITable(int columns);
        void SetItems(int count, const std::function<void(int, int, TableItem items[])> &drawItemsFn)
        { m_drawItemsFn = drawItemsFn; m_itemcount = count; }
        void DrawUI(UIContext& ctx) override;
        void OnTooltipText(const std::function<void(int, std::string&)> & tooltipFn)
        { m_tooltipFn = tooltipFn; }
        void OnItemSelected(const std::function<void(int)> &itemSelectedFn)
        { m_itemSelectedFn = itemSelectedFn; }
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
        float m_wheelDelta;
    public:
        UIManager() : m_wheelDelta(0){}
        virtual bool MouseDown(float x, float y, int buttonId);
        virtual bool MouseDrag(float x, float y, int buttonId);
        virtual bool MouseUp(int buttonId);
        virtual bool WheelScroll(float delta);
        virtual std::shared_ptr<UIControl> Build(DrawContext& ctx, int w, int h) = 0;
        void Update(Engine& engine, int w, int h, DrawContext& ctx);
        void KeyDown(int keyId);
        void KeyUp(int keyId);
    };
   
}