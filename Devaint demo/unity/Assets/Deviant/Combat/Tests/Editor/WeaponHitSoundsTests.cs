using System;
using Deviant.Combat.Contracts;
using NUnit.Framework;

namespace Deviant.Combat.Tests
{
    // The weapon (and a few skills) decides the hit sound, never the monster that was hit (2026-11-13).
    public sealed class WeaponHitSoundsTests
    {
        static CombatHitOrigin Origin(string action, CombatHitKind kind = CombatHitKind.Melee) =>
            new CombatHitOrigin(default, default, Guid.NewGuid(), 0, null, Guid.Empty, kind, actionId: action);

        [TestCase("base_sword_basic_0", "sfx_hit_sword")]
        [TestCase("base_sword_range", "sfx_hit_sword")]
        [TestCase("greatsword_follow_up", "sfx_hit_greatsword")]
        [TestCase("rapier_basic_2", "sfx_hit_rapier")]
        [TestCase("dagger_movement", "sfx_hit_dagger")]
        [TestCase("whip_range", "sfx_hit_whip")]
        [TestCase("hammer_basic_1", "sfx_hit_hammer")]
        [TestCase("shield_default", "sfx_hit_shield")]
        [TestCase("shuriken_basic_special", "sfx_hit_shuriken")]
        [TestCase("unarmed_basic_0", "sfx_hit_unarmed")]
        [TestCase("launcher_basic_0", "sfx_hit_launcher_ball")]
        [TestCase("launcher_move_special", "sfx_hit_launcher_ball")]
        [TestCase("launcher_range_special", "sfx_hit_launcher_laser")]
        public void WeaponOrSkillPicksTheHitSound(string action, string expected)
        {
            Assert.That(WeaponHitSounds.For(Origin(action)), Is.EqualTo(expected));
            Assert.That(SelectedAudioClips.Find(expected), Is.Not.Null, expected + " is registered");
        }

        [Test]
        public void EveryWeaponAndSkillHitUsesTheCommonClip()
        {
            // 2026-11-16 user selection: one common hit sound for every weapon basic attack and skill.
            foreach (var id in new[] { "sfx_hit_sword", "sfx_hit_whip", "sfx_hit_rapier", "sfx_hit_dagger", "sfx_hit_hammer", "sfx_hit_shield",
                         "sfx_hit_greatsword", "sfx_hit_launcher_ball", "sfx_hit_launcher_laser", "sfx_hit_shuriken", "sfx_hit_unarmed" })
                Assert.That(SelectedAudioClips.Find(id)?.name, Is.EqualTo("sfx_hit_common_01_v01"), id);
        }

        [Test]
        public void ExplosionsInkPartsLasersAndParriesKeepTheirOwnRules()
        {
            Assert.That(WeaponHitSounds.For(Origin("bomb_basic_0", CombatHitKind.Blast)), Is.Null, "the blast sound plays instead");
            Assert.That(WeaponHitSounds.For(Origin("launcher_followup_special", CombatHitKind.Fragment)), Is.Null);
            Assert.That(WeaponHitSounds.For(Origin("bomb_move_special", CombatHitKind.Missile)), Is.Null);
            Assert.That(WeaponHitSounds.For(Origin("bomb_basic_special")), Is.Null, "bomb hits are explosions");
            Assert.That(WeaponHitSounds.For(Origin("whip_default", CombatHitKind.InkPart)), Is.EqualTo(WeaponHitSounds.Ink));
            Assert.That(WeaponHitSounds.For(Origin(null, CombatHitKind.Laser)), Is.EqualTo(WeaponHitSounds.Laser));
            Assert.That(WeaponHitSounds.For(Origin("rapier_default", CombatHitKind.Parry)), Is.EqualTo("sfx_hit_shield"));
            Assert.That(WeaponHitSounds.For(Origin(null)), Is.Null, "an unknown source stays silent");
        }

        [Test]
        public void EveryAttackHasItsSelectedSwing()
        {
            foreach (var form in new[] { "base_sword", "whip", "rapier", "dagger", "greatsword", "shuriken", "hammer", "shield", "launcher", "bomb", "unarmed" })
                Assert.That(SelectedAudioClips.Find("sfx_weapon_" + form + "_basic_0"), Is.Not.Null, form);
            foreach (var skill in new[] { "hammer_default", "shield_range", "launcher_range_special", "bomb_move_special", "greatsword_range", "base_sword_follow_up" })
                Assert.That(SelectedAudioClips.Find("sfx_weapon_" + skill), Is.Not.Null, skill);
        }
    }
}
