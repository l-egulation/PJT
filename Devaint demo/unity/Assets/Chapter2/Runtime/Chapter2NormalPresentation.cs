using System;
using System.Collections.Generic;
using Deviant.Stage1Monsters.Game;
using UnityEngine;
namespace Deviant.Chapter2
{
    [Serializable] public sealed class C2FxClip
    {
        public string id;public string[] frames;public int[] milliseconds;public bool loop;
        [NonSerialized] Sprite[] sprites;
        public float Duration {get{float n=0;foreach(int ms in milliseconds)n+=ms;return n/1000;}}
        public Sprite Sample(float age)
        {
            if(sprites==null){sprites=Array.ConvertAll(frames,p=>Resources.Load<Sprite>(p));if(Array.Exists(sprites,s=>!s))throw new InvalidOperationException("Missing selected stage2 FX "+id);}
            float t=Mathf.Max(0,age)*1000;if(loop)t%=Duration*1000;
            for(int i=0;i<milliseconds.Length;i++){if(t<milliseconds[i])return sprites[i];t-=milliseconds[i];}return sprites[sprites.Length-1];
        }
    }
    // Melee wind-up cue, the stage 1 melee look (data/combat/presentation/chapter2_attack_fx.json windupCue): through the
    // wind-up the body steps one native pixel every shakeStepSeconds (both sides from shakeWidenFrom), reddens in tintColor
    // up to tintMax alpha, and is covered in flashColor for the last flashSeconds before the attack.
    [Serializable] public sealed class C2WindupCue{public string[] melee;public float shakeStepSeconds,shakeWidenFrom,flashSeconds,tintMax;public int[] flashColor,tintColor;}
    [Serializable] public sealed class C2FxData{public C2WindupCue windupCue;public C2FxClip[] clips;}
    [DefaultExecutionOrder(900)]
    public sealed class Chapter2NormalPresentation : MonoBehaviour
    {
        Chapter2NormalEnemy actor;C2FxData data;Chapter1NewMonsterArt common;
        readonly List<SpriteRenderer> parts=new List<SpriteRenderer>();int used;Material waveMaterial;MaterialPropertyBlock ground;
        SpriteRenderer cueOverlay;MaterialPropertyBlock cueColor;
        // This frame's cue: the body offset in native pixels, the red overlay alpha and whether the white flash shows.
        public int CueOffsetPixels{get;private set;}
        public float CueRedness{get;private set;}
        public bool CueFlashing{get;private set;}
        public int ActiveEffects=>used+(actor?Chapter2NormalOrdnance.Get(actor.arena).EffectCount(actor):0);
        public void Bind(Chapter2NormalEnemy owner)
        {
            actor=owner;data=JsonUtility.FromJson<C2FxData>(Resources.Load<TextAsset>("Chapter2AttackFx").text);common=Chapter1NewMonsterArt.Shared;
            gameObject.AddComponent<Chapter2PrototypeArt>().Bind(actor,Chapter2MonsterArt.Load().Find(actor.ArtId));
            waveMaterial=new Material(Shader.Find("Deviant/Stage1GroundClippedSprite"));ground=new MaterialPropertyBlock();
        }
        C2FxClip Clip(string id)=>Array.Find(data.clips,c=>c.id==id)??throw new InvalidOperationException(id);
        public void ResetPose(){foreach(var p in parts)if(p)p.enabled=false;used=0;CueOffsetPixels=0;CueRedness=0;CueFlashing=false;if(cueOverlay)cueOverlay.enabled=false;}
        void DrawWindupCue(float a)
        {
            var cue=data.windupCue;var s=actor.Settings;
            if(cue==null||cue.melee==null||Array.IndexOf(cue.melee,actor.ArtId)<0||a<0||a>=s.windup||!actor.visual.enabled)return;
            bool odd=Mathf.FloorToInt(a/cue.shakeStepSeconds)%2==1;
            CueOffsetPixels=a/s.windup>=cue.shakeWidenFrom?(odd?1:-1):(odd?1:0);
            // Chapter2PrototypeArt (earlier in LateUpdate) has just reset the body transform this frame.
            actor.visual.transform.localPosition=new Vector3(CueOffsetPixels/32f,0,0);
            if(!cueOverlay)
            {
                cueOverlay=new GameObject("Wind-up cue").AddComponent<SpriteRenderer>();cueOverlay.transform.SetParent(actor.visual.transform,false);
                cueOverlay.sharedMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");cueColor=new MaterialPropertyBlock();
            }
            cueOverlay.sortingLayerID=actor.visual.sortingLayerID;cueOverlay.sortingOrder=actor.visual.sortingOrder+1;
            cueOverlay.sprite=actor.visual.sprite;cueOverlay.flipX=actor.visual.flipX;cueOverlay.enabled=cueOverlay.sprite;
            CueFlashing=a>=s.windup-cue.flashSeconds;
            float t=a/Mathf.Max(.0001f,s.windup-cue.flashSeconds);CueRedness=CueFlashing?0:cue.tintMax*t*t;
            var c=CueFlashing?cue.flashColor:cue.tintColor;
            cueColor.SetColor("_Color",new Color32((byte)c[0],(byte)c[1],(byte)c[2],255));cueOverlay.SetPropertyBlock(cueColor);
            cueOverlay.color=new Color(1,1,1,CueFlashing?1:CueRedness);
        }
        SpriteRenderer Show(Sprite sprite,Vector2 position,bool flip=false,float scale=1,bool wave=false)
        {
            if(used==parts.Count){var go=new GameObject("Existing chapter2 body attack FX");go.transform.SetParent(transform,false);parts.Add(go.AddComponent<SpriteRenderer>());}
            var r=parts[used++];r.sprite=sprite;r.enabled=true;r.flipX=flip;r.transform.position=position;r.transform.localScale=Vector3.one*scale;r.color=Color.white;
            r.sortingLayerID=actor.visual.sortingLayerID;r.sortingOrder=actor.visual.sortingOrder+2;r.sharedMaterial=wave?waveMaterial:actor.visual.sharedMaterial;
            ground.SetFloat("_GroundY",actor.Position.y);r.SetPropertyBlock(wave?ground:null);return r;
        }
        void LateUpdate()=>Sample();
        public void Sample()
        {
            ResetPose();if(!actor||actor.Dead||!actor.aggressive||!actor.Attacking)return;
            float a=actor.AttackAge;var s=actor.Settings;bool flip=actor.facing>0;Vector2 feet=actor.Position;
            DrawWindupCue(a);
            if(actor.ArtId=="B3")
            {
                if(a<s.windup){float k=Mathf.Clamp01(a/s.windup);Show(common.Find("millstone-wave").SpriteAt(0),feet+new Vector2(actor.facing*72/32f,35/32f),flip,.25f+.35f*k,true);}
                else if(actor.Charging)Show(common.Find("millstone-wave").SpriteAt(a-s.windup),feet+new Vector2(actor.facing*72/32f,35/32f),flip,1.1f,true);
                float impact=actor.ChargeStopAge+s.slamWindup;
                if(!actor.Charging&&a>=impact&&a<impact+Clip("b3-impact").Duration)Show(Clip("b3-impact").Sample(a-impact),feet+Vector2.right*actor.facing*65/32f,flip);
            }
            if(actor.ArtId=="M3")for(int i=0;i<3;i++)
            {
                float star=s.hitWindowStart[i]+.02f;if(a>=star&&a<star+Clip("m3-star").Duration)Show(Clip("m3-star").Sample(a-star),feet+new Vector2(actor.facing*4,1.25f),flip);
            }
            if(actor.ArtId=="K4"&&a>=s.windup&&a<s.windup+s.active)Show(Clip("k4-ink").Sample(a-s.windup),feet+new Vector2(actor.facing*67/32f,57/32f),flip);
            if(actor.ArtId=="H3"&&a<s.windup)Show(Clip("h3-formation").Sample(a),feet+new Vector2(actor.facing*s.launchX,s.launchY),flip);
        }
        void OnDisable()=>ResetPose();
        void OnDestroy(){if(waveMaterial)Destroy(waveMaterial);}
    }
}
