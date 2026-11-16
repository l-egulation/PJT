using Deviant.Combat;

namespace Deviant.Definitions
{
    // Only values consumed after a shot is spawned. Muzzle, spread and recoil
    // remain activation concerns; the supplied velocity and damage stay pinned.
    public sealed class RangedProjectileDefinition
    {
        public string Type { get; }
        public float Distance { get; }
        public bool UnlimitedRange { get; }
        public float ImpactBoxSize { get; }
        public float Radius { get; }
        public bool Pierce { get; }
        public bool IgnoresTerrain { get; }
        public bool NoBasicStagger { get; }
        public float BlastRadius { get; }
        public float HitInterval { get; }
        public float ReturnDelay { get; }
        public float BehindGap { get; }
        public float HomingTurnRate { get; }
        public float HomingCone { get; }
        public float HomingMaxAngle { get; }
        public bool Homing => HomingTurnRate > 0;

        public RangedProjectileDefinition WithGeometry(float reach,float radius)=>new RangedProjectileDefinition(new RangedData{type=Type,ignoreTerrain=IgnoresTerrain,unlimitedRange=UnlimitedRange,impactBoxSize=ImpactBoxSize,distance=Distance*reach,radius=Radius*radius,pierce=Pierce,noBasicStagger=NoBasicStagger,blastRadius=BlastRadius*radius,hitInterval=HitInterval,returnDelay=ReturnDelay,behindGap=BehindGap,homingTurnRate=HomingTurnRate,homingCone=HomingCone,homingMaxAngle=HomingMaxAngle});
        public RangedProjectileDefinition(RangedData source)
        {
            Type = source.type; Distance = source.distance; UnlimitedRange = source.unlimitedRange; ImpactBoxSize = source.impactBoxSize; Radius = source.radius;
            Pierce = source.pierce; IgnoresTerrain = source.ignoreTerrain; NoBasicStagger = source.noBasicStagger; BlastRadius = source.blastRadius;
            HitInterval = source.hitInterval; ReturnDelay = source.returnDelay; BehindGap = source.behindGap;
            HomingTurnRate = source.homingTurnRate; HomingCone = source.homingCone; HomingMaxAngle = source.homingMaxAngle;
        }
    }
}
