#include "StdIncludes.h"
#include "Engine.h"
#include "Application.h"
#include <bx/readerwriter.h>
#include <bx/file.h>
#include <bimg/bimg.h>
#include "Hud.h"
#include "Mesh.h"
#include "Physics.h"
#include <bimg/decode.h>

namespace sam
{
    static Engine* sEngine = nullptr;
    Engine::Engine() :
        m_h(0),
        m_w(0),
        m_needRebuild(false),
        m_debugCam(false),
        m_root(std::make_shared<SceneGroup>())
    {
        sEngine = this;
        m_hud = std::make_shared<Hud>();
    }

    void Engine::Resize(int w, int h)
    {
        m_h = h;
        m_w = w;
        m_needRebuild = true;
    }

    void Engine::Tick(float time)
    {
        if (m_needRebuild)
        {
            {
                m_gbufferTex =
                    bgfx::createTexture2D(m_w, m_h, false, 1, bgfx::TextureFormat::RGBA32F, BGFX_TEXTURE_RT);
                m_depthTex =
                    bgfx::createTexture2D(m_w, m_h, false, 1, bgfx::TextureFormat::D32, 
                        BGFX_TEXTURE_RT | BGFX_SAMPLER_COMPARE_LEQUAL);
                bgfx::TextureHandle fbtextures[] = {
                    m_gbufferTex,
                    m_depthTex
                };

                m_depthFB = bgfx::createFrameBuffer(BX_COUNTOF(fbtextures), fbtextures, true);
            }
            {
                m_pickColorTex = bgfx::createTexture2D(PickBufSize, PickBufSize, false, 1, bgfx::TextureFormat::RG32F, BGFX_TEXTURE_RT);
                m_pickColorRB = bgfx::createTexture2D(PickBufSize, PickBufSize, false, 1, bgfx::TextureFormat::RG32F,
                    BGFX_TEXTURE_BLIT_DST
                    | BGFX_TEXTURE_READ_BACK
                    | BGFX_SAMPLER_MIN_POINT
                    | BGFX_SAMPLER_MAG_POINT
                    | BGFX_SAMPLER_MIP_POINT
                    | BGFX_SAMPLER_U_CLAMP
                    | BGFX_SAMPLER_V_CLAMP);
                m_pickDepthTex = bgfx::createTexture2D(PickBufSize, PickBufSize, false, 1, bgfx::TextureFormat::D32,
                    BGFX_TEXTURE_RT | BGFX_SAMPLER_COMPARE_LEQUAL);
                bgfx::TextureHandle fbtextures[] = {
                    m_pickColorTex,
                    m_pickDepthTex };

                m_pickFB = bgfx::createFrameBuffer(BX_COUNTOF(fbtextures), fbtextures, true);                
            }
            m_needRebuild = false;

        }
        m_camera.Update(m_w, m_h);

        for (auto itAnim = m_animations.begin();
            itAnim != m_animations.end();)
        {
            if ((*itAnim)->ProcessTick(time))
                itAnim++;
            else
                itAnim = m_animations.erase(itAnim);
        }
    }
    extern int nOctTilesTotal;
    extern int nOctTilesDrawn;
    bgfxh<bgfx::ProgramHandle> sFullscreenDeferred;
    bgfxh<bgfx::ProgramHandle> sBlit;

