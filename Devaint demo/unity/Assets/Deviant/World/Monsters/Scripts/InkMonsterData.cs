using System;
using UnityEngine;
namespace Deviant.World
{
    [Serializable] public class MonsterFrameMeasurement {public int[] size,bodyBounds;}
    [Serializable] public class MonsterMotionSource
    {
        public string species,motion;
        public float[] seconds,pivot;
        public bool loop;
        public MonsterFrameMeasurement[] frames;
    }
    [Serializable] public class MonsterImportSource { public MonsterMotionSource[] motions; }
    public class InkMonsterData : ScriptableObject
    {
        public string species;
        public MotionClip move,attack;
        public MotionClip death;
        public bool sourceFacesLeft=true;
        public Vector2 bodySize,bodyOffset;
        public Vector4[] moveHurt,attackHurt;
        public Vector2 hitSize,hitOffset;
        public float moveSpeed,chargeSpeed,attackRange,windupSeconds,recoverySeconds,hoverHeight;
    }
}
