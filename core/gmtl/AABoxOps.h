// GMTL is (C) Copyright 2001-2010 by Allen Bierbaum
// Distributed under the GNU Lesser General Public License 2.1 with an
// addendum covering inlined code. (See accompanying files LICENSE and
// LICENSE.addendum or http://www.gnu.org/copyleft/lesser.txt)

#ifndef _GMTL_AABOXOPS_H_
#define _GMTL_AABOXOPS_H_

#include <gmtl/AABox.h>
#include <gmtl/VecOps.h>

namespace gmtl
{

/** @ingroup Compare AABox 
 * @name AABox Comparitors
 * @{
 */

/**
 * Compare two AABoxes to see if they are EXACTLY the same. In other words,
 * this comparison is done with zero tolerance.
 *
 * @param b1      the first box to compare
 * @param b2      the second box to compare
 *
 * @return  true if they are equal, false otherwise
 */
template< class DATA_TYPE >
inline bool operator==( const AABox<DATA_TYPE>& b1, const AABox<DATA_TYPE>& b2 )
{
   return ( (b1.isEmpty() == b2.isEmpty()) &&
            (b1.getMin() == b2.getMin()) &&
            (b1.getMax() == b2.getMax()) );
}

/**
 * Compare two AABoxes to see if they are not EXACTLY the same. In other words,
 * this comparison is done with zero tolerance.
 *
 * @param b1      the first box to compare
 * @param b2      the second box to compare
 *
 * @return  true if they are not equal, false otherwise
 */
template< class DATA_TYPE >
inline bool operator!=( const AABox<DATA_TYPE>& b1, const AABox<DATA_TYPE>& b2 )
{
   return (! (b1 == b2));
}


template< class DATA_TYPE >
inline AABox<DATA_TYPE> &operator += (AABox<DATA_TYPE>& b1, const Vec<DATA_TYPE, 3>& p)
{
    for (int i = 0; i < 3; ++i)
    {
        if (p[i] > b1.mMax[i])
            b1.mMax[i] = p[i];
        if (p[i] < b1.mMin[i])
            b1.mMin[i] = p[i];
    }
    b1.mEmpty = false;
    return b1;
}


/**
 * Compare two AABoxes to see if they are the same within the given tolerance.
 *
 * @param b1      the first box to compare
 * @param b2      the second box to compare
 * @param eps     the tolerance value to use
 *
 * @pre eps must be >= 0
 *
 * @return  true if their points are within the given tolerance of each other, false otherwise
 */
template< class DATA_TYPE >
inline bool isEqual( const AABox<DATA_TYPE>& b1, const AABox<DATA_TYPE>& b2, const DATA_TYPE& eps )
{
   gmtlASSERT( eps >= 0 );
   return (b1.isEmpty() == b2.isEmpty()) &&
          isEqual( b1.getMin(), b2.getMin(), eps ) &&
          isEqual( b1.getMax(), b2.getMax(), eps );
}
/** @} */

template< class DATA_TYPE >
inline void getCorners(const AABox<DATA_TYPE>& a, Point<DATA_TYPE, 3> corners[8])
{
    corners[0] = Point<DATA_TYPE, 3>(a.mMin[0], a.mMin[1], a.mMin[2]);
    corners[1] = Point<DATA_TYPE, 3>(a.mMin[0], a.mMin[1], a.mMax[2]);
    corners[2] = Point<DATA_TYPE, 3>(a.mMin[0], a.mMax[1], a.mMin[2]);
    corners[3] = Point<DATA_TYPE, 3>(a.mMin[0], a.mMax[1], a.mMax[2]);
    corners[4] = Point<DATA_TYPE, 3>(a.mMax[0], a.mMin[1], a.mMin[2]);
    corners[5] = Point<DATA_TYPE, 3>(a.mMax[0], a.mMin[1], a.mMax[2]);
    corners[6] = Point<DATA_TYPE, 3>(a.mMax[0], a.mMax[1], a.mMin[2]);
    corners[7] = Point<DATA_TYPE, 3>(a.mMax[0], a.mMax[1], a.mMax[2]);
}

}

#endif

