#pragma once

#include <thread>
#include <future>
#include <list>

typedef struct _ENetHost ENetHost;
namespace sam
{
    struct ENetMsg
    {
        enum Type : int {
            GetOctTile = 1,
            SetOctTile = 2,
        };

        ENetMsg(Type t, size_t s) :
            m_size(s),
            m_type(t),
            m_uid(m_nextUid++) {}

        size_t m_size;
        Type m_type;
        size_t m_uid;

        static std::atomic<size_t> m_nextUid;
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
            uint64_t uid;
            std::shared_ptr<ENetMsg> msg;
            std::promise<ENetResponse> response;
        };
        std::mutex m_queueLock;
        std::list<QueuedMsg> m_queuedMsg;
        std::list<QueuedMsg> m_waitingResponse;
        bool m_terminate;
    public:
        ~ENetClient();
        ENetClient(const std::string& svr);

        std::future<ENetResponse>
            Send(std::shared_ptr<ENetMsg> msg);
    };

    class IServerHandler
    {
    public:
        virtual ENetResponse HandleMessage(const ENetMsg *msg) = 0;
    };
    class ENetServer
    {
        std::string m_server;
        std::thread m_thread;
        void BackgroundThread();
        ENetHost* m_enetHost;
        IServerHandler* m_svrHandler;
    public:
        ENetServer(const std::string& svr, IServerHandler*);
        ~ENetServer();
        void Start();
    };
    
}