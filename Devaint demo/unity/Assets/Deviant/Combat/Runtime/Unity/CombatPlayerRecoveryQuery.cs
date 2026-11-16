using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        PlayerRecoveryQuery recoveryQuery;
        IActorRecoveryQuery RecoveryQuery => recoveryQuery ?? (recoveryQuery = new PlayerRecoveryQuery(this));
        bool RecoveryRecordingSafe() => PlayerRecoveryQuery.HazardFree(this, BodyFrame);
        sealed class PlayerRecoveryQuery : IActorRecoveryQuery
        {
            readonly CombatPlayer player;
            public PlayerRecoveryQuery(CombatPlayer player) { this.player = player; }
            public bool IsSafe(ActorPose pose)
            {
                var body = player.BodyFrame;
                var candidate = new ActorBodyFrame(pose.X, pose.Y, body.Width, body.Height, body.OffsetX, body.OffsetY, pose.Facing);
                if (ActorPlacementResolver.Blocked(candidate, pose.X, pose.Y, pose.Facing, player.SurfaceQuery)) return false;
                if (!ActorPlacementResolver.ProbeSafeFloor(candidate, 0, player.SurfaceQuery, out float height) || Math.Abs(height - pose.Y) > .02f) return false;
                return HazardFree(player, candidate);
            }
            internal static bool HazardFree(CombatPlayer player, ActorBodyFrame body)
            {
                var bounds = new Bounds(new Vector3(body.CenterX, body.CenterY, 0), new Vector3(body.Width, body.Height, 0));
                foreach (var pit in player.arena.pits)
                    if (pit && pit.enabled && pit.gameObject.activeInHierarchy && pit.bounds.Intersects(bounds)) return false;
                return true;
            }
        }
    }
}
