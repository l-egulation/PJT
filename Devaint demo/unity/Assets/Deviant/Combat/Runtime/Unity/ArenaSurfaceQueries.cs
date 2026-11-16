using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Default-layer terrain is routed here when its marker wakes. Existing custom
    // layers retain their collision/camera meaning and are included in queries too.
    public static class ArenaSurfaceQueries
    {
        public const int TerrainLayer = ArenaSurface.TerrainLayer;
        internal static void Register(ArenaSurface surface)
        {
            if (surface.gameObject.layer == 0) surface.gameObject.layer = TerrainLayer;
        }
        public static ContactFilter2D Filter() => ArenaSurface.QueryFilter;
    }

    // List overloads grow to include every hit; a crowded query never truncates
    // at a fixed array capacity. Each caller owns its own reusable scratch lists.
    sealed class ArenaSurfaceQueryBuffer
    {
        readonly PhysicsScene2D? scene;
        public readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>(16);
        public readonly List<Collider2D> Overlaps = new List<Collider2D>(16);
        PhysicsScene2D Scene => scene ?? Physics2D.defaultPhysicsScene;
        public ArenaSurfaceQueryBuffer(PhysicsScene2D? scene = null) { this.scene = scene; }

        public void BoxCast(Vector2 at, Vector2 size, Vector2 direction, float distance)
        {
            Hits.Clear();
            Scene.BoxCast(at, size, 0, direction, distance, ArenaSurfaceQueries.Filter(), Hits);
        }
        public void Raycast(Vector2 at, Vector2 direction, float distance)
        {
            Hits.Clear();
            Scene.Raycast(at, direction, distance, ArenaSurfaceQueries.Filter(), Hits);
        }
        public void Linecast(Vector2 from, Vector2 to)
        {
            Hits.Clear();
            Scene.Linecast(from, to, ArenaSurfaceQueries.Filter(), Hits);
        }
        public void OverlapBox(Vector2 at, Vector2 size)
        {
            Overlaps.Clear();
            Scene.OverlapBox(at, size, 0, ArenaSurfaceQueries.Filter(), Overlaps);
        }
    }
}
