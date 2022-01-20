#pragma once
#include <map>
#include <set>
#include "OctTile.h"
#include "OctTileSelection.h"
#include "Level.h"
#include "PartDefs.h"

class SimplexNoise;

namespace sam
{

    struct DrawContext;
    class Engine;
    class Touch;
    class LegoBrick;
    class Physics;
    class Player;

    class World
    {
    private:

        OctTileSelection m_octTileSelection;

        int m_width;
        int m_height;
        gmtl::Point3f m_camVel;
        float m_tiltVel;
        bool m_flymode;
        bool m_inspectmode;
        int m_debugDraw;
        bool m_disableCollisionCheck;

        float m_gravityVel;

        std::shared_ptr<SceneGroup> m_octTiles;
        int m_currentTool;
        bgfx::ProgramHandle m_shader;        
        std::shared_ptr<SceneItem> m_frustum;
        Level m_level;
        std::shared_ptr<LegoBrick> m_pPickedBrick;
        std::shared_ptr<Physics> m_physics;
        std::shared_ptr<Player> m_player;
    public:

        const std::shared_ptr<Player> &GetPlayer()
        { return m_player; }

        void Layout(int w, int h);
        World();
        ~World();
        Level& Level() { return m_level; }
        void Update(Engine& engine, DrawContext& ctx);
        void RawMove(float dx, float dy);
        void MouseDown(float x, float y, int buttonId);
        void MouseDrag(float x, float y, int buttonId);
        void MouseUp(int buttonId);
        void KeyDown(int k);
        void KeyUp(int k);
        void WheelScroll(float delta);
        void Open(const std::string &path);
    };

}
