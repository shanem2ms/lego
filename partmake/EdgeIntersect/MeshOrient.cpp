#include "pch.h"
#include "PolygonClipper.h"
#define NOMINMAX
#include "gmtl/Math.h"
#include "gmtl/AABox.h"
#include "gmtl/AABoxOps.h"
#include "gmtl/Point.h"
#include "gmtl/Vec.h"
#include "gmtl/VecOps.h"
#include "gmtl/AxisAngle.h"
#include "gmtl/AxisAngleOps.h"
#include "gmtl/Quat.h"
#include "gmtl/QuatOps.h"
#include "gmtl/Matrix.h"
#include "gmtl/MatrixOps.h"
#include "gmtl/Generate.h"

using namespace gmtl;
double Area(const std::vector<Vec4>& poly);
double AreaPolys(const std::vector<std::vector<Vec4>>& polys);

inline Vec4 V4(const Vec3f& pt, int d)
{
    if (d == 0)
        return Vec4(pt[0], pt[2], 0, 0);
    else if (d == 1)
        return Vec4(pt[0], pt[1], 0, 0);
    else if (d == 2)
        return Vec4(pt[2], pt[1], 0, 0);
}

void GetUnion(Vec3f* vertices, int32_t* indices, int32_t numindices,
    std::vector<std::vector<Vec4>> &outPolys, int d)
{
    PolygonClipper clipper;
    for (int i = 0; i < numindices; i += 3)
    {
        std::vector<Vec4> poly;

        poly.push_back(V4(vertices[indices[i]], d));
        poly.push_back(V4(vertices[indices[i + 1]], d));
        poly.push_back(V4(vertices[indices[i + 2]], d));
        double a = Area(poly);
        if (a < 0.01 && a > -0.01)
            continue;
        if (a < 0)
        {
            auto tmp = poly[0];
            poly[0] = poly[2];
            poly[2] = tmp;
        }
           
        clipper.AddPolygon(poly);
    }
    clipper.Execute(outPolys, 1);
}

inline Vec3f midpt(const AABoxf& aabb)
{
    return (aabb.mMax + aabb.mMin) * 0.5f;
}

inline Vec3f len(const AABoxf& aabb)
{
    return (aabb.mMax - aabb.mMin);
}

float GetOverlap(Vec3f* vertices0, int32_t numvertices0, int32_t* indices0, int32_t numindices0,
    Vec3f* vertices1, int32_t numvertices1, int32_t* indices1, int32_t numindices1, Matrix44f &mat)
{
    std::vector<Vec3f> vtx0(numvertices0);
    memcpy(vtx0.data(), vertices0, numvertices0 * sizeof(Vec3f));

    AABoxf bbox0;
    for (int vIdx = 0; vIdx < numvertices0; ++vIdx)
    {
        Point3f out;
        xform(out, mat, Point3f(vertices0[vIdx]));
        vtx0[vIdx] = Vec4f(out);
        bbox0 += vtx0[vIdx];
    }

    AABoxf bbox1;
    for (int vIdx = 0; vIdx < numvertices1; ++vIdx)
    {
        bbox1 += vertices1[vIdx];
    }

    Vec3f offset = (midpt(bbox1) - midpt(bbox0));
    for (Vec3f& v : vtx0)
    {
        v += offset;
    }

    float worst = 1;
    for (int d = 0; d < 3; ++d)
    {
        std::vector<std::vector<Vec4>> unionPolys0;
        GetUnion(vtx0.data(), indices0, numindices0, unionPolys0, d);
        double a0 = AreaPolys(unionPolys0);

        std::vector<std::vector<Vec4>> unionPolys1;
        GetUnion(vertices1, indices1, numindices1, unionPolys1, d);
        double a1 = AreaPolys(unionPolys1);

        if ((std::min(a0, a1) / std::max(a0, a1)) < 0.5f)
            return -1;

        PolygonClipper intersect;
        intersect.AddPolygons(unionPolys0);
        intersect.AddPolygons(unionPolys1);
        std::vector<std::vector<Vec4>> iPolys;
        intersect.Execute(iPolys, 2);

        double a2 = AreaPolys(iPolys);

        worst = std::min(worst, (float)(a2 / (std::max(a0, a1))));

    }

    mat = makeTrans<Matrix44f>(Point3f(offset)) * mat;
    return worst;
}

extern "C" __declspec(dllexport) void FindOrientation(Vec3f *vertices0, int32_t numvertices0, int32_t *indices0, int32_t numindices0,
    Vec3f *vertices1, int32_t numvertices1, int32_t * indices1, int32_t numindices1, float *outMatrix)
{    
    Matrix44f bestMat;
    float bestScore = 0;
    for (int sx = 0; sx < 8; ++sx)
    {
        int numneg = ((sx & 1) ? 1 : 0) +
            ((sx & 2) ? 1 : 0) +
            ((sx & 4) ? 1 : 0);
        if (numneg & 1) continue;

        for (int rotIdx = 0; rotIdx < 4; ++rotIdx)
        {
            Matrix44f mat =
                makeRot<Matrix44f>(AxisAnglef(Math::PI_OVER_4 * rotIdx, Vec3f(0, 1, 0))) *
                makeScale<Matrix44f>(Vec3f(
                    (sx & 1) == 0 ? 1 : -1,
                    (sx & 2) == 0 ? 1 : -1,
                    (sx & 4) == 0 ? 1 : -1));
            float score = GetOverlap(vertices0, numvertices0, indices0, numindices0,
                vertices1, numvertices1, indices1, numindices1, mat);
            if (score > bestScore)
            {
                bestScore = score;
                bestMat = mat;
            }
        }
    }

    if (bestScore > 0.8f)
    {
        memcpy(outMatrix, bestMat.mData, sizeof(float) * 16);
        return;
    }

    bestScore = 0;
    for (int sx = 0; sx < 8; ++sx)
    {
        int numneg = ((sx & 1) ? 1 : 0) +
            ((sx & 2) ? 1 : 0) +
            ((sx & 4) ? 1 : 0);
        if (numneg & 1) continue;
        for (int rotIdx = 0; rotIdx < 8; ++rotIdx)
        {
            Matrix44f mat =
                makeRot<Matrix44f>(AxisAnglef(Math::PI_OVER_4 * rotIdx, Vec3f(1, 0, 0))) *
                makeScale<Matrix44f>(Vec3f(
                    (sx & 1) == 0 ? 1 : -1,
                    (sx & 2) == 0 ? 1 : -1,
                    (sx & 4) == 0 ? 1 : -1));
            float score = GetOverlap(vertices0, numvertices0, indices0, numindices0,
                vertices1, numvertices1, indices1, numindices1, mat);
            if (score > bestScore)
            {
                bestScore = score;
                bestMat = mat;
            }
        }
    }

    memcpy(outMatrix, bestMat.mData, sizeof(float) * 16);
}
