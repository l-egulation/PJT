using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        // Unity entity calls retain their original phase-entry order. The
        // simulation's existing reentry guard serializes this scratch buffer.
        sealed class ArenaTickWorld : ICombatTickWorld
        {
            readonly CombatArena arena;
            readonly CombatTickSnapshot snapshot = new CombatTickSnapshot();
            public ArenaTickWorld(CombatArena arena) { this.arena = arena; }
            public void ApplyInput(CombatInput input)
            { arena.Player.ReplaceInput(input); }
            public void Execute(CombatTickPhase phase, float dt)
            {
                try
                {
                    switch (phase)
                    {
                        case CombatTickPhase.Player:
                            arena.BeforePlayerStep?.Invoke(dt);
                            arena.Player.Step(dt);
                            arena.PlayerStepCompleted?.Invoke(dt);
                            break;
                        case CombatTickPhase.Actions:
                            snapshot.Capture(arena.casts);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var cast = (CombatCast)snapshot[i]; if (cast) cast.Step(dt); }
                            break;
                        case CombatTickPhase.ActionPush:
                            snapshot.Capture(arena.casts);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var cast = (CombatCast)snapshot[i]; if (cast) cast.PushTargets(dt); }
                            break;
                        case CombatTickPhase.Enemies:
                            foreach (var enemy in arena.enemies) if (enemy)
                            { enemy.DelayRunInkMotion(dt * arena.ReadRunInkMotionSlow(enemy)); enemy.Step(dt); } break;
                        case CombatTickPhase.EnemyProjectiles:
                            snapshot.Capture(arena.projectiles);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var projectile = (ArenaProjectile)snapshot[i]; if (projectile) projectile.Step(dt); }
                            break;
                        case CombatTickPhase.Bombs:
                            snapshot.Capture(arena.bombs);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var bomb = (BombProjectile)snapshot[i]; if (bomb) bomb.Step(dt); }
                            break;
                        case CombatTickPhase.PlayerProjectiles:
                            snapshot.Capture(arena.playerShots);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var shot = (PlayerProjectile)snapshot[i]; if (shot) shot.Step(dt); }
                            break;
                        case CombatTickPhase.Lasers:
                            snapshot.Capture(arena.lasers);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var laser = (LaserVisual)snapshot[i]; if (laser) laser.Step(); }
                            break;
                        case CombatTickPhase.Bursts:
                            snapshot.Capture(arena.bombBursts);
                            for (int i = 0; i < snapshot.Count; i++)
                            { var burst = (BombBurst)snapshot[i]; if (burst) burst.Step(); }
                            break;
                        case CombatTickPhase.Decoys:
                            for (int i = arena.decoys.Count - 1; i >= 0; i--)
                                if (!arena.decoys[i] || arena.decoys[i].Dead || arena.Now >= arena.decoys[i].expiredAt)
                                { if (arena.decoys[i]) { arena.decoys[i].GetComponent<ShieldDecoyRuntime>()?.Evaluate(arena.Now >= arena.decoys[i].expiredAt); UnityEngine.Object.Destroy(arena.decoys[i].gameObject); } arena.decoys.RemoveAt(i); }
                            break;
                        case CombatTickPhase.ExpiredFeedback:
                            arena.StepInkParts();
                            arena.flashes.RemoveAll(f => arena.Now >= f.end);
                            arena.SimulationStepCompleted?.Invoke(dt); break;
                        default: throw new ArgumentOutOfRangeException(nameof(phase));
                    }
                }
                finally { snapshot.Clear(); }
            }
            public void ResetState(SimulationResetMode mode) => arena.ResetActorsAndEffects(mode);
        }
    }
}
