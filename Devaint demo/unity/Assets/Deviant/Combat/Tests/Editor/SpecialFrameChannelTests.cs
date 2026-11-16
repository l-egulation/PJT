using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class SpecialFrameChannelTests
    {
        static AuthoredSkillDefinition Source(string id)
        {
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text); var form = combat.forms.Single(value => value.specials.Any(skill => skill.id == id));
            return new AuthoredSkillDefinition(form.id, form.specials.Single(value => value.id == id));
        }
        static MeleeFrameMedia Media(string id) => new MeleeFrameMedia("known/attack1", new[] { "prepare", "recover" },
            new AttackMotionTiming("known/attack1", new[] { "frame-prepare", "frame-recover" }, new[] { .25f, .25f }, -1, .25f, -1, 0, 1), false, id + "/Special");
        [Test] public void FullRapierPolicyExposesAllHitTeleportInvulnerabilityAndFreezeChannels()
        {
            var numeric = Source("rapier_follow_up"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            var action = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source); var ids = SpecialFrameChannels.Required(action);
            Assert.That(ids, Does.Contain("activation")); Assert.That(ids, Does.Contain("teleport-start")); Assert.That(ids, Does.Contain("freeze-start")); Assert.That(ids, Does.Contain("freeze-end"));
            Assert.That(ids, Does.Contain("invul-00-start")); Assert.That(ids, Does.Contain("invul-00-end"));
            for (int index = 0; index < 5; index++) { Assert.That(ids, Does.Contain("hit-" + index.ToString("D2") + "-start")); Assert.That(ids, Does.Contain("hit-" + index.ToString("D2") + "-end")); }
        }
        [Test] public void RecastCannotAcquirePretendActionAbsoluteApproachIntervals()
        {
            var numeric = Source("whip_movement"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            source.timeline.markers = source.timeline.markers.Concat(new[] { new FrameTimelineMarkerDocument { id = "approach-start", anchor = TimelineAnchor.Seconds, reference = "", offset = .25f } }).ToArray();
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
            Assert.That(SpecialFrameChannels.Epoch("movement", "recastWindow"), Is.EqualTo("accepted marked-target hit"));
            Assert.That(SpecialFrameChannels.Epoch("movement", "wallRecovery"), Is.EqualTo("actual wall or primary boss movement end"));
        }
        [Test] public void EffectStartMustKeepItsAcceptedActivationEpoch()
        {
            var numeric = Source("dagger_range"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            var marker = source.timeline.markers.Single(value => value.id == "effect-start"); marker.anchor = TimelineAnchor.Seconds; marker.reference = ""; marker.offset = .25f;
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
        }
    }
}
