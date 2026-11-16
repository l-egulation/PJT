using System;
using System.Collections.Generic;
using System.Globalization;
using Deviant.Combat.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;

namespace Deviant.Combat
{
    public sealed partial class CombatFeelFeedback
    {
        readonly List<DamageNumber> damageNumbers=new List<DamageNumber>();
        readonly List<Ring> rings=new List<Ring>();
        readonly List<DeathPop> deathPops=new List<DeathPop>();
        readonly HashSet<SpriteRenderer> hiddenDeaths=new HashSet<SpriteRenderer>();
        readonly List<Cleave> cleaves=new List<Cleave>();
        readonly HashSet<Guid> heavyExecutions=new HashSet<Guid>();
        readonly Queue<Guid> heavyOrder=new Queue<Guid>();
        Sprite ringArt,inkArt,cutLineArt;
        Texture2D ringTexture,inkTexture,vignetteTexture,cutLineTexture;
        float playerHurtLeft;
        public bool PlayerHurtVignetteActive => playerHurtLeft>0;
        public float PlayerHurtEnvelope
        {
            get
            {
                if (!PlayerHurtVignetteActive || settings == null) return 0;
                float age = settings.playerHurtSeconds - playerHurtLeft;
                float rise = settings.playerHurtRiseSeconds <= 0 ? 1 : Mathf.Clamp01(age / settings.playerHurtRiseSeconds);
                float fade = Mathf.Clamp01((age - settings.playerHurtRiseSeconds) / (settings.playerHurtSeconds - settings.playerHurtRiseSeconds));
                return rise * (1 - fade * fade);
            }
        }
        public float PlayerHurtOpacity => settings == null ? 0 : settings.playerHurtOpacity * PlayerHurtEnvelope;
        public bool CompactHealthBars => settings!=null && settings.underfootHealthBars;
        public int ActiveDamageNumberCount => damageNumbers.Count;
        public int ActiveDeathPopCount => deathPops.Count;
        public int ActiveRingCount => rings.Count;
        public int ActiveCleaveCount => cleaves.Count;
        public float LastDamageValue { get; private set; }
        public bool LastDamageWasHeavy { get; private set; }
        public Bounds LastCleaveBounds { get; private set; }
        public bool OwnsDeathVisual(SpriteRenderer target) => EffectsEnabled && settings.killPop && hiddenDeaths.Contains(target);
        // 같은 대상의 연속 피해 숫자는 한 점에 겹치지 않게 줄(lane)을 나눠 쌓고, 거의 동시에 들어온 타격은
        // DamageNumberStagger 간격으로 차례로 튀어나오게 한다. age가 음수인 동안은 아직 나타나지 않은 대기 상태다.
        public const float DamageNumberSeconds=.75f,DamageNumberStagger=.07f,DamageNumberStackWindow=.4f,DamageNumberMaxDelay=.35f;
        public const int DamageNumberMaxLanes=5;
        const float DamageNumberLanePixels=26,DamageNumberLaneSwayPixels=14;
        sealed class DamageNumber {public Vector3 origin;public float age;public string text;public bool heavy,player,boss;public CombatActor owner;public int lane;}
        public int VisibleDamageNumberCount {get{int count=0;foreach(var number in damageNumbers)if(number.age>=0)count++;return count;}}
        public int DamageNumberLane(int index)=>damageNumbers[index].lane;
        public float DamageNumberAge(int index)=>damageNumbers[index].age;
        void AddDamageNumber(CombatActor target,Vector3 origin,float damage,bool heavy)
        {
            DamageNumber newest=null;int occupied=0;
            foreach(var other in damageNumbers) {
                if(other.owner!=target||other.age>=DamageNumberStackWindow)continue;
                occupied|=1<<other.lane;
                if(newest==null||other.age<=newest.age)newest=other;
            }
            int lane=0;while(lane<DamageNumberMaxLanes&&(occupied&(1<<lane))!=0)lane++;
            if(lane==DamageNumberMaxLanes)lane=(newest.lane+1)%DamageNumberMaxLanes;
            float start=newest==null?0:Mathf.Max(-DamageNumberMaxDelay,Mathf.Min(0,newest.age-DamageNumberStagger));
            damageNumbers.Add(new DamageNumber{origin=origin,age=start,text=CombatDamageVariance.Format(damage),heavy=heavy,player=target.isPlayer,boss=target.isBoss,owner=target,lane=lane});
            if(damageNumbers.Count>64)damageNumbers.RemoveAt(0);
        }
        sealed class Ring {public SpriteRenderer visual,target;public float age,size;}
        sealed class DeathPop {public SpriteRenderer body;public SpriteRenderer[] bits;public Vector3[] velocity;public Vector3 origin,bodyOrigin,bodyScale;public float age,floor;}
        sealed class Cleave {public CombatCast cast;public SpriteRenderer visual;public SwordRibbonView ribbon;public HeavyCleaveSweep sweep;public WhipSkillSweep whipSweep;public WhipTrailView whipTrail;public bool whip;public float age=-1;public Bounds bounds;public int facing;}
        void CreateFinisherArt()
        {
            cutLineTexture=new Texture2D(64,8,TextureFormat.RGBA32,false){name="Directional cut line",filterMode=FilterMode.Point};
            var linePixels=new Color32[64*8];
            for(int y=0;y<8;y++)for(int x=0;x<64;x++)if(Mathf.Abs(y-3.5f)<=.5f+(1-Mathf.Abs(x-31.5f)/31.5f)*2.5f)linePixels[y*64+x]=new Color32(255,255,255,255);
            cutLineTexture.SetPixels32(linePixels);cutLineTexture.Apply();cutLineArt=Sprite.Create(cutLineTexture,new Rect(0,0,64,8),Vector2.one*.5f,32);
            ringTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Combat feel contact ring",filterMode=FilterMode.Point};
            var pixels=new Color32[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
                float r=new Vector2(x-15.5f,y-15.5f).magnitude;
                if(r>=13.5f&&r<=14.5f)pixels[y*32+x]=new Color32(255,255,255,255);
            }
            ringTexture.SetPixels32(pixels);ringTexture.Apply();
            ringArt=Sprite.Create(ringTexture,new Rect(0,0,32,32),Vector2.one*.5f,32);
            inkTexture=new Texture2D(4,4,TextureFormat.RGBA32,false){name="Combat feel ink fragment",filterMode=FilterMode.Point};
            var ink=new Color32[16];for(int i=0;i<16;i++)if(i!=0&&i!=15)ink[i]=new Color32(255,255,255,255);
            inkTexture.SetPixels32(ink);inkTexture.Apply();inkArt=Sprite.Create(inkTexture,new Rect(0,0,4,4),Vector2.one*.5f,32);
            vignetteTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Player hurt edge tint",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var edgePixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++) {
                float u=x/63f,v=y/63f,edge=Mathf.Min(u,1-u,v,1-v);
                float a=Mathf.Pow(Mathf.Clamp01((settings.playerHurtBorder-edge)/settings.playerHurtBorder),2)*settings.playerHurtOpacity;
                edgePixels[y*64+x]=new Color(.85f,.025f,.015f,a);
            }
            vignetteTexture.SetPixels(edgePixels);vignetteTexture.Apply();
        }
        SpriteRenderer FeelSprite(string name,Sprite sprite,Material shader,int order)
        {
            var node=new GameObject(name);node.transform.SetParent(transform,false);
            var view=node.AddComponent<SpriteRenderer>();view.sprite=sprite;view.sharedMaterial=shader;view.sortingOrder=order;return view;
        }
        readonly System.Collections.Generic.List<RapierSkillFxVisual> rapierSkillFx = new System.Collections.Generic.List<RapierSkillFxVisual>();
        readonly System.Collections.Generic.List<HammerSkillFxVisual> hammerSkillFx = new System.Collections.Generic.List<HammerSkillFxVisual>();
        readonly List<GreatswordSkillVisual> greatswordVisuals = new List<GreatswordSkillVisual>();
        readonly List<BaseSwordSkillVisual> baseSwordVisuals = new List<BaseSwordSkillVisual>();
        public void CaptureFeelCast(CombatCast cast)
        {
            bool reviewedSwordCleave = false;
            if (cast && cast.owner == arena.Player && EffectsEnabled)
            {
                baseSwordVisuals.RemoveAll(value => !value);
                var sword = BaseSwordSkillVisual.Attach(cast);
                if (sword)
                {
                    baseSwordVisuals.Add(sword);
                    reviewedSwordCleave = cast.Definition.Id == "base_sword_default";
                }
            }
            if (cast && cast.owner == arena.Player && EffectsEnabled && GreatswordSkillVisual.Supports(cast.Definition.Id))
            {
                greatswordVisuals.RemoveAll(value => !value);
                greatswordVisuals.Add(GreatswordSkillVisual.Attach(cast));
            }
            // Rapier basics draw their thrust through the set effect (RapierSkillFx/thrust_*); the four specials use their approved clips (2026-11-09).
            if (cast && cast.owner == arena.Player && EffectsEnabled && RapierSkillFxVisual.Supports(cast))
            {
                rapierSkillFx.RemoveAll(value => !value);
                rapierSkillFx.Add(RapierSkillFxVisual.Attach(cast, transform));
            }
            if (cast && cast.owner == arena.Player && EffectsEnabled && HammerSkillFxVisual.Supports(cast))
            {
                hammerSkillFx.RemoveAll(value => !value);
                hammerSkillFx.Add(HammerSkillFxVisual.Attach(cast, transform));
            }
            if(cast&&cast.owner==arena.Player) {
                var captured=arena.FeelCharacterBinding;
                if(captured!=null&&SwordRibbonView.Supports(captured.Attack))
                    ObserveBladeSweep(cast.ExecutionId,captured.Output.transform.TransformVector(SwordRibbonView.SwingDirection(captured.Attack,cast.FeelFacing)));
            }
            // Pixel effects draw the hammer arc and the 강베기 sweep themselves.
            if(cast&&cast.owner==arena.Player&&UsePixelEffects) {
                if(cast.isBasic&&cast.Definition.Id.StartsWith("hammer_basic",StringComparison.Ordinal))PixelFx.HammerArc(cast);
                if(cast.Definition.Id=="base_sword_default") {
                    if(heavyExecutions.Add(cast.ExecutionId)){heavyOrder.Enqueue(cast.ExecutionId);if(heavyOrder.Count>128)heavyExecutions.Remove(heavyOrder.Dequeue());}
                    if (!reviewedSwordCleave) PixelFx.CleaveArc(cast);return;
                }
            }
            if(!cast||cast.owner!=arena.Player||cast.isBasic)return;
            if(cast.Definition.Id=="whip_default") {
                // 전용 휘감기 그림(채찍·고리가 몸 그림에 들어 있음)이 있으면 코드로 그린 고리·궤적을 겹치지 않는다.
                if(EffectsEnabled&&settings.whipTrail&&!WhipSkillFx.HasSkillArt(arena))cleaves.Add(new Cleave{cast=cast,facing=cast.FeelFacing,whip=true});
                return;
            }
            if(cast.Definition.Id!="base_sword_default")return;
            if(heavyExecutions.Add(cast.ExecutionId)){heavyOrder.Enqueue(cast.ExecutionId);if(heavyOrder.Count>128)heavyExecutions.Remove(heavyOrder.Dequeue());}
            if(!reviewedSwordCleave&&EffectsEnabled&&settings.heavyCleave)cleaves.Add(new Cleave{cast=cast,facing=cast.FeelFacing});
        }
        void PresentFinisher(CombatAcceptedHit hit,CombatActor target,Vector3 origin,bool heavy)
        {
            if (!target.isPlayer && hit.Origin.ActionId == "base_sword_follow_up" && hit.Origin.HitIndex >= 0 && hit.Origin.HitIndex <= 1)
            {
                baseSwordVisuals.RemoveAll(value => !value);
                bool isEcho = hit.Origin.HitIndex == 1;
                var mark = baseSwordVisuals.Find(value => value.Matches(target, hit.Origin.ExecutionId));
                if (mark && isEcho) mark.Burst();
                else if (!mark && (isEcho || !target.Dead))
                {
                    mark = BaseSwordSkillVisual.AttachScar(arena, target, hit.Origin, isEcho);
                    if (mark) baseSwordVisuals.Add(mark);
                }
            }
            if(target.isPlayer&&hit.Damage>0)
                // A repeated hit keeps a rising glow, or restores a fading glow to its peak.
                // Restarting the rise from zero on every hit would hide the edge during rapid damage.
                playerHurtLeft=PlayerHurtVignetteActive
                    ? Mathf.Max(playerHurtLeft,settings.playerHurtSeconds-settings.playerHurtRiseSeconds)
                    : settings.playerHurtSeconds;
            if(settings.damageNumbers&&hit.Damage>0) {
                LastDamageValue=hit.Damage;LastDamageWasHeavy=heavy;
                AddDamageNumber(target,origin+Vector3.up*.55f,hit.Damage,heavy);
            }
            if(settings.impactRing&&settings.centeredImpact&&!UsePixelHits&&!target.isPlayer&&Array.IndexOf(settings.impactRingVariants,LastImpactVariant)>=0) {
                var view=FeelSprite("White contact ring",ringArt,material,target.visual.sortingOrder+7);
                view.sortingLayerID=target.visual.sortingLayerID;view.transform.position=origin;
                rings.Add(new Ring{visual=view,target=target.visual,size=heavy?1.35f:1.8f});
            }
            if(settings.killPop&&!target.isPlayer&&hit.Before.Health>0&&hit.After.Dead&&hiddenDeaths.Add(target.visual)) {
                var body=FeelSprite("Finishing white silhouette",target.visual.sprite,material,target.visual.sortingOrder+5);
                body.sortingLayerID=target.visual.sortingLayerID;body.flipX=target.visual.flipX;body.flipY=target.visual.flipY;
                body.transform.position=target.visual.transform.position;body.transform.rotation=target.visual.transform.rotation;
                body.transform.localScale=target.visual.transform.lossyScale;
                var death=new DeathPop{body=body,bodyOrigin=body.transform.position,bodyScale=body.transform.localScale,
                    origin=origin,floor=target.Position.y,bits=new SpriteRenderer[12],velocity=new Vector3[12]};
                for(int i=0;i<death.bits.Length;i++) {
                    var bit=FeelSprite("Death ink fragment",inkArt,arena.spriteMaterial,target.visual.sortingOrder+8);
                    bit.sortingLayerID=target.visual.sortingLayerID;bit.color=new Color(.12f,.13f,.12f,1);bit.enabled=false;
                    bit.transform.localScale=Vector3.one*Random(1.1f,2.05f);
                    float a=i*Mathf.PI*2/death.bits.Length+Random(-.2f,.2f);
                    death.velocity[i]=new Vector3(Mathf.Cos(a)*Random(2.5f,5.5f)+direction*1.4f,Mathf.Sin(a)*Random(2.5f,5)-1,0);
                    death.bits[i]=bit;
                }
                target.visual.enabled=false;deathPops.Add(death);
            }
        }
        void TickFinishers(float delta)
        {
            playerHurtLeft=Mathf.Max(0,playerHurtLeft-delta);
            for(int i=damageNumbers.Count-1;i>=0;i--){damageNumbers[i].age+=delta;if(damageNumbers[i].age>=DamageNumberSeconds)damageNumbers.RemoveAt(i);}
            for(int i=rings.Count-1;i>=0;i--) {
                var ring=rings[i];ring.age+=delta;
                if(ring.age>=.14f||!ring.visual){DestroyFeel(ring.visual);rings.RemoveAt(i);continue;}
                if(ring.target)ring.visual.transform.position=CombatFeelArt.WorldCenter(ring.target);
                float t=ring.age/.14f;ring.visual.transform.localScale=Vector3.one*ring.size*(.65f+t*.55f);
                ring.visual.color=new Color(1,1,1,1-t);
            }
            for(int i=cleaves.Count-1;i>=0;i--) {
                var effect=cleaves[i];
                if(effect.age<0) {
                    if(!effect.cast||effect.cast.PresentationCancelled){cleaves.RemoveAt(i);continue;}
                    if(effect.cast.Age<effect.cast.ImpactTime-.035f)continue;
                    effect.bounds=effect.cast.FeelPrimaryBounds;LastCleaveBounds=effect.bounds;effect.age=0;
                    var binding=arena.FeelCharacterBinding;
                    if(effect.whip) {
                        effect.whipSweep=new WhipSkillSweep(arena.Player.visual,effect.bounds,arena.Player.Position,effect.facing);
                        if(binding!=null&&WhipTrailView.Supports(binding.Attack))effect.whipTrail=new WhipTrailView(binding,binding.Attack,"Whip skill native trail");
                    }
                    else effect.sweep=new HeavyCleaveSweep(arena.Player.visual,effect.bounds,arena.Player.Position,effect.facing);
                    if(!effect.whip&&UseContinuousSwordTrail&&binding!=null&&SwordRibbonView.Supports(binding.Attack))
                        effect.ribbon=new SwordRibbonView(binding,binding.Attack,true,"Heavy cleave blade ribbon");
                    else if(!effect.whip) {
                        effect.visual=FeelSprite("Heavy cleave range trail",CombatFeelArt.Cleave[0],arena.spriteMaterial,84);
                        effect.visual.flipX=effect.facing<0;
                    }
                } else effect.age+=delta;
                if(effect.age>=(effect.whip?WhipTrailProfile.Load().skillSeconds:SwordRibbonProfile.Load().skillSweepSeconds)||effect.cast&&effect.cast.PresentationCancelled){DestroyFeel(effect.visual);effect.ribbon?.Dispose();effect.sweep?.Dispose();effect.whipSweep?.Dispose();effect.whipTrail?.Dispose();cleaves.RemoveAt(i);continue;}
                if(effect.whip){effect.whipSweep?.Sample(effect.age);effect.whipTrail?.Sample(effect.age);continue;}
                effect.sweep?.Sample(effect.age);
                if(effect.ribbon!=null){effect.ribbon.Sample(effect.age);continue;}
                int frame=Mathf.Min(3,(int)(effect.age/.045f));var sprite=CombatFeelArt.Cleave[frame];effect.visual.sprite=sprite;
                effect.visual.transform.position=effect.bounds.center;
                effect.visual.transform.localScale=new Vector3(effect.bounds.size.x/sprite.bounds.size.x,effect.bounds.size.y/sprite.bounds.size.y,1);
                effect.visual.color=new Color(1,1,1,effect.age>.12f?1-(effect.age-.12f)/.06f:1);
            }
            for(int i=deathPops.Count-1;i>=0;i--) {
                var pop=deathPops[i];pop.age+=delta;
                if(pop.age>=.48f){DisposeDeath(pop);deathPops.RemoveAt(i);continue;}
                if(pop.body){pop.body.enabled=pop.age<.055f;float t=Mathf.Clamp01(pop.age/.055f);pop.body.transform.localScale=Vector3.Scale(pop.bodyScale,new Vector3(1-t*.22f,1+t*.06f,1));}
                float age=Mathf.Max(0,pop.age-.055f);
                for(int j=0;j<pop.bits.Length;j++) {
                    var bit=pop.bits[j];bit.enabled=pop.age>=.055f;
                    var at=pop.origin+pop.velocity[j]*age+Vector3.down*(6.8f*age*age);
                    if(at.y<pop.floor)at.y=pop.floor+(pop.floor-at.y)*.25f;
                    bit.transform.position=at;bit.transform.rotation=Quaternion.Euler(0,0,(j%2==0?1:-1)*age*210);
                    bit.color=new Color(.12f,.13f,.12f,Mathf.Clamp01((.48f-pop.age)/.15f));
                }
            }
        }
        // One reused style: a new GUIStyle per number per OnGUI event (Layout and Repaint) allocated native-backed styles
        // all through a fight (QA 2026-11-16). Only Repaint draws; Layout has nothing to lay out for these GUI.Label calls.
        GUIStyle damageNumberStyle;
        void DrawDamageNumbers()
        {
            if(!EffectsEnabled||!arena.ViewCamera||Event.current!=null&&Event.current.type!=EventType.Repaint)return;
            int depth=GUI.depth;GUI.depth=-100;
            var old=GUI.color;
            if(playerHurtLeft>0&&vignetteTexture) {
                var projection=arena.ViewCamera.GetComponent<CameraDisplayProjection>();
                var rect=projection&&projection.DisplayRect!=null?projection.DisplayRect():new Rect(arena.ViewCamera.pixelRect.x,Screen.height-arena.ViewCamera.pixelRect.yMax,arena.ViewCamera.pixelRect.width,arena.ViewCamera.pixelRect.height);
                GUI.color=new Color(1,1,1,PlayerHurtEnvelope);GUI.DrawTexture(rect,vignetteTexture,ScaleMode.StretchToFill,true);GUI.color=old;
            }
            foreach(var number in damageNumbers) {
                if(number.age<0)continue;
                float t=number.age/DamageNumberSeconds;
                var p=CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera,number.origin+Vector3.up*(.85f*(1-Mathf.Pow(1-t,3))));
                if(p.z<=0)continue;
                float pop=1+Mathf.Max(0,1-number.age/.09f)*.45f;
                var style=damageNumberStyle??=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
                float scale=number.boss?1.4f:1;
                style.fontSize=Mathf.RoundToInt((number.heavy?38:25)*pop*scale);
                float sway=number.lane==0?0:(number.lane%2==1?1:-1)*DamageNumberLaneSwayPixels;
                var rect=new Rect(p.x-75*scale+sway*scale,Screen.height-p.y-38*scale-number.lane*DamageNumberLanePixels*scale,150*scale,60*scale);float alpha=Mathf.Clamp01((DamageNumberSeconds-number.age)/.18f);
                style.normal.textColor=new Color(.08f,.08f,.075f,alpha);
                for(int x=-2;x<=2;x+=2)for(int y=-2;y<=2;y+=2)if(x!=0||y!=0)GUI.Label(new Rect(rect.x+x,rect.y+y,rect.width,rect.height),number.text,style);
                style.normal.textColor=number.player?new Color(1,.3f,.25f,alpha):number.heavy?new Color(1,.65f,.2f,alpha):new Color(1,1,1,alpha);
                GUI.Label(rect,number.text,style);
            }
            GUI.color=old;GUI.depth=depth;
        }
        static void DestroyFeel(SpriteRenderer view){if(view){view.enabled=false;if(Application.isPlaying)Destroy(view.gameObject);else DestroyImmediate(view.gameObject);}}
        static void DisposeDeath(DeathPop pop){DestroyFeel(pop.body);foreach(var bit in pop.bits)DestroyFeel(bit);}
        void ResetFinishers()
        {
            foreach (var fx in rapierSkillFx) if (fx) { fx.gameObject.SetActive(false); Destroy(fx.gameObject); }
            rapierSkillFx.Clear();
            foreach (var fx in hammerSkillFx) if (fx) { fx.gameObject.SetActive(false); Destroy(fx.gameObject); }
            hammerSkillFx.Clear();
            foreach (var effect in greatswordVisuals) if (effect) { effect.gameObject.SetActive(false); Destroy(effect.gameObject); }
            greatswordVisuals.Clear();
            foreach (var effect in baseSwordVisuals) if (effect) DestroyFeel(effect.GetComponent<SpriteRenderer>());
            baseSwordVisuals.Clear();
            playerHurtLeft=0;damageNumbers.Clear();foreach(var ring in rings)DestroyFeel(ring.visual);rings.Clear();
            foreach(var pop in deathPops)DisposeDeath(pop);deathPops.Clear();hiddenDeaths.Clear();
            foreach(var effect in cleaves){DestroyFeel(effect.visual);effect.ribbon?.Dispose();effect.sweep?.Dispose();effect.whipSweep?.Dispose();effect.whipTrail?.Dispose();}cleaves.Clear();heavyExecutions.Clear();heavyOrder.Clear();
        }
        void DisposeFinisherArt(){ResetFinishers();if(ringArt)Destroy(ringArt);if(inkArt)Destroy(inkArt);if(ringTexture)Destroy(ringTexture);if(inkTexture)Destroy(inkTexture);if(vignetteTexture)Destroy(vignetteTexture);if(cutLineArt)Destroy(cutLineArt);if(cutLineTexture)Destroy(cutLineTexture);}
    }
}
