#pragma once

#include "StdIncludes.h"
#include <map>
#include <set>
#include <functional>
#include "Loc.h"
#include "PartDefs.h"
#include "Enet.h"

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

    struct GetOctTileMsg : public ENetMsg
    {
        Loc m_tileloc;
        GetOctTileMsg(const Loc& l) :
            ENetMsg(Type::GetOctTile, sizeof(GetOctTileMsg)),
            m_tileloc(l)
        {}

        GetOctTileMsg() : m_tileloc(0,0,0,0){}

        size_t GetSize() const override
        {
            return ENetMsg::GetSize() +
                sizeof(m_tileloc);
        }
        virtual uint8_t *WriteData(uint8_t* data)
        {
            uint8_t *dataNext = ENetMsg::WriteData(data);
            memcpy(dataNext, &m_tileloc, sizeof(m_tileloc));
            return dataNext + sizeof(m_tileloc);
        }

        virtual const uint8_t* ReadData(const uint8_t* data)
        {
            const uint8_t* dataNext = ENetMsg::ReadData(data);
            memcpy(&m_tileloc, dataNext, sizeof(m_tileloc));
            return dataNext + sizeof(m_tileloc);
        }
    };

    struct SetOctTileMsg : public ENetMsg
    {
        Loc m_tileloc;
        std::string m_data;

        SetOctTileMsg(const Loc& l, const char* byte, size_t len) :
            ENetMsg(Type::SetOctTile, sizeof(SetOctTile) - sizeof(m_data)),
            m_tileloc(l),
            m_data(byte, byte + len)
        {}

        SetOctTileMsg() : m_tileloc(0, 0, 0, 0) {}

        size_t GetSize() const override
        {
            return ENetMsg::GetSize() +
                sizeof(m_tileloc) + 
                sizeof(uint32_t) + 
                m_data.size();
        }
        virtual uint8_t* WriteData(uint8_t* data)
        {
            uint8_t* dataNext = ENetMsg::WriteData(data);
            memcpy(dataNext, &m_tileloc, sizeof(m_tileloc));
            dataNext += sizeof(m_tileloc);
            uint32_t sz = m_data.size();
            memcpy(dataNext, &sz, sizeof(sz));
            dataNext += sizeof(sz);
            memcpy(dataNext, m_data.data(), sz);
            dataNext += sz;
            return dataNext;
        }

        virtual const uint8_t* ReadData(const uint8_t* data)
        {
            const uint8_t* dataNext = ENetMsg::ReadData(data);
            memcpy(&m_tileloc, dataNext, sizeof(m_tileloc));
            dataNext += sizeof(m_tileloc);
            uint32_t sz;
            memcpy(&sz, dataNext, sizeof(sz));
            dataNext += sizeof(sz);
            m_data.resize(sz);
            memcpy(m_data.data(), dataNext, sz);
            dataNext += sz;
            return dataNext;
        }
    };
    
}
