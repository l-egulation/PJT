using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Presentation.Tests
{
    public sealed class BuffSlotGlowTests
    {
        static readonly Color32[] Ramp =
        {
            new Color32(15, 42, 24, 255), new Color32(42, 106, 51, 255), new Color32(79, 174, 92, 255),
            new Color32(126, 224, 138, 255), new Color32(196, 245, 203, 255), new Color32(244, 255, 245, 255),
        };

        [Test]
        public void FirstReadingIsABaselineEvenWhenAlreadyActive()
        {
            var glow = new BuffSlotGlow(); glow.Observe("a", 2, 4, 6, 10);
            Assert.That(glow.TriggeredAt, Is.EqualTo(float.NegativeInfinity), "no burst for a buff that was already running");
            Assert.That(glow.Active, Is.True); Assert.That(glow.Left, Is.EqualTo(.5f).Within(1e-4f));
        }

        [Test]
        public void ActivationGleamsThenTheLevelDrainsWithTheRemainingDuration()
        {
            var glow = new BuffSlotGlow();
            glow.Observe("a", 0, 0, 0, 10);
            glow.Observe("a", 4, 4, 8, 10.02f);
            Assert.That(glow.TriggeredAt, Is.EqualTo(10.02f));
            glow.Observe("a", 3.9f, 4, 8, 10.12f); glow.Paint(Ramp, null);
            // sweep = 0.1/0.22*60-30 ≈ -2.7: the gleam band crosses (-1, -2)
            var gleam = glow.At(-1, -2);
            Assert.That(Mathf.Min(gleam.r, Mathf.Min(gleam.g, gleam.b)), Is.GreaterThan(235), "the white gleam crosses the icon"); Assert.That(gleam.a, Is.EqualTo(255));
            // 3/4 of the duration left after it has filled: the surface sits 18 rows up from the bottom
            glow.Observe("a", 3, 4, 8, 11.02f); glow.Paint(Ramp, null);
            Assert.That(glow.Level, Is.EqualTo(12 - 18));
            Assert.That(glow.At(-12, 11).a, Is.GreaterThan(0), "below the surface the icon is lit");
            glow.Observe("a", 1, 4, 8, 13.02f); glow.Paint(Ramp, null);
            Assert.That(glow.Level, Is.EqualTo(6));
            Assert.That(glow.At(0, -12).a, Is.Zero, "above the drained surface the icon is untouched");
        }

        [Test]
        public void RefreshingWhileActiveRaisesTheLevelWithoutANewBurst()
        {
            var glow = new BuffSlotGlow();
            glow.Observe("red", 0, 0, 0, 10); glow.Observe("red", 3, 3, 0, 10.02f);
            glow.Observe("red", 1, 3, 0, 12.02f); Assert.That(glow.Level, Is.EqualTo(4));
            glow.Observe("red", 3, 3, 0, 12.05f);
            Assert.That(glow.TriggeredAt, Is.EqualTo(10.02f)); Assert.That(glow.Level, Is.EqualTo(-12));
        }

        [Test]
        public void MotesAreParticlesThatOutliveRefreshesAndTheBuffItself()
        {
            var glow = new BuffSlotGlow();
            glow.Observe("red", 0, 0, 0, 10); glow.Observe("red", 3, 3, 0, 10.02f);
            for (float t = 10.1f; t < 11.01f; t += .05f) glow.Observe("red", 3 - (t - 10.02f), 3, 0, t);
            int risen = glow.MoteCount; Assert.That(risen, Is.GreaterThan(3), "motes keep rising from the surface");
            glow.Observe("red", 3, 3, 0, 11.02f);
            Assert.That(glow.MoteCount, Is.GreaterThanOrEqualTo(risen), "a refresh does not restart the motes already in the air");
            glow.Observe("red", 0, 3, 0, 11.05f);
            Assert.That(glow.Active, Is.False); Assert.That(glow.MoteCount, Is.GreaterThan(0), "they finish fading after the buff ends");
            Assert.That(glow.Animating, Is.True);
            glow.Observe("red", 0, 3, 0, 11.05f + BuffSlotGlow.MoteLife + .31f);
            Assert.That(glow.MoteCount, Is.Zero); Assert.That(glow.Animating, Is.False);
        }

        [Test]
        public void InstantEffectsGleamOnTheCooldownStartAndPingWhenReady()
        {
            var glow = new BuffSlotGlow();
            glow.Observe("heal", 0, 0, 0, 10); glow.Observe("heal", 0, 0, 5, 10.02f);
            Assert.That(glow.TriggeredAt, Is.EqualTo(10.02f)); Assert.That(glow.Active, Is.False);
            glow.Observe("heal", 0, 0, 1, 14); Assert.That(glow.Animating, Is.False, "nothing to draw while only cooling down");
            glow.Observe("heal", 0, 0, 0, 15.02f);
            Assert.That(glow.ReadyAt, Is.EqualTo(15.02f)); Assert.That(glow.Animating, Is.True);
            glow.Observe("heal", 0, 0, 0, 15.02f + BuffSlotGlow.Ping + .01f); Assert.That(glow.Animating, Is.False);
        }

        [Test]
        public void EndingFlashesTheBottomRowAndChangingTheSlotResetsTheBaseline()
        {
            var glow = new BuffSlotGlow();
            glow.Observe("a", 0, 0, 0, 10); glow.Observe("a", 2, 2, 6, 10.02f);
            glow.Observe("a", 0, 2, 6, 12.05f);
            Assert.That(glow.EndedAt, Is.EqualTo(12.05f)); glow.Paint(Ramp, null);
            Assert.That(glow.Active, Is.False); Assert.That(glow.Animating, Is.True);
            glow.Observe("b", 3, 3, 6, 12.1f);
            Assert.That(glow.TriggeredAt, Is.EqualTo(float.NegativeInfinity)); Assert.That(glow.EndedAt, Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void TheIconMaskKeepsTransparentCornersClear()
        {
            var glow = new BuffSlotGlow(); var mask = new bool[24 * 24];
            for (int i = 0; i < mask.Length; i++) mask[i] = true;
            mask[23 * 24] = false; // bottom-left corner
            glow.Observe("a", 0, 0, 0, 10); glow.Observe("a", 4, 4, 0, 10.02f);
            glow.Observe("a", 4, 4, 0, 11); glow.Paint(Ramp, mask);
            Assert.That(glow.At(-12, 11).a, Is.Zero); Assert.That(glow.At(-11, 11).a, Is.GreaterThan(0));
        }
    }
}
