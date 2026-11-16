using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        struct NearestWorldTargetHost : IWorldNearestTargetHost
        {
            readonly CombatArena arena;
            readonly CombatActor owner;
            public CombatActor Target, Best;
            public NearestWorldTargetHost(CombatArena arena, CombatActor owner)
            { this.arena = arena; this.owner = owner; Target = Best = null; }
            public bool Exists => Target;
            public bool IsPlayer => Target.isPlayer;
            public bool IsDecoy => Target.isDecoy;
            public bool Dead => Target.Dead;
            public WorldHitPoint OwnerCenter { get { var center = owner.Center; return new WorldHitPoint(center.x, center.y); } }
            public WorldHitPoint TargetCenter { get { var center = Target.Center; return new WorldHitPoint(center.x, center.y); } }
            public bool Occluded() => arena.Occluded(owner.Center, Target.hurtCollider.ClosestPoint(owner.Center));
            public bool BestExists => Best;
            public int ActorId => Target.ActorId;
            public int BestActorId => Best.ActorId;
            public float ApproximationEpsilon => Mathf.Epsilon;
        }

        struct WorldHitVisibilityHost : IWorldHitVisibilityHost
        {
            readonly CombatArena arena;
            public WorldHitVisibilityHost(CombatArena arena) { this.arena = arena; }
            public bool Occluded(WorldHitPoint origin, WorldHitPoint point)
                => arena.Occluded(new Vector2(origin.X, origin.Y), new Vector2(point.X, point.Y));
        }
    }
}
