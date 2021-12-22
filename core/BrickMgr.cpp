#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "BrickMgr.h"
#include "ldrawloader.hpp"
#include <bx/bx.h>
#include <queue>
#include <sstream>
#include <fstream>
#include <filesystem>
#include <regex>
#include <fmt/format.h>
#include <VHACD.h>
#include "Simplify.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"


namespace sam
{
    struct Task
    {
        std::vector<LdrPartID> partIds;
        LdrResult result;
    };

    class BrickThreadPool
    {
        std::queue<std::shared_ptr<Task>> tasks;
        std::mutex task_mutex;
        std::condition_variable cv;
        std::vector<std::thread> threads;
        std::atomic<int> taskCtr = 0;
        bool terminate_pool = false;
        ldr::Loader* pLoader;
        LdrResult result;

    public:
        BrickThreadPool(const std::string& ldrpath,
            ldr::Loader* pL) :
            pLoader(pL),
            result(LDR_SUCCESS)
        {
            std::filesystem::path partspath(ldrpath);
            partspath /= "parts";
            int loaded = 0;
            int failed = 0;
            uint32_t numThreads = std::thread::hardware_concurrency();
            threads.resize(numThreads);

            for (uint32_t i = 0; i < numThreads; i++)
            {
                threads[i] = std::thread([this]() {
                    while (true)
                    {
                        std::shared_ptr<Task> task;
                        {
                            std::unique_lock<std::mutex> lock(task_mutex);
                            cv.wait(lock);
                            if (terminate_pool)
                                break;
                            if (tasks.size() == 0)
                                continue;
                            task = tasks.front();
                            tasks.pop();
                        }
                        if (task->partIds.size() > 0)
                        {
                            LdrResult r = DoTask(task);
                            if (r < LDR_SUCCESS)
                                result = r;
                        }
                        taskCtr--;
                    }}
                );
            }
        }

        LdrResult DoTask(const std::shared_ptr<Task>& task)
        {
            __try
            {
                LdrResult r = pLoader->loadDeferredParts(task->partIds.size(), task->partIds.data(), sizeof(LdrPartID));
                return r;
            }
            __except (true)
            {
                return LDR_ERROR_DEPENDENT_OPERATION;
            }
        }

        size_t NumThreads() const {
            return threads.size();
        }

        LdrResult GetResult() {
            LdrResult r = result; result = LDR_SUCCESS;
            return r;
        }
        void AddTask(const std::shared_ptr<Task>& t)
        {
            std::unique_lock<std::mutex> lock(task_mutex);
            tasks.push(t);
            taskCtr++;
            cv.notify_one();
        }

        void Flush()
        {
            while (taskCtr > 0)
                _sleep(1);
        }

        ~BrickThreadPool()
        {
            terminate_pool = true;
            cv.notify_all();
            for (auto& thread : threads)
            {
                thread.join();
            }
        }
    };


    PartId::PartId(const std::string& partfile)
    {
        memset(_id, 0, sizeof(_id));
        size_t idx = partfile.find('.');
        if (idx == std::string::npos)
            idx = partfile.size();
        memcpy(_id, partfile.c_str(), std::min(sizeof(_id), idx));
    }

    std::string PartId::GetFilename() const
    {
        std::string name;
        if (_id[sizeof(_id) - 1] != 0)
        {
            name.assign(_id, 8);
        }
        else
            name.assign(_id);

        name += ".dat";
        return name;
    }
    std::string PartId::Name() const
    {
        std::string name;
        if (_id[sizeof(_id) - 1] != 0)
        {
            name.assign(_id, 8);
        }
        else
            name.assign(_id);
        return name;
    }

    void DestroyBrickThreadPool(BrickThreadPool* ptr)
    {
        delete ptr;
    }

