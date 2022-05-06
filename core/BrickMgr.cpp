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
#include <filesystem>
#include <regex>
#define FMT_HEADER_ONLY 1
#include <fmt/format.h>
#include "Simplify.h"
#include "bullet/btBulletCollisionCommon.h"
#include "bullet/btBulletDynamicsCommon.h"
#include "rapidxml/rapidxml.hpp"
#include "nlohmann/json.hpp"

using namespace gmtl;
using namespace nlohmann;

namespace sam
{
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

    struct ConnectorInfo
    {
        ConnectorType type;
        std::vector<Vec3f> offsets;
    };

    std::map<std::string, ConnectorInfo> sConnectorMap =
    { { "stud.dat", { ConnectorType::Stud, { Vec3f(0,0,0) }}},
        { "stud4.dat", { ConnectorType::InvStud, { Vec3f(0,-4,0), Vec3f(10,-4,10), Vec3f(-10,-4,10), Vec3f(10,-4,-10), Vec3f(-10,-4,-10) }}},
        { "stud4o.dat", { ConnectorType::InvStud, { Vec3f(0,-4,0) }}},
        { "connect.dat", { ConnectorType::InvStud, { Vec3f(0,0,0) }}},
        { "stud2a.dat",{ ConnectorType::Stud, { Vec3f(0,0,0) }}},
        { "stud2.dat", { ConnectorType::InvStud, { Vec3f(0,0,0) }}},
        { "stud3.dat", { ConnectorType::InvStud, { Vec3f(10,-4,0), Vec3f(-10,-4,0) }}},
        { "stud6.dat", { ConnectorType::InvStud, { Vec3f(0,0,0)}}},
        { "1-4ring3.dat", { ConnectorType::InvStud, { Vec3f(0,0,0)}}},
    };

    static std::set<std::string> sConnectorNames;

    void InitConnectorNames()
    {
        if (sConnectorNames.size() == 0)
        {
            for (auto& connectorPair : sConnectorMap)
            {
                sConnectorNames.insert(connectorPair.first);
            }
        }
    }

    void Brick::LoadLores(ldr::Loader* pLoader, const std::string& name, std::filesystem::path& cachePath)
    {
        m_name = name;
        m_connectorsLoaded = false;
        static bool sEnableDirectLoad = true;
        PosTexcoordNrmVertex::init();
        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("lr_mesh");
        if (std::filesystem::exists(filepath))
        {
            std::ifstream ifs(filepath, std::ios_base::binary);
            uint32_t numvtx = 0;
            uint32_t numidx = 0;
            ifs.read((char*)&numvtx, sizeof(numvtx));
            if (numvtx == 0)
                return;
            m_verticesLR.resize(numvtx);
            ifs.read((char*)m_verticesLR.data(), sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)&numidx, sizeof(numidx));
            m_indicesLR.resize(numidx);
            ifs.read((char*)m_indicesLR.data(), sizeof(uint32_t) * numidx);

            PosTexcoordNrmVertex* curVtx = m_verticesLR.data();
            PosTexcoordNrmVertex* endVtx = curVtx + numvtx;
            for (; curVtx != endVtx; ++curVtx)
            {
                curVtx->m_y = -curVtx->m_y;
                if (curVtx->m_u == 16)
                    curVtx->m_u = -1;
                m_bounds += Vec3f(curVtx->m_x, curVtx->m_y, curVtx->m_z);
            }
            Vec3f ext = m_bounds.mMax - m_bounds.mMin;
            m_scale = std::max(std::max(ext[0], ext[1]), ext[2]);
            m_center = (m_bounds.mMax + m_bounds.mMin) * 0.5f;

            InitConnectorNames();
            LdrBbox bbox = pLoader->getBboxWithExlcusions(name.c_str(), sConnectorNames);
            m_collisionBox.mEmpty = false;
            m_collisionBox.mMin = Vec3f(bbox.min.x, bbox.min.y, bbox.min.z);
            m_collisionBox.mMax = Vec3f(bbox.max.x, bbox.max.y, bbox.max.z);

            Vec3f bnd = m_bounds.mMax - m_bounds.mMin;
            Vec3f col = m_collisionBox.mMax - m_collisionBox.mMin;
        }
        else
            return;

