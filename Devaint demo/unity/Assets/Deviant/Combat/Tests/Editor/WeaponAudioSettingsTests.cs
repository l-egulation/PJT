using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public sealed class WeaponAudioSettingsTests
    {
        readonly Dictionary<string, float?> previous = new Dictionary<string, float?>();
        [SetUp] public void Setup()
        {
            foreach(var form in WeaponAudioSettings.Forms)
            {
                string key=WeaponAudioSettings.Key(form);
                previous[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetFloat(key):(float?)null;
                PlayerPrefs.DeleteKey(key);
            }
        }
        [TearDown] public void Restore()
        {
            foreach(var entry in previous)
                if(entry.Value.HasValue)PlayerPrefs.SetFloat(entry.Key,entry.Value.Value);else PlayerPrefs.DeleteKey(entry.Key);
            PlayerPrefs.Save();previous.Clear();
        }
        [Test] public void EachWeaponPersistsIndependentlyAndClampsToAudioSourceRange()
        {
            WeaponAudioSettings.Set("hammer", .45f);
            Assert.That(WeaponAudioSettings.ForCue("hammer_basic_1"),Is.EqualTo(.45f).Within(.001f));
            Assert.That(WeaponAudioSettings.ForCue("sfx_weapon_hammer_range"),Is.EqualTo(.45f).Within(.001f));
            Assert.That(WeaponAudioSettings.ForCue("sfx_hit_hammer"),Is.EqualTo(.45f).Within(.001f));
            Assert.That(PlayerPrefs.GetFloat(WeaponAudioSettings.Key("hammer")),Is.EqualTo(.45f).Within(.001f));
            Assert.That(WeaponAudioSettings.ForCue("dagger_basic_0"),Is.EqualTo(.8f));
            WeaponAudioSettings.Set("hammer", 2);Assert.That(WeaponAudioSettings.Get("hammer"),Is.EqualTo(1));
            WeaponAudioSettings.Set("hammer", -1);Assert.That(WeaponAudioSettings.Get("hammer"),Is.Zero);
            WeaponAudioSettings.Set("hammer", float.NaN);Assert.That(WeaponAudioSettings.Get("hammer"),Is.EqualTo(.8f));
        }
        [TestCase("sfx_hit_sword", "base_sword")]
        [TestCase("sfx_hit_launcher_laser", "launcher")]
        [TestCase("sfx_weapon_greatsword_basic_0", "greatsword")]
        [TestCase("base_sword_follow_up", "base_sword")]
        [TestCase("sfx_player_heal", null)]
        [TestCase("explosion_bomb", null)]
        public void CueRoutingDoesNotAffectUnrelatedAudio(string cue,string weapon)
            => Assert.That(WeaponAudioSettings.WeaponForCue(cue),Is.EqualTo(weapon));
        [Test] public void ResetRestoresDefaultForAllWeapons()
        {
            foreach(var weapon in WeaponAudioSettings.Forms)WeaponAudioSettings.Set(weapon,.15f);
            WeaponAudioSettings.ResetAll();
            foreach(var weapon in WeaponAudioSettings.Forms)Assert.That(WeaponAudioSettings.Get(weapon),Is.EqualTo(WeaponAudioSettings.DefaultFor(weapon)));
        }
        // The team mix agreed on 2026-11-16 (Resources/AudioMixDefaults.json) is what a device without saved values hears.
        [TestCase("shield", .9f)]
        [TestCase("bomb", .2f)]
        [TestCase("hammer", .8f)]
        [TestCase("launcher", .8f)]
        [TestCase("base_sword", .8f)]
        public void ShippedWeaponMixIsTheDefault(string weapon, float volume)
            => Assert.That(WeaponAudioSettings.Get(weapon), Is.EqualTo(volume).Within(.001f));
    }
}
