using System;
using UnityEngine;

namespace Deviant.Stage1Monsters
{
    [Serializable]
    public sealed class Stage1MonsterClip
    {
        public string id;
        public Sprite[] frames;
        public int[] milliseconds;
        public bool loop;
        public AnimationClip animation;
        public Vector2 pivot;
        public int DurationMs { get { int total=0; foreach(int ms in milliseconds) total+=ms; return total; } }
        public int FrameAt(float seconds)
        {
            double t=Math.Max(0,seconds*1000.0);
            if(loop) t%=DurationMs;
            for(int i=0;i<milliseconds.Length;i++) { if(t<milliseconds[i])return i; t-=milliseconds[i]; }
            return frames.Length-1;
        }
    }

    [Serializable]
    public sealed class Stage1MonsterDefinition
    {
        public string id, displayName;
        public Stage1MonsterClip[] motions;
        public RuntimeAnimatorController controller;
        public GameObject prefab;
        public Stage1MonsterClip Find(string motion) { return Array.Find(motions,m=>m.id==motion); }
    }

    public sealed class Stage1MonsterCatalog : ScriptableObject
    {
        public string sourceManifestSHA256;
        public Stage1MonsterDefinition[] monsters;
        public Stage1MonsterDefinition Find(string id) { return Array.Find(monsters,m=>m.id==id); }
    }
}