    void Brick::Load(ldr::Loader* pLoader, const std::string& name, std::filesystem::path& cachePath)
    {
        m_name = name;
        m_connectorsLoaded = false;
        static bool sEnableDirectLoad = true;
        PosTexcoordNrmVertex::init();
        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("mesh");
        if (std::filesystem::exists(filepath))
        {
            std::ifstream ifs(filepath, std::ios_base::binary);
            uint32_t numvtx = 0;
            uint32_t numidx = 0;
            ifs.read((char*)&numvtx, sizeof(numvtx));
            if (numvtx == 0)
                return;
            m_vertices.resize(numvtx);
            ifs.read((char*)m_vertices.data(), sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)&numidx, sizeof(numidx));
            m_indices.resize(numidx);
            ifs.read((char*)m_indices.data(), sizeof(uint32_t) * numidx);

            PosTexcoordNrmVertex* curVtx = m_vertices.data();
            PosTexcoordNrmVertex* endVtx = curVtx + numvtx;
            for (; curVtx != endVtx; ++curVtx)
            {
                curVtx->m_y = -curVtx->m_y;
                m_bounds += Point3f(curVtx->m_x, curVtx->m_y, curVtx->m_z);
            }
            Vec3f ext = m_bounds.mMax - m_bounds.mMin;
            m_scale = std::max(std::max(ext[0], ext[1]), ext[2]);
            m_center = (m_bounds.mMax + m_bounds.mMin) * 0.5f;
        }
        else
            return;

