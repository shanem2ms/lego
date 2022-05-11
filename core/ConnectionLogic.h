#pragma once
#include <memory>
#include "Engine.h"

class CubeList;
namespace sam
{
    class LegoBrick;
    class Player;
    class OctTileSelection;
    class PartInst;

    class ConnectionLogic : public IEngineDraw
    {
        std::shared_ptr<CubeList> m_connnectorsCL;
    public:
        void PlaceBrick(std::shared_ptr<Player> player, std::shared_ptr<LegoBrick> pickedBrick,
            OctTileSelection &octTileSelection, std::shared_ptr<Physics> physics, bool doCollisionCheck);

        void GetAlignmentDomain(const PartInst& pi, float sweepDist);
        void Draw(DrawContext& dc) override;
    };
}