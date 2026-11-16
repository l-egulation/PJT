using System;
using System.Collections.Generic;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // One cast-start capture of the existing catalog input. Execution policies,
    // clocks and activation-time resource snapshots remain with their owners.
    public sealed class SkillExecutionDefinition
    {
        public string Kind { get; }
        public string Id { get; }
        public string Name { get; }
        public string Slot { get; }
        public string Vertical { get; }
        public string Facing { get; }
        public string Description { get; }
        public bool Unavailable { get; }
        public float Cooldown { get; }
        public float Activation { get; }
        public float End { get; }
        public float InputSpeed { get; }
        public float ChargeMin { get; }
        public float ChargeMax { get; }
        public float ChargeMinDamage { get; }
        public float ChargeMaxDamage { get; }
        public int ChargeSteps { get; }
        public float BasicLungeDistance { get; }
        public float BasicLungeStart { get; }
        public float BasicLungeDuration { get; }
        public int Capacity { get; }
        public IReadOnlyList<SkillHitDefinition> Hits { get; }
        public IReadOnlyList<float> InvulStart { get; }
        public IReadOnlyList<float> InvulEnd { get; }
        public IReadOnlyList<float> VerticalFreeze { get; }
        public SkillMovementDefinition Movement { get; }
        public SkillEffectDefinition Effect { get; }
        public SkillBombLaunchDefinition Bomb { get; }
        public SkillRangedLaunchDefinition Ranged { get; }
        public SkillMissileDefinition Missile { get; }
        public ActionTrackDefinition Tracks { get; }
        public bool UsesCompiledTracks => Tracks != null;
        public AuthoredSkillDefinition Authored { get; }
        public bool HasProjectileWave => UsesCompiledTracks ? Tracks.Effect?.Kind == ActionEffectTrackKind.ProjectileWave : Effect != null && Effect.Type == "projectile";
        public bool HasOwnerGuard => UsesCompiledTracks ? Tracks.Defense?.Kind == ActionDefenseTrackKind.OwnerGuard : Effect != null && Effect.Type == "owner_guard";

        public SkillExecutionDefinition(SkillData source) : this(source, ActionTrackDocument.IsAbsent(source?.actionTracks) ? null : new ActionTrackDefinition(source), null) { }
        internal SkillExecutionDefinition(SkillData source, ActionTrackDefinition tracks) : this(source, tracks, null) { }
        internal SkillExecutionDefinition(SkillData source, ActionTrackDefinition tracks, AuthoredSkillDefinition authored)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Tracks = tracks; Authored = authored;
            Kind = source.kind; Id = source.id; Name = source.name; Slot = source.slot;
            Vertical = source.vertical; Facing = source.facing; Description = source.description;
            Unavailable = source.unavailable; Cooldown = source.cooldown; Activation = tracks?.Activation ?? source.activation;
            End = tracks?.BodyEnd ?? source.end; InputSpeed = source.inputSpeed; Capacity = source.capacity;
            ChargeMin = source.chargeMin; ChargeMax = source.chargeMax;
            ChargeMinDamage = source.chargeMinDamage; ChargeMaxDamage = source.chargeMaxDamage; ChargeSteps = source.chargeSteps;
            BasicLungeDistance = source.basicLungeDistance; BasicLungeStart = source.basicLungeStart; BasicLungeDuration = source.basicLungeDuration;
            if (tracks != null) Hits = tracks.Execution?.Hits ?? tracks.CompileHits();
            else if (source.hits != null && source.hits.Length == 0) Hits = Array.Empty<SkillHitDefinition>();
            else if (source.hits != null)
            {
                var hits = new SkillHitDefinition[source.hits.Length];
                for (int i = 0; i < hits.Length; i++) hits[i] = source.hits[i] == null ? null : new SkillHitDefinition(source.hits[i]);
                Hits = Array.AsReadOnly(hits);
            }
            InvulStart = Copy(source.invulStart); InvulEnd = Copy(source.invulEnd);
            VerticalFreeze = Copy(source.verticalFreeze);
            Movement = tracks != null ? tracks.Execution?.Movement ?? tracks.CompileMovement() : source.movement == null ? null : new SkillMovementDefinition(source.movement);
            Effect = tracks != null ? tracks.Execution?.Effect ?? tracks.CompileEffect() : source.effect == null ? null : new SkillEffectDefinition(source.effect);
            Bomb = source.bomb == null ? null : new SkillBombLaunchDefinition(source.bomb);
            Ranged = source.ranged == null ? null : new SkillRangedLaunchDefinition(source.ranged);
            Missile = source.missile == null ? null : new SkillMissileDefinition(source.missile);
        }

        internal static IReadOnlyList<float> Copy(float[] source) => source == null ? null : source.Length == 0 ? Array.Empty<float>() : Array.AsReadOnly((float[])source.Clone());
    }

    public sealed class SkillBoxDefinition
    {
        public float Width { get; }
        public float Height { get; }
        public float X { get; }
        public float Y { get; }
        public SkillBoxDefinition(BoxData source)
        {
            Width = source.width; Height = source.height; X = source.x; Y = source.y;
        }
    }

    public sealed class SkillHitDefinition
    {
        public string Id { get; }
        public ActionHitAnchor? TrackAnchor { get; }
        public float Time { get; }
        public float Damage { get; }
        public float Active { get; }
        public float Root { get; }
        public float Stun { get; }
        public float Push { get; }
        public float PushSeconds { get; }
        public bool PushWithCaster { get; }
        public string Anchor { get; }
        public string Targets { get; }
        public SkillBoxDefinition Box { get; }
        public SkillHitDefinition(HitData source)
        {
            Time = source.time; Damage = source.damage; Active = source.active; Root = source.root;
            Stun = source.stun; Push = source.push; PushSeconds = source.pushSeconds; PushWithCaster = source.pushWithCaster;
            Anchor = source.anchor; Targets = source.targets;
            Box = source.box == null ? null : new SkillBoxDefinition(source.box);
        }
        internal SkillHitDefinition(HitData source, string id, ActionHitAnchor anchor) : this(source) { Id = id; TrackAnchor = anchor; }
    }

    public sealed class SkillMovementDefinition
    {
        public string Type { get; }
        public bool UsesCompiledTrack { get; }
        public string WallPolicy { get; }
        public float Start { get; }
        public float Duration { get; }
        public float Distance { get; }
        public float Speed { get; }
        public float WallRecovery { get; }
        public float Gap { get; }
        public float RecastWindow { get; }
        public float TargetRadius { get; }
        public SkillBoxDefinition TargetBox { get; }
        public bool PrimaryPush { get; }
        public float EndDamage { get; }
        public SkillMovementDefinition(MoveData source)
        {
            Type = source.type; WallPolicy = source.wallPolicy; Start = source.start; Duration = source.duration;
            Distance = source.distance; Speed = source.speed; WallRecovery = source.wallRecovery; Gap = source.gap;
            RecastWindow = source.recastWindow; TargetRadius = source.targetRadius; PrimaryPush = source.primaryPush; EndDamage = source.endDamage;
            TargetBox = source.targetBox == null ? null : new SkillBoxDefinition(source.targetBox);
        }
        internal SkillMovementDefinition(MoveData source, bool compiledTrack) : this(source) { UsesCompiledTrack = compiledTrack; }
    }

    public sealed class SkillEffectDefinition
    {
        public string Type { get; }
        public float Duration { get; }
        public float Hp { get; }
        public float Reduction { get; }
        public float Speed { get; }
        public float Distance { get; }
        public float Radius { get; }
        public float X { get; }
        public float Y { get; }
        public float ParryWindow { get; }
        public float ParryDamage { get; }
        public SkillBoxDefinition Box { get; }
        public SkillEffectDefinition(EffectData source)
        {
            Type = source.type; Duration = source.duration; Hp = source.hp; Reduction = source.reduction;
            Speed = source.speed; Distance = source.distance; Radius = source.radius; X = source.x; Y = source.y;
            ParryWindow = source.parryWindow; ParryDamage = source.parryDamage;
            Box = source.box == null ? null : new SkillBoxDefinition(source.box);
        }
    }

    public sealed class SkillBombLaunchDefinition
    {
        public float Angle { get; }
        public float Speed { get; }
        public float SpawnHeight { get; }
        public float SpawnGap { get; }
        public BombProjectileDefinition Projectile { get; }
        public SkillBombLaunchDefinition(BombData source)
        {
            Angle = source.angle; Speed = source.speed; SpawnHeight = source.spawnHeight; SpawnGap = source.spawnGap;
            Projectile = new BombProjectileDefinition(source);
        }
    }

    public sealed class SkillRangedLaunchDefinition
    {
        public float Speed { get; }
        public float SpawnHeight { get; }
        public float SpawnX { get; }
        public float SpawnGap { get; }
        public float Thickness { get; }
        public float RecoilDistance { get; }
        public float RecoilDuration { get; }
        public float RecoilHopHeight { get; }
        public IReadOnlyList<float> Angles { get; }
        public RangedProjectileDefinition Projectile { get; }
        // Full-charge collision radius of a charged shot; 0 for every other launch.
        public float ChargeRadius { get; }
        public SkillRangedLaunchDefinition(RangedData source)
        {
            Speed = source.speed; SpawnHeight = source.spawnHeight; SpawnX = source.spawnX; SpawnGap = source.spawnGap;
            Thickness = source.thickness; RecoilDistance = source.recoilDistance; RecoilDuration = source.recoilDuration; RecoilHopHeight = source.recoilHopHeight;
            Angles = SkillExecutionDefinition.Copy(source.angles); Projectile = new RangedProjectileDefinition(source); ChargeRadius = source.chargeRadius;
        }
        SkillRangedLaunchDefinition(SkillRangedLaunchDefinition source, RangedProjectileDefinition projectile)
        {
            Speed = source.Speed; SpawnHeight = source.SpawnHeight; SpawnX = source.SpawnX; SpawnGap = source.SpawnGap; Thickness = source.Thickness;
            RecoilDistance = source.RecoilDistance; RecoilDuration = source.RecoilDuration; RecoilHopHeight = source.RecoilHopHeight;
            Angles = source.Angles; ChargeRadius = source.ChargeRadius; Projectile = projectile;
        }
        // The released projectile radius moves from its minimum toward ChargeRadius by the charge amount (0..1).
        public SkillRangedLaunchDefinition WithChargeAmount(float amount)
        {
            if (ChargeRadius <= 0 || Projectile.Radius <= 0) return this;
            amount = amount < 0 ? 0 : amount > 1 ? 1 : amount;
            float radius = Projectile.Radius + (ChargeRadius - Projectile.Radius) * amount;
            return new SkillRangedLaunchDefinition(this, Projectile.WithGeometry(1, radius / Projectile.Radius));
        }
    }

    public sealed class SkillMissileDefinition
    {
        public float Speed { get; }
        public float Distance { get; }
        public float BlastRadius { get; }
        public float Recovery { get; }
        public float ManualDelay { get; }
        public float RecoilHeight { get; }
        public float RecoilDistance { get; }
        public float DistanceBonusMaxDamage { get; }
        public float DistanceBonusFullDistance { get; }
        public SkillMissileDefinition(MissileData source)
        {
            Speed = source.speed; Distance = source.distance; BlastRadius = source.blastRadius; Recovery = source.recovery;
            ManualDelay = source.manualDelay; RecoilHeight = source.recoilHeight; RecoilDistance = source.recoilDistance;
            DistanceBonusMaxDamage = source.distanceBonusMaxDamage; DistanceBonusFullDistance = source.distanceBonusFullDistance;
        }
        // 기본 폭발 피해에서 비행 거리에 비례해 DistanceBonusMaxDamage 까지 선형으로 늘어난다(그 이상은 고정).
        public float DamageAfter(float baseDamage, float traveled)
        {
            if (DistanceBonusMaxDamage <= baseDamage || DistanceBonusFullDistance <= 0) return baseDamage;
            float k = traveled <= 0 ? 0 : traveled >= DistanceBonusFullDistance ? 1 : traveled / DistanceBonusFullDistance;
            return baseDamage + (DistanceBonusMaxDamage - baseDamage) * k;
        }
    }
}
