using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class TerrainQueryActorProbe : CombatActor
    {
        public IActorPlacementQuery Queries => SurfaceQuery;
    }

    public class TerrainQueryPerformanceTests
    {
        readonly List<GameObject> roots = new List<GameObject>();
        Scene localScene;
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            roots.Clear();
            if (localScene.IsValid() && localScene.isLoaded) yield return SceneManager.UnloadSceneAsync(localScene);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        BoxCollider2D Add(Vector2 at, bool marked = true, int layer = 0, bool oneWay = false)
        {
            var root = new GameObject("Terrain query fixture"); roots.Add(root);
            root.transform.position = at; root.layer = layer;
            var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(2, .1f);
            if (marked) root.AddComponent<ArenaSurface>().oneWay = oneWay;
            return box;
        }
        TerrainQueryActorProbe Actor()
        {
            var root = new GameObject("Buffered query actor"); roots.Add(root);
            return root.AddComponent<TerrainQueryActorProbe>();
        }

        [UnityTest] public IEnumerator TerrainFilterExcludesActorsKeepsCustomLayersAndNativeOrder()
        {
            Add(new Vector2(0, 4), false); var solid = Add(new Vector2(0, 3));
            Add(new Vector2(0, 2), layer: 9, oneWay: true); Add(new Vector2(0, 1), layer: 2);
            for (int i = 0; i < 80; i++) Add(new Vector2(0, 1.5f), false);
            Physics2D.SyncTransforms();
            Assert.That(solid.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("ArenaTerrain")));
            Assert.That((int)ArenaSurfaceQueries.Filter().layerMask & 1, Is.Zero);
            var query = Actor().Queries;
            var request = new ActorBoxCast(0, 5, .5f, .5f, 0, -1, 6);
            var native = Physics2D.BoxCastAll(new Vector2(0, 5), Vector2.one * .5f, 0, Vector2.down, 6)
                .Where(h => h.collider.GetComponent<ArenaSurface>()).ToArray();
            var contacts = query.BoxCast(request);
            Assert.That(contacts.Count, Is.EqualTo(2));
            for (int i = 0; i < contacts.Count; i++)
            {
                Assert.That(contacts[i].Distance, Is.EqualTo(native[i].distance));
                Assert.That(contacts[i].Top, Is.EqualTo(native[i].collider.bounds.max.y));
                Assert.That(contacts[i].OneWay, Is.EqualTo(native[i].collider.GetComponent<ArenaSurface>().oneWay));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator CrowdedTerrainQueriesGrowBuffersAndKeepSnapshotLifetime()
        {
            const int count = 80;
            for (int i = 0; i < count; i++) Add(new Vector2(0, i * .2f));
            Physics2D.SyncTransforms();
            var actor = Actor(); var query = actor.Queries;
            var request = new ActorBoxCast(0, 20, .5f, .5f, 0, -1, 25);
            var first = query.BoxCast(request); Assert.That(first.Count, Is.EqualTo(count));
            var lastDistance = first[first.Count - 1].Distance;
            Assert.That(query.BoxCast(request), Is.SameAs(first), "The actor consumes a reusable view before its next query.");
            Assert.That(query.Raycast(new ActorRayCast(0, 20, 0, -1, 25)).Count, Is.EqualTo(count));
            var overlaps = query.OverlapBox(new ActorOverlapBox(0, 8, 3, 20)); Assert.That(overlaps.Count, Is.EqualTo(count));
            var snapshot = new UnityActorSurfaceQuery(_ => false).BoxCast(request);
            roots[0].GetComponent<Collider2D>().enabled = false; Physics2D.SyncTransforms();
            Assert.That(query.BoxCast(request).Count, Is.EqualTo(count - 1));
            Assert.That(snapshot.Count, Is.EqualTo(count));
            Assert.That(snapshot[snapshot.Count - 1].Distance, Is.EqualTo(lastDistance));
            Assert.That(query.OverlapBox(new ActorOverlapBox(0, 8, 3, 20)), Is.SameAs(overlaps));
            Assert.That(overlaps.Count, Is.EqualTo(count - 1));
            yield return null;
        }

        [UnityTest] public IEnumerator ExplicitPhysicsSceneDoesNotReadDefaultSceneTerrain()
        {
            Add(new Vector2(0, 3));
            localScene = SceneManager.CreateScene("Terrain isolated physics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var local = Add(new Vector2(0, 1), oneWay: true);
            SceneManager.MoveGameObjectToScene(local.gameObject, localScene);
            Physics2D.SyncTransforms(); localScene.GetPhysicsScene2D().Simulate(.02f);
            var query = new UnityActorSurfaceQuery(_ => false, localScene.GetPhysicsScene2D());
            var contacts = query.BoxCast(new ActorBoxCast(0, 5, .5f, .5f, 0, -1, 6));
            Assert.That(contacts.Count, Is.EqualTo(1)); Assert.That(contacts[0].OneWay, Is.True);
            Assert.That(contacts[0].Top, Is.EqualTo(local.bounds.max.y));
            yield return null;
        }

        [UnityTest] public IEnumerator SupportBatchesRefreshAfterTerrainMovementDisableAndOffsetChanges()
        {
            var floor = Add(new Vector2(0, -.5f), oneWay: true); floor.size = new Vector2(2, 1);
            Physics2D.SyncTransforms(); var probe = new ArenaSupportProbe();
            using (probe.BeginReadBatch())
            {
                Assert.That(probe.TryGet(Vector2.zero, 0, .001f, out float height), Is.True); Assert.That(height, Is.Zero);
                using (probe.BeginReadBatch()) Assert.That(probe.TryGet(Vector2.zero, 3, .001f, out _), Is.False);
                Assert.That(probe.TryGet(Vector2.zero, 0, .001f, out height), Is.True); Assert.That(height, Is.Zero);
            }
            floor.enabled = false; Physics2D.SyncTransforms();
            using (probe.BeginReadBatch()) Assert.That(probe.TryGet(Vector2.zero, 0, .001f, out _), Is.False);
            floor.enabled = true; floor.transform.position += Vector3.up * 2; Physics2D.SyncTransforms();
            using (probe.BeginReadBatch())
            {
                Assert.That(probe.TryGet(Vector2.up * 2, 0, .001f, out float height), Is.True); Assert.That(height, Is.EqualTo(2));
                Assert.That(probe.TryGet(Vector2.zero, 0, .001f, out _), Is.False);
            }
            floor.enabled = false; Physics2D.SyncTransforms();
            Assert.That(probe.TryGet(Vector2.up * 2, 0, .001f, out _), Is.False, "Reads outside a batch are never cached.");
            yield return null;
        }
        [UnityTest] public IEnumerator TerrainOnlyArenaQueriesKeepOneWayAndTriggerPolicies()
        {
            var root = new GameObject("Terrain query arena"); roots.Add(root);
            var arena = root.AddComponent<CombatArena>();
            var terrain = Add(new Vector2(0, 1), oneWay: true);
            for (int i = 0; i < 40; i++) Add(new Vector2(0, 1), false);
            Physics2D.SyncTransforms();
            var from = new Vector2(-3, 1); var to = new Vector2(3, 1);
            Assert.That(arena.Occluded(from, to), Is.False);
            Assert.That(arena.Occluded(from, to, true), Is.True);
            Assert.That(arena.SolidOverlap(new Vector2(0, 1), Vector2.one), Is.False);
            terrain.GetComponent<ArenaSurface>().oneWay = false;
            Assert.That(arena.Occluded(from, to), Is.True);
            Assert.That(arena.SolidOverlap(new Vector2(0, 1), Vector2.one), Is.True);
            Assert.That(arena.SkillProjectileBlocked(new Vector2(0, 1), Vector2.one), Is.True);
            Assert.That(arena.SolidTravel(from, Vector2.one * .2f, Vector2.right, 6), Is.InRange(1.8f, 2f));
            Assert.That(arena.SkillProjectileTravel(from, Vector2.one * .2f, Vector2.right, 6), Is.InRange(1.8f, 2f));
            Assert.That(arena.FindFloor(new Vector2(0, 3), 6), Is.EqualTo(terrain.bounds.max.y));
            bool triggers = Physics2D.queriesHitTriggers;
            try
            {
                terrain.isTrigger = true; Physics2D.SyncTransforms();
                Physics2D.queriesHitTriggers = true; Assert.That(arena.Occluded(from, to), Is.True);
                Physics2D.queriesHitTriggers = false; Assert.That(arena.Occluded(from, to), Is.False);
            }
            finally { Physics2D.queriesHitTriggers = triggers; }
            Object.DestroyImmediate(root); roots.Remove(root);
            yield return null;
        }

        [UnityTest] public IEnumerator RuntimeMotorKeepsGroundWallCeilingAndSafePlatformDrop()
        {
            var root = new GameObject("Native terrain motor arena"); roots.Add(root);
            var arena = root.AddComponent<CombatArena>(); arena.loadCharacterArt = false;
            var camera = new GameObject("Native terrain camera"); roots.Add(camera);
            var content = Deviant.Integration.CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Camera = camera.AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Enemies = System.Array.Empty<CombatEnemySpawn>(); content.PlayerSpawn = new Vector2(35, 0);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            Assert.That(arena.Initialize(content), Is.True);
            var actor = Actor(); actor.Configure(arena, Vector2.zero, new Vector2(.9375f, 2), new Vector2(.1875f, 1), false);
            var floor = Add(new Vector2(0, -.5f)); floor.size = new Vector2(30, 1);
            var wall = Add(new Vector2(3, 3)); wall.size = new Vector2(.4f, 8);
            var ceiling = Add(new Vector2(0, 3)); ceiling.size = new Vector2(20, .5f);
            Physics2D.SyncTransforms();
            using (arena.AcquireManualSimulation())
            {
                Assert.That(actor.ProbeGround(), Is.True);
                actor.PhysicsStep(1f / 120, 0, false); Assert.That(actor.grounded, Is.True);
                actor.PhysicsStep(.1f, 50, false); Assert.That(actor.wallHit, Is.True); Assert.That(actor.Position.x, Is.InRange(2f, 2.2f));
                actor.ResetActor(Vector2.zero); actor.VelocityY = 20; Physics2D.SyncTransforms();
                actor.PhysicsStep(.1f, 0, false); Assert.That(actor.VelocityY, Is.Zero); Assert.That(actor.Position.y, Is.InRange(.70f, .76f));
                floor.enabled = wall.enabled = ceiling.enabled = false;
                var platform = Add(new Vector2(0, 1.95f), oneWay: true); platform.size = new Vector2(12, .1f);
                var obstruction = Add(new Vector2(0, 1.46f)); obstruction.size = new Vector2(12, 1);
                actor.ResetActor(new Vector2(0, 2)); Physics2D.SyncTransforms();
                Assert.That(actor.ProbeGround(), Is.True); Assert.That(actor.DropThrough(), Is.False);
                Assert.That(actor.Position.y, Is.EqualTo(2));
                obstruction.enabled = false; Physics2D.SyncTransforms();
                Assert.That(actor.DropThrough(), Is.True); Assert.That(actor.Position.y, Is.LessThan(2));
                Assert.That(actor.ProbeGround(), Is.False);
            }
            yield return null;
        }
    }
}
