using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Presentation;
using Deviant.World;
using Deviant.World.Contracts;
using Deviant.World.Tests;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SequenceParallelCueTimingTests
    {
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class View : ICueView
        {
            public PresentationCueDefinition Definition;
            public readonly List<int> Frames = new List<int>();
            public bool Disposed;
            public void Sample(float elapsed) { Frames.Add(Definition.FrameAt(elapsed)); }
            public void Dispose() { Disposed = true; }
        }
        sealed class Media : ICueViewFactory, ICueCatalog, ICueCatalogSnapshot
        {
            public readonly View LateView = new View();
            Action<CueViewResult> pending;
            public Guid Version { get; } = Guid.NewGuid();
            public ICueCatalogSnapshot Capture(IReadOnlyList<string> cueIds) => this;
            public ICueDefinition Read(string cueId) => Cue(cueId);
            public IDisposable Begin(ICueDefinition definition, CueAnchor anchor, Action<CueViewResult> resolved)
            {
                if (definition.Id == "fixture.late") { LateView.Definition = (PresentationCueDefinition)definition; pending = resolved; return new Ticket(); }
                resolved(CueViewResult.Ready(new View { Definition = (PresentationCueDefinition)definition })); return null;
            }
            public void ResolveLate() { var callback = pending; pending = null; callback(CueViewResult.Ready(LateView)); }
            sealed class Ticket : IDisposable { public void Dispose() { } }
        }
        static PresentationCueDefinition Cue(string id) => new PresentationCueDefinition(id,
            new[] { "fixture.frame.0", "fixture.frame.1" }, new[] { .125f, .125f }, new[] { 0f, 0f }, new[] { 0f, 0f });

        [TestCase(false, .125)] [TestCase(true, .125)]
        [TestCase(false, .25)] [TestCase(true, .25)]
        public void LateMediaUsesItsOwnCurrentElapsedTimeWithOrWithoutAParallelPeer(bool parallel, double arrival)
        {
            var sources = new GameObject("ParallelCueTimingSources"); sources.SetActive(false);
            var module = new GameObject("FixtureFloor"); module.transform.SetParent(sources.transform, false);
            var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("FixtureMarker"); marker.transform.SetParent(sources.transform, false);
            var root = new GameObject("ParallelCueTimingChapter"); var world = root.AddComponent<ChapterWorldHost>();
            try
            {
                var layout = ChapterFixtures.Good();
                Assert.That(world.TryOpen(new AuthoredLayoutProvider(layout), new LayoutRequest(layout.Id, layout.Version), ChapterFixtures.Body(),
                    new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
                using (var controls = new ControlSession()) using (var modal = controls.OpenScope("fixture external modal"))
                using (var cameras = new CameraDirector())
                {
                    modal.BlockInput(InputChannels.Navigation); var pause = modal.PauseSimulation();
                    var media = new Media();
                    using (var cues = new CuePlayer(media))
                    {
                        var bindings = new ChapterSequenceBindings(world, world.Session.Id, new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(),
                            new InteractionPolicy(2, true, false, true), new DialogueFixtures.View(), new DialogueFixtures.Text(), controls,
                            new DialoguePresentationPolicy(InputChannels.None, false), cues, media, cameras, Array.Empty<SequenceDialogueBinding>(),
                            new[] { new SequenceCueBinding("ready", "focus", "fixture.ready", 1), new SequenceCueBinding("late", "focus", "fixture.late", 1) },
                            Array.Empty<SequenceCameraBinding>());
                        var late = SequenceStep.Cue("late", "late", 5);
                        var steps = parallel ? new[] { SequenceStep.ParallelAll("together", SequenceStep.Cue("ready", "ready", 5), late) } : new[] { late };
                        var definition = new SequenceDefinition("fixture-late-cue-time", steps, new[] { "focus" }); var clock = new Clock();
                        using (var runner = new SequenceRunner(definition, bindings, controls, new SequenceControlPolicy(InputChannels.Combat, false), clock))
                        {
                            Assert.That(media.LateView.Frames, Is.Empty); clock.Time = arrival; media.ResolveLate();
                            Assert.That(media.LateView.Frames, Is.Empty, "Media resolution only prepares the view; sampling belongs to the owning step.");
                            runner.Poll();
                            if (arrival == .25)
                            {
                                Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Failed), "A sibling must not make media first arriving at playback end look timely.");
                                Assert.That(runner.Read().Error, Does.Contain("not shown before")); Assert.That(media.LateView.Frames, Is.Empty);
                                Assert.That(media.LateView.Disposed, Is.True); Assert.That(cues.ActiveCount, Is.Zero);
                                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation));
                            }
                            else
                            {
                                Assert.That(runner.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(media.LateView.Frames, Is.Not.Empty);
                                Assert.That(media.LateView.Frames.All(frame => frame == 1), Is.True, "A sibling must not sample the late Cue using its previous elapsed time.");
                            }
                        }
                        Assert.That(cues.ActiveCount, Is.Zero); Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(pause.IsActive, Is.True);
                    }
                }
            }
            finally { world.Close(); Object.DestroyImmediate(root); Object.DestroyImmediate(sources); }
        }
    }
}
