#include "StdIncludes.h"
#include "OctTileSelection.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "gmtl/PlaneOps.h"
#include <sstream>
#include "gmtl/Ray.h"
#define NOMINMAX
#ifdef _WIN32
#include <Windows.h>
#endif


using namespace gmtl;

namespace sam
{
    extern Loc g_hitLoc;
    void convexHull(Point2f points[], size_t n, std::vector<Point2f>& outpts);
    static bool doBreak = false;
    int g_numLod9 = 0;
    int g_behindViewer = 0;

    std::atomic<size_t> OctTileSelection::sNumTiles = 0;

    class FrustumTiles
    {

        inline static float nearFrust = 0.1f;
        inline static float farFrust = 150.0f;
    public:
        static void Get(Camera& cam, std::vector<Loc>& locs, float pixelDist, int maxlod, const AABoxf& playerBounds)
        {
            Frustumf viewFrust = cam.GetFrustum(nearFrust, farFrust);
            Matrix44f viewproj = cam.GetPerspectiveMatrix(nearFrust, farFrust) * cam.ViewMatrix();
            Vec3f ctr = FrustumCenter(viewproj);


            Vec3f chkpos(floorf(ctr[0]), floorf(ctr[1]), floorf(ctr[2]));
            auto& fly = cam.GetFly();
            Vec3f r, u, f;
            fly.GetDirs(r, u, f);
            g_behindViewer = 0;
            GetLocsInView(locs, Loc(0, 0, 0, 0), viewFrust, viewproj, fly.pos, r, f, pixelDist, maxlod, playerBounds, true);
            g_numLod9 = 0;
            for (auto& tile : locs)
            {
                g_numLod9 += tile.m_l == 9 ? 1 : 0;
            }
        }

    private:


        enum class ContainmentType
        {
            Disjoint = 0,
            Contains = 1,
            Intersects = 2
        };

        static Vec3f FrustumCenter(Matrix44f viewproj)
        {
            Matrix44f mInv;
            invert(viewproj);
            Vec4f inpt4;
            xform(inpt4, mInv, Vec4f(0, 0, 0.5f, 1.0f));
            inpt4 /= inpt4[3];
            return Vec3f(inpt4[0], inpt4[1], inpt4[2]);
        }


        template< class DATA_TYPE >
        DATA_TYPE static abs_distance(const Plane<DATA_TYPE>& plane, const Point<DATA_TYPE, 3>& pt)
        {
            return (dot(plane.mNorm, static_cast<Vec<DATA_TYPE, 3>>(pt)) + plane.mOffset);
        }


        static ContainmentType Contains(const Frustumf& f, AABoxf box)
        {
            ContainmentType result = ContainmentType::Contains;
            for (int i = 0; i < 6; i++)
            {
                Planef plane = f.mPlanes[i];

                // Approach: http://zach.in.tu-clausthal.de/teaching/cg_literatur/lighthouse3d_view_frustum_culling/index.html

                Point3f positive = Point3f(box.mMin[0], box.mMin[1], box.mMin[2]);
                Point3f negative = Point3f(box.mMax[0], box.mMax[1], box.mMax[2]);

                if (plane.mNorm[0] >= 0)
                {
                    positive[0] = box.mMax[0];
                    negative[0] = box.mMin[0];
                }
                if (plane.mNorm[1] >= 0)
                {
                    positive[1] = box.mMax[1];
                    negative[1] = box.mMin[1];
                }
                if (plane.mNorm[2] >= 0)
                {
                    positive[2] = box.mMax[2];
                    negative[2] = box.mMin[2];
                }

                // If the positive vertex is outside (behind plane), the box is disjoint.
                float positiveDistance = abs_distance(plane, positive);
                if (positiveDistance < 0)
                {
                    return ContainmentType::Disjoint;
                }

                // If the negative vertex is outside (behind plane), the box is intersecting.
                // Because the above check failed, the positive vertex is in front of the plane,
                // and the negative vertex is behind. Thus, the box is intersecting this plane.
                float negativeDistance = abs_distance(plane, negative);
                if (negativeDistance < 0)
                {
                    result = ContainmentType::Intersects;
                }
            }

            return result;
        }


