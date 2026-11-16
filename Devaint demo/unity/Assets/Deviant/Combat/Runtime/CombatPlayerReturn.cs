using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        readonly IActorReturnSchedule returnSchedule = new ActorReturnSchedule();
        // Scene references only; captured timing/origin/weapon facts live in the schedule.
        readonly Dictionary<Guid, CombatActor> returnTargets = new Dictionary<Guid, CombatActor>();
        public IReadOnlyList<ActorReturnReservation> ReturnReservations => returnSchedule.Read();
        public bool ShurikenReturnPending(WeaponState weapon) => returnSchedule.Contains(weapon?.InstanceId ?? Guid.Empty);
        public float ShurikenReturnRemaining(WeaponState weapon)
        {
            var id = weapon?.InstanceId ?? Guid.Empty;
            return returnSchedule.Contains(id) ? returnSchedule.Remaining(id, arena.Now) : 0;
        }
        void ClearShurikenReturns() { returnSchedule.Clear(); returnTargets.Clear(); ShurikenArrivedAt=-10; MissileEmergedAt=-10; }
        // When the player last appeared behind a target; the body plays its arrival pose from here.
        public float ShurikenArrivedAt { get; private set; } = -10;
        // When the player last came out of the bomb missile's blast; the body plays its emerge pose from here (2026-11-10).
        public float MissileEmergedAt { get; internal set; } = -10;

        // Root pose accounts for asymmetric body offsets in both facing directions.
        Vector2 AdjacentTo(CombatActor target,int side,int desiredFacing,float gap)
        {
            var pose = ActorReturnPlacement.AdjacentTo(BodyFrame, target.BodyFrame, side, desiredFacing, gap);
            return new Vector2(pose.X, pose.Y);
        }
        bool CanReachPose(Vector2 at,int desiredFacing) => ActorReturnPlacement.CanReach(BodyFrame, new ActorPose(at.x, at.y, desiredFacing), SurfaceQuery);
        void SetReturnPose(Vector2 at,int desiredFacing)
        {
            facing=desiredFacing; Position=at; SyncFacing(desiredFacing);
            Physics2D.SyncTransforms(); grounded=ProbeGround(); tick.HadGround=grounded;
        }
        public bool TryShurikenVisit(CombatActor target,WeaponState source,float returnDelay,float behindGap)
        {
            if(Dead || !target || target.Dead || source==null || ShurikenReturnPending(source)) return false;
            int targetFacing=target.facing;
            Vector2 at=AdjacentTo(target,-targetFacing,targetFacing,behindGap);
            if(!CanReachPose(at,targetFacing)) { arena.Record("shuriken behind blocked: hit damage kept"); return false; }
            var pending = new ActorReturnReservation(Guid.NewGuid(), source.InstanceId, target.ActorId, new ActorPose(Position.x, Position.y, facing), arena.Now, returnDelay, behindGap);
            Vector2 left=Position; int leftFacing=facing;
            SetReturnPose(at,targetFacing);
            ShurikenArrivedAt=arena.Now;
            ShurikenSkillFx.Step(arena,left,leftFacing,Position,targetFacing);
            arena.Audio?.EmitSkillEvent("shuriken_move_special", "teleport", Position);
            if (!returnSchedule.TryAdd(pending)) throw new InvalidOperationException("The source return reservation changed during pose application.");
            returnTargets.Add(pending.Id, target);
            arena.Record("shuriken behind teleport; return in "+returnDelay+"s origin="+new Vector2(pending.Origin.X, pending.Origin.Y));
            return true;
        }
        bool TryNearOrigin(ActorReturnReservation pending,out Vector2 destination)
        {
            bool found = ActorReturnPlacement.TryNearOrigin(BodyFrame, pending.Origin, SurfaceQuery, out var pose);
            destination = new Vector2(pose.X, pose.Y); return found;
        }
        void ResolveShurikenReturns()
        {
            foreach(var snapshot in returnSchedule.Read())
            {
                if (!returnSchedule.TryTakeDue(snapshot.Id, snapshot.WeaponId, arena.Now, out var pending)) continue;
                returnTargets.TryGetValue(pending.Id, out var target); returnTargets.Remove(pending.Id);
                Vector2 at; int desiredFacing=pending.Origin.Facing;
                bool found=TryNearOrigin(pending,out at);
                if(!found && target && target.ActorId == pending.TargetActorId && !target.Dead)
                {
                    desiredFacing=-target.facing;
                    at=AdjacentTo(target,target.facing,desiredFacing,pending.Gap);
                    found=CanReachPose(at,desiredFacing);
                }
                if(!found) { arena.Record("shuriken return unavailable: keep current position"); continue; }
                CancelAction(); ClearInputs(); dash.ClearCarry();
                ClearBufferedJump(CombatCommandReason.Interrupted);
                Vector2 left=Position; int leftFacing=facing;
                SetReturnPose(at,desiredFacing);
                ShurikenSkillFx.Step(arena,left,leftFacing,Position,desiredFacing);
                arena.Audio?.EmitSkillEvent("shuriken_move_special", "return", Position);
                arena.Record("shuriken automatic return="+at);
            }
        }
    }
}
