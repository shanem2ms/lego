#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "gmtl/AxisAngle.h"
#include "Mesh.h"
#include "BrickMgr.h"
#include "ldrawloader.hpp"


namespace sam
{

    void BrickManager::Brick::Load(ldr::Loader* pLoader, const std::string& name)
    {
        LdrModelHDL model;
        LdrResult result = pLoader->createModel("91405.dat", LDR_TRUE, &model);
        LdrRenderModelHDL rmodel;
        result = pLoader->createRenderModel(model, LDR_TRUE, &rmodel);

        //PosTexcoordNrmVertex 
        // access the model and part details directly
        size_t numvtx = 0;
        size_t numidx = 0;
        for (uint32_t i = 0; i < rmodel->num_instances; i++) {
            const LdrInstance& instance = model->instances[i];
            const LdrRenderPart& rpart = pLoader->getRenderPart(instance.part);
            numvtx += rpart.num_vertices;
            numidx += rpart.num_triangles * 3;
        }

        const bgfx::Memory* pVtx = bgfx::alloc(sizeof(PosTexcoordNrmVertex) * numvtx);
        const bgfx::Memory* pIdx = bgfx::alloc(sizeof(uint32_t) * numidx);

        PosTexcoordNrmVertex::init();
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
                curVtx++;
            }
            memcpy(curIdx, rpart.triangles, rpart.num_triangles * 3 * sizeof(uint32_t));
            for (uint32_t idx = 0; idx < rpart.num_triangles * 3; ++idx, curIdx++)
                *curIdx = *curIdx + vtxOffset;
            vtxOffset += rpart.num_vertices;
        }


        m_vbh = bgfx::createVertexBuffer(pVtx, PosTexcoordNrmVertex::ms_layout);
        m_ibh = bgfx::createIndexBuffer(pIdx, BGFX_BUFFER_INDEX32);
    }

    static BrickManager* spMgr = nullptr;
    BrickManager::BrickManager(const std::string & ldrpath) :
        m_ldrLoader(std::make_shared<ldr::Loader>())
    {
        // initialize library
        LdrLoaderCreateInfo  createInfo = {};
        // while parts are not directly fixed, we will implicitly create a fixed version
        // for renderparts
        createInfo.partFixMode = LDR_PART_FIX_NONE;
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
        Brick &b = m_bricks[name];
        if (!b.m_vbh.isValid())
        {
            b.Load(m_ldrLoader.get(), name);
        }
        return b;
    }
}