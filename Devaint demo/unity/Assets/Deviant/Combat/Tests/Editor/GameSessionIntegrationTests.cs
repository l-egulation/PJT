using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Equipment.Contracts;
using Deviant.World.Contracts;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class GameSessionIntegrationTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void OpenScene()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity"); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
        }
        [UnityTest] public IEnumerator DeathClosesOnlySessionChildrenAndResetReusesActualPlayerAndEquipment()
        {
            OpenScene(); yield return new EnterPlayMode(); yield return null; yield return null;
            Assert.That(Application.isPlaying, Is.True, "Game lifetime assertions require actual Play mode.");
            var game = new GameView(Object.FindAnyObjectByType<CombatArena>()); var arena = game.combat; var session = game.Session;
            Assert.That(session.IsActive, Is.True); var player = arena.Player; var weapons = player.weapons; var loadout = session.Loadout;
            var first = loadout.GetWeapon(0); var second = loadout.GetWeapon(1); var terrain = game.forest.CurrentWindow; int views = game.forest.ActiveCount;
            var owner = new GameObject("GameSessionActualChildren"); var sequence = owner.AddComponent<SequenceHost>(); var dialogue = owner.AddComponent<NpcDialogueHost>();
            var foreignSequence = new GameObject("ForeignSequenceHost").AddComponent<SequenceHost>();
            var targets = new Targets(); var view = new View(); var clock = new Clock();
            try
            {
                Assert.That(session.TryConfigureDialogue(session.Id, dialogue, targets, new Conditions(), new InteractionPolicy(2, true, false, true),
                    view, new Text(), new DialoguePresentationPolicy(InputChannels.Dialogue, true), out var error), Is.True, error);
                Assert.That(session.TryInteract(session.Id, dialogue, targets.Target(), out error), Is.True, error); var oldDialogue = dialogue.Current;
                var definition = new SequenceDefinition("session-wait", new[] { SequenceStep.Wait("wait", 10) }, Array.Empty<string>());
                Assert.That(session.TryStartSequence(session.Id, sequence, definition, new WaitBindings(), new SequenceControlPolicy(InputChannels.Interaction, true), clock, out error), Is.True, error);
                var oldSequence = sequence.Current;
                var hud = (MonoBehaviour)game.Component.GetType().GetField("hud").GetValue(game.Component);
                hud.SendMessage("OpenBag"); Assert.That(session.Screens.OpenCount, Is.EqualTo(1));
                Assert.That(foreignSequence.TryStart(definition, new WaitBindings(), arena.Controls, new SequenceControlPolicy(InputChannels.None, false), clock, out error), Is.True, error);
                using (var foreign = arena.Controls.OpenScope("foreign session test owner"))
                using (var foreignBlock = foreign.BlockInput(InputChannels.Navigation))
                using (var foreignPause = foreign.PauseSimulation())
                {
                    player.health = 0; player.Die();
                    Assert.That(session.IsActive, Is.False); Assert.That(session.EndReason, Is.EqualTo("PlayerDied"));
                    Assert.That(oldSequence.Read().Status, Is.EqualTo(SequenceStatus.Cancelled)); Assert.That(oldDialogue.Read().Status, Is.EqualTo(DialogueStatus.Cancelled));
                    Assert.That(oldDialogue.Read().EndReason, Is.EqualTo(DialogueEndReason.RunEnded)); Assert.That(view.Bound, Is.EqualTo(Guid.Empty));
                    Assert.That(foreignSequence.Current.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(foreignBlock.IsActive && foreignPause.IsActive, Is.True);
                    Assert.That(arena.Controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                    Assert.That(game.forest.followTarget, Is.Null); Assert.That(game.forest.ActiveCount, Is.EqualTo(views)); Assert.That(game.forest.CurrentWindow, Is.SameAs(terrain));
                    Assert.That(arena.Player, Is.SameAs(player)); Assert.That(player.weapons, Is.SameAs(weapons)); Assert.That(player.weapons[0].InstanceId, Is.EqualTo(first.InstanceId));
                    Assert.That(session.Screens.OpenCount, Is.Zero); Assert.That(hud.GetType().GetField("dialog").GetValue(hud) is GameObject dialog && !dialog.activeSelf, Is.True);
                }
                using (var driver = arena.AcquireManualSimulation())
                {
                    driver.Reset(); var next = game.Session;
                    Assert.That(next.IsActive, Is.True); Assert.That(next.Id, Is.Not.EqualTo(session.Id)); Assert.That(next.Loadout, Is.SameAs(loadout));
                    Assert.That(arena.Player, Is.SameAs(player)); Assert.That(player.weapons, Is.SameAs(weapons)); Assert.That(player.weapons[1].InstanceId, Is.EqualTo(second.InstanceId));
                    Assert.That(next.Forest.HasValidatedChapterLayout, Is.True); Assert.That(next.Forest.ChapterLayoutLimitation, Does.Contain("not a finite chapter"));
                    Assert.That(session.TryStartSequence(session.Id, sequence, definition, new WaitBindings(), new SequenceControlPolicy(InputChannels.None, false), clock, out _), Is.False);
                    Assert.That(game.forest.followTarget, Is.SameAs(player.transform)); driver.Step(.02f); Assert.That(arena.SimulationFaulted, Is.False);
                }
            }
            finally { foreignSequence.Close(); Object.Destroy(foreignSequence.gameObject); Object.Destroy(owner); }
        }
        [UnityTest] public IEnumerator ReconfiguredHostAndNewExecutionAreNotClosedByOldSessionOwner()
        {
            OpenScene(); yield return new EnterPlayMode(); yield return null; yield return null;
            Assert.That(Application.isPlaying, Is.True, "Game lifetime assertions require actual Play mode.");
            var game = new GameView(Object.FindAnyObjectByType<CombatArena>()); var session = game.Session;
            var owner = new GameObject("SessionGenerationChildren"); var dialogue = owner.AddComponent<NpcDialogueHost>(); var sequence = owner.AddComponent<SequenceHost>();
            var targets = new Targets(); var view = new View(); var clock = new Clock();
            try
            {
                Assert.That(session.TryConfigureDialogue(session.Id, dialogue, targets, new Conditions(), new InteractionPolicy(2, true, false, true), view, new Text(), new DialoguePresentationPolicy(InputChannels.None, false), out var error), Is.True, error);
                Guid oldBinding = dialogue.BindingId;
                dialogue.Configure(targets, new Conditions(), new InteractionPolicy(2, true, false, true), view, new Text(), game.combat.Controls, new DialoguePresentationPolicy(InputChannels.Navigation, false));
                Assert.That(dialogue.TryInteract(targets.Target(), out error), Is.True, error); var foreignDialogue = dialogue.Current;
                var wait = new SequenceDefinition("generation-wait", new[] { SequenceStep.Wait("wait", 10) }, Array.Empty<string>());
                Assert.That(session.TryStartSequence(session.Id, sequence, wait, new WaitBindings(), new SequenceControlPolicy(InputChannels.None, false), clock, out error), Is.True, error);
                sequence.Close(); Assert.That(sequence.TryStart(wait, new WaitBindings(), game.combat.Controls, new SequenceControlPolicy(InputChannels.Interaction, false), clock, out error), Is.True, error);
                var foreignSequence = sequence.Current; session.Close("Disabled");
                Assert.That(dialogue.BindingId, Is.Not.EqualTo(oldBinding)); Assert.That(foreignDialogue.Read().Status, Is.EqualTo(DialogueStatus.Active));
                Assert.That(foreignSequence.Read().Status, Is.EqualTo(SequenceStatus.Running)); Assert.That(dialogue.TryClose(oldBinding, DialogueEndReason.RunEnded, out _), Is.False);
            }
            finally { dialogue.Close(DialogueEndReason.OwnerDisposed); sequence.Close(); Object.Destroy(owner); }
        }
        [UnityTest] public IEnumerator DisableDestroyReleaseFollowAndOnlyOwnedControlScopes()
        {
            OpenScene(); yield return new EnterPlayMode(); yield return null; yield return null;
            Assert.That(Application.isPlaying, Is.True, "Game lifetime assertions require actual Play mode.");
            var game = new GameView(Object.FindAnyObjectByType<CombatArena>()); var session = game.Session; var controls = game.combat.Controls;
            var owned = session.Controls.OpenScope("owned local request"); owned.BlockInput(InputChannels.Interaction);
            using (var foreign = controls.OpenScope("foreign local request")) using (var block = foreign.BlockInput(InputChannels.Navigation))
            {
                game.Component.enabled = false;
                Assert.That(session.IsActive, Is.False); Assert.That(owned.IsActive, Is.False); Assert.That(block.IsActive, Is.True);
                Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(game.forest.followTarget, Is.Null);
                game.Component.enabled = true; Assert.That(game.Session.IsActive, Is.True); Assert.That(game.Session.Id, Is.Not.EqualTo(session.Id));
                var restarted = game.Session; Object.DestroyImmediate(game.Component);
                Assert.That(restarted.IsActive, Is.False); Assert.That(block.IsActive, Is.True);
            }
        }
        [UnityTest] public IEnumerator CloseDuringActualSequencePreflightReturnsTheStartedChildWithoutReentry()
        {
            OpenScene(); yield return new EnterPlayMode(); yield return null; yield return null;
            Assert.That(Application.isPlaying, Is.True, "Game lifetime assertions require actual Play mode.");
            var game = new GameView(Object.FindAnyObjectByType<CombatArena>()); var session = game.Session;
            var owner = new GameObject("ClosingSessionSequence"); var host = owner.AddComponent<SequenceHost>();
            try
            {
                var wait = new SequenceDefinition("closing-session", new[] { SequenceStep.Wait("wait", 10) }, Array.Empty<string>());
                var bindings = new ClosingBindings(() => session.Close("Disabled"));
                Assert.That(session.TryStartSequence(session.Id, host, wait, bindings, new SequenceControlPolicy(InputChannels.Interaction, true), new Clock(), out _), Is.False);
                Assert.That(session.IsActive, Is.False); Assert.That(host.Current.Read().Status, Is.EqualTo(SequenceStatus.Cancelled));
                Assert.That(game.combat.Controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(game.combat.Controls.Current.SimulationPaused, Is.False);
            }
            finally { host.Close(); Object.Destroy(owner); }
        }
        sealed class ClosingBindings : ISequenceBindings
        {
            readonly Action close;
            public ClosingBindings(Action close) { this.close = close; }
            public bool Validate(SequenceDefinition definition, out string error) { close(); error = null; return true; }
            public ISequenceBindingScope OpenScope(Guid execution, SequenceDefinition definition) => new WaitScope();
            sealed class WaitScope : ISequenceBindingScope
            {
                public bool IsActive { get; private set; } = true;
                public ISequenceOperation Begin(SequenceStep step) => throw new InvalidOperationException("Wait is runner-owned.");
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) => throw new InvalidOperationException("No skip.");
                public void Dispose() { IsActive = false; }
            }
        }
        // Game/forest remain in the existing default Unity assembly. Like the
        // established GameIntegrationTests, cross that boundary only here; all
        // assertions above exercise live instances, scopes and real executions.
        sealed class GameView
        {
            public readonly MonoBehaviour Component;
            public readonly CombatArena combat;
            public readonly ForestView forest;
            public SessionView Session => new SessionView(Component.GetType().GetProperty("Session").GetValue(Component));
            public GameView(CombatArena arena)
            {
                combat = arena; Component = (MonoBehaviour)arena.GetComponent("GameComposition");
                forest = new ForestView((UnityEngine.Component)Component.GetType().GetField("forest").GetValue(Component));
            }
        }
        sealed class ForestView
        {
            readonly Component world;
            public ForestView(Component world) { this.world = world; }
            public Transform followTarget => (Transform)world.GetType().GetField("followTarget").GetValue(world);
            public int ActiveCount => (int)world.GetType().GetProperty("ActiveCount").GetValue(world);
            public StreamingWindow CurrentWindow => (StreamingWindow)world.GetType().GetProperty("CurrentWindow").GetValue(world);
        }
        sealed class SessionView
        {
            readonly object session;
            T Read<T>(string property) => (T)session.GetType().GetProperty(property).GetValue(session);
            public Guid Id => Read<Guid>("Id");
            public bool IsActive => Read<bool>("IsActive");
            public string EndReason => Read<object>("EndReason").ToString();
            public IWeaponLoadout Loadout => Read<IWeaponLoadout>("Loadout");
            public IControlRequests Controls => Read<IControlRequests>("Controls");
            public ForestCapability Forest => new ForestCapability(Read<object>("Forest"));
            public ScreenRouter Screens => Read<ScreenRouter>("Screens");
            public SessionView(object session) { this.session = session; }
            bool Invoke(string method, object[] arguments, int errorIndex, out string error)
            {
                bool accepted = (bool)session.GetType().GetMethod(method).Invoke(session, arguments); error = (string)arguments[errorIndex]; return accepted;
            }
            public bool TryConfigureDialogue(Guid id, NpcDialogueHost host, IInteractionTargets targets, INarrativeConditions conditions,
                InteractionPolicy policy, IDialogueView view, IDialogueText text, DialoguePresentationPolicy display, out string error)
                => Invoke("TryConfigureDialogue", new object[] { id, host, targets, conditions, policy, view, text, display, null, null, null }, 8, out error);
            public bool TryInteract(Guid id, NpcDialogueHost host, NpcTarget target, out string error)
                => Invoke("TryInteract", new object[] { id, host, target, null }, 3, out error);
            public bool TryStartSequence(Guid id, SequenceHost host, SequenceDefinition definition, ISequenceBindings bindings,
                SequenceControlPolicy policy, ISequenceClock clock, out string error)
                => Invoke("TryStartSequence", new object[] { id, host, definition, bindings, policy, clock, null }, 6, out error);
            public void Close(string reason)
            {
                var method = session.GetType().GetMethod("Close"); method.Invoke(session, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, reason) });
            }
        }
        sealed class ForestCapability
        {
            readonly object forest;
            public ForestCapability(object forest) { this.forest = forest; }
            public bool HasValidatedChapterLayout => (bool)forest.GetType().GetProperty("HasValidatedChapterLayout").GetValue(forest);
            public string ChapterLayoutLimitation => (string)forest.GetType().GetProperty("ChapterLayoutLimitation").GetValue(forest);
        }
        sealed class Clock : ISequenceClock { public double ReadTime() => 0; }
        sealed class WaitBindings : ISequenceBindings
        {
            public bool Validate(SequenceDefinition definition, out string error) { error = null; return true; }
            public ISequenceBindingScope OpenScope(Guid execution, SequenceDefinition definition) => new Scope();
            sealed class Scope : ISequenceBindingScope
            {
                public bool IsActive { get; private set; } = true;
                public ISequenceOperation Begin(SequenceStep step) => throw new InvalidOperationException("Wait is runner-owned.");
                public ISequenceOperation BeginSkip(SequenceSkipDefinition skip) => throw new InvalidOperationException("No skip is declared.");
                public void Dispose() { IsActive = false; }
            }
        }
        sealed class Targets : IInteractionTargets
        {
            readonly Guid world = Guid.NewGuid();
            readonly NpcDefinition definition = new NpcDefinition("technical.session.npc", new DialogueDefinition("technical.session.dialogue", "hold", new[] { new DialogueLine("hold", "speaker", "body") }));
            public NpcTarget Target() => new NpcTarget(world, "technical.session.placement", definition, 1, false);
            public System.Collections.Generic.IReadOnlyList<NpcTarget> ReadTargets() => new[] { Target() };
            public bool TryRead(Guid id, string placement, out NpcTarget target) { target = id == world && placement == "technical.session.placement" ? Target() : null; return target != null; }
        }
        sealed class Conditions : INarrativeConditions { public bool Evaluate(string condition, Guid world, string placement) => true; }
        sealed class Text : IDialogueText { public bool TryRead(string key, out string text) { text = key; return true; } }
        sealed class View : IDialogueView
        {
            public Guid Bound;
            public event Action<DialogueIntent> Input { add { } remove { } }
            public bool TryBind(Guid id, out string error) { Bound = id; error = null; return true; }
            public bool TryApply(DialogueDisplay display) => display.BindingId == Bound;
            public void Unbind(Guid id) { if (id == Bound) Bound = Guid.Empty; }
        }
    }
}
