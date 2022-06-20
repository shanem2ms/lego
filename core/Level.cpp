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

    bool LevelSvr::GetOctChunk(const Loc& l, std::string* val) const
    {
        leveldb::Slice key((const char*)&l, sizeof(l));
        leveldb::Status status = m_db->Get(leveldb::ReadOptions(), key, val);
        return status.ok();
    }

    bool LevelSvr::WriteOctChunk(const Loc& l, const char* byte, size_t len)
    {
        if (m_disableWrite)
            return true;
        leveldb::Slice key((const char*)&l, sizeof(l));
        leveldb::Slice val(byte, len);
        leveldb::Status status = m_db->Put(leveldb::WriteOptions(), key, val);
        return status.ok();
    }

    bool LevelSvr::WritePlayerData(const ILevel::PlayerData& pos)
    {
        if (m_disableWrite)
            return true;
        leveldb::Slice key("cam", 3);
        leveldb::Slice val((const char *)&pos, sizeof(ILevel::PlayerData));
        leveldb::Status status = m_db->Put(leveldb::WriteOptions(), key, val);
        return status.ok();
    }

    bool LevelSvr::GetPlayerData(PlayerData& pos)
    {
        leveldb::Slice key("cam", 3);
        std::string val;
        leveldb::Status status = m_db->Get(leveldb::ReadOptions(), key, &val);
        if (status.ok() && val.size() == sizeof(PlayerData))
        {
            memcpy(&pos, val.data(), sizeof(PlayerData));
            return true;
        }
        return false;
    }


    LevelCli::LevelCli()
    {

    }
    void LevelCli::Connect(ENetClient *cli, const std::string& path)
    {
        m_client = cli;
    }

    bool LevelCli::GetOctChunk(const Loc& l, std::string* val) const
    {
        auto promise = m_client->Send(std::make_shared<GetOctTileMsg>(l));
        ENetResponse resp = promise.get();
        *val = resp.data;
        return true;
    }
    bool LevelCli::WriteOctChunk(const Loc& il, const char* byte, size_t len)
    {
        return true;
    }

    bool LevelCli::WritePlayerData(const PlayerData& pos)
    {
        return true;
    }

    bool LevelCli::GetPlayerData(PlayerData& pos)
    {
        return true;
    }

}