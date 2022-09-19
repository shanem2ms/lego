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
        public List<PartInst> intersectparts = new List<PartInst>();
        public List<PartInst> mainparts = new List<PartInst>();
        static Random random = new Random();
        public Vector3 tileColor = new Vector3(random.NextSingle(),
            random.NextSingle(),
            random.NextSingle());

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
            for (int i = 0; i < intersectparts.Count; i++)
            {
                Vector3 mina = intersectparts[i].MinBounds;
                Vector3 maxa = intersectparts[i].MaxBounds;
                for (int j = i + 1; j < intersectparts.Count; j++)
                {
                    Vector3 minb = intersectparts[j].MinBounds;
                    Vector3 maxb = intersectparts[j].MaxBounds;
                    if (Intersects(mina, maxa, minb, maxb))
                    {
                        //
                    }
                }
            }
        }
        public bool CheckCollision(PartInst p)
        {
            Vector3 mina = p.MinBounds;
            Vector3 maxa = p.MaxBounds;
            for (int j = 0; j < intersectparts.Count; j++)
            {
                Vector3 minb = intersectparts[j].MinBounds;
                Vector3 maxb = intersectparts[j].MaxBounds;
                if (Intersects(mina, maxa, minb, maxb))
                {
                    return true;
                }
            }
            return false;
        }
        public void AddColliders(BulletSimulation bulletSimulation)
        {
            var partlist = mainparts.Where(p => p.grpId == -1);
            RigidBody rb = new RigidBody(partlist);
            bulletSimulation.AddObj(rb);
        }
    }


        public class OctTree
    {
        public static float scale = 1.0f / 128.0f;

        HashSet<OctTile> octTiles = new HashSet<OctTile>();


        public void AddColliders(BulletSimulation bulletSimulation)
        {
            foreach (OctTile octTile in octTiles)
            {
                octTile.AddColliders(bulletSimulation);
            }
        }
        public bool CollisionCheck(PartInst p)
        {
            OctTile olookup = new OctTile();
            int oxmin = (int)(p.MinBounds.X * scale);
            int oymin = (int)(p.MinBounds.Y * scale);
            int ozmin = (int)(p.MinBounds.Z * scale);
            int oxmax = (int)(p.MaxBounds.X * scale);
            int oymax = (int)(p.MaxBounds.Y * scale);
            int ozmax = (int)(p.MaxBounds.Z * scale);
            bool collides = false;
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
                        if (octTiles.TryGetValue(olookup, out tile))
                        {
                            collides |= tile.CheckCollision(p);
                        }
                    }
                }
            }
            return collides;
        }



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
                        tile.intersectparts.Add(p);
                        p.octTiles.Add(tile);
                    }
                }
            }
            {
                Vector3 v = (p.MinBounds + p.MaxBounds) * 0.5f;
                int oxw = (int)(v.X * scale);
                int oyw = (int)(v.Y * scale);
                int ozw = (int)(v.Z * scale);
                OctTile tile;
                if (!octTiles.TryGetValue(olookup, out tile))
                {
                    tile = new OctTile() { x = oxw, y = oyw, z = ozw };
                    octTiles.Add(tile);
                }
                tile.mainparts.Add(p);
                p.mainTile = tile;
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
