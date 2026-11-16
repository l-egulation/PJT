using System;
using Deviant.Combat;

namespace Deviant.Definitions
{
    public enum RangedActionPolicy { StraightFirstHit, LaserAllOnce, PiercingPellets, ContactBombWithFragments, ContactBomb, ContactBombBurst, CasterMissile, LandingFuseBomb, PiercingOnce, FirstContactBlast, PiercingVisitReturn, PeriodicPiercing, ChargedStraightShot }
    [Serializable]
    public sealed class RangedActionSnapshotData
    {
        public string id, formId, policy, contentHash, sourceVersion;
        public int selection;
        public bool isBasic;
        public float inputSpeed;
        public BasicData basic;
        public SkillData special;
    }
    [Serializable]
    public sealed class RangedActionNumericData
    {
        public float activation, end;
        public float[] releaseTimes, damages;
        public RangedData ranged;
        public BombData bomb;
        public MissileData missile;
    }
}
