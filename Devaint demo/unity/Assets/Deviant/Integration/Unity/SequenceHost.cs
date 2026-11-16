using System;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Sequences;
using Deviant.Sequences.Contracts;
using UnityEngine;

namespace Deviant.Integration
{
    // Composition supplies the sequence, clock and policies explicitly. Merely
    // adding this host never starts content or chooses a skip/pause/key policy.
    public sealed class SequenceHost : MonoBehaviour
    {
        SequenceRunner runner;
        Commands current;
        bool executing, closeRequested;
        SequenceSourceBinding sourceBinding;
        public SequenceSourceBinding SourceBinding => sourceBinding;
        public ISequenceCommands Current => current;
        public bool TryConfigureSource(SequenceSourceBinding binding, out string error)
        {
            error = null;
            if (executing || !isActiveAndEnabled || runner != null && runner.Read().Status == SequenceStatus.Running || binding == null)
            { error = "A live, idle sequence host and explicit source binding are required."; return false; }
            executing = true;
            try
            {
                if (!binding.Bindings.Validate(binding.Source.Definition, out error)) return false;
                if (closeRequested || !this || !isActiveAndEnabled) { error = "Sequence host closed during source validation."; return false; }
                sourceBinding = binding; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally { executing = false; FlushClose(); }
        }
        public bool TryApplySource(CompiledSequenceSource candidate, out string error)
        {
            error = null;
            if (executing || !isActiveAndEnabled || sourceBinding == null || candidate == null) { error = "An active explicit source binding and complete candidate are required outside host callbacks."; return false; }
            if (candidate.Definition.Id != sourceBinding.Source.Definition.Id) { error = "A candidate must retain the stable sequence identity."; return false; }
            executing = true;
            try
            {
                if (!sourceBinding.Bindings.Validate(candidate.Definition, out error)) return false;
                if (closeRequested || !this || !isActiveAndEnabled) { error = "Sequence host closed during candidate validation."; return false; }
                sourceBinding = sourceBinding.WithSource(candidate); return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally { executing = false; FlushClose(); }
        }
        public bool TryApplySource(TextAsset candidate, out string error)
        {
            try { return TryApplySource(SequenceSourceJson.Compile(candidate), out error); }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public bool TryStartSource(out string error)
        {
            var source = sourceBinding;
            if (source == null) { error = "No explicit sequence source is configured."; return false; }
            return TryStart(source.Source.Definition, source.Bindings, source.Controls, source.Policy, source.Clock, out error);
        }
        public bool TryStart(SequenceDefinition definition, ISequenceBindings bindings, IControlRequests controls,
            SequenceControlPolicy policy, ISequenceClock clock, out string error)
        {
            error = null;
            if (executing || !isActiveAndEnabled || runner != null && runner.Read().Status == SequenceStatus.Running)
            { error = "Sequence host is disabled, busy or already running."; return false; }
            executing = true;
            try
            {
                runner?.Dispose();
                runner = new SequenceRunner(definition, bindings, controls, policy, clock);
                current = new Commands(this, runner);
            }
            finally { executing = false; FlushClose(); }
            var result = runner.Read(); error = result.Error;
            if (result.Status == SequenceStatus.Cancelled) error = error ?? result.EndReason.ToString();
            return result.Status != SequenceStatus.Failed && result.Status != SequenceStatus.Cancelled;
        }
        public void Refresh()
        {
            if (executing) return;
            executing = true;
            try { runner?.Poll(); }
            finally { executing = false; FlushClose(); }
        }
        void Update() { Refresh(); }
        public void Close()
        {
            closeRequested = true;
            if (!executing) FlushClose();
        }
        void FlushClose()
        {
            if (!closeRequested && this && isActiveAndEnabled) return;
            closeRequested = false; executing = true;
            try { runner?.Dispose(); }
            finally { executing = false; closeRequested = false; }
        }
        bool Command(SequenceRunner expected, bool skip, Guid execution, out string error)
        {
            if (!this) return skip ? expected.TrySkip(execution, out error) : expected.TryCancel(execution, out error);
            if (executing) { error = "Sequence host is busy."; return false; }
            executing = true;
            try { return skip ? expected.TrySkip(execution, out error) : expected.TryCancel(execution, out error); }
            finally { executing = false; FlushClose(); }
        }
        sealed class Commands : ISequenceCommands
        {
            readonly SequenceHost owner;
            readonly SequenceRunner target;
            public Commands(SequenceHost owner, SequenceRunner target) { this.owner = owner; this.target = target; }
            public SequenceSnapshot Read() => target.Read();
            public void Poll() { if (owner && ReferenceEquals(owner.runner, target)) owner.Refresh(); else target.Poll(); }
            public bool TryCancel(Guid execution, out string error) => owner.Command(target, false, execution, out error);
            public bool TrySkip(Guid execution, out string error) => owner.Command(target, true, execution, out error);
            public void Dispose() { if (owner && ReferenceEquals(owner.runner, target)) owner.Close(); else target.Dispose(); }
        }
        void OnDisable() { Close(); }
        void OnDestroy() { Close(); }
    }
    public sealed class UnscaledSequenceClock : ISequenceClock
    {
        public double ReadTime() => Time.unscaledTimeAsDouble;
    }
}
