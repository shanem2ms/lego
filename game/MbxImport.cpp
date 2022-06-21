#include "StdIncludes.h"
#include "SceneItem.h"
#include "Application.h"
#include "PartDefs.h"
#include "MbxImport.h"
#include "BrickMgr.h"
#include "nlohmann/json.hpp"
#include "zip.h"

using namespace nlohmann;
using namespace gmtl;

namespace sam
{

    static int partIdx = 0;
    static Vec3f pos;

    inline std::string GetNumbers(const std::string& in)
    {
        std::string outstr;
        for (auto itchar = in.begin(); itchar != in.end() && isdigit(*itchar); ++itchar)
        {
            outstr.push_back(*itchar);
        }
        return outstr;
    }
    void MbxImport::ImportFile(const std::string& file, const Vec3f & partPos, std::vector<PartInst> &piList)
    {
        int err = 0;
        zip_t* za = zip_open(file.c_str(), 0, &err);

        std::string str;
        for (int i = 0; i < zip_get_num_entries(za, 0); i++) 
        {
            zip_stat_t sb;
            if (zip_stat_index(za, i, 0, &sb) == 0) 
            {
                int len = strlen(sb.name);
                zip_file_t *zf = zip_fopen_index(za, i, 0);
                if (zf == nullptr)
                    continue;

                str.resize(sb.size);
                int bytesRead = zip_fread(zf, str.data(), str.size());
                zip_fclose(zf);
            }
        }
        if (partIdx == 0)
            pos = partPos;

        AABoxf aabb;
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
            partname = GetNumbers(partname);
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
        Vec3f anchorpt(centerpt[0], aabb.mMax[1], centerpt[2]);
        for (auto& pi : piList)
        {
            pi.pos = pi.pos - anchorpt + pos;
        }
    }
}