        static inline void GetCorners(const AABoxf& box, Point3f pts[8])
        {
            const Point3f& l = box.mMin;
            const Point3f& u = box.mMax;
            pts[0] = Point3f(l[0], l[1], l[2]);
            pts[1] = Point3f(l[0], l[1], u[2]);
            pts[2] = Point3f(l[0], u[1], l[2]);
            pts[3] = Point3f(l[0], u[1], u[2]);
            pts[4] = Point3f(u[0], l[1], l[2]);
            pts[5] = Point3f(u[0], l[1], u[2]);
            pts[6] = Point3f(u[0], u[1], l[2]);
            pts[7] = Point3f(u[0], u[1], u[2]);
        }

    public:

        static int TargetLodForLoc(const Loc& curLoc, const Matrix44f& viewProj, const Vec3f &camFwd, const Point3f &camPos)
        {
            Point3f c[8];
            const AABoxf& bbox = curLoc.GetBBox();
            GetCorners(bbox, c);
            float extents = bbox.mMax[0] - bbox.mMin[0];
            float sq = extents / OctTile::SquarePtsCt;

            for (int i = 0; i < 8; ++i)
            {
                Point3f tp0(c[i][0], c[i][1], c[i][2]);
                float v = dot((tp0 - camPos), camFwd);
                if (v < 0)
                    return -1;
            }

            float maxlen = 0;
            for (int i = 0; i < 8; ++i)
            {
                Point4f tp0(c[i][0], c[i][1], c[i][2], 1), sp0, sp1;
                xform(sp0, viewProj, tp0);
                sp0 /= sp0[3];

                Point4f tp1(c[i][0] + sq, c[i][1] + sq, c[i][2] + sq, 1);
                xform(sp1, viewProj, tp1);
                sp1 /= sp1[3];

                
                maxlen = std::max(maxlen, lengthSquared(Vec2f(sp1[0] - sp0[0], sp1[1] - sp0[1])));
            }

            return std::max(log2(maxlen) + 9, 0.0f);
        }

        static bool GetLocsInView(std::vector<Loc>& locs, const Loc& curLoc,
            const Frustumf& frustum, const Matrix44f& viewProj, const Point3f& camPos, const Vec3f& camRight, const Vec3f& camFwd, float pixelDist, int maxlod, const AABoxf& playerBounds, bool behindCamera)
        {
            int targetLod = TargetLodForLoc(curLoc, viewProj, camFwd, camPos);            

            if (targetLod >= 0)
            {
                if (behindCamera && curLoc.m_l > targetLod)
                    return false;
                
                if (curLoc.m_l >= targetLod)
                {
                    locs.push_back(curLoc);
                    return true;
                }
            }
            if (targetLod < 0 && curLoc.m_l == maxlod)
            {
                locs.push_back(curLoc);
                return true;
            }

            bool hasChildren = false;
            if (curLoc.m_l < maxlod)
            {
                std::vector<Loc> fillLocs;
                fillLocs.reserve(8);

                std::vector<Loc> children = curLoc.GetChildren();
                for (const Loc& childLoc : children)
                {
                    AABoxf cbox = childLoc.GetBBox();
                    ContainmentType res = Contains(frustum, cbox);
                    bool intersectsPlayer = intersect(playerBounds, cbox);
                    if (res != ContainmentType::Disjoint || intersectsPlayer)
                    {
                        bool childExists = GetLocsInView(locs, childLoc, frustum, viewProj, camPos, camRight, camFwd, pixelDist, maxlod, playerBounds, targetLod < 0);
                        hasChildren |= childExists;
                        if (!childExists)
                            fillLocs.push_back(childLoc);
                    }
                }

                if (hasChildren)
                {
                    locs.insert(locs.end(), fillLocs.begin(), fillLocs.end());
                }
            }
            return hasChildren;
        }
    };


    OctTileSelection::OctTileSelection() :
        m_exit(false),
        m_loaderThread(LoaderThread, this)
    {
        m_nearfarmid[0] = 0.1f;
        m_nearfarmid[1] = 25.0f;
        m_nearfarmid[2] = 100.0f;
    }


