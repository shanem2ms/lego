#include "StdIncludes.h"
#include "Audio.h"
#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio/miniaudio.h"

namespace sam {


    void data_callback(ma_device* pDevice, void* pOutput, const void* pInput, ma_uint32 frameCount)
    {
        return;
    }

    Audio::Audio() :
        m_folder("C:\\homep4\\lego\\legosounds\\")
    {
        m_engine = std::make_unique<ma_engine>();
        ma_result result = ma_engine_init(NULL, m_engine.get());        
    }

    void Audio::PlayOnce(const std::string& sound)
    {
        std::string s = m_folder + sound;
        ma_result result = ma_engine_play_sound(
            m_engine.get(), s.c_str(), nullptr);
    }

    Audio::~Audio()
    {

    }
}