        //LoadConnectors(pLoader, name);
        m_vbh = bgfx::createVertexBuffer(bgfx::makeRef(m_vertices.data(), m_vertices.size() * sizeof(PosTexcoordNrmVertex)), PosTexcoordNrmVertex::ms_layout);
        m_ibh = bgfx::createIndexBuffer(bgfx::makeRef(m_indices.data(), m_indices.size() * sizeof(uint32_t)), BGFX_BUFFER_INDEX32);
    }

    static ldr::Loader* spLoader = nullptr;

    void Brick::LoadCollisionMesh()
    {
        if (m_collisionShape != nullptr)
            return;
#ifdef LOADMODEL
        LdrModelHDL model;
        std::vector<ldr::LdrConnection> connections;        
        spLoader->loadConnections(m_name.c_str(), connections);
        LdrResult result = spLoader->createModel(m_name.c_str(), LDR_TRUE, &model);

        LdrRenderModelHDL rmodel;
        result = spLoader->createRenderModel(model, LDR_TRUE, &rmodel);
        //PosTexcoordNrmVertex 
               // access the model and part details directly
        uint32_t numvtx = 0;
        uint32_t numidx = 0;
        for (uint32_t i = 0; i < rmodel->num_instances; i++) {
            const LdrInstance& instance = model->instances[i];
            const LdrRenderPart& rpart = spLoader->getRenderPart(instance.part);
            numvtx += rpart.num_vertices;
            numidx += rpart.num_triangles * 3;
        }

        std::vector<Vec3f> vtx;
        vtx.resize(numvtx);
        std::vector<uint32_t> idxs;
        idxs.resize(numidx);

        Vec3f* curVtx = (Vec3f*)vtx.data();
        uint32_t* curIdx = (uint32_t*)idxs.data();
        uint32_t vtxOffset = 0;
        for (uint32_t i = 0; i < rmodel->num_instances; i++) {
            const LdrInstance& instance = model->instances[i];
            const LdrRenderPart& rpart = spLoader->getRenderPart(instance.part);
            for (uint32_t idx = 0; idx < rpart.num_vertices; ++idx)
            {
                memcpy(curVtx, &rpart.vertices[idx].position, sizeof(LdrVector));
                curVtx++;
            }
            memcpy(curIdx, rpart.triangles, rpart.num_triangles * 3 * sizeof(uint32_t));
            for (uint32_t idx = 0; idx < rpart.num_triangles * 3; ++idx, curIdx++)
                *curIdx = *curIdx + vtxOffset;
            vtxOffset += rpart.num_vertices;
        }
#else
        std::vector<Vec3f> vtx;
        std::vector<uint32_t>& idxs = m_indices;
        float ss = 0.1f;
  
        for (auto& v : m_vertices)
        {
            vtx.push_back(Vec3f(v.m_x *ss, v.m_y *ss, v.m_z*ss));
        }
#endif

//#define HACD 1
#ifdef HACD
        Simplify::vertices.clear();
        Simplify::triangles.clear();
        for (auto& v : vtx)
        {
            Simplify::Vertex p;
            p.p.x = v[0];
            p.p.y = v[1];
            p.p.z = v[2];
            Simplify::vertices.push_back(p);
        }

        for (int i = 0; i < idxs.size(); i += 3)
        {
            Simplify::Triangle t;
            t.v[0] = m_indices[i+2];
            t.v[1] = m_indices[i + 1];
            t.v[2] = m_indices[i];
            Simplify::triangles.push_back(t);
        }

        Simplify::simplify_mesh(100,8);

        std::vector<Vec3f> pts(Simplify::vertices.size());
        VHACD::IVHACD* pVHACD = VHACD::CreateVHACD();
        auto it1 = Simplify::vertices.begin();
        auto it2 = pts.begin();
        for (; it1 != Simplify::vertices.end(); ++it1, ++it2)
        {
            it2->mData[0] = it1->p.x;
            it2->mData[1] = it1->p.y;
            it2->mData[2] = it1->p.z;
        }
        std::vector<uint32_t> ind;
        ind.reserve(Simplify::triangles.size() * 3);
        for (auto& t : Simplify::triangles)
        {
            ind.push_back(t.v[0]);
            ind.push_back(t.v[1]);
            ind.push_back(t.v[2]);
        }

        VHACD::IVHACD::Parameters p;
        p.m_oclAcceleration = false;
        pVHACD->Compute((const float*)pts.data(), pts.size(), ind.data(), ind.size() / 3,
            p);


        pVHACD->Release();
#endif

        const uint32_t* pI = (const uint32_t*)m_indices.data();
        const PosTexcoordNrmVertex* pV = m_vertices.data();
        btTriangleMesh* pMesh = new btTriangleMesh();
        constexpr float s = 1.0f;// BrickManager::Scale;
        for (uint32_t i = 0; i < m_indices.size(); i += 3) {
            if (pI[i] == (uint32_t)(-1) ||
                pI[i + 1] == (uint32_t)(-1) ||
                pI[i + 2] == (uint32_t)(-1))
                continue;
            auto& v0 = pV[pI[i]];
            auto& v1 = pV[pI[i + 1]];
            auto& v2 = pV[pI[i + 2]];
            pMesh->addTriangle(btVector3(v0.m_x * s, v0.m_y * s, v0.m_z * s),
                btVector3(v1.m_x * s, v1.m_y * s, v1.m_z * s),
                btVector3(v2.m_x * s, v2.m_y * s, v2.m_z * s));
        }
        m_collisionShape = std::make_shared<btBvhTriangleMeshShape>(pMesh, true, true);
    }

    void Brick::GenerateCacheItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
        const std::string& name, std::filesystem::path& filepath,
        const std::vector<int> atlasMaterialMapping)
    {
        LdrModelHDL model;
        LdrResult result;
        if (threadPool != nullptr)
        {
            result = pLoader->createModel(name.c_str(), LDR_FALSE, &model);
            if (result != LDR_SUCCESS)
                return;
            uint32_t numParts = pLoader->getNumRegisteredParts();
            uint32_t numThreads = threadPool->NumThreads();
            uint32_t perThread = (numParts + numThreads - 1) / numThreads;

            std::vector<LdrPartID> partIds(numParts);
            std::vector<LdrResult> partResults(numParts);
            for (uint32_t i = 0; i < numThreads; i++)
            {
                std::shared_ptr<Task> t = std::make_shared<Task>();
                uint32_t offset = i * perThread;
                uint32_t numLocal = offset > numParts ? 0 : std::min(perThread, numParts - i * perThread);
                if (numLocal == 0)
                    break;
                for (int p = 0; p < numLocal; p++)
                    t->partIds.push_back(offset + p);
                threadPool->AddTask(t);
            }

            threadPool->Flush();
            LdrResult buildResult = threadPool->GetResult();
            if (buildResult < LDR_SUCCESS)
                return;
            // must do manual resolve after parts are loaded
            pLoader->resolveModel(model);
        }
        else
        {
            std::vector<ldr::LdrConnection> connections;
            pLoader->loadConnections(name.c_str(), connections);
            result = pLoader->createModel(name.c_str(), LDR_TRUE, &model);
        }

        LdrRenderModelHDL rmodel;
        result = pLoader->createRenderModel(model, LDR_TRUE, &rmodel);

        if (rmodel->num_instances == 0)
            return;

        //PosTexcoordNrmVertex 
        // access the model and part details directly
        uint32_t numvtx = 0;
        uint32_t numidx = 0;
        for (uint32_t i = 0; i < rmodel->num_instances; i++) {
            const LdrInstance& instance = model->instances[i];
            const LdrRenderPart& rpart = pLoader->getRenderPart(instance.part);
            numvtx += rpart.num_vertices;
            numidx += rpart.num_triangles * 3;
        }

        std::vector<PosTexcoordNrmVertex> vtx;
        vtx.resize(numvtx);
        std::vector<uint32_t> idx;
        idx.resize(numidx);

        PosTexcoordNrmVertex* curVtx = (PosTexcoordNrmVertex*)vtx.data();
        uint32_t* curIdx = (uint32_t*)idx.data();
        uint32_t vtxOffset = 0;
        for (uint32_t i = 0; i < rmodel->num_instances; i++) {
            const LdrInstance& instance = model->instances[i];
            const LdrRenderPart& rpart = pLoader->getRenderPart(instance.part);
            for (uint32_t idx = 0; idx < rpart.num_vertices; ++idx)
            {
                memcpy(&curVtx->m_x, &rpart.vertices[idx].position, sizeof(LdrVector));
                memcpy(&curVtx->m_nx, &rpart.vertices[idx].normal, sizeof(LdrVector));
                curVtx->m_u = curVtx->m_v = -1;
                m_bounds += Point3f(curVtx->m_x, curVtx->m_y, curVtx->m_z);
                curVtx++;
            }
            memcpy(curIdx, rpart.triangles, rpart.num_triangles * 3 * sizeof(uint32_t));
            for (uint32_t idx = 0; idx < rpart.num_triangles * 3; ++idx, curIdx++)
                *curIdx = *curIdx + vtxOffset;
            if (rpart.materials != nullptr)
            {
                PosTexcoordNrmVertex* pvtx = (PosTexcoordNrmVertex*)vtx.data();
                LdrMaterialID* curMat = rpart.materials;
                LdrVertexIndex* pVtxIdx = rpart.triangles;
                for (uint32_t idx = 0; idx < rpart.num_triangles * 3; ++idx, pVtxIdx++)
                {

                    if (*pVtxIdx != LDR_INVALID_ID)
                        pvtx[*pVtxIdx + vtxOffset].m_u =
                        (*curMat != -1) ? atlasMaterialMapping[*curMat] : -1;
                    if ((idx % 3) == 2)
                        curMat++;
                }
            }
            vtxOffset += rpart.num_vertices;
        }

        std::ofstream ofs(filepath, std::ios_base::binary);
        ofs.write((const char*)&numvtx, sizeof(numvtx));
        ofs.write((const char*)vtx.data(), sizeof(PosTexcoordNrmVertex) * numvtx);
        ofs.write((const char*)&numidx, sizeof(numidx));
        ofs.write((const char*)idx.data(), sizeof(uint32_t) * numidx);
        ofs.close();
    }


    struct ConnectorInfo
    {
        ConnectorType type;
        std::vector<Vec3f> offsets;
    };

    static ConnectorInfo connectorInfo[] =
    {
        { ConnectorType::Unknown, { Vec3f(0,0,0) }},
        { ConnectorType::Stud, { Vec3f(0,0,0) }},
        { ConnectorType::InvStud, { Vec3f(0,-4,0), Vec3f(10,-4,10), Vec3f(-10,-4,10), Vec3f(10,-4,-10), Vec3f(-10,-4,-10) }},
        { ConnectorType::InvStud, { Vec3f(0,0,0) }},
        { ConnectorType::InvStud, { Vec3f(0,0,0) }},
        { ConnectorType::InvStud, { Vec3f(0,0,0) }},
        { ConnectorType::InvStud, { Vec3f(10,-4,0), Vec3f(-10,-4,0) }},
        { ConnectorType::InvStud, { Vec3f(0,0,0)}},
        { ConnectorType::InvStud, { Vec3f(0,0,0)}},
    };

    std::map<std::string, int> sConnectorMap =
    { { "stud.dat", 1 },
        { "stud4.dat", 2 },
        { "connect.dat", 3},
        { "stud2a.dat", 4},
        { "stud2.dat", 5},
        { "stud3.dat", 6},
        { "stud6.dat", 7},
        { "1-4ring3.dat", 8}
    };

    void Brick::LoadPrimitives(ldr::Loader* pLoader)
    {
        std::vector<ldr::LdrConnection> connections;
        pLoader->loadPrimitives(m_name.c_str());
    }
    void Brick::LoadConnectors(ldr::Loader* pLoader)
    {
        if (m_connectorsLoaded)
            return;
        std::vector<ldr::LdrConnection> connections;
        pLoader->loadConnections(m_name.c_str(), connections);
        for (auto& connection : connections)
        {
            Connector c;
            ConnectorInfo& ci = connectorInfo[connection.type];
            c.type = ci.type;

            Matrix44f m;
            memcpy(m.mData, connection.transform.values, sizeof(float) * 16);
            m.mState = Matrix44f::AFFINE;
            Vec4f p;
            xform(p, m, Vec4f(0, 0, 0, 1));
            c.pos = Vec3f(p[0], -p[1], p[2]);
            float* v = m.mData;
            c.scl = Vec3f(length(Vec3f(v[0], v[4], v[8])),
                length(Vec3f(v[1], v[5], v[9])),
                length(Vec3f(v[2], v[6], v[10])));
            Quatf q = gmtl::make<Quatf>(m);
            c.dir = q;

            for (auto& offset : ci.offsets)
            {
                Connector c2 = c;
                c2.pos += offset * c.scl;
                m_connectors.push_back(c2);
            }
        }
        std::sort(m_connectors.begin(), m_connectors.end());
        auto itunique = std::unique(m_connectors.begin(), m_connectors.end());
        m_connectors.erase(itunique, m_connectors.end());
        if (m_connectors.size() > 0)
        {
            std::vector<Vec3f> pts;
            for (auto& c : m_connectors)
            {
                c.pickIdx = pts.size();
                pts.push_back(c.pos);
            }
            m_connectorCL = std::make_shared<CubeList>();
            m_connectorCL->Create(pts, 5);
        }
        m_connectorsLoaded = true;
    }

    constexpr int iconW = 256;
    constexpr int iconH = 256;
    static BrickManager* spMgr = nullptr;
    BrickManager::BrickManager(const std::string& ldrpath) :
        m_ldrLoader(std::make_shared<ldr::Loader>()),
        m_mruCtr(0)
    {
        // initialize library
        LdrLoaderCreateInfo  createInfo = {};
        // while parts are not directly fixed, we will implicitly create a fixed version
        // for renderparts
        createInfo.partFixMode = LDR_PART_FIX_NONE;
        createInfo.renderpartBuildMode = LDR_RENDERPART_BUILD_ONLOAD;
        // required for chamfering
        createInfo.partFixTjunctions = LDR_FALSE;
        // optionally look for higher subdivided ldraw primitives
        createInfo.partHiResPrimitives = LDR_FALSE;
        // leave 0 to disable
        createInfo.renderpartChamfer = 0.0f;
        // installation path of the LDraw Part Library
        createInfo.basePath = ldrpath.c_str();
        m_ldrLoader->init(&createInfo);
        spLoader = m_ldrLoader.get();
        spMgr = this;
        m_cachePath = Application::Inst().Documents() + "/ldrcache";
        std::filesystem::create_directory(m_cachePath);
        LoadColors(ldrpath);
        LoadAllParts(ldrpath);
    }
    // trim from start (in place)
    static inline void ltrim(std::string& s) {
        s.erase(s.begin(), std::find_if(s.begin(), s.end(), [](unsigned char ch) {
            return !std::isspace(ch);
            }));
    }
    void BrickManager::LoadConnectors(Brick* pBrick)
    {
        pBrick->LoadConnectors(m_ldrLoader.get());
    }

    void BrickManager::LoadPrimitives(Brick* pBrick)
    {
        pBrick->LoadPrimitives(m_ldrLoader.get());
    }
    void BrickManager::LoadColors(const std::string& ldrpath)
    {
        std::filesystem::path configpath =
            std::filesystem::path(ldrpath) / "LDConfig.ldr";
        std::ifstream ifs(configpath);
        std::regex colorrg("0\\s!COLOUR\\s(\\w+)\\s+CODE\\s+(\\d+)\\s+VALUE\\s#([\\dA-F]+)\\s+EDGE\\s+#([\\dA-F]+)");
        while (!ifs.eof())
        {
            std::string line;
            std::getline(ifs, line);
            std::smatch match;
            if (std::regex_search(line, match, colorrg))
            {
                std::string name = match[1];
                int index = std::stoi(match[2].str());
                unsigned int fill = std::stoul(match[3].str(), nullptr, 16);
                unsigned int edge = std::stoul(match[4].str(), nullptr, 16);
                BrickColor bc{ index, name,
                    {
                    (fill >> 16) & 0xFF,
                    (fill >> 8) & 0xFF,
                    fill & 0xFF,
                    0xFF},
                    { (edge >> 16) & 0xFF,
                    (edge >> 8) & 0xFF,
                    edge & 0xFF,
                    0xFF}
                };
                m_colors.insert(std::make_pair(index, bc));
            }
        }

        const bgfx::Memory* m = bgfx::alloc(16 * 16 * sizeof(RGBA));
        RGBA* pgrbx = (RGBA*)m->data;
        int atlasidx = 0;
        for (auto& col : m_colors)
        {
            col.second.atlasidx = atlasidx++;
            pgrbx[col.second.atlasidx] = col.second.fill;
        }
        m_colorPalette = bgfx::createTexture2D(16, 16, false, 1, bgfx::TextureFormat::RGBA8, BGFX_SAMPLER_POINT, m);
    }

    void BrickManager::LoadAllParts(const std::string& ldrpath)
    {
        auto itcol = m_colors.end();
        std::advance(itcol, -1);
        std::vector<int> codeIdx(itcol->second.code + 1, -1);
        for (auto& col : m_colors)
        {
            codeIdx[col.second.code] = col.second.atlasidx;
        }

        std::string partnamefile = Application::Inst().Documents() + "/partnames.txt";
        if (std::filesystem::exists(partnamefile))
        {
            std::ifstream ifs(partnamefile);
            while (!ifs.eof())
            {
                std::string filename, ptype;
                std::getline(ifs, filename, ' ');
                std::getline(ifs, ptype, ' ');
                std::string dv[4];
                for (int i = 0; i < 4; ++i)
                {
                    std::getline(ifs, dv[i], ' ');
                }
                std::string desc;
                std::getline(ifs, desc);


                std::filesystem::path filepath = m_cachePath / filename;
                filepath.replace_extension("mesh");
                char* p_end;
                int val = std::strtol(filename.data(), &p_end, 10);
                if (std::filesystem::exists(filepath))
                {
                    PartDesc pd;
                    pd.index = val;
                    pd.type = ptype;
                    pd.filename = filename;
                    pd.ndims = std::stoi(dv[0]);
                    for (int i = 0; i < 3; ++i)
                        pd.dims[i] = std::stof(dv[i + 1]);
                    pd.desc = desc;
                    ltrim(pd.desc);
                    m_partsMap.insert(std::make_pair(pd.filename, pd));
                }
            }

            m_partsMap.sort();
        }
        else
        {
            m_threadPool = std::make_unique<BrickThreadPool>(
                ldrpath, m_ldrLoader.get());

            std::string tmpfile = Application::Inst().Documents() + "/pn.tmp";
            std::ofstream of(tmpfile);
            std::filesystem::path partspath(ldrpath);
            partspath /= "parts";

            std::regex partsizes("[\\d\\.]+(\\s+x\\s+([\\d\\.]+))+");
            std::regex partnames("0\\s[~=]?(\\w+)");
            std::regex dimen("[\\d\\.]+");

            for (const auto& entry : std::filesystem::directory_iterator(partspath))
            {
                std::string ext = entry.path().extension().string();
                if (ext == ".dat")
                {
                    std::string name = entry.path().filename().string();
                    {
                        std::ifstream ifs(entry);
                        std::string line;
                        std::getline(ifs, line);
                        if (line._Starts_with("0 ~Moved to"))
                            continue;
                        try
                        {
                            PartDesc pd;
                            pd.filename = name;
                            pd.ndims = 0;

                            std::smatch match;
                            std::string matchtest = line;
                            if (std::regex_search(matchtest, match, partsizes))
                            {
                                std::string nextmatch = match.prefix().str();
                                std::string sizestr = match[0].str();
                                pd.desc = match.suffix().str();

                                std::vector<float> dims;
                                while (std::regex_search(sizestr, match, dimen))
                                {
                                    std::string dstr = match[0].str();
                                    dims.push_back(std::stof(dstr));
                                    sizestr = match.suffix().str();
                                }

                                pd.ndims = dims.size();
                                for (int i = 0; i < dims.size(); ++i)
                                {
                                    pd.dims[i] = dims[i];
                                }

                                matchtest = nextmatch;
                            }

                            if (std::regex_search(matchtest, match, partnames))
                            {
                                pd.type = match[1].str();
                                if (pd.desc.empty())
                                    pd.desc = match.suffix().str();
                            }

                            int val = std::stoi(name);
                            if (match[2].matched)
                            {
                                for (int i = 0; i < 3; ++i)
                                {
                                    if (match[4 + i].matched)
                                    {
                                        pd.ndims++;
                                        pd.dims[i] = std::stoi(match[4 + i].str());
                                    }
                                }
                            }
                            of << pd.filename << " " << pd.type << " " << pd.ndims << " ";
                            for (int i = 0; i < 3; ++i)
                            {
                                of << pd.dims[i] << " ";
                            }
                            of << pd.desc << std::endl;

                            Brick b;
                            std::filesystem::path meshfilepath = m_cachePath / name;
                            meshfilepath.replace_extension("mesh");
                            if (!std::filesystem::exists(meshfilepath))
                            {
                                b.GenerateCacheItem(m_ldrLoader.get(), m_threadPool.get(),
                                    name, meshfilepath, codeIdx);
                            }

                            m_partsMap.insert(std::make_pair(pd.filename, pd));
                            Application::DebugMsg(pd.filename + "\n");
                        }
                        catch (...)
                        {
                        }
                    }
                }
            }
            of.close();
            std::filesystem::rename(tmpfile, partnamefile);
        }
        const auto& keys = m_partsMap.keys();
        for (const auto& key : keys)
        {
            const PartDesc& desc = m_partsMap[key];
            auto itType = m_typesMap.find(desc.type);
            if (itType == m_typesMap.end())
                itType = m_typesMap.insert(std::make_pair(desc.type,
                    std::vector<PartId>()));
            itType->second.push_back(key);
        }

        std::vector<PartId> misc;
        for (auto itType = m_typesMap.begin(); itType != m_typesMap.end(); )
        {
            if (itType->second.size() < 64)
            {
                misc.insert(misc.end(), itType->second.begin(), itType->second.end());
                itType = m_typesMap.erase(itType);
            }
            else
                ++itType;
        }

        m_typesMap.insert(std::make_pair("misc", misc));
    }

    static bgfx::ProgramHandle sShader(BGFX_INVALID_HANDLE);
    static bgfxh<bgfx::UniformHandle> sUparams;

    void BrickManager::Draw(DrawContext& ctx)
    {
        if (!bgfx::isValid(sShader))
            sShader = Engine::Inst().LoadShader("vs_brick.bin", "fs_targetcube.bin");
        if (!sUparams.isValid())
            sUparams = bgfx::createUniform("u_params", bgfx::UniformType::Vec4, 1);

        if (!m_paletteHandle.isValid())
            m_paletteHandle = bgfx::createUniform("s_brickPalette", bgfx::UniformType::Sampler);
        if (!m_iconDepth.isValid())
        {
            m_iconDepth =
                bgfx::createTexture2D(
                    iconW
                    , iconH
                    , false
                    , 1
                    , bgfx::TextureFormat::D32
                    , BGFX_TEXTURE_RT | BGFX_SAMPLER_COMPARE_LEQUAL
                );
        }

        Vec4f color = Vec4f(16, 0, 0, 0);
        PosTexcoordNrmVertex::init();
        std::vector<Brick*> brickRenderQueue;
        std::swap(brickRenderQueue, m_brickRenderQueue);
        for (auto& brick : brickRenderQueue)
        {
            if (!brick->m_vbh.isValid())
                continue;
            brick->m_icon =
                bgfx::createTexture2D(
                    iconW
                    , iconH
                    , false
                    , 1
                    , bgfx::TextureFormat::RGBA32F
                    , BGFX_TEXTURE_RT
                );
            bgfx::TextureHandle fbtextures[] = {
                brick->m_icon,
                m_iconDepth
            };
            bgfxh<bgfx::FrameBufferHandle> iconFB = bgfx::createFrameBuffer(BX_COUNTOF(fbtextures), fbtextures);

            int viewId = Engine::Inst().GetNextView();
            bgfx::setViewName(viewId, "brickicons");
            bgfx::setViewFrameBuffer(viewId, iconFB);
            Matrix44f view, proj;
            identity(view);
            identity(proj);
            bgfx::setUniform(sUparams, &color, 1);
            bgfx::setViewRect(viewId, 0, 0, bgfx::BackbufferRatio::Equal);
            bgfx::setViewTransform(viewId, view.getData(), proj.getData());
            bgfx::setViewClear(viewId,
                BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
                0x000000ff,
                1.0f,
                0
            );

            Matrix44f m;
            m = makeScale<Matrix44f>(Vec3f(1, -1, 0.25)) *
                makeTrans<Matrix44f>(Vec3f(0, 0, 0.75f)) *
                makeRot<Matrix44f>(AxisAnglef(gmtl::Math::PI, Vec3f(1, 0, 0))) *
                makeRot<Matrix44f>(AxisAnglef(gmtl::Math::PI_OVER_4, Vec3f(1, 0, 0))) *
                makeRot<Matrix44f>(AxisAnglef(gmtl::Math::PI_OVER_4, Vec3f(0, 1, 0))) *
                makeScale<Matrix44f>(1.0f / brick->m_scale) *
                makeTrans<Matrix44f>(-brick->m_center);
            bgfx::setTransform(m.getData());
            uint64_t state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_WRITE_Z
                | BGFX_STATE_DEPTH_TEST_LESS
                | BGFX_STATE_MSAA
                | BGFX_STATE_BLEND_ALPHA;
            // Set render states.l

            Vec4f color = Vec4f(1.0f, 1.0f, 0.0f, 1.0f);

            bgfx::setTexture(0, m_paletteHandle, m_colorPalette);
            bgfx::setState(state);
            bgfx::setVertexBuffer(0, brick->m_vbh);
            bgfx::setIndexBuffer(brick->m_ibh);
            bgfx::submit(viewId, sShader);


        }
    }

    Vec4f BrickManager::Color(uint32_t hex)
    {
        float r = (hex & 0xFF) / 255.0f;
        float g = ((hex >> 8) & 0xFF) / 255.0f;
        float b = ((hex >> 16) & 0xFF) / 255.0f;
        return Vec4f(r, g, b, 1);
    }

    BrickManager& BrickManager::Inst() { return *spMgr; }
    Brick* BrickManager::GetBrick(const PartId& name)
    {
        Brick& b = m_bricks[name];
        if (!b.m_vbh.isValid())
        {
            b.Load(m_ldrLoader.get(), name.GetFilename(), m_cachePath);
            m_brickRenderQueue.push_back(&b);
        }
        MruUpdate(&b);
        CleanCache();
        return &b;
    }

    void BrickManager::MruUpdate(Brick* pBrick)
    {
        pBrick->m_mruCtr = m_mruCtr++;
    }

    void BrickManager::CleanCache()
    {
        if (m_bricks.size() > 512)
        {
            std::vector<std::pair<size_t, PartId>> mrulist;
            mrulist.reserve(m_bricks.size());
            for (auto& pair : m_bricks)
            {
                mrulist.push_back(std::make_pair(pair.second.m_mruCtr,
                    pair.first));
            }

            std::sort(mrulist.begin(), mrulist.end());
            for (size_t idx = 0; idx < 64; ++idx)
            {
                m_bricks.erase(mrulist[idx].second);
            }
        }
    }

    const PartId& BrickManager::GetPartId(size_t idx)
    {
        return m_partsMap.keys()[idx];
    }

    std::string BrickManager::PartDescription(const std::string& partname)
    {
        PartDesc& desc = m_partsMap[partname];
        if (desc.ndims == 1)
            return fmt::format("{} {} {}\n{}", desc.type, desc.dims[0], desc.desc, desc.filename);
        if (desc.ndims == 2)
            return fmt::format("{} {}x{} {}\n{}", desc.type, desc.dims[0], desc.dims[1],
                desc.desc, desc.filename);
        if (desc.ndims == 3)
            return fmt::format("{} {}x{}x{} {}\n{}", desc.type, desc.dims[0], desc.dims[1],
                desc.dims[2], desc.desc, desc.filename);
        else
            return fmt::format("{} {}\n{}", desc.type, desc.desc, desc.filename);
    }
}