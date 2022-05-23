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
        m_pads[0].m_active = false;
        m_pads[0].m_color = Vec3f(1, 0, 1);
        m_pads[0].m_hitbox = AABoxf(Vec3f(0, 0, 0), Vec3f(0.5f, 0.75f, 1));
        m_pads[0].m_touch = TouchAction::Pad0;
        m_pads[1].m_active = false;
        m_pads[1].m_color = Vec3f(0, 1, 0);
        m_pads[1].m_hitbox = AABoxf(Vec3f(0.5f, 0, 0), Vec3f(1.0f, 0.55f, 1));
        m_pads[1].m_touch = TouchAction::Pad1;

        Vec2f centerPos(0.8f, 0.70f);
        float spread = 0.12f;
        Vec2f offsets[4] = { Vec2f(-1,0), Vec2f(1, 0), Vec2f(0, -1), Vec2f(0, 1) };
        Vec3f colors[4] = { Vec3f(0.5f, 1, 0) , Vec3f(1, 0.2f, 0), Vec3f(1, 1, 0), Vec3f(0, 0.2f, 1) };
        for (int i = 0; i < 4; ++i)
        {
            m_buttons[i].m_color = colors[i];
            m_buttons[i].m_pos = centerPos + offsets[i] * spread;
            m_buttons[i].m_size = 0.15f;
            m_buttons[i].m_isPressed = false;
            m_buttons[i].m_isPressedPrev = false;
            m_buttons[i].m_touch = (TouchAction)((int)TouchAction::Button0 + i);
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

        for (auto& pad: m_pads)
        {
            if (pad.m_active)
            {
                Matrix44f m = ctx.m_mat;
                Quad::init();

                uint64_t state = 0
                    | BGFX_STATE_WRITE_RGB
                    | BGFX_STATE_WRITE_A
                    | BGFX_STATE_MSAA
                    | BGFX_STATE_BLEND_ALPHA;

                float sx = (pad.m_pos[0] / m_width) * 2 - 1;
                float sy = (pad.m_pos[1] / m_height) * -2 + 1;
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
                float px = sx + pad.m_xaxis * m_padSize * 0.5f;
                float py = sy + pad.m_yaxis * m_padSize / m_aspect * -0.5f;
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
                    Vec4f color(pad.m_color, 0.5f);
                    bgfx::setUniform(sUparams, &color, 1);
                    bgfx::submit(DrawViewId::HUD, shader);
                }
            }
        }
    }

    void GameController::Update(DrawContext& ctx)
    {
        float lookScale = 0.05f;
        if (m_pads[0].m_active)
            m_player->RawMove(m_pads[0].m_xaxis * lookScale, -m_pads[0].m_yaxis * lookScale);
        else
            m_player->RawMove(0, 0);

        float moveScale = 2.0f;
        if (m_pads[1].m_active)
            m_player->MovePadXY(m_pads[1].m_xaxis * moveScale, -m_pads[1].m_yaxis * moveScale);
        else 
            m_player->MovePadXY(0, 0);

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

                    }
                }

                if (m_player->FlyMode())
                {
                    if (btn.m_touch == TouchAction::Button2)
                        zMove = -1;
                    else if (btn.m_touch == TouchAction::Button3)
                        zMove = 1;
                }
            }

            m_player->MovePadZ(zMove);
            btn.m_isPressedPrev = btn.m_isPressed;
        }
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

        if (action == TouchAction::None)
        {
            for (Pad& pad : m_pads)
            {
                if (isInVolume(pad.m_hitbox, vpos))
                {
                    pad.m_pos = Vec2f(x, y);
                    pad.m_active = true;
                    pad.m_xaxis = 0;
                    pad.m_yaxis = 0;
                    action = pad.m_touch;
                    break;
                }
            }
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
            if (touch.action >= TouchAction::Pad0 &&
                touch.action <= TouchAction::Pad9)
            {
                int padIdx = (int)(touch.action) - (int)TouchAction::Pad0;
                m_pads[padIdx].m_xaxis = (x - touch.startX) * 2 / (m_height * m_padSize);
                m_pads[padIdx].m_xaxis = std::max(-1.0f, std::min(1.0f, m_pads[padIdx].m_xaxis));
                m_pads[padIdx].m_yaxis = (y - touch.startY) * 2 / (m_height * m_padSize);
                m_pads[padIdx].m_yaxis = std::max(-1.0f, std::min(1.0f, m_pads[padIdx].m_yaxis));
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
            if (touch.action >= TouchAction::Pad0 &&
                touch.action <= TouchAction::Pad9)
            {
                int padIdx = (int)(touch.action) - (int)TouchAction::Pad0;
                m_pads[padIdx].m_active = false;
            }
            else if (touch.action >= TouchAction::Button0 &&
                touch.action <= TouchAction::Button9)
            {
                int btnIdx = (int)(touch.action) - (int)TouchAction::Button0;
                m_buttons[btnIdx].m_isPressed = false;
            }
            m_activeTouches.erase(itTouch);
        }
    }

    void GameController::ConnectPlayer(const std::shared_ptr<Player> &player,
        World* world)
    {
        m_player = player;
        m_world = world;
    }

}