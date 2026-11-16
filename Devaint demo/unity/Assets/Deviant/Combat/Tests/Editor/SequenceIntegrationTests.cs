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
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using Deviant.UI.Unity;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SequenceIntegrationTests
    {
        [UnityTest] public IEnumerator GameCoordinatesSequenceHandles() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCoordinatesSequenceHandles() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GameCoordinatesParallelSequenceHandles() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabCoordinatesParallelSequenceHandles() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", true);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        sealed class Clock : ISequenceClock { public double Time; public Action OnRead; public double ReadTime() { OnRead?.Invoke(); return Time; } }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls;
            public Action OnBegin;
            readonly HashSet<Guid> executions = new HashSet<Guid>();
            public bool Validate(string endpoint, out string error) { error = endpoint == "fixture.end" ? null : "unknown endpoint"; return error == null; }
            public ISequenceOperation Begin(Guid execution, string endpoint)
            {
                Assert.That(endpoint, Is.EqualTo("fixture.end")); Assert.That(executions.Add(execution), Is.True, "Endpoint is called once per execution."); Calls++; OnBegin?.Invoke(); return new EndOperation();
            }
            sealed class EndOperation : ISequenceOperation
            {
                public SequenceOperationStatus Status => SequenceOperationStatus.Completed;
                public string Error => null;
                public void Poll(double elapsed) { }
                public void Dispose() { }
            }
        }
        sealed class ViewBinding : IDialogueView
        {
            readonly DialogueView view;
            public bool ThrowUnbind;
            public ViewBinding(DialogueView view) { this.view = view; }
            public event Action<DialogueIntent> Input { add { view.Input += value; } remove { view.Input -= value; } }
            public bool TryBind(Guid binding, out string error) => view.TryBind(binding, out error);
            public bool TryApply(DialogueDisplay display) => view.TryApply(display);
            public void Unbind(Guid binding) { view.Unbind(binding); if (ThrowUnbind) throw new InvalidOperationException("fixture dialogue cleanup failed"); }
        }
        sealed class Media : ICueCatalog
        {
            readonly ICueCatalog catalog;
            ICueViewFactory pendingFactory;
            Action<CueViewResult> pending;
            PresentationCueDefinition definition;
            CueAnchor anchor;
            public bool DelayNext, FailNext;
            public string DelayCueId;
            public readonly List<int> LateFrames = new List<int>();
            public bool TicketReturned;
            public Media(ICueCatalog catalog) { this.catalog = catalog; }
            public ICueCatalogSnapshot Capture(IReadOnlyList<string> cueIds) => new Snapshot(this, catalog.Capture(cueIds));
            IDisposable Begin(ICueViewFactory factory, ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                if (FailNext) { FailNext = false; resolved(CueViewResult.Failure("fixture missing media")); return null; }
                if (!DelayNext || DelayCueId != null && definition.Id != DelayCueId) return factory.Begin(definition, anchor, resolved);
                DelayNext = false; DelayCueId = null; LateFrames.Clear(); this.definition = (PresentationCueDefinition)definition; this.anchor = anchor; pending = resolved; pendingFactory = factory; TicketReturned = false; return new Ticket(this);
            }
            public void ResolveLate()
            {
                var callback = pending; pending = null;
                pendingFactory.Begin(definition, anchor, result => callback(result.View == null ? result : CueViewResult.Ready(new RecordingView(result.View, definition, LateFrames))));
            }
            sealed class Snapshot : ICueCatalogSnapshot
            {
                readonly Media owner;
                readonly ICueCatalogSnapshot captured;
                public Guid Version => captured.Version;
                public Snapshot(Media owner, ICueCatalogSnapshot captured) { this.owner = owner; this.captured = captured; }
                public ICueDefinition Read(string cueId) => captured.Read(cueId);
                public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved) => owner.Begin(captured, definition, anchor, resolved);
            }
            sealed class RecordingView : ICueView
            {
                readonly ICueView view;
                readonly PresentationCueDefinition definition;
                readonly List<int> frames;
                public RecordingView(ICueView view, PresentationCueDefinition definition, List<int> frames) { this.view = view; this.definition = definition; this.frames = frames; }
                public void Sample(float elapsed) { frames.Add(definition.FrameAt(elapsed)); view.Sample(elapsed); }
                public void Dispose() { view.Dispose(); }
            }
            sealed class Ticket : IDisposable
            {
                readonly Media media;
                public Ticket(Media media) { this.media = media; }
                public void Dispose() { media.TicketReturned = true; }
            }
        }
        static SequenceDefinition Definition(bool skip = true, string[] anchors = null) => new SequenceDefinition("sequence-fixture", new[] {
            SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("camera", "focus", .5),
            SequenceStep.Cue("effect", "sprite", 5), SequenceStep.Wait("settle", .125)
        }, anchors ?? new[] { "focus" }, skip ? new SequenceSkipDefinition("fixture.end", 3) : null);
        static SequenceDefinition ParallelDefinition() => new SequenceDefinition("parallel-sequence-fixture", new[] {
            SequenceStep.ParallelAll("together", SequenceStep.Dialogue("talk", "npc", 10), SequenceStep.Camera("camera", "focus", 1),
                SequenceStep.Cue("effect", "sprite", 5), SequenceStep.Wait("hold", .25)), SequenceStep.Wait("settle", .125)
        }, new[] { "focus" }, new SequenceSkipDefinition("fixture.end", 3));
        static SequenceStepSnapshot Child(SequenceSnapshot snapshot, string id) => snapshot.Children.Single(c => c.Id == id);
        static IEnumerator Exercise(string scene, bool parallel = false)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("SequenceSources"); sources.SetActive(false);
            var module = new GameObject("ModuleSource"); module.transform.SetParent(sources.transform, false);
            var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("NpcMarkerSource"); marker.transform.SetParent(sources.transform, false);
            var root = new GameObject("SequenceChapterFixture"); root.transform.position = new Vector3(0, 1000, 0); var world = root.AddComponent<ChapterWorldHost>();
            var owner = new GameObject("SequenceComposition"); var host = owner.AddComponent<SequenceHost>();
            var canvas = CreateView(out var view, out var next, out _); var viewBinding = new ViewBinding(view);
            var texture = new Texture2D(2, 2); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 32);
            var factory = new SpriteCueFactory(); var catalog = new SequenceCueCatalog(factory); var media = new Media(catalog); var cues = new CuePlayer(factory);
            var cue = new PresentationCueDefinition("sequence-sprite-fixture", new[] { "fixture.frame" }, new[] { .25f }, new[] { 0f }, new[] { 0f });
            var lateCue = new PresentationCueDefinition("sequence-late-sprite-fixture", new[] { "fixture.a", "fixture.b" }, new[] { .125f, .125f }, new[] { 0f, 0f }, new[] { 0f, 0f });
            catalog.Publish(SequenceCueMedia.Sprite(cue, new[] { sprite }), SequenceCueMedia.Sprite(lateCue, new[] { sprite, sprite }));
            var endpoint = new Endpoint(); var output = Camera.main.GetComponent<CameraOutputBinding>();
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
                        var npc = new NpcDefinition("sequence-npc-fixture", DialogueFixtures.Definition(), "npc");
                        var targets = new ChapterNpcTargets(world, session.Id, new[] { new NpcPlacementBinding("marker-east", "focus", npc) }, () => player.Position, () => arena.CombatActive);
                        ChapterSequenceBindings Bind() => new ChapterSequenceBindings(world, session.Id, targets, new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true),
                            viewBinding, new DialogueFixtures.Text(), arena.Controls, new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, false), cues, media, arena.CameraRequests,
                            new[] { new SequenceDialogueBinding("npc", "marker-east") }, new[] { new SequenceCueBinding("sprite", "focus", cue.Id, -1), new SequenceCueBinding("late", "focus", lateCue.Id, 1) },
                            new[] { new SequenceCameraBinding("focus", "focus", new CameraPosition(1, 2, -10), 20) }, endpoint);
                        var bindings = Bind();
                        var clock = new Clock(); var policy = new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
                        float simulationTime = arena.Now; int hits = player.ReceivedHitCount;
                        if (parallel)
                        {
                            using (var modal = arena.Controls.OpenScope("parallel external modal")) using (var cameraOwner = arena.CameraRequests.OpenScope("parallel external camera"))
                            {
                                var block = modal.BlockInput(InputChannels.Navigation | InputChannels.Combat); var pause = modal.PauseSimulation();
                                var otherCamera = cameraOwner.Request(1, new FixedCameraPosition(new CameraPosition(77, 78, -10)));
                                Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                                var initial = host.Current.Read(); Assert.That(initial.Children.All(c => c.Status == SequenceStepStatus.Running), Is.True);
                                Assert.That(factory.LiveViewCount, Is.EqualTo(1)); Assert.That(view.Displayed.Token.LineId, Is.EqualTo("a"));
                                output.ApplyNow(); Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(13, 1003, -10)));
                                clock.Time = .25; host.Refresh(); Assert.That(factory.LiveViewCount, Is.Zero);
                                Assert.That(Child(host.Current.Read(), "effect").Status, Is.EqualTo(SequenceStepStatus.Completed));
                                Assert.That(Child(initial, "effect").Status, Is.EqualTo(SequenceStepStatus.Running), "Previous child snapshots are immutable.");
                                Assert.That(view.Displayed.Token.LineId, Is.EqualTo("a")); FinishDialogue(host, next, view);
                                Assert.That(host.Current.Read().StepId, Is.EqualTo("together")); Assert.That(Child(host.Current.Read(), "talk").Status, Is.EqualTo(SequenceStepStatus.Completed));
                                Assert.That(view.Displayed, Is.Null); Assert.That(arena.CameraRequests.ActiveCount, Is.EqualTo(2)); Assert.That(player.InputBlocked, Is.True);
                                clock.Time = 1; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("settle"));
                                output.ApplyNow(); Assert.That(Camera.main.transform.position.x, Is.EqualTo(77));
                                clock.Time = 1.125; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                                Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(otherCamera.Status, Is.EqualTo(CameraRequestStatus.Active));
                                Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation | InputChannels.Combat));
                            }
                            AssertReturned(arena, cues, factory);
                            Assert.That(arena.Now, Is.EqualTo(simulationTime)); Assert.That(player.ReceivedHitCount, Is.EqualTo(hits));
                            Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present));

                            // Same Cue definition in two concurrent leaves has distinct occurrences and two real views.
                            var twoCues = new SequenceDefinition("parallel-two-cues", new[] { SequenceStep.ParallelAll("together",
                                SequenceStep.Cue("a", "sprite", 5), SequenceStep.Cue("b", "sprite", 5), SequenceStep.Wait("hold", 1)) }, new[] { "focus" });
                            Assert.That(host.TryStart(twoCues, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            Assert.That(factory.LiveViewCount, Is.EqualTo(2)); Assert.That(cues.ActiveCount, Is.EqualTo(2));
                            clock.Time += .25; host.Refresh(); Assert.That(factory.LiveViewCount, Is.Zero); Assert.That(cues.ActiveCount, Is.Zero);
                            Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(Child(host.Current.Read(), "a").Status, Is.EqualTo(SequenceStepStatus.Completed));
                            host.Close(); AssertReturned(arena, cues, factory);

                            // A ready peer must not sample delayed media with that peer's previous step time.
                            foreach (double arrival in new[] { .125, .25 })
                            {
                                var lateDefinition = new SequenceDefinition("parallel-delayed-cue", new[] { SequenceStep.ParallelAll("together",
                                    SequenceStep.Cue("ready", "sprite", 5), SequenceStep.Cue("late", "late", 5)) }, new[] { "focus" });
                                media.DelayNext = true; media.DelayCueId = lateCue.Id; double started = clock.Time;
                                Assert.That(host.TryStart(lateDefinition, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                                clock.Time = started + arrival; media.ResolveLate(); Assert.That(media.LateFrames, Is.Empty); host.Refresh();
                                if (arrival == .25)
                                {
                                    Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(host.Current.Read().Error, Does.Contain("not shown before"));
                                    Assert.That(media.LateFrames, Is.Empty); AssertReturned(arena, cues, factory);
                                }
                                else
                                {
                                    Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(media.LateFrames, Is.EqualTo(new[] { 1 }));
                                    host.Close(); AssertReturned(arena, cues, factory);
                                }
                            }

                            media.FailNext = true;
                            Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.False);
                            Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(error, Does.Contain("effect"));
                            Assert.That(Child(host.Current.Read(), "talk").Status, Is.EqualTo(SequenceStepStatus.Cancelled));
                            Assert.That(Child(host.Current.Read(), "camera").Status, Is.EqualTo(SequenceStepStatus.Cancelled));
                            Assert.That(Child(host.Current.Read(), "hold").Status, Is.EqualTo(SequenceStepStatus.Pending)); AssertReturned(arena, cues, factory);

                            Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            Assert.That(session.TryUnloadModule("east", out error), Is.True, error); host.Refresh();
                            Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); Assert.That(host.Current.Read().Error, Does.Contain("talk").And.Contain("TargetLost")); AssertReturned(arena, cues, factory);
                            Assert.That(session.TryLoadModule("east", out error), Is.True, error);

                            media.DelayNext = true; int parallelCalls = endpoint.Calls;
                            Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            var parallelSkipped = host.Current; Assert.That(parallelSkipped.TrySkip(parallelSkipped.Read().ExecutionId, out error), Is.True, error);
                            Assert.That(parallelSkipped.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); Assert.That(endpoint.Calls, Is.EqualTo(parallelCalls + 1));
                            Assert.That(parallelSkipped.Read().Children.All(c => c.Status == SequenceStepStatus.Cancelled), Is.True);
                            Assert.That(media.TicketReturned, Is.True); AssertReturned(arena, cues, factory); media.ResolveLate(); Assert.That(factory.LiveViewCount, Is.Zero);
                            Assert.That(parallelSkipped.TrySkip(parallelSkipped.Read().ExecutionId, out _), Is.False);

                            var timeoutDefinition = new SequenceDefinition("parallel-timeout", new[] { SequenceStep.ParallelAll("together",
                                SequenceStep.Dialogue("talk", "npc", .5), SequenceStep.Camera("camera", "focus", 2), SequenceStep.Cue("effect", "sprite", 5)) }, new[] { "focus" });
                            Assert.That(host.TryStart(timeoutDefinition, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            clock.Time += .5; host.Refresh(); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut)); Assert.That(host.Current.Read().Error, Does.Contain("talk")); AssertReturned(arena, cues, factory);

                            long parallelRevision = arena.Controls.Current.Revision;
                            var missing = new SequenceDefinition("parallel-missing", new[] { SequenceStep.ParallelAll("together",
                                SequenceStep.Dialogue("talk", "npc", 5), SequenceStep.Cue("missing", "unknown", 5)) }, new[] { "focus" });
                            Assert.That(host.TryStart(missing, bindings, arena.Controls, policy, clock, out error), Is.False); Assert.That(error, Does.Contain("unknown"));
                            var twoDialogues = new SequenceDefinition("parallel-two-dialogues", new[] { SequenceStep.ParallelAll("together",
                                SequenceStep.Dialogue("a", "npc", 5), SequenceStep.Dialogue("b", "npc", 5)) }, Array.Empty<string>());
                            Assert.That(host.TryStart(twoDialogues, bindings, arena.Controls, policy, clock, out error), Is.False); Assert.That(error, Does.Contain("one dialogue view"));
                            Assert.That(arena.Controls.Current.Revision, Is.EqualTo(parallelRevision)); AssertReturned(arena, cues, factory);

                            var pinned = new SequenceDefinition("parallel-pinned-dialogue", new[] { SequenceStep.Wait("before", .25),
                                SequenceStep.ParallelAll("together", SequenceStep.Dialogue("talk", "npc", 5), SequenceStep.Camera("camera", "focus", 1), SequenceStep.Cue("effect", "sprite", 5)) }, new[] { "focus" });
                            Assert.That(host.TryStart(pinned, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "focus", new NpcDefinition(npc.Id, DialogueFixtures.Definition(".changed"), "npc")) });
                            clock.Time += .25; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed));
                            Assert.That(Child(host.Current.Read(), "camera").Status, Is.EqualTo(SequenceStepStatus.Pending)); AssertReturned(arena, cues, factory);
                            targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "focus", npc) });

                            Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            clock.OnRead = () => { clock.OnRead = null; owner.SetActive(false); }; host.Refresh();
                            var ended = host.Current; Assert.That(ended.Read().EndReason, Is.EqualTo(SequenceEndReason.OwnerDisposed)); AssertReturned(arena, cues, factory); owner.SetActive(true);
                            Assert.That(host.TryStart(ParallelDefinition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            ended.Dispose(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running));
                            world.Close(); host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); AssertReturned(arena, cues, factory);
                            continue;
                        }
                        using (var modal = arena.Controls.OpenScope("sequence external modal")) using (var cameraOwner = arena.CameraRequests.OpenScope("sequence external camera"))
                        {
                            var block = modal.BlockInput(InputChannels.Navigation | InputChannels.Combat); var pause = modal.PauseSimulation();
                            var camera = cameraOwner.Request(1, new FixedCameraPosition(new CameraPosition(77, 78, -10)));
                            Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            Assert.That(view.Displayed.Body, Is.EqualTo("fixture.a")); Assert.That(player.InputBlocked, Is.True); Assert.Throws<InvalidOperationException>(() => driver.Step(.02f));
                            FinishDialogue(host, next, view); Assert.That(host.Current.Read().StepId, Is.EqualTo("camera")); output.ApplyNow();
                            Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(13, 1003, -10))); Assert.That(arena.CameraRequests.ActiveCount, Is.EqualTo(2));
                            clock.Time = .5; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("effect")); Assert.That(factory.LiveViewCount, Is.EqualTo(1));
                            var cueRenderer = Object.FindObjectsByType<SpriteRenderer>().Single(r => r.name == cue.Id);
                            Assert.That(cueRenderer.sprite, Is.SameAs(sprite)); Assert.That(cueRenderer.transform.position, Is.EqualTo(new Vector3(point.x, point.y, 0)));
                            Assert.That(cueRenderer.transform.localScale.x, Is.EqualTo(-1)); Assert.That(cueRenderer.GetComponent<Collider2D>(), Is.Null); Assert.That(cueRenderer.GetComponent<Rigidbody2D>(), Is.Null);
                            output.ApplyNow(); Assert.That(Camera.main.transform.position.x, Is.EqualTo(77));
                            clock.Time = .75; host.Refresh(); Assert.That(host.Current.Read().StepId, Is.EqualTo("settle")); Assert.That(factory.LiveViewCount, Is.Zero);
                            clock.Time = .875; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed));
                            Assert.That(camera.Status, Is.EqualTo(CameraRequestStatus.Active)); Assert.That(arena.CameraRequests.ActiveCount, Is.EqualTo(1));
                            Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation | InputChannels.Combat));
                        }
                        AssertReturned(arena, cues, factory);
                        Assert.That(arena.Now, Is.EqualTo(simulationTime)); Assert.That(player.ReceivedHitCount, Is.EqualTo(hits));
                        Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present), "Sequence completion does not invent story/removal/reward state.");

                        // Cancel while camera owns a request. A duplicate/end input cannot run the Cue.
                        Assert.That(host.TryStart(Definition(false), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        var cancelled = host.Current; var oldId = cancelled.Read().ExecutionId;
                        Assert.That(cancelled.TrySkip(oldId, out _), Is.False); Assert.That(cancelled.TryCancel(oldId, out error), Is.True, error);
                        Assert.That(cancelled.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); AssertReturned(arena, cues, factory);

                        // Explicit skip executes one endpoint, without starting a camera/Cue or completing dialogue.
                        int calls = endpoint.Calls;
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        Assert.That(host.Current.TryCancel(oldId, out _), Is.False); cancelled.Dispose();
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Running)); var skipped = host.Current;
                        Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out error), Is.True, error); next.onClick.Invoke(); host.Refresh();
                        Assert.That(skipped.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); Assert.That(endpoint.Calls, Is.EqualTo(calls + 1));
                        Assert.That(skipped.TrySkip(skipped.Read().ExecutionId, out _), Is.False); AssertReturned(arena, cues, factory);

                        // An old delayed media result cannot revive a cancelled Cue or affect a new execution.
                        media.DelayNext = true;
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        clock.Time += .5; host.Refresh(); var delayed = host.Current;
                        Assert.That(delayed.Read().StepId, Is.EqualTo("effect")); Assert.That(cues.ActiveCount, Is.EqualTo(1)); Assert.That(factory.LiveViewCount, Is.Zero);
                        host.Close(); Assert.That(media.TicketReturned, Is.True); AssertReturned(arena, cues, factory);
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        clock.Time += .5; host.Refresh(); Assert.That(factory.LiveViewCount, Is.EqualTo(1)); media.ResolveLate();
                        Assert.That(factory.LiveViewCount, Is.EqualTo(1)); Assert.That(delayed.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                        host.Close(); AssertReturned(arena, cues, factory);

                        // Required anchor checks are shared by fixed/captured providers and have no control/view side effects.
                        long revision = arena.Controls.Current.Revision;
                        Assert.That(host.TryStart(Definition(anchors: new[] { "focus", "missing" }), bindings, arena.Controls, policy, clock, out error), Is.False);
                        Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.BindingFailed)); Assert.That(error, Does.Contain("missing"));
                        Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision)); AssertReturned(arena, cues, factory);
                        Assert.That(host.TryStart(Definition(anchors: Array.Empty<string>()), bindings, arena.Controls, policy, clock, out _), Is.False, "A binding anchor must be declared in the sequence.");
                        AssertReturned(arena, cues, factory);

                        media.FailNext = true;
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        clock.Time += .5; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed));
                        Assert.That(host.Current.Read().Error, Does.Contain("fixture missing media")); AssertReturned(arena, cues, factory);

                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        Assert.That(session.TryUnloadModule("east", out error), Is.True, error); host.Refresh();
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); Assert.That(host.Current.Read().Error, Does.Contain("TargetLost")); AssertReturned(arena, cues, factory);
                        Assert.That(session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present)); Assert.That(session.TryLoadModule("east", out error), Is.True, error);

                        viewBinding.ThrowUnbind = true;
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.CleanupFailed));
                        Assert.That(host.Current.Read().Error, Does.Contain("fixture dialogue cleanup failed")); AssertReturned(arena, cues, factory); viewBinding.ThrowUnbind = false;

                        // Rebinding an NPC while a sequence waits cannot replace its captured version.
                        var pinDefinition = new SequenceDefinition("pinned-dialogue-fixture", new[] { SequenceStep.Wait("wait", .25), SequenceStep.Dialogue("talk", "npc", 10) }, Array.Empty<string>());
                        Assert.That(host.TryStart(pinDefinition, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "focus", new NpcDefinition(npc.Id, DialogueFixtures.Definition(".changed"), "npc")) });
                        clock.Time += .25; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(view.Displayed, Is.Null); AssertReturned(arena, cues, factory);
                        targets.ReplaceBindings(new[] { new NpcPlacementBinding("marker-east", "focus", npc) });

                        // Owner shutdown inside a start or skip callback is deferred until the command returns, then returns all leases.
                        clock.OnRead = () => { clock.OnRead = null; owner.SetActive(false); };
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.False);
                        Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.OwnerDisposed)); AssertReturned(arena, cues, factory); owner.SetActive(true);
                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error);
                        endpoint.OnBegin = () => owner.SetActive(false);
                        Assert.That(host.Current.TrySkip(host.Current.Read().ExecutionId, out error), Is.True, error);
                        Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Skipped)); AssertReturned(arena, cues, factory); endpoint.OnBegin = null; owner.SetActive(true);

                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        owner.SetActive(false); Assert.That(host.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.OwnerDisposed)); AssertReturned(arena, cues, factory); owner.SetActive(true);
                        // Output loss cancels the real camera leaf and only the sequence's control.
                        var cameraOnly = new SequenceDefinition("camera-output-lifetime", new[] { SequenceStep.Camera("camera", "focus", 1) }, new[] { "focus" });
                        using (var modal = arena.Controls.OpenScope("camera lifetime external modal"))
                        {
                            var block = modal.BlockInput(InputChannels.Navigation); var pause = modal.PauseSimulation();
                            Assert.That(host.TryStart(cameraOnly, bindings, arena.Controls, policy, clock, out error), Is.True, error);
                            output.enabled = false; host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                            Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
                            output.enabled = true;
                            Assert.That(host.TryStart(cameraOnly, bindings, arena.Controls, policy, clock, out error), Is.False, "An old camera service cannot silently adopt the new output session.");
                            bindings = Bind(); Assert.That(host.TryStart(cameraOnly, bindings, arena.Controls, policy, clock, out error), Is.True, error); output.ApplyNow();
                            Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(13, 1003, -10))); host.Close();
                            Assert.That(block.IsActive && pause.IsActive, Is.True);
                        }
                        AssertReturned(arena, cues, factory);
                        Assert.That(arena.Now, Is.EqualTo(simulationTime)); Assert.That(player.ReceivedHitCount, Is.EqualTo(hits));

                        Assert.That(host.TryStart(Definition(), bindings, arena.Controls, policy, clock, out error), Is.True, error); FinishDialogue(host, next, view);
                        world.Close(); host.Refresh(); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); AssertReturned(arena, cues, factory);
                        Assert.That(bindings.Validate(Definition(), out _), Is.False, "An ended chapter cannot redirect bindings to a new session.");
                    }
                    Assert.That(captured.Calls, Is.EqualTo(1));
                }
            }
            finally
            {
                host.Close(); world.Close(); cues.Dispose(); factory.Dispose();
                Object.Destroy(owner); Object.Destroy(root); Object.Destroy(sources); Object.Destroy(canvas); Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
        static void AssertReturned(CombatArena arena, CuePlayer cues, SpriteCueFactory factory)
        {
            Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(arena.Controls.Current.SimulationPaused, Is.False);
            Assert.That(arena.CameraRequests.ActiveCount, Is.Zero); Assert.That(cues.ActiveCount, Is.Zero); Assert.That(factory.LiveViewCount, Is.Zero);
        }
        internal static void FinishDialogue(SequenceHost host, UnityEngine.UI.Button next, DialogueView view)
        {
            next.onClick.Invoke(); next.onClick.Invoke(); Assert.That(view.Displayed.Token.LineId, Is.EqualTo("a"), "Displayed token remains until refresh.");
            host.Refresh(); Assert.That(view.Displayed.Token.LineId, Is.EqualTo("b"));
            next.onClick.Invoke(); host.Refresh(); Assert.That(view.Displayed.Token.LineId, Is.EqualTo("c"));
            next.onClick.Invoke(); host.Refresh();
        }
        internal static GameObject CreateView(out DialogueView view, out UnityEngine.UI.Button next, out UnityEngine.UI.Button cancel)
        {
            var root = new GameObject("SequenceDialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("SequenceDialoguePanel", typeof(RectTransform)); panel.transform.SetParent(root.transform, false); panel.SetActive(false);
            var speaker = Label(panel.transform, "Speaker"); var body = Label(panel.transform, "Body");
            next = Button(panel.transform, "Advance"); cancel = Button(panel.transform, "Cancel");
            view = panel.AddComponent<DialogueView>(); view.Configure(panel, speaker, body, next, cancel); return root;
        }
        static TMPro.TMP_Text Label(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TMPro.TextMeshProUGUI>(); text.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); text.raycastTarget = false; return text;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button)); obj.transform.SetParent(parent, false);
            var button = obj.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>(); return button;
        }
    }
}
