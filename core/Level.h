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
    class ENetClient;
    class ILevel {
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

        virtual bool GetOctChunk(const Loc& l, std::string* val) const = 0;
        virtual bool WriteOctChunk(const Loc& il, const char* byte, size_t len) = 0;
        virtual bool WritePlayerData(const PlayerData& pos) = 0;
        virtual bool GetPlayerData(PlayerData& pos) = 0;
    };

    class LevelSvr : public ILevel {
        leveldb::DB* m_db;
        bool m_disableWrite;
    public: 
        LevelSvr(bool disableWrite);
        void OpenDb(const std::string& path);

        bool GetOctChunk(const Loc& l, std::string* val) const override;
        bool WriteOctChunk(const Loc& il, const char* byte, size_t len) override;
        bool WritePlayerData(const PlayerData &pos) override;
        bool GetPlayerData(PlayerData& pos) override;
    };

    class LevelCli : public ILevel {
        bool m_disableWrite;
        ENetClient *m_client;
    public:
        LevelCli();
        void Connect(ENetClient *cli, const std::string& path);
        bool GetOctChunk(const Loc& l, std::string* val) const override;
        bool WriteOctChunk(const Loc& il, const char* byte, size_t len) override;
        bool WritePlayerData(const PlayerData& pos) override;
        bool GetPlayerData(PlayerData& pos) override;
    };

}
