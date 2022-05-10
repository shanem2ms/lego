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
struct RGBA
{
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
};
struct BrickColor
{
    int code;
    std::string name;
    RGBA fill;
    RGBA edge;
    int atlasidx;
    int legoId;
    std::string legoName;
};

static std::map<int, BrickColor> sColors;
static std::vector<int> sAtlasMapping;

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

inline LdrMatrix mat_mul(const LdrMatrix& a, const LdrMatrix& b)
{
    LdrMatrix out;
    out.values[0] = a.values[0] * b.values[0] + a.values[4] * b.values[1] + a.values[8] * b.values[2];
    out.values[1] = a.values[1] * b.values[0] + a.values[5] * b.values[1] + a.values[9] * b.values[2];
    out.values[2] = a.values[2] * b.values[0] + a.values[6] * b.values[1] + a.values[10] * b.values[2];
    out.values[3] = 0;

    out.values[4] = a.values[0] * b.values[4] + a.values[4] * b.values[5] + a.values[8] * b.values[6];
    out.values[5] = a.values[1] * b.values[4] + a.values[5] * b.values[5] + a.values[9] * b.values[6];
    out.values[6] = a.values[2] * b.values[4] + a.values[6] * b.values[5] + a.values[10] * b.values[6];
    out.values[7] = 0;

    out.values[8] = a.values[0] * b.values[8] + a.values[4] * b.values[9] + a.values[8] * b.values[10];
    out.values[9] = a.values[1] * b.values[8] + a.values[5] * b.values[9] + a.values[9] * b.values[10];
    out.values[10] = a.values[2] * b.values[8] + a.values[6] * b.values[9] + a.values[10] * b.values[10];
    out.values[11] = 0;

    out.values[12] = a.values[0] * b.values[12] + a.values[4] * b.values[13] + a.values[8] * b.values[14] + a.values[12];
    out.values[13] = a.values[1] * b.values[12] + a.values[5] * b.values[13] + a.values[9] * b.values[14] + a.values[13];
    out.values[14] = a.values[2] * b.values[12] + a.values[6] * b.values[13] + a.values[10] * b.values[14] + a.values[14];
    out.values[15] = 1;
    return out;
}

inline LdrVector transform_point(const LdrMatrix& transform, const LdrVector& vec)
{
    LdrVector    out;
    const float* mat = transform.values;
    out.x = vec.x * (mat)[0] + vec.y * (mat)[4] + vec.z * (mat)[8] + (mat)[12];
    out.y = vec.x * (mat)[1] + vec.y * (mat)[5] + vec.z * (mat)[9] + (mat)[13];
    out.z = vec.x * (mat)[2] + vec.y * (mat)[6] + vec.z * (mat)[10] + (mat)[14];
    return out;
}

inline LdrVector transform_vec(const LdrMatrix& transform, const LdrVector& vec)
{
    LdrVector    out;
    const float* mat = transform.values;
    out.x = vec.x * (mat)[0] + vec.y * (mat)[4] + vec.z * (mat)[8];
    out.y = vec.x * (mat)[1] + vec.y * (mat)[5] + vec.z * (mat)[9];
    out.z = vec.x * (mat)[2] + vec.y * (mat)[6] + vec.z * (mat)[10];
    return out;
}
inline LdrVector vec_mul(const LdrVector a, const float b)
{
    return { a.x * b, a.y * b, a.z * b };
}
inline float vec_dot(const LdrVector a, const LdrVector b)
{
    return a.x * b.x + a.y * b.y + a.z * b.z;
}
inline float vec_sq_length(const LdrVector a)
{
    return vec_dot(a, a);
}
inline float vec_length(const LdrVector a)
{
    return sqrt(vec_dot(a, a));
}

