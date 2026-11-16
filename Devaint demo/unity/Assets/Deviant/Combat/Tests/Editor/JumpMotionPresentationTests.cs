using System;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class JumpMotionPresentationTests
    {
        [TestCase(20f)]
        [TestCase(-26f)]
        public void OrdinaryJumpKeepsNativeScaleAndLeavesNoBodyEchoesByDefault(float velocity)
        {
            var node = new GameObject("Native jump default", typeof(SpriteRenderer));
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero, 32);
            var drawing = node.GetComponent<SpriteRenderer>(); drawing.sprite = sprite;
            try
            {
                using (var art = new JumpMotionPresentation())
                {
                    var actor = Actor(1, false, velocity); art.Draw(in actor, drawing, false);
                    Assert.That(drawing.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(art.ActiveAfterimages, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node); UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(false, 20f, 0f, 1.16f)]   // launch speed: tallest
        [TestCase(false, 10f, 0f, 1.08f)]
        [TestCase(false, 0f, 0f, 1f)]       // apex hold
        [TestCase(false, -6f, 0f, 1f)]      // slow fall: untouched
        [TestCase(false, -26f, 0f, 1.08f)]
        [TestCase(true, 0f, 0f, 1f)]        // landing keeps native size
        [TestCase(true, 0f, .045f, 1f)]
        [TestCase(true, 0f, .09f, 1f)]
        public void StretchFollowsVerticalSpeedAndLanding(bool grounded, float vy, float sinceLanding, float expected)
            => Assert.That(JumpMotionPresentation.Stretch(grounded, vy, sinceLanding), Is.EqualTo(expected).Within(1e-4f));

        static ActorReadSnapshot Actor(float now, bool grounded, float vy)
            => new ActorReadSnapshot(default, 0, now, true, new ActorKinematicsSnapshot(0, 0, 0, vy, 1, grounded, false),
                new ActorVitalsSnapshot(100, 100, 0, 0, 0, 0), default, default, default, default, Guid.Empty, "base_sword", null, default, default);

        [Test] public void FirstAirborneDrawAndReenabledEchoesKeepTheirFixedCadenceAndClearAtLanding()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero, 32);
            var node = new GameObject("Airborne echo cadence", typeof(SpriteRenderer));
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            try
            {
                using (var art = new JumpMotionPresentation { StretchEnabled = true, AfterimagesEnabled = true })
                {
                    // A scene/pose can be bound while the player is already airborne.
                    var actor = Actor(1, false, 20); art.Draw(in actor, renderer, false);
                    actor = Actor(1.001f, false, 20); art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(1));
                    actor = Actor(1.04f, false, 20); art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(2));
                    art.AfterimagesEnabled = false; art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.Zero);
                    art.AfterimagesEnabled = true; art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(1));
                    actor = Actor(1.041f, true, 0); art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.Zero, "Landing removes the airborne silhouette immediately.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node); UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LandingClearsAirborneAfterimagesOnTheFirstGroundedFrame()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0), 32);
            var node = new GameObject("Jump echo regression", typeof(SpriteRenderer));
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            try
            {
                using (var art = new JumpMotionPresentation { StretchEnabled = true, AfterimagesEnabled = true })
                {
                    for (int i = 0; i < 4; i++) { var falling = Actor(i / 30f, false, -20); art.Draw(in falling, renderer, false); }
                    Assert.Greater(art.ActiveAfterimages, 0, "a fast fall leaves echoes");
                    var landed = Actor(4 / 30f + .001f, true, 0);
                    art.Draw(in landed, renderer, false);
                    Assert.AreEqual(0, art.ActiveAfterimages, "echoes never linger over the landed body");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LandingAndSuppressionRestoreTheNativeScaleWithoutAnotherSpriteAssignment(bool suppressed)
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0), 32);
            var node = new GameObject("Jump scale regression", typeof(SpriteRenderer));
            var renderer = node.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var nativeScale = Vector3.one;
            renderer.transform.localScale = nativeScale;
            try
            {
                using (var art = new JumpMotionPresentation { StretchEnabled = true, AfterimagesEnabled = true })
                {
                    var actor = Actor(0, true, 0);
                    art.Draw(in actor, renderer, false);
                    actor = Actor(.01f, false, 20);
                    art.Draw(in actor, renderer, false);
                    Assert.Greater(renderer.transform.localScale.y, nativeScale.y);
                    actor = Actor(.5f, true, 0);
                    art.Draw(in actor, renderer, suppressed);
                    Assert.That(renderer.transform.localScale, Is.EqualTo(nativeScale),
                        "Landing and another pose owner restore the native size without widening the body.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test] public void FastRiseStretchesFromFeetAndLeavesWorldFixedEchoesThatFade()
        {
            var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0), 32);
            var node = new GameObject("Jump test", typeof(SpriteRenderer)); var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            try
            {
                using (var art = new JumpMotionPresentation { StretchEnabled = true, AfterimagesEnabled = true })
                {
                    var actor = Actor(0, true, 0); art.Draw(in actor, renderer, false);
                    Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one), "Standing still is untouched.");

                    actor = Actor(.01f, false, 20); art.Draw(in actor, renderer, false);
                    Assert.That(renderer.transform.localScale.y, Is.EqualTo(1.16f).Within(1e-4f));
                    Assert.That(renderer.transform.localScale.x, Is.EqualTo(1 / Mathf.Sqrt(1.16f)).Within(1e-4f));
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(1));
                    var echo = GameObject.Find("Jump afterimage"); var at = echo.transform.position;
                    node.transform.position = Vector3.up * 3;
                    Assert.That(echo.transform.position, Is.EqualTo(at), "Echoes stay where the body was.");

                    actor = Actor(.02f, false, 19); art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(1), "One echo per 1/30 s, not per frame.");
                    actor = Actor(.05f, false, 18); art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.EqualTo(2));
                    Assert.That(echo.GetComponent<SpriteRenderer>().color.a, Is.LessThan(JumpMotionPresentation.EchoOpacity));

                    actor = Actor(.3f, false, 0); renderer.transform.localScale = Vector3.one; art.Draw(in actor, renderer, false);
                    Assert.That(art.ActiveAfterimages, Is.Zero, "Echoes expire and none spawn near the apex.");

                    actor = Actor(.5f, true, 0); renderer.transform.localScale = Vector3.one; art.Draw(in actor, renderer, false);
                    Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one), "Touchdown keeps the native body size.");

                    actor = Actor(.6f, false, 20); renderer.transform.localScale = Vector3.one; art.Draw(in actor, renderer, true);
                    Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one), "Dash or attack owns the body.");
                    Assert.That(art.ActiveAfterimages, Is.Zero);

                    actor = Actor(.7f, false, 20); art.Draw(in actor, renderer, false); art.Reset();
                    Assert.That(art.ActiveAfterimages, Is.Zero);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(node); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
