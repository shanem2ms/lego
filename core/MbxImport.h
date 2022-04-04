#pragma once

namespace sam
{
    class MbxImport
    {
    public:
        void ImportFile(const std::string& file, std::vector<PartInst>& piList);
    };
}