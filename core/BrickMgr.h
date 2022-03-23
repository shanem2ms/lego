#pragma once

#include <map>
#include <set>
#include <list>
#include <filesystem>
#include "SceneItem.h"
#include "Engine.h"
#include "Loc.h"
#include "PartDefs.h"
#include "Mesh.h"

struct CubeList;
struct LdrRenderModel;
typedef const LdrRenderModel* LdrRenderModelHDL;
class btCompoundShape;
namespace ldr
{
    struct Loader;
    struct LdrPrimitive;
}
namespace sam
{
    class BrickThreadPool;
    void DestroyBrickThreadPool(BrickThreadPool*);
    struct ConnectorInfo;
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

        U& operator [](const T &key)
        {
            return _map[key];
        }

        const U& operator [](const T &key) const
        {
            return _map[key];
        }

        void sort()
        {
            std::vector<std::pair<T, U>> items;
            for (const auto &pair : _map)
            {
                items.push_back(pair);
            }
            std::sort(items.begin(), items.end(), [](const std::pair<T, U>& lsh, std::pair<T, U>& rhs)
                {
                    return lsh.second < rhs.second;
                });
            _index.clear();
            for (const auto& pair : items)
            {
                _index.push_back(pair.first);
            }
        }
        size_t size() const { return _map.size(); }

        const std::vector<T> &keys() const
        { return _index; }
        std::vector<T>& keys()
        { return _index; }

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
        if ((lhs.desc.size() == 0) !=
            (rhs.desc.size() == 0))
            return lhs.desc.size() == 0;
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

    enum ConnectorType
    {
        Unknown = 0,
        Stud,
        InvStud,
        PinholeStud
    };

  
    static Vec3f ScaleForType(ConnectorType ctype)
    {
        return Vec3f(5, 5, 5);
    }
    struct Connector
    {
        ConnectorType type;
        Vec3f pos;
        Vec3f scl;
        Vec3f dir;
        int pickIdx;

        Quatf GetDirAsQuat()
        {
            if (dot(Vec3f(0, 1, 0), dir) > 0.999f)
                return Quatf(QUAT_MULT_IDENTITYF);
            else
            {
                Matrix44f mat = makeRot<Matrix44f>(Vec3f(0, 1, 0), dir);
                return make<Quatf>(mat);
            }
        }
        static bool CanConnect(ConnectorType a, ConnectorType b)
        {
            if (a > b)
            {
                ConnectorType tmp = b;
                b = a;
                a = tmp;
            }
            if (a == Stud && b == InvStud)
                return true;
            return false;
        }
    };

    inline bool operator < (const Vec3f& lhs, const Vec3f& rhs)
    {
        for (int i = 0; i < 3; ++i)
        {
            if (lhs[i] != rhs[i])
                return lhs[i] < rhs[i];
        }
        return false;
    }
    inline bool operator < (const Quatf& lhs, const Quatf& rhs)
    {
        for (int i = 0; i < 4; ++i)
        {
            if (lhs[i] != rhs[i])
                return lhs[i] < rhs[i];
        }
        return false;
    }

    inline bool operator < (const Connector& lhs, const Connector& rhs)
    {
        if (lhs.type != rhs.type)
            return lhs.type < rhs.type;
        if (lhs.pos != rhs.pos)
            return lhs.pos < rhs.pos;
        return lhs.dir < rhs.dir;
    }
    inline bool operator == (const Connector& lhs, const Connector& rhs)
    {
        if (lhs.type != rhs.type)
            return false;
        return lhs.pos == rhs.pos;
    }

    struct Brick
    {
        std::string m_name;
        std::vector<PosTexcoordNrmVertex> m_verticesLR;
        std::vector<uint32_t> m_indicesLR;
        bgfxh<bgfx::VertexBufferHandle> m_vbhLR;
        bgfxh<bgfx::IndexBufferHandle> m_ibhLR;

