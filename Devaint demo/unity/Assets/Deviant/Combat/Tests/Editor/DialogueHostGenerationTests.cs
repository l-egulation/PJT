using System;
using Deviant.Control;
using Deviant.Control.Contracts;
using Deviant.Integration;
using Deviant.Narrative.Contracts;
using Deviant.Narrative.Tests;
using Deviant.UI.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class DialogueHostGenerationTests
    {
        [Test] public void StaleScopeCloseCannotCancelReconfiguredActualDialogueHost()
        {
            var owner = new GameObject("DialogueHostGeneration"); var host = owner.AddComponent<NpcDialogueHost>();
            using (var controls = new ControlSession())
            try
            {
                var targets = new DialogueFixtures.Targets(); var view = new DialogueFixtures.View();
                void Configure() => host.Configure(targets, new DialogueFixtures.Conditions(), new InteractionPolicy(2, true, false, true),
                    view, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.Interaction, true));
                Configure(); Guid originalBinding = host.BindingId;
                Assert.That(host.TryInteract(targets.Target(), out var error), Is.True, error); var original = host.Current;
                Configure(); Guid replacementBinding = host.BindingId;
                Assert.That(replacementBinding, Is.Not.EqualTo(originalBinding)); Assert.That(original.Read().Status, Is.EqualTo(DialogueStatus.Cancelled));
                Assert.That(host.TryInteract(targets.Target(), out error), Is.True, error); var current = host.Current;
                Assert.That(host.TryClose(originalBinding, DialogueEndReason.RunEnded, out _), Is.False);
                Assert.That(host.Current, Is.SameAs(current)); Assert.That(current.Read().Status, Is.EqualTo(DialogueStatus.Active));
                Assert.That(controls.Current.SimulationPaused, Is.True); Assert.That(view.Bound, Is.Not.EqualTo(Guid.Empty));
                Assert.That(host.TryClose(replacementBinding, DialogueEndReason.RunEnded, out error), Is.True, error);
                Assert.That(current.Read().Status, Is.EqualTo(DialogueStatus.Cancelled)); Assert.That(current.Read().EndReason, Is.EqualTo(DialogueEndReason.RunEnded));
                Assert.That(host.BindingId, Is.EqualTo(Guid.Empty)); Assert.That(controls.Current.SimulationPaused, Is.False); Assert.That(view.Bound, Is.EqualTo(Guid.Empty));
                Assert.That(host.TryClose(replacementBinding, DialogueEndReason.OwnerDisposed, out _), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void FailedConfigureDoesNotPublishAUsableOwnerGeneration()
        {
            var owner = new GameObject("DialogueHostFailedGeneration"); var host = owner.AddComponent<NpcDialogueHost>();
            using (var controls = new ControlSession())
            try
            {
                Assert.Catch(() => host.Configure(new DialogueFixtures.Targets(), new DialogueFixtures.Conditions(), new InteractionPolicy(2, true, false, true),
                    null, new DialogueFixtures.Text(), controls, new DialoguePresentationPolicy(InputChannels.None, false)));
                Assert.That(host.BindingId, Is.EqualTo(Guid.Empty)); Assert.That(host.TryClose(Guid.NewGuid(), DialogueEndReason.OwnerDisposed, out _), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
