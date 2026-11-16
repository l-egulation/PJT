using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using NUnit.Framework;

namespace Deviant.Narrative.Tests
{
    public class DialogueContractTests
    {
        sealed class Context : IDialogueContext
        {
            public Action EvaluateCallback;
            public DialogueTargetState CheckTarget() => DialogueTargetState.Available;
            public bool Evaluate(string id) { EvaluateCallback?.Invoke(); return true; }
        }
        static InteractionPolicy Policy(bool recheck = false, bool cancel = true) => new InteractionPolicy(2, false, recheck, cancel);
        static IDialogueCommands Start(InteractionService service)
        {
            var candidates = service.Query(out var error); Assert.That(error, Is.Null); Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(service.TryStart(candidates[0], out var session, out error), Is.True, error); return session;
        }
        [Test] public void NarrativeAndPresenterRuntimeHaveNoEngineOrCombatReference()
        {
            foreach (var assembly in new[] { typeof(InteractionService).Assembly, typeof(IDialogueCommands).Assembly, typeof(DialoguePresenter).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat")), Is.False);
        }
        [Test] public void DefinitionsAreImmutableAndRejectCyclesOrDisconnectedLines()
        {
            var a = new DialogueLine("a", "speaker", "text", "b"); var b = new DialogueLine("b", "speaker", "text-b"); var lines = new[] { b, a };
            var definition = new DialogueDefinition("d", "a", lines); lines[0] = a;
            Assert.That(definition.Lines.Count, Is.EqualTo(2)); Assert.That(definition.Find("b"), Is.SameAs(b));
            Assert.Throws<NotSupportedException>(() => ((IList<DialogueLine>)definition.Lines)[0] = a);
            Assert.That(definition.ContentHash, Is.EqualTo(new DialogueDefinition("d", "a", new[] { a, b }).ContentHash));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { a, a }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { new DialogueLine("a", "speaker", "text", "a") }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { new DialogueLine("a", "speaker", "text"), b }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "missing", new[] { b }));
        }
        [Test] public void AdvanceUsesSessionNodeAndRevisionAndOnlyTheLastLineCompletes()
        {
            var targets = new DialogueFixtures.Targets(); using (var service = new InteractionService(targets, new DialogueFixtures.Conditions(), Policy()))
            {
                var session = Start(service); var a = session.Read();
                Assert.That(session.End(a.Token.SessionId, DialogueEndReason.Completed, out _), Is.False);
                Assert.That(session.TryAdvance(a.Token, out var error), Is.True, error); Assert.That(session.Read().Token.LineId, Is.EqualTo("b"));
                Assert.That(a.Token.LineId, Is.EqualTo("a")); Assert.That(session.TryAdvance(a.Token, out _), Is.False);
                Assert.That(session.TryAdvance(new DialogueToken(Guid.NewGuid(), "b", 1), out _), Is.False);
                Assert.That(session.TryAdvance(session.Read().Token, out error), Is.True, error); Assert.That(session.TryAdvance(session.Read().Token, out error), Is.True, error);
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed)); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.Completed));
                Assert.That(session.TryCancel(session.Read().Token, out _), Is.False);
                var next = Start(service); Assert.That(next.Read().Token.SessionId, Is.Not.EqualTo(a.Token.SessionId));
                Assert.That(next.TryAdvance(a.Token, out _), Is.False);
            }
        }
        [TestCase("distance")] [TestCase("combat")] [TestCase("condition")] [TestCase("version")] [TestCase("world")] [TestCase("gone")]
        public void InteractionRechecksEligibilityAfterCandidateDisplay(string change)
        {
            var targets = new DialogueFixtures.Targets(); var conditions = new DialogueFixtures.Conditions();
            using (var service = new InteractionService(targets, conditions, Policy()))
            {
                var selected = service.Query(out _)[0];
                switch (change)
                {
                    case "distance": targets.Distance = 3; break;
                    case "combat": targets.InCombat = true; break;
                    case "condition": conditions.Rejected = "npc"; break;
                    case "version": targets.Npc = new NpcDefinition("fixture-npc", DialogueFixtures.Definition("-edited"), "npc"); break;
                    case "world": targets.World = Guid.NewGuid(); break;
                    case "gone": targets.Exists = false; break;
                }
                Assert.That(service.TryStart(selected, out var session, out var error), Is.False); Assert.That(session, Is.Null); Assert.That(error, Is.Not.Empty);
            }
        }
        [Test] public void StartingDefinitionIsRetainedAndLineConditionsAreRecheckedWithoutAdvancing()
        {
            var targets = new DialogueFixtures.Targets(); var conditions = new DialogueFixtures.Conditions();
            using (var service = new InteractionService(targets, conditions, Policy()))
            {
                var session = Start(service); var initial = session.Read();
                targets.Npc = new NpcDefinition(targets.Npc.Id, DialogueFixtures.Definition("-new"), "npc"); conditions.Rejected = "next";
                Assert.That(session.TryAdvance(initial.Token, out _), Is.False); Assert.That(session.Read(), Is.SameAs(initial));
                conditions.Rejected = "entry"; Assert.That(session.TryAdvance(initial.Token, out _), Is.False);
                conditions.Rejected = null; Assert.That(session.TryAdvance(initial.Token, out var error), Is.True, error);
                Assert.That(session.Read().TextKey, Is.EqualTo("fixture.b")); Assert.That(session.Read().DefinitionVersion, Is.EqualTo(initial.DefinitionVersion));
                conditions.Throw = true; session.TryAdvance(session.Read().Token, out _);
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Failed)); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.ContextFailed));
            }
        }
        [Test] public void TargetLossEligibilityAndPlayerCancelHaveDistinctResults()
        {
            var targets = new DialogueFixtures.Targets();
            using (var service = new InteractionService(targets, new DialogueFixtures.Conditions(), Policy(true, false)))
            {
                var session = Start(service); Assert.That(session.TryCancel(session.Read().Token, out _), Is.False);
                targets.Distance = 3; service.Poll(); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.EligibilityLost));
                targets.Distance = 1; var next = Start(service); targets.Exists = false; service.Poll();
                Assert.That(next.Read().Status, Is.EqualTo(DialogueStatus.Cancelled)); Assert.That(next.Read().EndReason, Is.EqualTo(DialogueEndReason.TargetLost));
            }
            targets.Exists = true;
            using (var service = new InteractionService(targets, new DialogueFixtures.Conditions(), Policy(false, true)))
            {
                var session = Start(service); targets.Distance = 3; service.Poll(); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Active));
                Assert.That(session.TryCancel(session.Read().Token, out _), Is.True); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.PlayerCancelled));
            }
        }
        [Test] public void ContextCallbacksCannotReenterProgressCancelOrDisposal()
        {
            var context = new Context(); var target = new DialogueFixtures.Targets().Target();
            Assert.That(DialogueSession.TryStart(target, context, true, out var session, out var error), Is.True, error);
            using (session)
            {
                var expected = session.Read().Token; int rejected = 0; var exceptions = new List<Exception>();
                context.EvaluateCallback = () => {
                    if (!session.TryAdvance(expected, out _)) rejected++;
                    if (!session.TryCancel(expected, out _)) rejected++;
                    if (!session.End(expected.SessionId, DialogueEndReason.ViewClosed, out _)) rejected++;
                    try { session.Dispose(); } catch (Exception failure) { exceptions.Add(failure); }
                };
                Assert.That(session.TryAdvance(expected, out error), Is.True, error);
                Assert.That(rejected, Is.EqualTo(6)); Assert.That(exceptions.Count, Is.EqualTo(2));
                Assert.That(exceptions.All(e => e is InvalidOperationException), Is.True); Assert.That(session.Read().Token.LineId, Is.EqualTo("b"));
                Assert.That(session.Read().Token.Revision, Is.EqualTo(1));
            }
        }
        [Test] public void PresenterUsesDisplayedTokenAndReleasesOnlyItsOwnControl()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("pause/modal"))
            using (var service = new InteractionService(new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(), Policy()))
            {
                var pause = external.PauseSimulation(); external.BlockInput(InputChannels.Interaction);
                var session = Start(service); var view = new DialogueFixtures.View();
                using (var presenter = new DialoguePresenter(session, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false)))
                {
                    var first = view.Intent(DialogueIntentKind.Advance); view.Emit(first); view.Emit(first);
                    Assert.That(session.Read().Token.LineId, Is.EqualTo("b")); Assert.That(view.Display.Token.LineId, Is.EqualTo("a"), "Refresh publishes the next token, not the click callback.");
                    presenter.Refresh(); var blocker = external.BlockInput(InputChannels.Dialogue); view.Emit(view.Intent(DialogueIntentKind.Advance));
                    Assert.That(session.Read().Token.LineId, Is.EqualTo("b")); blocker.Dispose();
                    view.Emit(view.Intent(DialogueIntentKind.Advance)); presenter.Refresh(); view.Emit(view.Intent(DialogueIntentKind.Advance));
                    Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed)); Assert.That(presenter.IsActive, Is.False);
                    Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.False); Assert.That(controls.Current.Blocks(InputChannels.Interaction), Is.True); Assert.That(controls.Current.SimulationPaused, Is.True);
                    view.Emit(first); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                }
                pause.Dispose();
            }
        }
        [Test] public void DisabledViewMissingTextAndStaleUnbindCannotAffectAnotherBinding()
        {
            using (var controls = new ControlSession()) using (var service = new InteractionService(new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(), Policy()))
            {
                var session = Start(service); var view = new DialogueFixtures.View();
                var presenter = new DialoguePresenter(session, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, true));
                var oldBinding = presenter.BindingId; var oldInput = view.Intent(DialogueIntentKind.Advance);
                view.Emit(view.Intent(DialogueIntentKind.ViewClosed)); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.ViewClosed));
                Assert.That(controls.Current.SimulationPaused, Is.False); presenter.Dispose();
                var next = Start(service);
                using (var newer = new DialoguePresenter(next, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false)))
                {
                    view.Unbind(oldBinding); view.Emit(oldInput); Assert.That(view.Bound, Is.EqualTo(newer.BindingId)); Assert.That(next.Read().Token.LineId, Is.EqualTo("a"));
                }
                var missing = Start(service);
                using (var failure = new DialoguePresenter(missing, view, new DialogueFixtures.Text { Missing = "fixture.a" }, controls, new DialoguePresentationPolicy(InputChannels.Combat, true)))
                {
                    Assert.That(missing.Read().Status, Is.EqualTo(DialogueStatus.Failed)); Assert.That(missing.Read().EndReason, Is.EqualTo(DialogueEndReason.PresentationFailed));
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.None)); Assert.That(controls.Current.SimulationPaused, Is.False);
                }
            }
        }
    }
}
