using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Weapon/skill audio mix. The shipped default mix is Resources/AudioMixDefaults.json (the team's agreed
    // per-weapon and per-skill volumes); a device-local PlayerPrefs value overrides it. Run/profile resets do not
    // own these preferences.
    public static class WeaponAudioSettings
    {
        public const float DefaultVolume = .8f, Step = .05f;
        public static readonly string[] Forms = { "base_sword", "whip", "rapier", "dagger", "hammer", "shield", "greatsword", "launcher", "bomb", "shuriken", "unarmed" };
        public static readonly string[] Names = { "검", "채찍", "레이피어", "한손 단검", "망치", "방패", "대검", "발사기", "폭탄", "수리검", "맨손" };

        [Serializable] public sealed class MixEntry { public string id; public float volume; }
        [Serializable] public sealed class Mix { public MixEntry[] weapons; public MixEntry[] skills; }
        static Dictionary<string, float> weaponDefaults, skillDefaults;
        static void LoadMix()
        {
            if (weaponDefaults != null) return;
            weaponDefaults = new Dictionary<string, float>(StringComparer.Ordinal);
            skillDefaults = new Dictionary<string, float>(StringComparer.Ordinal);
            var json = Resources.Load<TextAsset>("AudioMixDefaults");
            if (!json) return;
            var mix = JsonUtility.FromJson<Mix>(json.text);
            foreach (var e in mix.weapons ?? Array.Empty<MixEntry>()) weaponDefaults[e.id] = Round(e.volume);
            foreach (var e in mix.skills ?? Array.Empty<MixEntry>()) skillDefaults[e.id] = Round(e.volume);
        }
        static float Round(float value) => Mathf.Round(Mathf.Clamp01(value) * 20) / 20f;
        // The shipped default of one weapon (DefaultVolume when the mix does not name it).
        public static float DefaultFor(string form)
        {
            LoadMix();
            return form != null && weaponDefaults.TryGetValue(form, out var v) ? v : DefaultVolume;
        }
        // The shipped default of one skill, if the mix sets one; otherwise the skill inherits its weapon.
        public static bool TrySkillDefault(string id, out float volume)
        {
            LoadMix();
            volume = 0;
            return id != null && skillDefaults.TryGetValue(id, out volume);
        }

        public static string Key(string form) => "deviant.audio.weapon." + form;
        public static float Get(string form) => string.IsNullOrEmpty(form) ? DefaultVolume
            : Clean(PlayerPrefs.GetFloat(Key(form), DefaultFor(form)), DefaultFor(form));
        static float Clean(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Round(value);
        public static void Set(string form, float value)
        {
            if (Array.IndexOf(Forms, form) < 0) throw new ArgumentException("Unknown weapon", nameof(form));
            PlayerPrefs.SetFloat(Key(form), Clean(value, DefaultFor(form))); PlayerPrefs.Save();
        }
        public static void ResetAll()
        {
            foreach (var form in Forms) PlayerPrefs.DeleteKey(Key(form));
            PlayerPrefs.Save();
        }
        public static string WeaponForCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("sfx_weapon_", StringComparison.Ordinal)) id = id.Substring(11);
            if (id.StartsWith("sfx_hit_", StringComparison.Ordinal))
            {
                id = id.Substring(8);
                if (id == "sword") id = "base_sword";
            }
            return WeaponHitSounds.WeaponOf(id);
        }
        public static float ForCue(string id) => Get(WeaponForCue(id));
    }
}
