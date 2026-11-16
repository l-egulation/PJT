using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    // Transform is output, never a source of the actor's 2D motion facts.
    static class ActorKinematicsBinding
    {
        public static void SetPosition(IActorKinematics owner, Transform target, float x, float y, float depth)
        {
            owner.WritePosition(x, y);
            var at = target.position;
            if (at.x != x || at.y != y || at.z != depth) target.position = new Vector3(x, y, depth);
        }
        public static void SyncPosition(IActorKinematics owner, Transform target)
        {
            // Depth belongs only to the native view. Even skipped motor axes
            // must restore owner x/y before Physics2D observes the output.
            var at = target.position;
            if (at.x != owner.X || at.y != owner.Y) target.position = new Vector3(owner.X, owner.Y, at.z);
        }
        public static void SetVisualPosition(IActorKinematics owner, Transform target, Transform visual, float x, float y)
        {
            var local = new Vector3(x, y, 0);
            if (visual == target)
            {
                // The legacy root-renderer alias moves the actor in local space.
                // Commit the corresponding world pose before the native write.
                var parent = target.parent;
                var world = parent ? parent.TransformPoint(local) : local;
                owner.WritePosition(world.x, world.y);
            }
            var at = visual.localPosition;
            if (at.x != local.x || at.y != local.y || at.z != local.z) visual.localPosition = local;
        }
    }
}
