#pragma once
#include <map>
#include <set>
#include "OctTile.h"
#include "OctTileSelection.h"
#include "Level.h"
#include "PartDefs.h"
#include "ConnectionLogic.h"

class SimplexNoise;

namespace sam
{

    struct DrawContext;
    class Engine;
    class Touch;
    class LegoBrick;
    class Physics;
    class Player;
    class ENetClient;

    class World
    {
    private:

        std::unique_ptr<ILevel> m_level;
        OctTileSelection m_octTileSelection;
        ConnectionLogic m_connectionLogic;

        int m_width;
        int m_height;
        int m_debugDraw;
        bool m_disableCollisionCheck;

        std::shared_ptr<SceneGroup> m_octTiles;
        int m_currentTool;
        bgfx::ProgramHandle m_shader;        
        std::shared_ptr<SceneItem> m_frustum;
        std::shared_ptr<LegoBrick> m_pPickedBrick;
        std::shared_ptr<Player> m_player;
        std::shared_ptr<Physics> m_physics;
        std::function<void()> m_showInventoryFn;
        
    public:

        const std::shared_ptr<Player> &GetPlayer()
        { return m_player; }

        void Layout(int w, int h);
        World();
        ~World();
        ILevel *Level() { return m_level.get(); }
        void Update(Engine& engine, DrawContext& ctx);
        void KeyDown(int k);
        void KeyUp(int k);
        void Open(ENetClient* cli);

        void PlaceBrick(Player *);
        void DestroyBrick(Player*);
        void UseBrick(Player*);
        void ImportMbx(const std::string& path);
        void OnShowInventory(const std::function<void()> &fn)
        { m_showInventoryFn = fn; }
    };

}
