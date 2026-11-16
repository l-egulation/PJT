using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ActorReturnPlacementIntegrationTests
    {
        [Serializable] sealed class Row { public int scenario, tick, facing, selected; public int[] bits; public bool pending, dashing; }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        [UnityTest] public IEnumerator GameReturnGeometryPreservesPolicyAndObservedTicks() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabReturnGeometryPreservesPolicyAndObservedTicks() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; var target = new GameObject("ReturnPlacementTarget").AddComponent<CombatActor>(); target.Configure(arena, new Vector2(4, 1000), new Vector2(1, 2), new Vector2(.2f, 1), false);
            var obstacles = new List<GameObject>(); var trace = new Trace();
            GameObject Surface(string name, Vector2 at, Vector2 size)
            { var root = new GameObject(name); root.transform.position = at; root.AddComponent<ArenaSurface>(); root.AddComponent<BoxCollider2D>().size = size; return root; }
            var floor = Surface("ReturnPlacementFloor", new Vector2(0, 999.5f), new Vector2(100, 1));
            void Wall(string name, Vector2 at, Vector2 size) => obstacles.Add(Surface(name, at, size));
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    for (int scenario = 0; scenario < 9; scenario++)
                    {
                        foreach (var root in obstacles) Object.DestroyImmediate(root); obstacles.Clear(); driver.Reset(); p.selected = 0; p.InputBlocked = false;
                        foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(-35, 1000); }
                        foreach (var weapon in p.weapons) weapon.StatSlots.Clear(); p.Reconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "shuriken"), 2);
                        p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.SyncFacing(scenario % 2 == 0 ? 1 : -1); int originFacing = p.facing;
                        target.ResetActor(new Vector2(4, 1000)); target.SyncFacing(scenario == 8 ? -1 : 1); var source = p.Weapon; var shot = arena.catalog.forms[source.FormIndex].specials[2].ranged;
                        if (scenario == 6) Wall("ReturnPathBlocker", new Vector2(1, 1001), new Vector2(.2f, 3)); Physics2D.SyncTransforms(); bool visited = p.TryShurikenVisit(target, source, shot.returnDelay, shot.behindGap);
                        Assert.That(visited, Is.EqualTo(scenario != 6));
                        if (visited)
                        {
                            Assert.That(p.ShurikenReturnRemaining(source), Is.EqualTo(5)); p.Position = new Vector2(scenario == 3 || scenario == 7 ? 10.5f : 8, 1000); p.velocity = Vector2.zero;
                            if (scenario == 1) Wall("ReturnOriginThin", new Vector2(0, 1001), new Vector2(.1f, 2));
                            if (scenario == 2) { Wall("ReturnVertical", new Vector2(.1875f, 1001), new Vector2(.1f, 8)); Wall("ReturnHorizontal", new Vector2(0, 1000.1f), new Vector2(4, .1f)); }
                            if (scenario == 3 || scenario == 5 || scenario == 7) Wall("ReturnOriginWide", new Vector2(0, 1001), new Vector2(5, 4));
                            if (scenario == 3) target.Position = new Vector2(6, 1000);
                            if (scenario == 4 || scenario == 5) target.health = 0;
                            if (scenario == 7) Wall("ReturnFrontBlocked", new Vector2(5.60625f, 1001), new Vector2(2, 3));
                            if (scenario == 8) p.selected = 1;
                        }
                        Physics2D.SyncTransforms();
                        for (int tick = 0; tick < 260; tick++)
                        {
                            if (scenario == 2 && tick == 249) { p.Position = new Vector2(8, 1000.2f); p.velocity = Vector2.zero; Physics2D.SyncTransforms(); }
                            driver.Step(.02f, new CombatInput(0, scenario == 8 && tick == 245 ? CombatButtons.Dash : CombatButtons.None));
                            trace.rows.Add(new Row { scenario = scenario, tick = tick, facing = p.facing, selected = p.selected, bits = new[] { Bits(arena.Now), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y), Bits(target.health), Bits(p.ShurikenReturnRemaining(source)), Bits(source.Cooldown.NextReady) }, pending = p.ShurikenReturnPending(source), dashing = p.Dashing });
                            if (visited && tick == 240) Assert.That(p.ShurikenReturnPending(source), Is.True);
                            if (scenario == 8 && tick == 247) Assert.That(p.Dashing, Is.True);
                        }
                        Assert.That(p.ShurikenReturnPending(source), Is.False); Assert.That(target.health, Is.EqualTo(scenario == 4 || scenario == 5 ? 0 : 100), "Movement/return never adds damage.");
                        if (scenario == 0 || scenario == 4 || scenario == 8) { Assert.That(p.Position.x, Is.EqualTo(0).Within(.0001f)); Assert.That(p.facing, Is.EqualTo(originFacing)); }
                        if (scenario == 1 || scenario == 2) { Assert.That(Vector2.Distance(p.Position, new Vector2(0, 1000)), Is.LessThanOrEqualTo(1.001f)); Assert.That(p.Blocked(p.Position, p.facing), Is.False); }
                        if (scenario == 2) { Assert.That(p.Position.x, Is.GreaterThan(.4f)); Assert.That(p.Position.y, Is.GreaterThan(1000.1f)); }
                        if (scenario == 3) { Assert.That(p.facing, Is.EqualTo(-target.facing)); Assert.That(p.bodyCollider.bounds.min.x - target.bodyCollider.bounds.max.x, Is.EqualTo(.25f).Within(.001f)); }
                        if (scenario == 5 || scenario == 7) Assert.That(p.Position.x, Is.EqualTo(scenario == 5 ? 8 : 10.5f).Within(.0001f));
                        if (scenario == 6) Assert.That(p.Position.x, Is.EqualTo(0));
                        if (scenario == 8) { Assert.That(p.selected, Is.EqualTo(1)); Assert.That(p.Dashing, Is.False); }
                        Assert.That(arena.SimulationFaulted, Is.False);
                    }
                }
                Assert.That(trace.rows.Count, Is.EqualTo(2340)); var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-actorReturnTraceRoot"); if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { Object.Destroy(target.gameObject); Object.Destroy(floor); foreach (var root in obstacles) Object.Destroy(root); }
        }
    }
}