    void OctTileSelection::LoaderThread(void* arg)
    {
        OctTileSelection* pThis = (OctTileSelection*)arg;
        std::unique_lock<std::mutex> lk(pThis->m_mtxcv);
        while (!pThis->m_exit)
        {
            pThis->m_cv.wait(lk);
            while (pThis->m_loaderTiles.size() > 0)
            {
                std::shared_ptr<OctTile> tile;
                {
                    std::lock_guard(pThis->m_mtx);
                    if (pThis->m_loaderTiles.size() > 0)
                    {
                        tile = pThis->m_loaderTiles.back();
                        pThis->m_loaderTiles.pop_back();
                    }
                }
                if (tile != nullptr && tile->GetReadyState() < 3)
                    tile->BackgroundLoad(pThis->m_pWorld);
            }
        }
    }


    void OctTileSelection::GetLocDistance(const Loc& loc, const Point3f& campos, const Vec3f& camdir,
        float& neardist, float& middist, float& fardist)
    {
        AABoxf box = loc.GetBBox();
        const Point3f& m0 = box.mMin;
        const Point3f& m1 = box.mMax;
        Point3f pts[8] = {
            Point3f(m0[0], m0[1], m0[2]),
            Point3f(m0[0], m0[1], m1[2]),
            Point3f(m0[0], m1[1], m0[2]),
            Point3f(m0[0], m1[1], m1[2]),
            Point3f(m1[0], m0[1], m0[2]),
            Point3f(m1[0], m0[1], m1[2]),
            Point3f(m1[0], m1[1], m0[2]),
            Point3f(m1[0], m1[1], m1[2])
        };
        float minlen = std::numeric_limits<float>::max();
        float maxlen = -std::numeric_limits<float>::max();
        for (int i = 0; i < 8; ++i)
        {
            float l = dot(camdir, Vec3f(pts[i] - campos));
            minlen = std::min(minlen, l);
            maxlen = std::max(maxlen, l);
        }
        neardist = minlen;
        fardist = maxlen;
        middist = (minlen + maxlen) * 0.5f;
    }

    extern int g_maxTileLod;
    void OctTileSelection::Update(Engine& e, DrawContext& ctx, const AABoxf& playerBounds)
    {
        auto oldTiles = m_activeTiles;
        m_activeTiles.clear();
        auto& cam = e.ViewCam();
        Camera::Fly fly = cam.GetFly();
        m_pWorld = ctx.m_pWorld;
        std::vector<Loc> locs;
        FrustumTiles::Get(cam, locs, 10.0f, g_maxTileLod, playerBounds);

        std::sort(locs.begin(), locs.end());
        std::vector<std::shared_ptr<OctTile>> loaderTiles;
        for (const auto& l : locs)
        {
            auto itSq = m_tiles.find(l);
            if (itSq == m_tiles.end())
            {
                std::shared_ptr<OctTile> sq = std::make_shared<OctTile>(l);
                { // Init OctTile
                    float sx = l.m_x;
                    float sy = l.m_z;

                    Point3f pos = l.GetCenter();
                    sq->SetOffset(pos);
                }
                itSq = m_tiles.insert(std::make_pair(l, sq)).first;
                sNumTiles++;
            }
            if (itSq->second->GetReadyState() < 3)
                loaderTiles.push_back(itSq->second);
            m_activeTiles.insert(l);
        }

        {
            std::lock_guard grd(m_mtx);
            std::swap(m_loaderTiles, loaderTiles);
        }
        m_cv.notify_one();

        for (auto loc : oldTiles)
        {
            if (m_activeTiles.find(loc) == m_activeTiles.end())
            {
                auto itTile = m_tiles.find(loc);
                itTile->second->Decomission(ctx);
                m_tiles.erase(itTile);
                sNumTiles--;
            }
        }

        Vec3f l, u, f;
        fly.GetDirs(l, u, f);
        for (auto loc : m_activeTiles)
        {
            auto itSq = m_tiles.find(loc);

            GetLocDistance(loc, fly.pos, f,
                itSq->second->m_nearDist,
                itSq->second->distFromCam,
                itSq->second->m_farDist);
        }
    }

