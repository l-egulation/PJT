using System;
using System.Collections;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.World;
using Deviant.World.Contracts;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class MovementValidationIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static MonoBehaviour Forest(CombatArena arena)
        {
            var game = arena.GetComponent("GameComposition"); return (MonoBehaviour)game.GetType().GetField("forest").GetValue(game);
        }
        static ChapterSession Chapter(MonoBehaviour forest) => (ChapterSession)forest.GetType().GetProperty("Chapter").GetValue(forest);
        static bool Prepare(MonoBehaviour forest, CombatPlayer player, LocomotionProfile movement, out ChapterSession.Revalidation prepared, out string error)
        {
            var arguments = new object[] { UnityChapterModules.ProfileFor(player, movement), null, null };
            bool result = (bool)forest.GetType().GetMethod("TryPrepareLayoutProfile").Invoke(forest, arguments);
            prepared = (ChapterSession.Revalidation)arguments[1]; error = (string)arguments[2]; return result;
        }
        [UnityTest] public IEnumerator ImpossibleCandidateIsRejectedBeforeAnyMovementResetOrProofPublication()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var forest = Forest(arena); var chapter = Chapter(forest);
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                player.health = 42; player.SubmitInput(new CombatInput(1, CombatButtons.Jump));
                var movement = arena.Movement; var cast = player.currentCast; var ingress = player.InputSession;
                var input = player.CurrentInput; var position = player.Position; var velocity = player.velocity;
                var proof = chapter.MovementProof; var profile = chapter.Profile; var map = chapter.ResolvedLayoutHash;
                long tick = arena.SimulationTickCount; float now = arena.Now; int loaded = chapter.LoadedModuleCount, reset = 0;
                Action observed = () => reset++; arena.ResetCompleted += observed;
                try
                {
                    var candidate = movement.ToData(); candidate.firstJumpVelocity = candidate.secondJumpVelocity = .01f;
                    Assert.That(arena.TryApplyMovementAndReset(candidate, out var error), Is.False);
                    Assert.That(error, Does.Contain("witness"));
                    Assert.That(arena.Movement, Is.SameAs(movement)); Assert.That(player.currentCast, Is.SameAs(cast));
                    Assert.That(player.health, Is.EqualTo(42)); Assert.That(player.Position, Is.EqualTo(position)); Assert.That(player.velocity, Is.EqualTo(velocity));
                    Assert.That(player.InputSession.Equals(ingress), Is.True); Assert.That(player.CurrentInput.Pressed, Is.EqualTo(input.Pressed));
                    Assert.That(player.CurrentInput.Axis, Is.EqualTo(input.Axis)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick)); Assert.That(arena.Now, Is.EqualTo(now));
                    Assert.That(chapter.Profile, Is.SameAs(profile)); Assert.That(chapter.MovementProof, Is.SameAs(proof));
                    Assert.That(chapter.ResolvedLayoutHash, Is.EqualTo(map)); Assert.That(chapter.LoadedModuleCount, Is.EqualTo(loaded)); Assert.That(reset, Is.Zero);
                }
                finally { arena.ResetCompleted -= observed; }
            }
        }
        [UnityTest] public IEnumerator AcceptedCustomDoubleJumpAndQuantizedRunProfilesCommitProofBeforeOneReset()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player; var chapter = Chapter(Forest(arena));
            using (var driver = arena.AcquireManualSimulation())
            {
                var original = arena.Movement; int reset = 0; bool matched = true;
                Action observed = () => { reset++; matched &= chapter.Profile.MovementVersion == arena.Movement.ContentHash; };
                arena.ResetCompleted += observed;
                try
                {
                    var candidate = original.ToData(); candidate.firstJumpVelocity = 12; candidate.secondJumpVelocity = 16; candidate.riseGravity = 30; candidate.fallGravity = 50;
                    Assert.That(arena.TryApplyMovementAndReset(candidate, out var error), Is.True, error);
                    Assert.That(reset, Is.EqualTo(1)); Assert.That(matched, Is.True); Assert.That(chapter.MovementProof.Succeeded, Is.True);
                    candidate = original.ToData(); candidate.runSpeed = 8; candidate.dashSpeed = 10;
                    Assert.That(arena.TryApplyMovementAndReset(candidate, out error), Is.True, error);
                    Assert.That(reset, Is.EqualTo(2)); Assert.That(matched, Is.True);
                    Assert.That(chapter.Profile.WalkSpeed, Is.EqualTo(8));
                    Assert.That(chapter.MovementProof.ProfileHash, Is.EqualTo(chapter.Profile.ContentHash));
                    Assert.That(arena.TryApplyMovementAndReset(original.ToData(), out error), Is.True, error);
                }
                finally { arena.ResetCompleted -= observed; }
            }
        }
        [UnityTest] public IEnumerator PreparedWorldTransactionsNeverPublishUntilCommitAndRejectAStaleProfileOwner()
        {
            var open = Open("Assets/Deviant/Game/Scenes/deviant.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var forest = Forest(arena); var chapter = Chapter(forest); var originalProof = chapter.MovementProof;
            var values = arena.Movement.ToData(); values.runSpeed = 4; Assert.That(LocomotionProfile.TryCreate(values, out var candidate, out _), Is.True);
            Assert.That(Prepare(forest, arena.Player, candidate, out var first, out var error), Is.True, error);
            using (first)
            {
                Assert.That(chapter.MovementProof, Is.SameAs(originalProof));
                Assert.That(Prepare(forest, arena.Player, arena.Movement, out var second, out error), Is.True, error);
                using (second) Assert.That(second.TryCommit(out error), Is.True, error);
                var current = chapter.MovementProof;
                Assert.That(first.TryCommit(out error), Is.False); Assert.That(error, Does.Contain("stale"));
                Assert.That(chapter.MovementProof, Is.SameAs(current));
            }
        }
        sealed class ReentrantValidation : ICombatMovementValidation
        {
            readonly CombatArena arena;
            readonly CombatArena.ManualSimulation driver;
            public bool ResetRejected, StepRejected, ApplyRejected;
            public ReentrantValidation(CombatArena arena, CombatArena.ManualSimulation driver) { this.arena = arena; this.driver = driver; }
            public bool TryPrepare(LocomotionProfile candidate, out ICombatMovementValidationTransaction transaction, out string error)
            {
                transaction = null;
                try { arena.ResetTest(); } catch (InvalidOperationException) { ResetRejected = true; }
                try { driver.Step(.02f); } catch (InvalidOperationException) { StepRejected = true; }
                ApplyRejected = !arena.TryApplyMovementAndReset(candidate.ToData(), out _);
                error = "fixture refuses candidate after checking reentry"; return false;
            }
        }
        [UnityTest] public IEnumerator ColdValidationRejectsReentrantApplyResetAndStepWithoutLeavingSimulationBusy()
        {
            var open = Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var original = arena.Movement;
            using (var driver = arena.AcquireManualSimulation())
            {
                var validation = new ReentrantValidation(arena, driver);
                using (arena.BindMovementValidation(validation))
                {
                    Assert.That(arena.TryApplyMovementAndReset(original.ToData(), out _), Is.False);
                    Assert.That(validation.ResetRejected && validation.StepRejected && validation.ApplyRejected, Is.True);
                    Assert.That(arena.Movement, Is.SameAs(original)); Assert.That(arena.SimulationFaulted, Is.False);
                }
                driver.Step(.02f); Assert.That(arena.SimulationFaulted, Is.False);
                Assert.That(arena.TryApplyMovementAndReset(original.ToData(), out var error), Is.True, error);
            }
        }
        sealed class MissingTransactionValidation : ICombatMovementValidation
        {
            public bool TryPrepare(LocomotionProfile candidate, out ICombatMovementValidationTransaction transaction, out string error)
            { transaction = null; error = null; return true; }
        }
        [UnityTest] public IEnumerator AClaimedSuccessfulValidationWithoutTransactionCannotApplyOrReset()
        {
            var open = Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); driver.Step(.02f, new CombatInput(0, CombatButtons.Basic));
                player.health = 42; player.SubmitInput(new CombatInput(1, CombatButtons.Jump));
                var original = arena.Movement; var cast = player.currentCast; var session = player.InputSession;
                float now = arena.Now; long tick = arena.SimulationTickCount; int resets = 0;
                Action observe = () => resets++; arena.ResetCompleted += observe;
                try
                {
                    using (arena.BindMovementValidation(new MissingTransactionValidation()))
                    {
                        var candidate = original.ToData(); candidate.runSpeed = 4;
                        Assert.That(arena.TryApplyMovementAndReset(candidate, out var error), Is.False);
                        Assert.That(error, Does.Contain("transaction")); Assert.That(arena.Movement, Is.SameAs(original));
                        Assert.That(player.currentCast, Is.SameAs(cast)); Assert.That(player.health, Is.EqualTo(42));
                        Assert.That(player.CurrentInput.Pressed, Is.EqualTo(CombatButtons.Jump)); Assert.That(player.InputSession.Equals(session), Is.True);
                        Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.SimulationTickCount, Is.EqualTo(tick)); Assert.That(resets, Is.Zero);
                    }
                    driver.Step(.02f); Assert.That(arena.SimulationFaulted, Is.False);
                }
                finally { arena.ResetCompleted -= observe; }
            }
        }
        sealed class Terrain : IDisposable
        {
            readonly GameObject root;
            public Terrain(Scene scene, bool blocked, bool ceiling = false)
            {
                root = new GameObject("DashWitnessTerrain"); SceneManager.MoveGameObjectToScene(root, scene);
                var floor = root.AddComponent<BoxCollider2D>(); floor.size = new Vector2(20, 1); floor.offset = new Vector2(5, -.5f); root.AddComponent<ArenaSurface>();
                var body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
                var composite = root.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Manual; floor.compositeOperation = Collider2D.CompositeOperation.Merge;
                if (blocked)
                {
                    var wall = new GameObject("ActualBlockingWall"); wall.transform.SetParent(root.transform, false); wall.transform.localPosition = new Vector3(1.5f, 2, 0);
                    var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.2f, 4); box.compositeOperation = Collider2D.CompositeOperation.Merge;
                }
                if (ceiling)
                {
                    var roof = new GameObject("ActualLowCeiling"); roof.transform.SetParent(root.transform, false); roof.transform.localPosition = new Vector3(2.5f, 1.9f, 0);
                    var box = roof.AddComponent<BoxCollider2D>(); box.size = new Vector2(2, .2f); box.compositeOperation = Collider2D.CompositeOperation.Merge;
                }
                Physics2D.SyncTransforms(); composite.GenerateGeometry();
            }
            public void Dispose() { root.SetActive(false); Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator DashWitnessReachesEndpointThenSettlesAfterNaturalEndButCannotPassBlockedEndpoint()
        {
            var open = Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); while (open.MoveNext()) yield return open.Current;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var profile = UnityChapterModules.ProfileFor(arena.Player);
            var layout = new ChapterLayout("dash-reach-fixture", "1", new LayoutOrigin(LayoutOriginKind.Authored, "fixture", "1", "fixture"), "entry",
                new[] { new LayoutModule("floor", "floor", default) }, new[] { new LayoutAnchor("entry", "floor", new LayoutPoint(0, .03f)) },
                Array.Empty<LayoutConnection>(), witnesses: new[] { new LayoutWitness("actual-dash-reach", new LayoutPoint(0, .03f), 1,
                    LayoutWitnessKind.Dash, new LayoutPoint(3, 0), new LayoutRect(2.95f, -.06f, .1f, .12f)) });
            var proof = new UnityLayoutTraversalProof((scene, ignored) => new Terrain(scene, false));
            Assert.That(proof.TryValidate(layout, profile, null, out var error), Is.True, error);
            Assert.That(proof.LastResult.Succeeded, Is.True);
            var blocked = new UnityLayoutTraversalProof((scene, ignored) => new Terrain(scene, true));
            Assert.That(blocked.TryValidate(layout, profile, null, out error), Is.False);
            Assert.That(error, Does.Contain("actual-dash-reach"));
            Assert.That(blocked.LastResult.Succeeded, Is.False);
            var ceiling = new UnityLayoutTraversalProof((scene, ignored) => new Terrain(scene, false, ceiling: true));
            Assert.That(ceiling.TryValidate(layout, profile, null, out error), Is.False);
            Assert.That(error, Does.Contain("actual-dash-reach"));
            Assert.That(ceiling.LastResult.Succeeded, Is.False);
        }
        static IEnumerator Open(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; yield return null;
        }
    }
}
