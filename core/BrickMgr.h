#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "Loc.h"

struct VoxCube;
namespace ldr
{
    struct Loader;
}

namespace sam
{

    class BrickManager
    {
    public:
        // Lego unit = 20, this makes each 1x1 space equal 16x16 legos
        static constexpr float Scale = 1 / 320.0f;

        struct Brick
        {
            bgfxh<bgfx::VertexBufferHandle> m_vbh;
            bgfxh<bgfx::IndexBufferHandle> m_ibh;

            void Load(ldr::Loader *pLoader, const std::string& name);
        };

        static BrickManager& Inst();
        const Brick &GetBrick(const std::string& name);
        BrickManager(const std::string& ldrpath);
        static Vec4f Color(uint32_t hex);
    private:
        std::map<std::string, Brick> m_bricks;
        std::shared_ptr<ldr::Loader> m_ldrLoader;

    };
}