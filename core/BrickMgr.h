#pragma once

#include <map>
#include <set>
#include <list>
#include <filesystem>
#include "SceneItem.h"
#include "Engine.h"
#include "Loc.h"

struct VoxCube;
namespace ldr
{
    struct Loader;
}
namespace sam
{
    class BrickThreadPool;
    void DestroyBrickThreadPool(BrickThreadPool*);
}

void std::default_delete<sam::BrickThreadPool>::operator()(sam::BrickThreadPool* ptr) const noexcept
{
    DestroyBrickThreadPool(ptr);
}

namespace sam
{   
    template <typename T, typename U> class index_map
    {
    public:
        typename std::map<T, U>::iterator insert(const std::pair<T, U> kv)
        {
            auto it = _map.insert(kv).first;
            _index.push_back(kv.first);
            return it;
        }

        typename std::map<T, U>::iterator begin()
        { return _map.begin(); }
        typename std::map<T, U>::const_iterator begin() const
        {
            return _map.begin();
        }
        typename std::map<T, U>::iterator end()
        { return _map.end(); }
        typename std::map<T, U>::const_iterator end() const
        {
            return _map.end();
        }
        typename std::map<T, U>::const_iterator find(const T &key) const
        { return _map.find(key); }
        typename std::map<T, U>::iterator find(const T& key)
        {
            return _map.find(key);
        }

        typename std::map<T, U>::iterator erase(typename std::map<T, U>::iterator it)
        {
            auto iterase = std::remove(_index.begin(), _index.end(), it->first);
            _index.erase(iterase, _index.end());
            return _map.erase(it);
        }

        size_t size() const { return _map.size(); }

        T& operator [](size_t idx)
        { return _index[idx]; }

        const T& operator [](size_t idx) const
        { return _index[idx]; }

    private:
        std::map<T, U>  _map;
        std::vector<T> _index;
    };

    struct PartDesc
    {
        PartDesc()
        {
            memset(dims, 0, sizeof(dims));
        }
        int index;
        std::string type;
        int ndims;
        float dims[3];
        std::string desc;
        std::string filename;
    };

    inline bool operator < (const PartDesc& lhs, const PartDesc& rhs)
    {
        if (lhs.type != rhs.type)
            return lhs.type < rhs.type;
        if (lhs.ndims != rhs.ndims)
            return lhs.ndims < rhs.ndims;
        for (int i = 0; i < lhs.ndims; ++i)
        {
            if (lhs.dims[i] != rhs.dims[i])
                return lhs.dims[i] < rhs.dims[i];
        }

        return lhs.desc < rhs.desc;
    }

    struct Brick
    {
        bgfxh<bgfx::VertexBufferHandle> m_vbh;
        bgfxh<bgfx::IndexBufferHandle> m_ibh;
        AABoxf m_bounds;
        float m_scale;
        bgfxh<bgfx::TextureHandle> m_icon;
        size_t m_mruCtr;
        Vec3f m_center;
    private:
        void Load(ldr::Loader* pLoader, BrickThreadPool* threadPool,
            const std::string& name, std::filesystem::path& cachePath);
        void GenerateCacheItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
            const std::string& name, std::filesystem::path& cachePath);
        friend class BrickManager;
    };

    class BrickManager : public IEngineDraw
    {
    public:
        // Lego unit = 20, this makes each 1x1 space equal 16x16 legos
        static constexpr float Scale = 1 / 320.0f;

        
        static BrickManager& Inst();
        Brick *GetBrick(const std::string& name);
        BrickManager(const std::string& ldrpath);
        static Vec4f Color(uint32_t hex);
        const std::string& PartName(size_t idx);
        size_t NumParts() const
        { return m_partsMap.size(); }
        size_t NumTypes() const
        { return m_typesMap.size(); }
        const std::string &TypeName(size_t idx) const
        { return m_typesMap[idx]; }

        void Draw(DrawContext& ctx) override;
        void MruUpdate(Brick* pBrick);        
        const std::vector<std::string>& PartsForType(
            const std::string typestr)
        {
            auto ittype = m_typesMap.find(typestr);
            if (ittype == m_typesMap.end())
                return m_typesMap.begin()->second;
            return ittype->second;
        }
    private:
        void LoadAllParts(const std::string& ldrpath);
        void CleanCache();

        std::map<std::string, Brick> m_bricks;
        std::shared_ptr<ldr::Loader> m_ldrLoader;
        index_map<std::string, PartDesc> m_partsMap;
        index_map<std::string, std::vector<std::string>> m_typesMap;
        std::filesystem::path m_cachePath;
        std::unique_ptr<BrickThreadPool> m_threadPool;
        std::vector<Brick*> m_brickRenderQueue;
        bgfxh<bgfx::TextureHandle> m_iconDepth;
        size_t m_mruCtr;
    };
}