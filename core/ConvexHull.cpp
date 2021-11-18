// A C++ program to find convex hull of a set of points. Refer
// https://www.geeksforgeeks.org/orientation-3-ordered-points/
// for explanation of orientation()
#include <iostream>
#include <stack>
#include <stdlib.h>
#include <gmtl/gmtl.h>
#include <gmtl/Point.h>

using namespace std;

using namespace gmtl;

namespace sam
{
	// A global point needed for sorting points with reference
	// to the first point Used in compare function of qsort()
	Point2f p0;

	// A utility function to find next to top in a stack
	Point2f nextToTop(stack<Point2f>& S)
	{
		Point2f p = S.top();
		S.pop();
		Point2f res = S.top();
		S.push(p);
		return res;
	}

	// A utility function to swap two points
	void swap(Point2f& p1, Point2f& p2)
	{
		Point2f temp = p1;
		p1 = p2;
		p2 = temp;
	}

	// A utility function to return square of distance
	// between p1 and p2
	int distSq(Point2f p1, Point2f p2)
	{
		return (p1[0] - p2[0]) * (p1[0] - p2[0]) +
			(p1[1] - p2[1]) * (p1[1] - p2[1]);
	}

	// To find orientation of ordered triplet (p, q, r).
	// The function returns following values
	// 0 --> p, q and r are collinear
	// 1 --> Clockwise
	// 2 --> Counterclockwise
	int orientation(Point2f p, Point2f q, Point2f r)
	{
		int val = (q[1] - p[1]) * (r[0] - q[0]) -
			(q[0] - p[0]) * (r[1] - q[1]);

		if (val == 0) return 0; // collinear
		return (val > 0) ? 1 : 2; // clock or counterclock wise
	}

	// A function used by library function qsort() to sort an array of
	// points with respect to the first point
	int compare(const void* vp1, const void* vp2)
	{
		Point2f* p1 = (Point2f*)vp1;
		Point2f* p2 = (Point2f*)vp2;

		// Find orientation
		int o = orientation(p0, *p1, *p2);
		if (o == 0)
			return (distSq(p0, *p2) >= distSq(p0, *p1)) ? -1 : 1;

		return (o == 2) ? -1 : 1;
	}

	// Prints convex hull of a set of n points.
	void convexHull(Point2f points[], size_t n, std::vector<Point2f> &outpts)
	{
		// Find the bottommost point
		int ymin = points[0][1], min = 0;
		for (int i = 1; i < n; i++)
		{
			int y = points[i][1];

			// Pick the bottom-most or chose the left
			// most point in case of tie
			if ((y < ymin) || (ymin == y &&
				points[i][0] < points[min][0]))
				ymin = points[i][1], min = i;
		}

		// Place the bottom-most point at first position
		swap(points[0], points[min]);

		// Sort n-1 points with respect to the first point.
		// A point p1 comes before p2 in sorted output if p2
		// has larger polar angle (in counterclockwise
		// direction) than p1
		p0 = points[0];
		qsort(&points[1], n - 1, sizeof(Point2f), compare);

		// If two or more points make same angle with p0,
		// Remove all but the one that is farthest from p0
		// Remember that, in above sorting, our criteria was
		// to keep the farthest point at the end when more than
		// one points have same angle.
		int m = 1; // Initialize size of modified array
		for (int i = 1; i < n; i++)
		{
			// Keep removing i while angle of i and i+1 is same
			// with respect to p0
			while (i < n - 1 && orientation(p0, points[i],
				points[i + 1]) == 0)
				i++;


			points[m] = points[i];
			m++; // Update size of modified array
		}

		// If modified array of points has less than 3 points,
		// convex hull is not possible
		if (m < 3) return;

		stack<Point2f> S;
		// Create an empty stack and push first three points
		// to it.		
		S.push(points[0]);
		S.push(points[1]);
		S.push(points[2]);

		// Process remaining n-3 points
		for (int i = 3; i < m; i++)
		{
			// Keep removing top while the angle formed by
			// points next-to-top, top, and points[i] makes
			// a non-left turn
			while (S.size() > 1 && orientation(nextToTop(S), S.top(), points[i]) != 2)
				S.pop();
			S.push(points[i]);
		}

		// Now stack has the output points, print contents of stack
		while (!S.empty())
		{
			Point2f p = S.top();
			outpts.push_back(p);
			S.pop();
		}
	}


}