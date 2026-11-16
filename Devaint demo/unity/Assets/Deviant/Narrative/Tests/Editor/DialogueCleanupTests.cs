using System;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Narrative.Contracts;
using Deviant.UI.Contracts;
using Deviant.UI.Presentation;
using NUnit.Framework;

namespace Deviant.Narrative.Tests
{
    public class DialogueCleanupTests
    {
        sealed class BrokenView : IDialogueView
        {
            readonly DialogueFixtures.View view = new DialogueFixtures.View();
            public int Unbinds;
            public event Action<DialogueIntent> Input
            {
                add { view.Input += value; }
                remove { view.Input -= value; throw new InvalidOperationException("fixture unsubscribe failed"); }
            }
            public bool TryBind(Guid binding, out string error) => view.TryBind(binding, out error);
            public bool TryApply(DialogueDisplay display) => view.TryApply(display);
            public void Unbind(Guid binding) { view.Unbind(binding); Unbinds++; throw new InvalidOperationException("fixture unbind failed"); }
        }
        sealed class CleanupControls : IControlRequests
        {
            readonly ControlSession controls;
            public CleanupControls(ControlSession controls) { this.controls = controls; }
            public ControlState Current => controls.Current;
            public IControlScope OpenScope(string owner) => new Scope(controls.OpenScope(owner));
            sealed class Scope : IControlScope
            {
                readonly IControlScope scope;
                public Scope(IControlScope scope) { this.scope = scope; }
                public bool IsActive => scope.IsActive;
                public IControlLease BlockInput(InputChannels channels) => scope.BlockInput(channels);
                public IControlLease PauseSimulation() => scope.PauseSimulation();
                public void Dispose() { scope.Dispose(); throw new InvalidOperationException("fixture control cleanup failed"); }
            }
        }
        [Test] public void CompletedDialogueExposesEveryCleanupErrorAndStillAttemptsEveryRelease()
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("external modal"))
            using (var interactions = new InteractionService(new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(), new InteractionPolicy(2, true, false, true)))
            {
                var block = modal.BlockInput(InputChannels.Navigation); var pause = modal.PauseSimulation();
                Assert.That(interactions.TryStart(interactions.Query(out _)[0], out var dialogue, out var error), Is.True, error);
                var view = new BrokenView();
                using (var presenter = new DialoguePresenter(dialogue, view, new DialogueFixtures.Text(), new CleanupControls(controls), new DialoguePresentationPolicy(InputChannels.Combat, true)))
                {
                    while (dialogue.Read().Status == DialogueStatus.Active) Assert.That(dialogue.TryAdvance(dialogue.Read().Token, out error), Is.True, error);
                    presenter.Refresh(); Assert.That(dialogue.Read().Status, Is.EqualTo(DialogueStatus.Completed)); Assert.That(presenter.IsActive, Is.False);
                    Assert.That(presenter.CleanupError, Does.Contain("unsubscribe failed").And.Contain("control cleanup failed").And.Contain("unbind failed"));
                    Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(block.IsActive && pause.IsActive, Is.True);
                    presenter.Dispose(); Assert.That(view.Unbinds, Is.EqualTo(1));
                }
            }
        }
        [Test] public void RejectedInputErrorIsSeparateFromSuccessfulPresenterCleanup()
        {
            using (var controls = new ControlSession()) using (var modal = controls.OpenScope("external modal"))
            using (var interactions = new InteractionService(new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(), new InteractionPolicy(2, true, false, true)))
            {
                Assert.That(interactions.TryStart(interactions.Query(out _)[0], out var dialogue, out var error), Is.True, error);
                var view = new DialogueFixtures.View();
                using (var presenter = new DialoguePresenter(dialogue, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, false)))
                {
                    modal.BlockInput(InputChannels.Dialogue); view.Emit(view.Intent(DialogueIntentKind.Advance)); Assert.That(presenter.LastError, Is.Not.Null);
                    presenter.Dispose(); Assert.That(presenter.CleanupError, Is.Null); Assert.That(dialogue.Read().Status, Is.EqualTo(DialogueStatus.Cancelled));
                }
            }
        }
    }
}
