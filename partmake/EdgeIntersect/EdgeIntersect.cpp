// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <math.h>
#include <vector>
#include <map>
#include <algorithm>
#include <sstream>



static double epsilon = 0.0000001f;
static bool epsEq(double a, double b)
{
    double e = a - b;
    return (e > -epsilon && e < epsilon);
}


struct Vector2
{
    Vector2(double _x, double _y) :
        X(_x),
        Y(_y) {}
    Vector2() {}
    double X;
    double Y;
};

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


inline Vector3 operator + (const Vector3& lhs, const Vector3& rhs)
{
    return Vector3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
}

inline Vector3 operator - (const Vector3& lhs, const Vector3& rhs)
{
    return Vector3(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
}

inline Vector3 operator * (const Vector3& lhs, const double& t)
{
    return Vector3(lhs.X * t, lhs.Y * t, lhs.Z * t);
}

inline double dot(const Vector3& lhs, const Vector3& rhs)
{
    return lhs.X * rhs.X +
        lhs.Y * rhs.Y +
        lhs.Z * rhs.Z;
}
inline Vector3 operator * (const double& t, const Vector3& lhs)
{
    return Vector3(lhs.X * t, lhs.Y * t, lhs.Z * t);
}
static bool epsEq(Vector3 a, Vector3 v)
{
    return (a - v).LengthSquared() < (epsilon * 4);
}


struct Edge
{
    Vector3* v0;
    Vector3* v1;
    Vector3 dir;
    int idx;

    Edge(Vector3* _v0, Vector3* _v1, int _idx)
    {
        v0 = _v0;
        v1 = _v1;
        idx = _idx;
        dir = (*v1 - *v0).Normalized();
    }

    Edge() {}

    Vector3 Interp(double t)
    {
        return *v0 + (*v1 - *v0) * t;
    }

    int CompareTo(const Edge& other) const
    {
        int64_t diff = v0 - other.v0;
        if (diff != 0) return diff < 0 ? -1 : 1;
        diff = v1 - other.v1;
        if (diff != 0) return diff < 0 ? -1 : 1;
        return 0;
    }

    double DistanceFromPt(const Vector3& p) const
    {
        const Vector3& v = *v0;
        const Vector3& w = *v1;
        double l2 = (v - w).LengthSquared();  // i.e. |w-v|^2 -  avoid a sqrt
        double t = max(0, min(1, dot(p - v, w - v) / l2));
        Vector3 projection = v + t * (w - v);  // Projection falls on the segment
        return (p - projection).LengthSquared();
    }

    static bool Intersect2D(Vector2 A, Vector2 B, Vector2 C, Vector2 D, Vector2& O)
    {
        // Line AB represented as a1x + b1y = c1 
        double a1 = B.Y - A.Y;
        double b1 = A.X - B.X;
        double c1 = a1 * (A.X) + b1 * (A.Y);

        // Line CD represented as a2x + b2y = c2 
        double a2 = D.Y - C.Y;
        double b2 = C.X - D.X;
        double c2 = a2 * (C.X) + b2 * (C.Y);

        double determinant = a1 * b2 - a2 * b1;

        if (epsEq(determinant, 0))
        {
            O = Vector2(0, 0);
            return false;
        }
        else
        {
            double x = (b2 * c1 - b1 * c2) / determinant;
            double y = (a1 * c2 - a2 * c1) / determinant;
            O = Vector2(x, y);
            return true;
        }
    }

    static bool IntersectV(Vector3 A, Vector3 B, Vector3 C, Vector3 D, double& ot0,
        double& ot1)
    {
        Vector2 i;
        if (Intersect2D(Vector2(A.X, A.Y), Vector2(B.X, B.Y),
            Vector2(C.X, C.Y), Vector2(D.X, D.Y), i))
        {
            double t0 = abs(B.X - A.X) > abs(B.Y - A.Y) ?
                (i.X - A.X) / (B.X - A.X) :
                (i.Y - A.Y) / (B.Y - A.Y);
            double t1 = abs(D.X - C.X) > abs(D.Y - C.Y) ?
                (i.X - C.X) / (D.X - C.X) :
                (i.Y - C.Y) / (D.Y - C.Y);
            if (t0 > epsilon && t0 < (1 - epsilon) &&
                t1 > epsilon && t1 < (1 - epsilon))
            {
                double Z0 = A.Z + t0 * (B.Z - A.Z);
                double Z1 = C.Z + t1 * (D.Z - C.Z);
                ot0 = t0;
                ot1 = t1;
                if (epsEq(Z0, Z1))
                    return true;
            }
        }
        ot0 = ot1 = -1;
        return false;
    }

    bool Intersect(Edge b, Vector3& ipt)
    {
        Vector3 A, B, C, D;
        int xyzshuffle = 0;
        if (epsEq(dir.X, 0) && epsEq(b.dir.X, 0))
        {
            xyzshuffle = 1;
            A = Vector3(v0->Y, v0->Z, v0->X);
            B = Vector3(v1->Y, v1->Z, v1->X);
            C = Vector3(b.v0->Y, b.v0->Z, b.v0->X);
            D = Vector3(b.v1->Y, b.v1->Z, b.v1->X);
        }
        else if (epsEq(dir.Y, 0) && epsEq(b.dir.Y, 0))
        {
            xyzshuffle = 2;
            A = Vector3(v0->X, v0->Z, v0->Y);
            B = Vector3(v1->X, v1->Z, v1->Y);
            C = Vector3(b.v0->X, b.v0->Z, b.v0->Y);
            D = Vector3(b.v1->X, b.v1->Z, b.v1->Y);
        }
        else
        {
            A = *v0;
            B = *v1;
            C = *b.v0;
            D = *b.v1;
        }

        double t0, t1;
        bool intersect = IntersectV(A, B, C, D, t0, t1);
        if (intersect)
        {
            ipt = A + t0 * (B - A);
            if (xyzshuffle == 1)
                ipt = Vector3(ipt.Z, ipt.X, ipt.Y);
            else if (xyzshuffle == 2)
                ipt = Vector3(ipt.X, ipt.Z, ipt.Y);
        }
        else
            ipt = Vector3(0, 0, 0);
        return intersect;
    }
};

struct EdgePart
{
    Edge e;
    double t0;
    double t1;

    Vector3 v0;
    Vector3 v1;

    EdgePart(const Edge& _e, double _t0, double _t1)
        : e(_e),
        t0(_t0),
        t1(_t1)
    {
        v0 = e.Interp(t0);
        v1 = e.Interp(t1);
    }

    bool IsParallel(int dim)
    {
        if (dim == 0)
        {
            return epsEq(e.v1->X - e.v0->X, 0);
        }
        else if (dim == 1)
        {
            return epsEq(e.v1->Y - e.v0->Y, 0);
        }
        else
        {
            return epsEq(e.v1->Z - e.v0->Z, 0);
        }
    }

    int CompareTo(const EdgePart& other) const
    {
        return e.CompareTo(other.e);
    }

    EdgePart GetSubPart(double d0, double d1, int dim)
    {
        if (IsParallel(dim))
            return *this;

        double nt0, nt1;
        if (dim == 0)
        {
            nt0 = (d0 - e.v0->X) / (e.v1->X - e.v0->X);
            nt1 = (d1 - e.v0->X) / (e.v1->X - e.v0->X);

        }
        else if (dim == 1)
        {

            nt0 = (d0 - e.v0->Y) / (e.v1->Y - e.v0->Y);
            nt1 = (d1 - e.v0->Y) / (e.v1->Y - e.v0->Y);

        }
        else
        {

            nt0 = (d0 - e.v0->Z) / (e.v1->Z - e.v0->Z);
            nt1 = (d1 - e.v0->Z) / (e.v1->Z - e.v0->Z);

        }

        if (isinf(nt0) || isinf(nt1) ||
            isnan(nt0) || isnan(nt1))
            throw;
        return EdgePart(e, nt0, nt1);
    }
};

bool operator == (const EdgePart& lhs, const EdgePart& rhs)
{
    return lhs.CompareTo(rhs) == 0;
}

struct EdgeBreak
{
    enum BreakType { Start, End };
    BreakType Break;
    double V;
    EdgePart e;
};

bool operator < (const EdgeBreak& lhs, const EdgeBreak& rhs)
{
    if (lhs.V != rhs.V)
        return lhs.V < rhs.V;
    if (lhs.Break != rhs.Break)
        return lhs.Break < rhs.Break;
    int c = lhs.e.CompareTo(rhs.e);
    if (c < 0)
        return true;
    else
        return false;
}

struct Intersection
{
    Edge e1;
    Edge e2;
    Vector3 pt;

    Intersection(const Edge& _e1, const Edge& _e2, const Vector3& _pt) :
        pt(_pt)
    {
        int c = _e1.CompareTo(_e2);
        if (c < 0)
        {
            e1 = _e1; e2 = _e2;
        }
        else
        {
            e1 = _e2;
            e2 = _e1;
        }
    }

    int CompareTo(const Intersection& other) const
    {
        int c = e1.CompareTo(other.e1);
        if (c != 0) return c;
        return e2.CompareTo(other.e2);
    }
};

bool operator < (const Intersection& lhs, const Intersection& rhs)
{
    int c = lhs.CompareTo(rhs);
    return c < 0;
}

bool operator == (const Intersection& lhs, const Intersection& rhs)
{
    int c = lhs.CompareTo(rhs);
    return c == 0;
}


struct EdgeIntersect
{

    static void AddToSort(EdgePart e, std::map<double,
        std::vector<EdgeBreak>> &edgesXSort, int dim)
    {
        EdgeBreak beginbrk
        {
            EdgeBreak::BreakType::Start,
            min(e.v0[dim], e.v1[dim]),
            e = e
        };
        EdgeBreak endbrk
        {
            EdgeBreak::BreakType::End,
            max(e.v0[dim], e.v1[dim]),
            e
        };
        auto itXSort = edgesXSort.find(beginbrk.V);
        if (itXSort == edgesXSort.end())
        {
            itXSort = edgesXSort.insert(std::make_pair(beginbrk.V,
                std::vector<EdgeBreak>())).first;
        }
        itXSort->second.push_back(beginbrk);

        itXSort = edgesXSort.find(endbrk.V);
        if (itXSort == edgesXSort.end())
        {
            itXSort = edgesXSort.insert(std::make_pair(endbrk.V,
                std::vector<EdgeBreak>())).first;
        }
        itXSort->second.push_back(endbrk);
    }

    int checkCount = 0;
    std::vector<Intersection> FindAllIntersections(const std::vector<Edge>& edges)
    {
        checkCount = 0;
        std::vector<Intersection> intersections;
        std::vector<EdgePart> edgeParts;
        for (const Edge& e : edges)
        {
            edgeParts.push_back(EdgePart(e, 0, 1));
        }
        FindAllIntersectionsInternal(edgeParts, intersections, 0);
        std::sort(intersections.begin(), intersections.end());
        intersections.erase(std::unique(intersections.begin(), intersections.end()),
            intersections.end());
        return intersections;
    }

    void FindAllIntersectionsInternal(const std::vector<EdgePart>& edges, std::vector<Intersection>& intersections, int dim)
    {
        std::map<double,
            std::vector<EdgeBreak>> edgesXSort;

        for (const EdgePart& e : edges)
        {
            AddToSort(e, edgesXSort, dim);
        }

        edgesXSort.insert(std::make_pair(1.0e20, std::vector<EdgeBreak>()));
        std::vector<EdgePart> activeEdges;
        std::vector<EdgeBreak> curEdges;
        double curXVal = 0;
        for (auto& kv : edgesXSort)
        {
            double nextXVal = kv.first;
            if (curEdges.size() > 0)
            {
                for (const EdgeBreak& eb : curEdges)
                {
                    if (eb.Break == EdgeBreak::BreakType::Start)
                        activeEdges.push_back(eb.e);
                }

                if (dim < 2)
                {
                    std::vector<EdgePart> subEdges;
                    subEdges.reserve(activeEdges.size());
                    for (auto& ep : activeEdges)
                    {
                        subEdges.push_back(ep.GetSubPart(curXVal, nextXVal, dim));
                    }

                    FindAllIntersectionsInternal(subEdges, intersections, dim + 1);
                }
                else
                {
                    for (EdgePart& e1 : activeEdges)
                    {
                        for (EdgePart& e2 : activeEdges)
                        {
                            if (e1.e.v0 == e2.e.v0 ||
                                e1.e.v0 == e2.e.v1 ||
                                e1.e.v1 == e2.e.v0 ||
                                e1.e.v1 == e2.e.v1)
                                continue;
                            Vector3 ipt;
                            checkCount++;
                            if (e1.e.Intersect(e2.e, ipt))
                            {
                                Intersection i(e1.e, e2.e, ipt);
                                intersections.push_back(i);
                            }
                            checkCount++;
                        }
                    }
                    checkCount += activeEdges.size();
                }

                for (EdgeBreak& eb : curEdges)
                {
                    if (eb.Break == EdgeBreak::BreakType::End)
                    {
                        auto itAE = std::find(activeEdges.begin(), activeEdges.end(), eb.e);
                        if (itAE != activeEdges.end())
                            activeEdges.erase(itAE);
                    }
                }
            }

            std::swap(curEdges, kv.second);
            curXVal = kv.first;
        }
    }
};


struct OutIntersection
{
    Vector3 pt;
    int e1;
    int e2;
};

extern "C" __declspec(dllexport) int FindAllIntersections(double* vtxVals, int vtxCount, int* edgeVals, int edgeCount,
    OutIntersection * oi, int maxIntersections)
{
    Vector3* vertices = (Vector3*)vtxVals;
    int* curVtx = edgeVals;
    std::vector<Edge> edges;
    for (int idx = 0; idx < edgeCount; ++idx)
    {
        int idx0 = *curVtx++;
        int idx1 = *curVtx++;
        edges.push_back(Edge(vertices + idx0, vertices + idx1, idx));
    }

    Vector3 ipt;
    EdgeIntersect ei;
    std::vector<Intersection> intersections =
        ei.FindAllIntersections(edges);

    OutIntersection* curOI = oi;
    int icnt = 0;
    for (const Intersection& i : intersections)
    {
        //if (!epsEq() __debugbreak();
        //if (!epsEq() __debugbreak();

        double dist1 = i.e1.DistanceFromPt(i.pt);
        double dist2 = i.e2.DistanceFromPt(i.pt);
        double dpt = (i.pt - *i.e1.v0).LengthSquared() /
            (*i.e1.v1 - *i.e1.v0).LengthSquared();
        curOI->e1 = i.e1.idx;
        curOI->e2 = i.e2.idx;
        curOI->pt = i.pt;
        curOI++;
        if (icnt++ == maxIntersections)
            break;
    }

    return intersections.size();
}