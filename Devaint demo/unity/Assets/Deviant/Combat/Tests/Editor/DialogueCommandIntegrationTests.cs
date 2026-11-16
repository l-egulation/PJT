using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Narrative;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.Presentation;
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
    public class DialogueCommandIntegrationTests
    {
        sealed class Clock : ISequenceClock { public double Time; public double ReadTime() => Time; }
        sealed class Endpoint : ISequenceSkipEndpoint
        {
            public int Calls;
            public bool Validate(string id, out string error) { error = id == "fixture.command.end" ? null : "unknown endpoint"; return error == null; }
            public ISequenceOperation Begin(Guid execution, string id) { Calls++; return new Operation(); }
            sealed class Operation : ISequenceOperation
            {
                public SequenceOperationStatus Status => SequenceOperationStatus.Completed;
                public string Error => null;
                public void Poll(double elapsed) { }
                public void Dispose() { }
            }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator GameCoordinatesChoiceCommandsWithNpcAndSequenceLifetimes() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCoordinatesChoiceCommandsWithNpcAndSequenceLifetimes() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var sources = new GameObject("CommandChapterSources"); sources.SetActive(false);
            var module = new GameObject("CommandFloor"); module.transform.SetParent(sources.transform, false); var floor = module.AddComponent<BoxCollider2D>(); floor.size = new Vector2(8, 1); floor.offset = new Vector2(4, -.5f);
            var marker = new GameObject("CommandMarker"); marker.transform.SetParent(sources.transform, false);
            var worldRoot = new GameObject("CommandChapter"); worldRoot.transform.position = new Vector3(0, 1000, 0); var world = worldRoot.AddComponent<ChapterWorldHost>();
            var owner = new GameObject("CommandHosts"); var npcHost = owner.AddComponent<NpcDialogueHost>(); var sequence = owner.AddComponent<SequenceHost>();
            var canvas = SequenceIntegrationTests.CreateView(out var view, out var advance, out var cancel);
            var speaker = view.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.gameObject.name == "Speaker"); var body = view.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.gameObject.name == "Body");
            var buttons = new Button[2]; var labels = new TMPro.TMP_Text[2];
            for (int i = 0; i < buttons.Length; i++)
            {
                var root = new GameObject("CommandChoice" + i, typeof(RectTransform), typeof(Image), typeof(Button)); root.transform.SetParent(view.transform, false); buttons[i] = root.GetComponent<Button>();
                var label = new GameObject("CommandLabel" + i, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); label.transform.SetParent(root.transform, false); labels[i] = label.GetComponent<TMPro.TMP_Text>(); labels[i].font = speaker.font; labels[i].raycastTarget = false;
            }
            view.Configure(view.gameObject, speaker, body, advance, cancel, buttons, labels);
            var result = DialogueCommandStatus.Succeeded; int attempts = 0, commits = 0; DialogueCommandContext observed = null;
            using (var commands = new DialogueCommandBindings(new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> {
                { DialogueCommandFixtures.Action, (context, request) => { observed = context; attempts++; if (result == DialogueCommandStatus.Succeeded) commits++; return new DialogueCommandResult(result, result == DialogueCommandStatus.Succeeded ? null : "fixture " + result); } } }))
            using (var cues = new CuePlayer(new SpriteCueFactory()))
            {
                try
                {
                    using (var driver = arena.AcquireManualSimulation())
                    {
                        var layout = ChapterFixtures.Good(); var endpoint = new Endpoint(); var clock = new Clock();
                        var definition = new SequenceDefinition("command-sequence-fixture", new[] { SequenceStep.Dialogue("talk", "npc", 1), SequenceStep.Wait("tail", .125) },
                            Array.Empty<string>(), new SequenceSkipDefinition("fixture.command.end", 1));
                        var sequencePolicy = new SequenceControlPolicy(InputChannels.Combat | InputChannels.Interaction, true);
                        var displayPolicy = new DialoguePresentationPolicy(InputChannels.Combat | InputChannels.Interaction, false);
                        foreach (var provider in new IChapterLayoutProvider[] { new AuthoredLayoutProvider(layout), new ChapterFixtures.CapturedProvider() })
                        {
                            driver.Reset(); foreach (var enemy in arena.enemies) enemy.aggressive = false;
                            Assert.That(world.TryOpen(provider, new LayoutRequest(layout.Id, layout.Version, 17), UnityChapterModules.ProfileFor(player),
                                new Dictionary<string, GameObject> { { "floor-8", module } }, new Dictionary<string, GameObject> { { "marker", marker } }, out var error), Is.True, error);
                            Assert.That(world.Session.TryLoadModule("east", out error), Is.True, error); Assert.That(world.TryGetWorldAnchor(world.Session.Id, "focus", out var point, out _), Is.True);
                            player.Position = point + Vector2.right; player.velocity = Vector2.zero; float now = arena.Now; int hits = player.ReceivedHitCount;
                            var targets = new ChapterNpcTargets(world, world.Session.Id, new[] { new NpcPlacementBinding("marker-east", "focus", new NpcDefinition("fixture-npc", DialogueCommandFixtures.Definition())) }, () => player.Position, () => arena.CombatActive);
                            var conditions = new DialogueFixtures.Conditions(); var interactionPolicy = new InteractionPolicy(2, false, false, true); var text = new DialogueFixtures.Text();
                            ChapterSequenceBindings Bind(IDialogueCommandBindings registry) => new ChapterSequenceBindings(world, world.Session.Id, targets, conditions, interactionPolicy,
                                view, text, arena.Controls, displayPolicy, cues, new SequenceCueCatalog(), arena.CameraRequests, new[] { new SequenceDialogueBinding("npc", "marker-east") },
                                Array.Empty<SequenceCueBinding>(), Array.Empty<SequenceCameraBinding>(), endpoint, commandBindings: registry);

                            npcHost.Configure(targets, conditions, interactionPolicy, view, text, arena.Controls, displayPolicy);
                            long revision = arena.Controls.Current.Revision; Assert.That(npcHost.TryInteract(npcHost.Query(out _)[0], out _), Is.False);
                            Assert.That(commands.ActiveScopeCount, Is.Zero); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision));
                            Assert.That(sequence.TryStart(definition, Bind(null), arena.Controls, sequencePolicy, clock, out _), Is.False);
                            Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision));

                            npcHost.Configure(targets, conditions, interactionPolicy, view, text, arena.Controls, displayPolicy, commands);
                            Assert.That(npcHost.TryInteract(npcHost.Query(out _)[0], out error), Is.True, error); var dialogue = npcHost.Current;
                            using (var modal = arena.Controls.OpenScope("fixture command modal"))
                            {
                                var block = modal.BlockInput(InputChannels.Dialogue | InputChannels.Combat); var pause = modal.PauseSimulation(); int beforeAttempts = attempts, beforeCommits = commits;
                                buttons[0].onClick.Invoke(); Assert.That(attempts, Is.EqualTo(beforeAttempts)); block.Dispose(); var remaining = modal.BlockInput(InputChannels.Combat);
                                result = DialogueCommandStatus.Rejected; buttons[0].onClick.Invoke(); Assert.That(dialogue.Read().Token.LineId, Is.EqualTo("choose")); Assert.That(dialogue.Read().LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Rejected));
                                result = DialogueCommandStatus.Succeeded; buttons[0].onClick.Invoke(); buttons[0].onClick.Invoke();
                                Assert.That(commits, Is.EqualTo(beforeCommits + 1)); Assert.That(attempts, Is.EqualTo(beforeAttempts + 2)); Assert.That(dialogue.Read().Token.LineId, Is.EqualTo("result"));
                                Assert.That(observed.SessionId, Is.EqualTo(dialogue.Read().Token.SessionId)); Assert.That(observed.WorldSessionId, Is.EqualTo(world.Session.Id)); Assert.That(observed.PlacementId, Is.EqualTo("marker-east"));
                                npcHost.Refresh(); buttons[0].onClick.Invoke(); npcHost.Refresh(); advance.onClick.Invoke(); Assert.That(dialogue.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                                Assert.That(commands.ActiveScopeCount, Is.Zero); Assert.That(remaining.IsActive && pause.IsActive && player.InputBlocked, Is.True);
                            }
                            npcHost.Close(DialogueEndReason.OwnerDisposed); Assert.That(player.InputBlocked || arena.Controls.Current.SimulationPaused, Is.False);
                            var bindings = Bind(commands); int expectedCommits = commits;
                            Assert.That(sequence.TryStart(definition, bindings, arena.Controls, sequencePolicy, clock, out error), Is.True, error);
                            buttons[0].onClick.Invoke(); buttons[0].onClick.Invoke(); sequence.Refresh(); buttons[0].onClick.Invoke(); sequence.Refresh(); advance.onClick.Invoke(); sequence.Refresh();
                            Assert.That(commands.ActiveScopeCount, Is.Zero); clock.Time += .25; sequence.Refresh(); Assert.That(sequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Completed)); Assert.That(commits, Is.EqualTo(expectedCommits + 1));

                            foreach (var status in new[] { DialogueCommandStatus.Failed, DialogueCommandStatus.Cancelled })
                            {
                                result = status; Assert.That(sequence.TryStart(definition, bindings, arena.Controls, sequencePolicy, clock, out error), Is.True, error);
                                buttons[0].onClick.Invoke(); sequence.Refresh(); Assert.That(sequence.Current.Read().Status, Is.EqualTo(status == DialogueCommandStatus.Failed ? SequenceStatus.Failed : SequenceStatus.Cancelled));
                                Assert.That(sequence.Current.Read().Error, Does.Contain(status == DialogueCommandStatus.Failed ? "fixture Failed" : "CommandCancelled")); Assert.That(commands.ActiveScopeCount, Is.Zero);
                            }
                            int unchanged = commits, skippedCalls = endpoint.Calls;
                            result = DialogueCommandStatus.Rejected; Assert.That(sequence.TryStart(definition, bindings, arena.Controls, sequencePolicy, clock, out error), Is.True, error);
                            buttons[0].onClick.Invoke(); sequence.Refresh(); Assert.That(sequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Running));
                            Assert.That(sequence.Current.TrySkip(sequence.Current.Read().ExecutionId, out error), Is.True, error); Assert.That(sequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Skipped));
                            Assert.That(endpoint.Calls, Is.EqualTo(skippedCalls + 1)); Assert.That(commands.ActiveScopeCount, Is.Zero); Assert.That(commits, Is.EqualTo(unchanged));

                            result = DialogueCommandStatus.Succeeded; Assert.That(sequence.TryStart(definition, bindings, arena.Controls, sequencePolicy, clock, out error), Is.True, error);
                            clock.Time += 2; sequence.Refresh(); Assert.That(sequence.Current.Read().EndReason, Is.EqualTo(SequenceEndReason.TimedOut)); Assert.That(commands.ActiveScopeCount, Is.Zero); Assert.That(commits, Is.EqualTo(unchanged));
                            Assert.That(sequence.TryStart(definition, bindings, arena.Controls, sequencePolicy, clock, out error), Is.True, error); Assert.That(world.Session.TryUnloadModule("east", out error), Is.True, error); sequence.Refresh();
                            Assert.That(sequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); Assert.That(commands.ActiveScopeCount, Is.Zero); Assert.That(world.Session.TryLoadModule("east", out error), Is.True, error);

                            var cleanup = new DialogueCommandFixtures.WrappedBindings(commands) { ThrowCleanup = true };
                            Assert.That(sequence.TryStart(definition, Bind(cleanup), arena.Controls, sequencePolicy, clock, out error), Is.True, error);
                            buttons[0].onClick.Invoke(); sequence.Refresh(); buttons[0].onClick.Invoke(); sequence.Refresh(); advance.onClick.Invoke(); sequence.Refresh();
                            Assert.That(sequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Failed)); Assert.That(sequence.Current.Read().Error, Does.Contain("command cleanup failed"));
                            Assert.That(cleanup.Disposals, Is.EqualTo(1)); Assert.That(commands.ActiveScopeCount, Is.Zero);
                            Assert.That(world.Session.State.ReadPresence("marker-east"), Is.EqualTo(PlacementPresence.Present)); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.ReceivedHitCount, Is.EqualTo(hits));
                            Assert.That(player.InputBlocked || arena.Controls.Current.SimulationPaused, Is.False);
                            sequence.Close(); world.Close();
                        }
                    }
                }
                finally { npcHost.Close(DialogueEndReason.OwnerDisposed); sequence.Close(); world.Close(); Object.DestroyImmediate(owner); Object.DestroyImmediate(worldRoot); Object.DestroyImmediate(sources); Object.DestroyImmediate(canvas); }
            }
        }
    }
}
