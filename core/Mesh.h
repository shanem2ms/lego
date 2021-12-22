#pragma once
#include "StdIncludes.h"
#include "SceneItem.h"
#include <bgfx/bgfx.h>

using namespace gmtl;

struct PosTexcoordVertex
{
    float m_x;
    float m_y;
    float m_z;
    float m_u;
    float m_v;

    static void init()
    {
        ms_layout
            .begin()
            .add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float)
            .add(bgfx::Attrib::TexCoord0, 2, bgfx::AttribType::Float)
            .end();
    };

    static bgfx::VertexLayout ms_layout;
};

struct PosTexcoordNrmVertex
{
    float m_x;
    float m_y;
    float m_z;
    float m_u;
    float m_v;
    float m_nx;
    float m_ny;
    float m_nz;

    static void init()
    {
        static bool isinit = false;
        if (!isinit)
        {
            ms_layout
                .begin()
                .add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float)
                .add(bgfx::Attrib::TexCoord0, 2, bgfx::AttribType::Float)
                .add(bgfx::Attrib::Normal, 3, bgfx::AttribType::Float)
                .end();
            isinit = true;
        }
    };

    static bgfx::VertexLayout ms_layout;
};

struct VoxelVertex
{
    float x;
    float y;
    float z;
    float d;

    static void init()
    {
        static bool isinit = false;
        if (!isinit)
        {
            ms_layout
                .begin()
                .add(bgfx::Attrib::TexCoord7, 4, bgfx::AttribType::Float)
                .end();
            isinit = true;
        }
    };

    static bgfx::VertexLayout ms_layout;
};

struct Cube
{
    inline static PosTexcoordNrmVertex s_cubeVertices[] =
    {
        {-1.0f,  1.0f,  1.0f,  0.0f,  1.0f, 0, 0, 1},
        { 1.0f,  1.0f,  1.0f,  1.0f,  1.0f, 0, 0, 1},
        {-1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0, 0, 1},
        { 1.0f, -1.0f,  1.0f,  1.0f,  0.0f, 0, 0, 1},

        {-1.0f,  1.0f, -1.0f, 0.0f,  1.0f, 0, 0, -1},
        { 1.0f,  1.0f, -1.0f, 1.0f,  1.0f, 0, 0, -1},
        {-1.0f, -1.0f, -1.0f, 0.0f,  0.0f, 0, 0, -1},
        { 1.0f, -1.0f, -1.0f, 1.0f,  0.0f, 0, 0, -1},

        {-1.0f,  1.0f,  1.0f, 0.0f,  1.0f, -1, 0, 0 },
        {-1.0f,  1.0f, -1.0f, 1.0f,  1.0f, -1, 0, 0 },
        {-1.0f, -1.0f,  1.0f, 0.0f,  0.0f, -1, 0, 0 },
        {-1.0f, -1.0f, -1.0f, 1.0f,  0.0f, -1, 0, 0 },

        { 1.0f,  1.0f,  1.0f, 0.0f,  1.0f, 1, 0, 0 },
        { 1.0f, -1.0f,  1.0f, 1.0f,  1.0f, 1, 0, 0},
        { 1.0f,  1.0f, -1.0f, 0.0f,  0.0f, 1, 0, 0},
        { 1.0f, -1.0f, -1.0f, 1.0f,  0.0f, 1, 0, 0},

        {-1.0f,  1.0f,  1.0f, 0.0f,  1.0f, 0, 1, 0},
        { 1.0f,  1.0f,  1.0f, 1.0f,  1.0f, 0, 1, 0},
        {-1.0f,  1.0f, -1.0f, 0.0f,  0.0f, 0, 1, 0},
        { 1.0f,  1.0f, -1.0f, 1.0f,  0.0f, 0, 1, 0},

        {-1.0f, -1.0f,  1.0f, 0.0f,  1.0f, 0, -1, 0},
        {-1.0f, -1.0f, -1.0f, 1.0f,  1.0f, 0, -1, 0},
        { 1.0f, -1.0f,  1.0f, 0.0f,  0.0f, 0, -1, 0},
        { 1.0f, -1.0f, -1.0f, 1.0f,  0.0f, 0, -1, 0},
    };

