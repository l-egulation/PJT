using System;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    [DefaultExecutionOrder(950)]
    public sealed class Chapter1NewMonsterPresentation : MonoBehaviour
    {
        Chapter1PrototypeEnemy actor;
        Chapter1NewMonsterArt art;
        public SpriteRenderer Warning { get; private set; }
        public SpriteRenderer Projectile { get; private set; }
        public SpriteRenderer Launch { get; private set; }
        public SpriteRenderer Wave { get; private set; }
        public SpriteRenderer Impact { get; private set; }
        // Red overlay on the body while the millstone winds up; Redness is its alpha this frame.
        public SpriteRenderer Red { get; private set; }
        public float Redness { get; private set; }
        public Vector2 ShakeOffset { get; private set; }
        Material waveMaterial;
        MaterialPropertyBlock waveProperties;
        float lastX=float.NaN,walkRoll;bool rolledOnFoot;
        public bool WalkRolling=>rolledOnFoot;
        public void Bind(Chapter1PrototypeEnemy owner,Chapter1NewMonsterArt source)
        {
            actor=owner;art=source;
            Warning=Part("Approved bomb warning",Mathf.Max(50,owner.visual.sortingOrder+3));
            Projectile=Part("Ink projectile",owner.visual.sortingOrder+4);
            Launch=Part("Steam muzzle pop",owner.visual.sortingOrder+3);
            Wave=Part("Growing white charge wave",owner.visual.sortingOrder+2);
            Impact=Part("Ink landing splash",owner.visual.sortingOrder+3);
            var shader=Shader.Find("Deviant/Stage1GroundClippedSprite");
            if(!shader)throw new InvalidOperationException("Missing ground-clipped charge shader.");
            waveMaterial=new Material(shader);Wave.sharedMaterial=waveMaterial;
            waveProperties=new MaterialPropertyBlock();
            if(owner.Spec.windupTintMax>0&&owner.Spec.windupTint!=null&&owner.Spec.windupTint.Length==3)
            {
                var node=new GameObject("Wind-up red");node.transform.SetParent(owner.visual.transform,false);
                Red=node.AddComponent<SpriteRenderer>();
                // The flat unlit material of the hit flash: one colour over the sprite's alpha, so the shake carries it.
                Red.sharedMaterial=Resources.Load<Material>("Chapter1HitFeedback/UnlitHit");
                Red.sortingLayerID=owner.visual.sortingLayerID;Red.sortingOrder=owner.visual.sortingOrder+1;Red.enabled=false;
                var tint=new MaterialPropertyBlock();var c=owner.Spec.windupTint;
                tint.SetColor("_Color",new Color32((byte)c[0],(byte)c[1],(byte)c[2],255));Red.SetPropertyBlock(tint);
            }
            actor.visual.sprite=art.Find(actor.Spec.ranged?"barrel-idle":"millstone-idle").SpriteAt(0);
            Draw();
        }
        SpriteRenderer Part(string name,int order)
        {
            var node=new GameObject(name);node.transform.SetParent(transform,false);
            var image=node.AddComponent<SpriteRenderer>();image.sharedMaterial=actor.visual.sharedMaterial;
            image.sortingLayerID=actor.visual.sortingLayerID;image.sortingOrder=order;image.enabled=false;return image;
        }
        void OnDestroy(){if(waveMaterial)Destroy(waveMaterial);}
        void OnDisable()=>Clear();
        // Resolve the approved native roll after actor/Animator updates as well as explicit test draws.
        // Repeated sampling is deterministic: only the arena simulation clock advances frames.
        void LateUpdate()=>Draw();
        public void Clear()
        { Redness=0;if(Red)Red.enabled=false;if(Warning)Warning.enabled=false;if(Projectile)Projectile.enabled=false;if(Launch)Launch.enabled=false;if(Wave)Wave.enabled=false;if(Impact)Impact.enabled=false; }
        static void Show(SpriteRenderer image,Sprite sprite,Vector2 at,bool mirror=false)
        {
            image.sprite=sprite;image.transform.position=at;image.transform.localScale=Vector3.one;
            image.transform.localRotation=Quaternion.identity;image.flipX=mirror;image.flipY=false;
            image.color=Color.white;image.enabled=true;
        }
        public static float ShakePixels(float age,float prepare,float stop)
        {
            float duration=prepare-stop;
            if(age<0||age>=duration||duration<=0)return 0;
            float u=age/duration,amplitude=.35f+2.25f*Mathf.Pow(u,1.4f);
            float phase=2*Mathf.PI*(3*age+6*age*age/(2*duration));
            return Mathf.Round(amplitude*Mathf.Sin(phase));
        }
        public void Draw()
        {
            Clear();if(!actor||actor.Dead||!actor.aggressive)return;
            float age=actor.AttackAge;
            actor.visual.enabled=true;actor.visual.transform.localScale=Vector3.one;
            actor.visual.transform.localRotation=Quaternion.identity;
            actor.visual.color=Color.white;
            ShakeOffset=Vector2.zero;
            if(actor.Spec.ranged)Barrel(age);else Millstone(age);
        }
        void Barrel(float age)
        {
            float impactAge=actor.arena.Now-actor.LastImpactAt;
            if(actor.LastImpactAt>=0&&impactAge>=0&&impactAge<.4f)
                Show(Impact,art.Find("barrel-impact").SpriteAt(impactAge),actor.LockedGround);
            bool active=actor.State==Chapter1PrototypeEnemy.Phase.Telegraph;
            actor.visual.transform.localPosition=Vector3.zero;actor.visual.flipX=false;
            actor.visual.sprite=art.Find(active?"barrel-attack":"barrel-idle").SpriteAt(active?age:0);
            float flight=age-actor.Spec.windupSeconds;
            if(active&&actor.ShotLaunched&&flight>=0)
            {
                float pulse=.5f+.5f*Mathf.Sin(flight*(10+flight/actor.Spec.warningSeconds*12));
                Show(Warning,art.Find("barrel-warning").SpriteAt(0),actor.BlastBounds.center);
                var native=Warning.sprite.bounds.size;
                Warning.transform.localScale=new Vector3(actor.BlastBounds.size.x/native.x,actor.BlastBounds.size.y/native.y,1);
                Warning.color=new Color(1,1,1,.62f+.38f*pulse);
                var muzzle=actor.Position+Vector2.up*(70/32f);
                if(flight<.32f)Show(Launch,art.Find("barrel-launch").SpriteAt(flight),muzzle);
                var camera=actor.PresentationCamera;
                float ceiling=camera?camera.ViewportToWorldPoint(new Vector3(.5f,1,Mathf.Abs(camera.transform.position.z))).y+2:actor.Position.y+18;
                float ascent=actor.Spec.ascentSeconds,wait=actor.Spec.offscreenSeconds,fall=actor.Spec.fallSeconds;
                bool rising=flight<ascent,falling=flight>=ascent+wait;
                if(rising||falling)
                {
                    Vector2 at;
                    if(rising)
                    { float u=Mathf.Clamp01(flight/ascent);at=new Vector2(actor.Position.x,Mathf.Lerp(actor.Position.y+88/32f,ceiling,1-Mathf.Pow(1-u,1.25f))); }
                    else
                    { float u=Mathf.Clamp01((flight-ascent-wait)/fall);at=new Vector2(actor.LockedGround.x,Mathf.Lerp(ceiling,actor.LockedGround.y+21.6f/32,u*u)); }
                    Show(Projectile,art.Find("barrel-projectile").SpriteAt(flight),at);Projectile.flipY=rising;
                }
            }
        }
        void Millstone(float age)
        {
            bool charge=actor.State==Chapter1PrototypeEnemy.Phase.Charge;
            bool preparing=actor.State==Chapter1PrototypeEnemy.Phase.Telegraph;
            float shake=preparing?ShakePixels(age,actor.Definition.prepare,actor.Spec.stopBeforeChargeSeconds):0;
            ShakeOffset=new Vector2(shake/32,0);actor.visual.transform.localPosition=ShakeOffset;
            actor.visual.flipX=actor.facing>0;
            // Walking rolls too (playtest 2026-11-06: it slid as one still picture). The charge's roll frames turn by the
            // distance walked, at the rate they turn while charging, so the stone rolls slower as it walks slower and
            // stays on its last frame when it stops. Driven by position only, so repeated draws in a frame agree.
            float walked=Mathf.Abs(actor.Position.x-lastX);lastX=actor.Position.x;
            if(!charge&&!preparing&&walked>0&&walked<1&&actor.Definition.chargeSpeed>0){walkRoll+=walked/actor.Definition.chargeSpeed;rolledOnFoot=true;}
            bool walkRolling=!charge&&!preparing&&rolledOnFoot;
            actor.visual.sprite=charge?art.Find("millstone-roll").SpriteAt(age-actor.Definition.prepare,true)
                :walkRolling?art.Find("millstone-roll").SpriteAt(walkRoll,true):art.Find("millstone-idle").SpriteAt(0);
            if(preparing)rolledOnFoot=false;
            // Reddens with the growing shake over the whole telegraph (including the still last 0.2 s), gone as it rolls.
            if(preparing&&Red&&actor.Definition.prepare>0)
            {
                float t=Mathf.Clamp01(age/actor.Definition.prepare);
                Redness=actor.Spec.windupTintMax*t*t;
                Red.sprite=actor.visual.sprite;Red.flipX=actor.visual.flipX;Red.color=new Color(1,1,1,Redness);Red.enabled=Redness>0;
            }
            if(!charge)return;
            float elapsed=age-actor.Definition.prepare;
            float growth=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.2f));
            if(growth<=0)return;
            Show(Wave,art.Find("millstone-wave").SpriteAt(elapsed),actor.Position+new Vector2(actor.facing*48/32f,62/32f),actor.facing>0);
            Wave.transform.localScale=Vector3.one*(1.55f*growth);
            waveProperties.SetFloat("_GroundY",actor.Position.y);Wave.SetPropertyBlock(waveProperties);
        }
    }
}
