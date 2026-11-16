using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The real chapter run owns the economy/growth; attacks use the public simulation input boundary.
    public sealed class Chapter1GrowthRuntimeTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void Prepare()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
        }
        static IEnumerator WaitForWorld()
        {
            for (int i = 0; i < 600; i++)
            {
                var w = Object.FindAnyObjectByType<ForestCombatWorld>();
                if (w && w.Ready && w.GetComponent<Chapter1RunBinding>()) yield break;
                yield return null;
            }
            Assert.Fail("Chapter1Procedural did not initialise");
        }
        sealed class Fixture
        {
            public readonly ForestCombatWorld World;
            public CombatArena Arena => World.arena;
            public CombatPlayer Player => Arena.Player;
            public WeaponGrowth Growth => World.GetComponent<Chapter1RunBinding>().Run.Growth;
            public readonly TestEnemy Target;
            public readonly Vector2 Feet;
            public Fixture()
            {
                World = Object.FindAnyObjectByType<ForestCombatWorld>();
                var offers = World.GetComponent<Chapter1StartingBlueprints>();
                for (int i = 0; i < 2; i++) { Player.Position = offers.OfferPosition(i); Assert.IsTrue(offers.TryChoose(i)); }
                Player.CancelAction(); Player.selected = 0;
                foreach (var e in Arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
                var room = World.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
                Feet = new Vector2(room.x + room.w / 2f, -(room.y + room.entryY));
                Player.Position = Feet; Player.velocity = Vector2.zero; Player.maxHealth = Player.health = 10000;
                Target = Arena.SpawnEnemy(0, Feet + Vector2.right * 1.1f); Target.maxHealth = Target.health = 10000; Target.aggressive = false;
                Growth.Economy.Earn(100000);
                Physics2D.SyncTransforms();
            }
            public void Equip(int weapon, params string[] ids)
            {
                var colors = ids.Select(id => Growth.Data.parts.Single(p => p.id == id).color).ToList();
                // Five-slot palettes permit arbitrary colours, including support parts in combination tests.
                while (colors.Count < 5) colors.Add(colors[0]);
                var palette = Growth.GrantPalette(colors.ToArray()); Growth.Bind(palette.Id, Player.weapons[weapon].InstanceId);
                for (int i = 0; i < ids.Length; i++) Growth.Insert(Player.weapons[weapon].InstanceId, i, Growth.GrantInk(ids[i]).Id);
            }
            public void Hit(TestEnemy target = null, int weapon = 0, float damage = 1)
            {
                target = target ? target : Target;
                Arena.TryBindActor(Player, out var source); var w = Player.weapons[weapon];
                var origin = new CombatHitOrigin(source, default, Guid.NewGuid(), 0, "growth_test", Guid.Empty, CombatHitKind.Melee,
                    stamp: new CombatAttackStamp(w.InstanceId, "base_sword", "growth_test", "test", true, -1, w.InkPalette.Read()), sourceFacing: 1);
                Assert.IsTrue(target.Receive(damage, Player, 1, true, null, origin: origin));
            }
            public void Switch() => typeof(CombatPlayer).GetMethod("SwitchNow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Player, new object[] { default(CombatCommandKey) });
            public object Start(CombatArena.ManualSimulation sim, int weapon = 0)
            {
                Player.CancelAction(); Player.selected = weapon; Player.Position = Feet; Player.velocity = Vector2.zero;
                Physics2D.SyncTransforms(); Player.grounded = Player.ProbeGround();
                sim.Step(.01f, new CombatInput(pressed: CombatButtons.Basic)); Assert.IsNotNull(Player.currentCast);
                return typeof(CombatCast).GetField("InkAction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.currentCast);
            }
            public List<CombatAcceptedHit> Strike(CombatArena.ManualSimulation sim, float healthFraction = -1)
            {
                Player.CancelAction(); Player.selected = 0; Player.Position = Feet; Player.velocity = Vector2.zero; Player.SyncFacing(1);
                Target.ResetEnemy(); if (healthFraction >= 0) Target.health = Target.maxHealth * healthFraction; Target.Position = Feet + Vector2.right * 1.1f; Target.aggressive = false;
                Physics2D.SyncTransforms(); Player.grounded = Player.ProbeGround();
                Arena.TryBindActor(Target, out var target); Arena.TryGetCursor(out var cursor);
                var random = UnityEngine.Random.state;
                try
                {
                    UnityEngine.Random.InitState(732);
                    sim.Step(1f / 120, new CombatInput(pressed: CombatButtons.Basic));
                    for (int i = 0; i < 180; i++) sim.Step(1f / 120);
                }
                finally { UnityEngine.Random.state = random; }
                var hits = Read(Arena, ref cursor).Where(h => h.Target.Equals(target)).ToList();
                Assert.That(hits.Count, Is.GreaterThan(0), "actual basic attack must land");
                return hits;
            }
        }
        static void Tick(CombatArena.ManualSimulation sim, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .01f); i++) sim.Step(.01f); }
        static List<CombatAcceptedHit> Read(CombatArena arena, ref CombatOutcomeCursor cursor)
        {
            var result = new List<CombatAcceptedHit>(); var buffer = new CombatAcceptedHit[128];
            while (true)
            {
                Assert.IsTrue(arena.HitEvents.TryRead(cursor, buffer, out var page)); Assert.Zero(page.DroppedBefore); cursor = page.Next;
                result.AddRange(buffer.Take(page.Count)); if (page.Count < buffer.Length) return result;
            }
        }
        static float Original(List<CombatAcceptedHit> hits) => hits.First(h => h.Origin.Kind != CombatHitKind.InkPart).RequestedDamage;
        static float Value(object action, string name) => (float)action.GetType().GetField(name).GetValue(action);

        [UnityTest] public IEnumerator MaintenanceAttackLevelsIncreaseRealBasicAndInkDamage()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            f.Equip(0, "ink3_red_01");
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                var zero = f.Strike(sim); float basic = Original(zero), extra = zero.First(h => h.Origin.Kind == CombatHitKind.InkPart).RequestedDamage;
                var shop = f.World.GetComponent<Chapter1MaintenanceShop>(); Assert.IsTrue(shop.TryOpen()); Assert.IsTrue(shop.TryUpgradeMvpAttack(out var error), error); shop.Close();
                var one = f.Strike(sim);
                Assert.That(Original(one), Is.EqualTo(basic * 1.05f).Within(.001));
                Assert.That(one.First(h => h.Origin.Kind == CombatHitKind.InkPart).RequestedDamage, Is.EqualTo(extra * 1.05f).Within(.001));
                for (int i = 1; i < 10; i++) f.Growth.UpgradeAttack();
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(basic * 1.5f).Within(.001));
                TestContext.WriteLine($"Chapter1 basic +0={basic}, +1={Original(one)}, +10={basic * 1.5f}; ink +0={extra}");
            }
        }
        [UnityTest] public IEnumerator SlotLevelsIncreaseOnlyTheMountedPartsRealExtraHit()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture(); f.Equip(0, "ink3_red_01");
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                float basic = 0, extra = 0;
                for (int level = 0; level <= 5; level++)
                {
                    if (level > 0) f.Growth.Upgrade(f.Player.weapons[0].InstanceId, 0);
                    var hits = f.Strike(sim); float hit = Original(hits), ink = hits.First(h => h.Origin.Kind == CombatHitKind.InkPart).RequestedDamage;
                    if (level == 0) { basic = hit; extra = ink; }
                    Assert.That(hit, Is.EqualTo(basic).Within(.001)); Assert.That(ink, Is.EqualTo(basic * f.Growth.Data.FindPart("ink3_red_01").levels[level].ratio).Within(.001));
                    TestContext.WriteLine($"slot +{level}: basic={hit}, extra={ink}");
                }
            }
        }
        [UnityTest] public IEnumerator UntestedSwitchKillAndDefencePartsChangeCombatNumbers()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                float plain = Original(f.Strike(sim));
                f.Equip(0, "ink3_red_06");
                for (int i = 0; i < 3; i++) { f.Target.ResetEnemy(); f.Target.health = .1f; f.Hit(); Tick(sim, .01f); }
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(plain * 1.009f).Within(.001), "red06 three kills");
                f.Equip(0, "ink3_green_01", "ink3_green_06"); f.Player.shield = 10; f.Player.health = f.Player.maxHealth;
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(plain * 1.3f).Within(.001), "green01 shield + green06 high health");
                f.Player.shield = 0; f.Player.health = f.Player.maxHealth * .5f;
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(plain).Within(.001), "both conditions absent");
                f.Equip(0, "ink3_green_03");
                f.Target.ResetEnemy(); float hp = f.Target.health; f.Player.guardUntil = 0;
                Assert.IsTrue(f.Player.Receive(1, f.Target, -1, false, null)); Tick(sim, .01f);
                Assert.That(f.Target.health, Is.LessThan(hp), "green03 returns damage to the attacker");
                f.Equip(0, "ink3_yellow_06"); f.Target.ResetEnemy(); f.Target.health = .1f; f.Hit(); Tick(sim, .01f);
                Assert.That(f.Arena.ReadMvpBuff("moving_power"), Is.EqualTo(.1f).Within(.001));
                Assert.That(f.Arena.ReadMvpBuff("move"), Is.EqualTo(.15f).Within(.001));
                f.Equip(0, "ink3_green_04"); float wounded = f.Player.health;
                for (int i = 0; i < 4; i++) { f.Target.ResetEnemy(); f.Target.health = .1f; f.Hit(); Tick(sim, .01f); }
                Assert.That(f.Player.health, Is.EqualTo(wounded).Within(.001));
                f.Target.ResetEnemy(); f.Target.health = .1f; f.Hit(); Tick(sim, .01f);
                Assert.That(f.Player.health, Is.EqualTo(wounded + 1).Within(.001), "green04 fifth kill heals one");
            }
        }
        [UnityTest] public IEnumerator CooldownAndStunSupportPartsApplyTheirNumericEffects()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                f.Equip(0, "ink3_blue_04"); var a = f.Player.weapons[0]; var b = f.Player.weapons[1];
                a.Cooldown.Reset(1, 10); a.Cooldown.Spend(f.Arena.Now); float due = a.Cooldown.NextReady;
                f.Hit(); Tick(sim, .01f); Assert.That(a.Cooldown.NextReady, Is.EqualTo(due - .08f).Within(.001));
                f.Equip(0, "ink3_blue_02"); b.Cooldown.Reset(1, 10); b.Cooldown.Spend(f.Arena.Now); due = b.Cooldown.NextReady;
                f.Switch(); f.Player.CancelAction(); Assert.That(b.Cooldown.NextReady, Is.EqualTo(due - .8f).Within(.001));
                f.Player.selected = 0; f.Equip(0, "ink3_purple_03", "ink3_purple_04"); f.Start(sim); f.Player.CancelAction(); f.Target.ResetEnemy();
                f.Arena.TryGetCursor(out var cursor); f.Hit(); Tick(sim, .3f);
                Assert.That(Read(f.Arena, ref cursor).Count(h => h.Origin.Kind == CombatHitKind.InkPart && h.RequestedDamage > 0), Is.GreaterThanOrEqualTo(1), "purple04 damages a stunned target");
                f.Equip(1, "ink3_blue_05", "ink3_purple_06");
                f.Target.ResetEnemy(); f.Target.rootUntil = f.Target.stunUntil = f.Arena.Now + 2; f.Hit(); Tick(sim, .01f);
                a.Cooldown.Reset(1, 10); b.Cooldown.Reset(1, 10); a.Cooldown.Spend(f.Arena.Now); b.Cooldown.Spend(f.Arena.Now);
                float aDue = a.Cooldown.NextReady, bDue = b.Cooldown.NextReady;
                f.Arena.TryGetCursor(out cursor); f.Hit(weapon: 1); Tick(sim, .01f);
                Assert.That(a.Cooldown.NextReady, Is.EqualTo(aDue - .4f).Within(.001)); Assert.That(b.Cooldown.NextReady, Is.EqualTo(bDue - .4f).Within(.001));
                float reference = f.Arena.catalog.forms[b.FormIndex].basics.Average(v => v.damage);
                Assert.That(Read(f.Arena, ref cursor).Any(h => h.Origin.Kind == CombatHitKind.InkPart && Mathf.Abs(h.RequestedDamage - reference * .3f) < .001f), Is.True, "purple06 counts bind and stun once each");
            }
        }
        // 결정타: 맞히기 직전 체력이 30% 이하인 몬스터에게만 원타격 피해 +35%
        [UnityTest] public IEnumerator ExecuteRaisesPrimaryDamageOnlyAtLowHealth()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                float plain = Original(f.Strike(sim));
                f.Equip(0, "ink3_red_08");
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(plain).Within(.001), "healthy targets take normal damage");
                Assert.That(Original(f.Strike(sim, .25f)), Is.EqualTo(plain * 1.4f).Within(.001), "red08 below 30%");
            }
        }
        [UnityTest] public IEnumerator SwitchPowerBurstAndGeometryPartsAffectTheNextActualAttack()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                float plain = Original(f.Strike(sim));
                f.Equip(1, "ink3_red_03"); f.Player.selected = 1;
                f.Target.ResetEnemy(); f.Target.Position = f.Feet + Vector2.right * 1.1f; f.Player.Position = f.Feet; f.Player.SyncFacing(1); Physics2D.SyncTransforms();
                f.Arena.TryBindActor(f.Target, out var target); f.Arena.TryGetCursor(out var switchCursor); f.Switch(); Tick(sim, 1.5f);
                Assert.That(Original(Read(f.Arena, ref switchCursor).Where(h => h.Target.Equals(target)).ToList()), Is.EqualTo(plain * 1.35f).Within(.001), "red03 incoming automatic attack consumes next-attack power");
                Assert.That(Original(f.Strike(sim)), Is.EqualTo(plain).Within(.001), "the next-attack bonus is consumed once");
                f.Equip(1, "ink3_red_04"); f.Target.ResetEnemy(); f.Hit(); Tick(sim, .01f);
                f.Arena.TryGetCursor(out var cursor); f.Hit(weapon: 1); Tick(sim, .01f);
                float reference = f.Arena.catalog.forms[f.Player.weapons[1].FormIndex].basics.Average(v => v.damage);
                Assert.IsTrue(Read(f.Arena, ref cursor).Any(h => h.Origin.Kind == CombatHitKind.InkPart && Mathf.Abs(h.RequestedDamage - reference * .3f) < .001f), "red04 adds 30% reference damage at mark burst");
                f.Equip(0, "ink3_blue_01");
                var action = f.Start(sim); Assert.That(Value(action, "MvpAllGeometryScale"), Is.EqualTo(1.25f).Within(.001)); Assert.That(Value(action, "MvpRadiusScale"), Is.EqualTo(1).Within(.001)); Assert.That(Value(action, "RangeScale"), Is.EqualTo(1).Within(.001));
                f.Player.CancelAction(); f.Equip(0, "ink3_blue_06"); f.Target.ResetEnemy(); f.Target.health = .1f; f.Hit(); Tick(sim, .01f);
                Assert.That(Value(f.Start(sim), "MvpRadiusScale"), Is.EqualTo(1.3f).Within(.001), "blue06 kill radius applies to the next cast");
                f.Player.CancelAction(); Tick(sim, 4.1f); Assert.That(Value(f.Start(sim), "MvpRadiusScale"), Is.EqualTo(1).Within(.001), "radius expires");
            }
        }
        [UnityTest] public IEnumerator MovementDashAndSwitchSpeedPartsTriggerInTheRealRun()
        {
            Prepare(); yield return new EnterPlayMode(); yield return WaitForWorld(); var f = new Fixture();
            using (var sim = f.Arena.AcquireManualSimulation())
            {
                f.Equip(0, "ink3_yellow_02");
                Assert.That(Value(f.Start(sim), "MvpAttackSpeed"), Is.EqualTo(.1f).Within(.001));
                Assert.That(f.Arena.InkMovementScale(f.Player), Is.EqualTo(1.1f).Within(.001)); f.Player.CancelAction();
                f.Equip(0, "ink3_yellow_04"); f.Switch();
                Assert.That(f.Arena.ReadMvpBuff("attack_speed"), Is.EqualTo(.3f).Within(.001));
                Assert.That(f.Arena.InkMovementScale(f.Player), Is.EqualTo(1.3f).Within(.001)); f.Player.CancelAction();
                f.Equip(0, "ink3_yellow_01"); f.Start(sim); f.Player.CancelAction();
                for (int i = 0; i < 100 && !f.Arena.ReadMvpFields().Any(); i++) sim.Step(.01f, new CombatInput(axis: 1));
                Assert.That(f.Arena.ReadMvpFields().Count(), Is.GreaterThan(0), "yellow01 actual movement leaves a field");
                // 전기 발자국은 전기장마다 작은 번개 파티클 셋을 바닥에 깐다(전기 장판의 번개 장판 시트·이동 속도 시트를 쓰지 않는다)
                var inkFx = f.World.GetComponent<Chapter1InkPartFx>();
                Assert.IsTrue(Resources.Load<Texture2D>("InkPartFx/footstep-electric-micro-spark"), "the electric footstep spark sheet is deployed");
                typeof(Chapter1InkPartFx).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inkFx, null);
                int liveFieldCount = f.Arena.ReadMvpFields().Count();
                Assert.That(inkFx.VisibleFields, Is.EqualTo(liveFieldCount), "every live footstep field is drawn");
                var fxNames = inkFx.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.gameObject.name).ToArray();
                Assert.That(fxNames.Count(n => n == "InkFx footstep-electric-micro-spark"), Is.EqualTo(liveFieldCount * 3), "three sparks per footstep field");
                Assert.That(fxNames.Any(n => n == "InkFx electric-field_yellow"), Is.False, "the footstep no longer uses the electric floor sheet");
                var field = f.Arena.ReadMvpFields().First(); f.Target.ResetEnemy(); f.Target.Position = field.feet - (f.Target.Center - f.Target.Position);
                Physics2D.SyncTransforms(); float hp = f.Target.health; Tick(sim, .51f); Assert.That(f.Target.health, Is.LessThan(hp), "the emitted field deals damage");
                f.Player.ResetPlayer(); f.Equip(0, "ink3_yellow_03"); f.Start(sim); f.Player.CancelAction();
                f.Target.ResetEnemy(); f.Target.Position = f.Player.Position + Vector2.right * .2f; Physics2D.SyncTransforms();
                f.Arena.TryGetCursor(out var cursor); sim.Step(.01f, new CombatInput(pressed: CombatButtons.Dash)); Tick(sim, .12f);
                var dashHits = Read(f.Arena, ref cursor).Where(h => h.Origin.Kind == CombatHitKind.InkPart && h.Origin.ActionId == "mvp_dash").ToArray();
                Assert.That(dashHits.Length, Is.EqualTo(1), "yellow03 damages one target only once in the same dash");
                float reference = f.Arena.catalog.forms[f.Player.weapons[0].FormIndex].basics.Average(v => v.damage);
                Assert.That(dashHits[0].RequestedDamage, Is.EqualTo(reference*1.1f).Within(.001));
            }
        }
    }
}