    void Engine::Draw(DrawContext& dc)
    {
        if (!sFullscreenDeferred.isValid())
            sFullscreenDeferred = LoadShader("vs_fullscreen.bin", "fs_deferred.bin");
        if (!sBlit.isValid())
            sBlit = LoadShader("vs_fullscreen.bin", "fs_blit.bin");

        if (!m_depthTexRef.isValid())
        {
            m_depthTexRef = bgfx::createUniform("s_depthtex", bgfx::UniformType::Sampler);
            m_gbufTexRef = bgfx::createUniform("s_gbuftex", bgfx::UniformType::Sampler);
            m_blitTexRef = bgfx::createUniform("s_blittex", bgfx::UniformType::Sampler);
            m_eyePosRef = bgfx::createUniform("u_eyePos", bgfx::UniformType::Vec4);
            m_texelSizeRef = bgfx::createUniform("u_texelSize", bgfx::UniformType::Vec4);
            m_invViewProjRef = bgfx::createUniform("u_deferredViewProj", bgfx::UniformType::Mat4);
            
        }
        nOctTilesTotal = nOctTilesDrawn = 0;
        gmtl::identity(dc.m_mat);
        float near = dc.m_nearfar[0];
        float far = dc.m_nearfar[2];
        gmtl::Matrix44f view = DrawCam().ViewMatrix();
        gmtl::Matrix44f proj0 = DrawCam().GetPerspectiveMatrix(near, far);

        bgfx::setViewName(DrawViewId::DeferredObjects, "DeferredObjects");
        bgfx::setViewFrameBuffer(DrawViewId::DeferredObjects, m_depthFB); 
        bgfx::setViewClear(DrawViewId::DeferredObjects,
            BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
            0x000000ff,
            1.0f,
            0
        );
        bgfx::setViewTransform(DrawViewId::DeferredObjects, view.getData(), proj0.getData());

        
        bgfx::setViewName(DrawViewId::DeferredLighting, "DeferredLighting");
        bgfx::setViewFrameBuffer(DrawViewId::DeferredLighting, BGFX_INVALID_HANDLE);
        bgfx::setViewClear(DrawViewId::DeferredLighting,
            BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
            0x000000ff,
            1.0f,
            0
        );
        Quad::init();
        Matrix44f m;
        gmtl::identity(m);
        bgfx::setTransform(m.getData());
        bgfx::setTexture(0, m_depthTexRef, m_depthTex, 0);
        bgfx::setTexture(1, m_gbufTexRef, m_gbufferTex, 0);
        Matrix44f invViewProj = proj0 * view;
        invert(invViewProj);
        bgfx::setUniform(m_invViewProjRef, invViewProj.getData());
        Point3f eyepos = DrawCam().GetFly().pos;
        bgfx::setUniform(m_eyePosRef, &eyepos);
        gmtl::Vec4f texelSize(1.0f / m_w, 1.0f / m_h, near, far);
        bgfx::setUniform(m_texelSizeRef, &texelSize);
        uint64_t state = 0
            | BGFX_STATE_WRITE_RGB
            | BGFX_STATE_WRITE_A
            | BGFX_STATE_MSAA;
        // Set render states.l
        bgfx::setState(state);
        bgfx::setVertexBuffer(0, Quad::vbh);
        bgfx::setIndexBuffer(Quad::ibh);
        bgfx::submit(DrawViewId::DeferredLighting, sFullscreenDeferred);


        bgfx::setViewName(DrawViewId::ForwardRendered, "ForwardRendered");
        bgfx::setViewFrameBuffer(DrawViewId::ForwardRendered, BGFX_INVALID_HANDLE);
        bgfx::setViewTransform(DrawViewId::ForwardRendered, view.getData(), proj0.getData());
        nOctTilesTotal = nOctTilesDrawn = 0;


        bgfx::setViewName(DrawViewId::HUD, "HUD");
        bgfx::setViewFrameBuffer(DrawViewId::HUD, BGFX_INVALID_HANDLE);
        bgfx::setViewName(DrawViewId::PickObjects, "PickObjects");
        bgfx::setViewClear(DrawViewId::PickObjects,
            BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
            0x000000ff,
            1.0f,
            0
        );
        Matrix44f p2 = proj0 * makeScale<Matrix44f>(Vec3f(dc.m_pickViewScale[0], dc.m_pickViewScale[1], 1));
        bgfx::setViewTransform(DrawViewId::PickObjects, view.getData(), p2.getData());


        bgfx::setViewFrameBuffer(DrawViewId::PickObjects, m_pickFB);
        bgfx::setViewName(DrawViewId::PickBlit, "PickBlit");
        bgfx::blit(DrawViewId::PickBlit, m_pickColorRB, 0, 0, m_pickColorTex);

        m_root->DoDraw(dc);
        m_hud->DoDraw(dc);
        auto pf = std::make_shared<PickFrame>();
        pf->pickData.resize(PickBufSize * PickBufSize * 2);
        pf->frameIdx = bgfx::readTexture(m_pickColorRB, pf->pickData.data());
        m_pickFrames.push_back(pf);
        pf->items = dc.m_pickedCandidates;
        if (dc.m_frameIdx == -1)
        {
            bgfx::FrameBufferHandle fbh = BGFX_INVALID_HANDLE;
            bgfx::requestScreenShot(fbh, "test.png");
        }        
      
        Physics::Inst().DebugRender(dc);
        
        bool drawPickBuffer = false;
        if (drawPickBuffer)
        {
            bgfx::setViewFrameBuffer(5, BGFX_INVALID_HANDLE);
            bgfx::setTexture(0, m_blitTexRef, m_pickColorTex, 0);
            state = 0
                | BGFX_STATE_WRITE_RGB
                | BGFX_STATE_WRITE_A
                | BGFX_STATE_MSAA;
            // Set render states.l
            bgfx::setState(state);
            bgfx::setVertexBuffer(0, Quad::vbh);
            bgfx::setIndexBuffer(Quad::ibh);
            bgfx::submit(5, sBlit);
        }
        m_nextView = 6;
        for (auto draw : m_externalDraws)
        {
            draw->Draw(dc);
        }
    }

