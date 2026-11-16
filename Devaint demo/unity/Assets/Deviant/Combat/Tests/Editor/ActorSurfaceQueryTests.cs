using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorSurfaceQueryTests
    {
        [UnityTest] public IEnumerator NativeAdapterKeepsCurrentSurfaceFactsAndPerActorIgnoreBinding()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var roots = new List<GameObject>();
            BoxCollider2D Add(string name, float y, bool marked, bool oneWay = false)
            {
                var root = new GameObject(name); roots.Add(root); root.transform.position = new Vector3(2000, y, 0); var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(5, .2f);
                if (marked) root.AddComponent<ArenaSurface>().oneWay = oneWay; return box;
            }
            try
            {
                Add("unmarked", 2004, false); var solid = Add("solid", 2002, true); var platform = Add("one-way", 2000, true, true); var ignored = Add("ignored", 1998, true); Physics2D.SyncTransforms();
                var adapter = new UnityActorSurfaceQuery(c => c == ignored); var request = new ActorBoxCast(2000, 2006, 1, 1, 0, -1, 10);
                var native = Physics2D.BoxCastAll(new Vector2(2000, 2006), Vector2.one, 0, Vector2.down, 10).Where(h => h.collider && h.collider.GetComponent<ArenaSurface>()).ToArray();
                var facts = adapter.BoxCast(request); Assert.That(facts.Count, Is.EqualTo(3)); Assert.That(facts.Count, Is.EqualTo(native.Length));
                for (int i = 0; i < native.Length; i++)
                {
                    Assert.That(facts[i].Distance, Is.EqualTo(native[i].distance)); Assert.That(facts[i].NormalX, Is.EqualTo(native[i].normal.x)); Assert.That(facts[i].NormalY, Is.EqualTo(native[i].normal.y)); Assert.That(facts[i].Top, Is.EqualTo(native[i].collider.bounds.max.y));
                    Assert.That(facts[i].OneWay, Is.EqualTo(native[i].collider == platform)); Assert.That(facts[i].Ignored, Is.EqualTo(native[i].collider == ignored));
                }
                var nativeRays = Physics2D.RaycastAll(new Vector2(2000, 2006), Vector2.down, 10).Where(h => h.collider && h.collider.GetComponent<ArenaSurface>()).ToArray();
                var rays = adapter.Raycast(new ActorRayCast(2000, 2006, 0, -1, 10)); Assert.That(rays.Count, Is.EqualTo(3));
                for (int i = 0; i < nativeRays.Length; i++) { Assert.That(rays[i].Y, Is.EqualTo(nativeRays[i].point.y)); Assert.That(rays[i].NormalY, Is.EqualTo(nativeRays[i].normal.y)); Assert.That(rays[i].Ignored, Is.EqualTo(nativeRays[i].collider == ignored)); }
                var nativeBoxes = Physics2D.OverlapBoxAll(new Vector2(2000, 2001), new Vector2(6, 10), 0).Where(c => c && c.GetComponent<ArenaSurface>()).ToArray();
                var boxes = adapter.OverlapBox(new ActorOverlapBox(2000, 2001, 6, 10)); Assert.That(boxes.Count, Is.EqualTo(3));
                for (int i = 0; i < nativeBoxes.Length; i++) { var bounds = nativeBoxes[i].bounds; Assert.That(new[] { boxes[i].MinX, boxes[i].MinY, boxes[i].MinZ, boxes[i].MaxX, boxes[i].MaxY, boxes[i].MaxZ }, Is.EqualTo(new[] { bounds.min.x, bounds.min.y, bounds.min.z, bounds.max.x, bounds.max.y, bounds.max.z })); Assert.That(boxes[i].OneWay, Is.EqualTo(nativeBoxes[i] == platform)); }
                Assert.Throws<NotSupportedException>(() => ((IList<ActorRayContact>)rays)[0] = default); Assert.Throws<NotSupportedException>(() => ((IList<ActorSurfaceBounds>)boxes)[0] = default);
                Assert.Throws<NotSupportedException>(() => ((IList<ActorSurfaceContact>)facts)[0] = default);
                var otherActor = new UnityActorSurfaceQuery(_ => false).BoxCast(request); Assert.That(otherActor.All(h => !h.Ignored), Is.True); Assert.That(facts.Any(h => h.Ignored), Is.True);
                var at = solid.transform.position; Object.DestroyImmediate(platform.gameObject); Physics2D.SyncTransforms(); Assert.That(adapter.BoxCast(request).Count, Is.EqualTo(2)); Assert.That(facts.Count, Is.EqualTo(3), "Old observations are immutable values and hold no live collider."); Assert.That(solid.transform.position, Is.EqualTo(at));
                Assert.That(rays.Count, Is.EqualTo(3)); Assert.That(boxes.Count, Is.EqualTo(3)); Assert.That(adapter.OverlapBox(new ActorOverlapBox(2000, 2001, 6, 10)).Count, Is.EqualTo(2));
                Assert.Throws<ArgumentNullException>(() => new UnityActorSurfaceQuery(null)); Assert.Throws<ArgumentException>(() => adapter.BoxCast(default)); Assert.Throws<ArgumentException>(() => adapter.Raycast(default)); Assert.Throws<ArgumentException>(() => adapter.OverlapBox(default));
            }
            finally { foreach (var root in roots) if (root) Object.Destroy(root); }
        }
        [UnityTest] public IEnumerator DenseQueriesKeepEverySurfaceAndObserveGeometryTriggerAndIgnoreChanges()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode();
            var root = new GameObject("Dense surface query");
            bool originalTriggers = Physics2D.queriesHitTriggers;
            try
            {
                var surfaces = new List<BoxCollider2D>();
                for (int i = 0; i < 40; i++)
                {
                    var go = new GameObject("floor " + i); go.transform.SetParent(root.transform);
                    go.transform.position = new Vector2(2000, 2000 - i);
                    var box = go.AddComponent<BoxCollider2D>(); box.size = new Vector2(4, .2f);
                    go.AddComponent<ArenaSurface>(); surfaces.Add(box);
                    Assert.That(go.layer, Is.EqualTo(ArenaSurface.TerrainLayer));
                }
                for (int i = 0; i < 96; i++)
                {
                    var go = new GameObject("crowded actor " + i); go.transform.SetParent(root.transform);
                    go.transform.position = new Vector2(2000, 2000); go.AddComponent<BoxCollider2D>();
                }
                // Explicit authoring layers keep their original native eligibility.
                surfaces[0].gameObject.layer = 9; surfaces[1].isTrigger = true;
                surfaces[2].gameObject.layer = 2;
                Physics2D.queriesHitTriggers = true; Physics2D.SyncTransforms();
                var ignored = new HashSet<Collider2D>();
                var adapter = new UnityActorSurfaceQuery(ignored.Contains);
                var request = new ActorBoxCast(2000, 2002, 1, 1, 0, -1, 50);
                var first = adapter.BoxCast(request);
                Assert.That(first.Count, Is.EqualTo(39), "growing buffers must never truncate crowded results");
                Assert.That(adapter.BoxCast(request), Is.SameAs(first), "identical immutable observations can be reused");
                ignored.Add(surfaces[0]); var changed = adapter.BoxCast(request);
                Assert.That(changed.Any(c => c.Ignored), Is.True); Assert.That(first.Any(c => c.Ignored), Is.False);
                surfaces[0].transform.position += Vector3.right * 10;
                surfaces[3].enabled = false; Physics2D.queriesHitTriggers = false; Physics2D.SyncTransforms();
                Assert.That(adapter.BoxCast(request).Count, Is.EqualTo(36));
                Assert.That(first.Count, Is.EqualTo(39));
            }
            finally { Physics2D.queriesHitTriggers = originalTriggers; Object.DestroyImmediate(root); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
