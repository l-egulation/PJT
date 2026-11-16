using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Definitions;
using Deviant.Narrative.Contracts;

namespace Deviant.Narrative
{
    // Composition registers finite command IDs and translates them to domain
    // APIs. This registry owns execution records, never inventory/world state.
    public sealed class DialogueCommandBindings : IDialogueCommandBindings, IDisposable
    {
        readonly Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> handlers;
        readonly HashSet<Scope> scopes = new HashSet<Scope>();
        bool disposed;
        public int ActiveScopeCount => scopes.Count;
        public DialogueCommandBindings(IDictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>> handlers)
        {
            if (handlers == null || handlers.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null)) throw new ArgumentException("Registered IDs and handlers are required.", nameof(handlers));
            this.handlers = new Dictionary<string, Func<DialogueCommandContext, DialogueCommandRequest, DialogueCommandResult>>(handlers, StringComparer.Ordinal);
        }
        public bool Validate(DialogueDefinition definition, out string error)
        {
            error = null;
            if (disposed || definition == null) { error = "Dialogue command bindings are unavailable."; return false; }
            foreach (var id in definition.CommandIds) if (!handlers.ContainsKey(id)) { error = "Missing dialogue command binding: " + id; return false; }
            return true;
        }
        public IDialogueCommandScope OpenScope(DialogueCommandContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Validate(context.Definition, out var error)) throw new InvalidOperationException(error);
            if (scopes.Any(s => s.Context.SessionId == context.SessionId)) throw new InvalidOperationException("This dialogue session already owns a command scope.");
            var scope = new Scope(this, context); scopes.Add(scope); return scope;
        }
        public void Dispose()
        {
            if (disposed) return;
            if (scopes.Any(s => s.Busy)) throw new InvalidOperationException("Command callbacks cannot dispose their binding owner.");
            disposed = true; foreach (var scope in scopes.ToArray()) scope.Dispose();
        }
        sealed class Scope : IDialogueCommandScope
        {
            readonly DialogueCommandBindings owner;
            readonly Dictionary<string, DialogueCommandReceipt> terminal = new Dictionary<string, DialogueCommandReceipt>(StringComparer.Ordinal);
            bool closed, faulted;
            public DialogueCommandContext Context { get; }
            public bool Busy { get; private set; }
            public bool IsActive => !closed && !owner.disposed;
            public Scope(DialogueCommandBindings owner, DialogueCommandContext context) { this.owner = owner; Context = context; }
            public DialogueCommandResult Execute(DialogueCommandRequest request)
            {
                if (!IsActive || Busy || request == null || request.Token.SessionId != Context.SessionId)
                    return new DialogueCommandResult(DialogueCommandStatus.Rejected, "Stale, closed or reentrant dialogue command request.");
                var line = Context.Definition.Find(request.Token.LineId);
                var choice = line?.Choices.FirstOrDefault(c => c.Id == request.ChoiceId);
                if (choice == null || choice.CommandId != request.CommandId)
                    return new DialogueCommandResult(DialogueCommandStatus.Rejected, "Command does not match the starting dialogue choice.");
                if (terminal.TryGetValue(line.Id, out var previous))
                    return previous.Request.ChoiceId == request.ChoiceId && previous.Request.CommandId == request.CommandId ? previous.Result :
                        new DialogueCommandResult(DialogueCommandStatus.Rejected, "A different choice already committed at this line.");
                if (faulted) return new DialogueCommandResult(DialogueCommandStatus.Failed, "This command scope has a terminal failure or cancellation.");
                Busy = true;
                try
                {
                    DialogueCommandResult result;
                    try { result = owner.handlers[request.CommandId](Context, request) ?? throw new InvalidOperationException("Command handler returned no result."); }
                    catch (Exception exception) { result = new DialogueCommandResult(DialogueCommandStatus.Failed, string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message); }
                    if (result.Status != DialogueCommandStatus.Rejected) terminal.Add(line.Id, new DialogueCommandReceipt(request, result));
                    if (result.Status == DialogueCommandStatus.Failed || result.Status == DialogueCommandStatus.Cancelled) faulted = true;
                    return result;
                }
                finally { Busy = false; }
            }
            public void Dispose()
            {
                if (closed) return;
                if (Busy) throw new InvalidOperationException("Command callbacks cannot dispose their execution scope.");
                closed = true; terminal.Clear(); owner.scopes.Remove(this);
            }
        }
    }
}
