using System;
using Deviant.Combat.Execution;
using Deviant.Combat.Contracts;

namespace Deviant.Combat
{
    public partial class CombatPlayer
    {
        float wallJumpUntil, wallBufferUntil, wallCoyoteUntil, wallSlideStartedAt;
        float wallKickSpeed, wallRecoverUntil, wallRecoverSeconds, staminaFace, staminaUsed, axisChangedAt;
        int wallJumpDirection, bufferedWallSide, climbSide, wallCoyoteSide, staminaSide, lastAxis, kickHeldAxis;
        bool wallFlight;
        CombatCommandKey bufferedWallJump;
        public bool WallClimbing { get; private set; }
        public bool WallSliding { get; private set; }
        bool WallUpHeld => CurrentInput.ClimbAxis > 0;
        bool CanWallSlide => arena.Movement.WallSlideMaxSpeed > 0 &&
            (arena.Movement.WallSlideStartSpeed > 0 || arena.Movement.WallSlideAcceleration > 0);
        public bool WallJumpInFlight => wallFlight;
        public int WallJumpHeading => wallJumpDirection;
        public float WallJumpStartedAt { get; private set; } = -1;
        public float WallLedgeStartedAt { get; private set; } = float.NegativeInfinity;
        // Wall-top climb played as authored (data wallJump.ledgeClimbSeconds > 0): the hero holds still against the wall while
        // the presentation plays the climb anchored at the wall corner, then stands on the top. Hits, stuns and death drop it.
        float ledgeClimbUntil, ledgeFace, ledgeTop; int ledgeSide;
        // The clock advances after the motor. Keep pose ownership until the motor commits the stand,
        // including a render at/after the deadline while the physical body is still held below the edge.
        public bool WallLedgeClimbing => arena && ledgeClimbUntil > 0;
        public UnityEngine.Vector2 WallLedgeCorner => new UnityEngine.Vector2(ledgeFace, ledgeTop);
        public int WallLedgeSide => ledgeSide;
        bool WallMovementAvailable => Abilities.Has(MovementAbilities.WallCling) && !Dead && !InputBlocked && !arena.Controls.Current.SimulationPaused &&
            !Dashing && !currentCast && arena.Now >= knockUntil && arena.Now >= moveLockUntil &&
            arena.Now >= stunUntil && arena.Now >= rootUntil && arena.Now >= staggerUntil;
        bool TouchesWall(int side) => HasClimbableWall(side, .08f, out _, out _);
        bool HasClimbableWall(int side, float reach) => HasClimbableWall(side, reach, out _, out _);
        // A touched wall that has spent its stamina cannot be grabbed until landing or another wall.
        bool Grabbable(int side) => HasClimbableWall(side, .08f, out _, out float face) && !Exhausted(side, face);
        bool SameStaminaWall(int side, float face) => staminaSide == side && Math.Abs(staminaFace - face) <= .01f;
        bool Exhausted(int side, float face) => arena.Movement.WallStaminaDuration > 0 &&
            SameStaminaWall(side, face) && staminaUsed >= arena.Movement.WallStaminaDuration;
        void RefillWallStamina() { staminaSide = 0; staminaUsed = 0; }
        // What is left of the held wall's stamina (1 full, 0 spent), NaN while no wall's stamina is in use (on the ground,
        // or before the first wall). WallStaminaRing draws it over the hero's head.
        public float WallStaminaLeft => arena && arena.Movement.WallStaminaDuration > 0 && staminaSide != 0
            ? Math.Max(0, Math.Min(1, 1 - staminaUsed / arena.Movement.WallStaminaDuration)) : float.NaN;
        // Holding or kicking from another wall clears the previous record; the same wall keeps what is spent.
        void RecordStaminaWall(int side, float face)
        { if (!SameStaminaWall(side, face)) { staminaSide = side; staminaFace = face; staminaUsed = 0; } }
        bool HasClimbableWall(int side, float reach, out float wallTop, out float wallFace) =>
            HasClimbableWall(side, reach, out wallTop, out wallFace, out _);
        bool HasClimbableWall(int side, float reach, out float wallTop, out float wallFace, out float wallGap)
        {
            wallTop=wallFace=wallGap=0;
            if(side==0 || !ActorContactResolver.ResolveSolidAxis(BodyFrame,side*reach,true,SurfaceQuery).Blocked)return false;
            var body=BodyFrame;
            float edge=body.CenterX+side*body.Width/2;
            var surfaces=SurfaceQuery.OverlapBox(new ActorOverlapBox(edge+side*reach/2,
                body.CenterY,reach+.02f,body.Height*3+.02f));
            float nearest=float.PositiveInfinity,face=0;
            foreach(var surface in surfaces)
            {
                if(surface.OneWay || surface.MaxY<=body.CenterY-body.Height/2+.003f ||
                    surface.MinY>=body.CenterY+body.Height/2-.003f)continue;
                float x=side>0?surface.MinX:surface.MaxX;
                float gap=side*(x-edge);
                // A floor continuing underneath the player is not part of the side wall.
                if(gap<-.02f || gap>reach+.01f || gap>=nearest)continue;
                nearest=gap;face=x;
            }
            if(float.IsPositiveInfinity(nearest))return false;
            wallFace=face;wallGap=nearest;
            float bottom=body.CenterY,top=body.CenterY;
            bool seeded=false,changed;
            do
            {
                changed=false;
                foreach(var surface in surfaces)
                {
                    if(surface.OneWay || Math.Abs((side>0?surface.MinX:surface.MaxX)-face)>.01f)continue;
                    if(!seeded)
                    {
                        if(surface.MaxY<=body.CenterY-body.Height/2+.003f ||
                            surface.MinY>=body.CenterY+body.Height/2-.003f)continue;
                        bottom=surface.MinY;top=surface.MaxY;seeded=true;changed=true;
                    }
                    else if(surface.MinY<=top+.003f && surface.MaxY>=bottom-.003f)
                    {
                        float nextBottom=Math.Min(bottom,surface.MinY),nextTop=Math.Max(top,surface.MaxY);
                        changed|=nextBottom<bottom || nextTop>top;bottom=nextBottom;top=nextTop;
                    }
                }
            }while(changed && top-bottom<=body.Height+.003f);
            wallTop=top;
            return seeded && top-bottom>body.Height+.003f;
        }
        int ContactWall()
        {
            // Without Up nothing grabs a wall: no climb, no slide.
            if (!WallUpHeld || !WallMovementAvailable || arena.Now < wallJumpUntil) return 0;
            if (Grabbable(axis)) return axis;
            if (Grabbable(facing)) return facing;
            return WallUpHeld && Grabbable(-facing) ? -facing : 0;
        }
        // Sliding starts immediately; there is no stationary cling interval.
        float WallSlideSpeed(float elapsed)
        {
            var movement = arena.Movement;
            return Math.Min(movement.WallSlideMaxSpeed,
                movement.WallSlideStartSpeed + movement.WallSlideAcceleration * Math.Max(0, elapsed));
        }
        void SyncWallFacing(int direction)
        {
            if (WallMovementAvailable)
            {
                if (arena.Now < wallJumpUntil) direction = wallJumpDirection;
                else if (ContactWall() != 0) direction = ContactWall();
            }
            SyncFacing(direction);
        }
        // Jump grace never bypasses the live up-direction requirement.
        bool WallCoyoteActive => WallUpHeld && wallCoyoteSide != 0 && arena.Now <= wallCoyoteUntil && arena.Now >= wallJumpUntil &&
            WallMovementAvailable && !ProbeGround();
        bool TryWallJump()
        {
            if (!WallUpHeld) return false;
            int side = ContactWall();
            if (side == 0 && WallCoyoteActive) { StartWallJump(wallCoyoteSide); return true; }
            if (side != 0 && !ProbeGround())
            { StartWallJump(side); return true; }
            side = NearbyWall(out float gap);
            if (side == 0) { RecordWallJumpMiss(); return false; }
            arena.Record($"WALL JUMP NEAR gap={gap:0.00} side={side} axis={axis} axisFor={AxisHeldFor:0.00}s vy={VelocityY:0.0}");
            StartWallJump(side);
            return true;
        }
        // An air Space kicks from a wall within reach on either side, whichever key is held and
        // even while rising past it, before an air jump is spent. A held key picks the wall behind it.
        int NearbyWall(out float gap)
        {
            gap = 0;
            float reach = Math.Max(.08f, arena.Movement.WallJumpCheckDistance);
            if (!WallMovementAvailable || arena.Now < wallJumpUntil || ProbeGround()) return 0;
            int first = axis != 0 ? -axis : facing;
            for (int side = first, i = 0; i < 2; side = -side, i++)
                if (HasClimbableWall(side, reach, out _, out float face, out gap) && !Exhausted(side, face))
                { RecordStaminaWall(side, face); return side; }
            return 0;
        }
        float AxisHeldFor => axis != lastAxis ? 0 : arena.Now - axisChangedAt;
        // Tuning aid: an air Space that became an air jump although a wall was close.
        void RecordWallJumpMiss()
        {
            if (ProbeGround()) return;
            for (int side = -1; side <= 1; side += 2)
            {
                if (!HasClimbableWall(side, 1, out _, out float face, out float gap)) continue;
                string reason = !WallMovementAvailable ? "blocked" : arena.Now < wallJumpUntil ? "carry" :
                    Exhausted(side, face) ? "spent" : "far";
                arena.Record($"WALL JUMP MISS {reason} gap={gap:0.00} side={side} axis={axis} axisFor={AxisHeldFor:0.00}s vy={VelocityY:0.0}");
            }
        }
        void StartWallJump(int side)
        {
            ClearBufferedJump(CombatCommandReason.Replaced);
            CancelAction(CombatCommandReason.Priority);
            wallJumpDirection = -side;
            wallCoyoteSide = 0; jumps.ArmCut(false);
            var movement = arena.Movement;
            // Every kick leaves the wall the same way, whatever the key. Holding the wall and
            // pressing Space is enough: the key held toward it is ignored until released.
            bool release = movement.WallReleaseKickCarryDuration > 0;
            wallKickSpeed = release ? movement.WallAwayKickSpeed : movement.WallJumpHorizontalSpeed;
            kickHeldAxis = release && CurrentInput.Axis == side ? side : 0;
            wallJumpUntil = arena.Now + (release ? movement.WallReleaseKickCarryDuration : movement.WallJumpCarryDuration);
            wallRecoverSeconds = release ? movement.WallKickControlRecoveryDuration : 0;
            wallRecoverUntil = wallJumpUntil + wallRecoverSeconds;
            WallJumpStartedAt = arena.Now;
            arena.Audio?.EmitSelected("sfx_player_wall_kick", Position);
            wallFlight = true;
            VelocityY = arena.Movement.FirstJumpVelocity;
            grounded = false; tick.HadGround = false; WallClimbing = WallSliding = false;
            SyncFacing(wallJumpDirection);
        }
        bool TryBufferWallJump(CombatCommandKey command)
        {
            if (!WallUpHeld || !WallMovementAvailable || ProbeGround() || arena.Movement.WallJumpBufferDuration <= 0) return false;
            int side = arena.Now < wallJumpUntil ? wallJumpDirection : axis;
            if (side == 0 || (!wallFlight && CurrentInput.ClimbAxis == 0 && VelocityY > 0)) return false;
            float speed = arena.Now < wallJumpUntil ? wallKickSpeed : arena.Movement.RunSpeed;
            // Reserve only when a solid side wall is within the input window.
            // A free-air Space remains an ordinary air jump.
            if (speed <= 0 || !ActorContactResolver.ResolveSolidAxis(BodyFrame,
                side * speed * arena.Movement.WallJumpBufferDuration, true, SurfaceQuery).Blocked) return false;
            if(!HasClimbableWall(side,speed*arena.Movement.WallJumpBufferDuration))return false;
            ClearBufferedJump(CombatCommandReason.Replaced);
            jumps.ClearBuffer();
            bufferedWallJump = command; bufferedWallSide = side;
            wallBufferUntil = arena.Now + arena.Movement.WallJumpBufferDuration;
            return true;
        }
        void ClearWallJumpBuffer(CombatCommandReason reason)
        {
            var key = bufferedWallJump; bufferedWallJump = default;
            if (arena) arena.RecordCommand(key, CombatCommandState.Rejected, reason);
        }
        // Room to stand on the wall top where the climb ends; otherwise the old hop is used.
        bool LedgeStandClear(int side, float face, float top, float forward)
        {
            var center = new UnityEngine.Vector2(face + side * (forward + offset.x), top + offset.y + .03f);
            return !arena.SolidOverlap(center, new UnityEngine.Vector2(size.x - .02f, size.y - .04f));
        }
        void FinishLedgeClimb()
        {
            ledgeClimbUntil = 0;
            Position = new UnityEngine.Vector2(ledgeFace + ledgeSide * arena.Movement.WallLedgeClimbForward, ledgeTop);
            velocity = UnityEngine.Vector2.zero;
        }
        void CancelWallMovement(CombatCommandReason reason)
        {
            ClearWallJumpBuffer(reason);
            wallJumpUntil = 0; wallFlight = false; WallClimbing = WallSliding = false; wallCoyoteSide = 0;
            wallRecoverUntil = 0;
            WallLedgeStartedAt = float.NegativeInfinity;
            ledgeClimbUntil = 0;

            if (reason == CombatCommandReason.Reset) { RefillWallStamina(); kickHeldAxis = 0; }
        }
        void ResolveWallArrival(int side)
        {
            if (!wallFlight && !bufferedWallJump.IsValid) return;
            if (side == 0 || !Grabbable(side) || ProbeGround()) return;
            if (!WallUpHeld) { ClearWallJumpBuffer(CombatCommandReason.Interrupted); wallFlight = false; return; }
            wallJumpUntil = 0; wallRecoverUntil = 0; wallFlight = false;
            bool reserved = WallUpHeld && bufferedWallJump.IsValid && bufferedWallSide == side && arena.Now <= wallBufferUntil;
            if (!WallUpHeld) ClearWallJumpBuffer(CombatCommandReason.Interrupted);
            if (!WallUpHeld && !CanWallSlide) return;
            HasClimbableWall(side, .08f, out _, out float arrivalFace);
            RecordStaminaWall(side, arrivalFace);
            WallClimbing = true; WallSliding = !WallUpHeld;
            climbSide = side; wallCoyoteSide = 0; wallSlideStartedAt = arena.Now;
            velocity = new UnityEngine.Vector2(0, WallUpHeld ? arena.Movement.RunSpeed : -WallSlideSpeed(0));
            SyncFacing(side);
            if (reserved)
            {
                var key = bufferedWallJump; bufferedWallJump = default;
                StartWallJump(side);
                var execution = Guid.NewGuid();
                arena.RecordCommand(key, CombatCommandState.Started, executionId: execution);
                arena.RecordCommand(key, CombatCommandState.Completed, executionId: execution);
            }
        }
        void WallPhysicsStep(float step, float horizontal, bool freezeVertical)
        {
            bool wasClimbing = WallClimbing, wasSliding = WallSliding;
            WallClimbing = WallSliding = false;
            if (!WallUpHeld) ClearWallJumpBuffer(CombatCommandReason.Interrupted);
            if (CurrentInput.Axis != kickHeldAxis) kickHeldAxis = 0;
            if (axis != lastAxis) { lastAxis = axis; axisChangedAt = arena.Now; }
            if (ledgeClimbUntil > 0 && WallMovementAvailable)
            {
                if (arena.Now < ledgeClimbUntil) { velocity = UnityEngine.Vector2.zero; return; }
                FinishLedgeClimb();
                PhysicsStep(step, 0, false);
                return;
            }
            if (!WallMovementAvailable)
            {
                CancelWallMovement(CombatCommandReason.Interrupted);
                PhysicsStep(step, horizontal, freezeVertical);
                return;
            }
            bool carrying = arena.Now < wallJumpUntil;
            int side = carrying ? wallJumpDirection : ContactWall();
            // Finish the push as soon as the opposite wall is reached.
            if (carrying && TouchesWall(side))
            {
                ResolveWallArrival(side);
                carrying = arena.Now < wallJumpUntil;
                side = carrying ? wallJumpDirection : ContactWall();
            }
            if (carrying)
                horizontal = wallJumpDirection * wallKickSpeed *
                    Math.Min(1, Math.Max(0, (wallJumpUntil - arena.Now) / step));
            else if (side != 0 && (!ProbeGround() || WallUpHeld) &&
                (WallUpHeld || CanWallSlide && (VelocityY <= 0 || !JumpApexArmed && (wasClimbing || wallFlight))))
            {
                var movement = arena.Movement;
                var body = BodyFrame;
                float feet = body.CenterY - body.Height / 2;
                HasClimbableWall(side, .08f, out float wallTop, out float wallFace);
                // Explicit up input climbs over the ledge; ordinary jumping past it is untouched.
                if (WallUpHeld && movement.WallLedgeHopHeight > 0 &&
                    wallTop - feet <= movement.WallLedgeHopHeight)
                {
                    WallLedgeStartedAt = arena.Now;
                    wallFlight = false; wallCoyoteSide = 0;
                    if (movement.WallLedgeClimbSeconds > 0 && LedgeStandClear(side, wallFace, wallTop, movement.WallLedgeClimbForward))
                    {
                        ledgeClimbUntil = arena.Now + movement.WallLedgeClimbSeconds;
                        ledgeFace = wallFace; ledgeTop = wallTop; ledgeSide = side;
                        velocity = UnityEngine.Vector2.zero; SyncFacing(side);
                        return;
                    }
                    VelocityY = (float)Math.Sqrt(2 * movement.RiseGravity * (wallTop - feet + .15f));
                    PhysicsStep(step, side * movement.RunSpeed, false);
                    return;
                }
                // Each wall has its own stamina, spent while it is held.
                if (movement.WallStaminaDuration > 0)
                {
                    RecordStaminaWall(side, wallFace);
                    staminaUsed += step;
                    if (staminaUsed >= movement.WallStaminaDuration)
                    {
                        // Spent: drop off, leaving only the coyote window to kick from this wall.
                        wallFlight = false; wallRecoverUntil = 0;
                        if (movement.WallCoyoteDuration > 0)
                        { wallCoyoteSide = side; wallCoyoteUntil = arena.Now + movement.WallCoyoteDuration; }
                        PhysicsStep(step, horizontal, freezeVertical);
                        return;
                    }
                }
                if (!WallUpHeld && (!wasSliding || climbSide != side)) wallSlideStartedAt = arena.Now;
                WallClimbing = true; WallSliding = !WallUpHeld;
                wallFlight = false; climbSide = side; wallCoyoteSide = 0; wallRecoverUntil = 0;
                float vertical = WallUpHeld ? movement.RunSpeed : -WallSlideSpeed(arena.Now - wallSlideStartedAt);
                PhysicsStep(step, side * arena.Movement.RunSpeed, false, vertical);
                ResolveWallArrival(side);
                return;
            }
            else if (arena.Now < wallRecoverUntil && wallRecoverSeconds > 0)
            {
                // Blend from the kick speed back to ordinary air control.
                float control = Math.Min(1, Math.Max(0, 1 - (wallRecoverUntil - arena.Now) / wallRecoverSeconds));
                horizontal = wallJumpDirection * wallKickSpeed * (1 - control) + horizontal * control;
            }
            PhysicsStep(step, horizontal, freezeVertical);
            if (grounded)
            {
                wallFlight = false; wallJumpUntil = 0; wallCoyoteSide = 0;
                wallRecoverUntil = 0; RefillWallStamina(); kickHeldAxis = 0;
                ClearWallJumpBuffer(CombatCommandReason.NoPlatform);
            }
            else
            {
                // Leaving sideways grants only jump grace; up is still required to use it.
                if (wasClimbing && !carrying && axis == -climbSide && arena.Movement.WallCoyoteDuration > 0 &&
                    HasClimbableWall(climbSide, arena.Movement.RunSpeed * arena.Movement.WallCoyoteDuration + .08f))
                { wallCoyoteSide = climbSide; wallCoyoteUntil = arena.Now + arena.Movement.WallCoyoteDuration; }
                ResolveWallArrival(horizontal > 0 ? 1 : horizontal < 0 ? -1 : 0);
            }
        }
    }
}
