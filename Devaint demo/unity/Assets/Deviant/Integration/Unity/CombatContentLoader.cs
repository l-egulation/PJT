using System;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Integration
{
    // Shared transport only; Game and Lab choose their own scene/profile/creation requests.
    public static class CombatContentLoader
    {
        public static CombatRuntimeConfiguration Load(CharacterAnimationCatalog suppliedAnimations, bool loadCharacterArt)
        {
            var content = ReadCatalog(suppliedAnimations, loadCharacterArt);
            foreach (var step in LoadMedia(content, loadCharacterArt)) { }
            return content;
        }
        public static System.Collections.IEnumerator LoadInFrames(CharacterAnimationCatalog suppliedAnimations, bool loadCharacterArt, Action<CombatRuntimeConfiguration> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            var content = ReadCatalog(suppliedAnimations, loadCharacterArt);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var step in LoadMedia(content, loadCharacterArt))
            {
                // Cached media should not add an artificial frame per batch.
                if (System.Diagnostics.Stopwatch.GetTimestamp() - started < System.Diagnostics.Stopwatch.Frequency / 250) continue;
                yield return null;
                started = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            completed(content);
        }
        static CombatRuntimeConfiguration ReadCatalog(CharacterAnimationCatalog suppliedAnimations, bool loadCharacterArt)
        {
            var content = new CombatRuntimeConfiguration {
                Catalog = JsonUtility.FromJson<Catalog>(Required("MeleeCatalog")),
                Locomotion = JsonUtility.FromJson<LocomotionData>(Required("PlayerLocomotion")),
                Animations = suppliedAnimations
            };
            if(loadCharacterArt)content.BindFeedback=arena=>HitFeedbackBridge.Attach(arena);
            bool unconfigured = suppliedAnimations == null ||
                (suppliedAnimations.sets == null || suppliedAnimations.sets.Length == 0) &&
                (suppliedAnimations.motionSources == null || suppliedAnimations.motionSources.Length == 0) && (suppliedAnimations.motionRegistry == null || suppliedAnimations.motionRegistry.IsEmpty);
            if (unconfigured)
            {
                var animations = Resources.Load<TextAsset>("CharacterV2/AnimationCatalog");
                if (!animations) throw new InvalidOperationException("CharacterV2/AnimationCatalog missing. Export the complete motion registry.");
                content.Animations = JsonUtility.FromJson<CharacterAnimationCatalog>(animations.text);
                var heavy = JsonUtility.FromJson<CharacterAnimationCatalog>(Required("CharacterV2/HeavyWeaponCatalog"));
                if (heavy?.sets == null || heavy.sets.Length != 3) throw new InvalidOperationException("The three approved heavy-weapon media sets are required.");
                var sets = new System.Collections.Generic.List<CharacterAnimationSet>(content.Animations.sets);
                foreach (var extra in heavy.sets)
                {
                    sets.RemoveAll(set => set.form == extra.form);
                    sets.Add(extra);
                }
                content.Animations.sets = sets.ToArray();
                if (content.Animations?.motionRegistry == null || content.Animations.motionRegistry.IsEmpty) throw new InvalidOperationException("Generated animation catalog is missing its complete motion registry. Export attack motion.");
            }
            return content;
        }
        // Both entry points populate the same dictionaries and clip arrays. The
        // asynchronous caller can draw its loading screen between media batches.
        // Character art is registered here but read only when a form is used or warmed
        // (CombatArena warms the equipped forms): every weapon's frames together exceeded the
        // Web heap budget on the chapter one to two transition (2026-11-16).
        static System.Collections.Generic.IEnumerable<bool> LoadMedia(CombatRuntimeConfiguration content, bool loadCharacterArt)
        {
            int loaded = 0;
            if (content.Animations?.motionRegistry?.palettes != null)
            {
                content.Animations.motionSprites = new System.Collections.Generic.Dictionary<string, Sprite>(StringComparer.Ordinal);
                content.Animations.deferMotionSprites = loadCharacterArt;
                foreach (var palette in content.Animations.motionRegistry.palettes)
                {
                    if (palette?.frames == null) throw new ArgumentException("A complete known motion media palette is required.");
                    foreach (var frame in palette.frames)
                    {
                        if (string.IsNullOrWhiteSpace(frame)) throw new ArgumentException("Known body frame references are required.");
                        if (!content.Animations.motionSprites.ContainsKey(frame))
                        {
                            content.Animations.motionSprites.Add(frame, null);
                            if (++loaded % 256 == 0) yield return true;
                        }
                    }
                }
            }
            if (loadCharacterArt && content.Animations?.sets != null)
                foreach (var set in content.Animations.sets)
                {
                    foreach (var clip in set.clips) clip.DeferLoad();
                    if (set.effects != null) foreach (var effect in set.effects) effect.DeferLoad();
                }
            // The bare-handed body is always on screen; read it now, under the loading screen.
            if (loadCharacterArt && content.Animations != null)
                foreach (var step in content.Animations.WarmForm("unarmed")) yield return step;
        }

        static string Required(string path)
        {
            var text = Resources.Load<TextAsset>(path);
            if (!text) throw new InvalidOperationException(path + " missing. Run export_unity_catalog.py.");
            return text.text;
        }
    }
}
