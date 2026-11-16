using Deviant.Combat;

namespace Deviant.Definitions
{
    // Muzzle settings stay at activation; velocity, damage and the fragment
    // multiplier remain the values supplied to the spawned native consumer.
    public sealed class BombProjectileDefinition
    {
        readonly float[] fragmentAngles;
        public float Radius { get; }
        public float Gravity { get; }
        public float BlastRadius { get; }
        public float Lifetime { get; }
        public float FuseSeconds { get; }
        public bool ExplodeOnExpiry { get; }
        public float FragmentDamage { get; }
        public float FragmentSpeed { get; }
        public float FragmentSpawnOffset { get; }
        public int FragmentCount => fragmentAngles == null ? 0 : fragmentAngles.Length;
        public BombProjectileDefinition Fragment { get; }
        public float FragmentAngleAt(int index) => fragmentAngles[index];

        public BombProjectileDefinition(BombData source)
        {
            Radius = source.radius; Gravity = source.gravity; BlastRadius = source.blastRadius;
            Lifetime = source.lifetime; FuseSeconds = source.fuseSeconds; ExplodeOnExpiry = source.explodeOnExpiry;
            if (source.fragmentAngles == null || source.fragmentAngles.Length == 0) return;
            fragmentAngles = (float[])source.fragmentAngles.Clone();
            FragmentDamage = source.fragmentDamage; FragmentSpeed = source.fragmentSpeed; FragmentSpawnOffset = source.fragmentSpawnOffset;
            Fragment = new BombProjectileDefinition(source.fragmentRadius, source.fragmentGravity, source.fragmentBlastRadius, source.fragmentLifetime);
        }

        public BombProjectileDefinition WithRadius(float scale)=>new BombProjectileDefinition(this,scale);
        BombProjectileDefinition(BombProjectileDefinition source,float scale)
        {Radius=source.Radius*scale;Gravity=source.Gravity;BlastRadius=source.BlastRadius*scale;Lifetime=source.Lifetime;FuseSeconds=source.FuseSeconds;ExplodeOnExpiry=source.ExplodeOnExpiry;fragmentAngles=source.fragmentAngles==null?null:(float[])source.fragmentAngles.Clone();FragmentDamage=source.FragmentDamage;FragmentSpeed=source.FragmentSpeed;FragmentSpawnOffset=source.FragmentSpawnOffset;Fragment=source.Fragment?.WithRadius(scale);}
        BombProjectileDefinition(float radius, float gravity, float blastRadius, float lifetime)
        {
            Radius = radius; Gravity = gravity; BlastRadius = blastRadius; Lifetime = lifetime;
            ExplodeOnExpiry = true;
        }
    }
}
