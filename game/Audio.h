#pragma once

#include <memory>

struct ma_engine;
namespace sam
{

    class Audio
    {
        std::unique_ptr<ma_engine> m_engine;
        std::string m_folder;
    public:
        Audio();
        ~Audio();
        void PlayOnce(const std::string& sound);
    };
}
