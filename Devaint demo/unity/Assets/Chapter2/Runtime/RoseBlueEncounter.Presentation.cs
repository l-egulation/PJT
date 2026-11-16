using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Chapter2.RoseBlue;
using UnityEngine;

namespace Deviant.Chapter2
{
    // Rendering only. PatternClock, projectile bodies, hitboxes and damage stay authoritative.
    public sealed partial class RoseBlueEncounter
    {
        [Serializable] sealed class Phase1Art
        {
            public int inkWidthPx,inkHeightPx,inkGroundY,inkFrameRate,inkFrames,blueReleaseIndex,pixelsPerUnit;
            public int[] blueKeys,blueDurationsMs;
        }
        // Dark-fantasy VFX set (resources monsters/stage-02/rose-blue-boss-vfx-20261105/runtime). RoseBlueVfxSheets.json copies its
        // sheets.json: cells run top-left row-major, the pivot is in pixels from the cell's top-left, 32 px = 1u.
        // scale: drawn size multiplier for sheets kept at half resolution (0 or missing means 1).
        [Serializable] sealed class VfxSheet{public string name;public int columns,rows,frames,cellWidth,cellHeight;public float pivotX,pivotY,scale;public float[] durationsMs;public bool loop;}
        [Serializable] sealed class VfxSheetTable{public VfxSheet[] sheets;}
        sealed class ArtFx
        {
            public string Kind,Sheet;public SpriteRenderer View;public float Age,Life;public Vector2 At,Size,Velocity,Offset;public int Index;public bool Flip;public RoseBlueActor Follow;
            public VolleyShot[] Shots;public SpriteRenderer[] Parts; // swap press controllers; their sprites are children of View
        }
        readonly Dictionary<string,Sprite[]> phase1Sprites=new Dictionary<string,Sprite[]>();
        readonly Dictionary<string,Sprite[]> vfxSprites=new Dictionary<string,Sprite[]>();
        readonly Dictionary<string,VfxSheet> vfxSheets=new Dictionary<string,VfxSheet>();
        // Looping tells (trap, platform and sequence warnings) restart from their own start time.
        readonly Dictionary<SpriteRenderer,(string sheet,float started)> loopingViews=new Dictionary<SpriteRenderer,(string,float)>();
        static bool RoseFlightKind(string kind)=>kind=="straight"||kind=="stun"||kind=="duo-homing";
        static bool Phase3ArrowKind(string kind)=>kind=="bounce"||kind=="fan-arrow"||kind=="rain";
        readonly List<ArtFx> artFx=new List<ArtFx>();
        Phase1Art phase1Art;SpriteRenderer rootArt,chargeArt;float rootArtAge,rootReleaseAge;bool rootShown;
        bool roseWasVisible,blueWasVisible;Vector2 lastRoseAt,lastBlueAt;Sprite lastRoseSprite,lastBlueSprite;
        Vector3 lastRoseScale,lastBlueScale;bool lastRoseFlip,lastBlueFlip;
        Sprite[] Art(string name,int cols=1,int rows=1,int count=1,Vector2? pivot=null)
        {
            if(phase1Sprites.TryGetValue(name,out var cached))return cached;
            var tex=Resources.Load<Texture2D>("RoseBluePhase1/"+name);
            if(!tex)throw new InvalidOperationException("Deploy approved phase1 artwork: "+name);
            int w=tex.width/cols,h=tex.height/rows;var frames=new Sprite[count];
            for(int i=0;i<count;i++){frames[i]=Sprite.Create(tex,new Rect(i%cols*w,tex.height-(i/cols+1)*h,w,h),pivot??new Vector2(.5f,.5f),32,0,SpriteMeshType.FullRect);ownedSprites.Add(frames[i]);}
            phase1Sprites[name]=frames;return frames;
        }
        Sprite[] Vfx(string name)
        {
            if(vfxSprites.TryGetValue(name,out var cached))return cached;
            if(!vfxSheets.TryGetValue(name,out var s))throw new InvalidOperationException("Unknown Rose/Blue VFX sheet: "+name);
            var tex=Resources.Load<Texture2D>("RoseBlueVfx/"+name);
            if(!tex)throw new InvalidOperationException("Deploy Rose/Blue VFX: "+name);
            var frames=new Sprite[s.frames];
            for(int i=0;i<frames.Length;i++)
            {
                int col=i%s.columns,row=i/s.columns;
                frames[i]=Sprite.Create(tex,new Rect(col*s.cellWidth,tex.height-(row+1)*s.cellHeight,s.cellWidth,s.cellHeight),new Vector2(s.pivotX/s.cellWidth,1-s.pivotY/s.cellHeight),32,0,SpriteMeshType.FullRect);
                ownedSprites.Add(frames[i]);
            }
            vfxSprites[name]=frames;return frames;
        }
        float VfxSeconds(string name)=>vfxSheets[name].durationsMs.Sum()/1000f;
        // The frame shown at age: a one-shot returns null after its last frame, a loop wraps around.
        Sprite VfxFrame(string name,float age)
        {
            var s=vfxSheets[name];var frames=Vfx(name);float t=age*1000;
            if(s.loop)t=Mathf.Repeat(t,s.durationsMs.Sum());
            for(int i=0;i<frames.Length;i++){if(t<s.durationsMs[i])return frames[i];t-=s.durationsMs[i];}
            return null;
        }
        void InitializePhase1Art()
        {
            var config=Resources.Load<TextAsset>("RoseBluePhase1Art");if(!config)throw new InvalidOperationException("Missing phase1 art binding");
            phase1Art=JsonUtility.FromJson<Phase1Art>(config.text);
            var sheets=Resources.Load<TextAsset>("RoseBlueVfxSheets");if(!sheets)throw new InvalidOperationException("Missing Rose/Blue VFX sheet binding");
            foreach(var s in JsonUtility.FromJson<VfxSheetTable>(sheets.text).sheets)vfxSheets[s.name]=s;
            // VFX sheets are cut on first use or by WarmArtNear, not when chapter 2 is built.
            Art("blue-palm",4,4,16,new Vector2(124f/320,1-55f/200));Art("blue-orb",4,1,4);
            LoadBossMotions();ShowMotion(Rose,"rose-breath",0);ShowMotion(Blue,"blue-breath",0);
            // Ink crystal machine (resources monsters/stage-02/rose-blue-machine-crystal-20261112/runtime). Its sheets share the VFX sheet
            // format; the 96x128 cell is drawn 1:1 and its pivot is the centre of the 2x3u collider, so the glow and the hovering tip
            // reach past the collider while the hit area stays exactly the data's machine box.
            var crystal=Resources.Load<TextAsset>("RoseBlueMachineCrystal");if(!crystal)throw new InvalidOperationException("Missing ink crystal machine sheet binding");
            foreach(var s in JsonUtility.FromJson<VfxSheetTable>(crystal.text).sheets){vfxSheets[s.name]=s;_=Vfx(s.name);}
            Machine.visual.sprite=VfxFrame(MachineIdleSheet(0),0);Machine.visual.color=Color.white;Machine.visual.transform.localScale=Vector3.one;
            machineSeenHealth=Machine.health;machineHitAt=float.NegativeInfinity;
        }
        float machineSeenHealth,machineHitAt;int machineHitStage;
        // Presentation-only wear steps: the crystal splits once under two thirds of its health and again under one third.
        int MachineDamageStage(){float f=Machine.health/(float)Tuning.MachineHp;return f<=1/3f?2:f<=2/3f?1:0;}
        static string MachineIdleSheet(int stage)=>stage==0?"machine-crystal-idle":"machine-crystal-damage"+stage;
        // Idle loop of the current wear step; each accepted hit plays that step's 3-frame white flash once.
        void StepMachineArt()
        {
            if(!Machine||!Machine.visual)return;
            int stage=MachineDamageStage();
            if(Machine.health<machineSeenHealth){machineHitAt=arena.Now;machineHitStage=stage;}
            machineSeenHealth=Machine.health;
            var hit=arena.Now-machineHitAt<VfxSeconds("machine-crystal-hit0")?VfxFrame("machine-crystal-hit"+machineHitStage,arena.Now-machineHitAt):null;
            Machine.visual.sprite=hit?hit:VfxFrame(MachineIdleSheet(stage),arena.Now);
        }
        void ApplyStanding(RoseBlueActor actor,string name)
        {
            var s=Art(name)[0];actor.visual.sprite=s;actor.visual.color=Color.white;actor.visual.transform.localPosition=new Vector3(0,actor.size.y/2,0);
            actor.visual.transform.localScale=Vector3.one*(actor.size.y/s.bounds.size.y);
        }
        SpriteRenderer ArtView(string label,Sprite sprite,Vector2 at,int order=23)
        {
            var go=new GameObject("Phase1 art / "+label);go.transform.SetParent(transform,false);go.transform.position=at;
            var v=go.AddComponent<SpriteRenderer>();v.sprite=sprite;v.sortingOrder=order;v.sharedMaterial=arena.spriteMaterial;return v;
        }
        // Plays one VFX sheet once at a world point. With follow, the effect keeps its offset from that actor.
        ArtFx StartSheetFx(string sheet,Vector2 at,float scale=1,bool flipX=false,int order=23,RoseBlueActor follow=null,string label=null)
        {
            var v=ArtView(label??sheet,VfxFrame(sheet,0),at,order);v.transform.localScale=Vector3.one*scale*(vfxSheets[sheet].scale>0?vfxSheets[sheet].scale:1);v.flipX=flipX;
            var fx=new ArtFx{Kind="sheet",Sheet=sheet,View=v,At=at,Life=VfxSeconds(sheet),Follow=follow,Offset=follow?at-follow.Position:Vector2.zero};
            artFx.Add(fx);return fx;
        }
        void StopFx(ArtFx fx){if(fx!=null)fx.Life=0;}
        // Phase2 swap press, Rose's side: no magic, just her bow. Three marker arrows plant on the final border during the tell, then a 70-arrow volley
        // drops in from above her wall along the advancing front and stands as a palisade until the press ends (confirmed rain arrow and landing sheets,
        // 1.6x). Same shots as the approved HTML simulation; the right-hand wall is its mirror. The flank box stays authoritative and unseen.
        sealed class VolleyShot{public float t0,x,angle;public bool marker,landed;public SpriteRenderer view;}
        ArtFx StartRoseVolley(Vector2 wall,bool fromLeft)
        {
            var fx=new ArtFx{Kind="volley",View=ArtView("rose volley",null,wall),At=wall,Flip=!fromLeft,Life=2f};
            float W=(float)Tuning.SwapWidth;var shots=new List<VolleyShot>();
            for(int i=0;i<3;i++)shots.Add(new VolleyShot{t0=.1f+i*.1f,x=W-.3f+i*.3f,angle=62+i*6,marker=true});
            for(int i=0;i<70;i++)
            {
                float land=.55f+.85f*((i+Hash(i,1)*.8f)/70),front=VolleyFront(land),x=Hash(i,2)<.7f?Mathf.Max(.3f,front-Hash(i,3)*2.2f):.3f+Hash(i,3)*Mathf.Max(0,front-.6f);
                shots.Add(new VolleyShot{t0=land-.18f,x=x,angle=58+Hash(i,4)*18});
            }
            foreach(var s in shots){s.view=new GameObject("Rose volley arrow").AddComponent<SpriteRenderer>();s.view.transform.SetParent(fx.View.transform,false);
                s.view.sharedMaterial=arena.spriteMaterial;s.view.transform.localScale=Vector3.one*1.6f;s.view.enabled=false;}
            fx.Shots=shots.ToArray();artFx.Add(fx);return fx;
        }
        static float VolleyFront(float t)=>(float)Tuning.SwapWidth*Mathf.Clamp01((t-(float)Tuning.SwapTell)/(float)Tuning.SwapExpandSeconds);
        void StepRoseVolley(ArtFx fx)
        {
            float dir=fx.Flip?-1:1,span=(float)Tuning.SwapHeight+1.5f,fade=1-Mathf.Clamp01((fx.Age-(float)Tuning.SwapAreaEnd)/.6f);
            foreach(var s in fx.Shots)
            {
                float age=fx.Age-s.t0,r=s.angle*Mathf.Deg2Rad,dx=Mathf.Cos(r),dy=Mathf.Sin(r);
                // Travelling down and away from the wall; a leftward arrow is the right-facing art turned 180 degrees with flipY.
                float rot=fx.Flip?-(180-s.angle):-s.angle;var land=new Vector2(fx.At.x+dir*s.x,Stage.Floor);
                s.view.enabled=age>=0&&(age<.18f||fade>0);if(!s.view.enabled)continue;
                s.view.transform.rotation=Quaternion.Euler(0,0,rot);s.view.flipY=fx.Flip;
                if(age<.18f){float u=age/.18f;s.view.sprite=VfxFrame("rose-rain-arrow-flight",age);s.view.sortingOrder=AbovePlayer(2);
                    s.view.transform.position=land+new Vector2(-dir*dx*span/dy*(1-u),span*(1-u)+.3f);continue;}
                if(!s.landed){s.landed=true;StartSheetFx("rose-rain-impact",land,1.6f,false,AbovePlayer(4));}
                // Planted with its head a third of the way into the floor, fading after the press ends.
                s.view.sprite=Vfx("rose-rain-arrow-flight")[0];s.view.sortingOrder=11;s.view.color=new Color(1,1,1,(s.marker?1:.9f)*fade);
                s.view.transform.position=land+new Vector2(-dir*dx*.35f,dy*.35f);
            }
        }
        // Phase2 swap press, Blue's side: the confirmed phase3 whirlwind dressing redrawn to the press size and Blue's colours (ink-whirl, back half behind the
        // bosses, front half over the player). Its pivot follows the middle of the zone while it widens from the wall.
        ArtFx StartInkWhirl(Vector2 wall,bool fromLeft)
        {
            var fx=new ArtFx{Kind="inkwhirl",View=ArtView("ink whirl",null,wall),At=wall,Flip=!fromLeft,Life=VfxSeconds("ink-whirl-back")};
            fx.Parts=new SpriteRenderer[2];
            for(int i=0;i<2;i++){var v=new GameObject(i==0?"Ink whirl / back":"Ink whirl / front").AddComponent<SpriteRenderer>();v.transform.SetParent(fx.View.transform,false);
                v.sharedMaterial=arena.spriteMaterial;v.sortingOrder=i==0?10:AbovePlayer(3);v.transform.localScale=Vector3.one*vfxSheets["ink-whirl-back"].scale;fx.Parts[i]=v;}
            artFx.Add(fx);return fx;
        }
        void StepInkWhirl(ArtFx fx)
        {
            float w=VolleyFront(fx.Age),x=fx.At.x+(fx.Flip?-1:1)*Mathf.Max(w/2,1.2f);
            for(int i=0;i<2;i++){var frame=VfxFrame(i==0?"ink-whirl-back":"ink-whirl-front",fx.Age);var v=fx.Parts[i];v.enabled=frame;if(frame)v.sprite=frame;v.transform.position=new Vector2(x,Stage.Floor);}
        }
        // Stretch the forming animation to the configured tell and match the smaller projectile.
        ArtFx StartHomingForm(Vector2 at,Vector2 aim)
        {
            var fx=StartSheetFx("homing-form",at,.5f,false,AbovePlayer(4));
            fx.Life=(float)Tuning.HomingTell;
            fx.View.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg);fx.View.flipY=aim.x<0;return fx;
        }
        // Phase3 expanding field: the whirlwind dressing (pixelfx roseblue/aoe-whirl) starts with the tell; back half behind the bosses, front half over the player.
        void StartWhirl(Cast c,Vector2 floor)
        {
            c.WhirlFx[0]=StartSheetFx("aoe-whirl-back",floor,1,false,10);
            c.WhirlFx[1]=StartSheetFx("aoe-whirl-front",floor,1,false,AbovePlayer(3));
        }
        // ...and 60 confirmed phase3 arrows circling it from the moment it starts to expand. Each has its own height, radius, speed,
        // size and tilt so they never line up; those behind (upper half of their ellipse) are smaller, dimmer and behind the bosses.
        void StartWhirlArrows(Vector2 floor)
        {
            for(int i=0;i<WhirlArrows;i++)
            {
                var v=ArtView("whirl arrow",VfxFrame("p3-arrow-flight",0),floor);v.enabled=false;
                artFx.Add(new ArtFx{Kind="whirl",View=v,At=floor,Index=i,Life=(float)Tuning.FanExpandSeconds+.3f});
            }
        }
        const int WhirlArrows=60;
        static float Hash(int i,int k){double v=Math.Sin((i*7.31+k*10.48)*12.9898)*43758.5453;return (float)(v-Math.Floor(v));}
        void StepWhirlArrow(ArtFx fx)
        {
            int i=fx.Index;float t=fx.Age+(float)Tuning.FanTell,g=Mathf.Clamp01(fx.Age/(float)Tuning.FanExpandSeconds),end=Mathf.Clamp01((fx.Age-(float)Tuning.FanExpandSeconds)/.3f);
            float hw=(float)Tuning.FanWidth/2*g,hh=(Stage.Area.yMax-Stage.Floor)*g,yf=.04f+.92f*Hash(i,1),speed=5+4.5f*Hash(i,2);
            float a=Hash(i,3)*Mathf.PI*2+t*speed,r=hw*(.5f+.5f*yf)*(.75f+.45f*Hash(i,4))*(1+end*.8f);
            float y=hh*yf+Mathf.Sin(t*(2+3*Hash(i,5))+i)*.5f*g;bool back=Mathf.Sin(a)>0;
            var at=fx.At+new Vector2(r*Mathf.Cos(a),y+.45f*Mathf.Sin(a)*(.6f+.4f*g));
            // Travel direction along the flattened ellipse, plus each arrow's own tilt.
            float angle=Mathf.Atan2(.45f*Mathf.Cos(a),-Mathf.Sin(a)*Mathf.Max(r,.01f))*Mathf.Rad2Deg+(Hash(i,7)-.5f)*.5f*Mathf.Rad2Deg;
            fx.View.sprite=VfxFrame("p3-arrow-flight",fx.Age+i*.05f);fx.View.transform.SetPositionAndRotation(at,Quaternion.Euler(0,0,angle));
            fx.View.transform.localScale=Vector3.one*(.75f+.6f*Hash(i,6))*(back?.8f:1.1f);fx.View.flipY=Mathf.Cos(angle*Mathf.Deg2Rad)<0;
            fx.View.sortingOrder=back?11:AbovePlayer(2);fx.View.enabled=g>0;fx.View.color=new Color(1,1,1,(back?.5f:1)*(1-end));
        }
        // Homing bolt afterimages: the same art at 0.05, 0.1 and 0.15 s back along the path it actually flew.
        static readonly float[] HomingGhostDelay={.05f,.1f,.15f},HomingGhostAlpha={.35f,.2f,.1f};
        void StepHomingArt(Hazard h,bool left)
        {
            var frame=VfxFrame("homing-bolt",h.Age);h.Art.sprite=frame;h.Art.flipY=left;
            var t=h.Art.transform;h.Trail.Add((t.position,t.eulerAngles.z,arena.Now));while(h.Trail.Count>0&&arena.Now-h.Trail[0].time>.2f)h.Trail.RemoveAt(0);
            for(int i=0;i<h.Ghosts.Length;i++)
            {
                float back=HomingGhostDelay[i];int k=h.Trail.FindIndex(e=>e.time>=arena.Now-back-.005f);var g=h.Ghosts[i];
                g.enabled=k>=0&&arena.Now-h.Trail[k].time>=back-.02f;if(!g.enabled)continue;
                var e=h.Trail[k];g.sprite=frame;g.transform.SetPositionAndRotation(e.at,Quaternion.Euler(0,0,e.angle));g.flipY=Mathf.Cos(e.angle*Mathf.Deg2Rad)<0;
            }
        }
        void StartRainFx(Vector2 at,Vector2 size,float life){var v=ArtView("rain",Vfx("rose-rain-arrow-flight")[0],at);artFx.Add(new ArtFx{Kind="rain",View=v,At=at,Size=size,Life=life});}
        int AbovePlayer(int lift)=>arena.Player.visual.sortingOrder+lift;
        void ImpactArt(Hazard h)
        {
            // Projectile hits are drawn twice their sheet size; the sheets were made at the 1u arrow's scale.
            if(RoseFlightKind(h.Kind))StartSheetFx("rose-arrow-impact",h.At,2,false,AbovePlayer(4));
            else if(Phase3ArrowKind(h.Kind))StartSheetFx("p3-arrow-impact",h.At,2,false,AbovePlayer(4));
        }
        void Ghost(Sprite sprite,Vector2 at,Vector3 scale,bool flip)
        {
            if(!sprite)return;
            for(int i=0;i<3;i++){var v=ArtView("ninja afterimage",sprite,at+Vector2.right*(flip?-1:1)*i*.28f,11);v.transform.localScale=scale;v.flipX=flip;
                artFx.Add(new ArtFx{Kind="ghost",View=v,At=v.transform.position,Life=.12f,Index=i,Flip=flip});}
        }
        // The crystal swells white, flashes in its own outline and bursts into shards with an ink splash (one 14-frame sheet, 0.98 s).
        // Its pivot is the collider centre like the idle cell, and it keeps the machine's layer just behind the player.
        void BreakMachineArt()
        {
            var fx=StartSheetFx("machine-crystal-break",Machine.Center,1,false,Machine.visual.sortingOrder,null,"machine break");
            fx.View.sortingLayerID=Machine.visual.sortingLayerID;
        }
        void StepPhase1Presentation(float dt)
        {
            if(phase1Art==null)return;
            StepMachineArt();
            bool machine=Model.Phase==Phase.Machine;
            var layer=casts.LastOrDefault(c=>c.Pattern==Pattern.LayerShots&&!c.PresentationCancelled);
            if(machine&&layer!=null)
            {
                float age=(float)layer.Clock.Age;
                Rose.PresentationVisible=age<Tuning.Phase1ShotTell+(Tuning.Phase1ArrowCount-1)*Tuning.Phase1ShotInterval+.12;Blue.PresentationVisible=age<Tuning.Phase1ShotTell+.12;
                ShowRoseTripleShot(age);
                float before=phase1Art.blueDurationsMs.Take(phase1Art.blueReleaseIndex).Sum()/1000f;
                float ms=age*before/(float)Tuning.Phase1ShotTell*1000;int key=0;
                while(key<phase1Art.blueDurationsMs.Length-1&&ms>=phase1Art.blueDurationsMs[key])ms-=phase1Art.blueDurationsMs[key++];
                Blue.visual.sprite=Art("blue-palm")[key];Blue.visual.transform.position=Hand(Blue);Blue.visual.transform.localScale=Vector3.one*(Blue.size.y*32/162f);
                if(age<Tuning.Phase1ShotTell)
                {
                    if(!chargeArt)chargeArt=ArtView("charging orb",Art("blue-orb")[0],Hand(Blue));
                    chargeArt.enabled=true;chargeArt.sprite=Art("blue-orb")[(int)(age/.22f)%4];chargeArt.transform.position=Hand(Blue);
                    chargeArt.transform.localScale=Vector3.one*Mathf.Lerp(.08f,1,(float)(age/Tuning.Phase1ShotTell));
                }else if(chargeArt)chargeArt.enabled=false;
            }
            else
            {
                if(chargeArt)chargeArt.enabled=false;
                ApplyBossMotions();
            }
            if(machine)
            {
                if(roseWasVisible&&!Rose.PresentationVisible)Ghost(lastRoseSprite,lastRoseAt,lastRoseScale,lastRoseFlip);
                if(blueWasVisible&&!Blue.PresentationVisible)Ghost(lastBlueSprite,lastBlueAt,lastBlueScale,lastBlueFlip);
                // Fast horizontal arrival streak, without changing the character's proportions.
                if(layer!=null&&layer.Clock.Age<.10){float alpha=Mathf.Clamp01((float)layer.Clock.Age/.045f);Rose.visual.color=Blue.visual.color=new Color(1,1,1,alpha);}
                else Rose.visual.color=Blue.visual.color=Color.white;
            }
            roseWasVisible=machine&&Rose.PresentationVisible;blueWasVisible=machine&&Blue.PresentationVisible;
            lastRoseAt=Rose.visual.transform.position;lastBlueAt=Blue.visual.transform.position;lastRoseSprite=Rose.visual.sprite;lastBlueSprite=Blue.visual.sprite;
            lastRoseScale=Rose.visual.transform.localScale;lastBlueScale=Blue.visual.transform.localScale;lastRoseFlip=Rose.visual.flipX;lastBlueFlip=Blue.visual.flipX;
            StepRootArt(dt);
            foreach(var view in loopingViews.Keys.ToArray())
            {
                if(!view){loopingViews.Remove(view);continue;}
                var (sheet,started)=loopingViews[view];view.sprite=VfxFrame(sheet,arena.Now-started);
            }
            foreach(var fx in artFx.ToArray())
            {
                fx.Age+=dt;if(fx.Age>=fx.Life){if(fx.View)Destroy(fx.View.gameObject);artFx.Remove(fx);continue;}
                if(fx.Kind=="sheet")
                {
                    float frameAge=fx.Sheet=="homing-form"?fx.Age/fx.Life*VfxSeconds(fx.Sheet):fx.Age;
                    var frame=VfxFrame(fx.Sheet,frameAge);
                    fx.View.enabled=frame;
                    if(frame)fx.View.sprite=frame;
                    if(fx.Follow)fx.View.transform.position=fx.Follow.Position+fx.Offset;
                }
                if(fx.Kind=="ghost"){fx.View.color=new Color(1,1,1,.35f*(1-fx.Age/fx.Life)/(fx.Index+1));fx.View.transform.position=fx.At+Vector2.right*(fx.Flip?-1:1)*fx.Age*8;}
                if(fx.Kind=="rain")DrawRainArt(fx);
                if(fx.Kind=="whirl")StepWhirlArrow(fx);
                if(fx.Kind=="volley")StepRoseVolley(fx);
                if(fx.Kind=="inkwhirl")StepInkWhirl(fx);            }
            foreach(var h in hazards)
            {
                if(!h.Art)continue;
                bool left=h.Velocity.x<0;float angle=h.Velocity.sqrMagnitude>0?Mathf.Atan2(h.Velocity.y,h.Velocity.x)*Mathf.Rad2Deg:0;
                if(h.Kind=="orb")
                {
                    h.Art.sprite=VfxFrame("blue-orb-trail",h.Age);
                    // Only the trail turns with travel; the sphere child stays upright so its highlight remains top-left.
                    h.Art.transform.localRotation=Quaternion.Euler(0,0,angle);
                    var body=h.View.transform.Find("Projectile artwork / sphere");if(body)body.GetComponent<SpriteRenderer>().sprite=VfxFrame("blue-orb-body",h.Age);
                }
                else if(h.Kind=="homing")StepHomingArt(h,left);
                else if(h.Kind=="ink-ball")
                {
                    // The fireball's collider stays square and unrotated; the flame turns with travel and flips to keep its lit side on top.
                    h.Art.sprite=VfxFrame("ink-fireball",h.Age);h.Art.transform.localRotation=Quaternion.Euler(0,0,angle);h.Art.flipY=left;
                }
                else
                {
                    // Arrows: the parent body already turns with travel. A leftward arrow is the right-facing art turned 180 degrees;
                    // flipping Y keeps its lit side on top.
                    bool rose=RoseFlightKind(h.Kind);h.Art.sprite=VfxFrame(rose?"rose-arrow-flight":"p3-arrow-flight",h.Age);h.Art.flipY=left;
                    if(h.Kind=="stun")h.Art.color=new Color(1,1,1,Mathf.Clamp01((h.Life-h.Age)/.2f));
                    if(h.WhiteArt){h.WhiteArt.sprite=VfxFrame("p3-arrow-flight-white",h.Age);h.WhiteArt.flipY=left;h.WhiteArt.color=new Color(1,1,1,h.White?Mathf.Clamp01(arena.Now-h.WhiteAt):0);}
                }
            }
            StepExchangeArt();
        }
        // While a trap or dive root holds the player: rings appear (3 frames), loop (6 frames, 90 ms) and break apart when it ends.
        void StepRootArt(float dt)
        {
            bool active=arena.Now<bossRootUntil&&!arena.Player.Dead;int frame=-1;
            if(active)
            {
                if(!rootShown||rootReleaseAge>0){rootArtAge=0;rootReleaseAge=0;}
                rootShown=true;rootArtAge+=dt;
                frame=rootArtAge<.03f?0:rootArtAge<.07f?1:rootArtAge<.12f?2:3+(int)((rootArtAge-.12f)/.09f)%6;
            }
            else if(rootShown)
            {
                rootReleaseAge+=dt;frame=rootReleaseAge<.05f?9:rootReleaseAge<.1f?10:rootReleaseAge<.16f?11:-1;
                if(frame<0){rootShown=false;rootReleaseAge=0;}
            }
            if(frame<0){if(rootArt)rootArt.enabled=false;return;}
            if(!rootArt)rootArt=ArtView("root overlay",Vfx("dive-root")[0],arena.Player.Position,AbovePlayer(2));
            rootArt.enabled=true;rootArt.sprite=Vfx("dive-root")[frame];rootArt.transform.position=arena.Player.Position;
        }
        void BindProjectileArt(Hazard h)
        {
            string sheet=h.Kind=="orb"?"blue-orb-trail":h.Kind=="ink-ball"?"ink-fireball":h.Kind=="homing"?"homing-bolt":RoseFlightKind(h.Kind)?"rose-arrow-flight":Phase3ArrowKind(h.Kind)?"p3-arrow-flight":null;
            if(sheet==null)return;
            var v=h.View.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(x=>x!=h.View);if(!v)return;h.Art=v;v.color=Color.white;
            v.flipX=false;v.transform.localScale=Vector3.one;v.sprite=VfxFrame(sheet,0);
            if(h.Kind=="orb")
            {
                var body=new GameObject("Projectile artwork / sphere").AddComponent<SpriteRenderer>();body.transform.SetParent(h.View.transform,false);
                body.sprite=VfxFrame("blue-orb-body",0);body.sortingOrder=v.sortingOrder+1;body.sharedMaterial=v.sharedMaterial;
            }
            if(h.Kind=="homing")
            {
                v.transform.localScale=Vector3.one*.5f;
                h.Ghosts=new SpriteRenderer[HomingGhostDelay.Length];
                for(int i=0;i<h.Ghosts.Length;i++)
                {
                    var g=new GameObject("Projectile artwork / afterimage").AddComponent<SpriteRenderer>();g.transform.SetParent(h.View.transform,false);
                    g.transform.localScale=v.transform.localScale;g.sortingOrder=v.sortingOrder-1;g.sharedMaterial=v.sharedMaterial;g.color=new Color(1,1,1,HomingGhostAlpha[i]);g.enabled=false;h.Ghosts[i]=g;
                }
            }
            if(Phase3ArrowKind(h.Kind))
            {
                // White silhouette laid over the arrow; it fades in once the arrow whitens before its delayed blast.
                var white=new GameObject("Projectile artwork / white").AddComponent<SpriteRenderer>();white.transform.SetParent(v.transform,false);
                white.sprite=VfxFrame("p3-arrow-flight-white",0);white.sortingOrder=v.sortingOrder+1;white.sharedMaterial=v.sharedMaterial;white.color=new Color(1,1,1,0);h.WhiteArt=white;
            }
        }
        void BindZoneArt(Hazard h)
        {
            // Phase3 bounce explosion: violet and rose, starting white to continue from the whitened arrow.
            if(h.Kind=="bounce-blast"){h.View.enabled=false;StartSheetFx("p3-blast",h.At,1,false,AbovePlayer(4));return;}
            if(h.Kind=="ink-blast"){h.View.enabled=false;StartSheetFx("ink-blast",h.At,1,false,AbovePlayer(4));return;}
            // Phase3 dropped-arrow column: one full-height pillar from the planted bolt (pixelfx roseblue/p3-pillar, recoloured dark fantasy).
            if(h.Kind=="rain-blast"){h.View.enabled=false;StartSheetFx("p3-pillar",new Vector2(h.At.x,Stage.Floor),1,false,AbovePlayer(4));return;}
            // Phase3 expanding field: drawn by the whirlwind dressing and circling arrows; the zone box stays authoritative but unseen.
            if(h.Kind=="fan-area"){h.View.enabled=false;StartWhirlArrows(new Vector2(h.At.x,Stage.Floor));return;}
            // The side walls are drawn by the press sheets started at the tell.
            if(h.Kind=="swap-flank"){h.View.enabled=false;return;}
            if(Model.Phase!=Phase.Machine)return;
            // One chain binding per platform, pivoted on the platform top centre.
            if(h.Kind=="trap-flash"){h.View.enabled=false;StartSheetFx("trap-snap",new Vector2(h.At.x,h.At.y-h.Size.y/2),1,false,AbovePlayer(1));}
            if(h.Kind=="visual-rain"){h.View.enabled=false;StartRainFx(new Vector2(h.At.x,h.At.y-h.Size.y/2),h.Size,h.Life);}
        }
        void ClearPhase1Presentation()
        {
            foreach(var fx in artFx)if(fx.View){fx.View.gameObject.SetActive(false);Destroy(fx.View.gameObject);}artFx.Clear();loopingViews.Clear();
            if(rootArt)rootArt.enabled=false;rootShown=false;if(chargeArt)chargeArt.enabled=false;roseWasVisible=blueWasVisible=false;
        }
    }
}
