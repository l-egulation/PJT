using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class RangedProjectileDefinitionTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        CombatActor target;
        Vector2 origin = new Vector2(0, 1008);
        [UnitySetUp] public IEnumerator SetUp()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            arena = Object.FindAnyObjectByType<CombatArena>(); driver = arena.AcquireManualSimulation(); driver.Reset();
            arena.Player.Position = new Vector2(-20, 1000); arena.Player.velocity = Vector2.zero;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.ResetActor(new Vector2(70, 1000)); enemy.health = 1000; }
            target = arena.enemies[2]; CenterAt(target, origin + Vector2.right * 3);
            var duplicate = new GameObject("CapturedProjectileDuplicate"); duplicate.transform.SetParent(target.transform, false);
            var box = duplicate.AddComponent<BoxCollider2D>(); box.size = target.size; box.offset = target.hurtCollider.offset; box.isTrigger = true;
            duplicate.AddComponent<HurtRegion>().actor = target; Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose(); driver = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        static void CenterAt(CombatActor actor, Vector2 center) => actor.Position = center - new Vector2(actor.offset.x * actor.facing, actor.offset.y);
        RangedData Source(int special)
        {
            int form = System.Array.FindIndex(arena.catalog.forms, item => item.id == "shuriken");
            return JsonUtility.FromJson<RangedData>(JsonUtility.ToJson(arena.catalog.forms[form].specials[special].ranged));
        }
        PlayerProjectile Spawn(RangedData data, float damage = 5)
        {
            var shot = PlayerProjectile.Spawn(arena, arena.Player, origin, Vector2.right * 3, data, damage);
            shot.SweepTo(origin); return shot;
        }
        [UnityTest] public IEnumerator DisablingSourceIntervalCannotReplaceTheRunningPerTargetClockOrExpiry()
        {
            var data = Source(3); var first = Spawn(data); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
            data.hitInterval = 0; first.Step(.24f); first.SweepTo(first.Position); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
            first.Step(.01f); Assert.That(target.ReceivedHitCount, Is.EqualTo(2));
            var next = Spawn(data); Assert.That(target.ReceivedHitCount, Is.EqualTo(3));
            next.Step(.5f); next.SweepTo(next.Position); Assert.That(target.ReceivedHitCount, Is.EqualTo(3), "The new single-hit shot has a separate dedup ledger.");
            first.Step(1.75f); Assert.That(target.ReceivedHitCount, Is.EqualTo(9), "The running shot keeps .25s hits through 1.75s, never at its 2s expiry.");
            Assert.That(target.health, Is.EqualTo(955)); Assert.That(first.traveled, Is.EqualTo(6).Within(.0001f)); first.Step(.02f);
            Assert.That(target.ReceivedHitCount, Is.EqualTo(9)); Assert.That(arena.playerShots.Contains(first), Is.False);
            next.Step(2); Assert.That(target.ReceivedHitCount, Is.EqualTo(9)); Assert.That(arena.playerShots, Is.Empty); yield break;
        }
        [UnityTest] public IEnumerator EnablingSourceIntervalCannotReplaceTheRunningSingleHitLedger()
        {
            var data = Source(0); data.radius = 1.5f;
            var first = Spawn(data); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); data.hitInterval = .25f;
            first.Step(.5f); first.SweepTo(first.Position); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
            var next = Spawn(data); Assert.That(target.ReceivedHitCount, Is.EqualTo(2)); next.Step(.5f);
            Assert.That(target.ReceivedHitCount, Is.EqualTo(4), "Only the new projectile adopts recurring contacts.");
            first.SweepTo(origin); first.SweepTo(first.Position); Assert.That(target.ReceivedHitCount, Is.EqualTo(4)); Assert.That(target.health, Is.EqualTo(980)); yield break;
        }
        [UnityTest] public IEnumerator AddingSourceBlastCannotConvertTheExistingDirectHitIntoAnExplosion()
        {
            var data = Source(0); CenterAt(target, origin); var edge = arena.enemies[1]; CenterAt(edge, origin + Vector2.right * 2.8f); Physics2D.SyncTransforms();
            var first = PlayerProjectile.Spawn(arena, arena.Player, origin, Vector2.right * 18, data, 24); data.blastRadius = 3;
            first.SweepTo(origin); first.Detonate(); first.SweepTo(origin);
            Assert.That(target.health, Is.EqualTo(976)); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(edge.health, Is.EqualTo(1000));
            Assert.That(arena.bombBursts, Is.Empty); Assert.That(arena.playerShots.Contains(first), Is.True);
            var next = PlayerProjectile.Spawn(arena, arena.Player, origin, Vector2.right * 18, data, 24); next.SweepTo(origin); next.Detonate();
            Assert.That(target.health, Is.EqualTo(952)); Assert.That(target.ReceivedHitCount, Is.EqualTo(2)); Assert.That(edge.health, Is.EqualTo(976));
            Assert.That(edge.ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.bombBursts.Count, Is.EqualTo(1)); Assert.That(arena.bombBursts[0].radius, Is.EqualTo(3)); yield break;
        }
    }
}
