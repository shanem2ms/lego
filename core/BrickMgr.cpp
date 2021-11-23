#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "BrickMgr.h"
#include "ldrawloader.hpp"
#include <queue>
#include <sstream>
#include <fstream>
#include <filesystem>


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
                    while (!terminate_pool)
                    {
                        std::shared_ptr<Task> task;
                        {
                            std::unique_lock<std::mutex> lock(task_mutex);
                            cv.wait(lock);

                            task = tasks.front();
                            tasks.pop();
                        }
                        if (task->partIds.size() > 0)
                        {
                            LdrResult r = pLoader->loadDeferredParts(task->partIds.size(), task->partIds.data(), sizeof(LdrPartID));
                            if (r < LDR_SUCCESS)
                                result = r;
                        }
                        taskCtr--;
                    }}
                );
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
        }
    };

    void DestroyBrickThreadPool(BrickThreadPool* ptr)
    {
        delete ptr;
    }
    void BrickManager::Brick::Load(ldr::Loader* pLoader, BrickThreadPool *threadPool, const std::string& name, std::filesystem::path &cachePath)
    {
        if (name == "33080.dat")
            __debugbreak();
        PosTexcoordNrmVertex::init();
        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("mesh");
        const bgfx::Memory* pVtx;
        const bgfx::Memory* pIdx;
        if (std::filesystem::exists(filepath))
        {
            std::ifstream ifs(filepath, std::ios_base::binary);
            uint32_t numvtx = 0;
            uint32_t numidx = 0;
            ifs.read((char*)&numvtx, sizeof(numvtx));
            pVtx = bgfx::alloc(sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)pVtx->data, sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)&numidx, sizeof(numidx));
            pIdx = bgfx::alloc(sizeof(uint32_t) * numidx);
            ifs.read((char*)pIdx->data, sizeof(uint32_t) * numidx);
        }
        else
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

            pVtx = bgfx::alloc(sizeof(PosTexcoordNrmVertex) * numvtx);
            pIdx = bgfx::alloc(sizeof(uint32_t) * numidx);

            PosTexcoordNrmVertex* curVtx = (PosTexcoordNrmVertex*)pVtx->data;
            uint32_t* curIdx = (uint32_t*)pIdx->data;
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
            ofs.write((const char*)pVtx->data, sizeof(PosTexcoordNrmVertex) * numvtx);
            ofs.write((const char*)&numidx, sizeof(numidx));
            ofs.write((const char*)pIdx->data, sizeof(uint32_t) * numidx);
            ofs.close();
        }
        m_vbh = bgfx::createVertexBuffer(pVtx, PosTexcoordNrmVertex::ms_layout);
        m_ibh = bgfx::createIndexBuffer(pIdx, BGFX_BUFFER_INDEX32);
    }

    static BrickManager* spMgr = nullptr;
    BrickManager::BrickManager(const std::string& ldrpath) :
        m_ldrLoader(std::make_shared<ldr::Loader>())
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

        std::string partnamefile = Application::Inst().Documents() + "/partnames.txt";
        if (std::filesystem::exists(partnamefile))
        {
            std::ifstream ifs(partnamefile);
            while (!ifs.eof())
            {
                std::string str;
                std::getline(ifs, str);
                std::filesystem::path filepath = m_cachePath / str;
                filepath.replace_extension("mesh");
                if (!std::filesystem::exists(filepath))
                {
                    m_partnames.push_back(str);
                }
            }
        }
        else
        {
            std::ofstream of(partnamefile);
            std::filesystem::path partspath(ldrpath);
            partspath /= "parts";
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
                        m_partnames.push_back(name);
                        of << name << std::endl;
                    }
                }
            }
            of.close();
        }

        m_threadPool = std::make_unique<BrickThreadPool>(
            ldrpath, m_ldrLoader.get());
//#define AUTOLOAD 1
#ifdef AUTOLOAD
        
        for (const auto& entry : std::filesystem::directory_iterator(partspath))
        {
            std::string ext = entry.path().extension().string();
            if (ext == ".dat")
            {
                LdrModelHDL model;
                std::string name = entry.path().filename().string();
                {
                    std::ifstream ifs(entry);
                    std::string line;
                    std::getline(ifs, line);
                    if (line._Starts_with("0 ~Moved to"))
                        continue;
                }

                LdrResult result = m_ldrLoader->createModel(name.c_str(), LDR_FALSE, &model);

                if (result == LDR_SUCCESS || result == LDR_WARNING_PART_NOT_FOUND)
                {

                    uint32_t numParts = m_ldrLoader->getNumRegisteredParts();
                    uint32_t perThread = (numParts + numThreads - 1) / numThreads;

                    std::vector<LdrPartID> partIds(numParts);
                    std::vector<LdrResult> partResults(numParts);
                    taskCtr = 0;
                    int numThreadsDispatched = 0;
                    for (uint32_t i = 0; i < numThreads; i++)
                    {
                        std::shared_ptr<Task> t = std::make_shared<Task>();
                        uint32_t offset = i * perThread;
                        uint32_t numLocal = offset > numParts ? 0 : std::min(perThread, numParts - i * perThread);
                        if (numLocal == 0)
                            break;
                        for (int p = 0; p < numLocal; p++)
                            t->partIds.push_back(offset + p);
                        std::unique_lock<std::mutex> lock(task_mutex);
                        tasks.push(t);
                        cv.notify_one();
                        numThreadsDispatched++;
                    }
                    while (taskCtr < numThreadsDispatched)
                        _sleep(1);
                    // must do manual resolve after parts are loaded
                    m_ldrLoader->resolveModel(model);
                }



                if (result >= 0)
                    m_ldrLoader->destroyModel(model);
                loaded++;
                if (result < LDR_SUCCESS)
                    failed++;
            }
            if (loaded % 100 == 0)
            {
                std::stringstream ss;
                ss << loaded << "parts";
                Application::DebugMsg(ss.str());
            }
        }
#endif
    }

    Vec4f BrickManager::Color(uint32_t hex)
    {
        float r = (hex & 0xFF) / 255.0f;
        float g = ((hex >> 8) & 0xFF) / 255.0f;
        float b = ((hex >> 16) & 0xFF) / 255.0f;
        return Vec4f(r, g, b, 1);
    }

    BrickManager& BrickManager::Inst() { return *spMgr; }
    const BrickManager::Brick& BrickManager::GetBrick(const std::string& name)
    {
        Brick& b = m_bricks[name];
        if (!b.m_vbh.isValid())
        {
            b.Load(m_ldrLoader.get(), m_threadPool.get(), name, m_cachePath);
        }
        return b;
    }

    const std::string& BrickManager::PartName(size_t idx)
    {
        return m_partnames[idx];
    }
}