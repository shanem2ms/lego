#pragma once
#include "Engine.h"

class CubeList;
namespace sam
{
    class Player;
    class World;
    class GameController : public IEngineDraw
    {
        enum class TouchAction
        {
            None = 0,
            Thumbpad = 10,
            Button0 = 20,
            Button1 = 21,
            Button2 = 22,
            Button3 = 23,
            Button9 = 29,
            MousePad = 30
        };

        struct Touch
        {
            float startX;
            float startY;
            float prevX;
            float prevY;
            TouchAction action;
        };

        struct ThumbPad
        {
            Vec2f m_pos;
            Vec3f m_color;
            bool m_active;
            float m_xaxis;
            float m_yaxis;
            AABoxf m_hitbox;
            TouchAction m_touch;
        };

        struct MousePad
        {
            Vec2f m_pos;
            Vec3f m_color;
            bool m_active;
            float m_xpos;
            float m_ypos;
            float m_xprev;
            float m_yprev;
            AABoxf m_hitbox;
            TouchAction m_touch;
        };

        struct Button
        {
            Vec2f m_pos;
            float m_size;
            Vec3f m_color;
            bool m_isPressed;
            bool m_isPressedPrev;
            TouchAction m_touch;
        };

        std::map<uint64_t, Touch> m_activeTouches;    
        int m_width;
        int m_height;
        float m_aspect;
        float m_padSize;
        ThumbPad m_thumbpad;
        MousePad m_mousepad;
        Button m_buttons[4];
        std::shared_ptr<Player> m_player;
        World *m_world;
    public:
        GameController();

        void ConnectPlayer(const std::shared_ptr<Player>& player,
            World* world);
        void SetSize(int width, int height) {
            m_width = width; m_height = height;
            m_aspect = (float)m_height / (float)m_width;
        }
        void Update(DrawContext& ctx);
        void TouchDown(float x, float y, uint64_t touchId);
        void TouchMove(float x, float y, uint64_t touchId);
        void TouchUp(float x, float y, uint64_t touchId);
        void Draw(DrawContext& dc) override;

    };
}