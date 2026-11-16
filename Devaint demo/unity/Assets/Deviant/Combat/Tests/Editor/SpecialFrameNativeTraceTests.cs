using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Intentionally uses only pre-SpecialFrame APIs: parent runs this exact
    // fixture before and after the cutover, with every float recorded as bits.
    public class SpecialFrameNativeTraceTests
    {
        [Serializable] sealed class Trace { public List<string> rows = new List<string>(); }
        static readonly MethodInfo BeginSpecial = typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
        [UnityTest] public IEnumerator GameAllTwentyThreeSpecialsKeepDefaultNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabAllTwentyThreeSpecialsKeepDefaultNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static void Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, string id, int scenario, CharacterAnimationCatalog media)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; enemy.maxHealth = enemy.health = 2000; }
            int form = Array.FindIndex(arena.catalog.forms, value => value.specials.Any(skill => skill.id == id));
            int special = Array.FindIndex(arena.catalog.forms[form].specials, value => value.id == id);
            Assert.That(player.TryReconfigure(0, form, special, out var error), Is.True, error);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            int facing = scenario == 1 ? -1 : 1; player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(facing);
            arena.enemies[0].Position = new Vector2(facing * 2, 1000); arena.enemies[1].Position = new Vector2(facing * 3, 1000); Physics2D.SyncTransforms();
            arena.characterAnimations = media;
            BeginSpecial.Invoke(player, new object[] { default(CombatCommandKey), -1f }); var cast = player.currentCast;
            Assert.That(cast, Is.Not.Null, id); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null, id);
            for (int tick = 0; tick < 180; tick++)
            {
                if (scenario == 2 && tick == 12) player.CancelAction();
                driver.Step(.02f, new CombatInput(scenario == 1 ? -1 : 0, CombatButtons.None)); arena.Presentation.Draw(0);
                var target = arena.enemies[0]; var effect = cast.Definition.Effect; var resource = cast.ResourceResult;
                trace.rows.Add(string.Join("|", new[] { id, scenario.ToString(), tick.ToString(), cast.DefinitionVersion, arena.Presentation.CurrentAttackMotionVersion,
                    arena.Presentation.CurrentForm, arena.Presentation.CurrentClip, player.visual.sprite ? player.visual.sprite.name : "", ReferenceEquals(player.currentCast, cast).ToString(),
                    Bits(arena.Now), Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(player.health), Bits(player.shield), Bits(player.guardUntil), Bits(player.stealthUntil), Bits(player.smokeUntil), cast.Invulnerable.ToString(), cast.FreezeVertical.ToString(), cast.Moving.ToString(),
                    Bits(target.health), Bits(target.Position.x), Bits(target.Position.y), Bits(target.rootUntil), Bits(target.stunUntil), Bits(target.knockUntil), Bits(target.knockSpeed), target.ReceivedHitCount.ToString(),
                    Bits(arena.enemies[1].health), arena.enemies[1].ReceivedHitCount.ToString(), player.Weapon.Cooldown.Available.ToString(), Bits(player.Weapon.Cooldown.NextReady),
                    resource == null ? "none" : resource.Status.ToString(), cast.ResourcesActive.ToString(), arena.casts.Count.ToString(), arena.decoys.Count.ToString(), arena.playerShots.Count.ToString(), arena.bombs.Count.ToString(),
                    effect?.Type ?? "", Bits(effect?.Duration ?? 0), arena.Presentation.ActiveEffects.ToString(), arena.Presentation.LastEffectHandle == null ? "none" : arena.Presentation.LastEffectHandle.Status.ToString(), arena.SimulationFaulted.ToString() }));
            }
        }
        static string Bits(float value) => BitConverter.SingleToInt32Bits(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var source = arena.characterAnimations; var trace = new Trace();
            var floor = new GameObject("SpecialFrameNativeTraceFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            var texture = new Texture2D(1, 1); var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var set in source.sets)
            {
                foreach (var clip in set.clips) foreach (string frame in clip.frames) Add(frame);
                foreach (var effect in set.effects ?? Array.Empty<CharacterEffect>()) foreach (string frame in effect.frames) Add(frame);
            }
            void Add(string frame) { if (sprites.ContainsKey(frame)) return; var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32); sprite.name = frame; sprites.Add(frame, sprite); }
            try
            {
                var ids = ActionTrackDefinition.SupportedIds.Concat(AuthoredSkillDefinition.SupportedIds).OrderBy(value => value, StringComparer.Ordinal).ToArray(); Assert.That(ids.Length, Is.EqualTo(23));
                using (var driver = arena.AcquireManualSimulation())
                    foreach (string id in ids) for (int scenario = 0; scenario < 3; scenario++)
                    {
                        var media = JsonUtility.FromJson<CharacterAnimationCatalog>(JsonUtility.ToJson(source));
                        media.motionSprites = source.motionSprites;
                        foreach (var set in media.sets)
                        {
                            foreach (var clip in set.clips) clip.sprites = clip.frames.Select(frame => sprites[frame]).ToArray();
                            foreach (var effect in set.effects ?? Array.Empty<CharacterEffect>()) effect.sprites = effect.frames.Select(frame => sprites[frame]).ToArray();
                        }
                        Run(trace, arena, driver, id, scenario, media);
                    }
                Assert.That(trace.rows.Count, Is.EqualTo(23 * 3 * 180));
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-specialFrameTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { arena.characterAnimations = source; arena.Presentation.Reset(); Object.Destroy(floor); foreach (var sprite in sprites.Values) Object.Destroy(sprite); Object.Destroy(texture); }
        }
    }
}
