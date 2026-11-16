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
    public class SpecialFrameTailPolicyTests
    {
        [UnityTest] public IEnumerator GameExplicitLocalPolicyKeepsZeroParryAndDelayedWorldTail() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabExplicitLocalPolicyKeepsZeroParryAndDelayedWorldTail() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string id)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; enemy.maxHealth = enemy.health = 1000; }
            int form = Array.FindIndex(arena.catalog.forms, value => value.specials.Any(skill => skill.id == id));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
            Assert.That(player.TryReconfigure(0, form, special, out var error), Is.True, error); foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static FrameTimeline Timeline(ActionSpecialFrameDefinition action, params TimelineMarker[] markers)
        {
            var frames = action.Media.StepIds.Select(id => new TimelineFrame(id, .125f)).ToArray();
            float duration = 0; foreach (var frame in frames) duration += frame.Seconds;
            return new FrameTimeline(frames, markers.Concat(new[] { new TimelineMarker("body-start", TimelineAnchor.Seconds, "", 0),
                new TimelineMarker("body-end", TimelineAnchor.Seconds, "", duration) }));
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var target = arena.enemies[0];
            var originals = arena.SpecialFrames.Actions.ToArray();
            var floor = new GameObject("SpecialFrameTailFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Setup(arena, driver, "shield_default"); var numeric = arena.GetSpecialFrame("shield_default");
                    Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.ProjectAuthored(numeric.NumericAuthored, numeric.Media));
                    var local = Timeline(numeric, new TimelineMarker("activation", TimelineAnchor.Seconds, "", 0),
                        new TimelineMarker("parry-start", TimelineAnchor.Seconds, "", 0), new TimelineMarker("parry-end", TimelineAnchor.Marker, "parry-start", .125f));
                    var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric.NumericAuthored, numeric.Media, local);
                    var parry = ActionSpecialFrameCompiler.CompileAuthored(numeric.NumericAuthored, numeric.Media, source);
                    Assert.That(arena.TryApplySpecialFrames(new[] { parry }, false, out var error), Is.True, error);
                    target.Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var cast = player.currentCast;
                    Assert.That(cast, Is.Not.Null); Assert.That(cast.Definition.Activation, Is.Zero);
                    Assert.That(cast.TryParry(target, false, target.Center), Is.True); Assert.That(target.ReceivedHitCount, Is.EqualTo(1));
                    for (int tick = 0; tick < 10; tick++) driver.Step(.02f);
                    Assert.That(cast.TryParry(target, false, target.Center), Is.False, "The selected positive parry interval ends at its original zero action epoch.");

                    Setup(arena, driver, "greatsword_follow_up"); numeric = arena.GetSpecialFrame("greatsword_follow_up");
                    Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.ProjectAuthored(numeric.NumericAuthored, numeric.Media));
                    var hitMarkers = Enumerable.Range(0, numeric.Execution.Hits.Count).SelectMany(i => new[]
                    {
                        new TimelineMarker($"hit-{i:00}-start", TimelineAnchor.Marker, "body-end", .25f + i * .15f),
                        new TimelineMarker($"hit-{i:00}-end", TimelineAnchor.Marker, $"hit-{i:00}-start", .08f)
                    });
                    local = Timeline(numeric, hitMarkers.Prepend(new TimelineMarker("activation", TimelineAnchor.Seconds, "", .125f)).ToArray());
                    source = ActionSpecialFrameCompiler.AuthorAuthored(numeric.NumericAuthored, numeric.Media, local);
                    var sword = ActionSpecialFrameCompiler.CompileAuthored(numeric.NumericAuthored, numeric.Media, source);
                    Assert.That(sword.Execution.Hits[0].Time, Is.GreaterThan(sword.Execution.End));
                    Assert.That(arena.TryApplySpecialFrames(new[] { sword }, false, out error), Is.True, error);
                    target.Position = new Vector2(numeric.NumericAuthored.Execution.Effect.X, 1000); Physics2D.SyncTransforms();
                    // A wall through the installation box blocks the throw
                    // before resources are committed. A supporting floor does not.
                    var wall = new GameObject("Throw obstruction");
                    wall.transform.position = new Vector2(numeric.NumericAuthored.Execution.Effect.X, 1001.5f);
                    wall.AddComponent<ArenaSurface>();
                    var wallCollider = wall.AddComponent<BoxCollider2D>();
                    wallCollider.size = new Vector2(.5f, 3);
                    Physics2D.SyncTransforms();
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var rejected = player.currentCast;
                    Assert.That(rejected, Is.Not.Null);
                    while (arena.Now < sword.Execution.Activation + .04f) driver.Step(.02f);
                    Assert.That(rejected.ResourceResult, Is.Null); Assert.That(rejected.ResourcesActive, Is.False);
                    Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(target.ReceivedHitCount, Is.Zero);
                    while (arena.Now < sword.Execution.End + .05f) driver.Step(.02f);
                    Assert.That(arena.casts.Contains(rejected), Is.False); Assert.That(target.ReceivedHitCount, Is.Zero);

                    wallCollider.enabled = false;
                    Object.Destroy(wall);
                    Physics2D.SyncTransforms();
                    Setup(arena, driver, sword.Id); target.Position = new Vector2(numeric.NumericAuthored.Execution.Effect.X, 1000); Physics2D.SyncTransforms();
                    Assert.That(arena.HitEvents.TryGetCursor(out var cursor), Is.True); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); cast = player.currentCast;
                    Assert.That(cast, Is.Not.Null); Guid execution = cast.ExecutionId; float end = sword.Execution.End;
                    while (arena.Now < sword.Execution.Activation + .04f) driver.Step(.02f);
                    Assert.That(cast.ResourceResult, Is.Not.Null); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
                    Assert.That(player.Weapon.Cooldown.Available, Is.Zero); Assert.That(player.currentCast, Is.SameAs(cast));
                    Assert.That(arena.ReadModel.TryGetSession(out var session), Is.True);
                    Assert.That(arena.ReadModel.TryReadPlayer(session, out var state), Is.True); Assert.That(state.Action.Phase, Is.EqualTo(CombatActionReadPhase.Active));
                    while (arena.Now < end + .05f) driver.Step(.02f);
                    Assert.That(player.currentCast, Is.Null); Assert.That(target.ReceivedHitCount, Is.Zero);
                    Assert.That(arena.casts.Contains(cast), Is.True, "The independent saved-spawn hit must retain its original owner past body completion.");
                    player.SyncFacing(-1);
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                    Assert.That(player.currentCast, Is.Not.Null);
                    Assert.That(player.currentCast.isBasic, Is.True);
                    Assert.That(arena.casts.Contains(cast), Is.True);
                    var restore = originals.Single(value => value.Id == sword.Id);
                    Assert.That(arena.TryApplySpecialFrames(new[] { restore }, false, out error), Is.True, error);
                    for (int tick = 0; tick < 30; tick++) driver.Step(.02f);
                    var hits = new CombatAcceptedHit[32]; Assert.That(arena.HitEvents.TryRead(cursor, hits, out var page), Is.True);
                    var accepted = hits.Take(page.Count).Where(hit => hit.Origin.ExecutionId == execution && hit.Target.ActorId == target.ActorId).ToArray();
                    Assert.That(accepted, Has.Length.EqualTo(3));
                    Assert.That(accepted.All(hit => hit.Origin.Kind == CombatHitKind.Independent), Is.True);
                    Assert.That(accepted.Sum(hit => hit.Damage), Is.EqualTo(60).Within(.001f));
                    Assert.That(arena.TryApplySpecialFrames(originals, true, out error), Is.True, error);
                }
            }
            finally { arena.Presentation.Reset(); Object.Destroy(floor); }
        }
    }
}
