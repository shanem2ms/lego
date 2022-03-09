#include "pch.h"
#include "PolygonClipper.h"

#include <cmath>
#include <string>
#include <sstream>

static int64_t const hiRange = 0x3FFFFFFFFFFFFFFFLL;
#define HORIZONTAL (-1.0E+40)
#define TOLERANCE (1.0e-20)
#define NEAR_ZERO(val) (((val) > -TOLERANCE) && ((val) < TOLERANCE))
#define NEAR_EQUAL(a, b) NEAR_ZERO((a) - (b))
#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

bool SlopesEqual(const typename PolygonClipper::Edge& e1, const typename PolygonClipper::Edge& e2)
{
    return e1.deltaY * e2.deltaX == e1.deltaX * e2.deltaY;
}

////////////////////////////////////////////////////////////////////////////////////////////////////

 bool SlopesEqual(const Vec4& pt1, const Vec4& pt2, const Vec4& pt3)
{
    return (pt1.y - pt2.y) * (pt2.x - pt3.x) == (pt1.x - pt2.x) * (pt2.y - pt3.y);
}


////////////////////////////////////////////////////////////////////////////////////////////////////


PolygonClipper::PolygonClipper(const double precisionScale)
{
    m_Scanbeam = 0;
    m_ActiveEdges = 0;
    m_SortedEdges = 0;
    m_IntersectNodes = 0;
    m_MinimaList = 0;
    m_CurrentLM = 0;
    m_resultWindCnt = 1;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


PolygonClipper::~PolygonClipper()
{
    Clear();
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::Edge::SetDx()
{
    deltaX = (top.x - bot.x);
    deltaY = (top.y - bot.y);
    deltaU = (top.u - bot.u);
    deltaV = (top.v - bot.v);

    if (deltaY == 0)
    {
        dx = HORIZONTAL;
    }
    else
    {
        dx = (double)(deltaX) / deltaY;
        du = (double)(deltaU) / deltaY;
        dv = (double)(deltaV) / deltaY;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::Edge::SwapX()
{
    //swap horizontal edges' top and bottom x's so they follow the natural
    //progression of the bounds - ie so their bot.xs will align with the
    //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]
    cur.x = top.x;
    top.x = bot.x;
    bot.x = cur.x;
    cur.u = top.u;
    top.u = bot.u;
    bot.u = cur.u;
    cur.v = top.v;
    top.v = bot.v;
    bot.v = cur.v;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::Edge::Init(Edge* eNext, Edge* ePrev, const Vec4& pt)
{
    std::memset(this, 0, sizeof(Edge));
    this->next = eNext;
    this->prev = ePrev;
    this->cur.x = pt.x;
    this->cur.y = pt.y;
    this->cur.u = pt.z;
    this->cur.v = pt.w;
    if (this->cur.y >= this->next->cur.y)
    {
        this->bot = this->cur;
        this->top = this->next->cur;
        this->windDelta = 1;
    }
    else
    {
        this->top = this->cur;
        this->bot = this->next->cur;
        this->windDelta = -1;
    }

    SetDx();

    this->outIdx = -1;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::AddPolygons(const std::vector<std::vector<Vec4>>& pg)
{
    bool succeeded = true;
    for (const auto& it : pg)
    {
        succeeded &= PolygonClipper::AddPolygon(it);
    }

    return succeeded;
}

inline bool EqXY(const Vec4& pt1, const Vec4& pt2)
{
    return pt1 == pt2;//(pt1.GetX() == pt2.GetX() && pt1.GetY() == pt2.GetY());
}
////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::AddPolygon(const std::vector<Vec4>& pg)
{
    int32_t len = (int32_t)pg.size();
    if (len < 3)
    {
        return false;
    }

    double maxVal = (double)hiRange;

    std::vector<Vec4> p(len);
    p[0] = pg[0];
    int32_t j = 0;

    for (int32_t i = 0; i < len; ++i)
    {
        if (i == 0 || EqXY(p[j], pg[i]))
        {
            continue;
        }
        else if (j > 0 && SlopesEqual(p[j - 1], p[j], pg[i]))
        {
            if (EqXY(p[j - 1], pg[i]))
            {
                j--;
            }
        }
        else
        {
            j++;
        }
        p[j] = pg[i];
    }
    if (j < 2)
    {
        return false;
    }

    len = j + 1;
    while (len > 2)
    {
        //nb: test for point equality before testing slopes ...
        if (EqXY(p[j], p[0]))
        {
            j--;
        }
        else if (EqXY(p[0], p[1]) || SlopesEqual(p[j], p[0], p[1]))
        {
            p[0] = p[j--];
        }
        else if (SlopesEqual(p[j - 1], p[j], p[0]))
        {
            j--;
        }
        else if (SlopesEqual(p[0], p[1], p[2]))
        {
            for (int32_t i = 2; i <= j; ++i)
            {
                p[i - 1] = p[i];
            }
            j--;
        }
        else
        {
            break;
        }
        len--;
    }

    if (len < 3)
    {
        return false;
    }

    //create a new edge array ...
    Edge* edges = new Edge[len];
    m_edges.push_back(edges);

    //convert vertices to a double-linked-list of edges and initialize ...
    edges[0].cur.x = p[0].x;
    edges[0].cur.y = p[0].y;
    edges[0].cur.u = p[0].z;
    edges[0].cur.v = p[0].w;
    edges[len - 1].Init(&edges[0], &edges[len - 2], p[len - 1]);
    for (int32_t i = len - 2; i > 0; --i)
    {
        edges[i].Init(&edges[i + 1], &edges[i - 1], p[i]);
    }
    edges[0].Init(&edges[1], &edges[len - 1], p[0]);

    //reset cur.x & cur.y and find 'eHighest' (given the Y axis coordinates
    //increase downward so the 'highest' edge will have the smallest top.y) ...
    Edge* e = &edges[0];
    Edge* eHighest = e;
    do
    {
        e->cur = e->bot;
        if (e->top.y < eHighest->top.y)
        {
            eHighest = e;
        }
        e = e->next;
    } while (e != &edges[0]);

    //make sure eHighest is positioned so the following loop works safely ...
    if (eHighest->windDelta > 0)
    {
        eHighest = eHighest->next;
    }
    if (NEAR_EQUAL(eHighest->dx, HORIZONTAL))
    {
        eHighest = eHighest->next;
    }

    //finally insert each local minima ...
    e = eHighest;
    do
    {
        e = AddBoundsToLML(e);
    } while (e != eHighest);
    return true;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::Edge* PolygonClipper::AddBoundsToLML(Edge* e)
{
    //Starting at the top of one bound we progress to the bottom where there's
    //a local minima. We then go to the top of the next bound. These two bounds
    //form the left and right (or right and left) bounds of the local minima.
    e->nextInLML = 0;
    e = e->next;
    for (;;)
    {
        if (NEAR_EQUAL(e->dx, HORIZONTAL))
        {
            //nb: proceed through horizontals when approaching from their right,
            //    but break on horizontal minima if approaching from their left.
            //    This ensures 'local minima' are always on the left of horizontals.
            if (e->next->top.y < e->top.y && e->next->bot.x > e->prev->bot.x)
            {
                break;
            }
            if (e->top.x != e->prev->bot.x)
            {
                e->SwapX();
            }
            e->nextInLML = e->prev;
        }
        else if (e->cur.y == e->prev->cur.y)
        {
            break;
        }
        else
        {
            e->nextInLML = e->prev;
        }
        e = e->next;
    }

    //e and e.prev are now at a local minima ...
    LocalMinima* newLm = new LocalMinima;
    newLm->next = 0;
    newLm->Y = e->prev->bot.y;

    if (NEAR_EQUAL(e->dx, HORIZONTAL))  //horizontal edges never start a left bound
    {
        if (e->bot.x != e->prev->bot.x)
        {
            e->SwapX();
        }
        newLm->leftBound = e->prev;
        newLm->rightBound = e;
    }
    else if (e->dx < e->prev->dx)
    {
        newLm->leftBound = e->prev;
        newLm->rightBound = e;
    }
    else
    {
        newLm->leftBound = e;
        newLm->rightBound = e->prev;
    }

    newLm->leftBound->side = ESLeft;
    newLm->rightBound->side = ESRight;
    InsertLocalMinima(newLm);

    for (;;)
    {
        if (e->next->top.y == e->top.y && !NEAR_EQUAL(e->next->dx, HORIZONTAL))
        {
            break;
        }
        e->nextInLML = e->next;
        e = e->next;
        if (NEAR_EQUAL(e->dx, HORIZONTAL) && e->bot.x != e->prev->top.x)
        {
            e->SwapX();
        }
    }
    return e->next;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::InsertLocalMinima(typename PolygonClipper::LocalMinima* newLm)
{
    if (!m_MinimaList)
    {
        m_MinimaList = newLm;
    }
    else if (newLm->Y >= m_MinimaList->Y)
    {
        newLm->next = m_MinimaList;
        m_MinimaList = newLm;
    }
    else
    {
        LocalMinima* tmpLm = m_MinimaList;
        while (tmpLm->next && (newLm->Y < tmpLm->next->Y))
        {
            tmpLm = tmpLm->next;
        }
        newLm->next = tmpLm->next;
        tmpLm->next = newLm;
    }
}

/// PROCESSING

////////////////////////////////////////////////////////////////////////////////////////////////////


inline double Round(double val)
{
    return val;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


inline double Abs(double val)
{
    return val < 0 ? -val : val;
}


void SwapPoints(Vec4& pt1, Vec4& pt2)
{
    Vec4 tmp = pt1;
    pt1 = pt2;
    pt2 = tmp;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void ReversePolyPtLinks(typename PolygonClipper::OutPt* pp)
{
    if (pp == NULL)
    {
        return;
    }

    typename PolygonClipper::OutPt* pp1, * pp2;
    pp1 = pp;
    do
    {
        pp2 = pp1->next;
        pp1->next = pp1->prev;
        pp1->prev = pp2;
        pp1 = pp2;
    } while (pp1 != pp);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


double GetDx(const Vec4 pt1, const Vec4 pt2)
{
    return (pt1.y == pt2.y) ? HORIZONTAL : (double)(pt2.x - pt1.x) / (pt2.y - pt1.y);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool FirstIsBottomPt(const typename PolygonClipper::OutPt* btmPt1, const typename PolygonClipper::OutPt* btmPt2)
{
    typename PolygonClipper::OutPt* p = btmPt1->prev;
    while (p->pt == btmPt1->pt && (p != btmPt1))
    {
        p = p->prev;
    }

    double dx1p = std::fabs(GetDx(btmPt1->pt, p->pt));
    p = btmPt1->next;
    while (p->pt == btmPt1->pt && (p != btmPt1))
    {
        p = p->next;
    }
    double dx1n = std::fabs(GetDx(btmPt1->pt, p->pt));

    p = btmPt2->prev;
    while (p->pt == btmPt2->pt && (p != btmPt2))
    {
        p = p->prev;
    }
    double dx2p = std::fabs(GetDx(btmPt2->pt, p->pt));
    p = btmPt2->next;

    while (p->pt == btmPt2->pt && (p != btmPt2))
    {
        p = p->next;
    }

    double dx2n = std::fabs(GetDx(btmPt2->pt, p->pt));

    return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::OutPt* GetBottomPt(typename PolygonClipper::OutPt* pp)
{
    typename PolygonClipper::OutPt* dups = 0;
    typename PolygonClipper::OutPt* p = pp->next;
    while (p != pp)
    {
        if (p->pt.y > pp->pt.y)
        {
            pp = p;
            dups = 0;
        }
        else if (p->pt.y == pp->pt.y && p->pt.x <= pp->pt.x)
        {
            if (p->pt.x < pp->pt.x)
            {
                dups = 0;
                pp = p;
            }
            else
            {
                if (p->next != pp && p->prev != pp)
                {
                    dups = p;
                }
            }
        }
        p = p->next;
    }
    if (dups)
    {
        //there appears to be at least 2 vertices at bottomPt so ...
        while (dups != p)
        {
            if (!FirstIsBottomPt(p, dups))
            {
                pp = dups;
            }
            dups = dups->next;
            while (!(dups->pt == pp->pt))
            {
                dups = dups->next;
            }
        }
    }
    return pp;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::OutRec* GetLowermostRec(typename PolygonClipper::OutRec* outRec1, typename PolygonClipper::OutRec* outRec2)
{
    //work out which std::vector<WxVector4<int64_t>> fragment has the correct hole state ...
    if (!outRec1->bottomPt)
    {
        outRec1->bottomPt = GetBottomPt(outRec1->pts);
    }
    if (!outRec2->bottomPt)
    {
        outRec2->bottomPt = GetBottomPt(outRec2->pts);
    }
    typename PolygonClipper::OutPt* outPt1 = outRec1->bottomPt;
    typename PolygonClipper::OutPt* outPt2 = outRec2->bottomPt;
    if (outPt1->pt.y > outPt2->pt.y)
    {
        return outRec1;
    }
    else if (outPt1->pt.y < outPt2->pt.y)
    {
        return outRec2;
    }
    else if (outPt1->pt.x < outPt2->pt.x)
    {
        return outRec1;
    }
    else if (outPt1->pt.x > outPt2->pt.x)
    {
        return outRec2;
    }
    else if (outPt1->next == outPt1)
    {
        return outRec2;
    }
    else if (outPt2->next == outPt2)
    {
        return outRec1;
    }
    else if (FirstIsBottomPt(outPt1, outPt2))
    {
        return outRec1;
    }
    else
    {
        return outRec2;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool GetOverlapSegment(Vec4 pt1a, Vec4 pt1b, Vec4 pt2a,
    Vec4 pt2b, Vec4& pt1, Vec4& pt2)
{
    //precondition: segments are colinear.
    if (Abs(pt1a.x - pt1b.x) > Abs(pt1a.y - pt1b.y))
    {
        if (pt1a.x > pt1b.x)
        {
            SwapPoints(pt1a, pt1b);
        }
        if (pt2a.x > pt2b.x)
        {
            SwapPoints(pt2a, pt2b);
        }
        if (pt1a.x > pt2a.x)
        {
            pt1 = pt1a;
        }
        else
        {
            pt1 = pt2a;
        }
        if (pt1b.x < pt2b.x)
        {
            pt2 = pt1b;
        }
        else
        {
            pt2 = pt2b;
        }
        return pt1.x < pt2.x;
    }
    else
    {
        if (pt1a.y < pt1b.y)
        {
            SwapPoints(pt1a, pt1b);
        }
        if (pt2a.y < pt2b.y)
        {
            SwapPoints(pt2a, pt2b);
        }
        if (pt1a.y < pt2a.y)
        {
            pt1 = pt1a;
        }
        else
        {
            pt1 = pt2a;
        }
        if (pt1b.y > pt2b.y)
        {
            pt2 = pt1b;
        }
        else
        {
            pt2 = pt2b;
        }
        return pt1.y > pt2.y;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::PopLocalMinima()
{
    if (!m_CurrentLM)
    {
        return;
    }
    m_CurrentLM = m_CurrentLM->next;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


double PolygonClipper::PopScanbeam()
{
    double Y = m_Scanbeam->Y;
    Scanbeam* sb2 = m_Scanbeam;
    m_Scanbeam = m_Scanbeam->next;
    delete sb2;
    return Y;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::InsertScanbeam(const double Y)
{
    if (!m_Scanbeam)
    {
        m_Scanbeam = new Scanbeam;
        m_Scanbeam->next = 0;
        m_Scanbeam->Y = Y;
    }
    else if (Y > m_Scanbeam->Y)
    {
        Scanbeam* newSb = new Scanbeam;
        newSb->Y = Y;
        newSb->next = m_Scanbeam;
        m_Scanbeam = newSb;
    }
    else
    {
        Scanbeam* sb2 = m_Scanbeam;
        while (sb2->next && (Y <= sb2->next->Y))
        {
            sb2 = sb2->next;
        }
        if (Y == sb2->Y)
        {
            return;    //ie ignores duplicates
        }
        Scanbeam* newSb = new Scanbeam;
        newSb->Y = Y;
        newSb->next = sb2->next;
        sb2->next = newSb;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::InsertLocalMinimaIntoAEL(const double botY)
{
    while (m_CurrentLM && (m_CurrentLM->Y == botY))
    {
        Edge* lb = m_CurrentLM->leftBound;
        Edge* rb = m_CurrentLM->rightBound;

        InsertEdgeIntoAEL(lb);
        InsertScanbeam(lb->top.y);
        InsertEdgeIntoAEL(rb);

        rb->windDelta = -lb->windDelta;

        SetWindingCount(*lb);

        if (NEAR_EQUAL(rb->dx, HORIZONTAL))
        {
            //nb: only rightbounds can have a horizontal bottom edge
            AddEdgeToSEL(rb);
            InsertScanbeam(rb->nextInLML->top.y);
        }
        else
        {
            InsertScanbeam(rb->top.y);
        }

        if (IsContributing(*lb))
        {
            AddLocalMinPoly(lb, rb, Vec4(lb->cur.x, m_CurrentLM->Y, lb->cur.u, lb->cur.v));
        }

        //if any output std::vector<std::vector<WxVector4<int64_t>>> share an edge, they'll need joining later ...
        if (rb->outIdx >= 0 && NEAR_EQUAL(rb->dx, HORIZONTAL))
        {
            for (uint32_t i = 0; i < m_HorizJoins.size(); ++i)
            {
                Vec4 pt, pt2; //returned by GetOverlapSegment() but unused here.
                HorzJoinRec* hj = m_HorizJoins[i];
                //if horizontals rb and hj.edge overlap, flag for joining later ...
                if (GetOverlapSegment(Vec4(hj->edge->bot.x, hj->edge->bot.y, hj->edge->bot.u, hj->edge->bot.v),
                    Vec4(hj->edge->top.x, hj->edge->top.y, hj->edge->top.u, hj->edge->top.v),
                    Vec4(rb->bot.x, rb->bot.y, rb->bot.u, rb->bot.v),
                    Vec4(rb->top.x, rb->top.y, rb->top.u, rb->top.v), pt, pt2))
                {
                    AddJoin(hj->edge, rb, hj->savedIdx);
                }
            }
        }

        if (lb->nextInAEL != rb)
        {
            if (rb->outIdx >= 0 && rb->prevInAEL && rb->prevInAEL->outIdx >= 0 &&
                SlopesEqual(*rb->prevInAEL, *rb))
            {
                AddJoin(rb, rb->prevInAEL);
            }

            Edge* e = lb->nextInAEL;
            Vec4 pt = Vec4(lb->cur.x, lb->cur.y, lb->cur.u, lb->cur.v);
            while (e != rb)
            {
                if (!e)
                {
                    throw "InsertLocalMinimaIntoAEL: missing rightbound!";
                }

                IntersectEdges(rb, e, pt, ipNone, true); //order important here
                e = e->nextInAEL;
            }
        }
        PopLocalMinima();
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


double InterpX(typename PolygonClipper::Edge& edge, const double currentY)
{
    return (currentY == edge.top.y) ?
        edge.top.x : edge.bot.x + Round(edge.dx * (currentY - edge.bot.y));
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void InterpXUV(typename PolygonClipper::Edge& edge, const double currentY, double& X,
    double& U, double& V)
{
    if (currentY == edge.top.y)
    {
        X = edge.top.x;
        U = edge.top.u;
        V = edge.top.v;
    }
    else
    {
        X = edge.bot.x + Round(edge.dx * (currentY - edge.bot.y));
        U = edge.bot.u + Round(edge.du * (currentY - edge.bot.y));
        V = edge.bot.v + Round(edge.dv * (currentY - edge.bot.y));
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


bool IntersectPoint(typename PolygonClipper::Edge& edge1, typename PolygonClipper::Edge& edge2,
    Vec4& ip)
{
    double b1, b2;
    if (SlopesEqual(edge1, edge2))
    {
        if (edge2.bot.y > edge1.bot.y)
        {
            ip.y = edge2.bot.y;
            ip.w = edge2.bot.v;
        }
        else
        {
            ip.y = edge1.bot.y;
            ip.w = edge1.bot.v;
        }
        return false;
    }
    else if (NEAR_ZERO(edge1.dx))
    {
        ip.x = edge1.bot.x;
        if (NEAR_EQUAL(edge2.dx, HORIZONTAL))
        {
            ip.y = edge2.bot.y;
            ip.w = edge2.bot.v;
        }
        else
        {
            b2 = edge2.bot.y - (edge2.bot.x / edge2.dx);
            ip.y = Round(ip.x / edge2.dx + b2);
            ip.w = edge2.bot.v;
        }
    }
    else if (NEAR_ZERO(edge2.dx))
    {
        ip.x = edge2.bot.x;
        ip.z = edge2.bot.u;
        if (NEAR_EQUAL(edge1.dx, HORIZONTAL))
        {
            ip.y = edge1.bot.y;
            ip.w = edge1.bot.v;
        }
        else
        {
            b1 = edge1.bot.y - (edge1.bot.x / edge1.dx);
            ip.y = Round(ip.x / edge1.dx + b1);
            ip.w = edge1.bot.v;
        }
    }
    else
    {
        b1 = edge1.bot.x - edge1.bot.y * edge1.dx;
        b2 = edge2.bot.x - edge2.bot.y * edge2.dx;
        double q = (b2 - b1) / (edge1.dx - edge2.dx);
        ip.y = Round(q);
        if (std::fabs(edge1.dx) < std::fabs(edge2.dx))
        {
            ip.x = Round(edge1.dx * q + b1);
        }
        else
        {
            ip.x = Round(edge2.dx * q + b2);
        }
    }

    if (edge1.bot.v > 0)
    {
        ip.w = (ip.y - edge1.bot.y) * edge1.dv + edge1.bot.v;
        ip.z = (ip.y - edge1.bot.y) * edge1.du + edge1.bot.u;
    }
    else
    {
        ip.w = (ip.y - edge2.bot.y) * edge2.dv + edge2.bot.v;
        ip.z = (ip.y - edge2.bot.y) * edge2.du + edge2.bot.u;
    }

    if (ip.y < edge1.top.y || ip.y < edge2.top.y)
    {
        if (edge1.top.y > edge2.top.y)
        {
            ip = Vec4(edge1.top.x, edge1.top.y, edge1.top.u, edge1.top.v);
            return InterpX(edge2, edge1.top.y) < edge1.top.x;
        }
        else
        {
            ip = Vec4(edge2.top.x, edge2.top.y, edge2.top.u, edge2.top.v);
            return InterpX(edge1, edge2.top.y) > edge2.top.x;
        }
    }
    else
    {
        return true;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


inline bool E2InsertsBeforeE1(typename PolygonClipper::Edge& e1, typename PolygonClipper::Edge& e2)
{
    if (e2.cur.x == e1.cur.x)
    {
        if (e2.top.y > e1.top.y)
        {
            return e2.top.x < InterpX(e1, e2.top.y);
        }
        else
        {
            return e1.top.x > InterpX(e2, e1.top.y);
        }
    }
    else
    {
        return e2.cur.x < e1.cur.x;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool Param1RightOfParam2(typename PolygonClipper::OutRec* outRec1, typename PolygonClipper::OutRec* outRec2)
{
    do
    {
        outRec1 = outRec1->FirstLeft;
        if (outRec1 == outRec2)
        {
            return true;
        }
    } while (outRec1);

    return false;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void SwapSides(typename PolygonClipper::Edge& edge1, typename PolygonClipper::Edge& edge2)
{
    typename PolygonClipper::EdgeSide side = edge1.side;
    edge1.side = edge2.side;
    edge2.side = side;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void SwapPolyIndexes(typename PolygonClipper::Edge& edge1, typename PolygonClipper::Edge& edge2)
{
    int32_t outIdx = edge1.outIdx;
    edge1.outIdx = edge2.outIdx;
    edge2.outIdx = outIdx;
}
////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::InsertEdgeIntoAEL(typename PolygonClipper::Edge* edge)
{
    edge->prevInAEL = 0;
    edge->nextInAEL = 0;
    if (!m_ActiveEdges)
    {
        m_ActiveEdges = edge;
    }
    else if (E2InsertsBeforeE1(*m_ActiveEdges, *edge))
    {
        edge->nextInAEL = m_ActiveEdges;
        m_ActiveEdges->prevInAEL = edge;
        m_ActiveEdges = edge;
    }
    else
    {
        Edge* e = m_ActiveEdges;
        while (e->nextInAEL && !E2InsertsBeforeE1(*e->nextInAEL, *edge))
        {
            e = e->nextInAEL;
        }
        edge->nextInAEL = e->nextInAEL;
        if (e->nextInAEL)
        {
            e->nextInAEL->prevInAEL = edge;
        }
        edge->prevInAEL = e;
        e->nextInAEL = edge;
    }
}


////////////////////////////////////////////////////////////////////////////////////////////////////


void WindFromAEL(typename PolygonClipper::Edge* estart)
{
    typename PolygonClipper::Edge* e = estart;
    int32_t curWindCnt = 0;
    while (e != NULL)
    {
        e->windCnt = curWindCnt;
        curWindCnt += e->windDelta;
        e = e->nextInAEL;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::SetWindingCount(Edge& edge)
{
    // Do alternate winding rule
    WindFromAEL(m_ActiveEdges);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddEdgeToSEL(Edge* edge)
{
    //SEL pointers in PEdge are reused to build a list of horizontal edges.
    //However, we don't need to worry about order with horizontal edge processing.
    if (!m_SortedEdges)
    {
        m_SortedEdges = edge;
        edge->prevInSEL = 0;
        edge->nextInSEL = 0;
    }
    else
    {
        edge->nextInSEL = m_SortedEdges;
        edge->prevInSEL = 0;
        m_SortedEdges->prevInSEL = edge;
        m_SortedEdges = edge;
    }
}


////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::IsContributingCnt(int32_t windCnt, int32_t windDelta) const
{
    const int32_t windCntIn = m_resultWindCnt;
    bool result = (windCnt == (windCntIn - 1) && windDelta == 1 ||
        windCnt == windCntIn && windDelta == -1);
    if (m_resultWindRule == Equal)
    {
        result |= (windCnt == windCntIn && windDelta == 1 ||
            windCnt == (windCntIn + 1) && windDelta == -1);
    }
    return result;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::IsContributing(const Edge& edge) const
{
    return IsContributingCnt(edge.windCnt, edge.windDelta);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddLocalMinPoly(Edge* e1, Edge* e2, const Vec4& pt)
{
    Edge* e, * prevE;
    if (NEAR_EQUAL(e2->dx, HORIZONTAL) || (e1->dx > e2->dx))
    {
        AddOutPt(e1, pt);
        e2->outIdx = e1->outIdx;
        e1->side = ESLeft;
        e2->side = ESRight;
        e = e1;
        if (e->prevInAEL == e2)
        {
            prevE = e2->prevInAEL;
        }
        else
        {
            prevE = e->prevInAEL;
        }
    }
    else
    {
        AddOutPt(e2, pt);
        e1->outIdx = e2->outIdx;
        e1->side = ESRight;
        e2->side = ESLeft;
        e = e2;
        if (e->prevInAEL == e1)
        {
            prevE = e1->prevInAEL;
        }
        else
        {
            prevE = e->prevInAEL;
        }
    }

    if (prevE && prevE->outIdx >= 0 && (InterpX(*prevE, pt.y) == InterpX(*e, pt.y)) &&
        SlopesEqual(*e, *prevE))
    {
        AddJoin(e, prevE, -1, -1);
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddOutPt(Edge* e, const Vec4& pt)
{
    bool ToFront = (e->side == ESLeft);
    if (e->outIdx < 0)
    {
        OutRec* outRec = CreateOutRec();
        e->outIdx = outRec->idx;
        OutPt* newOp = new OutPt;
        outRec->pts = newOp;
        newOp->pt = pt;
        newOp->idx = outRec->idx;
        newOp->next = newOp;
        newOp->prev = newOp;
        SetHoleState(e, outRec);
    }
    else
    {
        OutRec* outRec = m_PolyOuts[e->outIdx];
        OutPt* op = outRec->pts;
        if ((ToFront && EqXY(pt, op->pt)) ||
            (!ToFront && EqXY(pt, op->prev->pt)))
        {
            return;
        }

        OutPt* newOp = new OutPt;
        newOp->pt = pt;
        newOp->idx = outRec->idx;
        newOp->next = op;
        newOp->prev = op->prev;
        newOp->prev->next = newOp;
        op->prev = newOp;
        if (ToFront)
        {
            outRec->pts = newOp;
        }
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::SetHoleState(Edge* e, typename PolygonClipper::OutRec* outrec)
{
    bool isHole = false;
    Edge* e2 = e->prevInAEL;
    while (e2)
    {
        if (e2->outIdx >= 0)
        {
            isHole = !isHole;
            if (!outrec->FirstLeft)
            {
                outrec->FirstLeft = m_PolyOuts[e2->outIdx];
            }
        }
        e2 = e2->prevInAEL;
    }
    if (isHole)
    {
        outrec->isHole = true;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::OutRec* PolygonClipper::CreateOutRec()
{
    OutRec* result = new OutRec;
    result->isHole = false;
    result->FirstLeft = 0;
    result->pts = 0;
    result->bottomPt = 0;
    result->polyNode = 0;
    m_PolyOuts.push_back(result);
    result->idx = (int32_t)m_PolyOuts.size() - 1;
    return result;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddJoin(Edge* e1, Edge* e2, int32_t e1OutIdx, int32_t e2OutIdx)
{
    JoinRec* jr = new JoinRec;
    if (e1OutIdx >= 0)
    {
        jr->poly1Idx = e1OutIdx;
    }
    else
    {
        jr->poly1Idx = e1->outIdx;
    }

    jr->pt1a = Vec4(e1->cur.x, e1->cur.y, e1->cur.u, e1->cur.v);
    jr->pt1b = Vec4(e1->top.x, e1->top.y, e1->top.u, e1->top.v);

    if (e2OutIdx >= 0)
    {
        jr->poly2Idx = e2OutIdx;
    }
    else
    {
        jr->poly2Idx = e2->outIdx;
    }

    jr->pt2a = Vec4(e2->cur.x, e2->cur.y, e2->cur.u, e2->cur.v);
    jr->pt2b = Vec4(e2->top.x, e2->top.y, e2->top.u, e2->top.v);
    m_Joins.push_back(jr);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::IntersectEdges(Edge* e1, Edge* e2,
    const Vec4& pt, const IntersectProtects protects, bool swapLeftRightWindCnts)
{
    //e1 will be to the left of e2 BELOW the intersection. Therefore e1 is before
    //e2 in AEL except when e1 is being inserted at the intersection point ...
    bool e1stops = !(ipLeft & protects) && !e1->nextInLML &&
        e1->top.x == pt.x && e1->top.y == pt.y;
    bool e2stops = !(ipRight & protects) && !e2->nextInLML &&
        e2->top.x == pt.x && e2->top.y == pt.y;
    bool e1Contributing = (e1->outIdx >= 0);
    bool e2contributing = (e2->outIdx >= 0);

    int32_t afterIntersectWnd1, afterIntersectWnd2;

    if (!swapLeftRightWindCnts)
    {
        // this is the resulting wind counts of the left side of edges after they intersect.
        // I worked this out on paper. (SAM)
        afterIntersectWnd1 = e1->windCnt + e2->windDelta;
        afterIntersectWnd2 = e2->windCnt - e1->windDelta;
    }
    else
    {
        // Same as above, but assume left/right edges have been swapped.
        // This happens when there is an intersection right at the LocalMinima.  The edges
        // are already in the "after intersection" order.
        afterIntersectWnd1 = e2->windCnt + e2->windDelta;
        afterIntersectWnd2 = e2->windCnt;
    }

    if (e1Contributing && e2contributing)
    {
        if (e1stops || e2stops)
        {
            AddLocalMaxPoly(e1, e2, pt);
        }
        else if (!IsContributingCnt(afterIntersectWnd1, e1->windDelta) &&
            !IsContributingCnt(afterIntersectWnd2, e2->windDelta))
        {
            AddLocalMaxPoly(e1, e2, pt);
        }
        else
        {
            AddOutPt(e1, pt);
            AddOutPt(e2, pt);
            SwapSides(*e1, *e2);
            SwapPolyIndexes(*e1, *e2);
        }
    }
    else if (e1Contributing)
    {
        AddOutPt(e1, pt);
        SwapSides(*e1, *e2);
        SwapPolyIndexes(*e1, *e2);
    }
    else if (e2contributing)
    {
        AddOutPt(e2, pt);
        SwapSides(*e1, *e2);
        SwapPolyIndexes(*e1, *e2);
    }
    else if (!e1stops && !e2stops &&
        IsContributingCnt(afterIntersectWnd1, e1->windDelta) &&
        IsContributingCnt(afterIntersectWnd2, e2->windDelta))
    {
        AddLocalMinPoly(e1, e2, pt);
    }
    else if (!e1stops && !e2stops)
    {
        SwapSides(*e1, *e2);
    }

    if ((e1stops != e2stops) &&
        ((e1stops && (e1->outIdx >= 0)) || (e2stops && (e2->outIdx >= 0))))
    {
        SwapSides(*e1, *e2);
        SwapPolyIndexes(*e1, *e2);
    }

    //finally, delete any non-contributing maxima edges  ...
    if (e1stops)
    {
        DeleteFromAEL(e1);
    }
    if (e2stops)
    {
        DeleteFromAEL(e2);
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddLocalMaxPoly(Edge* e1, Edge* e2, const Vec4& pt)
{
    AddOutPt(e1, pt);
    if (e1->outIdx == e2->outIdx)
    {
        e1->outIdx = -1;
        e2->outIdx = -1;
    }
    else if (e1->outIdx < e2->outIdx)
    {
        AppendPolygon(e1, e2);
    }
    else
    {
        AppendPolygon(e2, e1);
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AppendPolygon(Edge* e1, Edge* e2)
{
    //get the start and ends of both output std::vector<std::vector<WxVector4<int64_t>>> ...
    OutRec* outRec1 = m_PolyOuts[e1->outIdx];
    OutRec* outRec2 = m_PolyOuts[e2->outIdx];

    OutRec* holeStateRec;
    if (Param1RightOfParam2(outRec1, outRec2))
    {
        holeStateRec = outRec2;
    }
    else if (Param1RightOfParam2(outRec2, outRec1))
    {
        holeStateRec = outRec1;
    }
    else
    {
        holeStateRec = GetLowermostRec(outRec1, outRec2);
    }

    OutPt* p1_lft = outRec1->pts;
    OutPt* p1_rt = p1_lft->prev;
    OutPt* p2_lft = outRec2->pts;
    OutPt* p2_rt = p2_lft->prev;

    EdgeSide side;
    //join e2 poly onto e1 poly and delete pointers to e2 ...
    if (e1->side == ESLeft)
    {
        if (e2->side == ESLeft)
        {
            //z y x a b c
            ReversePolyPtLinks(p2_lft);
            p2_lft->next = p1_lft;
            p1_lft->prev = p2_lft;
            p1_rt->next = p2_rt;
            p2_rt->prev = p1_rt;
            outRec1->pts = p2_rt;
        }
        else
        {
            //x y z a b c
            p2_rt->next = p1_lft;
            p1_lft->prev = p2_rt;
            p2_lft->prev = p1_rt;
            p1_rt->next = p2_lft;
            outRec1->pts = p2_lft;
        }
        side = ESLeft;
    }
    else
    {
        if (e2->side == ESRight)
        {
            //a b c z y x
            ReversePolyPtLinks(p2_lft);
            p1_rt->next = p2_rt;
            p2_rt->prev = p1_rt;
            p2_lft->next = p1_lft;
            p1_lft->prev = p2_lft;
        }
        else
        {
            //a b c x y z
            p1_rt->next = p2_lft;
            p2_lft->prev = p1_rt;
            p1_lft->prev = p2_rt;
            p2_rt->next = p1_lft;
        }
        side = ESRight;
    }

    outRec1->bottomPt = 0;
    if (holeStateRec == outRec2)
    {
        if (outRec2->FirstLeft != outRec1)
        {
            outRec1->FirstLeft = outRec2->FirstLeft;
        }
        outRec1->isHole = outRec2->isHole;
    }
    outRec2->pts = 0;
    outRec2->bottomPt = 0;

    outRec2->FirstLeft = outRec1;

    int32_t OKIdx = e1->outIdx;
    int32_t ObsoleteIdx = e2->outIdx;

    e1->outIdx = -1; //nb: safe because we only get here via AddLocalMaxPoly
    e2->outIdx = -1;

    Edge* e = m_ActiveEdges;
    while (e)
    {
        if (e->outIdx == ObsoleteIdx)
        {
            e->outIdx = OKIdx;
            e->side = side;
            break;
        }

        e = e->nextInAEL;
    }

    outRec2->idx = outRec1->idx;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::OutRec* PolygonClipper::GetOutRec(int32_t idx)
{
    OutRec* outrec = m_PolyOuts[idx];
    while (outrec != m_PolyOuts[outrec->idx])
    {
        outrec = m_PolyOuts[outrec->idx];
    }
    return outrec;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DeleteFromAEL(Edge* e)
{
    Edge* AelPrev = e->prevInAEL;
    Edge* AelNext = e->nextInAEL;
    if (!AelPrev && !AelNext && (e != m_ActiveEdges))
    {
        return;    //already deleted
    }
    if (AelPrev)
    {
        AelPrev->nextInAEL = AelNext;
    }
    else
    {
        m_ActiveEdges = AelNext;
    }
    if (AelNext)
    {
        AelNext->prevInAEL = AelPrev;
    }
    e->nextInAEL = 0;
    e->prevInAEL = 0;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::Execute(std::vector<std::vector<Vec4>>& outPolys, int32_t resultWindCnt, WindRule rule)
{
    m_resultWindCnt = resultWindCnt;
    m_resultWindRule = rule;
    bool succeeded = ExecuteInternal();
    if (succeeded)
    {
        BuildResult(outPolys);
    }

    return succeeded;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


double Area(const typename PolygonClipper::OutRec& outRec)
{
    typename PolygonClipper::OutPt* op = outRec.pts;
    if (!op)
    {
        return 0;
    }
    double a(0);
    do
    {
        a += (op->pt.x + op->prev->pt.x) * (op->prev->pt.y - op->pt.y);
        op = op->next;
    } while (op != outRec.pts);
    return (double)a / 2;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


double Area(const std::vector<Vec4>& poly)
{
    int32_t highI = (int32_t)poly.size() - 1;
    if (highI < 2)
    {
        return 0;
    }

    double a;
    a = ((double)poly[highI].x + poly[0].x) * ((double)poly[0].y - poly[highI].y);
    for (int32_t i = 1; i <= highI; ++i)
    {
        a += ((double)poly[i - 1].x + poly[i].x) * ((double)poly[i].y - poly[i - 1].y);
    }
    return a / 2;
}


////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::Orientation(const std::vector<Vec4>& poly)
{
    return Area(poly) >= 0;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void ReversePolygons(std::vector<std::vector<Vec4>>& polys)
{
    for (typename std::vector<std::vector<Vec4>>::iterator it = polys.begin();
        it != polys.end(); ++it)
    {
        std::reverse(it->begin(), it->end());
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::ExecuteInternal()
{
    bool succeeded;
    int32_t scanIdx = 0;
    try
    {
        Reset();
        if (!m_CurrentLM)
        {
            return true;
        }
        double botY = PopScanbeam();
        do
        {
            InsertLocalMinimaIntoAEL(botY);
            ClearHorzJoins();
            ProcessHorizontals();
            double topY = PopScanbeam();
            succeeded = ProcessIntersections(botY, topY);
            if (!succeeded)
            {
                break;
            }
            ProcessEdgesAtTopOfScanbeam(topY);
            botY = topY;
            scanIdx++;
        } while (m_Scanbeam || m_CurrentLM);
    }
    catch (...)
    {
        succeeded = false;
    }

    if (succeeded)
    {
        //tidy up output std::vector<std::vector<WxVector4<int64_t>>> and fix orientations where necessary ...
        for (uint32_t i = 0; i < m_PolyOuts.size(); ++i)
        {
            OutRec* outRec = m_PolyOuts[i];
            if (!outRec->pts)
            {
                continue;
            }
            FixupOutPolygon(*outRec);
            if (!outRec->pts)
            {
                continue;
            }

            if ((outRec->isHole) == (Area(*outRec) > 0))
            {
                ReversePolyPtLinks(outRec->pts);
            }
        }

        //if (!m_Joins.empty()) JoinCommonEdges();
    }

    ClearJoins();
    ClearHorzJoins();

    return succeeded;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::Reset()
{
    m_CurrentLM = m_MinimaList;
    if (!m_CurrentLM)
    {
        return;    //ie nothing to process
    }

    //reset all edges ...
    LocalMinima* lm = m_MinimaList;
    while (lm)
    {
        Edge* e = lm->leftBound;
        while (e)
        {
            e->cur = e->bot;
            e->side = ESLeft;
            e->outIdx = -1;
            e = e->nextInLML;
        }
        e = lm->rightBound;
        while (e)
        {
            e->cur = e->bot;
            e->side = ESRight;
            e->outIdx = -1;
            e = e->nextInLML;
        }
        lm = lm->next;
    }

    m_Scanbeam = 0;
    m_ActiveEdges = 0;
    m_SortedEdges = 0;

    lm = m_MinimaList;
    while (lm)
    {
        InsertScanbeam(lm->Y);
        lm = lm->next;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void DisposeOutPts(typename PolygonClipper::OutPt*& pp)
{
    if (pp == 0)
    {
        return;
    }
    pp->prev->next = 0;
    while (pp)
    {
        typename PolygonClipper::OutPt* tmpPp = pp;
        pp = pp->next;
        delete tmpPp;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DisposeAllPolyPts()
{
    for (uint32_t i = 0; i < m_PolyOuts.size(); ++i)
    {
        DisposeOutRec(i);
    }
    m_PolyOuts.clear();
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DisposeOutRec(uint32_t index)
{
    OutRec* outRec = m_PolyOuts[index];
    if (outRec->pts)
    {
        DisposeOutPts(outRec->pts);
    }
    delete outRec;
    m_PolyOuts[index] = 0;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ProcessHorizontals()
{
    Edge* horzEdge = m_SortedEdges;
    while (horzEdge)
    {
        DeleteFromSEL(horzEdge);
        ProcessHorizontal(horzEdge);
        horzEdge = m_SortedEdges;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DeleteFromSEL(Edge* e)
{
    Edge* SelPrev = e->prevInSEL;
    Edge* SelNext = e->nextInSEL;
    if (!SelPrev && !SelNext && (e != m_SortedEdges))
    {
        return;    //already deleted
    }
    if (SelPrev)
    {
        SelPrev->nextInSEL = SelNext;
    }
    else
    {
        m_SortedEdges = SelNext;
    }
    if (SelNext)
    {
        SelNext->prevInSEL = SelPrev;
    }
    e->nextInSEL = 0;
    e->prevInSEL = 0;
}

enum Direction { dRightToLeft, dLeftToRight };

/////////////////////////////////////////////////////////////////////////////////////////////////////


inline bool IsMaxima(typename PolygonClipper::Edge* e, const double Y)
{
    return e && e->top.y == Y && !e->nextInLML;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::Edge* GetMaximaPair(typename PolygonClipper::Edge* e)
{
    if (!IsMaxima(e->next, e->top.y) || e->next->top.x != e->top.x)
    {
        return e->prev;
    }
    else
    {
        return e->next;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


typename PolygonClipper::Edge* GetNextInAEL(typename PolygonClipper::Edge* e, Direction dir)
{
    return dir == dLeftToRight ? e->nextInAEL : e->prevInAEL;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


inline bool IsMinima(typename PolygonClipper::Edge* e)
{
    return e && (e->prev->nextInLML != e) && (e->next->nextInLML != e);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::IsTopHorz(const double XPos)
{
    Edge* e = m_SortedEdges;
    while (e)
    {
        if ((XPos >= min(e->cur.x, e->top.x)) &&
            (XPos <= max(e->cur.x, e->top.x)))
        {
            return false;
        }
        e = e->nextInSEL;
    }
    return true;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ProcessHorizontal(Edge* horzEdge)
{
    Direction dir;
    double horzLeft, horzRight;

    if (horzEdge->cur.x < horzEdge->top.x)
    {
        horzLeft = horzEdge->cur.x;
        horzRight = horzEdge->top.x;
        dir = dLeftToRight;
    }
    else
    {
        horzLeft = horzEdge->top.x;
        horzRight = horzEdge->cur.x;
        dir = dRightToLeft;
    }

    Edge* eMaxPair;
    if (horzEdge->nextInLML)
    {
        eMaxPair = 0;
    }
    else
    {
        eMaxPair = GetMaximaPair(horzEdge);
    }

    Edge* e = GetNextInAEL(horzEdge, dir);
    while (e)
    {
        if (e->cur.x == horzEdge->top.x && !eMaxPair)
        {
            if (SlopesEqual(*e, *horzEdge->nextInLML))
            {
                //if output std::vector<std::vector<WxVector4<int64_t>>> share an edge, they'll need joining later ...
                if (horzEdge->outIdx >= 0 && e->outIdx >= 0)
                {
                    AddJoin(horzEdge->nextInLML, e, horzEdge->outIdx);
                }
                break; //we've reached the end of the horizontal line
            }
            else if (e->dx < horzEdge->nextInLML->dx)
                //we really have got to the end of the intermediate horz edge so quit.
                //nb: More -ve slopes follow more +ve slopes ABOVE the horizontal.
            {
                break;
            }
        }

        Edge* eNext = GetNextInAEL(e, dir);

        if (eMaxPair ||
            ((dir == dLeftToRight) && (e->cur.x < horzRight)) ||
            ((dir == dRightToLeft) && (e->cur.x > horzLeft)))
        {
            //so far we're still in range of the horizontal edge
            if (e == eMaxPair)
            {
                //horzEdge is evidently a maxima horizontal and we've arrived at its end.
                if (dir == dRightToLeft)
                {
                    IntersectEdges(horzEdge, e, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, e->cur.v), ipNone);
                }
                else
                {
                    IntersectEdges(e, horzEdge, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, horzEdge->cur.v), ipNone);
                }
                if (eMaxPair->outIdx >= 0)
                {
                    throw "ProcessHorizontal error";
                }
                WindFromAEL(m_ActiveEdges);
                return;
            }
            else if (NEAR_EQUAL(e->dx, HORIZONTAL) && !IsMinima(e) && !(e->cur.x > e->top.x))
            {
                //An overlapping horizontal edge. Overlapping horizontal edges are
                //processed as if layered with the current horizontal edge (horizEdge)
                //being infinitesimally lower that the next (e). Therfore, we
                //intersect with e only if e.cur.x is within the bounds of horzEdge ...
                if (dir == dLeftToRight)
                    IntersectEdges(horzEdge, e, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, horzEdge->cur.v),
                        (IsTopHorz(e->cur.x)) ? ipLeft : ipBoth);
                else
                    IntersectEdges(e, horzEdge, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, horzEdge->cur.v),
                        (IsTopHorz(e->cur.x)) ? ipRight : ipBoth);
            }
            else if (dir == dLeftToRight)
            {
                IntersectEdges(horzEdge, e, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, horzEdge->cur.v),
                    (IsTopHorz(e->cur.x)) ? ipLeft : ipBoth);
            }
            else
            {
                IntersectEdges(e, horzEdge, Vec4(e->cur.x, horzEdge->cur.y, e->cur.u, horzEdge->cur.v),
                    (IsTopHorz(e->cur.x)) ? ipRight : ipBoth);
            }
            SwapPositionsInAEL(horzEdge, e);
        }
        else if ((dir == dLeftToRight && e->cur.x >= horzRight) ||
            (dir == dRightToLeft && e->cur.x <= horzLeft))
        {
            break;
        }
        e = eNext;
    } //end while

    if (horzEdge->nextInLML)
    {
        if (horzEdge->outIdx >= 0)
        {
            AddOutPt(horzEdge, Vec4(horzEdge->top.x, horzEdge->top.y, horzEdge->top.u, horzEdge->top.v));
        }
        UpdateEdgeIntoAEL(horzEdge);
    }
    else
    {
        if (horzEdge->outIdx >= 0)
            IntersectEdges(horzEdge, eMaxPair,
                Vec4(horzEdge->top.x, horzEdge->cur.y, horzEdge->top.u, horzEdge->cur.v), ipBoth);
        if (eMaxPair->outIdx >= 0)
        {
            throw "ProcessHorizontal error";
        }
        DeleteFromAEL(eMaxPair);
        DeleteFromAEL(horzEdge);
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::SwapPositionsInAEL(Edge* edge1, Edge* edge2)
{
    if (edge1->nextInAEL == edge2)
    {
        Edge* next = edge2->nextInAEL;
        if (next)
        {
            next->prevInAEL = edge1;
        }
        Edge* prev = edge1->prevInAEL;
        if (prev)
        {
            prev->nextInAEL = edge2;
        }
        edge2->prevInAEL = prev;
        edge2->nextInAEL = edge1;
        edge1->prevInAEL = edge2;
        edge1->nextInAEL = next;
        edge2->windCnt = edge1->windCnt;
        edge1->windCnt = edge2->windCnt + edge2->windDelta;
    }
    else if (edge2->nextInAEL == edge1)
    {
        Edge* next = edge1->nextInAEL;
        if (next)
        {
            next->prevInAEL = edge2;
        }
        Edge* prev = edge2->prevInAEL;
        if (prev)
        {
            prev->nextInAEL = edge1;
        }
        edge1->prevInAEL = prev;
        edge1->nextInAEL = edge2;
        edge2->prevInAEL = edge1;
        edge2->nextInAEL = next;
        edge1->windCnt = edge2->windCnt;
        edge2->windCnt = edge1->windCnt + edge1->windDelta;
    }
    else
    {
        throw "Swapping not adjacent edges";
#if 0
        Edge* next = edge1->nextInAEL;
        Edge* prev = edge1->prevInAEL;
        edge1->nextInAEL = edge2->nextInAEL;
        if (edge1->nextInAEL)
        {
            edge1->nextInAEL->prevInAEL = edge1;
        }
        edge1->prevInAEL = edge2->prevInAEL;
        if (edge1->prevInAEL)
        {
            edge1->prevInAEL->nextInAEL = edge1;
        }
        edge2->nextInAEL = next;
        if (edge2->nextInAEL)
        {
            edge2->nextInAEL->prevInAEL = edge2;
        }
        edge2->prevInAEL = prev;
        if (edge2->prevInAEL)
        {
            edge2->prevInAEL->nextInAEL = edge2;
        }
#endif
    }

    if (!edge1->prevInAEL)
    {
        m_ActiveEdges = edge1;
    }
    else if (!edge2->prevInAEL)
    {
        m_ActiveEdges = edge2;
    }
}


/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::SwapPositionsInSEL(Edge* edge1, Edge* edge2)
{
    if (!(edge1->nextInSEL) && !(edge1->prevInSEL))
    {
        return;
    }
    if (!(edge2->nextInSEL) && !(edge2->prevInSEL))
    {
        return;
    }

    if (edge1->nextInSEL == edge2)
    {
        Edge* next = edge2->nextInSEL;
        if (next)
        {
            next->prevInSEL = edge1;
        }
        Edge* prev = edge1->prevInSEL;
        if (prev)
        {
            prev->nextInSEL = edge2;
        }
        edge2->prevInSEL = prev;
        edge2->nextInSEL = edge1;
        edge1->prevInSEL = edge2;
        edge1->nextInSEL = next;
    }
    else if (edge2->nextInSEL == edge1)
    {
        Edge* next = edge1->nextInSEL;
        if (next)
        {
            next->prevInSEL = edge2;
        }
        Edge* prev = edge2->prevInSEL;
        if (prev)
        {
            prev->nextInSEL = edge1;
        }
        edge1->prevInSEL = prev;
        edge1->nextInSEL = edge2;
        edge2->prevInSEL = edge1;
        edge2->nextInSEL = next;
    }
    else
    {
        Edge* next = edge1->nextInSEL;
        Edge* prev = edge1->prevInSEL;
        edge1->nextInSEL = edge2->nextInSEL;
        if (edge1->nextInSEL)
        {
            edge1->nextInSEL->prevInSEL = edge1;
        }
        edge1->prevInSEL = edge2->prevInSEL;
        if (edge1->prevInSEL)
        {
            edge1->prevInSEL->nextInSEL = edge1;
        }
        edge2->nextInSEL = next;
        if (edge2->nextInSEL)
        {
            edge2->nextInSEL->prevInSEL = edge2;
        }
        edge2->prevInSEL = prev;
        if (edge2->prevInSEL)
        {
            edge2->prevInSEL->nextInSEL = edge2;
        }
    }

    if (!edge1->prevInSEL)
    {
        m_SortedEdges = edge1;
    }
    else if (!edge2->prevInSEL)
    {
        m_SortedEdges = edge2;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::UpdateEdgeIntoAEL(Edge*& e)
{
    if (!e->nextInLML) throw
        "UpdateEdgeIntoAEL: invalid call";
    Edge* AelPrev = e->prevInAEL;
    Edge* AelNext = e->nextInAEL;
    e->nextInLML->outIdx = e->outIdx;
    if (AelPrev)
    {
        AelPrev->nextInAEL = e->nextInLML;
    }
    else
    {
        m_ActiveEdges = e->nextInLML;
    }
    if (AelNext)
    {
        AelNext->prevInAEL = e->nextInLML;
    }
    e->nextInLML->side = e->side;
    e->nextInLML->windDelta = e->windDelta;
    e->nextInLML->windCnt = e->windCnt;
    e = e->nextInLML;
    e->prevInAEL = AelPrev;
    e->nextInAEL = AelNext;
    if (!NEAR_EQUAL(e->dx, HORIZONTAL))
    {
        InsertScanbeam(e->top.y);
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::ProcessIntersections(const double botY, const double topY)
{
    if (!m_ActiveEdges)
    {
        return true;
    }
    try
    {
        BuildIntersectList(botY, topY);
        if (!m_IntersectNodes)
        {
            return true;
        }
        if (!m_IntersectNodes->next || FixupIntersectionOrder())
        {
            ProcessIntersectList();
        }
        else
        {
            return false;
        }
    }
    catch (...)
    {
        m_SortedEdges = 0;
        DisposeIntersectNodes();
        throw "ProcessIntersections error";
    }
    m_SortedEdges = 0;
    return true;
}
/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DisposeIntersectNodes()
{
    while (m_IntersectNodes)
    {
        IntersectNode* iNode = m_IntersectNodes->next;
        delete m_IntersectNodes;
        m_IntersectNodes = iNode;
    }
}
/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::BuildIntersectList(const double botY, const double topY)
{
    if (!m_ActiveEdges)
    {
        return;
    }

    //prepare for sorting ...
    Edge* e = m_ActiveEdges;
    m_SortedEdges = e;
    while (e)
    {
        e->prevInSEL = e->prevInAEL;
        e->nextInSEL = e->nextInAEL;
        InterpXUV(*e, topY, e->cur.x, e->cur.u, e->cur.v);
        e = e->nextInAEL;
    }

    //bubblesort ...
    bool isModified;
    do
    {
        isModified = false;
        e = m_SortedEdges;
        while (e->nextInSEL)
        {
            Edge* eNext = e->nextInSEL;
            Vec4 pt;
            if (e->cur.x > eNext->cur.x)
            {
                if (!IntersectPoint(*e, *eNext, pt) && e->cur.x > eNext->cur.x + 1)
                {
                    throw "Intersection error";
                }
                if (pt.y > botY)
                {
                    double X, U, V;
                    InterpXUV(*e, pt.y, X, U, V);
                    pt = Vec4(X, botY, U, V);
                }
                InsertIntersectNode(e, eNext, pt);
                SwapPositionsInSEL(e, eNext);
                isModified = true;
            }
            else
            {
                e = eNext;
            }
        }
        //if(e->prevInSEL) e->prevInSEL->nextInSEL = 0;
        //else break;
    } while (isModified);
    m_SortedEdges = 0; //important
}
/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::InsertIntersectNode(Edge* e1, Edge* e2, const Vec4& pt)
{
    IntersectNode* newNode = new IntersectNode;
    newNode->edge1 = e1;
    newNode->edge2 = e2;
    newNode->pt = pt;
    newNode->next = 0;
    if (!m_IntersectNodes)
    {
        m_IntersectNodes = newNode;
    }
    else if (newNode->pt.y > m_IntersectNodes->pt.y)
    {
        newNode->next = m_IntersectNodes;
        m_IntersectNodes = newNode;
    }
    else
    {
        IntersectNode* iNode = m_IntersectNodes;
        while (iNode->next && newNode->pt.y <= iNode->next->pt.y)
        {
            iNode = iNode->next;
        }
        newNode->next = iNode->next;
        iNode->next = newNode;
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ProcessIntersectList()
{
    //WindFromAEL(m_ActiveEdges);
    while (m_IntersectNodes)
    {
        IntersectNode* iNode = m_IntersectNodes->next;
        {
            IntersectEdges(m_IntersectNodes->edge1,
                m_IntersectNodes->edge2, m_IntersectNodes->pt, ipBoth);
            SwapPositionsInAEL(m_IntersectNodes->edge1, m_IntersectNodes->edge2);
        }
        delete m_IntersectNodes;
        m_IntersectNodes = iNode;
        //WindFromAEL(m_ActiveEdges);
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void SwapIntersectNodes(typename PolygonClipper::IntersectNode& int1,
    typename PolygonClipper::IntersectNode& int2)
{
    //just swap the contents (because fIntersectNodes is a single-linked-list)
    const typename PolygonClipper::IntersectNode inode = int1; //gets a copy of Int1
    int1.edge1 = int2.edge1;
    int1.edge2 = int2.edge2;
    int1.pt = int2.pt;
    int2.edge1 = inode.edge1;
    int2.edge2 = inode.edge2;
    int2.pt = inode.pt;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


inline bool EdgesAdjacent(const typename PolygonClipper::IntersectNode& inode)
{
    return (inode.edge1->nextInSEL == inode.edge2) ||
        (inode.edge1->prevInSEL == inode.edge2);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


bool PolygonClipper::FixupIntersectionOrder()
{
    //pre-condition: intersections are sorted bottom-most (then left-most) first.
    //Now it's crucial that intersections are made only between adjacent edges,
    //so to ensure this the order of intersections may need adjusting ...
    IntersectNode* inode = m_IntersectNodes;
    CopyAELToSEL();
    while (inode)
    {
        if (!EdgesAdjacent(*inode))
        {
            IntersectNode* nextNode = inode->next;
            while (nextNode && !EdgesAdjacent(*nextNode))
            {
                nextNode = nextNode->next;
            }
            if (!nextNode)
            {
                return false;
            }
            SwapIntersectNodes(*inode, *nextNode);
        }
        SwapPositionsInSEL(inode->edge1, inode->edge2);
        inode = inode->next;
    }
    return true;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::CopyAELToSEL()
{
    Edge* e = m_ActiveEdges;
    m_SortedEdges = e;
    while (e)
    {
        e->prevInSEL = e->prevInAEL;
        e->nextInSEL = e->nextInAEL;
        e = e->nextInAEL;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


inline bool IsIntermediate(typename PolygonClipper::Edge* e, const double Y)
{
    return e->top.y == Y && e->nextInLML;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ProcessEdgesAtTopOfScanbeam(const double topY)
{
    Edge* e = m_ActiveEdges;
    while (e)
    {
        //1. process maxima, treating them as if they're 'bent' horizontal edges,
        //   but exclude maxima with horizontal edges. nb: e can't be a horizontal.
        if (IsMaxima(e, topY) && !NEAR_EQUAL(GetMaximaPair(e)->dx, HORIZONTAL))
        {
            //'e' might be removed from AEL, as may any following edges so ...
            Edge* ePrev = e->prevInAEL;
            DoMaxima(e, topY);
            if (!ePrev)
            {
                e = m_ActiveEdges;
            }
            else
            {
                e = ePrev->nextInAEL;
            }
        }
        else
        {
            bool intermediateVert = IsIntermediate(e, topY);
            //2. promote horizontal edges, otherwise update cur.x and cur.y ...
            if (intermediateVert && NEAR_EQUAL(e->nextInLML->dx, HORIZONTAL))
            {
                if (e->outIdx >= 0)
                {
                    AddOutPt(e, Vec4(e->top.x, e->top.y, e->top.u, e->top.v));

                    for (uint32_t i = 0; i < m_HorizJoins.size(); ++i)
                    {
                        Vec4 pt, pt2;
                        HorzJoinRec* hj = m_HorizJoins[i];
                        if (GetOverlapSegment(Vec4(hj->edge->bot.x, hj->edge->bot.y, hj->edge->bot.u, hj->edge->bot.v),
                            Vec4(hj->edge->top.x, hj->edge->top.y, hj->edge->top.u, hj->edge->top.v),
                            Vec4(e->nextInLML->bot.x, e->nextInLML->bot.y, e->nextInLML->bot.u, e->nextInLML->bot.v),
                            Vec4(e->nextInLML->top.x, e->nextInLML->top.y, e->nextInLML->top.u, e->nextInLML->top.v), pt, pt2))
                        {
                            AddJoin(hj->edge, e->nextInLML, hj->savedIdx, e->outIdx);
                        }
                    }

                    AddHorzJoin(e->nextInLML, e->outIdx);
                }
                UpdateEdgeIntoAEL(e);
                AddEdgeToSEL(e);
            }
            else
            {

                InterpXUV(*e, topY, e->cur.x, e->cur.u, e->cur.v);
                e->cur.y = topY;
            }
            e = e->nextInAEL;
        }
    }

    //3. Process horizontals at the top of the scanbeam ...
    ProcessHorizontals();

    //4. Promote intermediate vertices ...
    e = m_ActiveEdges;
    while (e)
    {
        if (IsIntermediate(e, topY))
        {
            if (e->outIdx >= 0)
            {
                AddOutPt(e, Vec4(e->top.x, e->top.y, e->top.u, e->top.v));
            }
            UpdateEdgeIntoAEL(e);

            //if output std::vector<std::vector<WxVector4<int64_t>>> share an edge, they'll need joining later ...
            Edge* ePrev = e->prevInAEL;
            Edge* eNext = e->nextInAEL;
            if (ePrev && ePrev->cur.x == e->bot.x &&
                ePrev->cur.y == e->bot.y && e->outIdx >= 0 &&
                ePrev->outIdx >= 0 && ePrev->cur.y > ePrev->top.y &&
                SlopesEqual(*e, *ePrev))
            {
                AddOutPt(ePrev, Vec4(e->bot.x, e->bot.y, e->bot.u, e->bot.v));
                AddJoin(e, ePrev);
            }
            else if (eNext && eNext->cur.x == e->bot.x &&
                eNext->cur.y == e->bot.y && e->outIdx >= 0 &&
                eNext->outIdx >= 0 && eNext->cur.y > eNext->top.y &&
                SlopesEqual(*e, *eNext))
            {
                AddOutPt(eNext, Vec4(e->bot.x, e->bot.y, e->bot.u, e->bot.v));
                AddJoin(e, eNext);
            }
        }
        e = e->nextInAEL;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DoMaxima(Edge* e, double topY)
{
    Edge* eMaxPair = GetMaximaPair(e);
    double X = e->top.x;
    double U = e->top.u;
    double V = e->top.v;
    Edge* eNext = e->nextInAEL;
    while (eNext != eMaxPair)
    {
        if (!eNext)
        {
            throw "DoMaxima error";
        }
        IntersectEdges(e, eNext, Vec4(X, topY, U, V), ipBoth);
        SwapPositionsInAEL(e, eNext);
        eNext = e->nextInAEL;
    }
    if (e->outIdx < 0 && eMaxPair->outIdx < 0)
    {
        DeleteFromAEL(e);
        DeleteFromAEL(eMaxPair);
    }
    else if (e->outIdx >= 0 && eMaxPair->outIdx >= 0)
    {
        IntersectEdges(e, eMaxPair, Vec4(X, topY, U, V), ipNone);
    }
    else
    {
        throw "DoMaxima error";
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::AddHorzJoin(Edge* e, int32_t idx)
{
    HorzJoinRec* hj = new HorzJoinRec;
    hj->edge = e;
    hj->savedIdx = idx;
    m_HorizJoins.push_back(hj);
}


////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ClearJoins()
{
    for (uint32_t i = 0; i < m_Joins.size(); i++)
    {
        delete m_Joins[i];
    }
    m_Joins.resize(0);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::ClearHorzJoins()
{
    for (uint32_t i = 0; i < m_HorizJoins.size(); i++)
    {
        delete m_HorizJoins[i];
    }
    m_HorizJoins.resize(0);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::DisposeLocalMinimaList()
{
    while (m_MinimaList)
    {
        LocalMinima* tmpLm = m_MinimaList->next;
        delete m_MinimaList;
        m_MinimaList = tmpLm;
    }
    m_CurrentLM = 0;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::Clear()
{
    DisposeLocalMinimaList();
    for (uint32_t i = 0; i < m_edges.size(); ++i)
    {
        delete[] m_edges[i];
    }
    m_edges.clear();
    DisposeAllPolyPts();
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::FixupOutPolygon(OutRec& outrec)
{
    //FixupOutPolygon() - removes duplicate points and simplifies consecutive
    //parallel edges by removing the middle vertex.
    OutPt* lastOK = 0;
    outrec.bottomPt = 0;
    OutPt* pp = outrec.pts;

    for (;;)
    {
        if (pp->prev == pp || pp->prev == pp->next)
        {
            DisposeOutPts(pp);
            outrec.pts = 0;
            return;
        }
        //test for duplicate points and for same slope (cross-product) ...
        if (EqXY(pp->pt, pp->next->pt) ||
            SlopesEqual(pp->prev->pt, pp->pt, pp->next->pt))
        {
            lastOK = 0;
            OutPt* tmp = pp;
            pp->prev->next = pp->next;
            pp->next->prev = pp->prev;
            pp = pp->prev;
            delete tmp;
        }
        else if (pp == lastOK)
        {
            break;
        }
        else
        {
            if (!lastOK)
            {
                lastOK = pp;
            }
            pp = pp->next;
        }
    }
    outrec.pts = pp;
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void PolygonClipper::BuildResult(std::vector<std::vector<Vec4>>& polys)
{
    polys.reserve(polys.size() + m_PolyOuts.size());
    for (uint32_t i = 0; i < m_PolyOuts.size(); ++i)
    {
        if (m_PolyOuts[i]->pts)
        {
            std::vector<Vec4> pg;
            OutPt* p = m_PolyOuts[i]->pts;
            do
            {
                pg.push_back(p->pt);
                p = p->prev;
            } while (p != m_PolyOuts[i]->pts);
            if (pg.size() > 2)
            {
                polys.push_back(pg);
            }
        }
    }
}


////////////////////////////////////////////////////////////////////////////////////////////////////

///   OffsetBuilder

////////////////////////////////////////////////////////////////////////////////////////////////////



class OffsetBuilder
{

    struct DoublePoint
    {
        double X;
        double Y;
        DoublePoint(double x = 0, double y = 0) : X(x), Y(y) {}
    };

public:

private:
    const std::vector<std::vector<Vec4>>* m_pp;
    std::vector<Vec4>* m_curr_poly;
    std::vector<DoublePoint> normals;
    double m_delta, m_rmin, m_r;
    uint32_t m_i, m_j, m_k;
    static const int32_t buffLength = 128;
    double m_curU;

private:

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    std::vector<Vec4> BuildArc(const Vec4& pt,
        const double a1, const double a2, const double r, double limit)
    {
        //see notes in clipper.pas regarding steps
        double arcFrac = std::fabs(a2 - a1) / (2 * M_PI);
        int32_t steps = (int32_t)(arcFrac * M_PI / std::acos(1 - limit / std::fabs(r)));
        if (steps < 2)
        {
            steps = 2;
        }
        else if (steps > (int32_t)(222.0 * arcFrac))
        {
            steps = (int32_t)(222.0 * arcFrac);
        }

        double x = std::cos(a1);
        double y = std::sin(a1);
        double c = std::cos((a2 - a1) / steps);
        double s = std::sin((a2 - a1) / steps);
        std::vector<Vec4> result(steps + 1);
        for (int32_t i = 0; i <= steps; ++i)
        {
            result[i] = Vec4(pt.x + Round(x * r),
                pt.y + Round(y * r), pt.z, pt.w);
            double x2 = x;
            x = x * c - s * y;  //cross product
            y = x2 * s + y * c; //dot product
        }
        return result;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    DoublePoint GetUnitNormal(const Vec4& pt1, const Vec4& pt2)
    {
        if (pt2.x == pt1.x && pt2.y == pt1.y)
        {
            return DoublePoint(0, 0);
        }

        double dx = (double)(pt2.x - pt1.x);
        double dy = (double)(pt2.y - pt1.y);
        double f = 1 * 1.0 / std::sqrt(dx * dx + dy * dy);
        dx *= f;
        dy *= f;
        return DoublePoint(dy, -dx);
    }

    inline const double& GetU(const double& u)
    {
        return m_curU >= 0 ? m_curU : u;
    }

    inline bool PointsEqual2d(const Vec4& pt1, const Vec4& pt2)
    {
        return pt1.x == pt2.x && pt1.y == pt2.y;
    }

public:
    OffsetBuilder(const std::vector<std::vector<Vec4>>& in_polys, std::vector<std::vector<Vec4>>& out_polys,
        bool isPolygon, double delta, JoinType jointype, EndType endtype, double limit, double Uval) : m_pp(&in_polys)
    {
        //precondition: &out_polys != &in_polys
        const std::vector<std::vector<Vec4>>& m_p = *m_pp;
        m_curU = -1;
        if (NEAR_ZERO(delta))
        {
            out_polys = in_polys;
            return;
        }
        m_rmin = 0.5;
        m_delta = delta;
        if (jointype == jtMiter)
        {
            if (limit > 2)
            {
                m_rmin = 2.0 / (limit * limit);
            }
            limit = 0.25; //just in case endtype == etRound
        }
        else
        {
            if (limit <= 0)
            {
                limit = 0.25;
            }
            else if (limit > std::fabs(delta))
            {
                limit = std::fabs(delta);
            }
        }

        double deltaSq = delta * delta;
        out_polys.clear();
        out_polys.resize(m_p.size());
        for (m_i = 0; m_i < m_p.size(); m_i++)
        {
            uint32_t len = (uint32_t)m_p[m_i].size();

            if (len == 0 || (len < 3 && delta <= 0))
            {
                continue;
            }
            else if (len == 1)
            {
                out_polys[m_i] = BuildArc(m_p[m_i][0], 0, 2 * M_PI, delta, limit);
                continue;
            }

            bool forceClose = PointsEqual2d(m_p[m_i][0], m_p[m_i][len - 1]);
            if (forceClose)
            {
                len--;
            }

            //build normals ...
            normals.clear();
            normals.resize(len);
            for (m_j = 0; m_j < len - 1; ++m_j)
            {
                normals[m_j] = GetUnitNormal(m_p[m_i][m_j], m_p[m_i][m_j + 1]);
            }
            if (isPolygon || forceClose)
            {
                normals[len - 1] = GetUnitNormal(m_p[m_i][len - 1], m_p[m_i][0]);
            }
            else //is open polyline
            {
                normals[len - 1] = normals[len - 2];
            }

            m_curr_poly = &out_polys[m_i];
            m_curr_poly->reserve(len);

            if (isPolygon || forceClose)
            {
                m_k = len - 1;
                for (m_j = 0; m_j < len; ++m_j)
                {
                    OffsetPoint(jointype, limit);
                }

                if (!isPolygon)
                {
                    uint32_t j = (uint32_t)out_polys.size();
                    out_polys.resize(j + 1);
                    m_curr_poly = &out_polys[j];
                    m_curr_poly->reserve(len);
                    m_delta = -m_delta;

                    m_k = len - 1;
                    for (m_j = 0; m_j < len; ++m_j)
                    {
                        OffsetPoint(jointype, limit);
                    }
                    m_delta = -m_delta;
                    std::reverse(m_curr_poly->begin(), m_curr_poly->end());
                }
            }
            else //is open polyline
            {
                //offset the polyline going forward ...
                m_k = 0;
                m_curU = 0;
                for (m_j = 1; m_j < len - 1; ++m_j)
                {
                    OffsetPoint(jointype, limit);
                }

                //handle the end (butt, round or square) ...
                Vec4 pt1;
                if (endtype == etButt)
                {
                    m_j = len - 1;
                    pt1 = Vec4(Round(m_p[m_i][m_j].x + normals[m_j].X * m_delta),
                        Round(m_p[m_i][m_j].y + normals[m_j].Y * m_delta), 0,
                        m_p[m_i][m_j].w);
                    AddPoint(pt1);
                    pt1 = Vec4(Round(m_p[m_i][m_j].x - normals[m_j].X * m_delta),
                        Round(m_p[m_i][m_j].y - normals[m_j].Y * m_delta),
                        Uval, m_p[m_i][m_j].w);
                    AddPoint(pt1);
                }
                else
                {
                    m_j = len - 1;
                    m_k = len - 2;
                    normals[m_j].X = -normals[m_j].X;
                    normals[m_j].Y = -normals[m_j].Y;
                    if (endtype == etSquare)
                    {
                        DoSquare();
                    }
                    else
                    {
                        DoRound(limit);
                    }
                }

                //re-build Normals ...
                for (uint32_t j = len - 1; j > 0; --j)
                {
                    normals[j].X = -normals[j - 1].X;
                    normals[j].Y = -normals[j - 1].Y;
                }
                normals[0].X = -normals[1].X;
                normals[0].Y = -normals[1].Y;

                m_curU = Uval;
                //offset the polyline going backward ...
                m_k = len - 1;
                for (m_j = m_k - 1; m_j > 0; --m_j)
                {
                    OffsetPoint(jointype, limit);
                }

                //finally handle the start (butt, round or square) ...
                if (endtype == etButt)
                {
                    pt1 = Vec4(Round(m_p[m_i][0].x - normals[0].X * m_delta),
                        Round(m_p[m_i][0].y - normals[0].Y * m_delta),
                        Uval, m_p[m_i][0].w);
                    AddPoint(pt1);
                    pt1 = Vec4(Round(m_p[m_i][0].x + normals[0].X * m_delta),
                        Round(m_p[m_i][0].y + normals[0].Y * m_delta),
                        0, m_p[m_i][0].w);
                    AddPoint(pt1);
                }
                else
                {
                    m_k = 1;
                    if (endtype == etSquare)
                    {
                        DoSquare();
                    }
                    else
                    {
                        DoRound(limit);
                    }
                }
            }
        }

        //and clean up untidy corners using Clipper ...
        PolygonClipper clpr;
        for (typename std::vector<std::vector<Vec4>>::iterator itPoly = out_polys.begin();
            itPoly != out_polys.end(); ++itPoly)
        {
            clpr.AddPolygon(*itPoly);
        }

        if (delta > 0)
        {
            out_polys.clear();
            if (!clpr.Execute(out_polys, 1))
            {
                out_polys.clear();
            }
        }
        else
        {
            throw "bad winding";
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

private:

    void OffsetPoint(JoinType jointype, double limit)
    {
        switch (jointype)
        {
        case jtMiter:
        {
            m_r = 1 + (normals[m_j].X * normals[m_k].X +
                normals[m_j].Y * normals[m_k].Y);
            if (m_r >= m_rmin)
            {
                DoMiter();
            }
            else
            {
                DoSquare();
            }
            break;
        }
        case jtSquare:
            DoSquare();
            break;
        case jtRound:
            DoRound(limit);
            break;
        }
        m_k = m_j;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    void AddPoint(const Vec4& pt)
    {
        if (m_curr_poly->size() == m_curr_poly->capacity())
        {
            m_curr_poly->reserve(m_curr_poly->capacity() + buffLength);
        }
        m_curr_poly->push_back(pt);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    void DoSquare()
    {
        const std::vector<std::vector<Vec4>>& m_p = *m_pp;
        Vec4 pt1 = Vec4(Round(m_p[m_i][m_j].x + normals[m_k].X * m_delta),
            Round(m_p[m_i][m_j].y + normals[m_k].Y * m_delta), GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
        Vec4 pt2 = Vec4(Round(m_p[m_i][m_j].x + normals[m_j].X * m_delta),
            Round(m_p[m_i][m_j].y + normals[m_j].Y * m_delta), GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
        if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * m_delta >= 0)
        {
            double a1 = std::atan2(normals[m_k].Y, normals[m_k].X);
            double a2 = std::atan2(-normals[m_j].Y, -normals[m_j].X);
            a1 = std::fabs(a2 - a1);
            if (a1 > M_PI)
            {
                a1 = M_PI * 2 - a1;
            }
            double dx = std::tan((M_PI - a1) / 4) * std::fabs(m_delta);
            pt1 = Vec4((double)(pt1.x - normals[m_k].Y * dx), (double)(pt1.y + normals[m_k].X * dx),
                GetU(pt1.z), pt1.w);
            AddPoint(pt1);
            pt2 = Vec4((double)(pt2.x + normals[m_j].Y * dx), (double)(pt2.y - normals[m_j].X * dx),
                GetU(pt2.z), pt2.w);
            AddPoint(pt2);
        }
        else
        {
            AddPoint(pt1);
            Vec4 pt = m_p[m_i][m_j];
            pt.z = GetU(pt.z);
            AddPoint(pt);
            AddPoint(pt2);
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    void DoMiter()
    {
        const std::vector<std::vector<Vec4>>& m_p = *m_pp;
        if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * m_delta >= 0)
        {
            double q = m_delta / m_r;
            AddPoint(Vec4(Round(m_p[m_i][m_j].x + (normals[m_k].X + normals[m_j].X) * q),
                Round(m_p[m_i][m_j].y + (normals[m_k].Y + normals[m_j].Y) * q),
                GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w));
        }
        else
        {
            Vec4 pt1 = Vec4(Round(m_p[m_i][m_j].x + normals[m_k].X * m_delta),
                Round(m_p[m_i][m_j].y + normals[m_k].Y * m_delta),
                GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
            Vec4 pt2 = Vec4(Round(m_p[m_i][m_j].x + normals[m_j].X * m_delta),
                Round(m_p[m_i][m_j].y + normals[m_j].Y * m_delta),
                GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
            AddPoint(pt1);
            Vec4 pt = m_p[m_i][m_j];
            pt.z = GetU(pt.z);
            AddPoint(pt);
            AddPoint(pt2);
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

    void DoRound(double limit)
    {
        const std::vector<std::vector<Vec4>>& m_p = *m_pp;
        Vec4 pt1 = Vec4(Round(m_p[m_i][m_j].x + normals[m_k].X * m_delta),
            Round(m_p[m_i][m_j].y + normals[m_k].Y * m_delta),
            GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
        Vec4 pt2 = Vec4(Round(m_p[m_i][m_j].x + normals[m_j].X * m_delta),
            Round(m_p[m_i][m_j].y + normals[m_j].Y * m_delta),
            GetU(m_p[m_i][m_j].z), m_p[m_i][m_j].w);
        AddPoint(pt1);
        //round off reflex angles (ie > 180 deg) unless almost flat (ie < ~10deg).
        if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * m_delta >= 0)
        {
            if (normals[m_j].X * normals[m_k].X + normals[m_j].Y * normals[m_k].Y < 0.985)
            {
                double a1 = std::atan2(normals[m_k].Y, normals[m_k].X);
                double a2 = std::atan2(normals[m_j].Y, normals[m_j].X);
                if (m_delta > 0 && a2 < a1)
                {
                    a2 += M_PI * 2;
                }
                else if (m_delta < 0 && a2 > a1)
                {
                    a2 -= M_PI * 2;
                }
                std::vector<Vec4> arc = BuildArc(m_p[m_i][m_j], a1, a2, m_delta, limit);
                for (typename std::vector<Vec4>::size_type m = 0; m < arc.size(); m++)
                {
                    arc[m].z = GetU(arc[m].z);
                    AddPoint(arc[m]);
                }
            }
        }
        else
        {
            Vec4 pt = m_p[m_i][m_j];
            pt.z = GetU(pt.z);
            AddPoint(pt);
        }
        AddPoint(pt2);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////

}; //end PolyOffsetBuilder


void BuildPolyline(const std::vector<std::vector<Vec4>>& inLines, std::vector<std::vector<Vec4>>& outPolys,
    double delta, JoinType jointype, EndType endtype, double limit, double Uval)
{
    std::vector<std::vector<Vec4>> inLinesFixed(inLines);

    for (uint32_t i = 0; i < inLinesFixed.size(); ++i)
    {
        if (inLinesFixed[i].size() < 2)
        {
            inLinesFixed[i].clear();
            continue;
        }
        typename std::vector<Vec4>::iterator it = inLinesFixed[i].begin() + 1;
        while (it != inLinesFixed[i].end())
        {
            if (*it == *(it - 1))
            {
                it = inLinesFixed[i].erase(it);
            }
            else
            {
                ++it;
            }
        }
    }

    OffsetBuilder(inLines, outPolys, false, delta, jointype, endtype, limit, Uval);
}

////////////////////////////////////////////////////////////////////////////////////////////////////


bool UpdateBotPt(const Vec4& pt, Vec4& botPt)
{
    if (pt.y > botPt.y || (pt.y == botPt.y && pt.x < botPt.x))
    {
        botPt = pt;
        return true;
    }
    else
    {
        return false;
    }
}

////////////////////////////////////////////////////////////////////////////////////////////////////


void OffsetPolygons(const std::vector<std::vector<Vec4>>& inPolysC, std::vector<std::vector<Vec4>>& outPolys,
    double delta, JoinType jointype, double limit, double Uval, bool autoFix)
{
    if (!autoFix && &inPolysC != &outPolys)
    {
        OffsetBuilder(inPolysC, outPolys, true, delta, jointype, etClosed, limit, Uval);
        return;
    }

    std::vector<std::vector<Vec4>> inPolys = std::vector<std::vector<Vec4>>(inPolysC);
    outPolys.clear();

    //ChecksInput - fixes polygon orientation if necessary and removes
    //duplicate vertices. Can be set false when you're sure that polygon
    //orientation is correct and that there are no duplicate vertices.
    if (autoFix)
    {
        uint32_t polyCount = (uint32_t)inPolys.size(), botPoly = 0;
        while (botPoly < polyCount && inPolys[botPoly].empty())
        {
            botPoly++;
        }
        if (botPoly == polyCount)
        {
            return;
        }

        //botPt: used to find the lowermost (in inverted Y-axis) & leftmost point
        //This point (on m_p[botPoly]) must be on an outer polygon ring and if
        //its orientation is false (counterclockwise) then assume all polygons
        //need reversing ...
        Vec4 botPt = inPolys[botPoly][0];
        for (uint32_t i = botPoly; i < polyCount; ++i)
        {
            if (inPolys[i].size() < 3)
            {
                inPolys[i].clear();
                continue;
            }
            if (UpdateBotPt(inPolys[i][0], botPt))
            {
                botPoly = i;
            }
            typename std::vector<Vec4>::iterator it = inPolys[i].begin() + 1;
            while (it != inPolys[i].end())
            {
                if (*it == *(it - 1))
                {
                    it = inPolys[i].erase(it);
                }
                else
                {
                    if (UpdateBotPt(*it, botPt))
                    {
                        botPoly = i;
                    }
                    ++it;
                }
            }
        }
        if (!PolygonClipper::Orientation(inPolys[botPoly]))
        {
            ReversePolygons(inPolys);
        }
    }

    OffsetBuilder(inPolys, outPolys, true, delta, jointype, etClosed, limit, Uval);
}

void TrimSmall(std::vector<std::vector<Vec4>>& polys)
{
    for (auto itPoly = polys.begin(); itPoly != polys.end();)
    {
        if (abs(Area(*itPoly)) < 1e-3)
            itPoly = polys.erase(itPoly);
        else
            ++itPoly;
    }
}

static std::string polygonLog;
static int sLogNode = 86;

extern "C" __declspec(dllexport) void SetLogNodeIdx(int idx)
{
    sLogNode = idx;
}

extern "C" __declspec(dllexport) int GetLogLength()
{ 
    return (int)polygonLog.size();
}

extern "C" __declspec(dllexport) void GetLogBytes(char *outchr)
{
    memcpy(outchr, polygonLog.c_str(), polygonLog.size());
}

void LogPoly(std::stringstream& str,
    const std::vector<Vec4>& poly)
{
    str << "[poly area=" << Area(poly) << "]" << std::endl;
    for (auto& p : poly)
    {
        str << p.x << "," << p.y << std::endl;
    }
    str << std::endl;
}

void LogPolys(std::stringstream &str,
    const std::vector<std::vector<Vec4>> &polys)
{
    for (auto& poly : polys)
    {
        LogPoly(str, poly);
    }
    str << std::endl;
}
extern "C" __declspec(dllexport) int ClipPolygons(int nodeIdx, double* pointListDbls, int* polyPointCounts, int* nodefaceIdxs, int nPortalPolys, int nModelPolys,
    int *connectedPortals)
{
    bool doLog = false;// nodeIdx == sLogNode;
    Vec2* pointList = (Vec2*)pointListDbls;
    std::vector<std::vector<Vec4>> portalPolys;
    int polyStartIdx = 0;
    std::vector<std::vector<Vec4>> modelPolys;
    std::stringstream str;
    if (doLog) str << "[clip1 nodeidx = " << nodeIdx << "]" << std::endl << std::endl;
    if (doLog) str << "[color=160,160,160]" << std::endl << std::endl;
    for (int polyIdx = 0; polyIdx < nPortalPolys; ++polyIdx)
    {
        int polyEndIdx = polyPointCounts[polyIdx];
        portalPolys.push_back(std::vector<Vec4>());
        std::vector<Vec4> &clipperPoly = portalPolys.back();
        if (doLog) str << "[nodeidx=" << nodefaceIdxs[polyIdx] << "]" << std::endl;
        for (int pointIdx = polyStartIdx; pointIdx < polyEndIdx; ++pointIdx)
        {
            const Vec2 &p = pointList[pointIdx];
            clipperPoly.push_back(Vec4(p.x, p.y, polyIdx, polyIdx));
            if (doLog) str << p.x << "," << p.y << std::endl;
        }
        if (doLog) str << std::endl;
        if (!PolygonClipper::Orientation(clipperPoly))
            std::reverse(clipperPoly.begin(), clipperPoly.end());
        polyStartIdx = polyEndIdx;
    }

    if (doLog) str << "[color=200,160,160]" << std::endl << std::endl;
    if (doLog) str << "[models]" << std::endl;
    for (int polyIdx = nPortalPolys; polyIdx < (nPortalPolys + nModelPolys); ++polyIdx)
    {
        if (doLog) str << "[faceidx=" << nodefaceIdxs[polyIdx] << "]" << std::endl;
        int polyEndIdx = polyPointCounts[polyIdx];
        modelPolys.push_back(std::vector<Vec4>());
        std::vector<Vec4>& clipperPoly = modelPolys.back();
        for (int pointIdx = polyStartIdx; pointIdx < polyEndIdx; ++pointIdx)
        {
            const Vec2& p = pointList[pointIdx];
            clipperPoly.push_back(Vec4(p.x, p.y, polyIdx, polyIdx));
            if (doLog) str << p.x << "," << p.y << std::endl;
        }
        if (doLog) str << std::endl;
        if (!PolygonClipper::Orientation(clipperPoly))
            std::reverse(clipperPoly.begin(), clipperPoly.end());
        polyStartIdx = polyEndIdx;
    }

    std::vector<std::vector<Vec4>> offsetModelPolys;
    OffsetPolygons(modelPolys, offsetModelPolys, 0.01, JoinType::jtSquare, 0.01, 0, false);
    PolygonClipper modelClipper;
    modelClipper.AddPolygons(modelPolys);
    std::vector<std::vector<Vec4>> unionModelPolys;
    modelClipper.Execute(unionModelPolys);
    if (doLog) str << "[color=160,200,160]" << std::endl << std::endl;
    if (doLog) str << "[union]" << std::endl;
    if (doLog) LogPolys(str, unionModelPolys);

    for (auto& poly : unionModelPolys)
    {
        std::reverse(poly.begin(), poly.end());
    }

    std::vector<std::pair<int, int>> intpolys;
    int* connectedPortalsCur = connectedPortals;
    for (int i = 0; i < portalPolys.size(); ++i)
    {
        for (int j = i + 1; j < portalPolys.size(); ++j)
        {           
            PolygonClipper clipper;
            clipper.AddPolygon(portalPolys[i]);
            clipper.AddPolygon(portalPolys[j]);
            std::vector<std::vector<Vec4>> outPolys;
            clipper.Execute(outPolys, 2);
            TrimSmall(outPolys);
            if (outPolys.size() > 0)
            {
                if (doLog) str << "[intersect=" << nodefaceIdxs[i] << ", " << nodefaceIdxs[j] << "]" << std::endl;
                if (doLog) LogPolys(str, outPolys);

                PolygonClipper finalClipper;
                finalClipper.AddPolygons(outPolys);
                finalClipper.AddPolygons(unionModelPolys);
                std::vector<std::vector<Vec4>> outOpenPolys;
                finalClipper.Execute(outOpenPolys);
                TrimSmall(outOpenPolys);
                if (outOpenPolys.size() > 0)
                {
                    if (doLog) str << "[openportal=" << nodefaceIdxs[i] << ", " << nodefaceIdxs[j] << "]" << std::endl;
                    if (doLog) LogPolys(str, outOpenPolys);

                    *connectedPortalsCur = i;
                    connectedPortalsCur++;
                    *connectedPortalsCur = j;
                    connectedPortalsCur++;
                    intpolys.push_back(std::make_pair(i, j));
                }
            }
        }
    }
    if (doLog) str << '\0';
    if (doLog) polygonLog = str.str();
    return (int)intpolys.size();
}

extern "C" __declspec(dllexport) int ClipPolygons2(int nodeIdx, double* pointListDbls, int* polyPointCounts, int* nodefaceIdxs, int nPortalPolys, int nModelPolys,
    int* coveredPortals)
{
    bool doLog = nodeIdx == sLogNode;

    Vec2* pointList = (Vec2*)pointListDbls;
    int polyStartIdx = 0;
    std::vector<std::vector<Vec4>> portalPolys;
    std::vector<std::vector<Vec4>> modelPolys;
    std::stringstream str;

    for (int polyIdx = 0; polyIdx < nPortalPolys; ++polyIdx)
    {
        int polyEndIdx = polyPointCounts[polyIdx];
        portalPolys.push_back(std::vector<Vec4>());
        std::vector<Vec4>& clipperPoly = portalPolys.back();
        for (int pointIdx = polyStartIdx; pointIdx < polyEndIdx; ++pointIdx)
        {
            const Vec2& p = pointList[pointIdx];
            clipperPoly.push_back(Vec4(p.x, p.y, polyIdx, polyIdx));
        }
        if (!PolygonClipper::Orientation(clipperPoly))
            std::reverse(clipperPoly.begin(), clipperPoly.end());
        polyStartIdx = polyEndIdx;
    }


    for (int polyIdx = nPortalPolys; polyIdx < (nPortalPolys + nModelPolys); ++polyIdx)
    {
        int polyEndIdx = polyPointCounts[polyIdx];
        modelPolys.push_back(std::vector<Vec4>());
        std::vector<Vec4>& clipperPoly = modelPolys.back();
        for (int pointIdx = polyStartIdx; pointIdx < polyEndIdx; ++pointIdx)
        {
            const Vec2& p = pointList[pointIdx];
            clipperPoly.push_back(Vec4(p.x, p.y, polyIdx, polyIdx));
        }
        if (!PolygonClipper::Orientation(clipperPoly))
            std::reverse(clipperPoly.begin(), clipperPoly.end());
        polyStartIdx = polyEndIdx;
    }

    std::vector<std::vector<Vec4>> offsetModelPolys;
    OffsetPolygons(modelPolys, offsetModelPolys, 0.01, JoinType::jtSquare, 0.01, 0, false);
    PolygonClipper modelClipper;
    modelClipper.AddPolygons(offsetModelPolys);
    std::vector<std::vector<Vec4>> unionModelPolys;
    modelClipper.Execute(unionModelPolys);

    for (auto& poly : unionModelPolys)
    {
        std::reverse(poly.begin(), poly.end());
    }

    std::vector<int> intpolys;
    int* coveredPortalsCur = coveredPortals;
    for (int i = 0; i < portalPolys.size(); ++i)
    {
        PolygonClipper finalClipper;
        finalClipper.AddPolygon(portalPolys[i]);
        finalClipper.AddPolygons(unionModelPolys);
        std::vector<std::vector<Vec4>> outOpenPolys;
        finalClipper.Execute(outOpenPolys);
        TrimSmall(outOpenPolys);
        if (outOpenPolys.size() == 0)
        {
            *coveredPortalsCur = i;
            coveredPortalsCur++;
            intpolys.push_back(i);
        }
        else
        {
            if (doLog)
            {                
                str << "[nodeidx=" << nodefaceIdxs[i] << "]" << std::endl;
                str << "[color=255,0,0]" << std::endl << std::endl;
                LogPoly(str, portalPolys[i]);

                str << "[union]" << std::endl;
                str << "[color=0,255,0]" << std::endl << std::endl;
                LogPolys(str, unionModelPolys);
                
                str << "[result]" << std::endl;
                str << "[color=0,0,255]" << std::endl << std::endl;
                LogPolys(str, outOpenPolys);
            }
        }
    }
    if (doLog) str << '\0';
    if (doLog) polygonLog = str.str();
    return (int)intpolys.size();
}