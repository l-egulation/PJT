using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Per-action overrides preserve the weapon mix until the user edits a skill.
    public static class SkillAudioSettings
    {
        static Dictionary<string, string> weapons;
        static Dictionary<string, string> Weapons
        {
            get
            {
                if (weapons != null) return weapons;
                weapons = new Dictionary<string, string>(StringComparer.Ordinal);
                var json = Resources.Load<TextAsset>("SelectedSkillAudio/Catalog");
                if (json)
                    foreach (var entry in JsonUtility.FromJson<ApprovedCombatAudio.Catalog>(json.text).entries)
                        if (!entry.basic && !entry.id.Contains("__") && !entry.id.StartsWith("explosion_", StringComparison.Ordinal))
                            weapons[entry.id] = entry.weapon;
                return weapons;
            }
        }
        public static IEnumerable<string> Ids => Weapons.Keys;
        public static bool Contains(string id) => id != null && Weapons.ContainsKey(id);
        public static string Key(string id) => "deviant.audio.skill." + id;
        public static bool IsOverridden(string id) => Contains(id) && PlayerPrefs.HasKey(Key(id));
        public static float Get(string id)
        {
            if (!Contains(id)) throw new ArgumentException("Unknown skill", nameof(id));
            // Shipped skill default (AudioMixDefaults.json) if the mix names this skill; otherwise the weapon's volume.
            float fallback = WeaponAudioSettings.TrySkillDefault(id, out var shipped) ? shipped : WeaponAudioSettings.Get(Weapons[id]);
            return Clean(PlayerPrefs.GetFloat(Key(id), fallback), fallback);
        }
        static float Clean(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value)
            ? fallback : Mathf.Round(Mathf.Clamp01(value) * 20) / 20f;
        public static void Set(string id, float value)
        {
            if (!Contains(id)) throw new ArgumentException("Unknown skill", nameof(id));
            PlayerPrefs.SetFloat(Key(id), Clean(value, WeaponAudioSettings.Get(Weapons[id])));
            PlayerPrefs.Save();
        }
        public static void Reset(string id)
        {
            if (!Contains(id)) throw new ArgumentException("Unknown skill", nameof(id));
            PlayerPrefs.DeleteKey(Key(id)); PlayerPrefs.Save();
        }
        public static void ResetAll()
        {
            foreach (var id in Ids) PlayerPrefs.DeleteKey(Key(id));
            PlayerPrefs.Save();
        }
        public static string SkillForCue(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return null;
            if (cue.StartsWith("sfx_weapon_", StringComparison.Ordinal)) cue = cue.Substring(11);
            if (cue == "launcher_charge_impact") cue = "launcher_basic_special";
            int split = cue.IndexOf("__", StringComparison.Ordinal);
            if (split >= 0) cue = cue.Substring(0, split);
            return Contains(cue) ? cue : null;
        }
        public static float ForCue(string cue, string weapon = null, string actionId = null)
        {
            string skill = SkillForCue(actionId) ?? SkillForCue(cue);
            return skill != null ? Get(skill) : string.IsNullOrEmpty(weapon)
                ? WeaponAudioSettings.ForCue(cue) : WeaponAudioSettings.Get(weapon);
        }
    }
}
