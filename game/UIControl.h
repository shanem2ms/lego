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
        bool m_isVisible;
        UIControl(float x, float y, float w, float h);    
        gmtl::Vec2f m_touchDown;
        gmtl::Vec2f m_touchPos;
        int m_buttonDown;

        void GetCoords(const UIContext& ctx, float& x, float& y, float& w, float& h);

    public:
        virtual UIControl *IsHit(float x, float y, int buttonId);
        void SetBackgroundColor(const Vec4f& color);
        void SetBorderColor(const Vec4f& color);
        virtual void DrawUI(UIContext &ctx) = 0;
        void SetVisible(bool v) { m_isVisible = v; }
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
        void RemoveControl(const UIControl *ctrl);
    };

    class UIWindow : public UIGroup
    {
    public:
        enum Flags
        {
            None = 0,
            Inivisible = 1,
            TitleBar = 2
        };
    protected:
        Flags m_flags;
        std::string m_name;
        std::function<void(bool)> m_onOpenChangedFn;
        bool m_initialized;

    public:
        void OnOpenChanged(const std::function<void(bool)> &fn)
        { m_onOpenChangedFn = fn; }
        UIWindow(float x, float y, float w, float h, const std::string& name,
            Flags);
        UIControl* IsHit(float x, float y, int buttonId) override;
        void DrawUI(UIContext& ctx) override;
        void Show() { m_isVisible = true; }
        void Close() { 
            if (!m_isVisible)
                return;
            if (m_onOpenChangedFn != nullptr)
                m_onOpenChangedFn(false);
            m_isVisible = false; }
    };

    class UIPanel : public UIGroup
    {
        std::string m_name;
        bool m_break;
    public:
        UIPanel(float x = 0, float y = 0, float w = 0, float h = 0);
        void DrawUI(UIContext& ctx) override;
        void LineBreak(bool b)
        { m_break = b; }
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
        bool m_layoutIsVertical;
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

    class UIFileDialog : public UIControl
    {
        bool m_isOpen;
        std::string m_dir;
        std::function<void(bool, const std::string& file)> m_resultFunc;
    public:
        UIFileDialog(float x, float y, float w, float h,
            const std::string &directory, std::function<void(bool, const std::string &file)> resultFunc);
        void DrawUI(UIContext& ctx) override;
        UIControl* IsHit(float x, float y, int buttonId) override;
    };

    class UITextEditor : public UIControl
    {
        bool m_isOpen;
        std::string m_dir;
        std::function<void(bool, const std::string& file)> m_resultFunc;
    public:
        UITextEditor(float x, float y, float w, float h,
            const std::string& directory, std::function<void(bool, const std::string& file)> resultFunc);
        void DrawUI(UIContext& ctx) override;
        UIControl* IsHit(float x, float y, int buttonId) override;
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