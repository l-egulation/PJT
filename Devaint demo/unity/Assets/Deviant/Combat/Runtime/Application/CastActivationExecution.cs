using Deviant.Combat.Contracts;
using Deviant.Definitions;

namespace Deviant.Combat.Execution
{
    // Stateless ordering over the cast's immutable start input. Constrained ref
    // calls keep current Native facts and callback partial results with the host;
    // the existing lifecycle remains the only activation/cancellation authority.
    public static class CastActivationExecution
    {
        public static void Activate<T>(SkillExecutionDefinition definition, ref T host) where T : struct, ICastActivationHost
        {
            if (definition.Vertical == "grounded_required" && !host.ProbeGround()) { host.Fail(); return; }
            if (definition.Movement != null && definition.Movement.Type == "target_teleport_strike")
            {
                // No target in reach: the skill still activates in place and whiffs (no teleport).
                if (definition.Movement.TargetRadius > 0) host.SelectTeleportTargetInCircle(definition.Movement.TargetRadius);
                else host.SelectTeleportTargetInBox();
            }
            // No nearest target: the hit sweeps its box and takes the first enemy it meets, or whiffs.
            if (definition.Hits.Count > 0 && definition.Hits[0].Targets == "nearest") host.SelectFirstHitTarget();
            var effect = definition.Effect;
            host.SaveOwnerPose();
            if (effect != null && (effect.Type == "delayed_world_sword" || effect.Type == "decoy"))
            {
                host.CaptureInstallCenter(effect.X, effect.Y);
                if (effect.Type == "decoy")
                {
                    var floor = host.FindInstallFloor(3);
                    if (!floor.HasValue) { host.Fail(); return; }
                    host.SetInstallHeight(floor.Value + (definition.Authored != null ? effect.Box.Y : .75f));
                }
                float width = definition.Authored != null && effect.Type == "decoy" ? effect.Box.Width : effect.Type == "decoy" ? .8f : 2.5f;
                float height = definition.Authored != null && effect.Type == "decoy" ? effect.Box.Height : effect.Type == "decoy" ? 1.5f : 3;
                if (host.InstallationOverlapsSolid(width, height, .01f) || host.InstallationOccluded) { host.Fail(); return; }
            }
            if (!host.CommitResources()) { host.Fail(); return; }
            host.ClaimActivation();
            if (definition.Kind == "missile") host.LaunchMissile();
            if (definition.Kind == "bomb") host.ReleaseDueBombs();
            if (definition.Kind == "ranged")
            {
                host.FireRanged();
                // Shotgun recoil hops backward from the ground as well as in the air.
                if (definition.Ranged.RecoilDuration > 0)
                {
                    float hop = host.BeginRangedRecoil(definition.Ranged.RecoilDuration, definition.Ranged.RecoilHopHeight);
                    if (definition.Ranged.RecoilHopHeight > 0) host.SetOwnerVerticalVelocity(hop);
                }
            }
            // An entered activation is not re-admitted after Native callbacks.
            // Self/area buffs without attack hits do not break stealth here.
            if (effect != null)
            {
                if (definition.HasProjectileWave) host.ActivateSkillWave();
                if (effect.Type == "stealth") host.SetStealthUntil(host.Now + effect.Duration);
                if (definition.HasOwnerGuard)
                {
                    host.SetShield(effect.Hp);
                    host.SetGuardUntil(Max(host.GuardUntil, host.Now + effect.Duration));
                    host.SetGuardReduction(effect.Reduction);
                }
                if (effect.Type == "smoke") { host.ReplaceSmoke(); host.ActivateSmoke(host.Activation); }
                if (effect.Type == "decoy") host.SpawnDecoy(definition.Authored != null ? effect.Box.Y : .75f);
            }
            host.RecordActivation();
        }
        static float Max(float a, float b) => a > b ? a : b;
    }
}
