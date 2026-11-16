using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Scene-owned broad phase for authored terrain that does not move or resize.
    // Rebuild after geometry changes; never share across scene lifetimes.
    public sealed class StaticArenaSurfaceIndex
    {
        const float BucketWidth = 16;
        sealed class Entry
        {
            public ArenaSurface surface;
            public Collider2D collider;
            public Bounds bounds;
        }
        readonly Dictionary<int,List<Entry>> buckets = new Dictionary<int,List<Entry>>();
        readonly HashSet<Entry> visited = new HashSet<Entry>();

        public StaticArenaSurfaceIndex(IEnumerable<ArenaSurface> surfaces) { Rebuild(surfaces); }
        public void Rebuild(IEnumerable<ArenaSurface> surfaces)
        {
            buckets.Clear();visited.Clear();
            foreach(var surface in surfaces)
            {
                if(!surface)continue;
                var collider=surface.GetComponent<Collider2D>();
                if(!collider||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                var entry=new Entry{surface=surface,collider=collider,bounds=collider.bounds};
                int first=Mathf.FloorToInt(entry.bounds.min.x/BucketWidth),last=Mathf.FloorToInt(entry.bounds.max.x/BucketWidth);
                for(int x=first;x<=last;x++)
                {
                    if(!buckets.TryGetValue(x,out var bucket))buckets.Add(x,bucket=new List<Entry>());
                    bucket.Add(entry);
                }
            }
        }
        public void Query(Bounds area,List<ArenaSurface> results)
        {
            results.Clear();visited.Clear();
            int first=Mathf.FloorToInt(area.min.x/BucketWidth),last=Mathf.FloorToInt(area.max.x/BucketWidth);
            for(int x=first;x<=last;x++)
            {
                if(!buckets.TryGetValue(x,out var bucket))continue;
                foreach(var entry in bucket)
                {
                    if(!visited.Add(entry)||!entry.surface||!entry.collider||!entry.collider.enabled||!entry.collider.gameObject.activeInHierarchy)continue;
                    var b=entry.bounds;
                    if(b.max.x>=area.min.x&&b.min.x<=area.max.x&&b.max.y>=area.min.y&&b.min.y<=area.max.y)results.Add(entry.surface);
                }
            }
        }
    }
}
