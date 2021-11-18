#include "StdIncludes.h"
#include "Mesh.h"

bgfx::VertexLayout PosTexcoordVertex::ms_layout;
bgfx::VertexLayout PosTexcoordNrmVertex::ms_layout;
bgfx::VertexLayout VoxelVertex::ms_layout;

bool Cube::isInit = false;
bgfx::VertexBufferHandle Cube::vbh;
bgfx::IndexBufferHandle Cube::ibh;


bool Quad::isInit = false;
bgfx::VertexBufferHandle Quad::vbh;
bgfx::IndexBufferHandle Quad::ibh;

template <int N> void Grid<N>::init()
{
    if (isInit)
        return;
    PosTexcoordVertex::init();
    const int size = N;
    vertices.resize(size * size);
    for (int x = 0; x < size; ++x)
    {
        for (int y = 0; y < size; ++y)
        {
            PosTexcoordVertex& v = vertices[y * size + x];
            v.m_x = ((float)(x) / (float)(size - 1)) * 2 - 1;
            v.m_z = ((float)(y) / (float)(size - 1)) * 2 - 1;
            v.m_y = 0;
            v.m_u = ((float)(x) / (float)(size - 1));
            v.m_v = ((float)(y) / (float)(size - 1));

            if (x < size - 1 &&
                y < size - 1)
            {
                int idx = y * size + x;
                indices.push_back(idx + 1);
                indices.push_back(idx + size);
                indices.push_back(idx);

                indices.push_back(idx + size + 1);
                indices.push_back(idx + size);
                indices.push_back(idx + 1);
            }
        }
    }

    vbh = bgfx::createVertexBuffer(
        bgfx::makeRef(vertices.data(), vertices.size() * sizeof(PosTexcoordVertex)),
        PosTexcoordVertex::ms_layout
    );

    ibh = bgfx::createIndexBuffer(
        bgfx::makeRef(indices.data(), indices.size() * sizeof(uint16_t))
    );

    isInit = true;
}

template <int N> bool Grid<N>::isInit = false;
template <int N> bgfx::VertexBufferHandle Grid<N>::vbh;
template <int N> bgfx::IndexBufferHandle Grid<N>::ibh;
template <int N> std::vector<PosTexcoordVertex> Grid<N>::vertices;
template <int N> std::vector<uint16_t> Grid<N>::indices;
template class Grid<16>;
template class Grid<1>;

std::atomic<size_t> sVBBytes;

void CubeList::Create(const std::vector<Vec3f>& pts, float cubeSize)
{

    static PosTexcoordNrmVertex s_cubeVertices[] =
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

    static const uint32_t s_cubeIndices[] =
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

    verticesSize = pts.size() * 24;
    pvertices = new PosTexcoordNrmVertex[verticesSize];
    PosTexcoordNrmVertex* pCurVtx = pvertices;
    indicesSize = pts.size() * 36;
    pindices = new uint32_t[indicesSize];
    uint32_t *pcurindex = pindices;
    size_t vtxoffset = 0;
    for (const Vec3f& pt : pts)
    {
        uint32_t offset = vtxoffset;
        for (uint32_t i : s_cubeIndices)
        {
            *(pcurindex++) = i + offset;
        }
        for (const PosTexcoordNrmVertex& cubevtx : s_cubeVertices)
        {
            PosTexcoordNrmVertex vtx = cubevtx;
            vtx.m_x = vtx.m_x * cubeSize + pt[0];
            vtx.m_y = vtx.m_y * cubeSize + pt[1];
            vtx.m_z = vtx.m_z * cubeSize + pt[2];
            vtxoffset++;
            (*pCurVtx++) = vtx;
        }
    }
}

void CubeList::Use()
{
    PosTexcoordNrmVertex::init();

    if (!vbh.isValid())
    {
        vbh = bgfx::createVertexBuffer(
            bgfx::makeRef(pvertices, verticesSize * sizeof(PosTexcoordNrmVertex), CubeList::ReleaseFn)
            , PosTexcoordNrmVertex::ms_layout
        );

        memsize += verticesSize * sizeof(PosTexcoordNrmVertex);
        pvertices = nullptr;

        ibh = bgfx::createIndexBuffer(
            bgfx::makeRef(pindices, indicesSize * sizeof(uint32_t), CubeList::ReleaseFn),
            BGFX_BUFFER_INDEX32
        );

        memsize += indicesSize * sizeof(uint32_t);
        pindices = nullptr;
        sVBBytes += memsize;
    }
}

CubeList::~CubeList()
{
    if (pvertices != nullptr)
        delete[]pvertices;
    if (pindices != nullptr)
        delete[]pindices;
    sVBBytes -= memsize;
}

void CubeList::ReleaseFn(void* ptr, void* pThis)
{
    delete[]ptr;

}

void VoxCube::Create(const std::vector<Vec3i>& pts)
{
    VoxelVertex::init();
    verticesSize = pts.size();
    pvertices = new VoxelVertex[verticesSize];
    VoxelVertex *pdata = pvertices;
    for (const Vec3i& pt : pts)
    {    
        VoxelVertex vtx = { (float)pt[0], (float)pt[1], (float)pt[2], 0 };
        *pdata++ = vtx;
    }
   
    memsize = verticesSize * sizeof(VoxelVertex);
    sVBBytes += memsize;
}


void VoxCube::Use()
{
    if (!vbh.isValid())
    {
        //vbh = bgfx::createynamicVertexBuffer(verticesSize, VoxelVertex::ms_layout, BGFX_BUFFER_COMPUTE_WRITE);
        vbh = bgfx::createVertexBuffer(
            bgfx::makeRef(pvertices, verticesSize * sizeof(VoxelVertex), VoxCube::ReleaseFn)
            , VoxelVertex::ms_layout
        );

    }           
}

VoxCube::~VoxCube()
{
    sVBBytes -= memsize;
}

void VoxCube::ReleaseFn(void* ptr, void* pThis)
{
    delete[]ptr;

}



