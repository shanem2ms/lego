#include "StdIncludes.h"
#include "Level.h"
#include "leveldb/dumpfile.h"
#include "leveldb/env.h"
#include "leveldb/status.h"
#include "leveldb/options.h"
#include "leveldb/filter_policy.h"
#include "leveldb/cache.h"
#include "leveldb/zlib_compressor.h"
#include "leveldb/decompress_allocator.h"
#include "leveldb/db.h"
#include "Enet.h"
#include <thread>


namespace sam
{   
    
    class NullLogger : public leveldb::Logger {
    public:
        void Logv(const char*, va_list) override {
        }
    };


    LevelSvr::LevelSvr(bool disableWrite) :
        m_disableWrite(disableWrite),
        m_db(nullptr)
    {
    }

      
    void LevelSvr::OpenDb(const std::string& path)
    {
        //leveldb::Env* env = leveldb::Env::Default();
        leveldb::Options options;
        //create a bloom filter to quickly tell if a key is in the database or not
        options.filter_policy = leveldb::NewBloomFilterPolicy(10);

        //create a 40 mb cache (we use this on ~1gb devices)
        options.block_cache = leveldb::NewLRUCache(40 * 1024 * 1024);

        //create a 4mb write buffer, to improve compression and touch the disk less
        options.write_buffer_size = 4 * 1024 * 1024;

        //disable internal logging. The default logger will still print out things to a file
        options.info_log = new NullLogger();

        //use the new raw-zip compressor to write (and read)
        options.compressors[0] = new leveldb::ZlibCompressorRaw(-1);

        //also setup the old, slower compressor for backwards compatibility. This will only be used to read old compressed blocks.
        options.compressors[1] = new leveldb::ZlibCompressor();

        options.create_if_missing = true;

        leveldb::Status status = leveldb::DB::Open(options, path.c_str(), &m_db);
    }

    bool LevelSvr::GetValue(const std::string &k, std::string* val) const
    {
        leveldb::Slice key(k);
        leveldb::Status status = m_db->Get(leveldb::ReadOptions(), key, val);
        return status.ok();
    }

    bool LevelSvr::WriteValue(const std::string& k, const char* byte, size_t len)
    {
        if (m_disableWrite)
            return true;
        leveldb::Slice key(k);
        leveldb::Slice val(byte, len);
        leveldb::Status status = m_db->Put(leveldb::WriteOptions(), key, val);
        return status.ok();
    }   

    ENetResponse LevelSvr::HandleMessage(const ENetMsg::Header* msg)
    {
        ENetResponse response;
        if (msg->m_type == ENetMsg::GetLevelDbValue)
        {
            GetLevelValueMsg gmsg;
            gmsg.ReadData((const uint8_t*)msg);
            std::string val;
            bool result = GetValue(gmsg.m_key, &response.data);
            if (!result) response.data = std::string();
        }
        else if (msg->m_type == ENetMsg::SetLevelDbValue)
        {
            SetLevelValueMsg gmsg;
            gmsg.ReadData((const uint8_t*)msg);
            Loc tileLoc;
            memcpy(&tileLoc, gmsg.m_key.data(), gmsg.m_key.size());
            std::string val;
            bool result = WriteValue(gmsg.m_key, gmsg.m_data.data(), gmsg.m_data.size());
            if (!result) response.data = std::string();

        }
        return response;
    }

    LevelCli::LevelCli()
    {

    }
    void LevelCli::Connect(ENetClient *cli)
    {
        m_client = cli;
    }
    
    template<typename R>
    bool is_ready(std::future<R> const& f)
    {
        return f.wait_for(std::chrono::seconds(0)) == std::future_status::ready;
    }
    bool LevelCli::GetOctChunk(const Loc& l, std::string* val) const
    {
        auto itCache = m_cache.find(l);
        if (itCache != m_cache.end())
        {
            *val = itCache->second;
            return true;
        }

        int itemReady = 0;
        for (auto itCheck = m_requests.begin(); itCheck != m_requests.end();)
        {
            if (is_ready(itCheck->second))
            {
                ENetResponse resp = itCheck->second.get();
                *val = resp.data;
                if (itCheck->first == l)
                    itemReady = 1;
                m_cache.insert(std::make_pair(itCheck->first, resp.data));
                itCheck = m_requests.erase(itCheck);
                *val = resp.data;
                
            }
            else
            {
                if (itCheck->first == l)
                    itemReady = -1;
                ++itCheck;
            }
        }

        if (itemReady == 1)
            return true;
        else if (itemReady == -1)
            return false;

        auto itRequest = m_requests.find(l);
        if (itRequest == m_requests.end())
        {
            std::future<ENetResponse> future = m_client->Send(std::make_shared<GetLevelValueMsg>((const uint8_t*)&l, sizeof(l)));
            itRequest =
                m_requests.insert(std::move(std::make_pair(l, 
                    std::move(future)))).first;
        }
        
        return false;
    }
    bool LevelCli::WriteOctChunk(const Loc& il, const char* byte, size_t len)
    {        
        auto future = m_client->Send(std::make_shared<SetLevelValueMsg>((const uint8_t *) & il, sizeof(il), byte, len));
        ENetResponse resp = future.get();
        return resp.status == 1;
    }

    bool LevelCli::WritePlayerData(const PlayerData& pos)
    {
        const char* key = "cam";
        auto future = m_client->Send(std::make_shared<SetLevelValueMsg>((const uint8_t*)key, 3, (const char *)& pos, sizeof(pos)));
        return true;
    }

    bool LevelCli::GetPlayerData(PlayerData& pos)
    {
        const char* key = "cam";
        auto future = m_client->Send(std::make_shared<GetLevelValueMsg>((const uint8_t*)key, 3));
        ENetResponse resp = future.get();
        if (resp.data.size() == sizeof(pos))
        {
            memcpy(&pos, resp.data.data(), sizeof(pos));
            return true;
        }
        return false;
    }

}