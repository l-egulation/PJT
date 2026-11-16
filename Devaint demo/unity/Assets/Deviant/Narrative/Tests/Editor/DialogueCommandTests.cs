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
    public static class DialogueCommandFixtures
    {
        // Registered technical action only. This does not grant content or rewards.
        public const string Action = "fixture.apply";
        public static DialogueDefinition Definition(string command = Action) => new DialogueDefinition("fixture-commands", "choose", new[] {
            new DialogueLine("choose", "fixture.speaker", "fixture.choose", conditionId: "current", choices: new[] {
                new DialogueChoice("apply", "fixture.apply", "result", "choice", command), new DialogueChoice("leave", "fixture.leave", "end") }),
            new DialogueLine("result", "fixture.speaker", "fixture.result", conditionId: "destination", choices: new[] { new DialogueChoice("finish", "fixture.finish", "end", "after") }),
            new DialogueLine("end", "fixture.speaker", "fixture.end") });
        public static DialogueFixtures.Targets Targets() => new DialogueFixtures.Targets { Npc = new NpcDefinition("fixture-npc", Definition()) };
        public sealed class WrappedBindings : IDialogueCommandBindings
        {
            readonly IDialogueCommandBindings inner;
            public bool ThrowOpen, ThrowCleanup, Inactive, NullScope, ThrowExecute, NullResult;
            public int Disposals;
            public System.Action OnDispose;
            public WrappedBindings(IDialogueCommandBindings inner) { this.inner = inner; }
            public bool Validate(DialogueDefinition definition, out string error) => inner.Validate(definition, out error);
            public IDialogueCommandScope OpenScope(DialogueCommandContext context)
            {
                if (ThrowOpen) throw new InvalidOperationException("fixture open failed");
                if (NullScope) return null;
                return new Scope(this, inner.OpenScope(context));
            }
            sealed class Scope : IDialogueCommandScope
            {
                readonly WrappedBindings owner; readonly IDialogueCommandScope inner;
                public Scope(WrappedBindings owner, IDialogueCommandScope inner) { this.owner = owner; this.inner = inner; }
                public bool IsActive => !owner.Inactive && inner.IsActive;
                public DialogueCommandResult Execute(DialogueCommandRequest request)
                {
                    if (owner.ThrowExecute) throw new InvalidOperationException("fixture provider threw");
                    return owner.NullResult ? null : inner.Execute(request);
                }
                public void Dispose() { owner.Disposals++; inner.Dispose(); owner.OnDispose?.Invoke(); if (owner.ThrowCleanup) throw new InvalidOperationException("fixture command cleanup failed"); }
            }
        }
    }
    public class DialogueCommandTests
    {
        sealed class Context : IDialogueContext
        {
            public string Reject, Throw;
            public bool After = true;
            public DialogueTargetState Target = DialogueTargetState.Available;
            public DialogueTargetState CheckTarget() => Target;
            public bool Evaluate(string id) { if (id == Throw) throw new InvalidOperationException("fixture context failed"); return id != Reject && (id != "after" || After); }
        }
        static DialogueCommandBindings Bind(Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult> handler) => new DialogueCommandBindings(
            new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> { { DialogueCommandFixtures.Action, handler } });
        static DialogueCommandResult Result(DialogueCommandStatus status) => new DialogueCommandResult(status, status == DialogueCommandStatus.Succeeded ? null : "fixture " + status);
        static DialogueSession Start(IDialogueCommandBindings bindings, Context context = null)
        {
            Assert.That(DialogueSession.TryStart(DialogueCommandFixtures.Targets().Target(), context ?? new Context(), true, out var session, out var error, bindings), Is.True, error); return session;
        }
        static void Complete(DialogueSession session)
        {
            Assert.That(session.TrySelectChoice(session.Read().Token, "finish", out _), Is.True); Assert.That(session.TryAdvance(session.Read().Token, out _), Is.True);
        }
        [Test] public void CommandReferencesAreImmutableHashedAndDoNotChangeCommandFreeVersions()
        {
            var original = DialogueCommandFixtures.Definition(); Assert.That(original.CommandIds, Is.EqualTo(new[] { DialogueCommandFixtures.Action }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)original.CommandIds).Clear());
            Assert.That(DialogueCommandFixtures.Definition("fixture.other").ContentHash, Is.Not.EqualTo(original.ContentHash));
            Assert.That(DialogueCommandFixtures.Definition(null).ContentHash, Is.Not.EqualTo(original.ContentHash));
            Assert.That(DialogueChoiceFixtures.Definition().ContentHash, Is.EqualTo(new DialogueDefinition("fixture-branches", "choose", DialogueChoiceFixtures.Definition().Lines.Reverse().ToArray()).ContentHash));
            Assert.Throws<ArgumentException>(() => new DialogueChoice("choice", "text", "end", commandId: " "));
        }
        [Test] public void MissingCommandBindingsRejectBeforeOpeningOrExecutingAnyScope()
        {
            var target = DialogueCommandFixtures.Targets().Target();
            Assert.That(DialogueSession.TryStart(target, new Context(), true, out var missing, out _), Is.False); Assert.That(missing, Is.Null);
            using (var bindings = new DialogueCommandBindings(new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>>()))
            {
                Assert.That(DialogueSession.TryStart(target, new Context(), true, out missing, out var error, bindings), Is.False);
                Assert.That(error, Does.Contain(DialogueCommandFixtures.Action)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
            }
            Assert.That(DialogueSession.TryStart(new DialogueFixtures.Targets().Target(), new Context(), true, out var plain, out _), Is.True); plain.Dispose();
        }
        [TestCase("throw")] [TestCase("null")] [TestCase("inactive")] public void FailedScopeAcquisitionReturnsNoSessionAndReleasesPartialOwnership(string failure)
        {
            using (var bindings = Bind((c, r) => Result(DialogueCommandStatus.Succeeded)))
            {
                var wrapper = new DialogueCommandFixtures.WrappedBindings(bindings) { ThrowOpen = failure == "throw", NullScope = failure == "null", Inactive = failure == "inactive" };
                Assert.That(DialogueSession.TryStart(DialogueCommandFixtures.Targets().Target(), new Context(), true, out var session, out var error, wrapper), Is.False);
                Assert.That(session, Is.Null); Assert.That(error, Is.Not.Empty); Assert.That(bindings.ActiveScopeCount, Is.Zero);
                Assert.That(wrapper.Disposals, Is.EqualTo(failure == "inactive" ? 1 : 0));
            }
        }
        [Test] public void SuccessCommitsOnceAndDestinationChoicesObserveTheCommandResult()
        {
            var context = new Context { After = false }; int calls = 0; DialogueCommandContext observed = null;
            using (var bindings = Bind((c, r) => { calls++; observed = c; context.After = true; return Result(DialogueCommandStatus.Succeeded); })) using (var session = Start(bindings, context))
            {
                var before = session.Read(); Assert.That(session.TrySelectChoice(before.Token, "apply", out var error), Is.True, error);
                var after = session.Read(); Assert.That(after.Token.LineId, Is.EqualTo("result")); Assert.That(after.Choices[0].IsAvailable, Is.True);
                Assert.That(after.LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Succeeded)); Assert.That(after.LastCommand.Request.Token.SessionId, Is.EqualTo(before.Token.SessionId));
                Assert.That(observed.WorldSessionId, Is.EqualTo(before.WorldSessionId)); Assert.That(observed.PlacementId, Is.EqualTo(before.PlacementId)); Assert.That(observed.Definition.ContentHash, Is.EqualTo(before.DefinitionVersion));
                Assert.That(session.TrySelectChoice(before.Token, "apply", out _), Is.False); Assert.That(calls, Is.EqualTo(1)); Assert.That(bindings.ActiveScopeCount, Is.EqualTo(1));
                Complete(session); Assert.That(bindings.ActiveScopeCount, Is.Zero); Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Completed));
                Assert.That(session.Read().LastCommand, Is.SameAs(after.LastCommand)); Assert.That(session.Read().CleanupError, Is.Null);
            }
        }
        [Test] public void RejectionLeavesTheCurrentTokenAndCanRetryWithoutDuplicatingACommit()
        {
            int calls = 0, commits = 0; bool ready = false;
            using (var bindings = Bind((c, r) => { calls++; if (!ready) return Result(DialogueCommandStatus.Rejected); commits++; return Result(DialogueCommandStatus.Succeeded); })) using (var session = Start(bindings))
            {
                var token = session.Read().Token; Assert.That(session.TrySelectChoice(token, "apply", out var error), Is.False); Assert.That(error, Does.Contain("Rejected"));
                Assert.That(session.Read().Status, Is.EqualTo(DialogueStatus.Active)); Assert.That(session.Read().Token.Revision, Is.EqualTo(token.Revision)); Assert.That(session.Read().LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Rejected));
                ready = true; Assert.That(session.TrySelectChoice(token, "apply", out _), Is.True); Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False);
                Assert.That(calls, Is.EqualTo(2)); Assert.That(commits, Is.EqualTo(1));
            }
        }
        [TestCase(DialogueCommandStatus.Failed, DialogueStatus.Failed, DialogueEndReason.CommandFailed)]
        [TestCase(DialogueCommandStatus.Cancelled, DialogueStatus.Cancelled, DialogueEndReason.CommandCancelled)]
        public void TerminalCommandResultsEndWithoutEnteringTheDestination(DialogueCommandStatus result, DialogueStatus status, DialogueEndReason reason)
        {
            int calls = 0; using (var bindings = Bind((c, r) => { calls++; return Result(result); })) using (var session = Start(bindings))
            {
                var token = session.Read().Token; Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False);
                Assert.That(session.Read().Token.LineId, Is.EqualTo("choose")); Assert.That(session.Read().Status, Is.EqualTo(status)); Assert.That(session.Read().EndReason, Is.EqualTo(reason));
                Assert.That(session.Read().LastCommand.Result.Status, Is.EqualTo(result)); Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False);
                Assert.That(calls, Is.EqualTo(1)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
            }
        }
        [TestCase(false)] [TestCase(true)] public void ExceptionsAndMissingResultsFailWithoutAutomaticRetry(bool returnNull)
        {
            int calls = 0; using (var bindings = Bind((c, r) => { calls++; if (returnNull) return null; throw new InvalidOperationException("fixture command threw"); })) using (var session = Start(bindings))
            {
                var token = session.Read().Token; Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.CommandFailed));
                Assert.That(session.Read().LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Failed)); session.Poll(); Assert.That(calls, Is.EqualTo(1)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
            }
        }
        [TestCase(false)] [TestCase(true)] public void MisbehavingProviderFailuresPublishTheAttemptedCommandReceipt(bool returnNull)
        {
            using (var bindings = Bind((c, r) => Result(DialogueCommandStatus.Succeeded)))
            {
                var wrapper = new DialogueCommandFixtures.WrappedBindings(bindings) { ThrowExecute = !returnNull, NullResult = returnNull };
                using (var session = Start(wrapper))
                {
                    var token = session.Read().Token; Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False);
                    Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.CommandFailed)); Assert.That(session.Read().LastCommand, Is.Not.Null);
                    Assert.That(session.Read().LastCommand.Request.CommandId, Is.EqualTo(DialogueCommandFixtures.Action));
                    Assert.That(session.Read().LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Failed)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
                }
            }
        }
        [TestCase("current")] [TestCase("choice")] [TestCase("destination")] public void ConditionsAreRecheckedBeforeAnyCommandInvocation(string condition)
        {
            int calls = 0; var context = new Context(); using (var bindings = Bind((c, r) => { calls++; return Result(DialogueCommandStatus.Succeeded); })) using (var session = Start(bindings, context))
            {
                var before = session.Read(); context.Reject = condition; Assert.That(session.TrySelectChoice(before.Token, "apply", out _), Is.False);
                Assert.That(calls, Is.Zero); Assert.That(session.Read(), Is.SameAs(before)); context.Reject = null; Assert.That(session.TrySelectChoice(before.Token, "apply", out _), Is.True);
            }
        }
        [Test] public void AFailureReadingTheNextOptionsDoesNotUndoOrRepeatAnAlreadySucceededCommand()
        {
            int commits = 0; var context = new Context(); using (var bindings = Bind((c, r) => { commits++; context.Throw = "after"; return Result(DialogueCommandStatus.Succeeded); })) using (var session = Start(bindings, context))
            {
                var token = session.Read().Token; Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False);
                Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.ContextFailed)); Assert.That(session.Read().LastCommand.Result.Status, Is.EqualTo(DialogueCommandStatus.Succeeded));
                Assert.That(session.Read().Token.LineId, Is.EqualTo("choose")); Assert.That(session.TrySelectChoice(token, "apply", out _), Is.False); Assert.That(commits, Is.EqualTo(1)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
            }
        }
        [Test] public void RegistryCachesTerminalResultsAcrossRefreshRevisionsAndClosesWithoutUndo()
        {
            int calls = 0; var handlers = new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> { { DialogueCommandFixtures.Action, (c, r) => { calls++; return Result(DialogueCommandStatus.Succeeded); } } };
            using (var bindings = new DialogueCommandBindings(handlers))
            {
                handlers.Clear(); var context = new DialogueCommandContext(Guid.NewGuid(), Guid.NewGuid(), "placement", DialogueCommandFixtures.Definition()); var scope = bindings.OpenScope(context);
                var first = new DialogueCommandRequest(new DialogueToken(context.SessionId, "choose", 0), "apply", DialogueCommandFixtures.Action); var result = scope.Execute(first);
                Assert.That(scope.Execute(new DialogueCommandRequest(new DialogueToken(context.SessionId, "choose", 5), "apply", DialogueCommandFixtures.Action)), Is.SameAs(result)); Assert.That(calls, Is.EqualTo(1));
                Assert.That(scope.Execute(new DialogueCommandRequest(new DialogueToken(Guid.NewGuid(), "choose", 0), "apply", DialogueCommandFixtures.Action)).Status, Is.EqualTo(DialogueCommandStatus.Rejected));
                Assert.That(scope.Execute(new DialogueCommandRequest(first.Token, "missing", DialogueCommandFixtures.Action)).Status, Is.EqualTo(DialogueCommandStatus.Rejected));
                Assert.Throws<InvalidOperationException>(() => bindings.OpenScope(context)); scope.Dispose(); scope.Dispose(); Assert.That(bindings.ActiveScopeCount, Is.Zero);
                Assert.That(scope.Execute(first).Status, Is.EqualTo(DialogueCommandStatus.Rejected)); Assert.That(calls, Is.EqualTo(1));
                using (var next = bindings.OpenScope(new DialogueCommandContext(Guid.NewGuid(), context.WorldSessionId, context.PlacementId, context.Definition))) Assert.That(next.IsActive, Is.True);
            }
        }
        [Test] public void ACommittedLineCannotExecuteADifferentCommandChoice()
        {
            var definition = new DialogueDefinition("two-commands", "choose", new[] {
                new DialogueLine("choose", "speaker", "body", choices: new[] { new DialogueChoice("a", "a", "end", commandId: DialogueCommandFixtures.Action), new DialogueChoice("b", "b", "end", commandId: DialogueCommandFixtures.Action) }), new DialogueLine("end", "speaker", "end") });
            int calls = 0; using (var bindings = Bind((c, r) => { calls++; return Result(DialogueCommandStatus.Succeeded); }))
            {
                var context = new DialogueCommandContext(Guid.NewGuid(), Guid.NewGuid(), "placement", definition); using (var scope = bindings.OpenScope(context))
                {
                    var token = new DialogueToken(context.SessionId, "choose", 0); Assert.That(scope.Execute(new DialogueCommandRequest(token, "a", DialogueCommandFixtures.Action)).Status, Is.EqualTo(DialogueCommandStatus.Succeeded));
                    Assert.That(scope.Execute(new DialogueCommandRequest(token, "b", DialogueCommandFixtures.Action)).Status, Is.EqualTo(DialogueCommandStatus.Rejected)); Assert.That(calls, Is.EqualTo(1));
                }
            }
        }
        [Test] public void CommandCallbacksCannotReenterDialogueOrDisposeTheBindingOwner()
        {
            DialogueSession session = null; DialogueCommandBindings bindings = null; int rejected = 0;
            bindings = Bind((c, r) => {
                if (!session.TrySelectChoice(r.Token, "apply", out _)) rejected++; if (!session.TryCancel(r.Token, out _)) rejected++;
                if (!session.End(r.Token.SessionId, DialogueEndReason.SceneChanged, out _)) rejected++; Assert.Throws<InvalidOperationException>(() => session.Dispose());
                Assert.Throws<InvalidOperationException>(() => bindings.Dispose()); return Result(DialogueCommandStatus.Succeeded);
            });
            using (bindings) using (session = Start(bindings)) { Assert.That(session.TrySelectChoice(session.Read().Token, "apply", out _), Is.True); Assert.That(rejected, Is.EqualTo(3)); }
        }
        [TestCase(false)] [TestCase(true)] public void CleanupErrorsPreserveTheEndFactAndPresenterStillReturnsItsOwnViewAndControl(bool complete)
        {
            using (var bindings = Bind((c, r) => Result(DialogueCommandStatus.Succeeded))) using (var controls = new ControlSession()) using (var external = controls.OpenScope("fixture modal"))
            {
                var wrapper = new DialogueCommandFixtures.WrappedBindings(bindings) { ThrowCleanup = true }; using (var session = Start(wrapper))
                {
                    var view = new DialogueFixtures.View(); var block = external.BlockInput(InputChannels.Navigation); var pause = external.PauseSimulation();
                    using (var presenter = new DialoguePresenter(session, view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Combat, true)))
                    {
                        int reentries = 0; wrapper.OnDispose = () => { if (!session.End(session.Read().Token.SessionId, DialogueEndReason.RunEnded, out _)) reentries++; };
                        if (complete) { Assert.That(session.TrySelectChoice(session.Read().Token, "apply", out _), Is.True); Complete(session); }
                        else Assert.That(session.TryCancel(session.Read().Token, out _), Is.True);
                        presenter.Refresh(); Assert.That(session.Read().Status, Is.EqualTo(complete ? DialogueStatus.Completed : DialogueStatus.Cancelled));
                        Assert.That(session.Read().CleanupError, Does.Contain("command cleanup failed")); Assert.That(presenter.CleanupError, Does.Contain("command cleanup failed"));
                        Assert.That(presenter.IsActive, Is.False); Assert.That(view.Bound, Is.EqualTo(Guid.Empty)); Assert.That(block.IsActive && pause.IsActive, Is.True);
                        Assert.That(controls.Current.Blocked, Is.EqualTo(InputChannels.Navigation)); Assert.That(bindings.ActiveScopeCount, Is.Zero); Assert.That(wrapper.Disposals, Is.EqualTo(1)); Assert.That(reentries, Is.EqualTo(1));
                    }
                }
            }
        }
        [Test] public void BindingOwnerDisposalEndsActiveSessionsAndNeverAffectsAnotherOwnersScope()
        {
            using (var bindings = Bind((c, r) => Result(DialogueCommandStatus.Succeeded))) using (var session = Start(bindings))
            {
                bindings.Dispose(); session.Poll(); Assert.That(session.Read().EndReason, Is.EqualTo(DialogueEndReason.CommandFailed)); Assert.That(bindings.ActiveScopeCount, Is.Zero);
            }
            using (var a = Bind((c, r) => Result(DialogueCommandStatus.Succeeded))) using (var b = Bind((c, r) => Result(DialogueCommandStatus.Succeeded)))
            using (var first = Start(a)) using (var second = Start(b)) { first.Dispose(); Assert.That(a.ActiveScopeCount, Is.Zero); Assert.That(b.ActiveScopeCount, Is.EqualTo(1)); Assert.That(second.Read().Status, Is.EqualTo(DialogueStatus.Active)); }
        }
    }
}
