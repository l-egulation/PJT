using Deviant.Combat.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        struct RangedLaunchHost : IRangedLaunchHost
        {
            readonly CombatArena arena;
            readonly CombatActor owner;
            readonly SkillRangedLaunchDefinition data;
            readonly float damage;
            readonly int direction;
            readonly WeaponState sourceWeapon;
            readonly CombatHitOrigin hitOrigin;
            Vector2 origin, muzzle, delta;
            RaycastHit2D[] hits;
            RaycastHit2D hit;
            ArenaSurface surface;
            PlayerProjectile shot;
            CombatPlayer shooter;
            public RangedLaunchHost(CombatArena arena, CombatActor owner, SkillRangedLaunchDefinition data,
                float damage, int direction, WeaponState sourceWeapon, CombatHitOrigin hitOrigin)
            {
                this.arena = arena; this.owner = owner; this.data = data; this.damage = damage;
                this.direction = direction; this.sourceWeapon = sourceWeapon;
                this.hitOrigin = hitOrigin;
                origin = muzzle = delta = default; hits = null; hit = default;
                surface = null; shot = null; shooter = null;
            }
            public void CaptureOrigin() => origin = owner.Position + new Vector2(owner.offset.x * direction, data.SpawnHeight);
            public float SpawnX => data.SpawnX;
            public float OwnerOffsetX => owner.offset.x;
            public float OwnerWidth => owner.size.x;
            public float ProjectileRadius => data.Projectile.Radius;
            public float SpawnGap => data.SpawnGap;
            public void CaptureMuzzle(float offsetX) => muzzle = owner.Position + new Vector2(offsetX * direction, data.SpawnHeight);
            public string ProjectileType => data.Projectile.Type;
            public bool IgnoresTerrain => data.Projectile.IgnoresTerrain;
            public void FireLaser() => arena.FireLaser(owner, data, damage, direction, origin, muzzle, hitOrigin);
            public void CaptureMuzzleDelta() => delta = muzzle - origin;
            public int QueryMuzzlePath()
            {
                hits = Physics2D.CircleCastAll(origin, data.Projectile.Radius, delta.normalized, delta.magnitude);
                return hits.Length;
            }
            public void SelectMuzzleHit(int index)
            { hit = hits[index]; surface = hit.collider.GetComponent<ArenaSurface>(); }
            public bool MuzzleSurfaceExists => surface;
            public bool MuzzleSurfaceIsOneWay => surface.oneWay;
            public float MuzzleHitDistance => hit.distance;
            public float BlastRadius => data.Projectile.BlastRadius;
            public void SpawnBlocked(float distance)
            {
                Vector2 at = origin + delta.normalized * Mathf.Clamp(distance, 0, delta.magnitude);
                shot = PlayerProjectile.Spawn(arena, owner, at, Vector2.right * direction * data.Speed, data.Projectile, damage, hitOrigin);
            }
            public void DetonateShot() => shot.Detonate();
            public void RecordBlocked() => arena.Record("shuriken muzzle blocked by solid");
            public void SpawnAtMuzzle()
                => shot = PlayerProjectile.Spawn(arena, owner, muzzle, Vector2.right * direction * data.Speed, data.Projectile, damage, hitOrigin);
            public bool HasSourceWeapon => sourceWeapon != null;
            public bool CapturePlayerOwner()
            {
                if (owner is CombatPlayer player) { shooter = player; return true; }
                return false;
            }
            public void AssignSourceWeapon(bool useCurrentPlayerWeapon)
                => shot.sourceWeapon = useCurrentPlayerWeapon ? shooter.Weapon : sourceWeapon;
            public void SweepShotToMuzzle() => shot.SweepTo(muzzle);
            public int AngleCount => data.Angles.Count;
            public float AngleAt(int index) => data.Angles[index];
            public void SpawnAtOrigin(float angle)
            {
                Vector2 velocity = new Vector2(direction * Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * data.Speed;
                shot = PlayerProjectile.Spawn(arena, owner, origin, velocity, data.Projectile, damage, hitOrigin);
            }
            public void RecordFired(int count)
                => arena.Record("ranged fired " + data.Projectile.Type + " count=" + count + " damage=" + damage.ToString("0.##"));
        }

        struct BombLaunchHost : IBombLaunchHost
        {
            readonly CombatArena arena;
            readonly CombatActor owner;
            readonly SkillBombLaunchDefinition data;
            readonly float damage;
            readonly int direction;
            readonly CombatHitOrigin hitOrigin;
            float radians;
            Vector2 origin;
            BombProjectile bomb;
            public BombProjectile Bomb => bomb;
            public BombLaunchHost(CombatArena arena, CombatActor owner, SkillBombLaunchDefinition data, float damage, int direction, CombatHitOrigin hitOrigin)
            {
                this.arena = arena; this.owner = owner; this.data = data; this.damage = damage; this.direction = direction;
                this.hitOrigin = hitOrigin;
                radians = 0; origin = default; bomb = null;
            }
            public void CaptureRadians() => radians = data.Angle * Mathf.Deg2Rad;
            public void CaptureOrigin() => origin = owner.Position + new Vector2(owner.offset.x * direction, data.SpawnHeight);
            public void Spawn() => bomb = BombProjectile.Spawn(arena, owner, origin,
                new Vector2(direction * Mathf.Cos(radians), Mathf.Sin(radians)) * data.Speed, data.Projectile, damage, hitOrigin);
            public void SetFragmentMultiplier(float multiplier) => bomb.fragmentMultiplier = multiplier;
            public float OwnerWidth => owner.size.x;
            public float ProjectileRadius => data.Projectile.Radius;
            public float SpawnGap => data.SpawnGap;
            public void SweepToMuzzle(float distance) => bomb.SweepTo(origin + Vector2.right * direction * distance);
        }
    }
}
