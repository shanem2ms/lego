#pragma once

#include <map>
#include <set>
#include <list>
#include <filesystem>
#include <mutex>
#include "SceneItem.h"
#include "Engine.h"
#include "ConnectionLogic.h"

#include "Loc.h"
#include "PartDefs.h"
#include "Mesh.h"
#include "indexed_map.h"

struct CubeList;
class btCompoundShape;
namespace ldr
{
    struct Loader;
    struct LdrPrimitive;
}
namespace sam
{
    struct ConnectorInfo;
}

namespace sam
{       
    class ZipFile;
    class vecstream;
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

    struct Brick
    {
        PartId m_name;
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


        Brick(const PartId& name) :
            m_name(name),
            m_connectorsLoaded(false),
            m_scale(0),
            m_mruCtr(0) {}
    private:
        void LoadLores(
            const vecstream &data);
        void LoadHires(const vecstream& data);
        void LoadConnectors(const vecstream &stream);
        bool LoadCollisionMesh(const vecstream& stream);
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
        int legoId;
        std::string legoName;
    };

    class BrickManager : public IEngineDraw
    {
    public:
        // Lego unit = 20, this makes each 1x1 space equal 16x16 legos
        static constexpr float Scale = 1 / 20.0f;

        static BrickManager& Inst();
        std::shared_ptr<Brick> GetBrick(const PartId& name, bool hires = false);
        bgfx::TextureHandle GetBrickThumbnail(const PartId& name);
        BrickManager();
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
        bool LoadCollision(Brick* pBrick);
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

        const BrickColor& GetColorFromIdx(size_t idx) const
        {
            if (idx < 0 || idx >= m_colors.size())
                return m_colors.find(0)->second;
            int key = m_colors.keys()[idx];
            return m_colors.find(key)->second; }

        const BrickColor& GetColorFromCode(uint32_t code) const
        {
            auto it = m_colors.find(code);
            if (it == m_colors.end()) return m_colors.begin()->second;
            return it->second;
        }

        const BrickColor& GetColorFromLegoId(uint32_t legoId) const
        {
            for (const auto& pair : m_colors)
            {
                if (pair.second.legoId == legoId)
                    return pair.second;
            }
            return m_colors.begin()->second;
        }

        bgfx::TextureHandle Palette() const
        {
            return m_colorPalette;
        }

        const std::string& PartAlias(const std::string& name);
    private:
        void LoadColors();
        void LoadAllParts();
        void DownloadCacheFile();
        void CleanCache();

        std::map<PartId, std::shared_ptr<Brick>> m_bricks;
        bgfxh<bgfx::UniformHandle> m_paletteHandle;
        index_map<PartId, PartDesc> m_partsMap;
        index_map<std::string, std::vector<PartId>> m_typesMap;
        std::filesystem::path m_cachePath;
        std::mutex m_cacheMtx;
        std::vector<std::shared_ptr<Brick>> m_brickRenderQueue;
        bgfxh<bgfx::TextureHandle> m_iconDepth;
        bgfxh<bgfx::TextureHandle> m_colorPalette;
        size_t m_mruCtr;
        index_map<int, BrickColor> m_colors;
        std::map<std::string, std::string> m_aliasParts;
        std::shared_ptr<ZipFile> m_cacheZip;
    };
}
