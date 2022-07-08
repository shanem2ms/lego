#pragma once

#include "StdIncludes.h"
#include <map>
#include <set>
#include <functional>
#include "Loc.h"
#include "PartDefs.h"
#include "Enet.h"
#include "dbl_list.h"

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

        struct OctKey
        {
            OctKey(const Loc& l, char type, uint16_t mdata = 0) :
                x(l.m_x),
                y(l.m_y),
                z(l.m_z),
                l((l.m_l & 0xFF) |
                    ((type & 0xFF) << 8) |
                    (mdata & 0xFFFF) << 16)
            {

            }
            int x;
            int y;
            int z;
            int l;

            bool operator == (const OctKey& other) const
            {
                return x == other.x &&
                    y == other.y &&
                    z == other.z &&
                    l == other.l;
            }

            bool operator < (const OctKey& rhs) const
            {
                if (l != rhs.l)
                    return l < rhs.l;
                if (x != rhs.x)
                    return x < rhs.x;
                if (y != rhs.y)
                    return y < rhs.y;
                return z < rhs.z;
            }
        };

        virtual bool GetOctChunk(const OctKey &, std::string* val) const = 0;
        virtual bool WriteOctChunk(const OctKey &, const char* byte, size_t len) = 0;
        virtual bool WritePlayerData(const PlayerData& pos) = 0;
        virtual bool GetPlayerData(PlayerData& pos) = 0;
    };

    class LevelSvr : public IServerHandler {
        leveldb::DB* m_db;
        bool m_disableWrite;

        bool AutoGenerateTile(const ILevel::OctKey& k, std::string* val) const;
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
        mutable std::map<OctKey,
            std::future<ENetResponse>> m_requests;
        mutable std::map<OctKey, std::string> m_cache;
    public:
        LevelCli();
        void Connect(ENetClient *cli);
        bool GetOctChunk(const ILevel::OctKey& l, std::string* val) const override;

        bool WriteOctChunk(const ILevel::OctKey& il, const char* byte, size_t len) override;
        bool WritePlayerData(const PlayerData& pos) override;
        bool GetPlayerData(PlayerData& pos) override;
    };
     
    struct GetLevelValueMsg : public ENetMsg
    {
        std::string m_key;
        GetLevelValueMsg(const uint8_t* key, size_t klen) :
            ENetMsg(Type::GetLevelDbValue),
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
            ENetMsg(Type::SetLevelDbValue),
            m_key(key, key+klen),
            m_data(byte, byte + len)
        {}

        SetLevelValueMsg() : ENetMsg(Type::SetLevelDbValue) {}

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
