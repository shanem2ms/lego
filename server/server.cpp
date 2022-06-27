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
        void Run(const std::string &path, const std::string &hostaddr, int hostport)
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
            if (msg->m_type == ENetMsg::GetLevelDbValue)
            {
                GetLevelValueMsg gmsg;
                gmsg.ReadData((const uint8_t*)msg);
                std::string val;
                bool result = m_levelSvr->GetValue(gmsg.m_key, &response.data);
                if (!result) response.data = std::string();
            }
            else if (msg->m_type == ENetMsg::SetLevelDbValue)
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
int main(int argc, char** argv)
{
    cxxopts::Options options("Blocko Server", "Runs Blocko Game Server");
    options.add_options()
        ("l,level", "Load level", cxxopts::value<std::string>())
        ("a,address", "host address", cxxopts::value<std::string>())
        ("p,port", "host port", cxxopts::value<int>())
        ("h,help", "Print usage")
        ;

    auto result = options.parse(argc, argv);

    if (result.count("help") || result.arguments().size() == 0)
    {
        std::cout << options.help() << std::endl;
        exit(0);
    }
    
    
    if (result.count("level"))
    {
        std::string path(result["level"].as<std::string>());
        sam::Server server;
        std::string hostaddr;
        if (result.count("address"))
            hostaddr = result["address"].as<std::string>();
        int port = 8000;
        if (result.count("port"))
            port = result["port"].as<int>();
        server.Run(path, hostaddr, port);
        while (true)
        {
#ifndef _WIN32
            usleep(10);
#endif
        }
    }
    return 0;
}