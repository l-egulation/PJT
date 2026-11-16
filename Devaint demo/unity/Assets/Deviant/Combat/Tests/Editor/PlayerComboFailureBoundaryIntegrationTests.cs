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
    public sealed class ComboCancellationOrderProbe : CombatPlayer
    {
        public Action BeforeCancel;
        public override void CancelAttack() { BeforeCancel?.Invoke(); base.CancelAttack(); }
    }

    public class PlayerComboFailureBoundaryIntegrationTests
    {
        const float Tick = .015625f;
        [UnityTest] public IEnumerator GameKeepsComboStateAtCallbackAndCreateFailureBoundaries() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabKeepsComboStateAtCallbackAndCreateFailureBoundaries() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.grounded = true; p.velocity = Vector2.zero; Physics2D.SyncTransforms();
        }
        static CombatCast FinishedFirst(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Step(Tick, new CombatInput(0, CombatButtons.Basic)); var first = arena.Player.currentCast;
            Assert.That(first && first.isBasic, Is.True);
            for (int i = 0; i < 100 && arena.Player.currentCast; i++) driver.Step(Tick);
            Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Player.BasicIndex, Is.Zero);
            Assert.That(arena.Now, Is.LessThan(first.EndTime + .3f)); return first;
        }
        static void CreateFailure(CombatArena arena, CombatArena.ManualSimulation driver, bool special)
        {
            Prepare(arena, driver); var p = arena.Player; FinishedFirst(arena, driver);
            if (special)
            {
                Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.True); Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.True);
                Assert.That(p.jumps.Press(arena.Now, arena.Movement, out _), Is.False); Assert.That(p.jumps.BufferedUntil, Is.GreaterThan(arena.Now));
            }
            // A same-owner unfinished cast deliberately fails the real Create
            // call in BreakStealth, after its new resources/runner exist but
            // before the returned cast can be assigned or listed. This is not
            // a simulated throwing callback or a replacement action factory.
            var sentinel = new GameObject("ComboCreateFailureSentinel").AddComponent<CombatCast>();
            sentinel.arena = arena; sentinel.owner = p; arena.casts.Add(sentinel);
            var before = new HashSet<CombatCast>(Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            var partial = new List<CombatCast>(); float now = arena.Now;
            try
            {
                p.QueueTestInput(basic: !special, special: special);
                Assert.Throws<NullReferenceException>(() => p.Step(Tick));
                Assert.That(p.currentCast, Is.Null); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationFaulted, Is.False);
                Assert.That(p.BasicIndex, Is.EqualTo(special ? -1 : 1), "Basic commits before Create; an admitted special clears before Create.");
                Assert.That(p.Weapon.Cooldown.Available, Is.EqualTo(1), "Neither failed start reaches activation-time resource spending.");
                if (special) { Assert.That(p.jumps.Used, Is.EqualTo(2)); Assert.That(p.jumps.BufferedUntil, Is.EqualTo(-1)); }
                foreach (var cast in Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!before.Contains(cast) && cast.arena == arena && cast.owner == p) partial.Add(cast);
                Assert.That(partial, Has.Count.EqualTo(1));
                Assert.That(partial[0].Definition.Id, Is.EqualTo(special ? "base_sword_default" : "base_sword_basic_1"));
                Assert.That(partial[0].StartedAt, Is.EqualTo(now)); Assert.That(partial[0].ResourcesActive, Is.True);
                Assert.That(partial[0].ResourceResult, Is.Null); Assert.That(arena.casts.Contains(partial[0]), Is.False);
            }
            finally
            {
                p.ClearInputState(); arena.casts.Remove(sentinel); Object.Destroy(sentinel.gameObject);
                foreach (var cast in Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!before.Contains(cast) && cast.arena == arena && cast.owner == p)
                    { cast.Cancel(); Object.Destroy(cast.gameObject); }
            }
            // The untouched deadline combines with the already committed index
            // after basic failure; the special's clear starts a fresh sequence.
            p.QueueTestInput(basic: true); p.Step(Tick);
            Assert.That(p.currentCast.Definition.Id, Is.EqualTo("base_sword_basic_0")); Assert.That(p.BasicIndex, Is.Zero);
            p.CancelAction();
        }
        static void CancelOrder(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            Prepare(arena, driver);
            var probe = new GameObject("ComboCancellationOrderProbe").AddComponent<ComboCancellationOrderProbe>();
            probe.BindLoadout(Deviant.Equipment.WeaponResources.CreateLoadout());
            probe.Configure(arena, new Vector2(8, 1000), arena.Player.size, arena.Player.offset, false);
            Assert.That(probe.BasicIndex, Is.EqualTo(-1), "A newly constructed player is not a default zero-index combo.");
            int callbacks = 0; CombatCast cast = null;
            try
            {
                typeof(CombatPlayer).GetMethod("BeginBasic", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(probe, new object[] { default(CombatCommandKey), 0, -1f }); cast = probe.currentCast;
                Assert.That(cast && cast.isBasic, Is.True); Assert.That(probe.BasicIndex, Is.Zero);
                probe.BeforeCancel = () =>
                {
                    callbacks++; Assert.That(probe.BasicIndex, Is.Zero); Assert.That(probe.currentCast, Is.SameAs(cast));
                    Assert.That(cast.ResourcesActive, Is.True, "The virtual cancellation callback precedes cast resource release and combo clearing.");
                    if (callbacks == 1) throw new InvalidOperationException("cancel callback");
                };
                Assert.Throws<InvalidOperationException>(() => probe.CancelAction());
                Assert.That(probe.BasicIndex, Is.Zero); Assert.That(probe.currentCast, Is.SameAs(cast)); Assert.That(cast.ResourcesActive, Is.True);
                probe.CancelAction(); Assert.That(callbacks, Is.EqualTo(2)); Assert.That(probe.BasicIndex, Is.EqualTo(-1));
                Assert.That(probe.currentCast, Is.Null); Assert.That(cast.ResourcesActive, Is.False);
                Assert.That(arena.Player.BasicIndex, Is.EqualTo(-1), "An independent player never shares combo state.");
            }
            finally { probe.BeforeCancel = null; probe.CancelAction(); arena.RemoveOwnedEffects(probe); Object.Destroy(probe.gameObject); }
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var p = arena.Player; Vector2 spawn = p.spawn;
            var floor = new GameObject("ComboFailureBoundaryFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); p.spawn = new Vector2(0, 1000);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                { CreateFailure(arena, driver, false); CreateFailure(arena, driver, true); CancelOrder(arena, driver); }
            }
            finally { p.spawn = spawn; Object.Destroy(floor); }
        }
    }
}
