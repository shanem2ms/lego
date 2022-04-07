#include "pch.h"
#include "ldrawloader.hpp"
#include <queue>
#include <sstream>
#include <fstream>
#include <filesystem>
#include <regex>
#include <map>
#include <memory>

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
};


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
            Sleep(1);
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

#define tricount (hires ? rpart.num_trianglesC : rpart.num_triangles)
#define tris (hires ? rpart.trianglesC : rpart.triangles)

void GetLdrItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
    const std::string& name, std::filesystem::path& filepath,
    const std::vector<int> atlasMaterialMapping, bool hires,
    std::vector<unsigned char> &data)
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
            uint32_t numLocal = offset > numParts ? 0 : min(perThread, numParts - i * perThread);
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
        numidx += tricount * 3;
    }

    std::vector<PosTexcoordNrmVertex> vtx;
    vtx.resize(numvtx);
    std::vector<uint32_t> idx;
    idx.resize(numidx);

    float bounds[6] = { 1e10,1e10,1e10,-1e10,-1e10,-11e10 };
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
            bounds[0] = min(curVtx->m_x, bounds[0]);
            bounds[1] = min(curVtx->m_y, bounds[1]);
            bounds[2] = min(curVtx->m_z, bounds[2]);
            bounds[3] = max(curVtx->m_x, bounds[3]);
            bounds[4] = max(curVtx->m_y, bounds[4]);
            bounds[5] = max(curVtx->m_z, bounds[5]);
            curVtx++;
        }
        memcpy(curIdx, tris, tricount * 3 * sizeof(uint32_t));
        for (uint32_t idx = 0; idx < tricount * 3; ++idx, curIdx++)
            *curIdx = *curIdx + vtxOffset;
        if (rpart.materials != nullptr)
        {
            PosTexcoordNrmVertex* pvtx = (PosTexcoordNrmVertex*)vtx.data();
            LdrMaterialID* curMat = rpart.materials;
            LdrVertexIndex* pVtxIdx = tris;
            for (uint32_t idx = 0; idx < tricount * 3; ++idx, pVtxIdx++)
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

    data.insert(data.end(), (const char*)&numvtx, (const char*)&numvtx + sizeof(numvtx));
    data.insert(data.end(), (const char*)vtx.data(), (const char*)vtx.data() + sizeof(PosTexcoordNrmVertex) * numvtx);
    data.insert(data.end(), (const char*)&numidx, (const char*)&numidx + sizeof(numidx));
    data.insert(data.end(), (const char*)idx.data(), (const char*)idx.data() + sizeof(uint32_t) * numidx);
}

static std::vector<unsigned char> resultData;
static std::shared_ptr<BrickThreadPool> threadPool;
static std::shared_ptr<ldr::Loader> ldrLoaderHR;

extern "C" __declspec(dllexport) void LdrLoadFile(const char* basepath, const char* name, float *matptr)
{
    if (ldrLoaderHR == nullptr)
    {
        ldrLoaderHR = std::make_shared<ldr::Loader>();
        // initialize library
        LdrLoaderCreateInfo  createInfo = {};
        // while parts are not directly fixed, we will implicitly create a fixed version
        // for renderparts
        createInfo.partFixMode = LDR_PART_FIX_NONE;
        createInfo.renderpartBuildMode = LDR_RENDERPART_BUILD_ONLOAD;
        // required for chamfering
        createInfo.partFixTjunctions = LDR_TRUE;
        // optionally look for higher subdivided ldraw primitives
        createInfo.partHiResPrimitives = LDR_TRUE;
        // leave 0 to disable
        createInfo.renderpartChamfer = 0.35f;
        // installation path of the LDraw Part Library
        createInfo.basePath = basepath;
        ldrLoaderHR->init(&createInfo);
    }
    /*
    createInfo.partHiResPrimitives = LDR_FALSE;
    createInfo.partFixTjunctions = LDR_FALSE;
    createInfo.renderpartChamfer = 0.2f;
    ldrLoaderLR->init(&createInfo);
    */
    if (threadPool == nullptr)
        threadPool = std::make_shared<BrickThreadPool>(basepath, ldrLoaderHR.get());
    std::vector<int> materialMaps;
    std::filesystem::path path(basepath);
    resultData.clear();
    GetLdrItem(ldrLoaderHR.get(), threadPool.get(),
        name, path, materialMaps, true, resultData);
}

extern "C" __declspec(dllexport) void *LdrGetResultPtr()
{
    return resultData.data();
}

extern "C" __declspec(dllexport) int LdrGetResultSize()
{
    return resultData.size();
}