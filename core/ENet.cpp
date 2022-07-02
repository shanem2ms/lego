#include "StdIncludes.h"
#include "PartDefs.h"
#include "ENet.h"
#include "nlohmann/json.hpp"
#include "zip.h"
#include <enet/enet.h>

using namespace nlohmann;
using namespace gmtl;

namespace sam
{
    std::atomic<size_t> ENetMsg::m_nextUid(100);
    ENetClient::ENetClient(const std::string& svr, uint16_t port) :
        m_port(port),
        m_server(svr),
        m_terminate(false)
    {
        std::thread t1(std::bind(&ENetClient::BackgroundThread, this));
        m_thread.swap(t1);
    }

    ENetClient::~ENetClient()
    {
        m_terminate = true;
        m_thread.join();
        enet_host_destroy(m_enetHost);
    }

    std::future<ENetResponse>
        ENetClient::Send(std::shared_ptr<ENetMsg> msg)
    {
        std::unique_ptr<std::promise<ENetResponse>> promise =
            std::make_unique<std::promise<ENetResponse>>();
        
        auto future = promise->get_future();
        uint64_t nextUid = 0;
        QueuedMsg qm{
            msg,
            std::move(promise),
            nullptr,
            0,
            0
        };
        std::lock_guard lock(m_queueLock);
        m_queuedMsg.push_back(std::move(qm));
        return future;
    }

    void 
        ENetClient::Request(std::shared_ptr<ENetMsg> msg,
            const std::function<void(const ENetResponse& response)>& func)
    {
        std::promise<ENetResponse> promise;

        auto future = promise.get_future();
        uint64_t nextUid = 0;
        QueuedMsg qm{
            msg
        };
        qm.callback = func;
        std::lock_guard lock(m_queueLock);
        m_queuedMsg.push_back(std::move(qm));
    }

    void ENetClient::BackgroundThread()
    {
        ENetAddress address;        
        ENetPeer* peer;
        char message[1024];
        ENetEvent evt;
        int eventStatus;
        const clock_t timeout = 2 * CLOCKS_PER_SEC;

        // a. Initialize enet
        if (enet_initialize() != 0) {
            return;;
        }

        atexit(enet_deinitialize);

        // b. Create a host using enet_host_create
        m_enetHost = enet_host_create(NULL, 1, 2, 57600 / 8, 14400 / 8);

        if (m_enetHost == NULL) {
            exit(EXIT_FAILURE);
        }

        enet_address_set_host(&address, m_server.c_str());
        address.port = m_port;

        // c. Connect and user service
        peer = enet_host_connect(m_enetHost, &address, 2, 0);

        if (peer == NULL) {
            exit(EXIT_FAILURE);
        }

        eventStatus = 1;

        std::list<QueuedMsg> retries;
        while (!m_terminate)
        {
            std::list<QueuedMsg> queuedMsg;
            {
                std::lock_guard lock(m_queueLock);
                std::swap(m_queuedMsg, queuedMsg);
                for (auto& msg : retries)
                    queuedMsg.push_back(std::move(msg));
                retries.clear();
            }
            for (QueuedMsg& msg : queuedMsg)
            {
                std::vector<uint8_t> data(msg.msg->GetSize());
                msg.msg->WriteData(data.data());
                msg.starttime = std::clock();
                ENetPacket* packet = enet_packet_create(data.data(), data.size(), ENET_PACKET_FLAG_RELIABLE);
                enet_peer_send(peer, 0, packet);
                m_waitingResponse.insert(std::make_pair(msg.msg->m_hdr.m_uid, std::move(msg)));
            }
            eventStatus = enet_host_service(m_enetHost, &evt, 5);
            
            // If we had some evt that interested us
            if (eventStatus > 0) {
                switch (evt.type) {
                case ENET_EVENT_TYPE_CONNECT:
                    printf("(Client) We got a new connection from %d.%d.%d.%d\n",
                        (evt.peer->address.host >> 24) & 255, 
                        (evt.peer->address.host >> 16) & 255, 
                        (evt.peer->address.host >> 8) & 255, 
                        (evt.peer->address.host >> 0) & 255);
                    break;

                case ENET_EVENT_TYPE_RECEIVE:
                {
                    ENetResponseHdr* hdr = (ENetResponseHdr*)evt.packet->data;
                    auto itResp = m_waitingResponse.find(hdr->m_uid);
                    if (itResp != m_waitingResponse.end())
                    {
                        ENetResponse resp;
                        resp.data.resize(hdr->m_size);
                        memcpy(resp.data.data(), evt.packet->data + sizeof(ENetResponseHdr),
                            resp.data.size());
                        if (itResp->second.response)
                            itResp->second.response->set_value(resp);
                        if (itResp->second.callback != nullptr)
                            itResp->second.callback(resp);
                        m_waitingResponse.erase(itResp);
                    }
                    enet_packet_destroy(evt.packet);
                    break;
                }

                case ENET_EVENT_TYPE_DISCONNECT:
                    evt.peer->data = NULL;
                    break;
                }
            }

            clock_t curtime = std::clock();
            for (auto itWaiting = m_waitingResponse.begin(); itWaiting !=
                m_waitingResponse.end(); )
            {
                if ((curtime - itWaiting->second.starttime) > timeout)
                {
                    itWaiting->second.retries++;
                    retries.push_back(std::move(itWaiting->second));                    
                    itWaiting = m_waitingResponse.erase(itWaiting);
                }
                else
                    ++itWaiting;
            }
        }
    }

