////////////////////////////////////////////////////////////////////////////////////////////////////
//  COPYRIGHT © 2014 by WSI Corporation
////////////////////////////////////////////////////////////////////////////////////////////////////
/// \file PolygonClipper.h
/// Declaration for a polygon clipper.  Uses vatti algorithm.
/// \author Shane Morrison

// double: Used for template method specialization, to improve precision
// int32_t: Used to work with persistent object

#pragma once

#include <vector>

struct Vec4
{
    Vec4(double _x, double _y, double _z, double _w) :
        x(_x), y(_y), z(_z), w(_w) {}
    Vec4() :
        x(0), y(0), z(0), w() {}
    double x;
    double y;
    double z;
    double w;
};

inline bool operator == (const Vec4& l, const Vec4& r)
{
    return l.x == r.x && l.y == r.y && l.z == r.z && l.w == r.w;
}

struct Vec2
{
    Vec2(double _x, double _y) :
        x(_x), y(_y) {}
    Vec2(const Vec4& v) :
        x(v.x),
        y(v.y) {}
    Vec2() :
        x(0), y(0) {}
    double x;
    double y;
};

////////////////////////////////////////////////////////////////////////////////////////////////////

class PolygonClipper
{
public:
    enum EdgeSide { ESLeft = 1, ESRight = 2 };
    enum IntersectProtects { ipNone = 0, ipLeft = 1, ipRight = 2, ipBoth = 3 };

    struct Pt
    {
        double x;
        double y;
        double u;
        double v;
    };

    struct Edge
    {
        Pt bot;
        Pt cur;
        Pt top;
        double dx;
        double du;
        double dv;
        double deltaX;
        double deltaY;
        double deltaU;
        double deltaV;
        EdgeSide side;
        int32_t windDelta; //1 or -1 depending on winding direction
        int32_t windCnt;
        int32_t outIdx;
        Edge* next;
        Edge* prev;
        Edge* nextInLML;
        Edge* nextInAEL;
        Edge* prevInAEL;
        Edge* nextInSEL;
        Edge* prevInSEL;

        void Init(Edge* eNext, Edge* ePrev, const Vec4& pt);
        void SetDx();
        void SwapX();
    };

    struct LocalMinima
    {
        double Y;
        Edge* leftBound;
        Edge* rightBound;
        LocalMinima* next;
    };

    struct Scanbeam
    {
        double Y;
        Scanbeam* next;
    };

    struct PolyNode;

    struct OutPt
    {
        int32_t idx;
        Vec4 pt;
        OutPt* next;
        OutPt* prev;
    };

    struct JoinRec
    {
        Vec4 pt1a;
        Vec4 pt1b;
        int32_t poly1Idx;
        Vec4 pt2a;
        Vec4 pt2b;
        int32_t poly2Idx;
    };

    struct HorzJoinRec
    {
        Edge* edge;
        int32_t savedIdx;
    };

    struct OutRec
    {
        int32_t idx;
        bool isHole;
        OutRec* FirstLeft;  //see comments in clipper.pas
        PolyNode* polyNode;
        OutPt* pts;
        OutPt* bottomPt;
    };

    struct IntersectNode
    {
        Edge* edge1;
        Edge* edge2;
        Vec4 pt;
        IntersectNode* next;
    };

    enum WindRule
    {
        Equal,
        GreaterOrEqual
    };

public:
    PolygonClipper(const double precisionScale = 1000000.0f);
    ~PolygonClipper();

    bool AddPolygon(const std::vector<Vec4>& pg);
    bool AddPolygons(const std::vector<std::vector<Vec4>>& pg);
    bool Execute(std::vector<std::vector<Vec4>>& outPolys, int32_t resultWindCnt = 1, WindRule rule = GreaterOrEqual);
    static bool Orientation(const std::vector<Vec4>& poly);

private:
    Edge* AddBoundsToLML(Edge* e);
    void InsertLocalMinima(LocalMinima* newLm);

    double PopScanbeam();
    void InsertScanbeam(const double Y);
    void InsertLocalMinimaIntoAEL(const double botY);
    void InsertEdgeIntoAEL(Edge* edge);
    void SetWindingCount(Edge& edge);
    void AddEdgeToSEL(Edge* edge);
    bool IsContributing(const Edge& edge) const;
    bool IsContributingCnt(int32_t windCnt, int32_t windDelta) const;
    void AddLocalMinPoly(Edge* e1, Edge* e2, const Vec4& pt);
    void AddOutPt(Edge* e, const Vec4& pt);
    void SetHoleState(Edge* e, OutRec* outrec);
    OutRec* CreateOutRec();
    OutRec* GetOutRec(int32_t idx);
    void AddJoin(Edge* e1, Edge* e2, int32_t e1OutIdx = -1, int32_t e2OutIdx = -1);
    void IntersectEdges(Edge* e1, Edge* e2, const Vec4& pt,
        const IntersectProtects protects, bool swapLeftRightWindCnts = false);
    void AddLocalMaxPoly(Edge* e1, Edge* e2, const Vec4& pt);
    void AppendPolygon(Edge* e1, Edge* e2);
    void DeleteFromAEL(Edge* e);
    void PopLocalMinima();
    bool ExecuteInternal();
    void Reset();
    void ProcessHorizontals();
    void DeleteFromSEL(Edge* e);
    void ProcessHorizontal(Edge* horzEdge);
    bool IsTopHorz(const double XPos);
    void SwapPositionsInAEL(Edge* edge1, Edge* edge2);
    void SwapPositionsInSEL(Edge* edge1, Edge* edge2);
    void UpdateEdgeIntoAEL(Edge*& e);
    bool ProcessIntersections(const double botY, const double topY);
    void BuildIntersectList(const double botY, const double topY);
    void ProcessIntersectList();
    bool FixupIntersectionOrder();
    void InsertIntersectNode(Edge* e1, Edge* e2, const Vec4& pt);
    void DisposeIntersectNodes();
    void CopyAELToSEL();
    void ProcessEdgesAtTopOfScanbeam(const double topY);
    void DoMaxima(Edge* e, double topY);
    void ClearJoins();
    void AddHorzJoin(Edge* e, int32_t idx);
    void ClearHorzJoins();
    void DisposeAllPolyPts();
    void DisposeOutRec(uint32_t index);
    void FixupOutPolygon(OutRec& outrec);
    void BuildResult(std::vector<std::vector<Vec4>>& polys);
    void Clear();
    void DisposeLocalMinimaList();

private:
    LocalMinima* m_CurrentLM;
    LocalMinima* m_MinimaList;
    std::vector<Edge*> m_edges;

    Scanbeam* m_Scanbeam;
    Edge* m_ActiveEdges;
    Edge* m_SortedEdges;
    std::vector<OutRec*> m_PolyOuts;
    std::vector<JoinRec*> m_Joins;
    std::vector<HorzJoinRec*> m_HorizJoins;
    IntersectNode* m_IntersectNodes;
    int32_t m_resultWindCnt;
    WindRule m_resultWindRule;
};

enum JoinType { jtSquare, jtRound, jtMiter };
enum EndType { etClosed, etButt, etSquare, etRound };

void BuildPolyline(const std::vector<std::vector<Vec4>>& inLines, std::vector<std::vector<Vec4>>& outPolys,
    double delta, JoinType jointype, EndType endtype, double limit, double Uval);

void OffsetPolygons(const std::vector<std::vector<Vec4>>& inPolysC, std::vector<std::vector<Vec4>>& outPolys,
    double delta, JoinType jointype, double limit, double Uval, bool autofix);

const double sPrecisionScale = 1000000.0f;
