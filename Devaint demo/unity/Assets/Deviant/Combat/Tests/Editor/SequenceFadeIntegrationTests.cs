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
    public class SequenceFadeIntegrationTests
    {
        [UnityTest] public IEnumerator GameCoordinatesFadeWithDialogueCameraAndControl() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCoordinatesFadeWithDialogueCameraAndControl() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls;
            public bool Deferred;
            public Operation Current;
            readonly HashSet<Guid> executions = new HashSet<Guid>();
            public bool Validate(string endpoint, out string error) { error = endpoint == "fixture.end" ? null : "unknown fixture endpoint"; return error == null; }
            public ISequenceOperation Begin(Guid execution, string endpoint)
            {
                Assert.That(executions.Add(execution), Is.True); Calls++;
                return Current = new Operation { Result = Deferred ? SequenceOperationStatus.Running : SequenceOperationStatus.Completed };
            }
            public sealed class Operation : ISequenceOperation
            {
                public SequenceOperationStatus Result;
                public SequenceOperationStatus Status => Result;
                public string Error => null;
                public void Poll(double elapsed) { }
                public void Dispose() { if (Result == SequenceOperationStatus.Running) Result = SequenceOperationStatus.Cancelled; }
            }
        }
        static SequenceDefinition Definition(params SequenceStep[] steps) => new SequenceDefinition("fade-sequence-fixture", steps, new[] { "focus" }, new SequenceSkipDefinition("fixture.end", 3));
        static FadeFrame Baseline(float opacity) => new FadeFrame(.1f, .2f, .3f, opacity);
        static void AssertReturned(CombatArena arena, FadeOutputBinding output, CuePlayer cues)
        {
            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(arena.CameraRequests.ActiveCount, Is.Zero); Assert.That(output.RequestCount, Is.Zero); Assert.That(cues.ActiveCount, Is.Zero);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("FadeChapterSources"); sources.SetActive(false);
            var module = new GameObject("ModuleSource"); module.transform.SetParent(sources.transform, false);
            var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("NpcMarkerSource"); marker.transform.SetParent(sources.transform, false);
            var worldRoot = new GameObject("FadeChapterFixture"); worldRoot.transform.position = new Vector3(0, 1000, 0); var world = worldRoot.AddComponent<ChapterWorldHost>();
            var ownerRoot = new GameObject("FadeSequenceComposition"); var host = ownerRoot.AddComponent<SequenceHost>();
            var canvas = SequenceIntegrationTests.CreateView(out var view, out var next, out _);
            var imageRoot = new GameObject("SequenceFadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); imageRoot.transform.SetParent(canvas.transform, false);
            var image = imageRoot.GetComponent<Image>(); image.raycastTarget = false;
            var output = canvas.AddComponent<FadeOutputBinding>(); output.Configure(image, Baseline(.125f));
            var baseline = output.BindBaseSource("fixture baseline");
            var texture = new Texture2D(2, 2); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 32);
            var factory = new SpriteCueFactory(); var cues = new CuePlayer(factory);
            var cue = new PresentationCueDefinition("fade-cue-fixture", new[] { "fixture.frame" }, new[] { .25f }, new[] { 0f }, new[] { 0f });
            var cueCatalog = new SequenceCueCatalog(factory); cueCatalog.Publish(SequenceCueMedia.Sprite(cue, new[] { sprite }));
            var fadeOut = new FadeDefinition("fixture-out", .25f, .5f, .75f, 0, 1, .5, true);
            var fadeIn = new FadeDefinition("fixture-in", .25f, .5f, .75f, 1, 0, .5, false);
            var endpoint = new Endpoint();
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
                        Assert.That(world.TryGetWorldAnchor(session.Id, "focus", out var point, out _), Is.True); player.Position = point + Vector2.right; player.velocity = Vector2.zero;
                        var npc = new NpcDefinition("fade-npc-fixture", DialogueFixtures.Definition(), "npc");
                        var targets = new ChapterNpcTargets(world, session.Id, new[] { new NpcPlacementBinding("marker-east", "focus", npc) }, () => player.Position, () => arena.CombatActive);
                        var bindings = new ChapterSequenceBindings(world, session.Id, targets, new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true),
                            view, new DialogueFixtures.Text(), arena.Controls, new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, false), cues, cueCatalog, arena.CameraRequests,
                            new[] { new SequenceDialogueBinding("npc", "marker-east") }, new[] { new SequenceCueBinding("sprite", "focus", cue.Id, 1) },
                            new[] { new SequenceCameraBinding("focus", "focus", new CameraPosition(1, 2, -10), 10) }, endpoint, output.Requests,
                            new[] { new SequenceFadeBinding("out", fadeOut, 20), new SequenceFadeBinding("in", fadeIn, 20) });
                        var clock = new Clock(); var policy = new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
                        float simulationTime = arena.Now; int hits = player.ReceivedHitCount;
                        using (var modal = arena.Controls.OpenScope("external fade modal")) using (var foreign = output.OpenScope("external fade owner"))
                        {
                            var block = modal.BlockInput(InputChannels.Navigation | InputChannels.Combat); var pause = modal.PauseSimulation();
                            var other = foreign.Play(new FadeDefinition("fixture-other", .75f, .25f, .125f, .75f, .25f, 1, true), 5); other.Poll(.25);
                            var definition = Definition(SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("camera", "focus", .25),
                                SequenceStep.Fade("out", "out", 2), SequenceStep.Wait("hold", .5), SequenceStep.Fade("in", "in", 2), SequenceStep.Wait("settle", .125));
                            Assert.That(host.TryStart(definition, bindings, arena.Controls, policy, clock, out error), Is.True, error); SequenceIntegrationTests.FinishDialogue(host, next, view);
                            clock.Time = .25; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("out")); output.ApplyNow(); Assert.That(image.color.a, Is.Zero);
                            clock.Time = .5; host.Refresh(); output.ApplyNow(); Assert.That(image.color, Is.EqualTo(new Color(.25f, .5f, .75f, .5f)));
                            clock.Time = .75; host.Refresh(); output.ApplyNow(); Assert.That(host.Current.Read().StepId, Is.EqualTo("hold")); Assert.That(image.color.a, Is.EqualTo(1));
                            Assert.That(output.RequestCount, Is.EqualTo(2)); other.Poll(.5); baseline.Submit(Baseline(.375f));
                            clock.Time = 1.25; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("in")); Assert.That(output.RequestCount, Is.EqualTo(2));
                            clock.Time = 1.5; host.Refresh(); output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(.5f));
                            clock.Time = 1.75; host.Refresh(); output.ApplyNow(); Assert.That(host.Current.Read().StepId, Is.EqualTo("settle"));
                            Assert.That(image.color, Is.EqualTo(new Color(.75f, .25f, .125f, .5f)), "The earlier held out fade must not reappear after in finishes.");
                            clock.Time = 1.875; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                            Assert.That(output.RequestCount, Is.EqualTo(1)); Assert.That(other.IsRetained, Is.True); Assert.That(block.IsActive && pause.IsActive, Is.True);
                            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation | InputChannels.Combat));
                        }
                        output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(.375f)); AssertReturned(arena, output, cues);
                        Assert.That(arena.Now, Is.EqualTo(simulationTime)); Assert.That(player.ReceivedHitCount, Is.EqualTo(hits)); Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));

                        var held = Definition(SequenceStep.Fade("out", "out", 2), SequenceStep.Wait("hold", 10), SequenceStep.Fade("in", "in", 2));
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        clock.Time += .25; host.Refresh(); output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(.5f));
                        Assert.That(host.Current.TryCancel(host.Current.Read().ExecutionId, out error), Is.True, error); output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(.375f)); AssertReturned(arena, output, cues);

                        endpoint.Deferred = true; int calls = endpoint.Calls;
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        clock.Time += .5; host.Refresh(); output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(1));
                        var skipped = host.Current; Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out error), Is.True, error);
                        Assert.That(skipped.Read().IsSkipping, Is.True); Assert.That(skipped.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(output.RequestCount, Is.EqualTo(1));
                        endpoint.Current.Result = SequenceOperationStatus.Completed; host.Refresh(); output.ApplyNow();
                        Assert.That(skipped.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); Assert.That(endpoint.Calls, Is.EqualTo(calls + 1)); Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out _), Is.False);
                        Assert.That(image.color.a, Is.EqualTo(.375f)); AssertReturned(arena, output, cues); endpoint.Deferred = false;

                        Assert.That(host.TryStart(Definition(SequenceStep.Fade("out", "out", .25)), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        clock.Time += .25; host.Refresh(); output.ApplyNow(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut)); AssertReturned(arena, output, cues);

                        long revision = arena.Controls.Current.Revision;
                        Assert.That(host.TryStart(Definition(SequenceStep.Fade("missing", "unknown", 2)), bindings, arena.Controls, policy, clock, out _), Is.False);
                        Assert.That(host.TryStart(Definition(SequenceStep.ParallelAll("together", SequenceStep.Fade("a", "out", 2), SequenceStep.Fade("b", "in", 2))), bindings, arena.Controls, policy, clock, out error), Is.False);
                        Assert.That(error, Does.Contain("one fade channel")); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); AssertReturned(arena, output, cues);

                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh();
                        image.enabled = false; host.Refresh(); output.ApplyNow(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); AssertReturned(arena, output, cues);
                        revision = arena.Controls.Current.Revision;
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out _), Is.False); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision));
                        Assert.That(host.TryStart(Definition(SequenceStep.Wait("legacy", 1)), bindings, arena.Controls, policy, clock, out error), Is.True, error); host.Close();
                        image.enabled = true; output.ApplyNow();

                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh();
                        var oldFadeSession = output.SessionId; output.enabled = false; output.enabled = true;
                        Assert.That(output.IsAvailable, Is.True); Assert.That(output.SessionId, Is.Not.EqualTo(oldFadeSession)); host.Refresh(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); AssertReturned(arena, output, cues);
                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); output.Configure(image, Baseline(.375f)); host.Refresh();
                        Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); AssertReturned(arena, output, cues);
                        baseline = output.BindBaseSource("fixture baseline after rebind");

                        var parallel = Definition(SequenceStep.ParallelAll("together", SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("camera", "focus", 1),
                            SequenceStep.Cue("effect", "sprite", 5), SequenceStep.Fade("out", "out", 2)), SequenceStep.Wait("hold", .5), SequenceStep.Fade("in", "in", 2));
                        Assert.That(host.TryStart(parallel, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .25; host.Refresh(); output.ApplyNow();
                        Assert.That(image.color.a, Is.EqualTo(.5f)); Assert.That(factory.LiveViewCount, Is.Zero); SequenceIntegrationTests.FinishDialogue(host, next, view);
                        clock.Time += .25; host.Refresh(); output.ApplyNow(); Assert.That(image.color.a, Is.EqualTo(1)); Assert.That(host.Current.Read().StepId, Is.EqualTo("together"));
                        clock.Time += .5; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("hold")); host.Close(); output.ApplyNow(); AssertReturned(arena, output, cues);
                        Assert.That(image.raycastTarget, Is.False); Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));

                        Assert.That(host.TryStart(held, bindings, arena.Controls, policy, clock, out error), Is.True, error); clock.Time += .5; host.Refresh(); world.Close(); host.Refresh();
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); output.ApplyNow(); AssertReturned(arena, output, cues);
                    }
                    Assert.That(captured.Calls, Is.EqualTo(1));
                }
            }
            finally
            {
                host.Close(); world.Close(); cues.Dispose(); factory.Dispose(); baseline.Dispose();
                Object.Destroy(ownerRoot); Object.Destroy(worldRoot); Object.Destroy(sources); Object.Destroy(canvas); Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
    }
}
