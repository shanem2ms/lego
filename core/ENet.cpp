#include "StdIncludes.h"
#include "SceneItem.h"
#include "Application.h"
#include "PartDefs.h"
#include "ENet.h"
#include "BrickMgr.h"
#include "nlohmann/json.hpp"
#include "zip.h"
#include <enet/enet.h>

using namespace nlohmann;
using namespace gmtl;

namespace sam
{
    ENetClient::ENetClient(const std::string& svr) :
        m_server(svr)
    {
        std::thread t1(std::bind(&ENetClient::BackgroundThread, this));
        m_thread.swap(t1);
    }

    ENetClient::~ENetClient()
    {
        char* message = "Hello";
        //ENetPacket* packet = enet_packet_create(message, strlen(message) + 1, ENET_PACKET_FLAG_RELIABLE);
        //enet_peer_send(m_enetHost, 0, packet);
        enet_host_destroy(m_enetHost);
        m_thread.join();
    }

    std::future<ENetResponse>
        ENetClient::Send(const ENetMsg& msg)
    {
        std::promise<ENetResponse> promise;
        
        auto future = promise.get_future();
        QueuedMsg qm{
            msg,
            std::move(promise)
        };
        m_queuedMsg.push_back(std::move(qm));
        /*
        char* message = "Hello";

        if (strlen(message) > 0) {
            ENetPacket* packet = enet_packet_create(message, strlen(message) + 1, ENET_PACKET_FLAG_RELIABLE);
            enet_peer_send(peer, 0, packet);
        }
        */
        return future;
    }

    void ENetClient::BackgroundThread()
    {
        ENetAddress address;        
        ENetPeer* peer;
        char message[1024];
        ENetEvent evt;
        int eventStatus;

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

        enet_address_set_host(&address, "localhost");
        address.port = 1234;

        // c. Connect and user service
        peer = enet_host_connect(m_enetHost, &address, 2, 0);

        if (peer == NULL) {
            exit(EXIT_FAILURE);
        }

        eventStatus = 1;

        while (1) {
            eventStatus = enet_host_service(m_enetHost, &evt, 5);

            // If we had some evt that interested us
            if (eventStatus > 0) {
                switch (evt.type) {
                case ENET_EVENT_TYPE_CONNECT:
                    printf("(Client) We got a new connection from %x\n",
                        evt.peer->address.host);
                    break;

                case ENET_EVENT_TYPE_RECEIVE:
                    printf("(Client) Message from server : %s\n", evt.packet->data);
                    // Lets broadcast this message to all
                    // enet_host_broadcast(m_enetHost, 0, evt.packet);
                    enet_packet_destroy(evt.packet);
                    break;

                case ENET_EVENT_TYPE_DISCONNECT:
                    printf("(Client) %s disconnected.\n", evt.peer->data);

                    // Reset m_enetHost's information
                    evt.peer->data = NULL;
                    break;
                }
            }
        }
    }

    ENetServer::ENetServer(const std::string& svr)
    {
    }

    int ENetServer::Start()
    {
        std::thread t1(std::bind(&ENetServer::BackgroundThread, this));
        m_thread.swap(t1);
    }

    ENetServer::~ENetServer()
    {
        char* message = "Hello";
        //ENetPacket* packet = enet_packet_create(message, strlen(message) + 1, ENET_PACKET_FLAG_RELIABLE);
        //enet_peer_send(m_enetHost, 0, packet);
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
        address.host = ENET_HOST_ANY;
        address.port = 1234;

        m_enetHost = enet_host_create(&address, 32, 2, 0, 0);

        if (m_enetHost == NULL) {
            fprintf(stderr, "An error occured while trying to create an ENet server host\n");
            exit(EXIT_FAILURE);
        }

        // c. Connect and user service
        eventStatus = 1;

        while (1) {
            eventStatus = enet_host_service(m_enetHost, &evt, 50000);

            // If we had some evt that interested us
            if (eventStatus > 0) {
                switch (evt.type) {
                case ENET_EVENT_TYPE_CONNECT:
                    printf("(Server) We got a new connection from %x\n",
                        evt.peer->address.host);
                    break;

                case ENET_EVENT_TYPE_RECEIVE:
                    printf("(Server) Message from m_enetHost : %s\n", evt.packet->data);
                    // Lets broadcast this message to all
                    enet_host_broadcast(m_enetHost, 0, evt.packet);
                    break;

                case ENET_EVENT_TYPE_DISCONNECT:
                    printf("%s disconnected.\n", evt.peer->data);

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