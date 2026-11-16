using System;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class StageActorOutputTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject Owner = new GameObject("StageOutputOwner");
            public readonly GameObject View = new GameObject("DedicatedStageView", typeof(SpriteRenderer));
            public readonly Texture2D Texture = new Texture2D(2, 2);
            public readonly Sprite Sprite;
            public readonly SpriteRenderer Renderer;
            public readonly StageActorOutputBinding Output;
            public Fixture()
            {
                Sprite = Sprite.Create(Texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 32); Renderer = View.GetComponent<SpriteRenderer>(); Renderer.sprite = Sprite;
                Output = Owner.AddComponent<StageActorOutputBinding>(); Output.Configure(Renderer, Baseline());
            }
            public void Dispose() { if (Owner) Object.DestroyImmediate(Owner); if (View) Object.DestroyImmediate(View); Object.DestroyImmediate(Sprite); Object.DestroyImmediate(Texture); }
        }
        static StagePose Baseline(float x = 7) => new StagePose(new StagePoint(x, 3), 0, 1, false);
        static StageMoveDefinition Definition() => new StageMoveDefinition("fixture-stage", .5, -1, -2);
        [Test] public void ActualSpriteUsesOnlyTheOwnedPoseAndReturnsToTheCurrentBaseline()
        {
            float scale = Time.timeScale;
            using (var fixture = new Fixture()) using (var baseline = fixture.Output.BindBaseSource("baseline")) using (var owner = fixture.Output.OpenScope("sequence"))
            {
                Assert.That(fixture.Output.IsAvailable, Is.True); Assert.That(fixture.Renderer.enabled, Is.False); Assert.Throws<InvalidOperationException>(() => fixture.Output.OpenScope("other"));
                var move = owner.Play(Definition(), new StagePoint(1, 2), new StagePoint(5, 6)); move.Poll(.25); fixture.Output.ApplyNow();
                Assert.That(fixture.View.transform.position, Is.EqualTo(new Vector3(3, 4, -2))); Assert.That(fixture.Renderer.flipX && fixture.Renderer.enabled, Is.True);
                baseline.Submit(Baseline(9)); move.Poll(.5); fixture.Output.ApplyNow(); Assert.That(fixture.View.transform.position, Is.EqualTo(new Vector3(5, 6, -2)));
                owner.Dispose(); fixture.Output.ApplyNow(); Assert.That(fixture.View.transform.position, Is.EqualTo(new Vector3(9, 3, 0))); Assert.That(fixture.Renderer.enabled || fixture.Output.IsOwned, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(scale)); Assert.That(fixture.View.GetComponent<Collider2D>(), Is.Null); Assert.That(fixture.View.GetComponent<Rigidbody2D>(), Is.Null);
            }
        }
        [TestCase("collider")] [TestCase("body")] [TestCase("script")] [TestCase("child")]
        [TestCase("parent-body")] [TestCase("parent-script")] [TestCase("animator")] [TestCase("parent-animation")]
        public void PhysicalOrScriptedViewsAreRejectedBeforeAnyPoseWrite(string fault)
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("existing owner"))
            {
                var move = owner.Play(Definition(), default, new StagePoint(2, 0)); move.Poll(.25); fixture.Output.ApplyNow(); var session = fixture.Output.SessionId;
                var bad = new GameObject("UnsafeStageView", typeof(SpriteRenderer)); var parent = new GameObject("UnsafeStageParent"); bad.transform.SetParent(parent.transform, false);
                var renderer = bad.GetComponent<SpriteRenderer>(); renderer.sprite = fixture.Sprite; bad.transform.position = new Vector3(123, 456, 0);
                try
                {
                    switch (fault)
                    {
                        case "collider": bad.AddComponent<BoxCollider2D>(); break;
                        case "body": bad.AddComponent<Rigidbody2D>(); break;
                        case "script": bad.AddComponent<CombatActor>(); break;
                        case "child": new GameObject("UnsupportedChild").transform.SetParent(bad.transform, false); break;
                        case "parent-body": parent.AddComponent<Rigidbody2D>(); break;
                        case "parent-script": parent.AddComponent<CombatActor>(); break;
                        case "animator": bad.AddComponent<Animator>(); break;
                        case "parent-animation": parent.AddComponent<Animation>(); break;
                    }
                    Assert.Throws<ArgumentException>(() => fixture.Output.Configure(renderer, Baseline()));
                    Assert.That(bad.transform.position, Is.EqualTo(new Vector3(123, 456, 0))); Assert.That(fixture.Output.SessionId, Is.EqualTo(session)); Assert.That(owner.IsActive, Is.True);
                    Assert.That(fixture.View.transform.position.x, Is.EqualTo(1));
                }
                finally { Object.DestroyImmediate(parent); }
            }
        }
        [Test] public void AViewThatBecomesPhysicalDuringPlaybackIsNotMovedOrRestoredByTheOutput()
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("sequence"))
            {
                var move = owner.Play(Definition(), default, new StagePoint(4, 0)); move.Poll(.25); fixture.Output.ApplyNow(); var position = fixture.View.transform.position;
                var collider = fixture.View.AddComponent<BoxCollider2D>(); fixture.Output.ApplyNow();
                Assert.That(fixture.Output.IsAvailable || fixture.Output.IsOwned || owner.IsActive, Is.False); Assert.That(move.Status, Is.EqualTo(StageMoveStatus.Cancelled));
                Assert.That(fixture.View.transform.position, Is.EqualTo(position), "Cleanup must not bypass the new physical owner's movement path.");
                Object.DestroyImmediate(collider); fixture.Output.Configure(fixture.Renderer, Baseline()); Assert.That(fixture.Output.IsAvailable, Is.True);
            }
        }
        [Test] public void ContentReplacementRequiresRebindAndOldHandlesCannotControlTheNewView()
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("old"))
            {
                var move = owner.Play(Definition(), default, new StagePoint(4, 0)); move.Poll(.25); fixture.Output.ApplyNow();
                var replacement = Sprite.Create(fixture.Texture, new Rect(0, 0, 2, 2), Vector2.zero, 32);
                try
                {
                    fixture.Renderer.sprite = replacement; fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.False); Assert.That(owner.IsActive, Is.False);
                    Assert.Throws<InvalidOperationException>(() => fixture.Output.OpenScope("unbound content")); fixture.Output.Configure(fixture.Renderer, Baseline(9));
                    using (var nextOwner = fixture.Output.OpenScope("new"))
                    {
                        var next = nextOwner.Play(Definition(), new StagePoint(10, 0), new StagePoint(12, 0)); move.Poll(.5); owner.Dispose(); next.Poll(.25); fixture.Output.ApplyNow();
                        Assert.That(fixture.View.transform.position.x, Is.EqualTo(11)); Assert.That(next.IsRetained, Is.True);
                    }
                }
                finally { Object.DestroyImmediate(replacement); }
            }
        }
        [TestCase(false)] [TestCase(true)] public void ObservedControllerOrViewDisableInvalidatesTheOriginalSession(bool viewOnly)
        {
            using (var fixture = new Fixture()) using (var owner = fixture.Output.OpenScope("old"))
            {
                var move = owner.Play(Definition(), default, new StagePoint(4, 0)); move.Poll(.25); fixture.Output.ApplyNow(); var id = fixture.Output.SessionId;
                if (viewOnly) fixture.View.SetActive(false); else fixture.Output.enabled = false;
                fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable || fixture.Output.IsOwned, Is.False); Assert.That(fixture.Output.SessionId, Is.Not.EqualTo(id));
                if (viewOnly) fixture.View.SetActive(true); else fixture.Output.enabled = true;
                fixture.Output.ApplyNow(); move.Poll(.5); Assert.That(fixture.Output.IsAvailable, Is.True); Assert.That(fixture.Renderer.enabled, Is.False);
                using (var next = fixture.Output.OpenScope("new")) { next.Play(Definition(), default, default); owner.Dispose(); Assert.That(fixture.Output.IsOwned, Is.True); }
            }
        }
        [Test] public void MissingSpriteOrInvalidBaselineCannotAcquireAView()
        {
            using (var fixture = new Fixture())
            {
                Assert.Throws<ArgumentException>(() => fixture.Output.Configure(fixture.Renderer, default)); Assert.That(fixture.Output.IsAvailable, Is.True);
                fixture.Renderer.sprite = null; fixture.Output.ApplyNow(); Assert.That(fixture.Output.IsAvailable, Is.False); Assert.Throws<InvalidOperationException>(() => fixture.Output.OpenScope("missing sprite"));
                fixture.Renderer.sprite = fixture.Sprite; fixture.Output.Configure(fixture.Renderer, Baseline()); Assert.That(fixture.Output.IsAvailable, Is.True);
            }
        }
    }
}
