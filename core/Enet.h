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
            GetLevelDbValue = 1,
            SetLevelDbValue = 2
        };

        struct Header
        {
            Header() {}
            Header(Type t) :
                m_size(-1),
                m_type(t),
                m_uid(m_nextUid++) {}
            size_t m_size;
            Type m_type;
            size_t m_uid;
        };

        ENetMsg(Type t) :
            m_hdr(t)
        {}

        ENetMsg() {}
        Header m_hdr;
        virtual size_t GetSize() const
        { return sizeof(m_hdr);}
        virtual uint8_t *WriteData(uint8_t* data)
        {
            memcpy(data, &m_hdr, sizeof(m_hdr));
            return data + sizeof(m_hdr);
        }

        virtual const uint8_t* ReadData(const uint8_t* data)
        {
            memcpy(&m_hdr, data, sizeof(m_hdr));
            return data + sizeof(m_hdr);
        }

        static std::atomic<size_t> m_nextUid;
    };
    

    struct ENetResponse
    {
        int status;
        std::string data;
    };

    struct ENetResponseHdr
    {
        size_t m_size;
        size_t m_uid;
    };

    class ENetClient
    {
        std::string m_server;
        std::thread m_thread;
        ENetHost* m_enetHost;
        uint16_t m_port;
        void BackgroundThread();

        struct QueuedMsg
        {
            std::shared_ptr<ENetMsg> msg;

            std::unique_ptr<std::promise<ENetResponse>> response;
            std::function<void(const ENetResponse& response)> callback;
        };
        std::mutex m_queueLock;
        std::list<QueuedMsg> m_queuedMsg;
        std::unordered_map<uint64_t, QueuedMsg> m_waitingResponse;
        bool m_terminate;
    public:
        ~ENetClient();
        ENetClient(const std::string& svr, uint16_t port);

        std::future<ENetResponse>
            Send(std::shared_ptr<ENetMsg> msg);

        void Request(std::shared_ptr<ENetMsg> msg,
            const std::function<void(const ENetResponse& response)>& func);
    };

    class IServerHandler
    {
    public:
        virtual ENetResponse HandleMessage(const ENetMsg::Header *msg) = 0;
    };
    class ENetServer
    {
        std::string m_hostaddr;
        uint16_t m_port;
        std::thread m_thread;
        void BackgroundThread();
        ENetHost* m_enetHost;
        IServerHandler* m_svrHandler;        
    public:
        ENetServer(const std::string& m_hostaddr, uint16_t port, IServerHandler*);
        ~ENetServer();
        void Start();
    };
    
}