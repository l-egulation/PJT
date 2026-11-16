using UnityEngine;

namespace Deviant.Combat
{
    // Run-scoped player modifiers set by a scene layer (chapter 1 run augments, docs/design/world/stage-gimmicks.md).
    // Neutral values change nothing; ink parts keep their own pipeline and are not read or written here.
    public partial class CombatArena
    {
        // Every hit the player deals, after the ink damage step: a flat scale, then a chance of double damage.
        public float PlayerDamageScale = 1, PlayerCritChance;
        // Every hit the player takes, before invulnerability, parry and ink protection.
        public float PlayerIncomingScale = 1;
        // Player hit boxes (range and radius) and the push the player's hits give enemies.
        public float PlayerReachScale = 1, PlayerPushScale = 1;
        // Distance the player is knocked back by a monster hit.
        public float PlayerKnockbackTakenScale = 1;
        // Seconds of invulnerability after an accepted hit on the player (0: none, the base rule).
        public float PlayerHitProtectSeconds;
        public float PlayerHitProtectUntil { get; internal set; } = -10;
        // Damage returned to a monster whose hit the player accepted.
        public float PlayerThornsDamage;
        public bool LastHitWasCritical { get; internal set; }

        public float ScalePlayerDamage(float damage)
        {
            LastHitWasCritical = PlayerCritChance > 0 && Random.value < PlayerCritChance;
            return damage * PlayerDamageScale * (LastHitWasCritical ? 2 : 1);
        }
    }
}
