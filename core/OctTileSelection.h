#pragma once
#include <map>
#include <set>
#include "OctTile.h"
#include <thread>
#include <condition_variable>

class SimplexNoise;
namespace sam
{

    struct DrawContext;
    class Engine;
    class Touch;
    class OctTileSelection
    {
    public:

        static std::atomic<size_t> sNumTiles;

        std::map<Loc, std::shared_ptr<OctTile>> m_tiles;
        std::set<Loc> m_activeTiles;
        std::vector<std::shared_ptr<OctTile>> m_loaderTiles;

        std::thread m_loaderThread;
        std::mutex m_mtx;
        bool m_exit;
        std::mutex m_mtxcv;
        std::condition_variable m_cv;
        World *m_pWorld;

        static void LoaderThread(void* arg);


        void Update(Engine& e, DrawContext& ctx, const AABoxf &playerBounds);

        void AddTilesToGroup(std::shared_ptr<SceneGroup> grp);
        float GetGroundHeight(const Point3f& pt);
        void GetNearFarMidDist(float nearfarmid[3]);

        std::shared_ptr<OctTile> TileFromPos(const Point3f& pos);
        bool Intersects(const Point3f& pos, const Vec3f& ray, Loc& outloc, Vec3i& outpt);
        static void GetLocDistance(const Loc& loc, const Point3f& campos, const Vec3f& camdir,
            float& neardir, float& middir, float& fardir);

        bool CanAddPart(const PartInst& pi, const AABoxf& bbox);
        void AddPartInst(const PartInst& pi);
        void RemovePart(const PartInst& pi);
        void GetInterectingParts(const Spheref& sphere, std::vector<PartInst>& piList);

        void AddMultipleParts(World* pWorld, const std::vector<PartInst>& piList);

    public:
        OctTileSelection();
        ~OctTileSelection();
        float m_nearfarmid[3];
    };
 

}
