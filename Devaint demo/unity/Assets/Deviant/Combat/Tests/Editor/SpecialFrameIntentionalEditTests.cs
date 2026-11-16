using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class SpecialFrameIntentionalEditTests
    {
        static AuthoredSkillDefinition Numeric(string id)
        {
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text); var form = combat.forms.Single(value => value.specials.Any(skill => skill.id == id));
            return new AuthoredSkillDefinition(form.id, form.specials.Single(value => value.id == id));
        }
        static MeleeFrameMedia Media(string id) => new MeleeFrameMedia("known/attack1", new[] { "prepare", "recover" },
            new AttackMotionTiming("known/attack1", new[] { "known-prepare", "known-recover" }, new[] { .25f, .25f }, -1, .25f, -1, 0, 1), false, id + "/Special");
        [Test] public void InstantTeleportRemainsAPointRatherThanAcquiringAnInventedPositiveDuration()
        {
            var numeric = Numeric("rapier_movement"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            source.timeline.markers.Single(value => value.id == "teleport-start").offset = .25f;
            var changed = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(changed.Execution.Movement.Start, Is.EqualTo(.25f)); Assert.That(changed.Execution.Movement.Duration, Is.Zero);
            Assert.That(changed.Execution.Movement.Type, Is.EqualTo("target_teleport_strike"));
            source.timeline.markers = source.timeline.markers.Concat(new[] { new FrameTimelineMarkerDocument { id = "teleport-end", anchor = TimelineAnchor.Marker, reference = "teleport-start", offset = .125f } }).ToArray();
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
        }
        [Test] public void PositiveInvulnerabilityAndFreezeKeepWholeSpanPolicyValidation()
        {
            var numeric = Numeric("rapier_follow_up"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            source.timeline.markers.Single(value => value.id == "invul-00-start").offset = .25f;
            source.timeline.markers.Single(value => value.id == "invul-00-end").offset = .75f;
            source.timeline.markers.Single(value => value.id == "freeze-start").offset = .25f;
            source.timeline.markers.Single(value => value.id == "freeze-end").offset = .75f;
            var changed = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(changed.Execution.InvulStart[0], Is.EqualTo(.25f)); Assert.That(changed.Execution.InvulEnd[0], Is.EqualTo(.75f));
            Assert.That(changed.Execution.VerticalFreeze, Is.EqualTo(new[] { .25f, .75f }));
            source.timeline.markers.Single(value => value.id == "freeze-end").offset = .25f;
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
        }
        [Test] public void FrameHitTimingCannotChangeItsDeclaredActorOrIndependentAnchor()
        {
            var numeric = Numeric("hammer_follow_up"); var media = Media(numeric.Id);
            var source = ActionSpecialFrameCompiler.AuthorAuthored(numeric, media, SpecialFramePolicyCoverageTests.IntentionalAuthored(numeric));
            source.timeline.markers.Single(value => value.id == "hit-03-start").offset = 2.5f;
            var changed = ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source);
            Assert.That(changed.Execution.Hits[3].Time, Is.GreaterThan(changed.Execution.End)); Assert.That(changed.Execution.Hits[3].Anchor, Is.EqualTo("saved_primary_world_pose"));
            Assert.That(changed.Execution.Hits[0].Anchor, Is.EqualTo("actor"));
            source.timeline.markers.Single(value => value.id == "hit-00-start").offset = 2.5f;
            Assert.Throws<ArgumentException>(() => ActionSpecialFrameCompiler.CompileAuthored(numeric, media, source));
        }
    }
}
