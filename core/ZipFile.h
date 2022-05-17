#pragma once
#include <mutex>
struct zip;

namespace sam
{
    class vecstream
    {
        std::vector<uint8_t> m_data;
        mutable size_t m_offset;

    public:
        vecstream(std::vector<uint8_t>&& data) :
            m_data(data),
            m_offset(0)
        {

        }

        size_t length() const { return m_data.size(); }
        bool valid() const { return m_data.size() > 0; }
        vecstream() : m_offset(0) {}

        vecstream(vecstream&& other)
        {
            m_data = std::move(other.m_data);
            m_offset = other.m_offset;
        }

        void read(char* outData, size_t size) const
        {
            memcpy(outData, m_data.data() + m_offset, size);
            m_offset += size;
        }

        std::istringstream readText() const;
    };

    class ZipFile
    {
        mutable std::mutex m_zipmutex;
        std::map<std::string, std::pair<int, uint64_t>> m_cacheZipIndices;
        zip* m_za;
    public:        
        ZipFile(const std::string& name);

        vecstream ReadFile(const std::string& name) const;
        std::vector<std::string> ListFiles(const std::string ext) const;
    };

   
}