    ENetServer::ENetServer(const std::string& hostaddr, uint16_t port, IServerHandler* pHandler) :
        m_hostaddr(hostaddr),
        m_port(port),
        m_svrHandler(pHandler)
    {
    }

    void ENetServer::Start()
    {
        std::thread t1(std::bind(&ENetServer::BackgroundThread, this));
        m_thread.swap(t1);        
    }

    ENetServer::~ENetServer()
    {
        enet_host_destroy(m_enetHost);
        m_thread.join();
    }

    void ENetServer::BackgroundThread()
    {
        ENetAddress address;
        ENetEvent evt;
        int eventStatus;

        // a. Initialize enet
        if (enet_initialize() != 0) {
            fprintf(stderr, "An error occured while initializing ENet.\n");
            return;
        }

        atexit(enet_deinitialize);

        // b. Create a host using enet_host_create
        if (m_hostaddr.length() > 0)
            enet_address_set_host(&address, m_hostaddr.c_str());
        else
            address.host = ENET_HOST_ANY;
        address.port = m_port;

        m_enetHost = enet_host_create(&address, 32, 2, 0, 0);

        if (m_enetHost == NULL) {
            fprintf(stderr, "An error occured while trying to create an ENet server host\n");
            exit(EXIT_FAILURE);
        }

        // c. Connect and user service
        eventStatus = 1;

        printf("(Server) start host\n");
        while (1) {
            eventStatus = enet_host_service(m_enetHost, &evt, 50000);

            // If we had some evt that interested us
            if (eventStatus > 0) {
                switch (evt.type) {
                case ENET_EVENT_TYPE_CONNECT:
                    printf("(Server) We got a new connection from %d.%d.%d.%d\n",
                        (evt.peer->address.host >> 24) & 255,
                        (evt.peer->address.host >> 16) & 255,
                        (evt.peer->address.host >> 8) & 255,
                        (evt.peer->address.host >> 0) & 255);
                    break;

                case ENET_EVENT_TYPE_RECEIVE:
                {
                    ENetMsg::Header* msg = (ENetMsg::Header*)evt.packet->data;
                    ENetResponse resp = m_svrHandler->HandleMessage(msg);                    
                    std::string rdata;
                    rdata.resize(sizeof(ENetResponseHdr) + resp.data.size());
                    ENetResponseHdr ehdr;
                    ehdr.m_size = resp.data.size();
                    ehdr.m_uid = msg->m_uid;
                    memcpy(rdata.data(), &ehdr, sizeof(ENetResponseHdr));
                    memcpy(rdata.data() + sizeof(ENetResponseHdr), resp.data.data(), resp.data.size());
                    ENetPacket* packet = enet_packet_create(rdata.data(), rdata.size(), ENET_PACKET_FLAG_RELIABLE);
                    enet_peer_send(evt.peer, 0, packet);
                    enet_packet_destroy(evt.packet);
                    break;
                }
                case ENET_EVENT_TYPE_DISCONNECT:

                    // Reset m_enetHost's information
                    evt.peer->data = NULL;
                    break;

                }
            }
            else
                break;
        }
    }

}