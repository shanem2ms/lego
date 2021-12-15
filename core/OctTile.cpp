#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "gmtl/Intersection.h"
#include "BrickMgr.h"
#include "LegoBrick.h"

#define NOMINMAX

namespace sam
{

    bgfxh<bgfx::ProgramHandle> sBboxshader;
    bgfxh<bgfx::ProgramHandle> sBrickShader;
    static bgfx::UniformHandle sPaletteHandle(BGFX_INVALID_HANDLE);

    OctTile::OctTile(const Loc& l) : m_image(-1), m_l(l),
        m_buildFrame(0),
        m_readyState(0),
        m_intersects(-1),
        m_lastUsedRawData(0),
        m_isdecommissioned(false),
        m_pBrick(nullptr)
    {
    }


    inline void AABoxAdd(AABoxf& aab, const Point3f& pt)
    {
        if (aab.isEmpty())
        {
            aab.setEmpty(false);
            aab.setMax(pt);
            aab.setMin(pt);
        }
        else
        {
            const Point3f& min = aab.getMin();
            aab.setMin(Point3f(pt[0] < min[0] ? pt[0] : min[0],
                pt[1] < min[1] ? pt[1] : min[1],
                pt[2] < min[2] ? pt[2] : min[2]));

            const Point3f& max = aab.getMax();
            aab.setMax(Point3f(pt[0] > max[0] ? pt[0] : max[0],
                pt[1] > max[1] ? pt[1] : max[1],
                pt[2] > max[2] ? pt[2] : max[2]));
        }
    }

    AABoxf OctTile::GetBounds() const
    {
        const int padding = 2;
        Matrix44f m = CalcMat() *
            makeScale<Matrix44f>(Vec3f(
                (float)(1.0f / 2 - padding), (float)(1.0f / 2 - padding), 0));

        Point3f pts[4] = { Point3f(-1, -1, 0),
            Point3f(1, -1, 0) ,
            Point3f(1, 1, 0) ,
                Point3f(-1, -1, 0) };

        AABoxf aab;
        for (int idx = 0; idx < 4; ++idx)
        {
            Point3f p1;
            xform(p1, m, Point3f(-1, -1, 0));
            AABoxAdd(aab, p1);
        }
        return aab;
    }


    int nOctTilesTotal;
    int nOctTilesDrawn;

    void OctTile::LoadVB()
    {
        AABoxf bboxoct = m_l.GetBBox();
        float minY = bboxoct.mMin[1];
        float maxY = bboxoct.mMax[1];
        float minX = bboxoct.mMin[0];
        float minZ = bboxoct.mMin[2];
        float len = (bboxoct.mMax[0] - bboxoct.mMin[0]) / OctTile::SquarePtsCt;


        const int tsz = OctTile::SquarePtsCt;
        std::vector<Vec3i> octPts;

        size_t offset = 0;

    }

    
    void OctTile::BackgroundLoad(World* pWorld)
    {
        if (m_readyState == 0)
        {
            m_readyState = 1;
            bool success = false;
            std::string strval;
            if (pWorld->Level().GetOctChunk(m_l, &strval))
            {
                m_readyState = 3;
            }

        }
        if (m_readyState == 1)
        {
            if (m_l.m_l == 8 && m_l.IsGroundLoc())
            {
                PartInst pi;
                pi.id = "91405";
                pi.paletteIdx = 0;
                pi.pos = Vec3f(0, -0.5f, 0);
                pi.rot = Quatf();
                m_parts.push_back(pi);
            }
            m_readyState = 2;
        }

        if (m_readyState == 2)
        {
            auto brick = std::make_shared<LegoBrick>("91405", 0);
            brick->SetOffset(Vec3f(0, -0.5f, 0));
            brick->SetScale(Vec3f(2, 2, 2));
            AddItem(brick);
            //m_pBrick = BrickManager::Inst().GetBrick("91405");
            m_readyState = 3;
        }
    }

