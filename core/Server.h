namespace sam
{
    class ENetServer;
    class LevelSvr;
    class Server : public IServerHandler
    {
        std::unique_ptr<ENetServer> m_server;
        std::unique_ptr<LevelSvr> m_levelSvr;
    public:
        void Start(const std::string& path, const std::string& hostaddr, int hostport);
        ENetResponse HandleMessage(const ENetMsg::Header* msg);
    };
}