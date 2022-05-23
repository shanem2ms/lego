#pragma once
#include "Engine.h"

class CubeList;
namespace sam
{
    class GameController : public IEngineDraw
    {
        enum class TouchAction
        {
            LeftPad,
            RightPad
        };

        struct Touch
        {
            float startX;
            float startY;
            float prevX;
            float prevY;
            TouchAction action;
        };

        struct Pad
        {
            Vec2f m_pos;
            bool m_active;
            float m_xaxis;
            float m_yaxis;
        };

        std::map<uint64_t, Touch> m_activeTouches;    
        int m_width;
        int m_height;
        float m_padSize;
        Pad m_pads[2];
    public:
        GameController();
        void SetSize(int width, int height) {
            m_width = width; m_height = height;
        }
        void Update(DrawContext& ctx);
        void TouchDown(float x, float y, uint64_t touchId);
        void TouchMove(float x, float y, uint64_t touchId);
        void TouchUp(float x, float y, uint64_t touchId);
        void Draw(DrawContext& dc) override;
    };
}