    void Engine::UpdatePickData(DrawContext& dc)
    {
        if (m_pickFrames.size() > 0 &&
            m_pickFrames[0]->frameIdx == dc.m_frameIdx)
        {
            PickFrame& pf = *m_pickFrames[0];
            int offset = (PickBufSize * PickBufSize / 2 + PickBufSize / 2) * 2;
            int partFl = (int)pf.pickData[offset + 1];
            if (partFl < pf.items.size())
            {
                dc.m_pickedItem = pf.items[partFl];
                dc.m_pickedVal = pf.pickData[offset];

            }
            m_pickFrames.erase(m_pickFrames.begin());
        }
    }

    void Engine::AddAnimation(const std::shared_ptr<Animation>& anim)
    {
        m_animations.push_back(anim);
    }

    Animation::Animation() :
        m_startTime(-1)
    {}

    bool Animation::ProcessTick(float fullTime)
    {
        if (m_startTime < 0)
            m_startTime = fullTime;

        return Tick(fullTime - m_startTime);
    }

    static const bgfx::Memory* loadMem(bx::FileReaderI* _reader, const char* _filePath)
    {
        if (bx::open(_reader, _filePath))
        {
            uint32_t size = (uint32_t)bx::getSize(_reader);
            const bgfx::Memory* mem = bgfx::alloc(size + 1);
            bx::Error err;
            bx::read(_reader, mem->data, size, &err);
            bx::close(_reader);
            mem->data[mem->size - 1] = '\0';
            return mem;
        }

        return NULL;
    }

    bgfx::ProgramHandle Engine::LoadShader(const std::string& vtx, const std::string& px)
    {
        std::string key = vtx + ":" + px;
        auto itshd = m_shaders.find(key);
        if (itshd != m_shaders.end())
            return itshd->second;
        bx::FileReader fileReader;
        std::string path = Application::Inst().StartupPath();
        std::string vtxpath = path + "\\" + vtx;
        std::string pxpath = path + "\\" + px;
        bgfx::ShaderHandle vtxShader = bgfx::createShader(loadMem(&fileReader, vtxpath.c_str()));
        bgfx::ShaderHandle fragShader = bgfx::createShader(loadMem(&fileReader, pxpath.c_str()));
        bgfx::ProgramHandle pgm = bgfx::createProgram(vtxShader, fragShader, true);
        m_shaders.insert(std::make_pair(key, pgm));
        return pgm;
    }

    bgfx::ProgramHandle Engine::LoadShader(const std::string& cs)
    {
        auto itshd = m_shaders.find(cs);
        if (itshd != m_shaders.end())
            return itshd->second;
        bx::FileReader fileReader;
        bgfx::ShaderHandle csShader = bgfx::createShader(loadMem(&fileReader, cs.c_str()));
        bgfx::ProgramHandle pgm = bgfx::createProgram(csShader, true);
        m_shaders.insert(std::make_pair(cs, pgm));
        return pgm;
    }

    Engine& Engine::Inst()
    {
        return *sEngine;
    }

    struct BgfxCallback : public bgfx::CallbackI
    {
        virtual ~BgfxCallback()
        {
        }

        virtual void fatal(const char* _filePath, uint16_t _line, bgfx::Fatal::Enum _code, const char* _str) override
        {
            BX_UNUSED(_filePath, _line);

            // Something unexpected happened, inform user and bail out.
            bx::debugPrintf("Fatal error: 0x%08x: %s", _code, _str);

            // Must terminate, continuing will cause crash anyway.
            abort();
        }

        virtual void traceVargs(const char* _filePath, uint16_t _line, const char* _format, va_list _argList) override
        {
            bx::debugPrintf("%s (%d): ", _filePath, _line);
            bx::debugPrintfVargs(_format, _argList);
        }

        virtual void profilerBegin(const char* /*_name*/, uint32_t /*_abgr*/, const char* /*_filePath*/, uint16_t /*_line*/) override
        {
        }

        virtual void profilerBeginLiteral(const char* /*_name*/, uint32_t /*_abgr*/, const char* /*_filePath*/, uint16_t /*_line*/) override
        {
        }

        virtual void profilerEnd() override
        {
        }

        virtual uint32_t cacheReadSize(uint64_t _id) override
        {
            return 0;
        }

        virtual bool cacheRead(uint64_t _id, void* _data, uint32_t _size) override
        {
            return false;
        }

        virtual void cacheWrite(uint64_t _id, const void* _data, uint32_t _size) override
        {         
        }

        
        void savePng(const char* _filePath, uint32_t _width, uint32_t _height, uint32_t _srcPitch, const void* _src, bimg::TextureFormat::Enum _format, bool _yflip)
        {
            bx::FileWriter writer;
            bx::Error err;
            if (bx::open(&writer, _filePath, false, &err))
            {
                bimg::imageWritePng(&writer, _width, _height, _srcPitch, _src, _format, _yflip, &err);
                bx::close(&writer);
            }
        }
        virtual void screenShot(const char* _filePath, uint32_t _width, uint32_t _height, uint32_t _pitch, const void* _data, uint32_t /*_size*/, bool _yflip) override
        {
            savePng(_filePath, _width, _height, _pitch, _data, bimg::TextureFormat::BGRA8, _yflip);
        }

