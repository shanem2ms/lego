#include "pch.h"
#include "ldrawloader.h"
#include "PolygonClipper.h"

double Area(const std::vector<Vec4>& poly);
double AreaPolys(const std::vector<std::vector<Vec4>>& polys);

inline Vec4 V4(const LdrVector& pt)
{
    return Vec4(pt.x, pt.z, 0, 0);
}

inline LdrVector vec_min(const LdrVector a, const LdrVector b)
{
    return { min(a.x, b.x), min(a.y, b.y), min(a.z, b.z) };
}
inline LdrVector vec_max(const LdrVector a, const LdrVector b)
{
    return { max(a.x, b.x), max(a.y, b.y), max(a.z, b.z) };
}

inline void bbox_merge(LdrBbox& bbox, const LdrVector vec)
{
    bbox.min = vec_min(bbox.min, vec);
    bbox.max = vec_max(bbox.max, vec);
}


void GetUnion(LdrVector* vertices, int32_t* indices, int32_t numindices,
    std::vector<std::vector<Vec4>> &outPolys)
{
    PolygonClipper clipper;
    for (int i = 0; i < numindices; i += 3)
    {
        std::vector<Vec4> poly;

        poly.push_back(V4(vertices[indices[i]]));
        poly.push_back(V4(vertices[indices[i + 1]]));
        poly.push_back(V4(vertices[indices[i + 2]]));
        if (Area(poly) >= 0)
            continue;
        clipper.AddPolygon(poly);
    }
    clipper.Execute(outPolys, 0);
}

extern "C" __declspec(dllexport) void FindOrientation(LdrVector *vertices0, int32_t numvertices0, int32_t *indices0, int32_t numindices0,
    LdrVector * vertices1, int32_t numvertices1, int32_t * indices1, int32_t numindices1)
{    

    std::vector<std::vector<Vec4>> unionPolys0;
    GetUnion(vertices0, indices0, numindices0, unionPolys0);
    double a0 = AreaPolys(unionPolys0);

    std::vector<std::vector<Vec4>> unionPolys1;
    GetUnion(vertices1, indices1, numindices1, unionPolys1);
    double a1 = AreaPolys(unionPolys1);

    PolygonClipper intersect;
    intersect.AddPolygons(unionPolys0);
    intersect.AddPolygons(unionPolys1);
    std::vector<std::vector<Vec4>> iPolys;
    intersect.Execute(iPolys, 1);

    double a2 = AreaPolys(iPolys);
}
