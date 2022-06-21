#pragma once

namespace sam
{
    class MbxImport
    {
    public:
        void ImportFile(const std::string& file, const Vec3f& pos, std::vector<PartInst>& piList);
    };
}