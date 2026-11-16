using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        struct LaserHitHost : ILaserHitHost
        {
            readonly CombatArena arena;
            readonly CombatActor owner;
            readonly SkillRangedLaunchDefinition data;
            readonly float damage;
            readonly int direction;
            readonly Vector2 origin, muzzle;
            readonly CombatHitOrigin hitOrigin;
            RaycastHit2D[] casts;
            RaycastHit2D current;
            ArenaSurface surface;
            HashSet<int> seen;
            List<Vector2> hitPoints;
            List<CombatActor>.Enumerator targets;
            CombatActor target;
            public LaserHitHost(CombatArena arena, CombatActor owner, SkillRangedLaunchDefinition data,
                float damage, int direction, Vector2 origin, Vector2 muzzle, CombatHitOrigin hitOrigin)
            {
                this.arena = arena; this.owner = owner; this.data = data; this.damage = damage;
                this.direction = direction; this.origin = origin; this.muzzle = muzzle;
                this.hitOrigin = hitOrigin;
                casts = null; current = default; surface = null; seen = null; hitPoints = null; targets = default; target = null;
            }
            public float MuzzleGap => Vector2.Distance(origin, muzzle);
            public bool IgnoresTerrain => data.Projectile.IgnoresTerrain;
            public float ProjectileDistance
            {
                get
                {
                    if (!data.Projectile.UnlimitedRange) return data.Projectile.Distance*arena.MvpGeometry(hitOrigin,false);
                    casts = Physics2D.BoxCastAll(origin, new Vector2(.001f, data.Thickness*arena.MvpGeometry(hitOrigin,true)),
                        0, Vector2.right * direction, Mathf.Infinity);
                    float extent = MuzzleGap + 1;
                    foreach (var hit in casts) extent = Mathf.Max(extent, hit.distance + hit.collider.bounds.size.x + 1);
                    var camera = Camera.main;
                    if (camera && camera.orthographic)
                        extent = Mathf.Max(extent, Mathf.Abs(camera.transform.position.x - origin.x) + camera.orthographicSize * camera.aspect);
                    return extent - MuzzleGap;
                }
            }
            public int QueryCast(float length)
            {
                Vector2 size = new Vector2(.001f, data.Thickness*arena.MvpGeometry(hitOrigin,true));
                if (!data.Projectile.UnlimitedRange) casts = Physics2D.BoxCastAll(origin, size, 0, Vector2.right * direction, length);
                return casts.Length;
            }
            public bool FindSurface(int index)
            {
                current = casts[index]; surface = current.collider.GetComponent<ArenaSurface>();
                return surface is ArenaSurface;
            }
            public bool SurfaceIsOneWay => surface.oneWay;
            public float SurfaceDistance => current.distance;
            public void BeginTargets() { seen = new HashSet<int>(); hitPoints = new List<Vector2>(); }
            public void QueryTargets(float length, float width)
            {
                Vector2 center = origin + Vector2.right * direction * length / 2;
                var candidates = arena.TargetsInBox(center, new Vector2(width, data.Thickness*arena.MvpGeometry(hitOrigin,true)));
                var scene = arena; var from = origin; bool ignoresTerrain = IgnoresTerrain;
                if (owner == arena.Player) arena.PrepareSketchHit(hitOrigin, candidates.Where(t => t && (ignoresTerrain || !scene.Occluded(from, t.hurtCollider.ClosestPoint(from)))));
                targets = candidates.GetEnumerator();
            }
            public bool NextTarget()
            {
                if (!targets.MoveNext()) return false;
                target = targets.Current; return true;
            }
            public bool TargetIsOwner => target == owner;
            public bool TargetIsPlayer => target.isPlayer;
            public bool TargetIsDecoy => target.isDecoy;
            public bool TargetDead => target.Dead;
            public bool TryClaimTarget() => seen.Add(target.ActorId);
            public bool TargetOccluded() => arena.Occluded(origin, target.hurtCollider.ClosestPoint(origin));
            public void ReceiveBasic()
            {
                Vector2 point = target.hurtCollider.ClosestPoint(origin);
                target.Receive(damage, owner, direction, true, null, origin: hitOrigin,
                    contact: CombatHitContacts.At(point, Vector2.right * direction, HitAnchorKind.BeamRepresentative));
                hitPoints?.Add(new Vector2(point.x, muzzle.y)); // spark on the beam where it met this target
            }
            public void ShowVisual(float visible)
            {
                var laser = new GameObject("LaserVisual").AddComponent<LaserVisual>(); laser.arena = arena; laser.until = arena.Now + .1f;
                laser.transform.position = muzzle + Vector2.right * direction * visible / 2;
                laser.transform.localScale = new Vector3(visible, data.Thickness*arena.MvpGeometry(hitOrigin,true), 1);
                var renderer = laser.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = arena.square; renderer.color = new Color(.1f, .6f, 1, .85f); renderer.sortingOrder = 25;
                if (arena.spriteMaterial) renderer.sharedMaterial = arena.spriteMaterial;
                arena.RangedPresentation?.Laser(laser,owner,muzzle+Vector2.right*direction*visible,direction,hitPoints,IgnoresTerrain);
                arena.lasers.Add(laser);
            }
            public int ClaimedTargetCount => seen.Count;
            public void Record(int targetCount) { arena.Record("laser fired damage=" + damage.ToString("0.##") + " targets=" + targetCount); arena.FinishSketchHit(); }
        }
    }
}
