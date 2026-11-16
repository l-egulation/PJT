using System.Collections.Generic;
using Deviant.Combat.Contracts;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class ArenaProjectile
    {
        struct SweepHost : IEnemyProjectileSweepHost
        {
            readonly ArenaProjectile projectile;
            List<CombatActor> query, candidates;
            public SweepHost(ArenaProjectile projectile)
            { this.projectile = projectile; query = candidates = null; }
            public void ReadPosition(out float x, out float y)
            { Vector2 position = projectile.transform.position; x = position.x; y = position.y; }
            public float VelocityMagnitude => projectile.velocity.magnitude;
            public void ReadDirection(out float x, out float y)
            { Vector2 direction = projectile.velocity.normalized; x = direction.x; y = direction.y; }
            public float SolidTravel(float x, float y, float width, float height, float directionX, float directionY, float distance)
                => projectile.arena.SolidTravel(new Vector2(x, y), projectile.CollisionSize, new Vector2(directionX, directionY), distance);
            public void BeginCandidates() => candidates = new List<CombatActor>();
            public int QueryBox(float x, float y, float width, float height)
            {
                query = projectile.arena.TargetsInBox(new Vector2(x, y), projectile.CollisionSize);
                var player = projectile.arena.Player;
                if (!projectile.execution.Read().Reflected && player && !player.Dead && player.currentCast &&
                    player.currentCast.TryGetShieldParryBounds(out var shield))
                {
                    shield.Expand(new Vector3(projectile.CollisionSize.x, projectile.CollisionSize.y, 0));
                    if (shield.Contains(new Vector3(x, y, shield.center.z)) && !query.Contains(player)) query.Add(player);
                }
                return query.Count;
            }
            public bool ContainsCandidate(int i) => candidates.Contains(query[i]);
            public void AddCandidate(int i) => candidates.Add(query[i]);
            public void SortCandidates(float originX, float originY)
            {
                Vector2 previous = new Vector2(originX, originY);
                candidates.Sort((a, b) => Vector2.Distance(previous, a.Center).CompareTo(Vector2.Distance(previous, b.Center)));
            }
            public int CandidateCount => candidates.Count;
            public bool AcceptsTarget(int i)
            {
                var target = candidates[i];
                return projectile.execution.AcceptsTarget(target.Dead, target == projectile.source, target.isPlayer, target.isDecoy);
            }
            public bool Occluded(int i, float originX, float originY)
            {
                Vector2 previous = new Vector2(originX, originY);
                return projectile.arena.Occluded(previous, candidates[i].hurtCollider.ClosestPoint(previous));
            }
            public bool TryContact(int i, float originX, float originY)
            {
                var target = candidates[i];
                var contact = new Contact(projectile, target, new Vector2(originX, originY));
                return projectile.execution.TryContact(target.ActorId, target.isPlayer, ref contact, CombatArena.LoadRunInkCatalog().damageUnitScale);
            }
            public void SetPosition(float x, float y) => projectile.transform.position = new Vector2(x, y);
            public void Release() => projectile.execution.Release(projectile);
        }
    }
}