    int g_nearTiles;
    int g_farTiles;

    void OctTileSelection::AddTilesToGroup(std::shared_ptr<SceneGroup> grp)
    {
        std::vector<std::shared_ptr<OctTile>> tiles;
        for (auto sqPair : m_activeTiles)
        {
            auto itSq = m_tiles.find(sqPair);
            if (!itSq->second->IsEmpty())
                tiles.push_back(itSq->second);
        }

        if (tiles.size() == 0)
            return;

        std::sort(tiles.begin(), tiles.end(), [](auto& t1, auto& t2) { return t1->distFromCam > t2->distFromCam;  });


        float splitdist = tiles[tiles.size() / 2]->distFromCam;
        float neardist = std::numeric_limits<float>::max();
        float fardist = 0;
        for (auto& t : tiles)
        {
            neardist = std::min(neardist, t->m_nearDist);
            fardist = std::max(fardist, t->m_farDist);
        }

        neardist = std::max(neardist, 0.01f);
        fardist = std::max(fardist, neardist * 2);
        float logsplitdist = (log2(neardist) + log2(fardist)) * 0.5f;
        splitdist = pow(2, logsplitdist);

        m_nearfarmid[0] = 0.1f;// neardist;
        m_nearfarmid[1] = 6.0f;// splitdist;
        m_nearfarmid[2] = 180.0f;// fardist;
        int nearCt = 0;
        int farCt = 0;
        for (auto& t : tiles)
        {
            if (t->m_nearDist < splitdist)
                nearCt++;
            if (t->m_farDist > splitdist)
                farCt++;
        }
        g_nearTiles = nearCt;
        g_farTiles = farCt;

        for (auto& t : tiles)
        {
            grp->AddItem(t);
        }

    }

    void OctTileSelection::GetNearFarMidDist(float nearfarmid[3])
    {
        for (int i = 0; i < 3; ++i)
        {
            nearfarmid[i] = m_nearfarmid[i];
        }
    }

    float OctTileSelection::GetGroundHeight(const Point3f& pt)
    {
        int tx = (int)floor(pt[0]);
        int tz = (int)floor(pt[2]);
        Loc queryLoc(tx, 0, tz);

        static float headHeight = 0.04f;
        auto itCamTile = m_tiles.find(queryLoc);
        if (itCamTile != m_tiles.end())
        {
            return 0;// itCamTile->second->GetGroundHeight(pt) + headHeight;
        }
    }


    std::shared_ptr<OctTile> OctTileSelection::TileFromPos(const Point3f& pos)
    {
        for (auto& pair : m_tiles)
        {
            if (intersect(pair.first.GetBBox(), pos))
            {
                return pair.second;
            }
        }

        return nullptr;
    }

    struct IntersectTile
    {
        float dist;
        float h1;
        float h2;
        Loc l;
        std::shared_ptr<OctTile> tile;
    };

    bool OctTileSelection::Intersects(const Point3f& pos, const Vec3f& ray, Loc& outloc, Vec3i& opt)
    {
        std::vector<IntersectTile> orderedTiles;
        Ray r(pos, ray);
        for (auto& pair : m_tiles)
        {
            unsigned int num_hits;
            float h1, h2;
            AABoxf aabb = pair.first.GetBBox();
            gmtl::Vec3f midpt = (aabb.mMin + aabb.mMax) * 0.5f;

            bool res = intersect(aabb, r, num_hits, h1, h2);
            if (res)
            {
                IntersectTile tile = {
                    lengthSquared(midpt),
                    num_hits > 1 ? h1 : 0,
                    num_hits > 1 ? h2 : h1 ,
                    pair.first,
                    pair.second };
                orderedTiles.push_back(tile);
            }
        }

        std::sort(orderedTiles.begin(), orderedTiles.end(), [](const auto& l, const auto& r)
            {
                return l.dist < r.dist;
            });

        size_t idx = 1;
        for (auto& it : orderedTiles)
        {
            Vec3i outpt;
            idx++;
        }

        return false;
    }


    OctTileSelection::~OctTileSelection()
    {
        m_exit = true;
        m_cv.notify_one();
        m_loaderThread.join();
    }


}