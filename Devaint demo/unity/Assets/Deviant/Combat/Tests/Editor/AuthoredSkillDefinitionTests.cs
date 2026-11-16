using System;
using System.Linq;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class AuthoredSkillDefinitionTests
    {
        public static AuthoredSkillDefinition[] Current()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            return AuthoredSkillDefinition.SupportedIds.Select(id => { var form = catalog.forms.Single(value => value.specials.Any(skill => skill.id == id)); return new AuthoredSkillDefinition(form.id, form.specials.Single(skill => skill.id == id)); }).ToArray();
        }
        [Test] public void EveryNineteenCanonicalPolicyCompilesAndRoundtripsWithoutNumericMutation()
        {
            var values = Current(); Assert.That(values.Length, Is.EqualTo(19));
            foreach (var value in values) {
                Assert.That(value.Execution.Authored, Is.SameAs(value)); Assert.That(value.Execution.Tracks, Is.Null);
                Assert.That(value.Recompile(value.ToNumericData()).ContentHash, Is.EqualTo(value.ContentHash), value.Id);
                Assert.That(new AuthoredSkillDefinition(value.FormId, value.ToData()).ContentHash, Is.EqualTo(value.ContentHash));
                var copy = value.ToData(); copy.end += 10; Assert.That(value.Execution.End, Is.Not.EqualTo(copy.end));
            }
        }
        [Test] public void SerializedFixedPresenceTagsAcceptOnlyEmptyAbsentInlineObjects()
        {
            foreach (var value in Current()) {
                var numeric = JsonUtility.FromJson<SkillNumericDocument>(JsonUtility.ToJson(value.ToNumericData()));
                Assert.That(value.Recompile(numeric).ContentHash, Is.EqualTo(value.ContentHash), value.Id);
                if (!numeric.hasMovement) { numeric.movement = new SkillMoveNumericData { speed = 1 }; Assert.Throws<ArgumentException>(() => value.Recompile(numeric)); }
                numeric = value.ToNumericData(); numeric.hasEffect = !numeric.hasEffect; Assert.Throws<ArgumentException>(() => value.Recompile(numeric));
            }
        }
        [Test] public void NumericEditsVersionContentButCannotChangeCountsKindsResourcesOrMetadata()
        {
            foreach (var value in Current()) {
                var data = value.ToNumericData(); data.end += .1f; if (data.hits.Length > 0) data.hits[0].damage += 1; else if (data.effect != null && value.CanEdit("effect", "duration")) data.effect.duration += .1f;
                var edited = value.Recompile(data); Assert.That(edited.SourceVersion, Is.EqualTo(value.SourceVersion)); Assert.That(edited.ContentHash, Is.Not.EqualTo(value.ContentHash));
                var bad = value.ToNumericData(); bad.hits = bad.hits.Concat(new[] { new SkillHitNumericData() }).ToArray(); Assert.Throws<ArgumentException>(() => value.Recompile(bad));
                var source = value.ToData(); source.cooldown += 1; var changedPolicy = new AuthoredSkillDefinition(value.FormId, source); Assert.That(changedPolicy.SourceVersion, Is.Not.EqualTo(value.SourceVersion));
            }
        }
        [Test] public void InvalidDirectWindowsGeometryDefenseAndFixedPoliciesRejectWholeCandidate()
        {
            var values = Current();
            foreach (var value in values) { var invalid = value.ToNumericData(); invalid.end = 0; Assert.Throws<ArgumentException>(() => value.Recompile(invalid)); }
            var rapier = values.Single(value => value.Id == "rapier_follow_up"); var freeze = rapier.ToNumericData(); freeze.verticalFreeze = new[] { .8f, .1f }; Assert.Throws<ArgumentException>(() => rapier.Recompile(freeze));
            var dagger = values.Single(value => value.Id == "dagger_range"); var smoke = dagger.ToNumericData(); smoke.effect.box.width = 0; Assert.Throws<ArgumentException>(() => dagger.Recompile(smoke));
            var mark = values.Single(value => value.Id == "whip_follow_up"); var marked = mark.ToNumericData(); marked.hits[1].box = new BoxData { width = 1, height = 1 }; Assert.Throws<ArgumentException>(() => mark.Recompile(marked));
            var hammer = values.Single(value => value.Id == "hammer_range"); var symmetrical = hammer.ToNumericData(); symmetrical.hits[0].box.x = 1; Assert.Throws<ArgumentException>(() => hammer.Recompile(symmetrical));
        }
        [Test] public void GroundSlamTargetsEveryEnemyOnScreenAtOneSecond()
        {
            var hammer = Current().Single(value => value.Id == "hammer_range"); var data = hammer.ToNumericData();
            Assert.That(hammer.ToData().hits[0].targets, Is.EqualTo(CombatCast.ScreenTargets));
            Assert.That(data.activation, Is.EqualTo(1f).Within(.0001f)); Assert.That(data.hits[0].time, Is.EqualTo(1f).Within(.0001f));
            Assert.That(data.hits[0].damage, Is.EqualTo(45f).Within(.0001f)); Assert.That(data.hits[0].stun, Is.EqualTo(1f).Within(.0001f));
            var boxOnly = hammer.ToData(); boxOnly.hits[0].targets = "all"; Assert.Throws<ArgumentException>(() => new AuthoredSkillDefinition(hammer.FormId, boxOnly));
        }
        [Test] public void DeadAnnotationsAreNotPresentedAsEditableConsumerKnobs()
        {
            var values = Current(); var dash = values.Single(value => value.Id == "dagger_movement"); Assert.That(dash.CanEdit("movement", "distance"), Is.False); Assert.That(dash.CanEdit("movement", "speed"), Is.True);
            var next = dash.ToNumericData(); next.movement.speed += 1; var changed = dash.Recompile(next); Assert.That(changed.Execution.Movement.Distance, Is.EqualTo(next.movement.speed * next.movement.duration));
            var teleport = values.Single(value => value.Id == "rapier_movement"); Assert.That(teleport.CanEdit("movement", "distance"), Is.False); Assert.That(teleport.CanEdit("movement", "targetRadius"), Is.True);
            var parry = values.Single(value => value.Id == "shield_default"); Assert.That(parry.Execution.Effect.ParryWindow, Is.EqualTo(.2f)); Assert.That(parry.Execution.Effect.ParryDamage, Is.EqualTo(26));
            Assert.That(parry.CanEdit("effect", "parryWindow"), Is.True); Assert.That(parry.CanEdit("effect", "duration"), Is.False);
        }
    }
}
