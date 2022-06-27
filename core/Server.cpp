// server.c
#include <StdIncludes.h>
#include <stdio.h>
#include <Enet.h>
#include <Level.h>
#include <cxxopts.hpp>
#include <signal.h>
#include <stdlib.h>
#include <unistd.h>

namespace sam
{
    class Server : public IServerHandler
    {
        std::unique_ptr<ENetServer> m_server;
        std::unique_ptr<LevelSvr> m_levelSvr;
    public:
        void Run(const std::string& path, const std::string& hostaddr, int hostport)
        {
            std::cout << "Starting Enet server on ip " << hostaddr << " port " << hostport << std::endl;
            m_server = std::make_unique<ENetServer>(hostaddr, hostport, this);
            m_levelSvr = std::make_unique<LevelSvr>(false);
            std::cout << "Loading level " << path << std::endl;
            m_levelSvr->OpenDb(path);
            m_server->Start();
        }
        ENetResponse HandleMessage(const ENetMsg::Header* msg)
        {
            ENetResponse response;
            if (msg->m_type == ENetMsg::GetValue)
            {
                GetLevelValueMsg gmsg;
                gmsg.ReadData((const uint8_t*)msg);
                std::string val;
                bool result = m_levelSvr->GetValue(gmsg.m_key, &response.data);
                if (!result) response.data = std::string();
            }
            else if (msg->m_type == ENetMsg::SetValue)
            {
                SetLevelValueMsg gmsg;
                gmsg.ReadData((const uint8_t*)msg);
                Loc tileLoc;
                memcpy(&tileLoc, gmsg.m_key.data(), gmsg.m_key.size());
                std::string val;
                bool result = m_levelSvr->WriteValue(gmsg.m_key, gmsg.m_data.data(), gmsg.m_data.size());
                if (!result) response.data = std::string();

            }
            return response;
        }
    };
}
