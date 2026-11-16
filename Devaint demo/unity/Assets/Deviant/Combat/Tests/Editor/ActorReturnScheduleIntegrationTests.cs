using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorReturnScheduleIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesOneOrderedScheduleAndClearsLostOrDeadBindings() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesOneOrderedScheduleAndClearsLostOrDeadBindings() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IReadOnlyList<ActorReturnReservation> Read(CombatPlayer player)
        {
            // Reflection allows compiling the same fixture with the exact old
            // partial during baseline runs; this test requires the new API.
            var property = typeof(CombatPlayer).GetProperty("ReturnReservations"); Assert.That(property, Is.Not.Null); return (IReadOnlyList<ActorReturnReservation>)property.GetValue(player);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var roots = new List<GameObject>();
            GameObject Surface(string name, Vector2 at, Vector2 size) { var root = new GameObject(name); roots.Add(root); root.transform.position = at; root.AddComponent<ArenaSurface>(); root.AddComponent<BoxCollider2D>().size = size; return root; }
            Surface("ReturnScheduleFloor", new Vector2(0, 999.5f), new Vector2(100, 1));
            var a = new GameObject("ReturnScheduleFirst").AddComponent<CombatActor>(); var b = new GameObject("ReturnScheduleSecond").AddComponent<CombatActor>(); a.Configure(arena, new Vector2(4, 1000), new Vector2(1, 2), Vector2.up, false); b.Configure(arena, new Vector2(8, 1000), new Vector2(1, 2), Vector2.up, false);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    void Reset()
                    { driver.Reset(); p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.SyncFacing(1); foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); } Physics2D.SyncTransforms(); }
                    var shot = arena.catalog.forms[Array.FindIndex(arena.catalog.forms, f => f.id == "shuriken")].specials[2].ranged;
                    Reset(); var first = p.weapons[0]; var second = p.weapons[1]; Assert.That(p.TryShurikenVisit(a, first, shot.returnDelay, shot.behindGap), Is.True); var intermediate = p.Position; Assert.That(p.TryShurikenVisit(b, second, shot.returnDelay, shot.behindGap), Is.True); var snapshot = Read(p);
                    Assert.That(snapshot.Count, Is.EqualTo(2)); Assert.That(snapshot[0].WeaponId, Is.EqualTo(first.InstanceId)); Assert.That(snapshot[1].WeaponId, Is.EqualTo(second.InstanceId)); Assert.That(snapshot[0].Origin.X, Is.Zero); Assert.That(snapshot[1].Origin.X, Is.EqualTo(intermediate.x)); Assert.That(snapshot[0].Due, Is.EqualTo(5)); Assert.Throws<NotSupportedException>(() => ((IList<ActorReturnReservation>)snapshot)[0] = snapshot[1]);
                    p.Position = new Vector2(12, 1000); Physics2D.SyncTransforms(); for (int tick = 0; tick < 252; tick++) driver.Step(.02f);
                    Assert.That(p.Position.x, Is.EqualTo(intermediate.x).Within(.0001f), "Same-tick due returns keep insertion order."); Assert.That(Read(p), Is.Empty); Assert.That(p.ShurikenReturnPending(first), Is.False); Assert.That(p.ShurikenReturnPending(second), Is.False); Assert.That(snapshot.Count, Is.EqualTo(2));
                    Reset(); Assert.That(p.TryShurikenVisit(a, first, shot.returnDelay, shot.behindGap), Is.True); p.Position = new Vector2(12, 1000); var wall = Surface("ReturnScheduleBlockedOrigin", new Vector2(0, 1001), new Vector2(5, 4)); Object.DestroyImmediate(a.gameObject); Physics2D.SyncTransforms(); for (int tick = 0; tick < 252; tick++) driver.Step(.02f);
                    Assert.That(p.Position.x, Is.EqualTo(12).Within(.0001f)); Assert.That(Read(p), Is.Empty); Assert.That(p.ShurikenReturnPending(first), Is.False); Object.DestroyImmediate(wall); roots.Remove(wall);
                    Reset(); Assert.That(p.TryShurikenVisit(b, first, shot.returnDelay, shot.behindGap), Is.True); var beforeDeath = Read(p); p.health = 0; p.Die(); Assert.That(Read(p), Is.Empty); Assert.That(beforeDeath.Count, Is.EqualTo(1)); Assert.That(p.ShurikenReturnPending(first), Is.False);
                    Reset(); Assert.That(p.TryShurikenVisit(b, first, shot.returnDelay, shot.behindGap), Is.True); var beforeReset = Read(p); driver.Reset(); Assert.That(Read(p), Is.Empty); Assert.That(beforeReset.Count, Is.EqualTo(1)); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { if (a) Object.Destroy(a.gameObject); Object.Destroy(b.gameObject); foreach (var root in roots) if (root) Object.Destroy(root); }
        }
    }
}
