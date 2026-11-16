using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Presentation.Tests
{
    public sealed class HealthBarInkTests
    {
        // 도트 체력판의 게이지(960x540 기준 길이 200, 높이 9, 위쪽 4화소 기울기)
        static HealthBarInk Make() => new HealthBarInk(200, 9, 4);
        static int GaugeX(HealthBarInk ink, float hp, float total = 100) => Mathf.FloorToInt(ink.MarginX + ink.LeanAt(ink.FillHeight / 2 + .5f) + hp / total * ink.BarLength);
        static int MidY(HealthBarInk ink) => ink.MarginY + ink.FillHeight / 2;

        [Test]
        public void FirstReadingIsABaselineWithoutAnyEffect()
        {
            var ink = Make(); ink.Observe(50, 100, 0, 10);
            Assert.That(ink.Animating, Is.False);
            Assert.That(ink.SolidFraction, Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(ink.GainAmount, Is.Zero);
        }

        [Test]
        public void HealIsBrushedInThenFoldsIntoTheGauge()
        {
            var ink = Make(); ink.Observe(46, 100, 0, 10); ink.Observe(72, 100, 0, 10.01f);
            Assert.That(ink.SolidFraction, Is.EqualTo(.46f).Within(1e-4f), "the gauge keeps the old value until the brush paints the gain");
            Assert.That(ink.GainAmount, Is.EqualTo(26)); Assert.That(ink.HealTint, Is.GreaterThan(.9f));
            ink.Observe(72, 100, 0, 10.01f + HealthBarInk.Stroke * .3f); ink.Paint();
            Assert.That(ink.At(GaugeX(ink, 47), MidY(ink)).a, Is.EqualTo(255), "the start of the gain is already painted");
            Assert.That(ink.At(GaugeX(ink, 71), MidY(ink)).a, Is.Zero, "the end of the gain is not painted yet");
            ink.Observe(72, 100, 0, 10.01f + HealthBarInk.Stroke + .05f); ink.Paint();
            Assert.That(ink.At(GaugeX(ink, 71), MidY(ink)).a, Is.EqualTo(255));
            Assert.That(ink.At(GaugeX(ink, 80), MidY(ink)).a, Is.Zero, "nothing past the new health");
            ink.Observe(72, 100, 0, 12);
            Assert.That(ink.SolidFraction, Is.EqualTo(.72f).Within(1e-4f));
            Assert.That(ink.Animating, Is.False);
        }

        [Test]
        public void HealsWithinHalfASecondAddUp()
        {
            var ink = Make(); ink.Observe(50, 100, 0, 0);
            for (int i = 1; i <= 4; i++) ink.Observe(50 + 2 * i, 100, 0, i * .12f);
            Assert.That(ink.GainAmount, Is.EqualTo(8));
            ink.Observe(60, 100, 0, 2);
            Assert.That(ink.GainAmount, Is.EqualTo(2), "a later heal starts a new number");
        }

        [Test]
        public void DamageFlashesFadesThenIsErasedFromTheRight()
        {
            var ink = Make(); ink.Observe(72, 100, 0, 5); ink.Observe(50, 100, 0, 5.01f);
            Assert.That(ink.SolidFraction, Is.EqualTo(.5f).Within(1e-4f), "the gauge drops at once");
            Assert.That(ink.DamageTint, Is.GreaterThan(.9f));
            ink.Observe(50, 100, 0, 5.04f); ink.Paint();
            Assert.That((Color)ink.At(GaugeX(ink, 60), MidY(ink)), Is.EqualTo(Color.white));
            ink.Observe(50, 100, 0, 5.1f); ink.Paint();
            var pale = ink.At(GaugeX(ink, 60), MidY(ink));
            Assert.That(pale.a, Is.EqualTo(255)); Assert.That(pale.g, Is.GreaterThan(180), "the lost part is pale before it is erased");
            ink.Observe(50, 100, 0, 5.01f + HealthBarInk.RubDelay + HealthBarInk.Rub * .5f); ink.Paint();
            Assert.That(ink.At(GaugeX(ink, 51), MidY(ink)).a, Is.EqualTo(255), "the left end is still waiting");
            Assert.That(ink.At(GaugeX(ink, 71), MidY(ink)).g, Is.LessThan(180), "the right end is already rubbed out");
            ink.Observe(50, 100, 0, 7); ink.Paint();
            Assert.That(ink.Animating, Is.False);
            Assert.That(ink.At(GaugeX(ink, 60), MidY(ink)).a, Is.Zero);
        }

        [Test]
        public void ShieldIsDrawnBehindHealthThenBreaks()
        {
            var ink = Make(); ink.Observe(72, 100, 0, 1); ink.Observe(72, 100, 12, 1.01f);
            Assert.That(ink.Animating, Is.True);
            ink.Observe(72, 100, 12, 1.01f + HealthBarInk.ShieldDraw + .05f); ink.Paint();
            var mint = ink.At(GaugeX(ink, 78), MidY(ink));
            Assert.That(mint.a, Is.EqualTo(255)); Assert.That(mint.g, Is.GreaterThan(mint.r), "the shield is mint");
            Assert.That(ink.At(GaugeX(ink, 90), MidY(ink)).a, Is.Zero, "nothing past health plus shield");
            Assert.That(ink.At(ink.MarginX + 100, ink.MarginY - 1).a, Is.EqualTo(255), "the frame above the gauge is traced");
            ink.Observe(72, 100, 7, 2); ink.Observe(72, 100, 0, 2.5f);
            ink.Observe(72, 100, 0, 2.5f + HealthBarInk.Unravel + .01f); ink.Paint();
            Assert.That(ink.At(ink.MarginX + 100, ink.MarginY - 1).a, Is.Zero, "the frame unravels after the break");
            ink.Observe(72, 100, 0, 4);
            Assert.That(ink.Animating, Is.False);
        }

        [Test]
        public void ShieldThatDrainsOverTimeOnlyShrinks()
        {
            var ink = Make(); ink.Observe(72, 100, 0, 0); ink.Observe(72, 100, 12, .01f);
            ink.Observe(72, 100, 12, 1);
            ink.Observe(72, 100, 6, 1.02f, struck: false); ink.Paint();
            Assert.That((Color)ink.At(GaugeX(ink, 77), MidY(ink)), Is.Not.EqualTo(Color.white), "no hit flash at the shield end");
            Assert.That(ink.At(GaugeX(ink, 75), MidY(ink)).a, Is.EqualTo(255), "the remaining shield is still painted");
            Assert.That(ink.At(GaugeX(ink, 80), MidY(ink)).a, Is.Zero, "the drained part is gone");
            ink.Observe(72, 100, 0, 1.5f, struck: false);
            ink.Observe(72, 100, 0, 1.5f + HealthBarInk.Unravel + .1f);
            Assert.That(ink.Animating, Is.False, "a shield that drains away leaves nothing behind once the frame unravels");
        }

        [Test]
        public void ShieldPastMaximumShrinksTheWholeGauge()
        {
            var ink = Make(); ink.Observe(95, 100, 20, 0);
            Assert.That(ink.Total, Is.EqualTo(115));
            Assert.That(ink.SolidFraction, Is.EqualTo(95f / 115).Within(1e-4f));
        }

        [Test]
        public void TheMaskFollowsTheGaugeSlant()
        {
            var ink = Make();
            Assert.That(ink.Inside(0, ink.FillHeight - 1), Is.True, "bottom-left corner");
            Assert.That(ink.Inside(0, 0), Is.False, "top-left leans right");
            Assert.That(ink.Inside(ink.FillWidth - 1, 0), Is.True, "top-right corner");
            Assert.That(ink.Inside(ink.FillWidth - 1, ink.FillHeight - 1), Is.False, "bottom-right leans left");
        }

        [Test]
        public void ResetForgetsEverything()
        {
            var ink = Make(); ink.Observe(40, 100, 0, 0); ink.Observe(90, 100, 10, .1f); ink.Reset();
            ink.Observe(90, 100, 10, .2f);
            Assert.That(ink.GainAmount, Is.Zero);
            Assert.That(ink.SolidFraction, Is.EqualTo(.9f).Within(1e-4f));
        }
    

        [Test]
        public void PlaqueDrawsThePlateGaugeAndNumbersWithoutAPortraitCircle()
        {
            var plaque = new HealthPlaque(); plaque.Draw(50, 100, 0, 0, false, 0);
            int yellow = 0;
            for (int y = 0; y < HealthPlaque.Height; y++) for (int x = 0; x < HealthPlaque.Width; x++) { var c = plaque.At(x, y); if (c.r > 200 && c.g > 150 && c.b < 100) yellow++; }
            Assert.That(yellow, Is.Zero, "the approved portrait image replaces the drawn circle and eyes");
            var plate = HealthPlaque.ToTexture(150, -10);
            Assert.That((Color)plaque.At(plate.x, plate.y), Is.EqualTo((Color)new Color32(21, 21, 19, 255)), "the black plate");
            var filled = HealthPlaque.BarPoint(40); var empty = HealthPlaque.BarPoint(60);
            Assert.That((Color)plaque.At(filled.x, filled.y), Is.EqualTo((Color)HealthBarInk.Red), "red up to the health");
            Assert.That(plaque.At(empty.x, empty.y).r, Is.LessThan(40), "dark track past the health");
            // "/ 100" is the mini font at twice its size: its slash spans 10 rows ending where the big number ends
            int numberRows = 0, maxRows = 0;
            for (int ly = -28; ly < 0; ly++)
            {
                var a = HealthPlaque.ToTexture(HealthPlaque.NumberX + 2, ly); var b = HealthPlaque.ToTexture(HealthPlaque.NumberX + 2 * HealthPlaque.MaxScale + 24 + 4, ly);
                bool n = false, m = false;
                for (int dx = 0; dx < 24; dx++) { if (plaque.At(a.x + dx, a.y).r > 200) n = true; if (plaque.At(b.x + dx, b.y).r > 150) m = true; }
                if (n) numberRows++; if (m) maxRows++;
            }
            Assert.That(numberRows, Is.EqualTo(14)); Assert.That(maxRows, Is.EqualTo(10));
            plaque.Draw(50, 100, 0, .01f, true, 1);
            Assert.That((Color)plaque.At(filled.x, filled.y), Is.EqualTo(Color.white), "the hit flash turns the gauge white");
        }

        [Test]
        public void PlaqueNumbersTurnGreenAndShowTheGain()
        {
            var plaque = new HealthPlaque(); plaque.Draw(46, 100, 0, 0, false, 0); plaque.Draw(72, 100, 0, .05f, false, 0);
            int green = 0;
            for (int y = 0; y < 30; y++) for (int x = 40; x < 140; x++) { var c = plaque.At(x, y); if (c.g > 200 && c.r < 190 && c.b < 190) green++; }
            Assert.That(green, Is.GreaterThan(40), "the health number and +26 are green");
            Assert.That(plaque.Ink.GainAmount, Is.EqualTo(26));
        }
    }
}
