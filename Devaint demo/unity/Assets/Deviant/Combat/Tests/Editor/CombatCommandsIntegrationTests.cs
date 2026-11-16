using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CombatCommandsIntegrationTests
    {
        [Test] public void UnconfiguredAndDestroyedCommandAdaptersRejectWithoutCreatingActorsOrAdvancing()
        {
            var root = new GameObject("UnboundCommands"); var arena = root.AddComponent<CombatArena>(); var commands = arena.Commands;
            var sample = new CombatInputSample(new CombatInputSession(7, 1, 0), 1, new CombatInput(0, CombatButtons.Basic),
                CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, 0);
            Assert.That(commands.SubmitPlayerInput(sample).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
            Assert.That(arena.Player, Is.Null); Assert.That(arena.SimulationTickCount, Is.Zero);
            Assert.That(commands.TryReadOutcomes(default, new CombatCommandOutcome[1], out _), Is.False);
            Object.DestroyImmediate(root);
            Assert.That(commands.SubmitPlayerInput(sample).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
            Assert.That(commands.TryReadOutcomes(default, new CombatCommandOutcome[1], out _), Is.False);
        }
        [UnityTest] public IEnumerator GameCommandsReportActualStartsReservationsAndTerminals() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCommandsReportActualStartsReservationsAndTerminals() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static CombatInputSample Sample(CombatPlayer player, CombatButtons button) => new CombatInputSample(player.InputSession, player.NextInputSequence,
            new CombatInput(0, button), CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, player.arena.Now);
        static List<CombatCommandOutcome> Poll(ICombatCommands commands, ref CombatOutcomeCursor cursor)
        {
            var values = new List<CombatCommandOutcome>(); var page = new CombatCommandOutcome[64];
            do
            {
                Assert.That(commands.TryReadOutcomes(cursor, page, out var result), Is.True);
                Assert.That(result.DroppedBefore, Is.Zero); cursor = result.Next;
                for (int i = 0; i < result.Count; i++) values.Add(page[i]);
                if (result.Count < page.Length) return values;
            } while (true);
        }
        static void Bind(CombatArena arena, out CombatOutcomeCursor cursor)
        {
            Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True);
            cursor = new CombatOutcomeCursor(session); Poll(arena.Commands, ref cursor);
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, out CombatOutcomeCursor cursor)
        {
            driver.Reset(); var player = arena.Player;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 1000); }
            player.selected = 0;
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var error), Is.True, error);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.SyncFacing(1); player.grounded = true;
            Physics2D.SyncTransforms(); Bind(arena, out cursor);
        }
        static CombatCommandOutcome Find(List<CombatCommandOutcome> outcomes, CombatInputSample sample, CombatCommandState state)
            => outcomes.Single(o => o.Command.Equals(CombatCommandKey.For(sample)) && o.State == state);
        static void Tick(CombatArena arena) => ((CombatSimulation)typeof(CombatArena).GetField("simulation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(arena)).Step(.02f);
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            CheckCommands();
        }
        // Keep captured callbacks out of the iterator state restored across a domain reload.
        static void CheckCommands()
        {
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var commands = arena.Commands;
            var floor = new GameObject("CommandOutcomeFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Prepare(arena, driver, out var cursor);
                    var basic = Sample(player, CombatButtons.Basic);
                    Assert.That(commands.SubmitPlayerInput(basic).Status, Is.EqualTo(CombatInputAdmissionStatus.Admitted));
                    Assert.That(Poll(commands, ref cursor), Is.Empty); Assert.That(player.currentCast, Is.Null);
                    Tick(arena); var first = player.currentCast; var started = Find(Poll(commands, ref cursor), basic, CombatCommandState.Started);
                    Assert.That(started.ExecutionId, Is.EqualTo(first.ExecutionId)); Assert.That(started.Snapshot.Action.ExecutionId, Is.EqualTo(first.ExecutionId));
                    Assert.That(started.Snapshot.Handle.Equals(started.Command.Actor), Is.True); Assert.That(started.TickCount, Is.Zero); Assert.That(started.Now, Is.Zero);
                    Assert.That(first.ResourceResult, Is.Null, "Starting windup must not move resource spending earlier.");
                    var queued = Sample(player, CombatButtons.Basic); commands.SubmitPlayerInput(queued); Tick(arena);
                    Assert.That(Poll(commands, ref cursor), Is.Empty, "Reservation is neither a start nor completion.");
                    var special = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(special); Tick(arena);
                    var replaced = Find(Poll(commands, ref cursor), queued, CombatCommandState.Rejected); Assert.That(replaced.Reason, Is.EqualTo(CombatCommandReason.Priority));
                    var all = new List<CombatCommandOutcome>();
                    for (int i = 0; i < 80 && player.currentCast == first; i++) { driver.Step(.02f); all.AddRange(Poll(commands, ref cursor)); }
                    Assert.That(Find(all, basic, CombatCommandState.Completed).ExecutionId, Is.EqualTo(started.ExecutionId));
                    var specialStart = Find(all, special, CombatCommandState.Started); Assert.That(specialStart.Command.Sequence, Is.EqualTo(special.Sequence));
                    Assert.That(player.currentCast.StartedAt, Is.EqualTo(first.EndTime)); Assert.That(specialStart.ExecutionId, Is.EqualTo(player.currentCast.ExecutionId));
                    player.CancelAction(); var cancelled = Find(Poll(commands, ref cursor), special, CombatCommandState.Cancelled);
                    Assert.That(cancelled.ExecutionId, Is.EqualTo(specialStart.ExecutionId)); Assert.That(cancelled.Reason, Is.EqualTo(CombatCommandReason.Interrupted));
                    Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));

                    Prepare(arena, driver, out cursor);
                    var low = Sample(player, CombatButtons.Basic); commands.SubmitPlayerInput(low);
                    var dash = Sample(player, CombatButtons.Dash); commands.SubmitPlayerInput(dash);
                    var duplicateDash = Sample(player, CombatButtons.Dash); commands.SubmitPlayerInput(duplicateDash);
                    Tick(arena); var priority = Poll(commands, ref cursor);
                    Assert.That(Find(priority, low, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Priority));
                    Assert.That(Find(priority, duplicateDash, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Priority));
                    var dashStart = Find(priority, dash, CombatCommandState.Started); Assert.That(player.Dashing, Is.True);
                    for (int i = 0; i < 30 && player.Dashing; i++) driver.Step(.02f);
                    Tick(arena); Assert.That(Find(Poll(commands, ref cursor), dash, CombatCommandState.Completed).ExecutionId, Is.EqualTo(dashStart.ExecutionId));

                    Prepare(arena, driver, out cursor);
                    basic = Sample(player, CombatButtons.Basic); commands.SubmitPlayerInput(basic); Tick(arena); Poll(commands, ref cursor);
                    queued = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(queued); Tick(arena); Poll(commands, ref cursor);
                    var oldSession = player.InputSession; var oldCursor = cursor;
                    arena.ResetTest(); Assert.That(commands.TryReadOutcomes(oldCursor, new CombatCommandOutcome[8], out _), Is.False);
                    Assert.That(arena.ReadModel.TryGetSession(out var reset), Is.True); cursor = new CombatOutcomeCursor(reset, oldCursor.AfterEventSequence);
                    var resetOutcomes = Poll(commands, ref cursor);
                    Assert.That(Find(resetOutcomes, basic, CombatCommandState.Cancelled).Reason, Is.EqualTo(CombatCommandReason.Reset));
                    Assert.That(Find(resetOutcomes, queued, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Reset));
                    long next = player.NextInputSequence;
                    var stale = new CombatInputSample(oldSession, next, new CombatInput(0, CombatButtons.Basic), CombatInputSource.Simulation, 0, CombatInputTimeDomain.Simulation, arena.Now);
                    Assert.That(commands.SubmitPlayerInput(stale).Status, Is.EqualTo(CombatInputAdmissionStatus.RejectedSession));
                    Assert.That(player.NextInputSequence, Is.EqualTo(next)); Assert.That(Find(Poll(commands, ref cursor), stale, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Session));
                    player.InputBlocked = true;
                    try
                    {
                        var blocked = Sample(player, CombatButtons.Basic); Assert.That(commands.SubmitPlayerInput(blocked).Status, Is.EqualTo(CombatInputAdmissionStatus.Discarded));
                        Assert.That(Find(Poll(commands, ref cursor), blocked, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Blocked));
                    }
                    finally { player.InputBlocked = false; }

                    // The third jump press is deferred until the existing landing
                    // buffer executes. No false rejection/start at its press tick.
                    Prepare(arena, driver, out cursor); player.Position = new Vector2(0, 1010); player.grounded = false;
                    for (int i = 0; i < 2; i++) { var jump = Sample(player, CombatButtons.Jump); commands.SubmitPlayerInput(jump); Tick(arena); Poll(commands, ref cursor); }
                    var buffered = Sample(player, CombatButtons.Jump); commands.SubmitPlayerInput(buffered); Tick(arena);
                    Assert.That(Poll(commands, ref cursor), Is.Empty);
                    player.Landed(); var landing = Poll(commands, ref cursor);
                    Assert.That(Find(landing, buffered, CombatCommandState.Started).ExecutionId, Is.Not.EqualTo(Guid.Empty));
                    Assert.That(Find(landing, buffered, CombatCommandState.Completed).ExecutionId, Is.EqualTo(Find(landing, buffered, CombatCommandState.Started).ExecutionId));
                    var secondJump = Sample(player, CombatButtons.Jump); commands.SubmitPlayerInput(secondJump); Tick(arena); Poll(commands, ref cursor);
                    var expired = Sample(player, CombatButtons.Jump); commands.SubmitPlayerInput(expired); Tick(arena);
                    Assert.That(Poll(commands, ref cursor), Is.Empty);
                    all.Clear();float expiresAt=player.jumps.BufferedUntil;
                    for(int i=0;i<16&&arena.Now<=expiresAt+.02f;i++){driver.Step(.02f);all.AddRange(Poll(commands,ref cursor));}
                    Assert.That(Find(all, expired, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.BufferExpired));

                    Prepare(arena, driver, out cursor);
                    var swap = Sample(player, CombatButtons.Swap); commands.SubmitPlayerInput(swap); Tick(arena); var swapped = Poll(commands, ref cursor);
                    Assert.That(Find(swapped, swap, CombatCommandState.Started).ExecutionId, Is.EqualTo(Find(swapped, swap, CombatCommandState.Completed).ExecutionId));
                    Assert.That(player.selected, Is.EqualTo(1));
                    var drop = Sample(player, CombatButtons.Drop); commands.SubmitPlayerInput(drop); Tick(arena);
                    Assert.That(Find(Poll(commands, ref cursor), drop, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.NoPlatform));

                    Prepare(arena, driver, out cursor);
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "whip"), 2, out var recastError), Is.True, recastError);
                    var marked = arena.enemies[0]; marked.Position = new Vector2(1, 1000); marked.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    special = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(special); Tick(arena); var primary = player.currentCast; Poll(commands, ref cursor);
                    for (int i = 0; i < 60 && marked.ReceivedHitCount == 0; i++) driver.Step(.02f);
                    Assert.That(marked.ReceivedHitCount, Is.GreaterThan(0)); Poll(commands, ref cursor);
                    var recast = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(recast); Tick(arena);
                    var recastStart = Find(Poll(commands, ref cursor), recast, CombatCommandState.Started);
                    Assert.That(recastStart.ExecutionId, Is.EqualTo(primary.ExecutionId)); Assert.That(primary.Approaching, Is.True);
                    all.Clear(); for (int i = 0; i < 50; i++) { driver.Step(.02f); all.AddRange(Poll(commands, ref cursor)); }
                    Assert.That(Find(all, recast, CombatCommandState.Completed).ExecutionId, Is.EqualTo(primary.ExecutionId));
                    Prepare(arena, driver, out cursor);
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "whip"), 2, out recastError), Is.True, recastError);
                    marked = arena.enemies[0]; marked.Position = new Vector2(1, 1000); marked.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    special = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(special); Tick(arena); Poll(commands, ref cursor);
                    for (int i = 0; i < 60 && marked.ReceivedHitCount == 0; i++) driver.Step(.02f); Poll(commands, ref cursor);
                    recast = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(recast);
                    Application.LogCallback cancelAtRecord = (message, stack, type) => { if (message.Contains("whip D recast: approach speed=")) player.CancelAction(); };
                    Application.logMessageReceived += cancelAtRecord;
                    try { Tick(arena); }
                    finally { Application.logMessageReceived -= cancelAtRecord; }
                    var callbackOutcomes = Poll(commands, ref cursor);
                    recastStart = Find(callbackOutcomes, recast, CombatCommandState.Started);
                    var recastCancel = Find(callbackOutcomes, recast, CombatCommandState.Cancelled);
                    Assert.That(recastCancel.EventSequence, Is.GreaterThan(recastStart.EventSequence)); Assert.That(recastCancel.ExecutionId, Is.EqualTo(recastStart.ExecutionId));
                    Assert.That(callbackOutcomes.Count(o => o.Command.Equals(CombatCommandKey.For(recast)) && o.State == CombatCommandState.Started), Is.EqualTo(1));

                    // Manual missile detonation uses its real boolean receipt;
                    // an early press cannot be reported as a completed action.
                    Prepare(arena, driver, out cursor);
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "bomb"), 2, out var missileError), Is.True, missileError);
                    foreach (var enemy in arena.enemies) enemy.Position = new Vector2(45, 1000);
                    special = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(special); Tick(arena); var missile = player.currentCast; Poll(commands, ref cursor);
                    for (int i = 0; i < 60 && !missile.MissileFlying; i++) driver.Step(.02f);
                    Assert.That(missile.MissileFlying, Is.True); Poll(commands, ref cursor);
                    var early = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(early); Tick(arena);
                    Assert.That(Find(Poll(commands, ref cursor), early, CombatCommandState.Rejected).Reason, Is.EqualTo(CombatCommandReason.Cooldown));
                    for (int i = 0; i < 12; i++) driver.Step(.02f); Poll(commands, ref cursor);
                    var detonate = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(detonate); Tick(arena); var detonated = Poll(commands, ref cursor);
                    Assert.That(Find(detonated, detonate, CombatCommandState.Started).ExecutionId, Is.EqualTo(missile.ExecutionId));
                    Assert.That(Find(detonated, detonate, CombatCommandState.Completed).ExecutionId, Is.EqualTo(missile.ExecutionId));
                    Assert.That(missile.MissileFlying, Is.False);

                    // Accepted special windup can fail at the actual activation
                    // resource boundary without completing or spending a charge.
                    Prepare(arena, driver, out cursor); special = Sample(player, CombatButtons.Special); commands.SubmitPlayerInput(special); Tick(arena);
                    var cast = player.currentCast; Poll(commands, ref cursor); Assert.That(player.Weapon.Cooldown.Spend(arena.Now), Is.True);
                    all.Clear(); for (int i = 0; i < 60; i++) { driver.Step(.02f); all.AddRange(Poll(commands, ref cursor)); }
                    var failed = Find(all, special, CombatCommandState.Failed); Assert.That(failed.ExecutionId, Is.EqualTo(cast.ExecutionId));
                    Assert.That(failed.Reason, Is.EqualTo(CombatCommandReason.Resources)); Assert.That(failed.Detail, Is.Not.Empty);
                    Assert.That(all.Any(o => o.Command.Equals(CombatCommandKey.For(special)) && o.State == CombatCommandState.Completed), Is.False);
                    Assert.That(arena.SimulationFaulted, Is.False);

                    // The existing partially constructed same-owner sentinel
                    // makes real Create fail in BreakStealth before assignment.
                    Prepare(arena, driver, out cursor);
                    var sentinel = new GameObject("CommandCreateFailure").AddComponent<CombatCast>(); sentinel.arena = arena; sentinel.owner = player; arena.casts.Add(sentinel);
                    var before = new HashSet<CombatCast>(Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                    basic = Sample(player, CombatButtons.Basic); commands.SubmitPlayerInput(basic);
                    try
                    {
                        Assert.Throws<NullReferenceException>(() => Tick(arena));
                        var exception = Find(Poll(commands, ref cursor), basic, CombatCommandState.Failed);
                        Assert.That(exception.Reason, Is.EqualTo(CombatCommandReason.Exception)); Assert.That(player.currentCast, Is.Null);
                        Assert.That(arena.SimulationFaulted, Is.True); Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));
                    }
                    finally
                    {
                        arena.casts.Remove(sentinel); Object.DestroyImmediate(sentinel.gameObject);
                        foreach (var partial in Object.FindObjectsByType<CombatCast>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (!before.Contains(partial) && partial.arena == arena) Object.DestroyImmediate(partial.gameObject);
                    }
                    driver.Reset();
                }
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
