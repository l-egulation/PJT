using System;
using System.Linq;
using Deviant.Presentation.Contracts;
using NUnit.Framework;

namespace Deviant.Presentation.Tests
{
    public class CameraDirectorTests
    {
        sealed class Source : ICameraPositionSource
        {
            public CameraPosition Position;
            public bool Throw;
            public int Reads;
            public Action OnRead;
            public CameraPosition Read() { Reads++; OnRead?.Invoke(); if (Throw) throw new InvalidOperationException("missing camera anchor"); return Position; }
        }
        static CameraPosition P(float x) => new CameraPosition(x, x + 1, -10);
        [Test] public void CameraRuntimeHasNoEngineCombatControlOrUiDependency()
        {
            Assert.That(typeof(CameraDirector).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor") || a.Name.StartsWith("Deviant.Combat") || a.Name.StartsWith("Deviant.Control") || a.Name.StartsWith("Deviant.UI")), Is.False);
        }
        [Test] public void BaselineAndRemainingSourcesAreReadNowInsteadOfRestoringOldPositions()
        {
            using (var director = new CameraDirector()) using (var a = director.OpenScope("dialogue")) using (var b = director.OpenScope("sequence"))
            {
                Assert.That(director.Resolve(P(0)).Position, Is.EqualTo(P(0)));
                var source = new Source { Position = P(10) }; var first = a.Request(10, source);
                Assert.That(director.Resolve(P(1)).Position, Is.EqualTo(P(10)));
                var second = b.Request(20, new FixedCameraPosition(P(20)));
                Assert.That(director.Resolve(P(2)).Position, Is.EqualTo(P(20)));
                source.Position = P(30); second.Complete(); second.Cancel();
                Assert.That(second.Status, Is.EqualTo(CameraRequestStatus.Completed)); Assert.That(first.Status, Is.EqualTo(CameraRequestStatus.Active));
                Assert.That(director.Resolve(P(3)).Position, Is.EqualTo(P(30)), "The hidden source may have moved.");
                first.Cancel(); Assert.That(director.Resolve(P(40)).Position, Is.EqualTo(P(40))); Assert.That(director.LastSelection.RequestSequence, Is.Null);
            }
        }
        [Test] public void PriorityUsesLatestSequenceForTiesAndDistinctOwnersReleaseOnlyThemselves()
        {
            using (var director = new CameraDirector())
            {
                var a = director.OpenScope("same name"); using (var b = director.OpenScope("same name"))
                {
                    var old = a.Request(5, new FixedCameraPosition(P(1))); var next = b.Request(5, new FixedCameraPosition(P(2)));
                    Assert.That(director.Resolve(P(0)).RequestSequence, Is.EqualTo(next.Sequence));
                    Assert.That(director.Resolve(P(0)).Position, Is.EqualTo(P(2)), "Ties remain stable between resolutions.");
                    a.Dispose(); a.Dispose(); old.Cancel();
                    Assert.That(director.ActiveCount, Is.EqualTo(1)); Assert.That(next.Status, Is.EqualTo(CameraRequestStatus.Active));
                    Assert.That(director.Resolve(P(0)).Position, Is.EqualTo(P(2)));
                    next.Dispose(); Assert.That(director.ActiveCount, Is.Zero);
                }
            }
        }
        [Test] public void InvalidRequestsDoNotMutateExistingSelectionAndSourceFailureFallsBack()
        {
            using (var director = new CameraDirector()) using (var owner = director.OpenScope("owner"))
            {
                var low = new Source { Position = P(1) }; var high = new Source { Throw = true };
                var lower = owner.Request(1, low); var higher = owner.Request(2, high);
                Assert.Throws<ArgumentNullException>(() => owner.Request(3, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => new CameraPosition(float.NaN, 0, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => new CameraPosition(0, float.PositiveInfinity, 0));
                var selected = director.Resolve(P(0));
                Assert.That(higher.Status, Is.EqualTo(CameraRequestStatus.Failed)); Assert.That(higher.Error, Does.Contain("missing camera anchor"));
                Assert.That(lower.Status, Is.EqualTo(CameraRequestStatus.Active)); Assert.That(selected.Position, Is.EqualTo(P(1)));
                high.Throw = false; high.Position = P(2); director.Resolve(P(0)); Assert.That(high.Reads, Is.EqualTo(1), "A failed request does not silently revive.");
            }
        }
        [Test] public void HiddenLowerPrioritySourceIsNotSampledUntilItWins()
        {
            using (var director = new CameraDirector()) using (var owner = director.OpenScope("owner"))
            {
                var low = new Source { Position = P(1) }; owner.Request(1, low);
                var high = owner.Request(2, new FixedCameraPosition(P(2))); director.Resolve(P(0)); Assert.That(low.Reads, Is.Zero);
                high.Complete(); director.Resolve(P(0)); Assert.That(low.Reads, Is.EqualTo(1));
            }
        }
        [Test] public void ResetAndEndInvalidateOldScopesAndHandlesCannotReleaseTheNewSession()
        {
            var director = new CameraDirector(); var oldOwner = director.OpenScope("old"); var old = oldOwner.Request(1, new FixedCameraPosition(P(1))); var session = director.SessionId;
            director.Reset(); Assert.That(oldOwner.IsActive, Is.False); Assert.That(old.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(director.SessionId, Is.Not.EqualTo(session));
            Assert.Throws<ObjectDisposedException>(() => oldOwner.Request(1, new FixedCameraPosition(P(2))));
            using (var owner = director.OpenScope("new"))
            {
                var next = owner.Request(1, new FixedCameraPosition(P(3))); old.Complete(); oldOwner.Dispose();
                Assert.That(director.Resolve(P(0)).RequestSequence, Is.EqualTo(next.Sequence)); Assert.That(next.Status, Is.EqualTo(CameraRequestStatus.Active));
                director.Dispose(); director.Dispose(); Assert.That(next.Status, Is.EqualTo(CameraRequestStatus.Cancelled));
                Assert.Throws<ObjectDisposedException>(() => director.Resolve(P(0)));
            }
        }
        [Test] public void ASourceCannotReenterTheDirectorAndDoesNotBlockOtherOwners()
        {
            using (var director = new CameraDirector()) using (var owner = director.OpenScope("owner"))
            {
                var bad = new Source { OnRead = () => director.Reset() }; var request = owner.Request(2, bad);
                var valid = owner.Request(1, new FixedCameraPosition(P(1)));
                Assert.That(director.Resolve(P(0)).RequestSequence, Is.EqualTo(valid.Sequence));
                Assert.That(request.Status, Is.EqualTo(CameraRequestStatus.Failed)); Assert.That(request.Error, Does.Contain("reenter"));
                Assert.That(owner.IsActive, Is.True);
            }
        }
    }
}
