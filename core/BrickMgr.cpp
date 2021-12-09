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

    void DestroyBrickThreadPool(BrickThreadPool* ptr)
    {
        delete ptr;
    }

    void Brick::Load(ldr::Loader* pLoader, BrickThreadPool* threadPool, const std::string& name, std::filesystem::path& cachePath)
    {
        static bool sEnableDirectLoad = true;
        PosTexcoordNrmVertex::init();
        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("mesh");
        const bgfx::Memory* pVtx = nullptr;
        const bgfx::Memory* pIdx = nullptr;
        if (std::filesystem::exists(filepath))
        {
            std::ifstream ifs(filepath, std::ios_base::binary);
            uint32_t numvtx = 0;
            uint32_t numidx = 0;
            ifs.read((char*)&numvtx, sizeof(numvtx));
            if (numvtx == 0)
                return;
            pVtx = bgfx::alloc(sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)pVtx->data, sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)&numidx, sizeof(numidx));
            pIdx = bgfx::alloc(sizeof(uint32_t) * numidx);
            ifs.read((char*)pIdx->data, sizeof(uint32_t) * numidx);
            PosTexcoordNrmVertex* curVtx = (PosTexcoordNrmVertex*)pVtx->data;
            PosTexcoordNrmVertex* endVtx = curVtx + numvtx;
            for (; curVtx != endVtx; ++curVtx)
            {
                m_bounds += Point3f(curVtx->m_x, curVtx->m_y, curVtx->m_z);
            }
            Vec3f ext = m_bounds.mMax - m_bounds.mMin;
            m_scale = std::max(std::max(ext[0], ext[1]), ext[2]);
            m_center = (m_bounds.mMax + m_bounds.mMin) * 0.5f;
        }
        else
            return;
        m_vbh = bgfx::createVertexBuffer(pVtx, PosTexcoordNrmVertex::ms_layout);
        m_ibh = bgfx::createIndexBuffer(pIdx, BGFX_BUFFER_INDEX32);
    }

    void Brick::GenerateCacheItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
        const std::string& name, std::filesystem::path& cachePath)
    {
        LdrModelHDL model;
        LdrResult result = pLoader->createModel(name.c_str(), LDR_FALSE, &model);
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
                m_bounds += Point3f(curVtx->m_x, curVtx->m_y, curVtx->m_z);
                curVtx++;
            }
            memcpy(curIdx, rpart.triangles, rpart.num_triangles * 3 * sizeof(uint32_t));
            for (uint32_t idx = 0; idx < rpart.num_triangles * 3; ++idx, curIdx++)
                *curIdx = *curIdx + vtxOffset;
            vtxOffset += rpart.num_vertices;
        }

        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("mesh");
        std::ofstream ofs(filepath, std::ios_base::binary);
        ofs.write((const char*)&numvtx, sizeof(numvtx));
        ofs.write((const char*)vtx.data(), sizeof(PosTexcoordNrmVertex) * numvtx);
        ofs.write((const char*)&numidx, sizeof(numidx));
        ofs.write((const char*)idx.data(), sizeof(uint32_t) * numidx);
        ofs.close();
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
        createInfo.partFixMode = LDR_PART_FIX_ONLOAD;
        createInfo.renderpartBuildMode = LDR_RENDERPART_BUILD_ONLOAD;
        // required for chamfering
        createInfo.partFixTjunctions = LDR_TRUE;
        // optionally look for higher subdivided ldraw primitives
        createInfo.partHiResPrimitives = LDR_FALSE;
        // leave 0 to disable
        createInfo.renderpartChamfer = 0.35f;
        // installation path of the LDraw Part Library
        createInfo.basePath = ldrpath.c_str();
        m_ldrLoader->init(&createInfo);
        spMgr = this;
        m_cachePath = Application::Inst().Documents() + "/ldrcache";
        std::filesystem::create_directory(m_cachePath);
        LoadAllParts(ldrpath);
    }

    void BrickManager::LoadAllParts(const std::string& ldrpath)
    {
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
                    m_partsMap.insert(std::make_pair(pd.filename, pd));
                }
            }
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
                            b.GenerateCacheItem(m_ldrLoader.get(), m_threadPool.get(),
                                name, m_cachePath);

                            m_partsMap.insert(std::make_pair(pd.filename, pd));
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
        for (auto itPar = m_partsMap.begin(); itPar != m_partsMap.end(); ++itPar)
        {
            auto itType = m_typesMap.find(itPar->second.type);
            if (itType == m_typesMap.end())
                itType = m_typesMap.insert(std::make_pair(itPar->second.type,
                    std::vector<std::string>()));
            itType->second.push_back(itPar->first);
        }

        std::vector<std::string> misc;
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
    void BrickManager::Draw(DrawContext& ctx)
    {
        if (!bgfx::isValid(sShader))
            sShader = Engine::Inst().LoadShader("vs_cubes.bin", "fs_targetcube.bin");
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
    Brick* BrickManager::GetBrick(const std::string& name)
    {
        Brick& b = m_bricks[name];
        if (!b.m_vbh.isValid())
        {
            b.Load(m_ldrLoader.get(), m_threadPool.get(), name, m_cachePath);
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
            std::vector<std::pair<size_t, std::string>> mrulist;
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

    const std::string& BrickManager::PartName(size_t idx)
    {
        return m_partsMap[idx];
    }
}