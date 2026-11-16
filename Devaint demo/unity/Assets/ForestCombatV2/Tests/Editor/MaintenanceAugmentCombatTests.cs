using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // 정비소 증강 in combat (docs/design/progression/maintenance-augments.md): the generated catalog, the purchase in
    // the growth, additive damage/incoming/healing/max-health, counted extras, kills, reflection and the bookmark.
    public sealed class MaintenanceAugmentCombatTests
    {
        static float oldMin, oldMax; static bool varianceChanged;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (varianceChanged) { CombatDamageVariance.Settings.minimum = oldMin; CombatDamageVariance.Settings.maximum = oldMax; varianceChanged = false; }
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static CombatArena Create()
        {
            oldMin = CombatDamageVariance.Settings.minimum; oldMax = CombatDamageVariance.Settings.maximum; varianceChanged = true;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            var a = new GameObject("Augment arena").AddComponent<CombatArena>(); a.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Camera").AddComponent<Camera>(); content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 100);
            content.Enemies = new[] { new CombatEnemySpawn("Target", 0, new Vector2(1, 100), new Vector2(1, 2), Vector2.up, 100000, false, false, 1) };
            Assert.IsTrue(a.Initialize(content)); a.Player.UseTestInput = true;
            a.MvpGrowth = new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),
                new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
            Assert.IsNotNull(a.Augments, "the arena attaches the generated augment catalog to the run growth");
            return a;
        }

        static readonly MethodInfo BeginAttack = typeof(CombatArena).GetMethod("BeginAugmentAttack", BindingFlags.Instance | BindingFlags.NonPublic);

        // One player attack: a new execution started like a cast, then one hit on the target. Returns the health it removed.
        static float Hit(CombatArena a, bool basic = true, float damage = 10, int slot = 0, TestEnemy target = null, string form = "base_sword")
        {
            target = target ?? a.enemies[0];
            var w = a.Player.weapons[slot]; a.TryBindActor(a.Player, out var source); var execution = Guid.NewGuid();
            var stamp = new CombatAttackStamp(w.InstanceId, form, "augment_test", "test", basic, -1, w.InkPalette.Read());
            BeginAttack.Invoke(a, new object[] { stamp, execution });
            var origin = new CombatHitOrigin(source, default, execution, 0, "augment_test", Guid.Empty, CombatHitKind.Melee, stamp: stamp, sourceFacing: 1);
            float before = target.health;
            Assert.IsTrue(target.Receive(damage, a.Player, 1, basic, null, origin: origin));
            return before - target.health;
        }

        static void Tick(CombatArena.ManualSimulation sim, float seconds) { for (int i = 0; i < (int)Math.Ceiling(seconds / .01f); i++) sim.Step(.01f); }

        [Test]
        public void GeneratedCatalogHoldsTheThirtyImplementedCards()
        {
            var catalog = CombatArena.LoadMaintenanceAugmentCatalog();
            Assert.AreEqual(30, catalog.augments.Length);
            foreach (var blocked in new[] { "S07", "S08", "W01", "W04", "W05", "W07", "W09", "X01", "X11" })
                Assert.IsNull(catalog.Find(blocked), blocked + " waits for blueprint durability");
            Assert.AreEqual(10, catalog.augments.Count(c => c.Stable));
        }

        [UnityTest]
        public IEnumerator DamageTermsOfOneStatAddBeforeMultiplying()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create();
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .02f);
                float basic = Hit(a, true), skill = Hit(a, false);
                Assert.Greater(basic, 0); Assert.Greater(skill, 0);
                a.MvpGrowth.GrantAugment("S01"); a.MvpGrowth.GrantAugment("C01");
                Assert.That(Hit(a, true), Is.EqualTo(basic * 1.55f).Within(.01f), "+20% and +35% add to 1.55");
                a.MvpGrowth.GrantAugment("C08");
                Assert.That(Hit(a, true), Is.EqualTo(basic * 2.05f).Within(.01f), "basic +50% joins the same sum");
                Assert.That(Hit(a, false), Is.EqualTo(skill * 1.05f).Within(.01f), "skill -50% joins the same sum");
                Assert.That(Hit(a, true, form: CombatArena.UnarmedFormId), Is.EqualTo(basic * 2.05f).Within(.01f), "bare hands still count as a basic attack");
                Assert.IsFalse(a.SimulationFaulted);
            }
        }

        [UnityTest]
        public IEnumerator AlternatingWeaponsStrengthenTheFirstAttack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create();
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .02f);
                float base0 = Hit(a, true, slot: 0);
                a.MvpGrowth.GrantAugment("W08");
                Assert.That(Hit(a, true, slot: 0), Is.EqualTo(base0).Within(.01f), "the first counted attack has no previous weapon");
                Assert.That(Hit(a, true, slot: 0), Is.EqualTo(base0 * .8f).Within(.01f), "the same weapon again: -20%");
                Assert.That(Hit(a, true, slot: 1), Is.EqualTo(base0 * 1.45f).Within(.01f), "the other weapon: +45%");
                Assert.That(Hit(a, true, slot: 0), Is.EqualTo(base0 * 1.45f).Within(.01f), "and back again: +45%");
            }
        }

        [UnityTest]
        public IEnumerator FifthWeaponAttackIsTheHeavyOne()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create();
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .02f);
                float base0 = Hit(a);
                a.MvpGrowth.GrantAugment("C10");
                for (int i = 1; i <= 4; i++) Assert.That(Hit(a), Is.EqualTo(base0 * .8f).Within(.01f), "attack " + i);
                Assert.That(Hit(a), Is.EqualTo(base0 * 3.5f).Within(.01f), "the fifth: +250%");
            }
        }

        [UnityTest]
        public IEnumerator EveryThirdWeaponAttackRepeatsItsDamage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create();
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .02f);
                float base0 = Hit(a);
                a.MvpGrowth.GrantAugment("M04");
                Assert.That(Hit(a), Is.EqualTo(base0).Within(.01f));
                Assert.That(Hit(a), Is.EqualTo(base0).Within(.01f));
                Assert.That(Hit(a), Is.EqualTo(base0 * 2).Within(.01f), "the third attack deals its damage once more");
                Assert.That(Hit(a), Is.EqualTo(base0).Within(.01f), "the extra damage is not counted");
            }
        }

        // The scales are read directly: a real player hit or heal plays its sound, whose deployed payload a
        // checkout without restored resources does not have.
        [UnityTest]
        public IEnumerator IncomingDamageAndHealingUseTheAugmentSums()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create(); var enemy = a.enemies[0];
            var incoming = typeof(CombatArena).GetMethod("AugmentIncomingScale", BindingFlags.Instance | BindingFlags.NonPublic);
            float Scale(CombatActor source, bool projectile) => (float)incoming.Invoke(a, new object[] { source, default(CombatHitOrigin), projectile });
            Assert.AreEqual(1, Scale(enemy, false), 1e-5);
            a.MvpGrowth.GrantAugment("C01"); a.MvpGrowth.GrantAugment("M02");
            Assert.AreEqual(.8f, Scale(enemy, false), 1e-5, "+25% and -45% add to 0.8");
            a.MvpGrowth.GrantAugment("C11");
            a.Player.facing = enemy.Position.x > a.Player.Position.x ? 1 : -1;
            Assert.AreEqual(.8f, Scale(enemy, false), 1e-5, "a hit from the front adds nothing");
            a.Player.facing = -a.Player.facing;
            Assert.AreEqual(1.2f, Scale(enemy, false), 1e-5, "a hit from behind adds +40%");
            a.MvpGrowth.GrantAugment("X08");
            Assert.AreEqual(1.5f, Scale(enemy, true), 1e-5, "an enemy shot adds +30%");
            a.MvpGrowth.GrantAugment("S09"); a.MvpGrowth.GrantAugment("C13");
            Assert.AreEqual(.8f, a.AugmentHealingScale, 1e-5, "+30% and -50% heal 80%");
        }

        [UnityTest]
        public IEnumerator MaximumHealthRisesWithHealthAndFallsByCutting()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create(); var player = a.Player;
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .02f);
                float baseMax = player.maxHealth; player.health = baseMax - 30;
                a.MvpGrowth.GrantAugment("S02"); Tick(sim, .02f);
                Assert.That(player.maxHealth, Is.EqualTo(Mathf.Round(baseMax * 1.2f)).Within(.01f));
                Assert.That(player.health, Is.EqualTo(baseMax - 30 + Mathf.Round(baseMax * 1.2f) - baseMax).Within(.01f), "the rise is added to health");
                a.MvpGrowth.GrantAugment("C14"); Tick(sim, .02f);
                Assert.That(player.maxHealth, Is.EqualTo(Mathf.Round(baseMax * 1.2f * .3f)).Within(.01f), "growing pains starts at 30% of the maximum before it");
                Assert.That(player.health, Is.EqualTo(player.maxHealth).Within(.01f), "a fall cuts health to the new maximum");
                var enemy = a.enemies[0];
                for (int i = 0; i < 10; i++) { enemy.health = 1; Hit(a, damage: 5); Assert.IsTrue(enemy.Dead); enemy.ResetEnemy(); }
                Tick(sim, .02f);
                Assert.AreEqual(10, a.Augments.GrowthKills);
                Assert.That(player.maxHealth, Is.EqualTo(Mathf.Round(baseMax * 1.2f * .4f)).Within(.01f), "ten kills add 10% of that basis");
            }
        }

        [UnityTest]
        public IEnumerator ReturnedShotFliesBackWithDoubleDamage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create(); var enemy = a.enemies[0];
            a.FireEnemyProjectile(enemy, a.Player.Center + Vector2.right * 2, Vector2.left * 5, 7);
            var shot = a.projectiles.Last();
            typeof(ArenaProjectile).GetMethod("ReflectByAugment", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shot, new object[] { a.Player, default(CombatHitOrigin), 2f });
            Assert.IsTrue(shot.Execution.Reflected);
            Assert.AreEqual(14, shot.Execution.Damage, .001);
            Assert.AreSame(a.Player, shot.source);
            Assert.Greater(shot.velocity.x, 0);
            typeof(ArenaProjectile).GetMethod("ReflectByAugment", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shot, new object[] { a.Player, default(CombatHitOrigin), 2f });
            Assert.AreEqual(14, shot.Execution.Damage, .001, "a returned shot is not returned again");
        }

        [UnityTest]
        public IEnumerator BookmarkReturnsThePlayerThreeSecondsAfterASkill()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var a = Create();
            using (var sim = a.AcquireManualSimulation())
            {
                Tick(sim, .5f);
                a.MvpGrowth.GrantAugment("X09");
                Vector2 mark = a.Player.Position;
                Hit(a, false);
                Assert.IsTrue(a.AugmentBookmarkActive);
                a.Player.Position = mark + Vector2.left * 3; Tick(sim, 1);
                Hit(a, false);
                Assert.AreEqual(mark.x, a.AugmentBookmarkPosition.x, .001, "a second skill keeps the first bookmark");
                Tick(sim, 2.1f);
                Assert.IsFalse(a.AugmentBookmarkActive);
                Assert.AreEqual(mark.x, a.Player.Position.x, .1f);
            }
        }
    }
}
