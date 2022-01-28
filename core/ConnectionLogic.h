#pragma once
#include <memory>

namespace sam
{
    class LegoBrick;
    class Player;
    class OctTileSelection;
    class PartInst;

    class ConnectionLogic
    {
    public:
        void PlaceBrick(std::shared_ptr<Player> player, std::shared_ptr<LegoBrick> pickedBrick,
            OctTileSelection &octTileSelection, bool doCollisionCheck);

        void GetAlignmentDomain(const PartInst& pi, float sweepDist);
    };
}