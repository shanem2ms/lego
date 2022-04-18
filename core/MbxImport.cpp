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

    static int partIdx = 0;
    static Vec3f pos;
    void MbxImport::ImportFile(const std::string& file, const Vec3f & partPos, std::vector<PartInst> &piList)
    {
        if (partIdx == 0)
            pos = partPos;
        std::ifstream infile(file);

        AABoxf aabb;
        //get length of files
        infile.seekg(0, std::ios::end);
        size_t length = infile.tellg();
        infile.seekg(0, std::ios::beg);
        std::string str;
        str.resize(length);
        infile.read(str.data(), length);

        json doc = json::parse(str);
        json parts = doc["parts"];
        size_t count = 0;
        for (int pidx = partIdx; pidx < (int)parts.size(); ++pidx)
        {
            json part = parts[pidx];
            std::string partname = part["configuration"];
            size_t idx = partname.find('.');
            if (idx < partname.size())
                partname = partname.substr(0, idx);
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
            int colorval = 0;
            if (col[0].is_number_integer())
                colorval = col[0];
            else if (col[0].is_string())
            {
                std::string str = col[0];
                colorval = std::stoi(str);
            }
            PartInst pi;
            pi.id = PartId(partname);
            pi.atlasidx = BrickManager::Inst().GetColorFromLegoId(colorval).atlasidx;
            Vec4f out;  
            xform(out, mat, Vec4f(0, 0, 0, 1));
            pi.pos = Vec3f(out);
            pi.pos[0] = -pi.pos[0];
            pi.pos[1] = -pi.pos[1];
            pi.pos[2] = -pi.pos[2];
            //pi.pos[2] = -pi.pos[2];
            pi.pos *= 0.125f;
            aabb += pi.pos;
            pi.rot = make<Quatf>(mat);
            //invert(pi.rot);
            piList.push_back(pi);
        }

        Vec3f centerpt = aabb.mMax + aabb.mMin;
        Vec3f anchorpt(centerpt[0], aabb.mMin[1], centerpt[2]);
        for (auto& pi : piList)
        {
            pi.pos = pi.pos - anchorpt + pos;
        }
    }
}