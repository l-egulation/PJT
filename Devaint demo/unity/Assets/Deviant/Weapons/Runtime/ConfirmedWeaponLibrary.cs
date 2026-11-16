using System;
using UnityEngine;

namespace Deviant.Weapons
{
    [Serializable] public class PixelFrame { public string id,path; public Sprite sprite; }
    [Serializable] public class PoseStep { public string frame,phase; public float ms,dx,dy; }
    [Serializable] public class PoseClip
    {
        public string id; public PoseStep[] steps; public bool loop; public float duration_ms;
        public PoseStep At(float ms)
        {
            if(loop) ms=Mathf.Repeat(ms,duration_ms);
            foreach(var s in steps){if(ms<s.ms) return s; ms-=s.ms;}
            return steps[steps.Length-1];
        }
    }
    [Serializable] public class AttackEvent
    {
        public int hit,damage; public float impact,fxStart,range,height,advance,chainAt,bufferEnd,bufferMs;
    }
    [Serializable] public class EffectStep
    {
        public string path; public Sprite sprite; public float ms,ox,oy,scale=1,angle; public bool flip;
    }
    [Serializable] public class EffectClip
    {
        public int hit; public EffectStep[] steps;
        public EffectStep At(float time)
        {
            foreach(var s in steps){if(time<s.ms)return s;time-=s.ms;}
            return null;
        }
    }
    [Serializable] public class ConfirmedWeapon
    {
        public string id,name; public float pivotX,pivotY,fxPivotX,fxPivotY,comboWindowMs;
        public PixelFrame[] frames; public PoseClip[] modules; public AttackEvent[] attacks; public EffectClip[] effects;
        public PoseClip Clip(string id)=>Array.Find(modules,m=>m.id==id);
        public Sprite Sprite(string id)=>Array.Find(frames,f=>f.id==id).sprite;
    }
    [Serializable] public class SourceData { public ConfirmedWeapon[] weapons; public PixelFrame[] dummy; public float dummyPivotX,dummyPivotY; }
    public class ConfirmedWeaponLibrary : ScriptableObject
    {
        public ConfirmedWeapon[] weapons;
        public PixelFrame[] dummy;
        public float dummyPivotX,dummyPivotY;
        public Material material;
        public const float PPU=32;
    }
}
