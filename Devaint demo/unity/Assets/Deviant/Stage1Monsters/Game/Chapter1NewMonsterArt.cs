using System;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    [Serializable] public sealed class Chapter1NativeFrame { public string resource,sha256; }
    [Serializable] public sealed class Chapter1NativeClip
    {
        public string id,role,alpha;
        public int width,height,ppu;
        public float[] pivot;
        public int[] ms;
        public bool loop;
        public string[] palette;
        public Chapter1NativeFrame[] frames;
        [NonSerialized] Sprite[] sprites;
        public int DurationMs { get { int sum=0;foreach(int t in ms)sum+=t;return sum; } }
        public int FrameAt(float age)
        {
            double time=Math.Max(0,age*1000.0);if(loop)time%=DurationMs;
            for(int i=0;i<ms.Length;i++){if(time<ms[i])return i;time-=ms[i];}
            return ms.Length-1;
        }
        public Sprite SpriteAt(float age,bool reverse=false)
        {
            Warm();
            int index=FrameAt(age);return sprites[reverse?(frames.Length-index)%frames.Length:index];
        }
        // Loads every frame now, so the first time a clip plays does not read it from disk mid-fight.
        public void Warm()
        {
            if(sprites!=null)return;
            var loaded=new Sprite[frames.Length];
            for(int i=0;i<frames.Length;i++)
            {
                loaded[i]=Resources.Load<Sprite>(frames[i].resource);
                if(!loaded[i])throw new InvalidOperationException("Missing approved monster frame: "+frames[i].resource);
            }
            sprites=loaded;
        }
    }
    [Serializable] public sealed class Chapter1NewMonsterArt
    {
        public string schema;
        public Chapter1NativeClip[] clips;
        public Chapter1NativeClip Find(string id)
        {
            var clip=Array.Find(clips,c=>c.id==id);
            if(clip==null)throw new InvalidOperationException("Missing approved monster clip: "+id);
            return clip;
        }
        static Chapter1NewMonsterArt shared;
        // One catalog for every monster, all frames loaded the first time a stage builds its monsters (behind the
        // stage's load fade) instead of each clip's first play in a fight (playtest 2026-11-04 lag analysis).
        public static Chapter1NewMonsterArt Shared
        {
            get
            {
                if(shared==null){var art=Load();foreach(var clip in art.clips)clip.Warm();shared=art;}
                return shared;
            }
        }
        // Leaving chapter one (Chapter2EntryBridge): drop the shared frames so the next scene load can unload them.
        // A later chapter-one stage warms them again behind its own load fade.
        public static void ReleaseShared() => shared = null;
        public static Chapter1NewMonsterArt Load()
        {
            var asset=Resources.Load<TextAsset>("Chapter1NewMonsterArt");
            if(!asset)throw new InvalidOperationException("Import approved Chapter1 new monster art first.");
            var value=JsonUtility.FromJson<Chapter1NewMonsterArt>(asset.text);
            if(value.schema!="chapter1-new-monster-art-v1"||value.clips==null||value.clips.Length<8)
                throw new InvalidOperationException("Invalid approved new monster art catalog.");
            return value;
        }
    }
}
