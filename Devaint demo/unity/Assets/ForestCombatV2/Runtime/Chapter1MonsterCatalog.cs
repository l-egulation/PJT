using System;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable]
    public sealed class Chapter1MonsterClip
    {
        public string species, motion, displayName;
        public Sprite[] frames;
        public float[] seconds;
        public bool loop;
        public float Duration { get { float total=0; foreach(float t in seconds) total+=t; return total; } }
        public int FrameAt(float age, bool repeat)
        {
            float t=repeat ? Mathf.Repeat(Mathf.Max(0,age),Duration) : Mathf.Max(0,age);
            for(int i=0;i<seconds.Length;i++) { if(t<seconds[i])return i; t-=seconds[i]; }
            return frames.Length-1;
        }
    }

    public sealed class Chapter1MonsterCatalog : ScriptableObject
    {
        public Chapter1MonsterClip[] clips;
        public Chapter1MonsterClip Find(string species,string motion)
        { return Array.Find(clips,c=>c.species==species && c.motion==motion); }
    }
}
