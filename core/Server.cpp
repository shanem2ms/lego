// server.c
#include <StdIncludes.h>
#include <stdio.h>
#include <Enet.h>
#include <Level.h>
#include <cxxopts.hpp>
#include <signal.h>
#include <stdlib.h>
#include <Server.h>

namespace sam
{
    void Server::Start(const std::string& path, const std::string& hostaddr, int hostport)
    {
        std::cout << "Starting Enet server on ip " << hostaddr << " port " << hostport << std::endl;
        m_server = std::make_unique<ENetServer>(hostaddr, hostport, this);
        m_levelSvr = std::make_unique<LevelSvr>(false);
        std::cout << "Loading level " << path << std::endl;
        m_levelSvr->OpenDb(path);
        m_server->Start();
    }
    ENetResponse Server::HandleMessage(const ENetMsg::Header* msg)
    {
        return m_levelSvr->HandleMessage(msg);
    }
}
