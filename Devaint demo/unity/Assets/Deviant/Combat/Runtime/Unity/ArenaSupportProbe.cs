using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // A read batch must contain only synchronous geometry reads. It cannot span
    // movement, terrain edits, intent submission, or any gameplay callback.
    public sealed class ArenaSupportProbe
    {
        readonly ArenaSurfaceQueryBuffer query = new ArenaSurfaceQueryBuffer();
        readonly Dictionary<(Vector2 feet, float offset, float epsilon, float skin), (bool found, float height)> cache
            = new Dictionary<(Vector2, float, float, float), (bool, float)>(8);
        int readDepth;

        public ReadBatch BeginReadBatch()
        {
            if (readDepth++ == 0) cache.Clear();
            return new ReadBatch(this);
        }
        public readonly struct ReadBatch : IDisposable
        {
            readonly ArenaSupportProbe probe;
            internal ReadBatch(ArenaSupportProbe probe) { this.probe = probe; }
            public void Dispose() { if (probe != null) probe.readDepth--; }
        }

        public bool TryGet(Vector2 feet, float centerOffset, float epsilon, out float height)
        {
            float skin = Physics2D.defaultContactOffset;
            var key = (feet, centerOffset, epsilon, skin);
            if (readDepth > 0 && cache.TryGetValue(key, out var known))
            { height = known.height; return known.found; }

            float closest = float.PositiveInfinity;
            height = 0; bool found = false;
            query.Raycast(feet + new Vector2(centerOffset, skin), Vector2.down, skin * 2);
            for (int i = 0; i < query.Hits.Count; i++)
            {
                var hit = query.Hits[i];
                if (!hit.collider || !hit.collider.TryGetComponent<ArenaSurface>(out _) || hit.normal.y < .99f) continue;
                float distance = Mathf.Abs(hit.point.y - feet.y);
                if (distance > skin + epsilon || distance >= closest) continue;
                closest = distance; height = hit.point.y; found = true;
            }
            if (readDepth > 0) cache[key] = (found, height);
            return found;
        }
    }
}
