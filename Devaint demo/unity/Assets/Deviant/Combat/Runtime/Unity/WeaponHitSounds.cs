using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    // Which selected cue a player hit makes. The weapon (and a few skills) decides the cue, never the monster
    // that was hit (2026-11-13 user decision). Since 2026-11-16 every sfx_hit_* cue points at the same common
    // clip (sfx_hit_common_01_v01) in SelectedAudioClips; the per-weapon ids remain so per-weapon/skill
    // volume settings and the 60 ms repeat rule keep working.
    public static class WeaponHitSounds
    {
        public const string Ink = "sfx_combat_hit_ink";
        public const string Laser = "sfx_hit_launcher_laser";
        // Longest ids first so "greatsword" is not read as a "sword" prefix.
        static readonly string[] Weapons = { "base_sword", "greatsword", "launcher", "shuriken", "rapier", "dagger", "hammer", "shield", "whip", "bomb", "unarmed" };

        public static string WeaponOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var weapon in Weapons)
                if (id == weapon || id.StartsWith(weapon + "_")) return weapon;
            return null;
        }

        // Null means no hit sound: explosions (bomb, grenade, explosive shuriken, missile) already play their blast.
        public static string For(CombatHitOrigin origin)
        {
            switch (origin.Kind)
            {
                case CombatHitKind.Blast: case CombatHitKind.Fragment: case CombatHitKind.Missile: return null;
                case CombatHitKind.InkPart: return Ink;
                case CombatHitKind.Laser: return Laser;
            }
            if (origin.ActionId == "launcher_range_special") return Laser;
            string weapon = origin.Kind == CombatHitKind.Parry ? "shield" : WeaponOf(origin.ActionId) ?? WeaponOf(origin.Stamp.FormId);
            return ForWeapon(weapon);
        }

        public static string ForWeapon(string weapon)
        {
            switch (weapon)
            {
                case null: case "bomb": return null;
                case "base_sword": return "sfx_hit_sword";
                case "launcher": return "sfx_hit_launcher_ball";
                default: return "sfx_hit_" + weapon;
            }
        }
    }
}
