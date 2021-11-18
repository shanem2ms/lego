#pragma once

#include "SceneItem.h"

namespace sam
{
struct DrawContext;

class Animation
{
protected:
    float m_startTime;
    Animation();
    virtual bool Tick(float time) = 0;

public:
    bool ProcessTick(float fullTime);
};

class Hud;
class Engine
{
    int m_w;
    int m_h;
    bool m_needRebuild;
    Camera m_camera;
    Camera m_debugCamera;
    
    std::shared_ptr<SceneGroup> m_root;
    std::shared_ptr<Hud> m_hud;
    std::vector<std::shared_ptr<Animation>> m_animations;
    bgfxh<bgfx::UniformHandle> m_depthTexRef;
    bgfxh<bgfx::UniformHandle> m_gbufTexRef;
    bgfxh<bgfx::UniformHandle> m_invViewProjRef;
    bgfxh<bgfx::UniformHandle> m_eyePosRef;
    bgfxh<bgfx::UniformHandle> m_texelSizeRef;
    bgfxh<bgfx::UniformHandle> m_noiseTexRef;
    bgfxh<bgfx::FrameBufferHandle> m_depthFB;
    bgfxh<bgfx::TextureHandle> m_depthTex;
    bgfxh<bgfx::TextureHandle> m_gbufferTex;
    bgfxh<bgfx::TextureHandle> m_noiseTex;

    std::map<std::string, bgfx::ProgramHandle> m_shaders;
    bool m_debugCam;


public:
    Engine();

    static Engine& Inst();
    Camera& ViewCam() { return m_camera; }
    Camera& DrawCam() { return m_debugCam ? m_debugCamera : m_camera; }
    void SetDbgCam(bool dbgCam)
    {
        m_debugCam = dbgCam;
        m_debugCamera = m_camera;
    }
    void Tick(float time);

    bgfx::ProgramHandle LoadShader(const std::string& vtx, const std::string& px);
    bgfx::ProgramHandle LoadShader(const std::string& cs);
    static DrawContext & Ctx();
    void Resize(int w, int h);
    void Draw(DrawContext & nvg);
    void AddAnimation(const std::shared_ptr<Animation>& anim);
    const std::shared_ptr<SceneGroup> &Root() { return m_root; }
};

}