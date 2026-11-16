using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Authoring.Tests
{
    public class ChargedMeleePreviewTests
    {
        [TestCase("rapier_default")] [TestCase("greatsword_default")]
        public void SelectedReleaseUsesExistingChargeResolveAndCapturedFixedMediaCurve(string id)
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text); var form = catalog.forms.Single(value => value.specials.Any(skill => skill.id == id));
            Assert.That(ChargedMeleeDefinition.TryCreate(form.id, form.specials.Single(value => value.id == id), out var definition, out var error), Is.True, error);
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            foreach (float at in new[] { definition.Policy.Minimum, (definition.Policy.Minimum + definition.Policy.Maximum) / 2, definition.Policy.Maximum }) {
                var preview = ActionGeometryPreviewModel.Compile(definition, at, media, _ => null); var release = definition.Policy.Resolve(at);
                Assert.That(preview.Release.Value.Damage, Is.EqualTo(release.Damage)); Assert.That(preview.End, Is.EqualTo(release.MotionEnd)); Assert.That(preview.AdvanceAt(90, 1), Is.Zero);
                float impact = definition.Policy.Minimum + release.Shift + definition.Policy.ReleaseWindup; Assert.That(preview.Sample(impact, 1, 0).HitActive, Is.True);
                float roundedEnd = impact + definition.CreateHit().active;
                float inside = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(roundedEnd) - 1);
                float outside = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(roundedEnd) + 1);
                Assert.That(preview.Sample(inside, 1, 0).HitActive, Is.True, "The representable point immediately inside the charge hit end remains active.");
                Assert.That(preview.Sample(outside, 1, 0).HitActive, Is.False, "The representable point immediately outside the charge hit end is inactive.");
                Assert.That(preview.Sample(0, 1, 0).Root, Is.EqualTo(Vector2.zero));
                if (definition.Policy.HoldUntilRelease)
                {
                    Assert.That(preview.BoundBinding, Is.EqualTo("greatsword/attack_charge_slam"));
                    Assert.That(preview.Sample(release.ReleaseTime, 1, 0).HitActive, Is.False);
                }
                if (preview.Motion != null) {
                    float time = AttackMotionTiming.MapSourceTime(impact, impact, definition.Policy.BaseEnd, preview.Motion.SourceImpact, preview.Motion.SourceDuration);
                    int frame = AttackMotionTiming.SelectFrame(preview.Motion.Seconds, preview.Motion.SourceDuration, time, false);
                    Assert.That(preview.Sample(impact, 1, 0).FrameIndex, Is.EqualTo(frame));
                }
            }
        }
    }
}
