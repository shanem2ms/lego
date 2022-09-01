using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Numerics;

namespace partmake
{
    public class OctTile : IEquatable<OctTile>
    {
        public int x;
        public int y;
        public int z;

        static bool Intersects(Vector3 mina, Vector3 maxa, Vector3 minb, Vector3 maxb)
        {
            const float allowedOverlap = 1;
            Vector3 v1 = maxa - minb;
            Vector3 v2 = maxb - mina;
            if (v1.X < allowedOverlap ||
                v1.Y < allowedOverlap ||
                v1.Z < allowedOverlap ||
                v2.X < allowedOverlap ||
                v2.Y < allowedOverlap ||
                v2.Z < allowedOverlap)
                return false;
            else
                return true;
        }

        public List<PartInst> parts = new List<PartInst>();

        public bool Equals(OctTile other)
        {
            return (x == other.x && y == other.y && z == other.z);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }

        public void CheckCollisions()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 mina = parts[i].MinBounds;
                Vector3 maxa = parts[i].MaxBounds;
                for (int j = i + 1; j < parts.Count; j++)
                {
                    Vector3 minb = parts[j].MinBounds;
                    Vector3 maxb = parts[j].MaxBounds;
                    if (Intersects(mina, maxa, minb, maxb))
                    {
                        parts[j].paletteIdx = 1;
                    }
                }
            }
        }
    }


    public class OctTree
    {
        public static float scale = 1.0f / 128.0f;

        HashSet<OctTile> octTiles = new HashSet<OctTile>();


        public void AddPart(PartInst p)
        {
            OctTile olookup = new OctTile();
            int oxmin = (int)(p.MinBounds.X * scale);
            int oymin = (int)(p.MinBounds.Y * scale);
            int ozmin = (int)(p.MinBounds.Z * scale);
            int oxmax = (int)(p.MaxBounds.X * scale);
            int oymax = (int)(p.MaxBounds.Y * scale);
            int ozmax = (int)(p.MaxBounds.Z * scale);
            for (int ox = oxmin; ox <= oxmax; ++ox)
            {
                for (int oy = oymin; oy <= oymax; ++oy)
                {
                    for (int oz = ozmin; oz <= ozmax; ++oz)
                    {
                        olookup.x = ox;
                        olookup.y = oy;
                        olookup.z = oz;
                        OctTile tile;
                        if (!octTiles.TryGetValue(olookup, out tile))
                        {
                            tile = new OctTile() { x = ox, y = oy, z = oz };
                            octTiles.Add(tile);
                        }
                        tile.parts.Add(p);
                    }
                }
            }
        }

        public void CheckCollisions()
        {
            foreach (var tile in octTiles)
            {
                tile.CheckCollisions();
            }
        }
    }
}
