#pragma once
#include "StdIncludes.h"

using namespace gmtl;
namespace sam
{
    struct Loc
    {
        Loc(int x, int y, int z) :
            Loc(x, y, z, 8) {}

        Loc(int x, int y, int z, int l) :
            m_x(x),
            m_y(y),
            m_z(z),
            m_l(l) {}

        int m_x;
        int m_y;
        int m_z;
        int m_l;
        static const int lsize = 12;
        static const int ox = -(1 << (lsize - 1));
        static const int oy = -(1 << (lsize - 1));
        static const int oz = -(1 << (lsize - 1));

        static constexpr uint64_t GetLodOffset(int l)
        {
            if (l == 0)
                return 0;
            else
                return (1 << l) * (1 << l) * (1 << l) +
                GetLodOffset(l - 1);
        }

        static constexpr uint64_t GetTileIndex(const Loc& l)
        {
            return GetLodOffset(l.m_l) +
                l.m_x * (1 << lsize) * (1 << lsize) +
                l.m_y * (1 << lsize) +
                l.m_z;
        }

        static constexpr void GetLocFromIndex(Loc& loc, uint64_t index)
        {
            int l = 0;
            while (index > (1 << l) * (1 << l) * (1 << l))
            {
                index -= (1 << l) * (1 << l) * (1 << l);
                l++;
            }

            loc.m_l = l;
            loc.m_x = index / ((1 << l) * (1 << l));
            index -= loc.m_x * ((1 << l) * (1 << l));
            loc.m_y = index / (1 << l);
            index -= (1 << l);
            loc.m_z = index;
        }

        bool operator < (const Loc& rhs) const
        {
            if (m_l != rhs.m_l)
                return m_l < rhs.m_l;
            if (m_x != rhs.m_x)
                return m_x < rhs.m_x;
            if (m_y != rhs.m_y)
                return m_y < rhs.m_y;
            return m_z < rhs.m_z;
        }

        bool operator == (const Loc& rhs) const
        {
            if (m_x != rhs.m_x)
                return false;
            if (m_y != rhs.m_y)
                return false;
            if (m_z != rhs.m_z)
                return false;
            if (m_l != rhs.m_l)
                return false;

            return true;
        }

        float GetExtent() const { return powf(2.0, lsize - m_l); }

        AABoxf GetBBox() const {
            float dist = GetExtent();
            return AABoxf(Point3f(ox + m_x * dist, oy + m_y * dist, oz + m_z * dist), Point3f(ox + (m_x + 1) * dist, oy + (m_y + 1) * dist, oz + (m_z + 1) * dist));
        }

        Point3f GetCenter() const
        {
            float dist = GetExtent();
            return Point3f(ox + (m_x + 0.5f) * dist, oy + (m_y + 0.5f) * dist, oz + (m_z + 0.5f) * dist);
        }

        std::vector<Loc> GetChildren() const
        {
            return std::vector<Loc>{
                Loc(m_x * 2, m_y * 2, m_z * 2, m_l + 1),
                    Loc(m_x * 2, m_y * 2, m_z * 2 + 1, m_l + 1),
                    Loc(m_x * 2, m_y * 2 + 1, m_z * 2, m_l + 1),
                    Loc(m_x * 2, m_y * 2 + 1, m_z * 2 + 1, m_l + 1),
                    Loc(m_x * 2 + 1, m_y * 2, m_z * 2, m_l + 1),
                    Loc(m_x * 2 + 1, m_y * 2, m_z * 2 + 1, m_l + 1),
                    Loc(m_x * 2 + 1, m_y * 2 + 1, m_z * 2, m_l + 1),
                    Loc(m_x * 2 + 1, m_y * 2 + 1, m_z * 2 + 1, m_l + 1)
            };
        }

        void GetChildrenAtLevel(int l, std::vector<Loc> &locs) const
        {
            int ldelta = l - m_l;
            Loc loffset(m_x << ldelta, m_y << ldelta, m_z << ldelta, l);
            int sz = 1 << ldelta;
            locs.reserve(sz * sz * sz);
            for (int x = 0; x < sz; ++x)
            {
                for (int y = 0; y < sz; ++y)
                {
                    for (int z = 0; z < sz; ++z)
                    {
                        locs.push_back(Loc(loffset.m_x + x, loffset.m_y + y, loffset.m_z + z, l));
                    }
                }
            }
        }
        bool IsGroundLoc() const
        {
            return m_y == (1 << (m_l - 1));
        }

        Loc GetGroundLoc() const
        {
            return Loc(m_x, (1 << (m_l - 1)), m_z, m_l);
        }

        Loc Parent() const
        {
            return Loc(m_x >> 1, m_y >> 1, m_z >> 1, m_l - 1);
        }

        Loc ParentAtLevel(int l) const
        {
            int d = m_l - l;
            return Loc(m_x >> d, m_y >> d, m_z >> d, l);
        }

        Loc GetLocal(const Loc& parent)
        {
            int d = m_l - parent.m_l;
            return Loc(
                m_x - (parent.m_x << d),
                m_y - (parent.m_y << d),
                m_z - (parent.m_z << d),
                d);
        }

        Loc GetChild(const Loc& local)
        {
            return Loc(
                (m_x << local.m_l) + local.m_x,
                (m_y << local.m_l) + local.m_y,
                (m_z << local.m_l) + local.m_z,
                m_l + local.m_l);
        }

    };

    inline std::ostream& operator<<(std::ostream& os, const Loc& loc)
    {
        os << "[" << loc.m_l << ", " << loc.m_x << ", " << loc.m_y << ", " << loc.m_z << "]";
        return os;
    }

}