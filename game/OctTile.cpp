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
        m_needsPersist(false),
        m_needsRefresh(false)
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

    
    bool OctTile::BackgroundLoad(World* pWorld)
    {
        if (m_readyState == 0)
        {
            m_readyState = 1;
            if (m_l.m_l == 8)
            {
                bool success = false;
                std::string strval;
                if (pWorld->Level()->GetOctChunk(ILevel::OctKey(m_l, 0), &strval))
                {
                    size_t parts = strval.size() / sizeof(PartInst);
                    m_parts.resize(parts);
                    memcpy(m_parts.data(), strval.data(), strval.size());
                    for (auto& part : m_parts)
                    {
                        m_bricks.push_back(BrickManager::Inst().GetBrick(part.id));
                    }
                    m_readyState = 3;
                    m_needsRefresh = true;
                }
            }            
            else if (m_l.m_l >= 5)
            {
                std::vector<Loc> level8Locs;
                m_l.GetChildrenAtLevel(8, level8Locs);
                std::string strval;
                std::vector<std::vector<PartInst>> partsVec;
                size_t totalCt = 0;
                Point3f parentCenter = m_l.GetCenter();
                for (auto& cl : level8Locs)
                {
                    if (pWorld->Level()->GetOctChunk(ILevel::OctKey(cl, 0), &strval))
                    {
                        partsVec.push_back(std::vector<PartInst>());
                        std::vector<PartInst>& parts = partsVec.back();
                        Point3f childCenter = cl.GetCenter();
                        size_t partsCt = strval.size() / sizeof(PartInst);
                        totalCt += partsCt;
                        parts.resize(partsCt);
                        memcpy(parts.data(), strval.data(), strval.size());
                        for (auto& part : parts)
                        {
                            part.pos = (part.pos + childCenter) - parentCenter;
                        }
                    }
                }
                if (totalCt > 0)
                {
                    m_parts.resize(totalCt);
                    PartInst *piPtr = m_parts.data();
                    for (std::vector<PartInst>& parts : partsVec)
                    {
                        memcpy(piPtr, parts.data(), parts.size() * sizeof(PartInst));
                        piPtr += parts.size();
                    }
                    for (auto& part : m_parts)
                    {
                        m_bricks.push_back(BrickManager::Inst().GetBrick(part.id));
                    }
                    m_readyState = 3;
                    m_needsRefresh = true;
                }
            }
        }
        if (m_readyState == 1)
        {        
            m_readyState = 2;
        }

        if (m_readyState == 2)
        {
            m_readyState = 3;
        }

        return m_readyState == 3;
    }

    void OctTile::Refresh()
    {

    }

    extern Loc g_hitLoc;
    bool OctTile::IsEmpty() const
    {
        return m_parts.size() == 0;
    }


    void OctTile::Draw(DrawContext& ctx)
    {
        SceneGroup::Draw(ctx);

        nOctTilesTotal++;
        nOctTilesDrawn++;
                   
        AABoxf box = m_l.GetBBox();

        if (!bgfx::isValid(m_uparams))
        {
            m_uparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);
        }

        if (m_needsRefresh)
        {
            SceneGroup::Decomission(ctx);
            Clear();
            for (auto& part : m_parts)
            {
                auto brick = std::make_shared<LegoBrick>(part, part.atlasidx, m_l.m_l == 8,
                    m_l.m_l == 8 ? (
                    part.connected ? LegoBrick::Physics::Static : LegoBrick::Physics::Dynamic) :
                    LegoBrick::Physics::None,
                    false);
                brick->SetOffset(part.pos);
                brick->SetRotate(part.rot);
                AddItem(brick);
            }
            m_needsRefresh = false;
        }
        
        if (ctx.debugDraw == 2)
        {
            if (!sBboxshader.isValid())
                sBboxshader = Engine::Inst().LoadShader("vs_cubes.bin", "fs_bbox.bin");
            Cube::init();
            AABoxf bbox = m_l.GetBBox();
            float scl = (bbox.mMax[0] - bbox.mMin[0]) * 0.45f;
            Point3f off = (bbox.mMax + bbox.mMin) * 0.5f;
            Matrix44f m = makeTrans<Matrix44f>(off) *
                makeScale<Matrix44f>(scl);
            bgfx::setTransform(m.getData());
            bool istarget = m_l == g_hitLoc;
            Vec4f color = istarget ? Vec4f(1.0f, 1.0f, 1.0f, 1.0f) : Vec4f(1.0f, 1.0f, 0.0f, 0.65f);
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
            bgfx::submit(DrawViewId::ForwardRendered, sBboxshader);
        }

        if (m_needsPersist && m_l.m_l == 8)
        {            
            Persist(ctx.m_pWorld);
        }
    }

    void OctTile::Persist(World *pWorld)
    {
        pWorld->Level()->WriteOctChunk(ILevel::OctKey(m_l, 0), (const char*)m_parts.data(), m_parts.size() *
            sizeof(PartInst));
        m_needsPersist = false;
    }
    void OctTile::Decomission(DrawContext& ctx)
    {
        SceneGroup::Decomission(ctx);
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

    constexpr float epsilon = 1e-5f;

    bool ge(const float& a, const float& b)
    { return (a - b > -epsilon); }

    bool intersectepsilon(const AABox<float>& box1, const AABox<float>& box2)
    {
        // Look for a separating axis on each box for each axis
        if (ge(box1.getMin()[0], box2.getMax()[0]))  return false;
        if (ge(box1.getMin()[1], box2.getMax()[1]))  return false;
        if (ge(box1.getMin()[2], box2.getMax()[2]))  return false;

        if (ge(box2.getMin()[0], box1.getMax()[0]))  return false;
        if (ge(box2.getMin()[1], box1.getMax()[1]))  return false;
        if (ge(box2.getMin()[2], box1.getMax()[2]))  return false;

        // No separating axis ... they must intersect
        return true;
    }

    void OctTile::GetInterectingParts(const Spheref& sphere, std::vector<PartInst>& piList)
    {
        auto itBrick = m_bricks.begin();
        auto itPart = m_parts.begin();
        for (; itPart != m_parts.end(); ++itPart, ++itBrick)
        {
            AABox cb = (*itBrick)->m_bounds;
            cb.mMin = cb.mMin * BrickManager::Scale + itPart->pos;
            cb.mMax = cb.mMax * BrickManager::Scale + itPart->pos;
            if (intersect(sphere, cb))
                piList.push_back(*itPart);
        }
    }

    bool OctTile::CanAddPart(const PartInst& pi, const AABoxf& bbox)
    {
        auto itBrick = m_bricks.begin();
        auto itPart = m_parts.begin();
        for (; itPart != m_parts.end(); ++itPart, ++itBrick)
        {
            AABox cb = (*itBrick)->m_collisionBox;
            cb.mMin = cb.mMin * BrickManager::Scale; 
            cb.mMax = cb.mMax * BrickManager::Scale; 
            cb = RotateAABox(cb, itPart->rot);
            cb.mMin += itPart->pos;
            cb.mMax += itPart->pos;
            if (intersectepsilon(bbox, cb))
                return false;
        }
        return true;
    }

    void OctTile::AddPartInst(const PartInst& pi)
    {
        m_parts.push_back(pi);
        m_bricks.push_back(BrickManager::Inst().GetBrick(pi.id));
        m_needsPersist = true;
        m_needsRefresh = true;
    }
    
    void OctTile::RemovePart(const PartInst& pi)
    {
        bool removed = false;
        auto itBrick = m_bricks.begin();
        for (auto itPart = m_parts.begin(); itPart != m_parts.end();
            )
        {
            if (itPart->id == pi.id && itPart->pos == pi.pos)
            {
                removed = true;
                itPart = m_parts.erase(itPart);
                itBrick = m_bricks.erase(itBrick);
            }
            else
            {
                ++itPart;
                ++itBrick;
            }
        }
        if (removed)
        {
            m_needsPersist = true;
            m_needsRefresh = true;
        }
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

}
