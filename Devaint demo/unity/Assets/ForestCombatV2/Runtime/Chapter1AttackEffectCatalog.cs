using System;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable]
    public sealed class Chapter1AttackEffectClip
    {
        public string species;
        public Sprite[] frames;
        public float[] seconds;
        public Sprite[] afterimages;
        public float Duration { get { float sum=0;foreach(float t in seconds)sum+=t;return sum; } }
        public int FrameAt(float age)
        {
            if(age<0)return -1;
            for(int i=0;i<seconds.Length;i++){if(age<seconds[i])return i;age-=seconds[i];}
            return -1;
        }
    }

    public sealed class Chapter1AttackEffectCatalog : ScriptableObject
    {
        public Chapter1AttackEffectClip[] clips;
        public Chapter1AttackEffectClip Find(string species) => Array.Find(clips,c=>c.species==species);
    }
}
