using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Stateless orchestration: the actor retains its live pose and motion facts.
    public static class ActorMotorExecution
    {
        public static ActorMotionProposal Step<T>(float step, float desiredX, bool freezeVertical, ref T host) where T : struct, IActorMotorHost
        {
            float now = host.Now;
            host.ExpireShield(now);
            host.RefreshPlatforms(now);
            host.PruneUnusedPlatforms();
            host.Grounded = ActorContactResolver.ProbeGround(host.BodyFrame, host.VelocityY, host.SurfaceQuery);
            var motion = ActorMotionResolver.Resolve(host.ReadMotionInput(now, step, desiredX, freezeVertical));
            host.SetVelocity(motion.VelocityX, motion.VelocityY);
            host.WallHit = false;
            MoveAxis(motion.DeltaX, true, ref host);
            MoveAxis(motion.DeltaY, false, ref host);
            host.SyncTransforms();
            return motion;
        }
        static void MoveAxis<T>(float distance, bool horizontal, ref T host) where T : struct, IActorMotorHost
        {
            // Both body and query are read again for Y, after X has applied its
            // displacement and any synchronous callbacks have changed the actor.
            var motion = ActorContactResolver.ResolveAxis(host.BodyFrame, distance, horizontal, host.SurfaceQuery);
            if (motion.Skipped) return;
            host.MovePosition(motion.DeltaX, motion.DeltaY);
            if (motion.Blocked)
            {
                if (horizontal) host.WallHit = true;
                else { host.VelocityY = 0; if (distance < 0) host.Grounded = true; }
            }
        }
    }
}
