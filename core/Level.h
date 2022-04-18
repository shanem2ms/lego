#pragma once

#include "StdIncludes.h"
#include <map>
#include <set>
#include <functional>
#include "Loc.h"
#include "PartDefs.h"

namespace leveldb
{
    class DB;
}

namespace sam
{
    class TerrainTile;

    class Level {
        leveldb::DB* m_db;
        bool m_disableWrite;
    public: 

        struct PlayerData
        {
            Vec3f pos;
            Vec2f dir;
            bool flymode;
            bool inspect;
            Vec3f inspectpos;
            Vec2f inspectdir;
            PartInst rightHandPart;
            SlotPart slots[16];
        };

        Level(bool disableWrite);
        void OpenDb(const std::string& path);

        bool GetOctChunk(const Loc& l, std::string* val) const;
        bool WriteOctChunk(const Loc& il, const char* byte, size_t len);
        bool WritePlayerData(const PlayerData &pos);
        bool GetPlayerData(PlayerData& pos);
    };
}
