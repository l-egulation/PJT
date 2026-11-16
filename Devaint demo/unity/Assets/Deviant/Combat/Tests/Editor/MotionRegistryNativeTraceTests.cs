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
    public class MotionRegistryNativeTraceTests
    {
        readonly struct ActionCase
        {
            public readonly string Form, Id;
            public readonly int Hit, Special;
            public bool Basic => Hit >= 0;
            public string Key => Form + "/" + Id + (Basic ? "/basic/" + Hit + "/selection/" + Special : "/special/" + Special);
            public ActionCase(string form, string id, int hit, int special) { Form = form; Id = id; Hit = hit; Special = special; }
        }
        [Serializable] sealed class Trace { public List<string> rows = new List<string>(); }
        static readonly MethodInfo BeginBasic = typeof(CombatPlayer).GetMethod("BeginBasic", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo BeginSpecial = typeof(CombatPlayer).GetMethod("BeginSpecial", BindingFlags.Instance | BindingFlags.NonPublic);
        [UnityTest] public IEnumerator GameDefaultAllSixtyBindingsKeepNativeBodyAndGameplayTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabDefaultAllSixtyBindingsKeepNativeBodyAndGameplayTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        // This fixture deliberately uses only pre-registry APIs. The exact same
        // file runs before and after cutover; no production-timing normalization.
        static CharacterAnimationCatalog Bind(CharacterAnimationCatalog source, Dictionary<string, Sprite> sprites)
        {
            var media = JsonUtility.FromJson<CharacterAnimationCatalog>(JsonUtility.ToJson(source));
            foreach (var set in media.sets)
            {
                foreach (var clip in set.clips) clip.sprites = clip.frames.Select(frame => sprites[frame]).ToArray();
                foreach (var effect in set.effects ?? Array.Empty<CharacterEffect>()) effect.sprites = effect.frames.Select(frame => sprites[frame]).ToArray();
            }
            return media;
        }
        static IEnumerable<ActionCase> Cases(Catalog combat)
        {
            foreach (var form in combat.forms)
            {
                for (int hit = 0; hit < form.basics.Length; hit++)
                    if (MotionSourceRegistry.HasInkAppearance(form.id))
                        for (int variant = 0; variant < MotionSourceRegistry.InkAppearanceCount; variant++) yield return new ActionCase(form.id, form.id + "_basic_" + hit, hit, variant);
                    else yield return new ActionCase(form.id, form.id + "_basic_" + hit, hit, 0);
                for (int special = 0; special < form.specials.Length; special++)
                    if (!form.specials[special].unavailable) yield return new ActionCase(form.id, form.specials[special].id, -1, special);
            }
        }
        static void Run(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, ActionCase action, CharacterAnimationCatalog media)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; enemy.health = 1000; }
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == action.Form), action.Special, out var error), Is.True, error);
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 1000); Physics2D.SyncTransforms(); arena.characterAnimations = media;
            if (action.Basic) BeginBasic.Invoke(player, new object[] { default(CombatCommandKey), action.Hit, -1f });
            else BeginSpecial.Invoke(player, new object[] { default(CombatCommandKey), -1f });
            var cast = player.currentCast;
            Assert.That(cast, Is.Not.Null, action.Key); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null, action.Key);
            for (int tick = 0; tick < 90; tick++)
            {
                driver.Step(.02f, new CombatInput(0, CombatButtons.None, tick < 25)); arena.Presentation.Draw(0);
                var receipt = cast.ResourceResult;
                trace.rows.Add(string.Join("|", new[] { action.Key, tick.ToString(), arena.Presentation.CurrentForm, arena.Presentation.CurrentClip, player.visual.sprite ? player.visual.sprite.name : "",
                    Bits(arena.Now), Bits(cast.StartedAt), Bits(cast.Age), Bits(cast.ImpactTime), Bits(cast.MotionDuration), Bits(cast.EndTime), Bits(player.Position.x), Bits(player.Position.y), Bits(player.velocity.x), Bits(player.velocity.y),
                    Bits(arena.enemies[0].health), arena.enemies[0].ReceivedHitCount.ToString(), player.Weapon.Cooldown.Available.ToString(), Bits(player.Weapon.Cooldown.NextReady),
                    arena.casts.Count.ToString(), arena.playerShots.Count.ToString(), arena.bombs.Count.ToString(), arena.Presentation.ActiveEffects.ToString(),
                    receipt == null ? "none" : receipt.Status.ToString(), cast.ChargeRelease.HasValue.ToString(), cast.ChargeActive.ToString(), cast.ResourcesActive.ToString(), ReferenceEquals(player.currentCast, cast).ToString(),
                    arena.Presentation.LastEffectHandle == null ? "none" : arena.Presentation.LastEffectHandle.Status.ToString(), arena.SimulationFaulted.ToString() }));
            }
        }
        static string Bits(float value) => BitConverter.SingleToInt32Bits(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var source = arena.characterAnimations; var trace = new Trace();
            var floor = new GameObject("RegistryNativeTraceFloor"); floor.transform.position = new Vector3(0, 999.5f, 0); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>(); Physics2D.SyncTransforms();
            var texture = new Texture2D(1, 1); var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var set in source.sets)
            {
                foreach (var clip in set.clips) foreach (string frame in clip.frames) Add(frame);
                foreach (var effect in set.effects ?? Array.Empty<CharacterEffect>()) foreach (string frame in effect.frames) Add(frame);
            }
            void Add(string frame) { if (sprites.ContainsKey(frame)) return; var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32); sprite.name = frame; sprites.Add(frame, sprite); }
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    int count = 0;
                    foreach (var action in Cases(arena.catalog)) { Run(trace, arena, driver, action, Bind(source, sprites)); count++; }
                    Assert.That(count, Is.EqualTo(60)); Assert.That(trace.rows.Count, Is.EqualTo(60 * 90));
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-motionRegistryTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally { arena.characterAnimations = source; arena.Presentation.Reset(); Object.Destroy(floor); foreach (var sprite in sprites.Values) Object.Destroy(sprite); Object.Destroy(texture); }
        }
    }
}
