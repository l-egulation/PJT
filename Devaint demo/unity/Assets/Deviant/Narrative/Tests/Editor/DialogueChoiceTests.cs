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
    // Technical routes and text keys only; this fixture does not define story content.
    public static class DialogueChoiceFixtures
    {
        public static DialogueDefinition Definition(string suffix = "") => new DialogueDefinition("fixture-branches", "choose", new[] {
            new DialogueLine("choose", "fixture.speaker", "fixture.choose" + suffix, conditionId: "current", choices: new[] {
                new DialogueChoice("left", "fixture.choice.left" + suffix, "left", "left-enabled"),
                new DialogueChoice("right", "fixture.choice.right" + suffix, "right", "right-enabled") }),
            new DialogueLine("left", "fixture.speaker", "fixture.left" + suffix, "end", "destination"),
            new DialogueLine("right", "fixture.speaker", "fixture.right" + suffix, "end"),
            new DialogueLine("end", "fixture.speaker", "fixture.end" + suffix) });
        public static DialogueFixtures.Targets Targets() => new DialogueFixtures.Targets { Npc = new NpcDefinition("fixture-npc", Definition()) };
    }
    public class DialogueChoiceTests
    {
        sealed class Context : IDialogueContext
        {
            public string Rejected, ThrowOn;
            public readonly HashSet<string> RejectedConditions = new HashSet<string>();
            public DialogueTargetState Target = DialogueTargetState.Available;
            public Action Callback;
            public DialogueTargetState CheckTarget() => Target;
            public bool Evaluate(string condition)
            { Callback?.Invoke(); if (condition == ThrowOn) throw new InvalidOperationException("fixture condition failed"); return condition != Rejected && !RejectedConditions.Contains(condition); }
        }
        static DialogueSession Start(Context context)
        {
            Assert.That(DialogueSession.TryStart(DialogueChoiceFixtures.Targets().Target(), context, true, out var session, out var error), Is.True, error); return session;
        }
        [Test] public void BranchGraphAcceptsMergingPathsAndPreservesLinearHashesAndChoiceOrder()
        {
            var linear = new DialogueDefinition("linear", "a", new[] { new DialogueLine("b", "speaker", "end"), new DialogueLine("a", "speaker", "text", "b") });
            Assert.That(linear.ContentHash, Is.EqualTo("951324e519b304cc4899a14a7b74ab3e76e0ccb20f880e5062c2932941151497"), "Published pre-choice Player definition hash.");
            var original = DialogueChoiceFixtures.Definition(); var shuffled = new DialogueDefinition(original.Id, original.EntryId, original.Lines.Reverse().ToArray());
            Assert.That(shuffled.ContentHash, Is.EqualTo(original.ContentHash)); Assert.That(original.Find("choose").Choices.Select(c => c.Id), Is.EqualTo(new[] { "left", "right" }));
            var reversedLine = new DialogueLine("choose", "fixture.speaker", "fixture.choose", conditionId: "current", choices: original.Find("choose").Choices.Reverse().ToArray());
            Assert.That(new DialogueDefinition(original.Id, original.EntryId, original.Lines.Select(l => l.Id == "choose" ? reversedLine : l).ToArray()).ContentHash, Is.Not.EqualTo(original.ContentHash));
            Assert.That(DialogueChoiceFixtures.Definition("-edited").ContentHash, Is.Not.EqualTo(original.ContentHash));
        }
        [Test] public void DefinitionsSnapshotsAndDisplaysCopyChoiceArraysAndExposeReadOnlyLists()
        {
            var choice = new DialogueChoice("a", "choice.a", "end"); var array = new[] { choice };
            var line = new DialogueLine("start", "speaker", "body", choices: array); array[0] = new DialogueChoice("b", "choice.b", "end");
            Assert.That(line.Choices[0], Is.SameAs(choice)); Assert.Throws<NotSupportedException>(() => ((IList<DialogueChoice>)line.Choices).Clear());
            using (var session = Start(new Context()))
            {
                var before = session.Read(); Assert.Throws<NotSupportedException>(() => ((IList<DialogueChoiceSnapshot>)before.Choices).Clear());
                Assert.That(session.TrySelectChoice(before.Token, "left", out _), Is.True); Assert.That(before.Choices.Count, Is.EqualTo(2));
                var options = new[] { new DialogueChoiceDisplay("a", "A", true) }; var display = new DialogueDisplay(Guid.NewGuid(), before.Token, "S", "B", true, options); options[0] = null;
                Assert.That(display.Choices[0].Text, Is.EqualTo("A")); Assert.Throws<NotSupportedException>(() => ((IList<DialogueChoiceDisplay>)display.Choices).Clear());
            }
        }
        [Test] public void AmbiguousMissingDuplicateUnreachableAndCyclicBranchesAreRejected()
        {
            var end = new DialogueLine("end", "speaker", "end");
            Assert.Throws<ArgumentException>(() => new DialogueChoice("", "key", "end"));
            Assert.Throws<ArgumentException>(() => new DialogueChoice("a", "key", null));
            Assert.Throws<ArgumentException>(() => new DialogueLine("a", "s", "t", "end", choices: new[] { new DialogueChoice("a", "key", "end") }));
            Assert.Throws<ArgumentException>(() => new DialogueLine("a", "s", "t", choices: new[] { new DialogueChoice("a", "key", "end"), new DialogueChoice("a", "other", "end") }));
            Assert.Throws<ArgumentException>(() => new DialogueLine("a", "s", "t", choices: new DialogueChoice[] { null }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { new DialogueLine("a", "s", "t", choices: new[] { new DialogueChoice("a", "key", "missing") }), end }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { new DialogueLine("a", "s", "t", choices: new[] { new DialogueChoice("a", "key", "a"), new DialogueChoice("b", "key", "end") }), end }));
            Assert.Throws<ArgumentException>(() => new DialogueDefinition("d", "a", new[] { new DialogueLine("a", "s", "t"), end }));
        }
        [TestCase("left")] [TestCase("right")] public void ExplicitChoiceSelectsOnePathAndOldOrDuplicateInputCannotSelectAnother(string route)
        {
            using (var session = Start(new Context()))
            {
                var before = session.Read(); Assert.That(session.TryAdvance(before.Token, out _), Is.False);
                Assert.That(session.TrySelectChoice(before.Token, "missing", out _), Is.False); Assert.That(session.Read(), Is.SameAs(before));
                Assert.That(session.TrySelectChoice(new DialogueToken(Guid.NewGuid(), "choose", 0), route, out _), Is.False);
                Assert.That(session.TrySelectChoice(before.Token, route, out var error), Is.True, error);
                Assert.That(session.Read().Token.LineId, Is.EqualTo(route)); Assert.That(session.Read().Choices.Count, Is.Zero);
                Assert.That(session.TrySelectChoice(before.Token, route == "left" ? "right" : "left", out _), Is.False);
                Assert.That(session.TryAdvance(session.Read().Token, out error), Is.True, error); Assert.That(session.Read().Token.LineId, Is.EqualTo("end"));
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Active)); Assert.That(session.TryAdvance(session.Read().Token, out _), Is.True);
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed)); Assert.That(session.TrySelectChoice(before.Token, route, out _), Is.False);
            }
        }
        [TestCase("current")] [TestCase("left-enabled")] [TestCase("destination")]
        public void SelectionRechecksCurrentChoiceAndDestinationBeforeAnyProgress(string rejected)
        {
            var context = new Context(); using (var session = Start(context))
            {
                var before = session.Read(); context.Rejected = rejected;
                Assert.That(session.TrySelectChoice(before.Token, "left", out var error), Is.False); Assert.That(error, Is.Not.Empty); Assert.That(session.Read(), Is.SameAs(before));
                context.Rejected = null; Assert.That(session.TrySelectChoice(before.Token, "left", out error), Is.True, error);
            }
        }
        [Test] public void PollChangesOnlyTheChoiceAvailabilityRevisionAndDoesNotChooseADefault()
        {
            var context = new Context { Rejected = "left-enabled" }; using (var session = Start(context))
            {
                var first = session.Read(); Assert.That(first.Choices.Select(c => c.IsAvailable), Is.EqualTo(new[] { false, true })); session.Poll(); Assert.That(session.Read(), Is.SameAs(first));
                context.Rejected = "right-enabled"; session.Poll(); var second = session.Read();
                Assert.That(second.Token.LineId, Is.EqualTo("choose")); Assert.That(second.Token.Revision, Is.EqualTo(first.Token.Revision + 1)); Assert.That(second.Choices.Select(c => c.IsAvailable), Is.EqualTo(new[] { true, false }));
                Assert.That(first.Choices.Select(c => c.IsAvailable), Is.EqualTo(new[] { false, true })); Assert.That(session.TrySelectChoice(first.Token, "left", out _), Is.False);
                context.Rejected = null; session.Poll(); Assert.That(session.TrySelectChoice(second.Token, "left", out _), Is.False);
                Assert.That(session.TrySelectChoice(session.Read().Token, "right", out _), Is.True);
            }
        }
        [TestCase(false)] [TestCase(true)] public void ChoiceConditionExceptionsFailWithoutEnteringAnyDestination(bool duringPoll)
        {
            var context = new Context(); using (var session = Start(context))
            {
                var token = session.Read().Token; context.ThrowOn = "left-enabled";
                if (duringPoll) session.Poll(); else Assert.That(session.TrySelectChoice(token, "left", out _), Is.False);
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Failed)); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.ContextFailed));
                Assert.That(session.Read().Token.LineId, Is.EqualTo("choose")); Assert.That(session.Read().Choices.Count, Is.Zero);
            }
        }
        [Test] public void NoAvailableChoiceWaitsForExplicitInputRatherThanCompletingOrChoosingAFallback()
        {
            var context = new Context(); context.RejectedConditions.UnionWith(new[] { "left-enabled", "right-enabled" });
            using (var session = Start(context))
            {
                var first = session.Read(); session.Poll(); Assert.That(session.Read(), Is.SameAs(first)); Assert.That(first.Choices.All(c => !c.IsAvailable), Is.True);
                Assert.That(session.TryAdvance(first.Token, out _), Is.False); Assert.That(session.TrySelectChoice(first.Token, "left", out _), Is.False);
                Assert.That(session.TrySelectChoice(first.Token, "right", out _), Is.False); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Active));
                Assert.That(session.TryCancel(first.Token, out _), Is.True); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.PlayerCancelled));
            }
        }
        [TestCase(false)] [TestCase(true)] public void EnteringAnotherChoiceBuildsItsSnapshotBeforeCommittingTheTransition(bool fail)
        {
            var definition = new DialogueDefinition("fixture-two-choices", "a", new[] {
                new DialogueLine("a", "speaker", "body-a", choices: new[] { new DialogueChoice("same", "label-a", "b") }),
                new DialogueLine("b", "speaker", "body-b", choices: new[] { new DialogueChoice("same", "label-b", "end", "nested-enabled") }),
                new DialogueLine("end", "speaker", "body-end") });
            var context = new Context(); var target = new NpcTarget(Guid.NewGuid(), "placement", new NpcDefinition("npc", definition), 0, false);
            Assert.That(DialogueSession.TryStart(target, context, true, out var session, out _), Is.True);
            using (session)
            {
                var token = session.Read().Token; if (fail) context.ThrowOn = "nested-enabled";
                Assert.That(session.TrySelectChoice(token, "same", out _), Is.EqualTo(!fail));
                if (fail) { Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Failed)); Assert.That(session.Read().Token.LineId, Is.EqualTo("a")); }
                else
                {
                    Assert.That(session.Read().Token.LineId, Is.EqualTo("b")); Assert.That(session.Read().Choices[0].TextKey, Is.EqualTo("label-b"));
                    Assert.That(session.TrySelectChoice(token, "same", out _), Is.False); Assert.That(session.TrySelectChoice(session.Read().Token, "same", out _), Is.True);
                }
            }
        }
        [Test] public void AChoiceProviderFailureAtStartReturnsNoPartiallyCreatedSession()
        {
            Assert.That(DialogueSession.TryStart(DialogueChoiceFixtures.Targets().Target(), new Context { ThrowOn = "left-enabled" }, true, out var session, out var error), Is.False);
            Assert.That(session, Is.Null); Assert.That(error, Does.Contain("fixture condition failed"));
        }
        [Test] public void ChoiceConditionCallbacksCannotReenterSelectionAdvanceCancelOrDisposal()
        {
            var context = new Context(); using (var session = Start(context))
            {
                var token = session.Read().Token; int rejected = 0; context.Callback = () => {
                    if (!session.TrySelectChoice(token, "right", out _)) rejected++; if (!session.TryAdvance(token, out _)) rejected++;
                    if (!session.TryCancel(token, out _)) rejected++; if (!session.End(token.SessionId, DialogueEndReason.OwnerDisposed, out _)) rejected++;
                    Assert.Throws<InvalidOperationException>(() => session.Dispose());
                };
                Assert.That(session.TrySelectChoice(token, "left", out _), Is.True); Assert.That(rejected, Is.EqualTo(12)); Assert.That(session.Read().Token.LineId, Is.EqualTo("left"));
            }
        }
        [Test] public void LostTargetsAndClosedSessionsRejectChoiceTokensWithoutCreatingCompletion()
        {
            var context = new Context(); using (var session = Start(context))
            {
                var token = session.Read().Token; context.Target = DialogueTargetState.Missing;
                Assert.That(session.TrySelectChoice(token, "left", out _), Is.False); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.TargetLost));
                Assert.That(session.TrySelectChoice(token, "right", out _), Is.False); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Cancelled));
            }
            using (var session = Start(new Context())) { var token = session.Read().Token; session.End(token.SessionId, DialogueEndReason.RunEnded, out _); Assert.That(session.TrySelectChoice(token, "left", out _), Is.False); }
        }
        [Test] public void PresenterKeepsDisplayedTokensAndExternalControlWhileChoosingAndFinishing()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture modal"))
            using (var service = new InteractionService(DialogueChoiceFixtures.Targets(), new DialogueFixtures.Conditions(), new InteractionPolicy(2, false, false, true)))
            {
                var pause = external.PauseSimulation(); var other = external.BlockInput(InputChannels.Interaction);
                Assert.That(service.TryStart(service.Query(out _)[0], out var session, out var error), Is.True, error); var view = new DialogueFixtures.View();
                using (var presenter = new DialoguePresenter(session, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, true)))
                {
                    Assert.That(view.Display.CanAdvance, Is.False); Assert.That(view.Display.Choices.Select(c => c.Text), Is.EqualTo(new[] { "fixture.choice.left", "fixture.choice.right" }));
                    var input = new DialogueIntent(view.Bound, view.Display.Token, DialogueIntentKind.SelectChoice, "left"); var restriction = external.BlockInput(InputChannels.Dialogue);
                    view.Emit(input); Assert.That(session.Read().Token.LineId, Is.EqualTo("choose")); restriction.Dispose();
                    view.Emit(input); view.Emit(input); Assert.That(session.Read().Token.LineId, Is.EqualTo("left")); Assert.That(view.Display.Token.LineId, Is.EqualTo("choose"));
                    presenter.Refresh(); view.Emit(view.Intent(DialogueIntentKind.Advance)); presenter.Refresh(); view.Emit(view.Intent(DialogueIntentKind.Advance));
                    Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed)); Assert.That(presenter.IsActive, Is.False);
                    Assert.That(other.IsActive && pause.IsActive, Is.True); Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.False);
                }
            }
        }
        [Test] public void MissingChoiceTextFailsAndReturnsOnlyThePresentersControl()
        {
            using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture other")) using (var session = Start(new Context()))
            {
                var lease = external.BlockInput(InputChannels.Interaction); var view = new DialogueFixtures.View();
                using (var presenter = new DialoguePresenter(session, view, new DialogueFixtures.Text { Missing = "fixture.choice.right" }, controls, new DialoguePresentationPolicy(InputChannels.Combat, true)))
                {
                    Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.PresentationFailed)); Assert.That(presenter.IsActive, Is.False);
                    Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(lease.IsActive, Is.True); Assert.That(view.Bound, Is.EqualTo(Guid.Empty));
                }
            }
        }
    }
}
