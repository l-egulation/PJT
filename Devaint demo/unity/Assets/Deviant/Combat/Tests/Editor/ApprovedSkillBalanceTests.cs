using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ApprovedSkillBalanceTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        float varianceMin, varianceMax;
        TestEnemy Target => arena.enemies[0];
        CombatPlayer Player => arena.Player;

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("SkillBalanceArena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            arena = Object.FindAnyObjectByType<CombatArena>();
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("SkillBalanceCamera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(0, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("SkillBalanceTarget", 1, new Vector2(1, 1000), new Vector2(1, 2), new Vector2(0, 1), 10000, true, false, 0) };
            content.PrepareEnvironment = _ => {
                var floor = new GameObject("SkillBalanceFloor"); floor.transform.position = new Vector2(0, 999.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            };
            Assert.That(arena.Initialize(content), Is.True);
            driver = arena.AcquireManualSimulation(); driver.Reset();
            Player.SetWeaponSlotReady(0, true); Player.SetWeaponSlotReady(1, true);
            Player.SyncFacing(1); Target.aggressive = false;
            varianceMin = CombatDamageVariance.Settings.minimum; varianceMax = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            Physics2D.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose(); driver = null;
            CombatDamageVariance.Settings.minimum = varianceMin; CombatDamageVariance.Settings.maximum = varianceMax;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        CombatCast Start(string form, int special)
        {
            Assert.That(Player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special, out var error), Is.True, error);
            Player.selected = 0;
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Weapon1Special));
            Assert.That(arena.SimulationFaulted, Is.False);
            Assert.That(arena.casts, Is.Not.Empty);
            return arena.casts.Last();
        }
        void Until(float time, bool follow = false)
        {
            while (arena.Now < time - .000001f)
            {
                if (follow) { Target.Position = Player.Position + Vector2.right; Target.velocity = Vector2.zero; Physics2D.SyncTransforms(); }
                driver.Step(Mathf.Min(.01f, time - arena.Now));
            }
            Assert.That(arena.SimulationFaulted, Is.False);
        }
        void MoveTarget(float x)
        { Target.Position = new Vector2(x, 1000); Target.velocity = Vector2.zero; Physics2D.SyncTransforms(); }

        [UnityTest] public IEnumerator SmokeDelaysFirstTickAndDealsEightIndependentTicksAfterCancellationAndMovement()
        {
            Assert.That(Player.TryReconfigure(1, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var switchError), Is.True, switchError);
            Player.stealthUntil = arena.Now + 3;
            var cast = Start("dagger", 1); float start = cast.StartedAt;
            Assert.That(Player.Hidden, Is.True, "Installing smoke does not consume existing stealth.");
            Assert.That(cast.AmbushBonus, Is.Zero, "Area damage never captures a direct-hit ambush.");
            Until(start + .3f); cast.Cancel();
            Player.selected = 1;
            Player.Position = new Vector2(15, 1000); Player.velocity = Vector2.zero;
            Until(start + .64f); Assert.That(Target.health, Is.EqualTo(10000).Within(.001));
            Until(start + .66f); Assert.That(Target.health, Is.EqualTo(9994).Within(.001));
            Until(start + 4.18f); Assert.That(Target.health, Is.EqualTo(9952).Within(.001));
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(8));
            Until(start + 5); Assert.That(Target.ReceivedHitCount, Is.EqualTo(8));
            yield return null;
        }
        [UnityTest] public IEnumerator SmokeChecksMembershipEachTickWithoutCatchingUpMissedTicks()
        {
            var cast = Start("dagger", 1); float start = cast.StartedAt;
            Until(start + .66f); Assert.That(Target.ReceivedHitCount, Is.EqualTo(1));
            MoveTarget(10); Until(start + 1.66f); Assert.That(Target.ReceivedHitCount, Is.EqualTo(1));
            MoveTarget(1); Until(start + 4.18f);
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(6));
            Assert.That(Target.health, Is.EqualTo(9964).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator SmokeSnapshotsSpecialDamageAndResetRemovesRemainingTicks()
        {
            Player.weapons[0].TestSpecialDamageScale = 2;
            var cast = Start("dagger", 1); float start = cast.StartedAt;
            Until(start + .3f); Player.weapons[0].TestSpecialDamageScale = 1;
            Until(start + .66f); Assert.That(Target.health, Is.EqualTo(9988).Within(.001));
            driver.Reset(); float health = Target.health;
            Until(arena.Now + 5);
            Assert.That(Target.health, Is.EqualTo(health).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator SmokeRespectsSolidOcclusion()
        {
            var wall = new GameObject("SmokeOcclusionWall"); wall.transform.position = new Vector2(.6f, 1001.5f);
            wall.AddComponent<ArenaSurface>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.15f, 3);
            MoveTarget(1.6f);
            var cast = Start("dagger", 1); Until(cast.StartedAt + 4.18f);
            Assert.That(Target.ReceivedHitCount, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator ReplacingSmokeRemovesTheOldTickSchedule()
        {
            var original = Start("dagger", 1);
            Until(original.StartedAt + .3f); original.Cancel();
            Player.Weapon.Cooldown.Reset(1, Player.Special.cooldown);
            var replacement = CombatCast.Create(arena, Player, Player.Special, Player.Weapon, false);
            Player.currentCast = replacement;
            Until(replacement.StartedAt + .51f);
            Assert.That(Target.ReceivedHitCount, Is.Zero, "The old smoke's first scheduled hit must be removed.");
            Until(replacement.StartedAt + 4.18f);
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(8));
            Assert.That(Target.health, Is.EqualTo(9952).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator WhirlwindDoublesPerHitAndHitCountInsideItsOriginalMovementWindow()
        {
            var cast = Start("greatsword", 2); float start = cast.StartedAt;
            Assert.That(cast.Definition.End, Is.EqualTo(2.5f));
            Assert.That(cast.Definition.Hits.Count, Is.EqualTo(8));
            Until(start + 2.52f, true);
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(8));
            Assert.That(Target.health, Is.EqualTo(9808).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator WhirlwindCancellationRemovesFutureDirectHits()
        {
            var cast = Start("greatsword", 2); float start = cast.StartedAt;
            Until(start + .47f, true); Assert.That(Target.ReceivedHitCount, Is.EqualTo(2));
            cast.Cancel(); Until(start + 3, true);
            Assert.That(Target.ReceivedHitCount, Is.EqualTo(2));
            Assert.That(Target.health, Is.EqualTo(9952).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator SourceSpecificAmbushAndLatestSwordOverridesLoadThroughTheGameCatalog()
        {
            var ambush = arena.StealthAmbush;
            // 2026-11-10: 기습 추가 피해 삭제(숨죽이기·연막 모두 0), 숨죽이기 해제 후 4초 기본공격 공속 +50%.
            Assert.That(ambush.BonusFor(true, false), Is.Zero);
            Assert.That(ambush.BonusFor(false, true), Is.Zero);
            Assert.That(ambush.BonusFor(true, true), Is.Zero);
            Assert.That(ambush.BonusFor(false, false), Is.Zero);
            Assert.That(ambush.ExitBasicSpeedBonus, Is.EqualTo(.5f));
            Assert.That(ambush.ExitSeconds, Is.EqualTo(4));
            var sword = arena.catalog.forms.Single(f => f.id == "base_sword");
            foreach (var skill in sword.specials.Where(s => s.id == "base_sword_range" || s.id == "base_sword_movement"))
            { Assert.That(skill.hits.Single().damage, Is.EqualTo(50)); Assert.That(skill.cooldown, Is.EqualTo(6)); }
            Assert.That(sword.specials.Single(s => s.id == "base_sword_follow_up").hits.Select(h => h.damage), Is.EqualTo(new float[] {35,35}));
            yield return null;
        }
        [UnityTest] public IEnumerator SwordWaveDashAndEchoDealTheirApprovedDamageThroughActualExecution()
        {
            foreach (int special in new[] {1,2,3})
            {
                driver.Reset();
                Player.SetWeaponSlotReady(0, true); Player.SetWeaponSlotReady(1, true);
                Player.Position = new Vector2(0, 1000); Player.velocity = Vector2.zero; Player.SyncFacing(1);
                MoveTarget(1);
                var cast = Start("base_sword", special);
                Until(cast.StartedAt + 1);
                Assert.That(Target.health, Is.EqualTo(special == 3 ? 9930 : 9950).Within(.001), cast.Definition.Id);
                Assert.That(Target.ReceivedHitCount, Is.EqualTo(special == 3 ? 2 : 1), cast.Definition.Id);
            }
            yield return null;
        }
    }
}
