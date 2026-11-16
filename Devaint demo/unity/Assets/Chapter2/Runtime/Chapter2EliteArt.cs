using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class C2EliteArtFrame
    {
        public string path;public float startMs,holdMs;public int source;
        [NonSerialized] public Sprite sprite;
    }
    [Serializable] public sealed class C2EliteArtClip
    {
        public string id;public bool loop;public float durationMs;public C2EliteArtFrame[] frames;
        public int FrameAt(float seconds)
        {
            float ms=seconds*1000;if(loop)ms=Mathf.Repeat(ms,durationMs);
            for(int i=frames.Length-1;i>=0;i--)if(ms>=frames[i].startMs-.0001f)return i;
            return 0;
        }
        public C2EliteArtFrame At(float seconds)=>frames[FrameAt(seconds)];
    }
    [Serializable] public sealed class C2EliteDebrisParticle
    {public int image,delayMs,flightMs;public float vx,vy,scale,angle,spin,originX;}
    [Serializable] public sealed class C2EliteDebrisBurst
    {public int attack,count;public C2EliteDebrisParticle[] particles;}
    [Serializable] public sealed class C2EliteDebris
    {public float gravity;public int[] impactTimesMs;public C2EliteDebrisBurst[] bursts;}
    [Serializable] public sealed class C2EliteSpriteImport
    {public string path,resource;public float[] canvas,pivot;public int ppu;}
    [Serializable] public sealed class Chapter2EliteArtData
    {
        public int schema,bodyReferenceHeightPx,ppu,teleportVanishMs,teleportArrivalMs,teleportHitMs,teleportEndMs,rockWrapMs,rockHitMs,rockBodyEndMs;
        public float[] fragmentAxes,rockCenterOffsetPx;public float rockDiameterPx;public C2EliteDebris debris;public C2EliteArtClip[] clips;public C2EliteSpriteImport[] sprites;
        public C2EliteArtClip Find(string id)
        {foreach(var clip in clips)if(clip.id==id)return clip;throw new InvalidOperationException("A3 art clip missing: "+id);}
    }
    public static class Chapter2EliteArt
    {
        static Chapter2EliteArtData loaded;
        public static Chapter2EliteArtData Load()
        {
            if(loaded!=null)return loaded;
            var text=Resources.Load<TextAsset>("Chapter2EliteArt");if(!text)throw new InvalidOperationException("Approved A3 art binding missing");
            var data=JsonUtility.FromJson<Chapter2EliteArtData>(text.text);
            if(data.schema!=1||data.ppu!=32||data.bodyReferenceHeightPx!=88||data.fragmentAxes?.Length!=5||data.rockCenterOffsetPx?.Length!=2||data.rockDiameterPx<=0)throw new InvalidOperationException("Invalid A3 art binding");
            var paths=new Dictionary<string,Sprite>();
            foreach(var clip in data.clips)foreach(var frame in clip.frames)
            {
                if(!paths.TryGetValue(frame.path,out var sprite)){sprite=Resources.Load<Sprite>(frame.path);if(!sprite)throw new InvalidOperationException("A3 sprite not deployed: "+frame.path);paths.Add(frame.path,sprite);}
                frame.sprite=clip.id=="fragment"?PadFragment(sprite):sprite;
            }
            loaded=data;return data;
        }
        static Sprite PadFragment(Sprite source)
        {
            // Keep original pixels untouched, with room for a one-native-pixel shader outline.
            var texture=new Texture2D(20,20,TextureFormat.RGBA32,false){name="A3 padded fragment",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[400];var original=source.texture.GetPixels32();
            for(int y=0;y<16;y++)Array.Copy(original,y*16,pixels,(y+2)*20+2,16);
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,20,20),new Vector2(.5f,.5f),32,0,SpriteMeshType.FullRect);
        }
    }
}
