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

    class LevelSvr : public IServerHandler {
        leveldb::DB* m_db;
        bool m_disableWrite;
    public: 
        LevelSvr(bool disableWrite);
        void OpenDb(const std::string& path);

        bool GetValue(const std::string& key, std::string* val) const;
        bool WriteValue(const std::string& key, const char* byte, size_t len);
        ENetResponse HandleMessage(const ENetMsg::Header* msg);
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
     
    struct GetLevelValueMsg : public ENetMsg
    {
        std::string m_key;
        GetLevelValueMsg(const uint8_t* key, size_t klen) :
            ENetMsg(Type::GetValue),
            m_key(key, key + klen)
        {}

        GetLevelValueMsg() {}

        size_t GetSize() const override
        {
            return ENetMsg::GetSize() +
                sizeof(uint32_t) +
                m_key.size();
        }
        virtual uint8_t* WriteData(uint8_t* data)
        {
            uint8_t* dataNext = ENetMsg::WriteData(data);
            uint32_t sz = m_key.size();
            memcpy(dataNext, &sz, sizeof(sz));
            dataNext += sizeof(sz);
            memcpy(dataNext, m_key.data(), sz);
            dataNext += sz;
            return dataNext;
        }

        virtual const uint8_t* ReadData(const uint8_t* data)
        {
            const uint8_t* dataNext = ENetMsg::ReadData(data);
            uint32_t sz;
            memcpy(&sz, dataNext, sizeof(sz));
            dataNext += sizeof(sz);
            m_key.resize(sz);
            memcpy(m_key.data(), dataNext, sz);
            dataNext += sz;
            return dataNext;
        };

    };
   
    struct SetLevelValueMsg : public ENetMsg
    {
        std::string m_key;
        std::string m_data;

        SetLevelValueMsg(const uint8_t* key, size_t klen,
            const char* byte, size_t len) :
            ENetMsg(Type::SetValue),
            m_key(key, key+klen),
            m_data(byte, byte + len)
        {}

        SetLevelValueMsg() : ENetMsg(Type::SetValue) {}

        size_t GetSize() const override
        {
            return ENetMsg::GetSize() +
                sizeof(uint32_t) +
                m_key.size() +
                sizeof(uint32_t) +
                m_data.size();
        }
        virtual uint8_t* WriteData(uint8_t* data)
        {
            uint8_t* dataNext = ENetMsg::WriteData(data);
            uint32_t sz = m_key.size();
            memcpy(dataNext, &sz, sizeof(sz));
            dataNext += sizeof(sz);
            memcpy(dataNext, m_key.data(), sz);
            dataNext += sz;


            sz = m_data.size();
            memcpy(dataNext, &sz, sizeof(sz));
            dataNext += sizeof(sz);
            memcpy(dataNext, m_data.data(), sz);
            dataNext += sz;
            return dataNext;
        }

        virtual const uint8_t* ReadData(const uint8_t* data)
        {
            const uint8_t* dataNext = ENetMsg::ReadData(data);

            uint32_t sz;
            memcpy(&sz, dataNext, sizeof(sz));
            dataNext += sizeof(sz);
            m_key.resize(sz);
            memcpy(m_key.data(), dataNext, sz);
            dataNext += sz;

            sz;
            memcpy(&sz, dataNext, sizeof(sz));
            dataNext += sizeof(sz);
            m_data.resize(sz);
            memcpy(m_data.data(), dataNext, sz);
            dataNext += sz;
            return dataNext;
        }
    };
}
