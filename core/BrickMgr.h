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
    struct Brick
    {
        bgfxh<bgfx::VertexBufferHandle> m_vbh;
        bgfxh<bgfx::IndexBufferHandle> m_ibh;
        AABoxf m_bounds;
        float m_scale;
        bgfxh<bgfx::TextureHandle> m_icon;
        size_t m_mruCtr;
    private:
        void Load(ldr::Loader* pLoader, BrickThreadPool* threadPool,
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
        { return m_partnames.size(); }
        void Draw(DrawContext& ctx) override;
        void MruUpdate(Brick* pBrick);
        void CleanCache();
    private:
        std::map<std::string, Brick> m_bricks;
        std::shared_ptr<ldr::Loader> m_ldrLoader;
        std::vector<std::pair<int, std::string>> m_partnames;
        std::filesystem::path m_cachePath;
        std::unique_ptr<BrickThreadPool> m_threadPool;
        std::vector<Brick*> m_brickRenderQueue;
        bgfxh<bgfx::TextureHandle> m_iconDepth;
        size_t m_mruCtr;
    };
}