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
    public class BombProjectileDefinitionTests
    {
        static int Form(CombatArena arena, string id) => Array.FindIndex(arena.catalog.forms, item => item.id == id);
        static void CenterAt(CombatActor actor, Vector2 center) => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        static GameObject Surface(Vector2 position)
        {
            var result = new GameObject("BombSnapshotTrigger"); result.transform.position = position;
            result.AddComponent<BoxCollider2D>().size = new Vector2(.02f, .02f); result.AddComponent<ArenaSurface>(); return result;
        }
        static void ParkActors(CombatArena arena)
        {
            var player = arena.Player; player.Position = new Vector2(-20, 1000); player.velocity = Vector2.zero;
            player.SyncFacing(1); player.grounded = true; player.axis = 0;
            for (int i = 0; i < arena.enemies.Count; i++)
            {
                var enemy = arena.enemies[i]; enemy.aggressive = false; enemy.ResetActor(new Vector2(70 + i * 10, 1000));
                enemy.health = 1000; enemy.SyncFacing(1);
            }
            Physics2D.SyncTransforms();
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, int scenario)
        {
            driver.Reset(); var player = arena.Player; player.InputBlocked = false; player.UseTestInput = true; player.selected = 0;
            ParkActors(arena);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            int special = scenario == 2 ? 1 : scenario >= 3 && scenario <= 5 || scenario == 9 || scenario >= 11 ? 3 : 0;
            Assert.That(player.TryReconfigure(0, Form(arena, scenario == 9 ? "launcher" : "bomb"), special, out string error), Is.True, error);
        }
        [UnityTest] public IEnumerator GameSourceEditsCannotReinterpretExistingBombs()
            => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabSourceEditsCannotReinterpretExistingBombs()
            => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        // These are intended bug-before/fix-after assertions, not preservation
        // traces. Only pre-existing native APIs are used so the old consumer runs too.
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; Assert.That(Application.isPlaying, Is.True);
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var floor = new GameObject("BombSnapshotFloor"); floor.transform.position = new Vector2(0, 999.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(300, 1); floor.AddComponent<ArenaSurface>();
            bool oldBoxes = arena.showBoxes;
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    arena.showBoxes = false;
                    for (int scenario = 0; scenario < 13; scenario++)
                    {
                        Setup(arena, driver, scenario);
                        // Edit the actual catalog DTO supplied to Spawn, not a shadow definition.
                        var data = scenario == 9 ? player.Form.specials[3].bomb : scenario == 2 || scenario >= 3 && scenario <= 5 || scenario >= 11 ? player.Special.bomb : player.Form.basics[0].bomb;
                        string original = JsonUtility.ToJson(data); var geometry = new List<GameObject>();
                        var first = arena.enemies[0]; var second = arena.enemies[1]; var origin = new Vector2(0, 1008);
                        BombProjectile Spawn(Vector2 at, Vector2 velocity, float damage = 18)
                            => BombProjectile.Spawn(arena, player, at, velocity, data, damage);
                        try
                        {
                            switch (scenario)
                            {
                                case 0:
                                    var flight = Spawn(origin, new Vector2(3, 4), 37);
                                    data.gravity = 0; data.angle = -90; data.speed = 1; data.spawnHeight = 7; data.spawnGap = .5f;
                                    flight.Step(.2f);
                                    Assert.That(flight.Position.x - origin.x, Is.EqualTo(.6f).Within(.0001f));
                                    Assert.That(flight.Position.y - origin.y, Is.EqualTo(.4f).Within(.0002f)); Assert.That(flight.velocity.y, Is.EqualTo(0).Within(.000001f));
                                    Assert.That(flight.damage, Is.EqualTo(37));
                                    var nextFlight = arena.SpawnBomb(player, data, 37, 1); float nextHeight = nextFlight.Position.y;
                                    Assert.That(nextHeight, Is.EqualTo(1007)); Assert.That(nextFlight.velocity.x, Is.EqualTo(0).Within(.000001f)); Assert.That(nextFlight.velocity.y, Is.EqualTo(-1));
                                    nextFlight.Step(.2f); Assert.That(nextFlight.Position.y - nextHeight, Is.EqualTo(-.2f).Within(.0002f));
                                    Assert.That(nextFlight.velocity.y, Is.EqualTo(-1)); Assert.That(nextFlight.damage, Is.EqualTo(37)); break;
                                case 1:
                                    var contact = Spawn(origin, Vector2.zero); data.radius = .01f;
                                    CenterAt(first, origin + Vector2.up * (first.size.y / 2 + .15f)); Physics2D.SyncTransforms(); contact.SweepTo(origin);
                                    Assert.That(first.health, Is.EqualTo(982)); Assert.That(first.ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    var nextContact = Spawn(origin, Vector2.zero); nextContact.SweepTo(origin);
                                    Assert.That(arena.bombs.Contains(nextContact), Is.True); Assert.That(first.ReceivedHitCount, Is.EqualTo(1));
                                    CenterAt(first, origin); Physics2D.SyncTransforms(); nextContact.SweepTo(origin); nextContact.SweepTo(origin);
                                    Assert.That(first.health, Is.EqualTo(964)); Assert.That(first.ReceivedHitCount, Is.EqualTo(2)); break;
                                case 2:
                                    var blast = Spawn(origin, Vector2.zero, 30); data.blastRadius = .1f;
                                    CenterAt(first, origin); CenterAt(second, origin + Vector2.right * 2.8f); Physics2D.SyncTransforms(); blast.SweepTo(origin);
                                    Assert.That(first.health, Is.EqualTo(970)); Assert.That(second.health, Is.EqualTo(970));
                                    Assert.That(arena.bombBursts.Count, Is.EqualTo(1)); Assert.That(arena.bombBursts[0].radius, Is.EqualTo(3));
                                    var nextBlast = Spawn(origin, Vector2.zero, 30); nextBlast.SweepTo(origin);
                                    Assert.That(first.health, Is.EqualTo(940)); Assert.That(second.health, Is.EqualTo(970));
                                    Assert.That(arena.bombBursts.Count, Is.EqualTo(2)); Assert.That(arena.bombBursts[1].radius, Is.EqualTo(.1f)); break;
                                case 3:
                                    var deadline = Spawn(new Vector2(0, 2000), Vector2.zero, 50); data.lifetime = .2f; deadline.Step(.21f);
                                    Assert.That(arena.BombExplosionCount, Is.Zero); Assert.That(arena.bombs.Contains(deadline), Is.True);
                                    var nextDeadline = Spawn(new Vector2(8, 2000), Vector2.zero, 50); nextDeadline.Step(.19f);
                                    Assert.That(arena.BombExplosionCount, Is.Zero); nextDeadline.Step(.02f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs.Contains(nextDeadline), Is.False);
                                    deadline.Step(4.78f); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); deadline.Step(.02f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); Assert.That(arena.LastBombExplosion.y, Is.EqualTo(1750).Within(.03f)); break;
                                case 4:
                                    var fuse = Spawn(new Vector2(0, 1000.3f), Vector2.down * 2, 50); fuse.Step(.1f);
                                    Assert.That(fuse.TimedLanded, Is.True); float remaining = fuse.TimedFuseRemaining; data.fuseSeconds = .2f;
                                    Assert.That(fuse.TimedFuseRemaining, Is.EqualTo(remaining)); fuse.Step(.21f); Assert.That(arena.BombExplosionCount, Is.Zero);
                                    var nextFuse = Spawn(new Vector2(8, 1000.3f), Vector2.down * 2, 50); nextFuse.Step(.1f);
                                    Assert.That(nextFuse.TimedLanded, Is.True); Assert.That(nextFuse.TimedFuseRemaining, Is.LessThan(.2f)); nextFuse.Step(.21f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs.Contains(fuse), Is.True);
                                    fuse.Step(1.4f); Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); break;
                                case 5:
                                    var timed = Spawn(origin, Vector2.right * 4, 50); data.fuseSeconds = 0;
                                    CenterAt(first, origin + Vector2.right * 2); Physics2D.SyncTransforms(); timed.SweepTo(origin + Vector2.right * 3);
                                    Assert.That(first.health, Is.EqualTo(1000)); Assert.That(arena.BombExplosionCount, Is.Zero); Assert.That(arena.bombs.Contains(timed), Is.True);
                                    var nextDirect = Spawn(origin, Vector2.right * 4, 50); nextDirect.SweepTo(origin + Vector2.right * 3);
                                    Assert.That(first.health, Is.EqualTo(950)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 6:
                                    var direct = Spawn(origin, Vector2.right * 4); data.fuseSeconds = .2f;
                                    CenterAt(first, origin + Vector2.right * 2); Physics2D.SyncTransforms(); direct.SweepTo(origin + Vector2.right * 3);
                                    Assert.That(first.health, Is.EqualTo(982)); Assert.That(arena.BombExplosionCount, Is.EqualTo(1));
                                    var nextTimed = Spawn(origin, Vector2.right * 4); nextTimed.SweepTo(origin + Vector2.right * 3);
                                    Assert.That(first.health, Is.EqualTo(982)); Assert.That(arena.bombs.Contains(nextTimed), Is.True); Assert.That(nextTimed.TimedLanded, Is.False);
                                    nextTimed.Step(.2f); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); break;
                                case 7:
                                case 8:
                                    bool wasExploding = scenario == 8; data.explodeOnExpiry = wasExploding;
                                    var expiry = Spawn(new Vector2(0, 2000), Vector2.zero); data.explodeOnExpiry = !wasExploding;
                                    driver.Step(5.01f); driver.Step(.02f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(wasExploding ? 1 : 0)); Assert.That(arena.bombs.Contains(expiry), Is.False);
                                    var nextExpiry = Spawn(new Vector2(0, 2000), Vector2.zero); driver.Step(5.01f); driver.Step(.02f);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs.Contains(nextExpiry), Is.False); break;
                                case 9:
                                    geometry.Add(Surface(origin)); var parent = Spawn(origin, Vector2.zero, 12); parent.fragmentMultiplier = 1.2f;
                                    data.fragmentAngles[0] = -90; data.fragmentAngles = new[] { 0f };
                                    data.fragmentDamage = 9; data.fragmentSpeed = 2; data.fragmentGravity = 0; data.fragmentRadius = .01f;
                                    data.fragmentBlastRadius = .05f; data.fragmentLifetime = .2f; data.fragmentSpawnOffset = .8f;
                                    Physics2D.SyncTransforms(); parent.SweepTo(origin);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs.Count, Is.EqualTo(4));
                                    var fragments = arena.bombs.ToArray(); float[] angles = { 30, 60, 120, 150 };
                                    for (int i = 0; i < fragments.Length; i++)
                                    {
                                        var direction = new Vector2(Mathf.Cos(angles[i] * Mathf.Deg2Rad), Mathf.Sin(angles[i] * Mathf.Deg2Rad));
                                        Assert.That(fragments[i].Position, Is.EqualTo(origin + direction * .2f));
                                        Assert.That(fragments[i].velocity, Is.EqualTo(direction * 6)); Assert.That(fragments[i].damage, Is.EqualTo(4.8f).Within(.00001f));
                                    }
                                    driver.Step(.1f); var child = fragments[0];
                                    Assert.That(child.velocity.y, Is.EqualTo(1).Within(.00001f));
                                    CenterAt(first, child.Position + Vector2.up * (first.size.y / 2 + .05f));
                                    CenterAt(second, child.Position + Vector2.right * (.6f + second.size.x / 2 - .01f)); Physics2D.SyncTransforms(); child.SweepTo(child.Position);
                                    Assert.That(first.health, Is.EqualTo(995.2f).Within(.0001f)); Assert.That(second.health, Is.EqualTo(995.2f).Within(.0001f));
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); Assert.That(arena.bombBursts[arena.bombBursts.Count - 1].radius, Is.EqualTo(.6f));
                                    first.Position = second.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); driver.Step(.48f);
                                    Assert.That(arena.bombs.Count, Is.EqualTo(3)); driver.Step(.03f);
                                    Assert.That(arena.bombs, Is.Empty); Assert.That(arena.BombExplosionCount, Is.EqualTo(5));
                                    var nextOrigin = origin + Vector2.right * 12; geometry.Add(Surface(nextOrigin)); Physics2D.SyncTransforms();
                                    var nextParent = Spawn(nextOrigin, Vector2.zero, 12); nextParent.fragmentMultiplier = 1.2f; nextParent.SweepTo(nextOrigin);
                                    Assert.That(arena.bombs.Count, Is.EqualTo(1)); var nextChild = arena.bombs[0];
                                    Assert.That(nextChild.Position, Is.EqualTo(nextOrigin + Vector2.right * .8f)); Assert.That(nextChild.velocity, Is.EqualTo(Vector2.right * 2));
                                    Assert.That(nextChild.damage, Is.EqualTo(10.8f).Within(.00001f));
                                    CenterAt(first, nextChild.Position + Vector2.up * (first.size.y / 2 + .05f)); Physics2D.SyncTransforms(); nextChild.SweepTo(nextChild.Position);
                                    Assert.That(arena.bombs.Contains(nextChild), Is.True); Assert.That(first.health, Is.EqualTo(995.2f).Within(.0001f));
                                    var end = nextOrigin + Vector2.right * 1.2f;
                                    first.Position = new Vector2(70, 1000); Physics2D.SyncTransforms(); driver.Step(.19f);
                                    Assert.That(arena.bombs.Contains(nextChild), Is.True); Assert.That(nextChild.velocity, Is.EqualTo(Vector2.right * 2));
                                    CenterAt(first, end + Vector2.up * (first.size.y / 2 + .02f)); CenterAt(second, end + Vector2.right * (second.size.x / 2 + .07f)); Physics2D.SyncTransforms();
                                    nextChild.Step(.02f); Assert.That(arena.bombs, Is.Empty); Assert.That(arena.BombExplosionCount, Is.EqualTo(7));
                                    Assert.That(first.health, Is.EqualTo(984.4f).Within(.0002f)); Assert.That(second.health, Is.EqualTo(995.2f).Within(.0001f));
                                    Assert.That(arena.bombBursts[arena.bombBursts.Count - 1].radius, Is.EqualTo(.05f)); break;
                                case 10:
                                    geometry.Add(Surface(origin)); var noFragments = Spawn(origin, Vector2.zero);
                                    var approved = arena.catalog.forms[Form(arena, "launcher")].specials[3].bomb;
                                    data.fragmentAngles = new[] { 0f, 90f }; data.fragmentRadius = approved.fragmentRadius; data.fragmentGravity = approved.fragmentGravity;
                                    data.fragmentBlastRadius = approved.fragmentBlastRadius; data.fragmentLifetime = approved.fragmentLifetime;
                                    data.fragmentDamage = approved.fragmentDamage; data.fragmentSpeed = approved.fragmentSpeed; data.fragmentSpawnOffset = approved.fragmentSpawnOffset;
                                    Physics2D.SyncTransforms(); noFragments.SweepTo(origin);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.bombs, Is.Empty);
                                    var addedOrigin = origin + Vector2.right * 12; geometry.Add(Surface(addedOrigin)); Physics2D.SyncTransforms();
                                    var added = Spawn(addedOrigin, Vector2.zero); added.SweepTo(addedOrigin);
                                    Assert.That(arena.BombExplosionCount, Is.EqualTo(2)); Assert.That(arena.bombs.Count, Is.EqualTo(2)); break;
                                case 11:
                                    var timedGeometry = Spawn(new Vector2(0, 1000.3f), Vector2.down * 2, 50); data.radius = .01f; data.blastRadius = .1f;
                                    timedGeometry.Step(.1f); Assert.That(timedGeometry.TimedLanded, Is.True);
                                    Assert.That(timedGeometry.Position.y - .2f, Is.EqualTo(floor.GetComponent<BoxCollider2D>().bounds.max.y).Within(.01f));
                                    CenterAt(first, timedGeometry.Position); CenterAt(second, timedGeometry.Position + Vector2.right * 2.8f); Physics2D.SyncTransforms(); timedGeometry.Step(2);
                                    Assert.That(first.health, Is.EqualTo(950)); Assert.That(second.health, Is.EqualTo(950));
                                    Assert.That(arena.bombBursts[0].radius, Is.EqualTo(3));
                                    var nextGeometry = Spawn(new Vector2(8, 1000.3f), Vector2.down * 2, 50); nextGeometry.Step(.1f);
                                    Assert.That(nextGeometry.TimedLanded, Is.True);
                                    Assert.That(nextGeometry.Position.y - data.radius, Is.EqualTo(floor.GetComponent<BoxCollider2D>().bounds.max.y).Within(.01f));
                                    CenterAt(first, nextGeometry.Position); CenterAt(second, nextGeometry.Position + Vector2.right * 2.8f); Physics2D.SyncTransforms(); nextGeometry.Step(2);
                                    Assert.That(first.health, Is.EqualTo(900)); Assert.That(second.health, Is.EqualTo(950));
                                    Assert.That(arena.bombBursts[1].radius, Is.EqualTo(.1f)); break;
                                case 12:
                                    var timedFlight = Spawn(origin, new Vector2(3, 4), 50); data.gravity = 0; timedFlight.Step(.2f);
                                    Assert.That(timedFlight.Position.x - origin.x, Is.EqualTo(.6f).Within(.0001f));
                                    Assert.That(timedFlight.Position.y - origin.y, Is.EqualTo(.4f).Within(.001f));
                                    Assert.That(timedFlight.velocity.y, Is.EqualTo(0).Within(.000001f)); Assert.That(timedFlight.TimedLanded, Is.False);
                                    var nextTimedFlight = Spawn(origin + Vector2.right * 8, new Vector2(3, 4), 50); nextTimedFlight.Step(.2f);
                                    Assert.That(nextTimedFlight.Position.y - origin.y, Is.EqualTo(.8f).Within(.001f)); Assert.That(nextTimedFlight.velocity.y, Is.EqualTo(4));
                                    Assert.That(arena.BombExplosionCount, Is.Zero); break;
                            }
                            Assert.That(arena.SimulationFaulted, Is.False);
                        }
                        finally { JsonUtility.FromJsonOverwrite(original, data); foreach (var item in geometry) if (item) Object.DestroyImmediate(item); }
                    }
                }
            }
            finally { arena.showBoxes = oldBoxes; Object.Destroy(floor); }
        }
    }
}
