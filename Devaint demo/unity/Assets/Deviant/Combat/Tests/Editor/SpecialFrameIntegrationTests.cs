using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class SpecialFrameIntegrationTests
    {
        [UnityTest] public IEnumerator GameCapturesOneSpecialFramePayloadAndRetainsIndependentTail() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabCapturesOneSpecialFramePayloadAndRetainsIndependentTail() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string id)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; enemy.maxHealth = enemy.health = 1000; }
            int form = Array.FindIndex(arena.catalog.forms, value => value.specials.Any(skill => skill.id == id));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
            Assert.That(player.TryReconfigure(0, form, special, out var error), Is.True, error); foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms();
        }
        static ActionSpecialFrameDefinition Shift(ActionSpecialFrameDefinition current)
        {
            var source = current.Family == SpecialFrameFamily.FiniteTracks
                ? ActionSpecialFrameCompiler.ProjectFinite(current.NumericFinite, current.FormId, current.Media)
                : ActionSpecialFrameCompiler.ProjectAuthored(current.NumericAuthored, current.Media);
            source.timeline.frames[0].seconds += .03125f;
            return current.Family == SpecialFrameFamily.FiniteTracks ? ActionSpecialFrameCompiler.CompileFinite(current.NumericFinite, current.FormId, current.Media, source)
                : ActionSpecialFrameCompiler.CompileAuthored(current.NumericAuthored, current.Media, source);
        }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            Assert.That(arena.SpecialFrames.Actions.Count, Is.EqualTo(23)); var original = arena.SpecialFrames.Actions.ToArray();
            var floor = new GameObject("SpecialFrameIntegrationFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            var texture = new Texture2D(2, 1); var sprites = new[] { Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32), Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero, 32) };
            foreach (var set in arena.characterAnimations.sets) foreach (var clip in set.clips) clip.sprites = clip.frames.Select((_, index) => sprites[index % 2]).ToArray();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    foreach (string id in new[] { ActionTrackDefinition.FollowUpId, ActionTrackDefinition.MovementId, "whip_default", "rapier_follow_up", "dagger_range", "hammer_follow_up" })
                    {
                        Setup(arena, driver, id); Assert.That(arena.TryApplySpecialFrames(original, false, out var error), Is.True, error);
                        var old = arena.GetSpecialFrame(id); var edited = Shift(old);
                        driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var running = player.currentCast;
                        Assert.That(running, Is.Not.Null); string oldPayload = running.DefinitionVersion, oldMedia = arena.Presentation.CurrentAttackMotionVersion;
                        float now = arena.Now; Assert.That(arena.TryApplySpecialFrames(new[] { edited }, false, out error), Is.True, error);
                        Assert.That(player.currentCast, Is.SameAs(running)); Assert.That(arena.Now, Is.EqualTo(now));
                        Assert.That(running.DefinitionVersion, Is.EqualTo(oldPayload)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(oldMedia));
                        Setup(arena, driver, id); driver.Step(.02f, new CombatInput(0, CombatButtons.Special)); var next = player.currentCast;
                        Assert.That(next.DefinitionVersion, Is.EqualTo(edited.ContentHash)); Assert.That(next.SpecialFrame, Is.SameAs(arena.GetSpecialFrame(id)));
                        Assert.That(next.Definition.Activation, Is.EqualTo(edited.Execution.Activation)); Assert.That(next.Definition.End, Is.EqualTo(edited.Execution.End));
                        Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(edited.CompileMotion().ContentHash));
                        var applied = arena.GetSpecialFrame(id);
                        if (applied.Authored != null) Assert.That(next.Definition.Authored, Is.SameAs(applied.Authored)); else Assert.That(next.Definition.Tracks, Is.SameAs(applied.Finite));
                        var invalid = edited.ToDocument(); invalid.timeline.frames[0].id = "not-shared"; var snapshot = arena.SpecialFrames.ContentHash; now = arena.Now;
                        Assert.Catch<ArgumentException>(() => { if (edited.Family == SpecialFrameFamily.FiniteTracks) ActionSpecialFrameCompiler.CompileFinite(edited.NumericFinite, edited.FormId, edited.Media, invalid); else ActionSpecialFrameCompiler.CompileAuthored(edited.NumericAuthored, edited.Media, invalid); });
                        Assert.That(arena.SpecialFrames.ContentHash, Is.EqualTo(snapshot)); Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(player.currentCast, Is.SameAs(next));
                    }
                    Setup(arena, driver, "whip_default"); var current = arena.GetSpecialFrame("whip_default"); var active = Shift(original.Single(value => value.Id == current.Id));
                    Assert.That(arena.TryApplySpecialFrames(new[] { active }, false, out var applyError), Is.True, applyError);
                    var numeric = arena.GetNumericAuthoredSkill(current.Id); var numbers = numeric.ToNumericData(); numbers.activation -= .03125f;
                    string retained = arena.SpecialFrames.ContentHash; float retainedNow = arena.Now;
                    Assert.That(arena.TryApplyAuthoredSkills(new[] { numeric.Recompile(numbers) }, true, out applyError), Is.False);
                    Assert.That(arena.SpecialFrames.ContentHash, Is.EqualTo(retained)); Assert.That(arena.Now, Is.EqualTo(retainedNow));
                    numbers = numeric.ToNumericData(); numbers.hits[0].damage += 3;
                    Assert.That(arena.TryApplyAuthoredSkills(new[] { numeric.Recompile(numbers) }, false, out applyError), Is.True, applyError);
                    Assert.That(arena.GetSpecialFrame(current.Id).Execution.Hits[0].Damage, Is.EqualTo(numbers.hits[0].damage));
                    Assert.That(arena.GetSpecialFrame(current.Id).Timeline.ContentHash, Is.EqualTo(active.Timeline.ContentHash));
                    Assert.That(arena.TryApplyAuthoredSkills(new[] { numeric }, false, out applyError), Is.True, applyError);
                    Assert.That(arena.TryApplySpecialFrames(original, true, out applyError), Is.True, applyError);
                }
            }
            finally { arena.Presentation.Reset(); Object.Destroy(floor); foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture); }
        }
    }
}
