using System;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    [Serializable] public class CharacterAnimationCatalog
    {
        public CharacterAnimationSet[] sets;
        public AttackMotionDocument[] motionSources;
        public MotionRegistryDocument motionRegistry;
        public MeleeFrameCatalogDocument meleeFrames;
        public SpecialFrameCatalogDocument specialFrames;
        [NonSerialized] public System.Collections.Generic.Dictionary<string, Sprite> motionSprites;
        // When set, motionSprites holds every known frame with a null value until the frame is first used
        // (or warmed for an equipped form). Read frames through TryMotionSprite.
        [NonSerialized] public bool deferMotionSprites;
        [NonSerialized] System.Collections.Generic.HashSet<string> resolvedMotionSprites;
        public bool TryMotionSprite(string frame, out Sprite sprite)
        {
            sprite = null;
            if (motionSprites == null || frame == null || !motionSprites.TryGetValue(frame, out sprite)) return false;
            if (!sprite && deferMotionSprites)
            {
                resolvedMotionSprites ??= new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                if (resolvedMotionSprites.Add(frame)) motionSprites[frame] = sprite = Resources.Load<Sprite>(frame);
            }
            return true;
        }
        // Reads the deferred art of one gameplay form (palettes and clip sets), one frame or clip per step.
        public System.Collections.Generic.IEnumerable<bool> WarmForm(string form)
        {
            if (string.IsNullOrEmpty(form)) yield break;
            bool Matches(string candidate) => candidate == form || candidate != null && candidate.StartsWith(form + "_", StringComparison.Ordinal);
            var palettes = motionRegistry?.palettes;
            if (deferMotionSprites && palettes != null)
                foreach (var palette in palettes)
                {
                    if (palette?.frames == null || !Matches(palette.form)) continue;
                    foreach (var frame in palette.frames) { if (TryMotionSprite(frame, out _)) yield return true; }
                }
            if (sets == null) yield break;
            foreach (var set in sets)
            {
                if (set == null || !(Matches(set.form) || set.gameplayForm == form)) continue;
                if (set.clips != null) foreach (var clip in set.clips) if (clip != null && clip.Deferred) { clip.Load(); yield return true; }
                if (set.effects != null) foreach (var effect in set.effects) if (effect != null && effect.Deferred) { effect.Load(); yield return true; }
            }
        }
    }
    [Serializable] public class CharacterAnimationSet
    {
        public string form;
        public string gameplayForm;
        public int launcherMode=-1;
        // Dash afterimages in the body's own colours instead of ink (the human hero before the ink, in the opening).
        public bool plainAfterimage;
        // Five frames of a longer "dash" clip to play as the approved dash poses (the village and opening bodies draw 12);
        // a five-frame dash clip needs none.
        public int[] dashPoses;
        public CharacterClip[] clips;
        public CharacterEffect[] effects;
        public float[] iconUvRect;
        public CharacterClip Find(string name) { foreach (var c in clips) if (c.name==name) return c; return null; }
        public CharacterEffect Effect(string attack) { if(effects!=null) foreach(var e in effects) if(e.attack==attack) return e; return null; }
    }
    [Serializable] public class CharacterClip
    {
        public string name;
        public string[] frames;
        public string[] stepIds; // Present only on migrated game-owned motion bindings.
        public float[] seconds;
        public int contact;
        public float sourceImpact=-1,fxStart=-1;
        public float sourceRelease=-1;
        public float[] muzzleX,muzzleY;
        // Native anchors survive motion edits that temporarily omit a known frame.
        [NonSerialized] internal System.Collections.Generic.IReadOnlyDictionary<string, Vector2> nativeMuzzles;
        [NonSerialized] Sprite[] loadedSprites;
        [NonSerialized] bool deferred;
        // A deferred clip reads its frames the first time anything asks for them, so weapons that are never
        // equipped never bring their art into memory (Web heap).
        public Sprite[] sprites
        {
            get { if (deferred) Load(); return loadedSprites; }
            set { deferred = false; loadedSprites = value; }
        }
        public bool Deferred => deferred;
        public void DeferLoad() { if (loadedSprites == null && frames != null) deferred = true; }
        public float Duration { get { float t=0; foreach(float s in seconds) t+=s; return t; } }
        public void Load()
        {
            deferred=false;
            var loaded=new Sprite[frames.Length];
            for(int i=0;i<frames.Length;i++)
            {
                loaded[i]=Resources.Load<Sprite>(frames[i]);
                if (!loaded[i]) Debug.LogError("Missing character frame: "+frames[i]);
            }
            loadedSprites=loaded;
        }
        public Sprite At(float time, bool loop)
        {
            if(sprites==null || sprites.Length==0) return null;
            return sprites[FrameIndex(time,loop)];
        }
        public int FrameIndex(float time,bool loop=false)
            => AttackMotionTiming.SelectFrame(seconds,Duration,time,loop);
        public float SourceTime(float age,float impact,float end)
        {
            float hit=sourceImpact;
            if(hit<0) { hit=0; for(int i=0;i<Mathf.Max(0,contact);i++) hit+=seconds[i]; }
            return AttackMotionTiming.MapSourceTime(age,impact,end,hit,Duration);
        }
        public Sprite Attack(float age,float impact,float end) { return At(SourceTime(age,impact,end),false); }
    }
    [Serializable] public class CharacterEffect : CharacterClip
    {
        public string attack;
        public bool flipX;
        public float scale = 1;
        public float rotation;
        public float[] x,y;
    }
}