    extern Loc g_hitLoc;
    bool g_showOctBoxes = false;
    bool OctTile::IsEmpty() const
    {
        return false;
    }
    void OctTile::Draw(DrawContext& ctx)
    {
        SceneGroup::Draw(ctx);

        if (!m_l.IsGroundLoc() || m_l.m_l != 8)
            return;
        nOctTilesTotal++;
        nOctTilesDrawn++;
        
        if (ctx.m_curviewIdx > 1)
            return;
            
        if (!bgfx::isValid(m_uparams))
        {
            m_uparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
        }

        if (m_readyState == 3)
        {
            m_readyState++;
        }

        if (m_pBrick == nullptr)
            return;
        BrickManager::Inst().MruUpdate(m_pBrick);

        if (g_showOctBoxes)
        {
            if (!sBboxshader.isValid())
                sBboxshader = Engine::Inst().LoadShader("vs_brick.bin", "fs_bbox.bin");
            Cube::init();
            AABoxf bbox = m_l.GetBBox();
            float scl = (bbox.mMax[0] - bbox.mMin[0]) * 0.45f;
            Point3f off = (bbox.mMax + bbox.mMin) * 0.5f;
            Matrix44f m = makeTrans<Matrix44f>(off) *
                makeScale<Matrix44f>(scl);
            bgfx::setTransform(m.getData());
            bool istarget = m_l == g_hitLoc;
            Vec4f color = istarget ? Vec4f(1.0f, 1.0f, 1.0f, 1.0f) : Vec4f(1.0f, 1.0f, 0.0f, 0.25f);
            bgfx::setUniform(m_uparams, &color, 1);
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | (istarget ? 0 :
                    (BGFX_STATE_WRITE_A
                    | BGFX_STATE_WRITE_Z
                    | BGFX_STATE_DEPTH_TEST_LESS ))
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;
            // Set render states.l
            bgfx::setState(state);
            bgfx::setVertexBuffer(0, Cube::vbh);
            bgfx::setIndexBuffer(Cube::ibh);
            bgfx::submit(ctx.m_curviewIdx, sBboxshader);
        }
        else if (false)
        {
            if (!sBrickShader.isValid())
                sBrickShader = Engine::Inst().LoadShader("vs_brick.bin", "fs_cubes.bin");
            if (!bgfx::isValid(sPaletteHandle))
                sPaletteHandle = bgfx::createUniform("s_brickPalette", bgfx::UniformType::Sampler);

            AABoxf bbox = m_l.GetBBox();
            float scl = (bbox.mMax[0] - bbox.mMin[0]) * BrickManager::Scale;
            Point3f off = (bbox.mMax + bbox.mMin) * 0.5f;
            off[1] = bbox.mMin[1];
            Matrix44f m = makeTrans<Matrix44f>(off) *
                makeRot<Matrix44f>(AxisAnglef(Math::PI, 1.0f, 0.0f, 0.0f)) *
                makeScale<Matrix44f>(scl);
            bgfx::setTransform(m.getData());

            bool istarget = m_l == g_hitLoc;
            Vec4f color = BrickManager::Color(0x8A928D);
            bgfx::setTexture(0, sPaletteHandle, BrickManager::Inst().Palette());
            bgfx::setUniform(m_uparams, &color, 1);
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_CULL_CCW
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;
            // Set render states.l
            bgfx::setState(state);
            bgfx::setVertexBuffer(0, m_pBrick->m_vbh);
            bgfx::setIndexBuffer(m_pBrick->m_ibh);
            bgfx::submit(ctx.m_curviewIdx, sBrickShader);
        }
    }

    void OctTile::Decomission(DrawContext& ctx)
    {
        m_readyState = 0;
        m_isdecommissioned = true;
    }

