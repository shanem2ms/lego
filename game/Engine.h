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

class IEngineDraw
{
public:
    virtual void Draw(DrawContext & dc) = 0;
};

constexpr int PickBufSize = 128;

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
    bgfxh<bgfx::FrameBufferHandle> m_depthFB;
    bgfxh<bgfx::TextureHandle> m_depthTex;
    bgfxh<bgfx::TextureHandle> m_gbufferTex;
    bgfxh<bgfx::TextureHandle> m_pickDepthTex;
    bgfxh<bgfx::TextureHandle> m_pickColorTex;
    bgfxh<bgfx::TextureHandle> m_envTex;
    bgfxh<bgfx::TextureHandle> m_envIrrTex;
    bgfxh<bgfx::UniformHandle> m_envTexRef;
    bgfxh<bgfx::UniformHandle> m_envIrrTexRef;
    bgfxh<bgfx::TextureHandle> m_pickColorRB;
    bgfxh<bgfx::UniformHandle> m_blitTexRef;
    bgfxh<bgfx::TextureHandle> m_atmTransmittance;
    bgfxh<bgfx::TextureHandle> m_atmIrradiance;
    bgfxh<bgfx::TextureHandle> m_atmScatter;
    bgfxh<bgfx::FrameBufferHandle> m_pickFB;

    std::map<std::string, bgfx::ProgramHandle> m_shaders;
    std::vector<IEngineDraw*> m_externalDraws;
    bool m_debugCam;
    std::atomic<int> m_nextView;

    struct PickFrame
    {
        std::vector<float> pickData;
        std::vector<std::shared_ptr<SceneItem>> items;
        uint32_t frameIdx;
    };
    std::vector<std::shared_ptr<PickFrame>> m_pickFrames;
    SceneItem* m_pickedItem;
    float m_pickedVal;
public:
    Engine();

    static Engine& Inst();
    Camera& ViewCam() { return m_camera; }
    Camera& DrawCam() { return m_debugCam ? m_debugCamera : m_camera; }
    void SetDbgCam(bool dbgCam);
    void Tick(float time);
    int GetNextView() {
        return m_nextView++;
    }
    bgfx::ProgramHandle LoadShader(const std::string& vtx, const std::string& px);
    bgfx::ProgramHandle LoadShader(const std::string& cs);
    static DrawContext & Ctx();
    void Resize(int w, int h);
    void Draw(DrawContext & nvg);
    void UpdatePickData(DrawContext& nvg);
    void AddAnimation(const std::shared_ptr<Animation>& anim);
    const std::shared_ptr<SceneGroup> &Root() { return m_root; }
    void AddExternalDraw(IEngineDraw* externalDraw) {
        m_externalDraws.push_back(externalDraw);
    }
};

}