    static void init()
    {
        if (isInit)
            return;
        PosTexcoordNrmVertex::init();



    

        static const uint16_t s_cubeIndices[] =
        {
             0,  1,  2, // 0
             1,  3,  2,

             4,  6,  5, // 2
             5,  6,  7,

             8, 10,  9, // 4
             9, 10, 11,

            12, 14, 13, // 6
            14, 15, 13,

            16, 18, 17, // 8
            18, 19, 17,

            20, 22, 21, // 10
            21, 22, 23,
        };

        vbh = bgfx::createVertexBuffer(
            bgfx::makeRef(s_cubeVertices, sizeof(s_cubeVertices))
            , PosTexcoordNrmVertex::ms_layout
        );

        ibh = bgfx::createIndexBuffer(
            bgfx::makeRef(s_cubeIndices, sizeof(s_cubeIndices))
        );

        isInit = true;
    }

    static bgfx::VertexBufferHandle vbh;
    static bgfx::IndexBufferHandle ibh;
    static bool isInit;
};

struct CubeList
{
    void Create(const std::vector<Vec3f>& pts, float cubeSize);

    CubeList() :
        pvertices(nullptr),
        verticesSize(0),
        pindices(nullptr),
        indicesSize(0),
        memsize(0)
    {
    }
    ~CubeList();
    void Use();
    bgfxh<bgfx::VertexBufferHandle> vbh;
    bgfxh<bgfx::IndexBufferHandle> ibh;
    PosTexcoordNrmVertex *pvertices;
    size_t verticesSize;
    uint32_t *pindices;
    size_t indicesSize;
    size_t memsize;
    static void ReleaseFn(void *ptr, void* pThis);

};

template <int N> struct Grid
{
    static void init();

    static bgfx::VertexBufferHandle vbh;
    static bgfx::IndexBufferHandle ibh;
    static std::vector<PosTexcoordVertex> vertices;
    static std::vector<uint16_t> indices;
    static bool isInit;
};


struct Quad
{
    static void init()
    {
        if (isInit)
            return;
        PosTexcoordVertex::init();


        static PosTexcoordVertex s_quadVertices[] =
        {
            {-1.0f,  1.0f,  0.0f,  0.0f,  1.0f},
            { 1.0f,  1.0f,  0.0f,  1.0f,  1.0f},
            {-1.0f, -1.0f,  0.0f,  0.0f,  0.0f},
            { 1.0f, -1.0f,  0.0f,  1.0f,  0.0f}
        };

        static const uint16_t s_quadIndices[] =
        {
             0,  1,  2, // 0
             1,  3,  2
        };

        vbh = bgfx::createVertexBuffer(
            bgfx::makeRef(s_quadVertices, sizeof(s_quadVertices))
            , PosTexcoordVertex::ms_layout
        );

        ibh = bgfx::createIndexBuffer(
            bgfx::makeRef(s_quadIndices, sizeof(s_quadIndices))
        );

        isInit = true;
    }

    static bgfx::VertexBufferHandle vbh;
    static bgfx::IndexBufferHandle ibh;
    static bool isInit;
};

struct VoxCube
{
    void Create(const std::vector<Vec3i>& pts);

    VoxCube() :
        //pvertices(nullptr),
        verticesSize(0),
        memsize(0)
    {
    }
    ~VoxCube();
    void Use();

    bgfxh<bgfx::VertexBufferHandle> vbh;

    VoxelVertex* pvertices;
    size_t verticesSize;
    size_t memsize;
    static void ReleaseFn(void* ptr, void* pThis);

};
