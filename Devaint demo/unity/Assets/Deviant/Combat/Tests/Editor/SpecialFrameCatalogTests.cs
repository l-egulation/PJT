using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class SpecialFrameCatalogTests
    {
        static Catalog Combat() => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
        static CharacterAnimationCatalog Media() => JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
        static FixedAttackMotion First(CharacterAnimationCatalog media)
        {
            var clip = media.sets.Single(value => value.form == "base_sword").Find("attack1");
            return new FixedAttackMotion(new AttackMotionDocument { schemaVersion = 1, id = FixedAttackMotion.FirstSwordId, actionId = FixedAttackMotion.FirstSwordAction, mode = FixedAttackMotion.Mode,
                sourceImpact = clip.sourceImpact, fxStart = clip.fxStart, steps = clip.frames.Select((frame, index) => new AttackMotionStep { id = clip.stepIds[index], frame = frame, seconds = clip.seconds[index] }).ToArray() });
        }
        static ActionTrackDefinition[] Finite(Catalog combat) => combat.forms.SelectMany(value => value.specials).Where(skill => ActionTrackDefinition.Supports(skill.id)).Select(skill => new ActionTrackDefinition(skill)).ToArray();
        static AuthoredSkillDefinition[] Authored(Catalog combat) => combat.forms.SelectMany(form => form.specials.Where(skill => AuthoredSkillDefinition.Supports(skill.id)).Select(skill => new AuthoredSkillDefinition(form.id, skill))).ToArray();
        [Test] public void CatalogRequiresExactFiniteFourAndAuthoredNineteenSources()
        {
            var combat = Combat(); var media = Media(); var registry = new MotionSourceRegistry(media.motionRegistry, combat); var first = First(media);
            var finite = Finite(combat); var authored = Authored(combat); var document = ActionSpecialFrameCatalog.FixedSources(combat, registry, first, finite, authored);
            var catalog = new ActionSpecialFrameCatalog(document, combat, registry, first, finite, authored);
            Assert.That(catalog.Actions.Count, Is.EqualTo(23)); Assert.That(catalog.Actions.Select(value => value.Id), Is.EquivalentTo(ActionTrackDefinition.SupportedIds.Concat(AuthoredSkillDefinition.SupportedIds)));
            Assert.That(catalog.Actions.All(value => !value.UsesFrameTime), Is.True);
            Assert.That(new ActionSpecialFrameCatalog(catalog.ToDocument(), combat, registry, first, finite, authored).ContentHash, Is.EqualTo(catalog.ContentHash));
            Assert.That(catalog.ToDocument().actions.All(value => !string.IsNullOrEmpty(value.policyVersion) && !string.IsNullOrEmpty(value.mediaBindingVersion)), Is.True, "Applied snapshots retain their exact resolved fixed context.");
            Assert.That(catalog.ToSourceDocument().actions.All(value => string.IsNullOrEmpty(value.policyVersion) && string.IsNullOrEmpty(value.mediaBindingVersion) && value.timeline == null), Is.True, "Canonical fixed sources inherit rather than pin inactive context.");
            var stale = catalog.ToDocument(); stale.actions[0].mediaBindingVersion = "stale-applied-media";
            Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(stale, combat, registry, first, finite, authored));
            Assert.That(new ActionSpecialFrameCatalog(catalog.ToSourceDocument(), combat, registry, first, finite, authored).ContentHash, Is.EqualTo(catalog.ContentHash));
            var missing = catalog.ToDocument(); missing.actions = missing.actions.Skip(1).ToArray(); Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(missing, combat, registry, first, finite, authored));
            var duplicate = catalog.ToDocument(); duplicate.actions[0] = duplicate.actions[1]; Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(duplicate, combat, registry, first, finite, authored));
            var foreign = catalog.ToDocument(); foreign.actions[0].actionId = "rapier_default"; Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(foreign, combat, registry, first, finite, authored));
            Assert.Throws<ArgumentException>(() => new ActionSpecialFrameCatalog(document, combat, registry, first, finite.Skip(1), authored));
        }
        [Test] public void ActiveAuthoredFrameSourceRejectsNumericTimingBypassButAllowsDamageShapeAndEventDurations()
        {
            var original = Authored(Combat()).Single(value => value.Id == "whip_movement"); var numeric = original.ToNumericData();
            numeric.hits[0].damage += 7; numeric.hits[0].box.width += .5f; numeric.hits[0].root += .125f; numeric.movement.recastWindow += 1;
            var allowed = original.Recompile(numeric); Assert.DoesNotThrow(() => SpecialFrameTimingGuards.RequireAuthoredNumericEdit(original, allowed));
            // Keep the whole candidate legal: moving activation later would
            // put its unchanged first hit before activation and fail Recompile.
            numeric.activation -= .03125f; var invalid = original.Recompile(numeric);
            Assert.That(invalid.Execution.Hits[0].Time, Is.GreaterThan(invalid.Execution.Activation));
            Assert.Throws<ArgumentException>(() => SpecialFrameTimingGuards.RequireAuthoredNumericEdit(original, invalid));
            var dash = Authored(Combat()).Single(value => value.Id == "dagger_movement"); numeric = dash.ToNumericData(); numeric.movement.speed += 1;
            Assert.DoesNotThrow(() => SpecialFrameTimingGuards.RequireAuthoredNumericEdit(dash, dash.Recompile(numeric)));
            numeric = dash.ToNumericData(); numeric.movement.duration -= .03125f;
            Assert.Throws<ArgumentException>(() => SpecialFrameTimingGuards.RequireAuthoredNumericEdit(dash, dash.Recompile(numeric)));
            var smoke = Authored(Combat()).Single(value => value.Id == "dagger_range"); numeric = smoke.ToNumericData(); numeric.effect.duration += 1;
            Assert.Throws<ArgumentException>(() => SpecialFrameTimingGuards.RequireAuthoredNumericEdit(smoke, smoke.Recompile(numeric)));
        }
        [Test] public void ActiveFiniteFrameSourceAllowsNonTimingGraphPayloadButCannotReplaceTemporalEdges()
        {
            var original = Finite(Combat()).Single(value => value.Id == ActionTrackDefinition.MovementId); var numeric = original.ToNumericData(); var document = original.ToDocument();
            numeric.hits[0].damage += 3; numeric.dashSpeed += 1; numeric.wallRecovery += .125f; document.hits[0].box.width += .5f;
            var allowed = original.Recompile(document, numeric); Assert.DoesNotThrow(() => SpecialFrameTimingGuards.RequireFiniteNumericEdit(original, allowed));
            document.timeline.frames[0].seconds += .03125f; var changed = original.Recompile(document, numeric);
            Assert.Throws<ArgumentException>(() => SpecialFrameTimingGuards.RequireFiniteNumericEdit(original, changed));
            Assert.That(SpecialFrameTimingGuards.TemporalVersion(allowed), Is.EqualTo(SpecialFrameTimingGuards.TemporalVersion(original)));
        }
        [Test] public void CompiledModeDocumentAndDefinitionsAreDefensiveSnapshots()
        {
            var original = Authored(Combat()).Single(value => value.Id == "whip_default");
            var media = new MeleeFrameMedia("fixture/attack1", new[] { "first", "last" }, new AttackMotionTiming("fixture/attack1", new[] { "frame-a", "frame-b" }, new[] { .25f, .25f }, -1, .25f, -1, 0, 1), false, "whip_default/Special");
            var source = ActionSpecialFrameCompiler.ProjectAuthored(original, media); var compiled = ActionSpecialFrameCompiler.CompileAuthored(original, media, source); string hash = compiled.ContentHash;
            Assert.That(compiled.CompileMotion(), Is.SameAs(compiled.CompileMotion()), "The complete accepted FrameTime body is compiled once, not rehashed at Draw/start validation.");
            source.timeline.frames[0].seconds = 99; source.policyVersion = "changed";
            Assert.That(compiled.ContentHash, Is.EqualTo(hash)); Assert.That(compiled.Timeline.Frames[0].Seconds, Is.LessThan(2));
            var copy = compiled.ToDocument(); copy.timeline.frames[0].seconds = 80;
            Assert.That(compiled.Timeline.Frames[0].Seconds, Is.LessThan(2)); Assert.That(compiled.Execution.Authored, Is.SameAs(compiled.Authored));
        }
    }
}
