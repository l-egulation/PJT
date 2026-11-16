using System;
using System.Collections;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Combat.Tests
{
    public class BasicMeleeDefinitionTests
    {
        static BasicData Source()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            return Array.Find(catalog.forms, f => f.id == "base_sword").basics[0];
        }

        [Test] public void SnapshotDoesNotShareAuthoringOrExecutionBoxes()
        {
            var source = Source();
            Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_0", source, out var definition, out var error), Is.True, error);
            Assert.That(definition.Duration, Is.EqualTo(.33f));
            Assert.That(definition.Impact, Is.EqualTo(.13f));
            Assert.That(definition.Active, Is.EqualTo(.08f));
            Assert.That(definition.Damage, Is.EqualTo(13));
            var execution = definition.ToData(); var another = definition.ToData();
            source.box.width = 999; execution.box.width = 123; execution.impact = 77;
            Assert.That(definition.Width, Is.EqualTo(3.1875f));
            Assert.That(another.box.width, Is.EqualTo(definition.Width));
            Assert.That(another.impact, Is.EqualTo(.13f));
            var roundTrip = JsonUtility.FromJson<BasicData>(JsonUtility.ToJson(another));
            Assert.That(BasicMeleeDefinition.TryCreate(definition.Id, roundTrip, out var restored, out error), Is.True, error);
            Assert.That(restored.ContentHash, Is.EqualTo(definition.ContentHash));
            roundTrip.box.x += .25f;
            BasicMeleeDefinition.TryCreate(definition.Id, roundTrip, out var changed, out _);
            Assert.That(changed.ContentHash, Is.Not.EqualTo(definition.ContentHash));
        }

        [TestCase(-1f, .2f, .08f)]
        [TestCase(.4f, -.1f, .08f)]
        [TestCase(.4f, .2f, 0f)]
        [TestCase(.4f, .35f, .1f)]
        [TestCase(float.PositiveInfinity, .2f, .08f)]
        [TestCase(.4f, float.NaN, .08f)]
        public void InvalidTimingIsRejected(float duration, float impact, float active)
        {
            var data = Source(); data.duration = duration; data.impact = impact; data.active = active;
            Assert.That(BasicMeleeDefinition.TryCreate("base_sword_basic_0", data, out var definition, out var error), Is.False);
            Assert.That(definition, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [Test] public void InvalidGeometryAndUnsupportedPoliciesAreRejected()
        {
            var data = Source(); data.box.width = 0;
            Assert.That(BasicMeleeDefinition.TryCreate("first", data, out _, out _), Is.False);
            data = Source(); data.box.y = float.NaN;
            Assert.That(BasicMeleeDefinition.TryCreate("first", data, out _, out _), Is.False);
            data = Source(); data.kind = "ranged";
            Assert.That(BasicMeleeDefinition.TryCreate("first", data, out _, out _), Is.False);
            data = Source(); data.kind = "bomb"; data.bomb = new BombData();
            Assert.That(BasicMeleeDefinition.TryCreate("first", data, out _, out _), Is.False);
        }

        [UnityTest] public IEnumerator ArenaKeepsActiveVersionAndAppliesNextExecution() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator CommonGameUsesTheSameVersionedBasicHit() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene);
            UnityEngine.Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = UnityEngine.Object.FindAnyObjectByType<CombatArena>();
            var player = arena.Player; player.UseTestInput = true; player.axis = 0;
            yield return new WaitForFixedUpdate();
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "base_sword"), 0, out var setupError), Is.True, setupError);
            player.Position = player.spawn;
            var target = arena.enemies[0]; target.Position = player.Position + new Vector2(6, 0); target.aggressive = false;
            for (int i = 1; i < arena.enemies.Count; i++) { arena.enemies[i].Position = new Vector2(38, 0); arena.enemies[i].aggressive = false; }
            Physics2D.SyncTransforms();
            var original = arena.BasicSwordFirst;
            string catalogBefore = JsonUtility.ToJson(arena.catalog);
            player.QueueTestInput(basic: true); yield return Advance(arena, .08f);
            var oldCast = player.currentCast;
            Assert.That(oldCast, Is.Not.Null);
            Assert.That(oldCast.DefinitionVersion, Is.EqualTo(original.ContentHash));
            var edited = original.ToData(); edited.duration = .6f; edited.impact = .35f; edited.active = .1f; edited.damage = 24;
            edited.box.width = 8; edited.box.x = 4.1875f;
            Assert.That(arena.TryApplyBasicSwordFirst(edited, false, out var error), Is.True, error);
            var accepted = arena.BasicSwordFirst;
            edited.box.width = 999; edited.impact = 99;
            Assert.That(player.currentCast, Is.SameAs(oldCast));
            Assert.That(oldCast.DefinitionVersion, Is.EqualTo(original.ContentHash));
            Assert.That(oldCast.Definition.End, Is.EqualTo(.33f));
            Assert.That(oldCast.ImpactTime, Is.EqualTo(.13f));
            Assert.That(oldCast.Definition.Hits[0].Box.Width, Is.EqualTo(3.1875f));
            Assert.That(arena.BasicSwordFirst.Width, Is.EqualTo(8));
            yield return Advance(arena, .8f); // Allow combo window to expire: next input starts hit 1.
            Assert.That(target.health, Is.EqualTo(100), "The old narrow hit must not adopt the wider edited box.");
            for (int i = 0; i < player.Weapon.StatSlots.Length; i++) player.Weapon.StatSlots[i] = 3;
            player.QueueTestInput(basic: true); yield return Advance(arena, .08f);
            var next = player.currentCast;
            Assert.That(next.DefinitionVersion, Is.EqualTo(accepted.ContentHash));
            Assert.That(next.ImpactTime, Is.EqualTo(.35f / player.Weapon.Speed));
            Assert.That(next.Definition.End, Is.EqualTo(.6f / player.Weapon.Speed));
            Assert.That(next.Definition.Hits[0].Active, Is.EqualTo(.1f / player.Weapon.Speed));
            Assert.That(target.health, Is.EqualTo(100), "The new hit must wait for its new impact time.");
            var invalid = accepted.ToData(); invalid.active = 99;
            Assert.That(arena.TryApplyBasicSwordFirst(invalid, true, out error), Is.False);
            Assert.That(player.currentCast, Is.SameAs(next));
            Assert.That(arena.BasicSwordFirst, Is.SameAs(accepted));
            yield return Advance(arena, .25f);
            Assert.That(target.health, Is.EqualTo(76), "The new timing and wide box must produce the edited damage once.");
            Assert.That(arena.TryApplyBasicSwordFirst(original.ToData(), true, out error), Is.True, error);
            Assert.That(player.currentCast, Is.Null);
            Assert.That(arena.casts, Is.Empty);
            Assert.That(target.health, Is.EqualTo(100));
            Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(original.ContentHash));
            Assert.That(JsonUtility.ToJson(arena.catalog), Is.EqualTo(catalogBefore), "Temporary overrides must not rewrite the loaded catalog.");
        }

        static IEnumerator Advance(CombatArena arena, float seconds)
        {
            float target = arena.Now + seconds, limit = Time.realtimeSinceStartup + 5;
            while (arena.Now < target && Time.realtimeSinceStartup < limit) yield return new WaitForFixedUpdate();
            Assert.That(arena.Now, Is.GreaterThanOrEqualTo(target));
        }
    }
}