        //LoadConnectors(pLoader, name);
        m_vbhLR = bgfx::createVertexBuffer(bgfx::makeRef(m_verticesLR.data(), m_verticesLR.size() * sizeof(PosTexcoordNrmVertex)), PosTexcoordNrmVertex::ms_layout);
        m_ibhLR = bgfx::createIndexBuffer(bgfx::makeRef(m_indicesLR.data(), m_indicesLR.size() * sizeof(uint32_t)), BGFX_BUFFER_INDEX32);
    }

    void Brick::LoadHires(const std::string& name, std::filesystem::path& cachePath)
    {
        PosTexcoordNrmVertex::init();
        std::filesystem::path filepath = cachePath / name;
        filepath.replace_extension("hr_mesh");
        if (std::filesystem::exists(filepath))
        {
            std::ifstream ifs(filepath, std::ios_base::binary);
            uint32_t numvtx = 0;
            uint32_t numidx = 0;
            ifs.read((char*)&numvtx, sizeof(numvtx));
            if (numvtx == 0)
                return;
            m_verticesHR.resize(numvtx);
            ifs.read((char*)m_verticesHR.data(), sizeof(PosTexcoordNrmVertex) * numvtx);
            ifs.read((char*)&numidx, sizeof(numidx));
            m_indicesHR.resize(numidx);
            ifs.read((char*)m_indicesHR.data(), sizeof(uint32_t) * numidx);
            PosTexcoordNrmVertex* curVtx = m_verticesHR.data();
            PosTexcoordNrmVertex* endVtx = curVtx + numvtx;
            for (; curVtx != endVtx; ++curVtx)
            {
                curVtx->m_y = -curVtx->m_y;
                if (curVtx->m_u == 16)
                    curVtx->m_u = -1;
            }
        }
        else
            return;

        if (m_verticesHR.size() == 0 ||
            m_indicesHR.size() == 0)
            return;

        m_vbhHR = bgfx::createVertexBuffer(bgfx::makeRef(m_verticesHR.data(), m_verticesHR.size() * sizeof(PosTexcoordNrmVertex)), PosTexcoordNrmVertex::ms_layout);
        m_ibhHR = bgfx::createIndexBuffer(bgfx::makeRef(m_indicesHR.data(), m_indicesHR.size() * sizeof(uint32_t)), BGFX_BUFFER_INDEX32);
    }

    bool Brick::LoadCollisionMesh(const std::filesystem::path& collisionPath)
    {
        if (m_collisionShape != nullptr)
            return true;

        std::vector<std::vector<Vec3d>> meshes;
        if (std::filesystem::exists(collisionPath))
        {
            std::ifstream ifs(collisionPath, std::ios_base::binary);
            uint32_t nummeshes = 0;
            ifs.read((char*)&nummeshes, sizeof(nummeshes));
            std::vector<uint32_t> triCount(nummeshes);
            ifs.read((char*)triCount.data(), sizeof(triCount[0]) * triCount.size());
            size_t tricntPrev = 0;
            for (int idx = 0; idx < nummeshes; ++idx)
            {
                meshes.push_back(std::vector<Vec3d>());
                std::vector<Vec3d>& pts = meshes.back();
                pts.resize(triCount[idx] - tricntPrev);
                ifs.read((char*)pts.data(), sizeof(pts[0]) * pts.size());
                tricntPrev = triCount[idx];
            }
        }
        m_collisionShape = std::make_shared<btCompoundShape>();
        for (auto &mesh : meshes)
        {
            btConvexHullShape* pCvxShape = new btConvexHullShape();            
            for (auto& pt : mesh)
            {
                pCvxShape->addPoint(btVector3(pt[0], -pt[1], pt[2]) * BrickManager::Scale, false);
            }
            pCvxShape->recalcLocalAabb();
            m_collisionShape->addChildShape(btTransform::getIdentity(), pCvxShape);
        }

        
        btVector3 min, max;
        m_collisionShape->getAabb(btTransform::getIdentity(), min, max);
        return true;
    }

