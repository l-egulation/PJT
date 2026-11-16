using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SkillExecutionDefinitionIntegrationTests
    {
        // Existing DTO-producing definition APIs supply the retained-runner
        // fixtures. Mutation cases keep this exact input before calling Create.
        internal static SkillData BasicSource(CombatArena arena, int index)
        {
            var p = arena.Player; var definition = arena.GetBasicMelee(p.Form.id, index); var b = definition.ToData(); float rate = p.Weapon.Speed;
            return new SkillData { id = definition.Id, name = p.Form.name + " " + (index + 1), activation = b.impact / rate, end = b.duration / rate,
                inputSpeed = p.Form.inputSpeed, vertical = "normal", facing = "windup_start", invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(),
                basicLungeDistance = p.Form.inputSpeed > 0 ? .65f : 0, basicLungeStart = Mathf.Max(0,b.impact-.04f)/rate, basicLungeDuration = .08f/rate,
                hits = new[] { new HitData { time = b.impact/rate, damage = b.damage, active = b.active/rate, box = b.box, anchor = "actor", targets = "all" } } };
        }
        internal static CombatCast ReplaceBasic(CombatArena arena, CombatCast compiled, SkillData source, bool timeline)
        {
            var p = arena.Player; float started = compiled.StartedAt; string version = compiled.DefinitionVersion; var frame = compiled.FrameTiming;
            var definition = arena.GetBasicMelee(p.Form.id, int.Parse(source.id.Substring(source.id.LastIndexOf('_') + 1)));
            var action = timeline ? new MeleeActionDefinition(definition, p.Weapon.Speed, source.basicLungeDistance, source.basicLungeStart, source.basicLungeDuration) : null;
            compiled.Cancel(); arena.casts.Remove(compiled); Object.Destroy(compiled.gameObject);
            var cast = CombatCast.Create(arena, p, source, p.Weapon, true, started, version, action, timeline ? frame : null); p.currentCast = cast; return cast;
        }

        [UnityTest] public IEnumerator GameDirectAndIndependentHitsKeepStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", (arena, driver, floor) => DirectAndIndependentHits(arena, driver, floor));
        [UnityTest] public IEnumerator LabDirectAndIndependentHitsKeepStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", (arena, driver, floor) => DirectAndIndependentHits(arena, driver, floor));
        [UnityTest] public IEnumerator GameMovementAndDefenseKeepStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", MovementAndDefense);
        [UnityTest] public IEnumerator LabMovementAndDefenseKeepStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", MovementAndDefense);
        [UnityTest] public IEnumerator GameEffectsKeepStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", (arena, driver, floor) => Effects(arena, driver, floor));
        [UnityTest] public IEnumerator LabEffectsKeepStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", (arena, driver, floor) => Effects(arena, driver, floor));
        [UnityTest] public IEnumerator GameProjectileLaunchesKeepStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", ProjectileLaunches);
        [UnityTest] public IEnumerator LabProjectileLaunchesKeepStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", ProjectileLaunches);
        [UnityTest] public IEnumerator GameMissileKeepsStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", Missile);
        [UnityTest] public IEnumerator LabMissileKeepsStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", Missile);
        [UnityTest] public IEnumerator GameSpreadAndFragmentsKeepStartInput() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", SpreadAndFragments);
        [UnityTest] public IEnumerator LabSpreadAndFragmentsKeepStartInput() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", SpreadAndFragments);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static SkillData Copy(SkillData source) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(source));
        static SkillData Source(CombatArena arena, string form, int special) => Copy(Array.Find(arena.catalog.forms, f => f.id == form).specials[special]);
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form, int special, float targetX = 2)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(targetX, 1000); arena.enemies[0].health = 1000; Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var cast = CombatCast.Create(arena, arena.Player, source, arena.Player.Weapon, false); arena.Player.currentCast = cast; return cast;
        }
        static void Ticks(CombatArena.ManualSimulation driver, int count) { for (int i = 0; i < count; i++) driver.Step(.02f); }
        static IEnumerator Exercise(string scene, Action<CombatArena, CombatArena.ManualSimulation> exercise) => Exercise(scene, (arena, driver, floor) => exercise(arena, driver));
        static IEnumerator Exercise(string scene, Action<CombatArena, CombatArena.ManualSimulation, ArenaSurface> exercise)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var floor = new GameObject("SkillSourceMutationFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); var support = floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    exercise(arena, driver, support);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { Object.Destroy(floor); }
        }

        static void DirectAndIndependentHits(CombatArena arena, CombatArena.ManualSimulation driver, ArenaSurface floor)
        {
            Setup(arena, driver, "whip", 0); var source = Source(arena, "whip", 0); var cast = Start(arena, source);
            source.hits[0].damage = 1; source.hits[0].root = 0; source.hits[0].box.width = .001f; source.hits[0].box.x = -50; source.hits[0].targets = "all"; source.end = 99;
            Ticks(driver, 32); Assert.That(arena.enemies[0].health, Is.EqualTo(985)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(arena.enemies[0].rootUntil, Is.GreaterThan(arena.Now)); Assert.That(ReferenceEquals(arena.Player.currentCast, cast), Is.False);
            Setup(arena, driver, "whip", 0); Start(arena, source); Ticks(driver, 30); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero, "The next Create adopts the edited nearest hit box.");

            Setup(arena, driver, "base_sword", 3); source = Source(arena, "base_sword", 3); cast = Start(arena, source); Ticks(driver, 12);
            Assert.That(arena.enemies[0].health, Is.EqualTo(975)); source.hits[1].damage = 2; source.hits[1].time = 99; source.hits[1].anchor = "actor"; source.hits[1].box.x = -50; source.hits = Array.Empty<HitData>();
            cast.Cancel(); arena.Player.Position = new Vector2(10, 1000); Physics2D.SyncTransforms(); Ticks(driver, 24);
            Assert.That(arena.enemies[0].health, Is.EqualTo(950)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(2), "The captured world hit survives cancellation and input-array replacement.");
            Setup(arena, driver, "base_sword", 3); Assert.Throws<ArgumentException>(() => Start(arena, source), "A malformed whole finite candidate is rejected instead of reverting to legacy timing.");
            source = Source(arena, "base_sword", 3); source.actionTracks.hits[0].box.x = source.actionTracks.hits[1].box.x = -50;
            Start(arena, source); Ticks(driver, 40); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero, "The next valid finite start adopts authored geometry.");

            bool wasOneWay = floor.oneWay; floor.oneWay = true;
            try
            {
                Setup(arena, driver, "greatsword", 3, 3); source = Source(arena, "greatsword", 3); cast = Start(arena, source);
                source.effect.x = -50; source.effect.y = 50; source.effect.type = "stealth"; source.hits[0].box.width = .001f; source.hits[0].time = 99; source.hits[0].damage = 1;
                Ticks(driver, 50); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                Assert.That(arena.enemies[0].health, Is.EqualTo(940)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
            }
            finally { floor.oneWay = wasOneWay; }
        }

        static void SpreadAndFragments(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, "launcher", 0, 40);
            var basic = arena.Player.Form.basics[0]; var source = new SkillData { id = "launcher_start_spread_fixture", kind = basic.kind, activation = basic.impact, end = basic.duration,
                inputSpeed = arena.Player.Form.inputSpeed, vertical = "normal", facing = "windup_start", ranged = JsonUtility.FromJson<RangedData>(JsonUtility.ToJson(basic.ranged)),
                hits = new[] { new HitData { time = basic.impact, damage = basic.damage, active = basic.active, anchor = "ranged_projectile", targets = "all" } }, invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>() };
            source.ranged.angles = new[] { -30f, 0f, 30f }; var cast = Start(arena, source);
            source.ranged.angles[0] = -90; source.ranged.angles[1] = 90; source.ranged.angles = new[] { 90f };
            Ticks(driver, 8); Assert.That(arena.playerShots.Count, Is.EqualTo(3));
            Assert.That(arena.playerShots[0].velocity.y, Is.LessThan(0)); Assert.That(arena.playerShots[1].velocity.y, Is.Zero); Assert.That(arena.playerShots[2].velocity.y, Is.GreaterThan(0));
            Setup(arena, driver, "launcher", 0, 40); Start(arena, source); Ticks(driver, 8); Assert.That(arena.playerShots.Count, Is.EqualTo(1)); Assert.That(arena.playerShots[0].velocity.y, Is.GreaterThan(0));

            Setup(arena, driver, "bomb", 0, 40); source = Source(arena, "bomb", 0); source.hits = new[] { source.hits[0] }; source.bomb.spawnHeight = 5;
            source.bomb.fragmentAngles = new[] { 0f, 90f }; source.bomb.fragmentSpeed = 4; source.bomb.fragmentDamage = 3; source.bomb.fragmentRadius = .1f;
            source.bomb.fragmentBlastRadius = .3f; source.bomb.fragmentLifetime = 1; source.bomb.fragmentGravity = 0; source.bomb.fragmentSpawnOffset = .25f;
            cast = Start(arena, source); source.bomb.fragmentAngles[0] = 180; source.bomb.fragmentAngles = new[] { -90f }; source.bomb.fragmentSpeed = 1;
            source.bomb.fragmentDamage = 1; source.bomb.fragmentRadius = .05f; source.bomb.fragmentSpawnOffset = 2;
            Ticks(driver, 12); Assert.That(arena.bombs.Count, Is.EqualTo(1)); var bomb = arena.bombs[0]; Vector2 at = bomb.Position; ExplodeOnContact(bomb);
            Assert.That(arena.bombs.Count, Is.EqualTo(2)); Assert.That(arena.bombs[0].velocity.x, Is.EqualTo(4)); Assert.That(arena.bombs[1].velocity.y, Is.EqualTo(4));
            Assert.That(arena.bombs[0].damage, Is.EqualTo(3)); Assert.That(arena.bombs[0].transform.GetChild(0).localScale.x, Is.EqualTo(.2f)); Assert.That(arena.bombs[0].Position.x-at.x, Is.EqualTo(.25f).Within(.0001f));
            Setup(arena, driver, "bomb", 0, 40); Start(arena, source); Ticks(driver, 12); ExplodeOnContact(arena.bombs[0]); Assert.That(arena.bombs.Count, Is.EqualTo(1));
            Assert.That(arena.bombs[0].velocity.y, Is.EqualTo(-1)); Assert.That(arena.bombs[0].damage, Is.EqualTo(1));
        }
        static void ExplodeOnContact(BombProjectile bomb)
        {
            var contact = new GameObject("SkillFragmentContact"); contact.transform.position = bomb.Position; contact.AddComponent<ArenaSurface>();
            var collider = contact.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.02f, .02f); Physics2D.SyncTransforms();
            try { bomb.SweepTo(bomb.Position); }
            finally { collider.enabled = false; Object.Destroy(contact); }
        }

        static void MovementAndDefense(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, "base_sword", 2, 40); var source = Source(arena, "base_sword", 2); var cast = Start(arena, source);
            source.movement.speed = 1; source.movement.duration = .04f; source.movement.start = 99; source.vertical = "normal"; source.inputSpeed = 90;
            Ticks(driver, 32); Assert.That(arena.Player.Position.x, Is.EqualTo(4).Within(.0001f)); Assert.That(arena.Player.currentCast, Is.Null);
            Setup(arena, driver, "base_sword", 2, 40); Assert.Throws<ArgumentException>(() => Start(arena, source), "Typed movement/control policy mismatches cannot be partially published.");
            source = Source(arena, "base_sword", 2); source.actionTracks.timeline.frames[0].seconds = .25f;
            var delayed = Start(arena, source); Assert.That(delayed.Definition.Activation, Is.EqualTo(.25f)); Ticks(driver, 5); Assert.That(arena.Player.Position.x, Is.Zero.Within(.0001f));
            Ticks(driver, 35); Assert.That(arena.Player.Position.x, Is.EqualTo(4).Within(.0001f));

            Setup(arena, driver, "dagger", 1, 40); source = Source(arena, "dagger", 1); cast = Start(arena, source);
            source.invulStart[0] = 98; source.invulEnd[0] = 99; source.invulStart = Array.Empty<float>(); source.invulEnd = Array.Empty<float>();
            Ticks(driver, 9); Assert.That(cast.Invulnerable, Is.True); Assert.That(arena.Player.Receive(10, arena.enemies[0], 1, false, null), Is.False);
            Setup(arena, driver, "dagger", 1, 40); cast = Start(arena, source); Ticks(driver, 9); Assert.That(cast.Invulnerable, Is.False); Assert.That(arena.Player.Receive(10, arena.enemies[0], 1, false, null), Is.True);

            Setup(arena, driver, "shield", 0); source = Source(arena, "shield", 0); cast = Start(arena, source); source.effect.type = "stealth"; source.effect.duration = 99;
            driver.Step(.02f); Assert.That(cast.TryParry(arena.enemies[0], false), Is.True); Assert.That(arena.enemies[0].health, Is.EqualTo(970)); Assert.That(arena.Player.Hidden, Is.False);
            Setup(arena, driver, "shield", 0); cast = Start(arena, source); driver.Step(.02f); Assert.That(cast.TryParry(arena.enemies[0], false), Is.False); Assert.That(arena.Player.Hidden, Is.True);
        }

        static void Effects(CombatArena arena, CombatArena.ManualSimulation driver, ArenaSurface floor)
        {
            Setup(arena, driver, "shield", 1, 40); var source = Source(arena, "shield", 1); Start(arena, source); source.effect.hp = 1; source.effect.reduction = 0; source.effect.duration = .01f;
            Ticks(driver, 8); Assert.That(arena.Player.shield, Is.EqualTo(30)); Assert.That(arena.Player.guardReduction, Is.EqualTo(.4f)); Assert.That(arena.Player.guardUntil, Is.GreaterThan(3));
            Setup(arena, driver, "shield", 1, 40); source.actionTracks.timeline.markers.Single(marker => marker.id == "guard-end").offset = .01f;
            Start(arena, source); Ticks(driver, 8); Assert.That(arena.Player.shield, Is.Zero); Assert.That(arena.Player.guardUntil, Is.LessThan(.2f));

            Setup(arena, driver, "dagger", 1, 40); source = Source(arena, "dagger", 1); var cast = Start(arena, source); Ticks(driver, 10); source.effect.type = "stealth"; source.effect.duration = .01f; cast.Cancel();
            Ticks(driver, 30); Assert.That(arena.Player.Hidden, Is.True, "Captured smoke remains independent after action cancellation."); arena.BreakStealth(arena.Player); Ticks(driver, 5); Assert.That(arena.Player.Hidden, Is.False, "Arena smoke-spent lookup reads the same immutable effect identity.");
            Setup(arena, driver, "dagger", 1, 40); Start(arena, source); Ticks(driver, 40); Assert.That(arena.Player.Hidden, Is.False);

            bool wasOneWay = floor.oneWay; floor.oneWay = true;
            try
            {
                Setup(arena, driver, "shield", 3, 40); source = Source(arena, "shield", 3); cast = Start(arena, source); source.effect.hp = 1; source.effect.duration = .01f; source.effect.x = -50;
                Ticks(driver, 15); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                Assert.That(arena.decoys.Count, Is.EqualTo(1)); Assert.That(arena.decoys[0].health, Is.EqualTo(40)); Assert.That(arena.decoys[0].Position.x, Is.GreaterThan(0));
                Setup(arena, driver, "shield", 3, 40); Start(arena, source); Ticks(driver, 15); Assert.That(arena.decoys.Count, Is.Zero);
            }
            finally { floor.oneWay = wasOneWay; }
        }

        static void ProjectileLaunches(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, "shuriken", 0, 40); var source = Source(arena, "shuriken", 0); var cast = Start(arena, source);
            source.ranged.speed = 1; source.ranged.spawnHeight = 7; source.ranged.spawnX = 9; source.ranged.distance = 1; source.ranged.radius = .05f; source.ranged.angles[0] = 90; source.hits[0].damage = 1;
            Ticks(driver, 12); Assert.That(arena.playerShots.Count, Is.EqualTo(1)); var shot = arena.playerShots[0]; Assert.That(shot.velocity.x, Is.EqualTo(18)); Assert.That(shot.Position.y, Is.EqualTo(1001).Within(.0001f)); Assert.That(shot.damage, Is.EqualTo(24)); Assert.That(shot.Definition.Distance, Is.EqualTo(8));
            cast.Cancel(); source.ranged.type = "laser"; source.ranged.distance = 90; Ticks(driver, 5); Assert.That(shot.Definition.Type, Is.EqualTo("shuriken_special")); Assert.That(shot.velocity.x, Is.EqualTo(18));
            source.ranged.type = "shuriken_special"; Setup(arena, driver, "shuriken", 0, 40); Start(arena, source); Ticks(driver, 12); shot = arena.playerShots[0]; Assert.That(shot.velocity.x, Is.EqualTo(1)); Assert.That(shot.Position.y, Is.EqualTo(1007).Within(.0001f)); Assert.That(shot.damage, Is.EqualTo(1));

            Setup(arena, driver, "bomb", 0, 40); source = Source(arena, "bomb", 0); cast = Start(arena, source); Ticks(driver, 12); Assert.That(arena.bombs.Count, Is.EqualTo(1));
            source.bomb.angle = -90; source.bomb.speed = 1; source.bomb.gravity = 0; source.bomb.spawnHeight = 7; source.bomb.radius = .05f; source.bomb.blastRadius = 9; source.hits[1].damage = source.hits[2].damage = 1;
            Ticks(driver, 16); Assert.That(arena.bombs.Count, Is.EqualTo(3)); Assert.That(arena.bombs[1].velocity.x, Is.GreaterThan(8)); Assert.That(arena.bombs[2].damage, Is.EqualTo(12)); Assert.That(arena.bombs[2].Definition.BlastRadius, Is.EqualTo(1.5f));
            Setup(arena, driver, "bomb", 0, 40); Start(arena, source); Ticks(driver, 12); Assert.That(arena.bombs[0].Position.y, Is.GreaterThan(1006)); Assert.That(arena.bombs[0].velocity.y, Is.EqualTo(-1)); Assert.That(arena.bombs[0].Definition.BlastRadius, Is.EqualTo(9));

            Setup(arena, driver, "base_sword", 1, 40); source = Source(arena, "base_sword", 1); cast = Start(arena, source); source.effect.speed = 0; source.effect.distance = .01f; source.hits[0].box.width = .001f; source.hits[0].box.height = .001f; source.hits[0].damage = 1;
            Ticks(driver, 15); arena.enemies[0].Position = new Vector2(5, 1000); Physics2D.SyncTransforms(); cast.Cancel(); Ticks(driver, 20); Assert.That(arena.enemies[0].health, Is.EqualTo(960)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
            Setup(arena, driver, "base_sword", 1, 40); Assert.Throws<ArgumentException>(() => Start(arena, source), "A zero-speed finite wave candidate cannot replace its live typed policy.");
            source = Source(arena, "base_sword", 1); source.actionTracks.timeline.frames[0].seconds = .3f;
            var delayedWave = Start(arena, source); Assert.That(delayedWave.Definition.Activation, Is.EqualTo(.3f)); Ticks(driver, 50);
        }

        static void Missile(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Setup(arena, driver, "bomb", 2, 40); var source = Source(arena, "bomb", 2); var cast = Start(arena, source);
            source.missile.speed = 1; source.missile.distance = 1; source.missile.manualDelay = 99; source.missile.blastRadius = .01f; source.missile.recoilHeight = 2; source.missile.recoilDistance = 9; source.missile.recovery = 9; source.hits[0].damage = 1;
            Ticks(driver, 21); Assert.That(cast.MissileFlying, Is.True); Assert.That(arena.Player.Position.x, Is.GreaterThan(2));
            arena.enemies[0].Position = arena.Player.Position + new Vector2(1.5f, 0); Physics2D.SyncTransforms();
            Assert.That(cast.TryMissileDetonate(), Is.True); Assert.That(arena.BombExplosionCount, Is.EqualTo(1)); Assert.That(arena.enemies[0].health, Is.EqualTo(976));
            Assert.That(arena.Player.velocity.y, Is.EqualTo(Mathf.Sqrt(2*arena.Movement.RiseGravity*.5f)));
            float end = cast.EndTime; Assert.That(end - arena.Now, Is.EqualTo(.3f).Within(.00001f));
            Setup(arena, driver, "bomb", 2, 40); cast = Start(arena, source); Ticks(driver, 20); Assert.That(cast.MissileFlying, Is.True); Assert.That(arena.Player.Position.x, Is.LessThan(.3f)); Assert.That(cast.TryMissileDetonate(), Is.False);
        }
    }
}
