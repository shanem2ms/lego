#include "StdIncludes.h"
#include "ZipFile.h"
#include "zip.h"
#include <sstream>
#include <iostream>
#include <regex>

namespace sam
{
    ZipFile::ZipFile(const std::string& name)
    {
        int err;
        if ((m_za = zip_open(name.c_str(), 0, &err)) == NULL) {
            return;
        }
        struct zip_stat sb;
        for (int i = 0; i < zip_get_num_entries(m_za, 0); i++) {
            if (zip_stat_index(m_za, i, 0, &sb) == 0) {
                m_cacheZipIndices.insert(std::make_pair(sb.name, std::make_pair(sb.index, sb.size)));
            }
        }
    }

    vecstream ZipFile::ReadFile(const std::string& name) const
    {
        auto itlores = m_cacheZipIndices.find(name);
        if (itlores != m_cacheZipIndices.end())
        {
            m_zipmutex.lock();
            zip_file_t* zf = zip_fopen_index(m_za, itlores->second.first, 0);
            std::vector<uint8_t> data;
            uint64_t filesize = itlores->second.second;
            data.resize(filesize);
            int64_t len = zip_fread(zf, data.data(), data.size());
            zip_fclose(zf);
            m_zipmutex.unlock();
            return vecstream(std::move(data));
        }
        return vecstream();
    }

    std::istringstream vecstream::readText() const
    {
        std::istringstream strstr(std::string(m_data.begin(), m_data.end()));
        return strstr;
    }

    std::vector<std::string> ZipFile::ListFiles(const std::string ext) const
    {
        std::regex extensionregex("[\\w\\d_]+\\."+ext);
        std::vector<std::string> outfiles;
        for (auto itfile = m_cacheZipIndices.begin(); itfile != m_cacheZipIndices.end();
            ++itfile)
        {
            if (std::regex_match(itfile->first, extensionregex))
            {
                outfiles.push_back(itfile->first);
            }
        }
        return outfiles;
    }

}