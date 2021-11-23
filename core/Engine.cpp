#include "StdIncludes.h"
#include "Engine.h"
#include <bx/readerwriter.h>
#include <bx/file.h>
#include <bimg/bimg.h>
#include "Hud.h"
#include "Mesh.h"

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
            m_gbufferTex =
                bgfx::createTexture2D(
                    m_w
                    , m_h
                    , false
                    , 1
                    , bgfx::TextureFormat::RGBA32F
                    , BGFX_TEXTURE_RT
                );
            m_depthTex =
                bgfx::createTexture2D(
                    m_w
                    , m_h
                    , false
                    , 1
                    , bgfx::TextureFormat::D32
                    , BGFX_TEXTURE_RT | BGFX_SAMPLER_COMPARE_LEQUAL
                );
            bgfx::TextureHandle fbtextures[] = {
                m_gbufferTex,
                m_depthTex
            };
            m_depthFB = bgfx::createFrameBuffer(BX_COUNTOF(fbtextures), fbtextures, true);
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
    bgfxh<bgfx::ProgramHandle> sFullscreen;

    void Engine::Draw(DrawContext& dc)
    {
        if (!sFullscreen.isValid())
            sFullscreen = LoadShader("vs_fullscreen.bin", "fs_deferred.bin");

        if (!m_depthTexRef.isValid())
        {
            m_depthTexRef = bgfx::createUniform("s_depthtex", bgfx::UniformType::Sampler);
            m_gbufTexRef = bgfx::createUniform("s_gbuftex", bgfx::UniformType::Sampler);
            m_noiseTexRef = bgfx::createUniform("s_noisetex", bgfx::UniformType::Sampler);
            m_eyePosRef = bgfx::createUniform("u_eyePos", bgfx::UniformType::Vec4);
            m_texelSizeRef = bgfx::createUniform("u_texelSize", bgfx::UniformType::Vec4);
            m_invViewProjRef = bgfx::createUniform("u_deferredViewProj", bgfx::UniformType::Mat4);
            
        }

        if (!m_noiseTex.isValid())
        {
            int noisedim = 64;
            // Noise texture.
            const bgfx::Memory *m = bgfx::alloc(noisedim * noisedim * 4);
            for (uint32_t x = 0; x < noisedim*noisedim*4; x++)
            {
                m->data[x] = uint8_t(rand() % 255);
            }

            m_noiseTex = bgfx::createTexture2D(noisedim, noisedim, false, 1, bgfx::TextureFormat::RGBA8, 0, m);
        }

        bgfx::setViewName(0, "depth");
        bgfx::setViewName(1, "ssao");
        bgfx::setViewName(2, "items");
        bgfx::setViewFrameBuffer(0, m_depthFB);
        bgfx::setViewFrameBuffer(1, BGFX_INVALID_HANDLE);
        bgfx::setViewFrameBuffer(2, BGFX_INVALID_HANDLE);
        gmtl::identity(dc.m_mat);
        float near = dc.m_nearfar[0];
        float far = dc.m_nearfar[1];
        gmtl::Matrix44f view = DrawCam().ViewMatrix();
        
        bgfx::setViewClear(0,
            BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
            0x000000ff,
            1.0f,
            0
        );
        nOctTilesTotal = nOctTilesDrawn = 0;
        
        gmtl::Matrix44f proj0 = DrawCam().GetPerspectiveMatrix(near, far);
        bgfx::setViewTransform(0, view.getData(), proj0.getData());
        dc.m_curviewIdx = 0;
        dc.m_nearfarpassIdx = 0;
        m_root->DoDraw(dc);
        nOctTilesTotal = nOctTilesDrawn = 0;

        bgfx::setViewClear(1,
            BGFX_CLEAR_DEPTH,
            0x0,
            1.0f,
            0
        );
       
        //  d = 1 - ((1 / z) - (1 / fr)) / ((1 / nr) - (1 / fr))
        //  z = (fr * nr) / (-d * fr + d * nr + fr)
        
        Quad::init();
        Matrix44f m;
        gmtl::identity(m);
        bgfx::setTransform(m.getData());
        bgfx::setTexture(0, m_depthTexRef, m_depthTex, 0);
        bgfx::setTexture(1, m_gbufTexRef, m_gbufferTex, 0);
        bgfx::setTexture(2, m_noiseTexRef, m_noiseTex, 0);
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
        bgfx::submit(1, sFullscreen);
        
        dc.m_curviewIdx = 2;
        bgfx::setViewClear(2,
            BGFX_CLEAR_DEPTH,
            0x0,
            1.0f,
            0
        );

        gmtl::Matrix44f proj2 = DrawCam().GetPerspectiveMatrix(near, far);
        bgfx::setViewTransform(2, view.getData(), proj2.getData());
        m_root->DoDraw(dc);
        m_hud->DoDraw(dc);        

        if (dc.m_frameIdx == -1)
        {
            bgfx::FrameBufferHandle fbh = BGFX_INVALID_HANDLE;
            bgfx::requestScreenShot(fbh, "test.png");
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
        bgfx::ShaderHandle vtxShader = bgfx::createShader(loadMem(&fileReader, vtx.c_str()));
        bgfx::ShaderHandle fragShader = bgfx::createShader(loadMem(&fileReader, px.c_str()));
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
}

