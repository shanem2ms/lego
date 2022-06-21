#include "StdIncludes.h"
#include "GameController.h"
#include "PlayerView.h"
#include "Application.h"
#include <bx/readerwriter.h>
#include <bx/file.h>
#include <bimg/bimg.h>
#include "Hud.h"
#include "Mesh.h"
#include "Physics.h"
#include "World.h"
#include <bimg/decode.h>

namespace sam
{
    GameController::GameController() :
        m_padSize(0.25f),
        m_player(),
        m_world(nullptr)
    {
        m_thumbpad.m_active = false;
        m_thumbpad.m_color = Vec3f(1, 0, 1);
        m_thumbpad.m_hitbox = AABoxf(Vec3f(0, 0, 0), Vec3f(0.5f, 0.75f, 1));
        m_thumbpad.m_touch = TouchAction::Thumbpad;
        m_mousepad.m_active = false;
        m_mousepad.m_color = Vec3f(0.3f, 0.3f, 0.3f);
        m_mousepad.m_hitbox = AABoxf(Vec3f(0.5f, 0, 0), Vec3f(1.0f, 0.75f, 1));
        m_mousepad.m_touch = TouchAction::MousePad;


        Vec2f centerPos(0.8f, 0.80f);
        float spread = 0.08f;
        Vec2f offsets[4] = { Vec2f(-1,0), Vec2f(1, 0), Vec2f(0, -1), Vec2f(0, 1) };
        Vec3f colors[4] = { Vec3f(0.5f, 1, 0) , Vec3f(1, 0.2f, 0), Vec3f(1, 1, 0), Vec3f(0, 0.2f, 1) };
        for (int i = 0; i < 4; ++i)
        {
            m_buttons[i].m_color = colors[i];
            m_buttons[i].m_pos = centerPos + offsets[i] * spread;
            m_buttons[i].m_size = 0.14f;
            m_buttons[i].m_isPressed = false;
            m_buttons[i].m_isPressedPrev = false;
            m_buttons[i].m_touch = (TouchAction)((int)TouchAction::Button0 + i);
        }
    }

    void GameController::ConnectPlayer(const std::shared_ptr<Player>& player,
        World* world)
    {
        m_player = player;
        m_world = world;
    }

    void GameController::TouchDown(float x, float y, uint64_t touchId)
    {
        Point3f vpos(x / m_width, y / m_height, 0.5f);
        TouchAction action = TouchAction::None;
        for (Button& btn : m_buttons)
        {
            if (length(Vec2f(Vec2f(vpos) - btn.m_pos)) < btn.m_size / 2)
            {
                action = btn.m_touch;
                btn.m_isPressed = true;
                break;
            }
        }

        if (action == TouchAction::None && 
            isInVolume(m_thumbpad.m_hitbox, vpos))
        {
            m_thumbpad.m_pos = Vec2f(x, y);
            m_thumbpad.m_active = true;
            m_thumbpad.m_xaxis = 0;
            m_thumbpad.m_yaxis = 0;
            action = m_thumbpad.m_touch;
        }

        if (action == TouchAction::None &&
            isInVolume(m_mousepad.m_hitbox, vpos))
        {
            m_mousepad.m_pos = Vec2f(x, y);
            m_mousepad.m_active = true;
            m_mousepad.m_xpos = 0;
            m_mousepad.m_ypos = 0;
            m_mousepad.m_xprev = 0;
            m_mousepad.m_yprev = 0;
            action = m_mousepad.m_touch;
        }
        m_activeTouches.insert(std::make_pair(touchId,
            Touch{ x, y, x, y, action }));
    }

    void GameController::TouchMove(float x, float y, uint64_t touchId)
    {
        auto itTouch = m_activeTouches.find(touchId);
        if (itTouch != m_activeTouches.end())
        {
            Touch& touch = itTouch->second;
            if (touch.action == TouchAction::Thumbpad)
            {
                m_thumbpad.m_xaxis = (x - touch.startX) * 2 / (m_height * m_padSize);
                m_thumbpad.m_xaxis = std::max(-1.0f, std::min(1.0f, m_thumbpad.m_xaxis));
                m_thumbpad.m_yaxis = (y - touch.startY) * 2 / (m_height * m_padSize);
                m_thumbpad.m_yaxis = std::max(-1.0f, std::min(1.0f, m_thumbpad.m_yaxis));
            }
            else if (touch.action == TouchAction::MousePad)
            {
                m_mousepad.m_xpos = (x - touch.startX);
                m_mousepad.m_ypos = (y - touch.startY);
            }

            touch.prevX = x;
            touch.prevY = y;
        }
    }

    void GameController::TouchUp(float x, float y, uint64_t touchId)
    {
        auto itTouch = m_activeTouches.find(touchId);
        if (itTouch != m_activeTouches.end())
        {
            Touch& touch = itTouch->second;
            if (touch.action == TouchAction::Thumbpad)
            {
                m_thumbpad.m_active = false;
            }
            else if (touch.action >= TouchAction::Button0 &&
                touch.action <= TouchAction::Button9)
            {
                int btnIdx = (int)(touch.action) - (int)TouchAction::Button0;
                m_buttons[btnIdx].m_isPressed = false;
            }
            else if (touch.action == TouchAction::MousePad)
            {
                m_mousepad.m_active = false;
            }
            m_activeTouches.erase(itTouch);
        }
    }