    bool OctTile::IsCollided(Point3f& oldpos, Point3f& newpos, AABoxf& playerbox, Vec3f& outNormal)
    {
        AABoxf aabb = m_l.GetBBox();
        Vec3f extents = aabb.mMax - aabb.mMin;
        float scale = OctTile::SquarePtsCt / extents[0];
        Point3f off = aabb.mMin;
        Point3f pmin = (playerbox.mMin - off + newpos) * scale;
        Point3f pmax = (playerbox.mMax - off + newpos) * scale;
        return false;

    }

    OctTile::~OctTile()
    {
    }


#define checkhit(x, y, z) if (data[y * tsz * tsz + z * tsz + x] > 0) return Vec3i(x, y, z);
#define R(x) std::max(0, std::min(OctTile::SquarePtsCt - 1, x))

    Vec3i OctTile::FindHit(const std::vector<byte>& data, const Vec3i pt1, const Vec3i pt2)
    {
        const int tsz = OctTile::SquarePtsCt;

        int x1 = R(pt1[0]), y1 = R(pt1[1]), z1 = R(pt1[2]);
        int x2 = R(pt2[0]), y2 = R(pt2[1]), z2 = R(pt2[2]);

        checkhit(x1, y1, z1);

        int dx = abs(x2 - x1);
        int dy = abs(y2 - y1);
        int dz = abs(z2 - z1);
        int xs, ys, zs;
        if (x2 > x1)
            xs = 1;
        else
            xs = -1;

        if (y2 > y1)
            ys = 1;
        else
            ys = -1;
        if (z2 > z1)
            zs = 1;
        else
            zs = -1;

        // Driving axis is X - axis"
        if (dx >= dy && dx >= dz)
        {
            int p1 = 2 * dy - dx;
            int p2 = 2 * dz - dx;
            while (x1 != x2)
            {
                x1 += xs;
                if (p1 >= 0)
                {
                    y1 += ys;
                    p1 -= 2 * dx;
                }
                if (p2 >= 0)
                {
                    z1 += zs;
                    p2 -= 2 * dx;
                }
                p1 += 2 * dy;
                p2 += 2 * dz;
                checkhit(x1, y1, z1);
            }
        }
        // Driving axis is Y - axis"
        else if (dy >= dx && dy >= dz)
        {
            int p1 = 2 * dx - dy;
            int p2 = 2 * dz - dy;
            while (y1 != y2) {
                y1 += ys;
                if (p1 >= 0)
                {
                    x1 += xs;
                    p1 -= 2 * dy;
                }
                if (p2 >= 0) {
                    z1 += zs;
                    p2 -= 2 * dy;
                }
                p1 += 2 * dx;
                p2 += 2 * dz;
                checkhit(x1, y1, z1);
            }
        }

        // Driving axis is Z - axis"
        else
        {
            int p1 = 2 * dy - dz;
            int p2 = 2 * dx - dz;
            while (z1 != z2) {
                z1 += zs;
                if (p1 >= 0) {
                    y1 += ys;
                    p1 -= 2 * dz;
                }
                if (p2 >= 0) {
                    x1 += xs;
                    p2 -= 2 * dz;
                }
                p1 += 2 * dy;
                p2 += 2 * dx;
                checkhit(x1, y1, z1);
            }
        }
        return Vec3i(-1, -1, -1);
    }

    void TargetCube::Initialize(DrawContext& nvg)
    {
        m_shader = Engine::Inst().LoadShader("vs_brick.bin", "fs_targetcube.bin");
    }

    void TargetCube::Draw(DrawContext& ctx)
    {
        if (ctx.m_curviewIdx != 2)
            return;
        Cube::init();
        Matrix44f m = ctx.m_mat * CalcMat();
        bgfx::setTransform(m.getData());
        // Set vertex and index buffer.
        bgfx::setVertexBuffer(0, Cube::vbh);
        bgfx::setIndexBuffer(Cube::ibh);
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_WRITE_Z
            | BGFX_STATE_MSAA
            | BGFX_STATE_BLEND_ALPHA;
        // Set render states.l
        bgfx::setState(state);
        bgfx::submit(ctx.m_curviewIdx, m_shader);
    }

}