inline LdrVector vec_normalize(const LdrVector a)
{
    float len = vec_length(a);
    return vec_mul(a, (1.0f / len));
}
inline LdrVector vec_cross(const LdrVector a, const LdrVector b)
{
    return { a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x };
}
inline LdrVector vec_sub(const LdrVector a, const LdrVector b)
{
    return { a.x - b.x, a.y - b.y, a.z - b.z };
}

inline LdrVector getTriangleNormal(const LdrVector& v0,
    const LdrVector& v1,
    const LdrVector& v2)
{
    return vec_normalize(vec_cross(vec_sub(v1, v0), vec_sub(v2, v0)));
}


#define tricount (hires ? rpart.num_trianglesC : rpart.num_triangles)
#define tris (hires ? rpart.trianglesC : rpart.triangles)
void LoadColors(const std::string& ldrpath);

void GetLdrItem(ldr::Loader* pLoader, BrickThreadPool* threadPool,
    const std::string& name, std::filesystem::path& filepath,
    const std::vector<int> atlasMaterialMapping, bool hires,
    const LdrMatrix *matrix,
    std::vector<unsigned char> &data)
{
    if (sColors.size() == 0)
        LoadColors("c:\\homep4\\lego\\ldraw");
    LdrModelHDL model;
    LdrResult result;
    if (threadPool != nullptr)
    {
        result = pLoader->createModel(name.c_str(), LDR_FALSE, &model,
            *matrix);
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
        LdrMatrix idmat = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        result = pLoader->createModel(name.c_str(), LDR_TRUE, &model,
            idmat);
        if (result < LDR_SUCCESS)
            return;

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
            LdrMatrix mat = mat_mul(*matrix, instance.transform);
            LdrVector v = transform_point(mat, rpart.vertices[idx].position);
            LdrVector n = transform_vec(mat, rpart.vertices[idx].normal);
            memcpy(&curVtx->m_x, &v, sizeof(LdrVector));
            memcpy(&curVtx->m_nx, &n, sizeof(LdrVector));
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
        uint32_t* endIdx = curIdx + tricount * 3;

        auto ldrvtxs = rpart.vertices;
        for (uint32_t* pIdx = curIdx; pIdx < endIdx; pIdx += 3)
        {
            LdrVector n = getTriangleNormal(ldrvtxs[*pIdx].position,
                ldrvtxs[*(pIdx + 1)].position,
                ldrvtxs[*(pIdx + 2)].position);

            if (vec_dot(ldrvtxs[*pIdx].normal, n) < 0)
            {
                uint32_t tmp = *pIdx;
                *pIdx = *(pIdx + 2);
                *(pIdx + 2) = tmp;
            }
        }

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

extern "C" __declspec(dllexport) void LdrLoadCachedFile(const char* file)
{
    std::ifstream iff(file, std::ios::binary | std::ios::ate);
    size_t filesize = iff.tellg();
    iff.seekg(0);
    resultData.resize(filesize);
    iff.read((char*)resultData.data(), resultData.size());
    const unsigned char* ptr = resultData.data();
    uint32_t numvtx = *(uint32_t*)ptr;
    ptr += sizeof(uint32_t);
    std::vector<PosTexcoordNrmVertex> vtxs(numvtx);
    for (int ivtx = 0; ivtx < numvtx; ++ivtx)
    {
        memcpy(&vtxs[ivtx], ptr, sizeof(PosTexcoordNrmVertex));
        ptr += sizeof(PosTexcoordNrmVertex);
    }
    uint32_t numidx = *(uint32_t*)ptr;
}

extern "C" __declspec(dllexport) void LdrLoadFile(const char* basepath, const char* name, float *matptr)
{
    std::shared_ptr<ldr::Loader> ldrLoaderHR = std::make_shared<ldr::Loader>();
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
    /*
    createInfo.partHiResPrimitives = LDR_FALSE;
    createInfo.partFixTjunctions = LDR_FALSE;
    createInfo.renderpartChamfer = 0.2f;
    ldrLoaderLR->init(&createInfo);
    */
    if (threadPool == nullptr)
        threadPool = std::make_shared<BrickThreadPool>(basepath, ldrLoaderHR.get());
    std::filesystem::path path(basepath);
    resultData.clear();
    GetLdrItem(ldrLoaderHR.get(), threadPool.get(),
        name, path, sAtlasMapping, true, (const LdrMatrix *)matptr, resultData);

    ldrLoaderHR->deinit();
}

extern "C" __declspec(dllexport) void *LdrGetResultPtr()
{
    return resultData.data();
}

extern "C" __declspec(dllexport) int LdrGetResultSize()
{
    return resultData.size();
}


extern "C" __declspec(dllexport) void LdrWriteFile(const char* basepath, const char* name, float* matptr,
    char* outPath, bool force)
{
    std::shared_ptr<ldr::Loader> ldrThLoaderHR = std::make_shared<ldr::Loader>();
    std::shared_ptr<ldr::Loader> ldrThLoaderLR = std::make_shared<ldr::Loader>();
        
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
    ldrThLoaderHR->init(&createInfo);

    createInfo.partHiResPrimitives = LDR_FALSE;
    createInfo.partFixTjunctions = LDR_FALSE;
    createInfo.renderpartChamfer = 0.2f;
    ldrThLoaderLR->init(&createInfo);

    {
        std::string hrpath(outPath);
        hrpath += ".hr_mesh";
        if (force || !std::filesystem::exists(hrpath))
        {
            std::filesystem::path path(basepath);
            std::vector<unsigned char> outdata;
            GetLdrItem(ldrThLoaderHR.get(), nullptr,
                name, path, sAtlasMapping, true, (const LdrMatrix*)matptr, outdata);
            std::ofstream file(hrpath, std::ios::binary);
            file.write((char*)outdata.data(), outdata.size());
            file.flush();
            file.close();
        }
    }
    {
        std::string lrpath(outPath);
        lrpath += ".lr_mesh";
        if (force || !std::filesystem::exists(lrpath))
        {
            std::filesystem::path path(basepath);
            std::vector<unsigned char> outdata;
            GetLdrItem(ldrThLoaderLR.get(), nullptr,
                name, path, sAtlasMapping, false, (const LdrMatrix*)matptr, outdata);
            std::ofstream file(lrpath, std::ios::binary);
            file.write((char*)outdata.data(), outdata.size());
            file.flush();
            file.close();
        }
    }

    ldrThLoaderLR->deinit();
    ldrThLoaderHR->deinit();
}



void LoadColors(const std::string& ldrpath)
{
    std::filesystem::path configpath =
        std::filesystem::path(ldrpath) / "LDConfig.ldr";
    std::ifstream ifs(configpath);
    std::regex colorrg("0\\s!COLOUR\\s(\\w+)\\s+CODE\\s+(\\d+)\\s+VALUE\\s#([\\dA-F]+)\\s+EDGE\\s+#([\\dA-F]+)");
    std::regex legoidrg("0\\s+\\/\\/\\sLEGOID\\s+(\\d+)\\s-\\s([\\w\\s]+)");
    int legoidCur;
    std::string legoNameCur;
    while (!ifs.eof())
    {
        std::string line;
        std::getline(ifs, line);
        std::smatch match;
        if (std::regex_search(line, match, legoidrg))
        {
            legoidCur = stoi(match[1].str());
            legoNameCur = match[2];
        }
        else if (std::regex_search(line, match, colorrg))
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
                0xFF},
                0,
                legoidCur,
                legoNameCur
            };
            sColors.insert(std::make_pair(index, bc));
        }
    }
    auto itcol = sColors.end();
    std::advance(itcol, -1);
    sAtlasMapping.resize(itcol->second.code + 1, -1);
    for (auto& col : sColors)
    {
        sAtlasMapping[col.second.code] = col.second.atlasidx;
    }

}