        std::vector<PosTexcoordNrmVertex> m_verticesHR;
        std::vector<uint32_t> m_indicesHR;
        bgfxh<bgfx::VertexBufferHandle> m_vbhHR;
        bgfxh<bgfx::IndexBufferHandle> m_ibhHR;

        AABoxf m_bounds;
        AABoxf m_collisionBox;
        float m_scale;
        bgfxh<bgfx::TextureHandle> m_icon;
        size_t m_mruCtr;
        Vec3f m_center;
        bool m_connectorsLoaded;
        std::vector<Connector> m_connectors;
        std::shared_ptr<CubeList> m_connectorCL;
        std::shared_ptr<btCompoundShape> m_collisionShape;


    private:
        void LoadLores(ldr::Loader* pLoader,
            const std::string& name, std::filesystem::path& cachePath);
        void LoadHires(const std::string& name, std::filesystem::path& cachePath);
        void LoadConnectors(const std::filesystem::path &connectorPath);
        void LoadCollisionMesh(const std::filesystem::path& collisionPath);
        void LoadPrimitives(ldr::Loader* pLoader);
        void GenerateCacheItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
            const std::string& name, std::filesystem::path& cachePath, 
            const std::vector<int> atlasMaterialMapping, bool hires);
        friend class BrickManager;
    };

    struct RGBA
    {
        uint8_t r;
        uint8_t g;
        uint8_t b;
        uint8_t a;
    };
    struct BrickColor
    {
        int code;
        std::string name;
        RGBA fill;
        RGBA edge;
        int atlasidx;
    };

    class BrickManager : public IEngineDraw
    {
    public:
        // Lego unit = 20, this makes each 1x1 space equal 16x16 legos
        static constexpr float Scale = 1 / 20.0f;

        static BrickManager& Inst();
        Brick *GetBrick(const PartId& name, bool hires = false);
        BrickManager(const std::string& ldrpath);
        static Vec4f Color(uint32_t hex);
        const PartId& GetPartId(size_t idx);
        std::string PartDescription(const std::string& partname);
        size_t NumParts() const
        { return m_partsMap.size(); }
        size_t NumTypes() const
        { return m_typesMap.size(); }
        const std::string &TypeName(size_t idx) const
        { return m_typesMap.keys()[idx]; }

        void Draw(DrawContext& ctx) override;
        void MruUpdate(Brick* pBrick);        
        void LoadConnectors(Brick* pBrick);
        void LoadCollision(Brick* pBrick);
        void LoadPrimitives(Brick* pBrick);
        const std::vector<PartId>& PartsForType(
            const std::string typestr)
        {
            auto ittype = m_typesMap.find(typestr);
            if (ittype == m_typesMap.end())
                return m_typesMap.begin()->second;
            return ittype->second;
        }

        size_t NumColors() const
        { return m_colors.size(); }

        const BrickColor& GetColor(size_t idx) const
        {
            int key = m_colors.keys()[idx];
            return m_colors.find(key)->second; }

        bgfx::TextureHandle Palette() const
        {
            return m_colorPalette;
        }
    private:
        void LoadColors(const std::string& ldrpath);
        void LoadAllParts(const std::string& ldrpath);
        void CleanCache();

        std::map<PartId, Brick> m_bricks;
        bgfxh<bgfx::UniformHandle> m_paletteHandle;
        std::shared_ptr<ldr::Loader> m_ldrLoaderHR;
        std::shared_ptr<ldr::Loader> m_ldrLoaderLR;
        index_map<PartId, PartDesc> m_partsMap;
        index_map<std::string, std::vector<PartId>> m_typesMap;
        std::filesystem::path m_cachePath;
        std::filesystem::path m_connectorPath;
        std::filesystem::path m_collisionPath;
        std::unique_ptr<BrickThreadPool> m_threadPool;
        std::vector<Brick*> m_brickRenderQueue;
        bgfxh<bgfx::TextureHandle> m_iconDepth;
        bgfxh<bgfx::TextureHandle> m_colorPalette;
        size_t m_mruCtr;
        index_map<int, BrickColor> m_colors;
    };
}