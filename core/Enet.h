#pragma once

#include <thread>
#include <future>
#include <list>

typedef struct _ENetHost ENetHost;
namespace sam
{
    struct ENetMsg
    {
        enum Id : int {
            GetOctTile = 1,
            SetOctTile = 2,
        };

        std::string msg;
    };

    struct ENetResponse
    {
        std::string data;
    };

    class ENetClient
    {
        std::string m_server;
        std::thread m_thread;
        ENetHost* m_enetHost;
        void BackgroundThread();

        struct QueuedMsg
        {
            ENetMsg msg;
            std::promise<ENetResponse> response;
        };
        std::mutex m_queueLock;
        std::list<QueuedMsg> m_queuedMsg;
    public:
        ~ENetClient();
        ENetClient(const std::string& svr);

        std::future<ENetResponse>
            Send(const ENetMsg& msg);
    };

    class ENetServer
    {
        std::string m_server;
        std::thread m_thread;
        void BackgroundThread();
        ENetHost* m_enetHost;
    public:
        ENetServer(const std::string& svr);
        ~ENetServer();
        int Start();
    };
}