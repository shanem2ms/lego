#include "pch.h"
#include "VHACD.h"

#include <cmath>
#include <string>
#include <sstream>
#include <vector>
using namespace VHACD;
struct Vector3
{
    Vector3(double _x, double _y, double _z) :
        X(_x),
        Y(_y),
        Z(_z) {}

    Vector3() {}

    double X;
    double Y;
    double Z;

    double LengthSquared() { return X * X + Y * Y + Z * Z; }

    Vector3 Normalized() {
        double mul = 1 / sqrt(LengthSquared());
        return Vector3(X * mul, Y * mul, Z * mul);
    }

    inline const double& operator [] (int x) const { return *(((double*)this) + x); }
};
extern "C" __declspec(dllexport) int ConvexDecomp(double* pointListDbls, int numTriangles,
    Vector3 * outPointListDbls, int maxPoints, int *outNumTriangles, int maxTris)
{
    IVHACD* interfaceVHACD = CreateVHACD();
    std::vector<uint32_t> triangles;
    for (int i = 0; i < numTriangles * 3; i++)
    {
        triangles.push_back(i);
    }
    IVHACD::Parameters m_paramsVHACD;
    m_paramsVHACD.m_resolution *= 1;
    bool res = interfaceVHACD->Compute(pointListDbls, numTriangles * 3, triangles.data(), numTriangles,
        m_paramsVHACD);
    if (res)
    {
        IVHACD::ConvexHull ch;
        uint32_t vertexOffset = 0;
        uint32_t nConvexHulls = interfaceVHACD->GetNConvexHulls();
        Vector3* outCur = outPointListDbls;
        int* pOutTriCur = outNumTriangles;
        int nOutPoints = 0;
        for (unsigned int p = 0; p < nConvexHulls; ++p) {
            interfaceVHACD->GetConvexHull(p, ch);
            Vector3* inPts = (Vector3 *)ch.m_points;
            vertexOffset += ch.m_nTriangles * 3;
            for (int t = 0; t < ch.m_nTriangles; ++t)
            {
                uint32_t t0 = ch.m_triangles[t * 3];
                memcpy(outCur, inPts + t0, sizeof(double) * 3);
                nOutPoints++;
                if (nOutPoints == maxPoints)
                    break;
                outCur++;
                uint32_t t1 = ch.m_triangles[t * 3 + 1];
                memcpy(outCur, inPts + t1, sizeof(double) * 3);
                nOutPoints++;
                if (nOutPoints == maxPoints)
                    break;
                outCur++;
                uint32_t t2 = ch.m_triangles[t * 3 + 2];                
                memcpy(outCur, inPts + t2, sizeof(double) * 3);
                nOutPoints++;
                if (nOutPoints == maxPoints)
                    break;
                outCur++;
            }
            if (nOutPoints == maxPoints)
                break;

            *pOutTriCur++ = vertexOffset;
        }
        interfaceVHACD->Release();
        return (nOutPoints == maxPoints) ? -1 : nConvexHulls;
    }
    interfaceVHACD->Release();
    return -1;
}