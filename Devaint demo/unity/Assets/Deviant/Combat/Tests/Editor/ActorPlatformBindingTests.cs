using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorPlatformBindingTests
    {
        [UnityTest] public IEnumerator BindingsKeepOnlyClaimedReferencesAndNeverReuseOldHandles()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var roots = new List<GameObject>(); var boxes = new List<BoxCollider2D>();
            for (int i = 0; i < 2; i++) { var root = new GameObject("PlatformBinding" + i); roots.Add(root); root.transform.position = new Vector2(2000, 1999.95f); root.AddComponent<ArenaSurface>().oneWay = true; var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(8, .1f); boxes.Add(box); }
            try
            {
                Physics2D.SyncTransforms(); var state = new ActorPlatformState(); var bindings = new UnityActorPlatforms(state); var surfaces = new UnityActorSurfaceQuery(bindings.Contains); var body = new ActorBodyFrame(2000, 2000, 1, 2, .25f, 1, 1);
                var request = new ActorBoxCast(body.X + body.OffsetX * body.Facing, body.Y + .03f, body.Width - .08f, .04f, 0, -1, .08f);
                var nativeHits = Physics2D.BoxCastAll(new Vector2(request.X, request.Y), new Vector2(request.Width, request.Height), 0,
                    new Vector2(request.DirectionX, request.DirectionY), request.Distance)
                    .Where(hit => { if (!hit.collider) return false; var surface = hit.collider.GetComponent<ArenaSurface>(); return surface && surface.oneWay; }).ToArray();
                var facts = bindings.CastOneWayPlatforms(request); Assert.That(facts.Count, Is.EqualTo(2)); Assert.Throws<NotSupportedException>(() => ((IList<ActorPlatformContact>)facts)[0] = default); bindings.PruneUnused(); Assert.That(bindings.BindingCount, Is.Zero);
                Assert.That(facts.Select(contact => contact.PlaneY), Is.EqualTo(nativeHits.Select(hit => hit.point.y)), "Bindings preserve the native cast's exact ordered hit planes, including its contact margin.");
                Assert.That(facts.Select(contact => contact.NormalY), Is.EqualTo(nativeHits.Select(hit => hit.normal.y)));
                Assert.That(state.TryDrop(body, 0, 0, surfaces, bindings).Applied, Is.True); bindings.PruneUnused(); Assert.That(bindings.BindingCount, Is.EqualTo(1)); var oldKey = state.Read().Entries.Single().Key; var tracked = boxes.Single(bindings.Contains); foreach (var box in boxes) if (box != tracked) box.enabled = false;
                float originalPlane = state.Read().Entries.Single().PlaneY;
                Assert.That(originalPlane, Is.EqualTo(nativeHits.First(hit => hit.collider == tracked && hit.normal.y > .8f).point.y), "The retained plane belongs to the selected native platform contact.");
                tracked.enabled = false; Physics2D.SyncTransforms(); Assert.That(bindings.Observe(oldKey).Alive, Is.True, "Disable is not native object destruction.");
                tracked.enabled = true; tracked.transform.position += Vector3.up; Physics2D.SyncTransforms();
                Assert.That(state.Read().Entries.Single().PlaneY, Is.EqualTo(originalPlane));
                state.Refresh(.25f, originalPlane - 2.01f, 2, bindings); Assert.That(state.Contains(oldKey), Is.True, "Moving the collider upward cannot restore collision before clearance of the original plane.");
                tracked.transform.position -= Vector3.up * 2; Physics2D.SyncTransforms();
                state.Refresh(.25f, originalPlane - 2.03f, 2, bindings); bindings.PruneUnused();
                Assert.That(state.Read().Entries, Is.Empty, "Moving the collider downward cannot postpone clearance of the original plane.");
                tracked.transform.position += Vector3.up; Physics2D.SyncTransforms();
                state.Reset(); Assert.That(bindings.Contains(tracked), Is.False); bindings.PruneUnused(); Assert.That(bindings.BindingCount, Is.Zero); Assert.That(bindings.Observe(oldKey).Alive, Is.False);
                Physics2D.SyncTransforms(); Assert.That(state.TryDrop(body, 0, 0, surfaces, bindings).Applied, Is.True); bindings.PruneUnused(); var newKey = state.Read().Entries.Single().Key; Assert.That(newKey, Is.Not.EqualTo(oldKey));
                Object.DestroyImmediate(tracked.gameObject); state.Refresh(.1f, 2000, 2, bindings); bindings.PruneUnused(); Assert.That(state.Read().Entries, Is.Empty); Assert.That(bindings.BindingCount, Is.Zero); Assert.That(bindings.Observe(newKey).Alive, Is.False);
                Assert.Throws<ArgumentException>(() => bindings.Observe(new ActorPlatformKey(Guid.NewGuid(), Guid.NewGuid()))); Assert.Throws<ArgumentException>(() => bindings.CastOneWayPlatforms(default)); Assert.Throws<ArgumentNullException>(() => new UnityActorPlatforms(null));
            }
            finally { foreach (var root in roots) if (root) Object.Destroy(root); }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
