#include "StdIncludes.h"
#include "SceneItem.h"
#include "Application.h"
#include "PartDefs.h"
#include "MbxImport.h"
#include "BrickMgr.h"
#include "nlohmann/json.hpp"

using namespace nlohmann;
using namespace gmtl;

namespace sam
{

    void MbxImport::ImportFile(const std::string& file, std::vector<PartInst> &piList)
    {
        std::ifstream infile(file);

        //get length of file
        infile.seekg(0, std::ios::end);
        size_t length = infile.tellg();
        infile.seekg(0, std::ios::beg);
        std::string str;
        str.resize(length);
        infile.read(str.data(), length);

        json doc = json::parse(str);
        json parts = doc["parts"];
        for (json part : parts)
        {
            std::string partname = part["configuration"];
            partname = BrickManager::Inst().PartAlias(partname);
            json matrix = part["matrix"];
            float m[16];
            Matrix44f mat;
            mat.mState = Matrix44f::AFFINE;
            for (int i = 0; i < 16; ++i)
            {
                mat.mData[i] = matrix[i];
            }
            transpose(mat);
            json col = part["material"]["base"];
            int colorval = col[0];
            PartInst pi;
            pi.id = PartId(partname);
            pi.atlasidx = BrickManager::Inst().GetColorFromLegoId(colorval).atlasidx;
            Vec4f out;  
            xform(out, mat, Vec4f(0, 0, 0, 1));
            pi.pos = Vec3f(out);
            pi.rot = make<Quatf>(mat);
            piList.push_back(pi);
        }
    }
}