        virtual void captureBegin(uint32_t _width, uint32_t _height, uint32_t /*_pitch*/, bgfx::TextureFormat::Enum /*_format*/, bool _yflip) override
        {      
        }

        virtual void captureEnd() override
        {
        }

        virtual void captureFrame(const void* _data, uint32_t /*_size*/) override
        {
        }
    };

    std::shared_ptr< bgfx::CallbackI> CreateCallback()
    {
        return std::make_shared<BgfxCallback>();
    }

    void Engine::SetDbgCam(bool dbgCam)
    {
        m_debugCam = dbgCam;
        m_debugCamera = m_camera;        
        m_debugCamera.SetNearFar(m_camera.GetNear() * 10,
            m_camera.GetFar() * 10);
    }

}

void* load(bx::FileReaderI* _reader, bx::AllocatorI* _allocator, const char* _filePath, uint32_t* _size)
{
    if (bx::open(_reader, _filePath))
    {
        uint32_t size = (uint32_t)bx::getSize(_reader);
        void* data = BX_ALLOC(_allocator, size);
        bx::Error err;
        _reader->read(data, size, &err);
        bx::close(_reader);
        if (NULL != _size)
        {
            *_size = size;
        }
        return data;
    }
    else
    {
    }

    if (NULL != _size)
    {
        *_size = 0;
    }

    return NULL;
}

static bx::DefaultAllocator sAllocator;

static void imageReleaseCb(void* _ptr, void* _userData)
{
    BX_UNUSED(_ptr);
    bimg::ImageContainer* imageContainer = (bimg::ImageContainer*)_userData;
    bimg::imageFree(imageContainer);
}

void unload(void* _ptr)
{
    BX_FREE(&sAllocator, _ptr);
}

bgfx::TextureHandle loadTexture(bx::FileReaderI* _reader, const char* _filePath, uint64_t _flags, uint8_t _skip, bgfx::TextureInfo* _info, bimg::Orientation::Enum* _orientation)
{
    BX_UNUSED(_skip);
    bgfx::TextureHandle handle = BGFX_INVALID_HANDLE;

    uint32_t size;
    void* data = load(_reader, &sAllocator, _filePath, &size);
    if (NULL != data)
    {
        bimg::ImageContainer* imageContainer = bimg::imageParse(&sAllocator, data, size);

        if (NULL != imageContainer)
        {
            if (NULL != _orientation)
            {
                *_orientation = imageContainer->m_orientation;
            }

            const bgfx::Memory* mem = bgfx::makeRef(
                imageContainer->m_data
                , imageContainer->m_size
                , imageReleaseCb
                , imageContainer
            );
            unload(data);

            if (imageContainer->m_cubeMap)
            {
                handle = bgfx::createTextureCube(
                    uint16_t(imageContainer->m_width)
                    , 1 < imageContainer->m_numMips
                    , imageContainer->m_numLayers
                    , bgfx::TextureFormat::Enum(imageContainer->m_format)
                    , _flags
                    , mem
                );
            }
            else if (1 < imageContainer->m_depth)
            {
                handle = bgfx::createTexture3D(
                    uint16_t(imageContainer->m_width)
                    , uint16_t(imageContainer->m_height)
                    , uint16_t(imageContainer->m_depth)
                    , 1 < imageContainer->m_numMips
                    , bgfx::TextureFormat::Enum(imageContainer->m_format)
                    , _flags
                    , mem
                );
            }
            else if (bgfx::isTextureValid(0, false, imageContainer->m_numLayers, bgfx::TextureFormat::Enum(imageContainer->m_format), _flags))
            {
                handle = bgfx::createTexture2D(
                    uint16_t(imageContainer->m_width)
                    , uint16_t(imageContainer->m_height)
                    , 1 < imageContainer->m_numMips
                    , imageContainer->m_numLayers
                    , bgfx::TextureFormat::Enum(imageContainer->m_format)
                    , _flags
                    , mem
                );
            }

            if (bgfx::isValid(handle))
            {
                bgfx::setName(handle, _filePath);
            }

            if (NULL != _info)
            {
                bgfx::calcTextureSize(
                    *_info
                    , uint16_t(imageContainer->m_width)
                    , uint16_t(imageContainer->m_height)
                    , uint16_t(imageContainer->m_depth)
                    , imageContainer->m_cubeMap
                    , 1 < imageContainer->m_numMips
                    , imageContainer->m_numLayers
                    , bgfx::TextureFormat::Enum(imageContainer->m_format)
                );
            }
        }
    }

    return handle;
}

