using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public sealed class SkillAudioSettingsTests
    {
        readonly Dictionary<string, float?> previous = new Dictionary<string, float?>();
        [SetUp] public void Setup()
        {
            foreach (var key in SkillAudioSettings.Ids.Select(SkillAudioSettings.Key).Concat(WeaponAudioSettings.Forms.Select(WeaponAudioSettings.Key)))
            {
                previous[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : (float?)null;
                PlayerPrefs.DeleteKey(key);
            }
        }
        [TearDown] public void Restore()
        {
            foreach (var entry in previous)
                if (entry.Value.HasValue) PlayerPrefs.SetFloat(entry.Key, entry.Value.Value); else PlayerPrefs.DeleteKey(entry.Key);
            PlayerPrefs.Save(); previous.Clear();
        }
        [Test] public void UneditedSkillsInheritButAnOverrideIsIndependentOfWeaponAndSibling()
        {
            WeaponAudioSettings.Set("base_sword", .6f);
            Assert.That(SkillAudioSettings.Get("base_sword_range"), Is.EqualTo(.6f));
            SkillAudioSettings.Set("base_sword_follow_up", .35f);
            WeaponAudioSettings.Set("base_sword", .9f);
            Assert.That(SkillAudioSettings.Get("base_sword_follow_up"), Is.EqualTo(.35f));
            Assert.That(SkillAudioSettings.Get("base_sword_range"), Is.EqualTo(.9f));
            Assert.That(SkillAudioSettings.ForCue("base_sword_basic_0"), Is.EqualTo(.9f));
            Assert.That(PlayerPrefs.GetFloat(SkillAudioSettings.Key("base_sword_follow_up")), Is.EqualTo(.35f));
            SkillAudioSettings.Reset("base_sword_follow_up");
            Assert.That(SkillAudioSettings.Get("base_sword_follow_up"), Is.EqualTo(.9f));
        }
        [Test] public void EverySelectedSkillEventRoutesToItsOwnSkillEvenWithSharedClips()
        {
            Assert.That(SkillAudioSettings.Ids.Count(), Is.EqualTo(40));
            var data = JsonUtility.FromJson<ApprovedCombatAudio.Catalog>(Resources.Load<TextAsset>("SelectedSkillAudio/Catalog").text);
            foreach (var entry in data.entries.Where(e => e.id.Contains("__")))
            {
                string skill = entry.id.Split(new[] { "__" }, System.StringSplitOptions.None)[0];
                Assert.That(SkillAudioSettings.Contains(skill), Is.True, entry.id);
                SkillAudioSettings.Set(skill, .25f);
                Assert.That(SkillAudioSettings.ForCue(entry.id, entry.weapon), Is.EqualTo(.25f), entry.id);
                SkillAudioSettings.Reset(skill);
            }
            SkillAudioSettings.Set("bomb_range_special", .2f);
            SkillAudioSettings.Set("shuriken_range_special", .65f);
            Assert.That(SkillAudioSettings.ForCue("bomb_range_special__explosion"), Is.EqualTo(.2f));
            Assert.That(SkillAudioSettings.ForCue("shuriken_range_special__explosion"), Is.EqualTo(.65f));
            Assert.That(SkillAudioSettings.ForCue("explosion_bomb"), Is.EqualTo(.8f));
        }
        [Test] public void HitAndChargeImpactKeepTheirOriginatingSkillVolume()
        {
            SkillAudioSettings.Set("launcher_range_special", .3f);
            SkillAudioSettings.Set("launcher_basic_special", .55f);
            Assert.That(SkillAudioSettings.ForCue("sfx_hit_launcher_laser", actionId: "launcher_range_special"), Is.EqualTo(.3f));
            Assert.That(SkillAudioSettings.ForCue("launcher_charge_impact", "launcher"), Is.EqualTo(.55f));
            Assert.That(SkillAudioSettings.ForCue("sfx_hit_launcher_ball", actionId: "launcher_basic_0"), Is.EqualTo(.8f));
        }
        // The team mix agreed on 2026-11-16: named skills use their own shipped volume, others inherit the weapon's.
        [TestCase("dagger_range", .15f)]
        [TestCase("shield_follow_up", .25f)]
        [TestCase("greatsword_range", .2f)]
        [TestCase("launcher_range_special", .05f)]
        [TestCase("launcher_move_special", .6f)]
        [TestCase("launcher_followup_special", .45f)]
        [TestCase("bomb_range_special", .2f)]
        [TestCase("shield_range", .9f)]
        [TestCase("base_sword_range", .8f)]
        public void ShippedSkillMixIsTheDefault(string skill, float volume)
            => Assert.That(SkillAudioSettings.Get(skill), Is.EqualTo(volume).Within(.001f));
        [Test] public void ResetAndClampingDoNotChangeWeaponPreferences()
        {
            WeaponAudioSettings.Set("dagger", .7f);
            SkillAudioSettings.Set("dagger_range", -1);
            Assert.That(SkillAudioSettings.Get("dagger_range"), Is.Zero);
            SkillAudioSettings.Set("dagger_range", 2);
            Assert.That(SkillAudioSettings.Get("dagger_range"), Is.EqualTo(1));
            SkillAudioSettings.Set("dagger_range", float.NaN);
            Assert.That(SkillAudioSettings.Get("dagger_range"), Is.EqualTo(.7f));
            SkillAudioSettings.ResetAll();
            Assert.That(SkillAudioSettings.IsOverridden("dagger_range"), Is.False);
            Assert.That(WeaponAudioSettings.Get("dagger"), Is.EqualTo(.7f));
        }
    }
}
