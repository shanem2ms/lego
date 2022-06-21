#pragma once

namespace sam
{
    class Mongo
    {
        std::string m_server;
    public:
        Mongo(const std::string& svr);
    };
}