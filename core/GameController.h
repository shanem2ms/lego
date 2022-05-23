#pragma once
#include "Engine.h"

class CubeList;
class Player;
namespace sam
{
    class GameController : public IEngineDraw
    {
        enum class TouchAction
        {
            LeftPad,
            RightPad,
            None
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
            Vec3f m_color;
            bool m_active;
            float m_xaxis;
            float m_yaxis;
            AABoxf m_hitbox;
            TouchAction m_touch;
        };

        std::map<uint64_t, Touch> m_activeTouches;    
        int m_width;
        int m_height;
        float m_padSize;
        Pad m_pads[2];
        std::shared_ptr<Player> m_player;
    public:
        GameController();

        void ConnectPlayer(std::shared_ptr<Player> player);
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