    void GameController::Update(DrawContext& ctx)
    {
        float moveScale = 4.0f;
        if (m_thumbpad.m_active)
            m_player->MovePadXY(m_thumbpad.m_xaxis * moveScale, -m_thumbpad.m_yaxis * moveScale);
        else
            m_player->MovePadXY(0, 0);

        float lookScale = 0.005f;
        if (m_mousepad.m_active)
        {
            float vx = m_mousepad.m_xpos - m_mousepad.m_xprev;
            float vy = m_mousepad.m_ypos - m_mousepad.m_yprev;
            m_player->RawMove(vx * lookScale, -vy * lookScale);
            m_mousepad.m_xprev = m_mousepad.m_xpos;
            m_mousepad.m_yprev = m_mousepad.m_ypos;
        }

        float zMove = 0;
        for (Button& btn : m_buttons)
        {
            if (btn.m_isPressed)
            {
                if (!btn.m_isPressedPrev)
                {
                    if (btn.m_touch == TouchAction::Button0)
                        m_world->PlaceBrick(m_player.get());
                    else if (btn.m_touch == TouchAction::Button1)
                        m_world->DestroyBrick(m_player.get());
                    else if (btn.m_touch == TouchAction::Button2 && !m_player->FlyMode())
                    {
                        m_player->Jump();
                    }
                }

                if (m_player->FlyMode())
                {
                    if (btn.m_touch == TouchAction::Button2)
                        zMove = -moveScale;
                    else if (btn.m_touch == TouchAction::Button3)
                        zMove = moveScale;
                }
            }

            m_player->MovePadZ(zMove);
            btn.m_isPressedPrev = btn.m_isPressed;
        }
    }

    void GameController::Draw(DrawContext& ctx)
    {
        static bgfxh<bgfx::ProgramHandle> shader;
        static bgfxh<bgfx::UniformHandle> sUparams;

        if (!shader.isValid())
            shader = Engine::Inst().LoadShader("vs_gamecontroller.bin", "fs_gamecontroller.bin");
        if (!sUparams.isValid())
            sUparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);

        for (auto& btn : m_buttons)
        {
            Matrix44f m = ctx.m_mat;
            Quad::init();

            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;

            float posx = btn.m_pos[0] * 2 - 1;
            float posy = btn.m_pos[1] * -2 + 1;
            {
                // Set vertex and index buffer.
                bgfx::setVertexBuffer(0, Quad::vbh);
                bgfx::setIndexBuffer(Quad::ibh);
                // Set render states.l
                bgfx::setState(state);
                float size = m_padSize;
                m = makeTrans<Matrix44f>(Vec3f(posx, posy, 0.5f)) *
                    makeScale<Matrix44f>(Vec3f(m_aspect * btn.m_size, btn.m_size, 1));
                bgfx::setTransform(m.getData());
                Vec4f color(btn.m_color, btn.m_isPressed ? 0.75f : 0.25f);
                bgfx::setUniform(sUparams, &color, 1);
                bgfx::submit(DrawViewId::HUD, shader);
            }
        }

        if (m_thumbpad.m_active)
        {
            Matrix44f m = ctx.m_mat;
            Quad::init();

            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;

            float sx = (m_thumbpad.m_pos[0] / m_width) * 2 - 1;
            float sy = (m_thumbpad.m_pos[1] / m_height) * -2 + 1;
            {
                // Set vertex and index buffer.
                bgfx::setVertexBuffer(0, Quad::vbh);
                bgfx::setIndexBuffer(Quad::ibh);
                // Set render states.l
                bgfx::setState(state);
                float size = m_padSize;
                m = makeTrans<Matrix44f>(Vec3f(sx, sy, 0.5f)) *
                    makeScale<Matrix44f>(Vec3f(m_aspect * size, size, size));
                bgfx::setTransform(m.getData());
                Vec4f color(1, 1, 1, 0.20f);
                bgfx::setUniform(sUparams, &color, 1);
                bgfx::submit(DrawViewId::HUD, shader);
            }
            float px = sx + m_thumbpad.m_xaxis * m_padSize * 0.5f;
            float py = sy + m_thumbpad.m_yaxis * m_padSize / m_aspect * -0.5f;
            {
                // Set vertex and index buffer.
                bgfx::setVertexBuffer(0, Quad::vbh);
                bgfx::setIndexBuffer(Quad::ibh);
                // Set render states.l
                bgfx::setState(state);

                float size = m_padSize / 3.5f;
                m = makeTrans<Matrix44f>(Vec3f(px, py, 0.5f)) *
                    makeScale<Matrix44f>(Vec3f(m_aspect * size, size, size));
                bgfx::setTransform(m.getData());
                Vec4f color(m_thumbpad.m_color, 0.5f);
                bgfx::setUniform(sUparams, &color, 1);
                bgfx::submit(DrawViewId::HUD, shader);
            }
        }
    }

}