#define tricount (hires ? rpart.num_trianglesC : rpart.num_triangles)
#define tris (hires ? rpart.trianglesC : rpart.triangles)

    void Brick::LoadPrimitives(ldr::Loader* pLoader)
    {
        std::vector<ldr::LdrPrimitive> primitives;
        pLoader->loadPrimitives(m_name.c_str(),
            std::set<std::string>(), true, primitives);
    }

    inline bool epEquals(float a, float b)
    {
        constexpr float epsilon = 1e-5f;
        float c = (a - b);
        return c < epsilon || c >(-epsilon);
    }
    inline bool BoundsXZSizeEq(const LdrBbox& bb, const Vec2f& v)
    {
        if (epEquals(bb.max.x - bb.min.x, v[0]) &&
            epEquals(bb.max.z - bb.min.z, v[1]))
            return true;
        else
            return false;
    }

    ConnectorInfo GetConnectorForPrimitive(ldr::Loader* pLoader, const ldr::LdrPrimitive& prim)
    {
        const std::string& name = pLoader->getPrimitiveName(prim.idx);
        auto itconnection = sConnectorMap.find(name);
        if (itconnection != sConnectorMap.end())
            return itconnection->second;

        if (name == "box5.dat" && prim.inverted && BoundsXZSizeEq(prim.bbox, Vec2f(12, 12)))
            return ConnectorInfo{ ConnectorType::InvStud, { Vec3f(0,0,0) } };

        return ConnectorInfo{ ConnectorType::Unknown };
    }


    void Brick::LoadConnectors(const std::filesystem::path& connectorPath)
    {
        if (m_connectorsLoaded)
            return;

        std::map<int, ConnectorType> cTypeMap =
        { { 1, ConnectorType::Stud },
            { 2, ConnectorType::Unknown },
            { 4, ConnectorType::Unknown },
            { 8, ConnectorType::InvStud } };
        //open file
        std::ifstream infile(connectorPath);

        //get length of file
        infile.seekg(0, std::ios::end);
        size_t length = infile.tellg();
        infile.seekg(0, std::ios::beg);
        std::string str;
        str.resize(length);
        infile.read(str.data(), length);
        json doc = json::parse(str);

        for (json elem : doc)
        {
            Connector c;
            Matrix44f trans;
            float* vals = trans.mData;
            int type = elem["type"];
            json mat = elem["mat"];
            vals[0] = mat["M11"];
            vals[1] = mat["M12"];
            vals[2] = mat["M13"];
            vals[3] = mat["M14"];
            vals[4] = mat["M21"];
            vals[5] = mat["M22"];
            vals[6] = mat["M23"];
            vals[7] = mat["M24"];
            vals[8] = mat["M31"];
            vals[9] = mat["M32"];
            vals[10] = mat["M33"];
            vals[11] = mat["M34"];
            vals[12] = mat["M41"];
            vals[13] = mat["M42"];
            vals[14] = mat["M43"];
            vals[15] = mat["M44"];
            trans.mState = Matrix44f::AFFINE;
            trans = makeScale<Matrix44f>(Vec3f(1, -1, 1)) * trans;
            c.pos = Vec3f(vals[12], vals[13], vals[14]);
            Vec4f out;
            xform(out, trans, Vec4f(0, -1, 0, 0));
            c.dir = Vec3f(out);
            normalize(c.dir);
            if (type & 1) c.type = ConnectorType::Stud;
            else if (type & 4) c.type = ConnectorType::InvStud;
            else c.type = ConnectorType::Unknown;
            m_connectors.push_back(c);
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
    BrickManager::BrickManager(const std::string& _ldrpath) :
        m_ldrLoaderHR(std::make_shared<ldr::Loader>()),
        m_ldrLoaderLR(std::make_shared<ldr::Loader>()),
        m_mruCtr(0)
    {
        std::string ldrpath = Application::Inst().Documents() + "/ldraw";
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
        createInfo.basePath = ldrpath.c_str();
        m_ldrLoaderHR->init(&createInfo);

        createInfo.partFixTjunctions = LDR_FALSE;
        createInfo.partHiResPrimitives = LDR_FALSE;
        createInfo.renderpartChamfer = 0.2f;
        m_ldrLoaderLR->init(&createInfo);
        spMgr = this;
        m_cachePath = Application::Inst().Documents() + "/cache";
        m_connectorPath = Application::Inst().Documents() + "/cache";
        m_collisionPath = Application::Inst().Documents() + "/cache";
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
        std::filesystem::path connectorPath = m_connectorPath / pBrick->m_name;
        connectorPath.replace_extension("json");

        if (std::filesystem::exists(connectorPath))
            pBrick->LoadConnectors(connectorPath);
    }

    bool BrickManager::LoadCollision(Brick* pBrick)
    {
        std::filesystem::path collisionPath = m_collisionPath / pBrick->m_name;
        collisionPath.replace_extension("col");

        if (std::filesystem::exists(collisionPath))
            return pBrick->LoadCollisionMesh(collisionPath);
        return false;
    }
    void BrickManager::LoadPrimitives(Brick* pBrick)
    {
        pBrick->LoadPrimitives(m_ldrLoaderLR.get());
    }
    void BrickManager::LoadColors(const std::string& ldrpath)
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
        std::string aliasfile = Application::Inst().Documents() + "/aliases.txt";
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
                std::filesystem::path connectorPath = m_connectorPath / filename;
                connectorPath.replace_extension("json");
                filepath.replace_extension("lr_mesh");
                char* p_end;
                int val = std::strtol(filename.data(), &p_end, 10);
                if (std::filesystem::exists(filepath) &&
                    std::filesystem::exists(connectorPath))
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

            std::ifstream ifa(aliasfile);
            while (!ifa.eof())
            {
                std::string aliasline;
                std::getline(ifa, aliasline);
                size_t offset = aliasline.find(' ');
                std::string w1 = aliasline.substr(0, offset);
                std::string w2 = aliasline.substr(offset + 1);
                m_aliasParts.insert(std::pair(w1, w2));
            }
        }
        else
        {
            std::set<std::string> includeParts;
            {
                std::string ingamefile = Application::Inst().Documents() + "/ingame.txt";
                std::ifstream ifs(ingamefile);
                while (!ifs.eof())
                {
                    std::string partnm;
                    std::getline(ifs, partnm);
                    includeParts.insert(partnm);
                }
            }
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
                    std::string namenoext = name.substr(0, name.size() - 4);
                    //if (includeParts.find(name) == includeParts.end())
                    //    continue;
                    {
                        std::ifstream ifs(entry.path());
                        std::string line;
                        std::getline(ifs, line);
                        std::string movedLine("0 ~Moved to ");
                        if (line._Starts_with(movedLine))
                        {
                            std::string aliasPart = line.substr(movedLine.size());
                            m_aliasParts.insert(
                                std::make_pair(namenoext, aliasPart));
                            continue;
                        }
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
                            meshfilepath.replace_extension("lr_mesh");

                            m_partsMap.insert(std::make_pair(pd.filename, pd));
                            Application::DebugMsg(pd.filename + "\n");
                        }
                        catch (...)
                        {
                        }
                    }
                }
            }            
            std::ofstream ofa(aliasfile, std::ios::binary);
            for (auto& pair : m_aliasParts)
            {
                ofa << pair.first << " " << pair.second << std::endl;
            }
            ofa.close();
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
            if (itType->second.size() < 16)
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

        Vec4f color = Vec4f(15, 0, 0, 0);
        PosTexcoordNrmVertex::init();
        std::vector<Brick*> brickRenderQueue;
        std::swap(brickRenderQueue, m_brickRenderQueue);
        for (auto& brick : brickRenderQueue)
        {
            if (!brick->m_vbhLR.isValid())
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
            gmtl::identity(view);
            gmtl::identity(proj);
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
            m = makeScale<Matrix44f>(Vec3f(1, 1, 0.25)) *
                makeTrans<Matrix44f>(Vec3f(0, 0, 0.75f)) *
                makeRot<Matrix44f>(AxisAnglef(3 * gmtl::Math::PI_OVER_4, Vec3f(1, 0, 0))) *
                makeRot<Matrix44f>(AxisAnglef(1 * gmtl::Math::PI_OVER_4, Vec3f(0, 1, 0))) *
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
            bgfx::setVertexBuffer(0, brick->m_vbhLR);
            bgfx::setIndexBuffer(brick->m_ibhLR);
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
    Brick* BrickManager::GetBrick(const PartId& name, bool hires)
    {
        Brick& b = m_bricks[name];
        if (!b.m_vbhLR.isValid())
        {
            b.LoadLores(m_ldrLoaderLR.get(), name.GetFilename(), m_cachePath);
        }
        if (hires && (!b.m_vbhHR.isValid()))
        {
            std::filesystem::path meshfilepath = m_cachePath / name.GetFilename();
            meshfilepath.replace_extension("hr_mesh");
            b.LoadHires(name.GetFilename(), m_cachePath);
        }
        MruUpdate(&b);
        CleanCache();
        return &b;
    }

    bgfx::TextureHandle BrickManager::GetBrickThumbnail(const PartId& name)
    {
        Brick* b = GetBrick(name);
        if (!b->m_icon.isValid())
            m_brickRenderQueue.push_back(b);

        return b->m_icon;
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

    const std::string& BrickManager::PartAlias(const std::string& name)
    {
        auto itPa = m_aliasParts.find(name);
        if (itPa == m_aliasParts.end())
            return name;
        else
            return itPa->second;
    }
}