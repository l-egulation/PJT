using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Presentation;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SequenceStageActorIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesDedicatedStageViewsWithSequenceLifetimes() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesDedicatedStageViewsWithSequenceLifetimes() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls; public bool Deferred; public Operation Current;
            public bool Validate(string endpoint, out string error) { error = endpoint == "fixture.end" ? null : "unknown endpoint"; return error == null; }
            public ISequenceOperation Begin(Guid execution, string endpoint) { Calls++; return Current = new Operation { Result = Deferred ? SequenceOperationStatus.Running : SequenceOperationStatus.Completed }; }
            public sealed class Operation : ISequenceOperation
            {
                public SequenceOperationStatus Result;
                public SequenceOperationStatus Status => Result;
                public string Error => null;
                public void Poll(double elapsed) { }
                public void Dispose() { if (Result == SequenceOperationStatus.Running) Result = SequenceOperationStatus.Cancelled; }
            }
        }
        static StagePose Baseline(float x = 50) => new StagePose(new StagePoint(x, 800), 0, 1, false);
        static SequenceDefinition Definition(params SequenceStep[] steps) => new SequenceDefinition("stage-sequence-fixture", steps, new[] { "entry", "goal", "focus" }, new SequenceSkipDefinition("fixture.end", 3));
        static void AssertReturned(CombatArena arena, StageActorOutputBinding a, StageActorOutputBinding b, CuePlayer cues, FadeOutputBinding fade)
        {
            Assert.That(a.IsOwned || b.IsOwned, Is.False); Assert.That(cues.ActiveCount, Is.Zero); Assert.That(fade.RequestCount, Is.Zero); Assert.That(arena.CameraRequests.ActiveCount, Is.Zero);
            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("StageChapterSources"); sources.SetActive(false);
            var module = new GameObject("ModuleSource"); module.transform.SetParent(sources.transform, false);
            var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("NpcMarkerSource"); marker.transform.SetParent(sources.transform, false);
            var worldRoot = new GameObject("StageChapterFixture"); worldRoot.transform.position = new Vector3(0, 1000, 0); var world = worldRoot.AddComponent<ChapterWorldHost>();
            var ownerRoot = new GameObject("StageSequenceOwner"); var host = ownerRoot.AddComponent<SequenceHost>(); var a = ownerRoot.AddComponent<StageActorOutputBinding>();
            var otherOwner = new GameObject("OtherStageOutputOwner"); var b = otherOwner.AddComponent<StageActorOutputBinding>();
            var canvas = SequenceIntegrationTests.CreateView(out var view, out var next, out _);
            var imageObject = new GameObject("StageFadeImage", typeof(RectTransform), typeof(Image)); imageObject.transform.SetParent(canvas.transform, false);
            var image = imageObject.GetComponent<Image>(); image.raycastTarget = false; var fade = canvas.AddComponent<FadeOutputBinding>(); fade.Configure(image, default);
            var texture = new Texture2D(2, 2); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 32);
            var actorA = new GameObject("StageSpriteA", typeof(SpriteRenderer)); var rendererA = actorA.GetComponent<SpriteRenderer>(); rendererA.sprite = sprite;
            var actorB = new GameObject("StageSpriteB", typeof(SpriteRenderer)); var rendererB = actorB.GetComponent<SpriteRenderer>(); rendererB.sprite = sprite;
            a.Configure(rendererA, Baseline()); b.Configure(rendererB, Baseline(60)); var baseline = a.BindBaseSource("stage baseline");
            var factory = new SpriteCueFactory(); var cues = new CuePlayer(factory);
            var cue = new PresentationCueDefinition("stage-cue-fixture", new[] { "fixture.frame" }, new[] { .25f }, new[] { 0f }, new[] { 0f });
            var cueCatalog = new SequenceCueCatalog(factory); cueCatalog.Publish(SequenceCueMedia.Sprite(cue, new[] { sprite }));
            var go = new StageMoveDefinition("fixture-go", .5, 1, 0); var back = new StageMoveDefinition("fixture-back", .5, -1, 0); var endpoint = new Endpoint();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    var authored = ChapterFixtures.Good(); var captured = new ChapterFixtures.CapturedProvider();
                    foreach (var provider in new IChapterLayoutProvider[] { new AuthoredLayoutProvider(authored), captured })
                    {
                        driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                        Assert.That(world.TryOpen(provider, new LayoutRequest(authored.Id, authored.Version, 17), UnityChapterModules.ProfileFor(player),
                            new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
                        var session = world.Session; Assert.That(session.TryLoadModule("east", out error), Is.True, error);
                        Assert.That(world.TryGetWorldAnchor(session.Id, "focus", out var focus, out _), Is.True); player.Position = focus + Vector2.right; player.velocity = Vector2.zero;
                        var originalPosition = player.Position; float originalHealth = player.health, originalTime = arena.Now; int originalHits = player.ReceivedHitCount;
                        var npc = new NpcDefinition("stage-npc-fixture", DialogueFixtures.Definition(), "npc");
                        var targets = new ChapterNpcTargets(world, session.Id, new[] { new NpcPlacementBinding("marker-east", "focus", npc) }, () => player.Position, () => arena.CombatActive);
                        var bindings = new ChapterSequenceBindings(world, session.Id, targets, new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true),
                            view, new DialogueFixtures.Text(), arena.Controls, new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, false), cues, cueCatalog, arena.CameraRequests,
                            new[] { new SequenceDialogueBinding("npc", "marker-east") }, new[] { new SequenceCueBinding("sprite", "focus", cue.Id, 1) },
                            new[] { new SequenceCameraBinding("focus", "focus", new CameraPosition(1, 2, -10), 10) }, endpoint, fade.Requests,
                            new[] { new SequenceFadeBinding("fade", new FadeDefinition("stage-fade", 0, 0, 0, 0, 1, .5, true), 10) },
                            new[] { new SequenceStageActorBinding("go", "a", "entry", "goal", go), new SequenceStageActorBinding("back", "a", "goal", "entry", back),
                                new SequenceStageActorBinding("go-b", "b", "entry", "goal", go), new SequenceStageActorBinding("alias-go", "alias", "entry", "goal", go),
                                new SequenceStageActorBinding("no-view", "missing", "entry", "goal", go) },
                            new Dictionary<string, IStageActorRequests> { { "a", a.Requests }, { "b", b.Requests }, { "alias", a.Requests } });
                        var clock = new Clock(); var policy = new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
                        using (var modal = arena.Controls.OpenScope("external stage modal")) using (var foreign = b.OpenScope("foreign stage owner"))
                        {
                            var block = modal.BlockInput(InputChannels.Navigation); var pause = modal.PauseSimulation();
                            var other = foreign.Play(new StageMoveDefinition("other", 1, 1, 0), new StagePoint(60, 1000), new StagePoint(70, 1000)); other.Poll(.25); b.ApplyNow();
                            Assert.That(host.TryStart(Definition(SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.StageMove("go", "go", 2), SequenceStep.Wait("hold", .25),
                                SequenceStep.StageMove("back", "back", 2), SequenceStep.Wait("settle", .125)), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            Assert.That(a.IsOwned, Is.False); SequenceIntegrationTests.FinishDialogue(host, next, view); a.ApplyNow(); Assert.That(actorA.transform.position, Is.EqualTo(new Vector3(1, 1000, 0)));
                            clock.Time = .25; host.Refresh(); a.ApplyNow(); Assert.That(actorA.transform.position.x, Is.EqualTo(8)); Assert.That(rendererA.enabled, Is.True);
                            baseline.Submit(Baseline(55)); clock.Time = .5; host.Refresh(); a.ApplyNow(); Assert.That(host.Current.Read().StepId, Is.EqualTo("hold")); Assert.That(actorA.transform.position.x, Is.EqualTo(15));
                            clock.Time = .75; host.Refresh(); clock.Time = 1; host.Refresh(); a.ApplyNow(); Assert.That(actorA.transform.position.x, Is.EqualTo(8)); Assert.That(rendererA.flipX, Is.True);
                            clock.Time = 1.25; host.Refresh(); clock.Time = 1.375; host.Refresh(); a.ApplyNow(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                            Assert.That(a.IsOwned || rendererA.enabled, Is.False); Assert.That(actorA.transform.position, Is.EqualTo(new Vector3(55, 800, 0)));
                            b.ApplyNow(); Assert.That(actorB.transform.position.x, Is.EqualTo(62.5f)); Assert.That(other.IsRetained, Is.True); Assert.That(block.IsActive && pause.IsActive, Is.True);
                            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
                        }
                        b.ApplyNow(); AssertReturned(arena, a, b, cues, fade);
                        Assert.That(player.Position, Is.EqualTo(originalPosition)); Assert.That(player.health, Is.EqualTo(originalHealth)); Assert.That(player.ReceivedHitCount, Is.EqualTo(originalHits)); Assert.That(arena.Now, Is.EqualTo(originalTime));

                        var held = Definition(SequenceStep.StageMove("go", "go", 2), SequenceStep.Wait("hold", 10), SequenceStep.StageMove("back", "back", 2));
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .25; host.Refresh();
                        Assert.That(host.Current.TryCancel(host.Current.Read().ExecutionId, out _), Is.True); a.ApplyNow(); Assert.That(rendererA.enabled, Is.False); AssertReturned(arena, a, b, cues, fade);
                        endpoint.Deferred = true; int calls = endpoint.Calls;
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh();
                        var skipped = host.Current; Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out error), Is.True, error); Assert.That(a.IsOwned, Is.True);
                        endpoint.Current.Result = SequenceOperationStatus.Completed; host.Refresh(); a.ApplyNow(); Assert.That(skipped.Read().Status, Is.EqualTo(SequenceStatus.Skipped));
                        Assert.That(endpoint.Calls, Is.EqualTo(calls + 1)); Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out _), Is.False); AssertReturned(arena, a, b, cues, fade); endpoint.Deferred = false;

                        Assert.That(host.TryStart(Definition(SequenceStep.StageMove("timeout", "go", .25)), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        clock.Time += .25; host.Refresh(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut)); AssertReturned(arena, a, b, cues, fade);
                        long revision = arena.Controls.Current.Revision;
                        foreach (var bad in new[] {
                            Definition(SequenceStep.StageMove("missing", "unknown", 2)), Definition(SequenceStep.StageMove("missing", "no-view", 2)),
                            Definition(SequenceStep.ParallelAll("same-view", SequenceStep.StageMove("a", "go", 2), SequenceStep.StageMove("b", "back", 2))),
                            Definition(SequenceStep.ParallelAll("alias", SequenceStep.StageMove("a", "go", 2), SequenceStep.StageMove("b", "alias-go", 2))),
                            new SequenceDefinition("undeclared-stage-anchors", new[] { SequenceStep.StageMove("go", "go", 2) }, Array.Empty<string>()) })
                            Assert.That(host.TryStart(bad, bindings, arena.Controls, policy, clock, out _), Is.False);
                        Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); AssertReturned(arena, a, b, cues, fade);

                        using (var external = a.OpenScope("preflight competitor"))
                        {
                            var move = external.Play(go, default, new StagePoint(10, 0));
                            Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out _), Is.False); Assert.That(external.IsActive && move.IsRetained, Is.True);
                        }
                        Assert.That(host.TryStart(Definition(SequenceStep.Wait("before", .5), SequenceStep.StageMove("go", "go", 2)), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        using (var external = a.OpenScope("late competitor"))
                        {
                            var move = external.Play(go, default, new StagePoint(10, 0)); clock.Time += .5; host.Refresh();
                            Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(external.IsActive && move.IsRetained, Is.True); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None));
                        }

                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .25; host.Refresh(); a.ApplyNow();
                        var previous = actorA.transform.position; var collider = actorA.AddComponent<BoxCollider2D>(); host.Refresh(); a.ApplyNow();
                        Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); Assert.That(actorA.transform.position, Is.EqualTo(previous)); AssertReturned(arena, a, b, cues, fade);
                        Object.Destroy(collider); yield return null; a.Configure(rendererA, Baseline(55)); baseline.Dispose(); baseline = a.BindBaseSource("baseline after safety rebind");

                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh();
                        a.enabled = false; a.enabled = true; host.Refresh(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); AssertReturned(arena, a, b, cues, fade);
                        actorA.SetActive(false);
                        Assert.That(host.TryStart(Definition(SequenceStep.Wait("legacy", 1)), bindings, arena.Controls, policy, clock, out error), Is.True, error); host.Close();
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out _), Is.False); actorA.SetActive(true); a.ApplyNow();

                        var parallel = Definition(SequenceStep.ParallelAll("together", SequenceStep.StageMove("a", "go", 2), SequenceStep.StageMove("b", "go-b", 2),
                            SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("camera", "focus", 1), SequenceStep.Cue("effect", "sprite", 5), SequenceStep.Fade("fade", "fade", 2)), SequenceStep.Wait("hold", 1));
                        Assert.That(host.TryStart(parallel, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .25; host.Refresh(); a.ApplyNow(); b.ApplyNow(); fade.ApplyNow();
                        Assert.That(actorA.transform.position.x, Is.EqualTo(8)); Assert.That(actorB.transform.position.x, Is.EqualTo(8)); Assert.That(image.color.a, Is.EqualTo(.5f));
                        SequenceIntegrationTests.FinishDialogue(host, next, view); clock.Time += .25; host.Refresh(); a.ApplyNow(); b.ApplyNow(); Assert.That(actorA.transform.position.x, Is.EqualTo(15));
                        Assert.That(host.Current.Read().StepId, Is.EqualTo("together")); host.Close(); a.ApplyNow(); b.ApplyNow(); fade.ApplyNow(); AssertReturned(arena, a, b, cues, fade);
                        Assert.That(rendererA.enabled || rendererB.enabled, Is.False); Assert.That(player.Position, Is.EqualTo(originalPosition));
                        Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh(); world.Close(); host.Refresh();
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); a.ApplyNow(); AssertReturned(arena, a, b, cues, fade);
                    }
                    Assert.That(captured.Calls, Is.EqualTo(1));
                }
            }
            finally
            {
                host.Close(); world.Close(); cues.Dispose(); factory.Dispose(); baseline.Dispose();
                Object.Destroy(ownerRoot); Object.Destroy(otherOwner); Object.Destroy(worldRoot); Object.Destroy(sources); Object.Destroy(canvas); Object.Destroy(actorA); Object.Destroy(actorB); Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
    }
}
