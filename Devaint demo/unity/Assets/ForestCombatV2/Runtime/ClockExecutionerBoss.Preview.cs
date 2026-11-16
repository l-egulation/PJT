using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Draws the resource workbench's hanging-clock-boss-battle-v1/app.mjs preview.
    // from native parts exported by tools/combat/export_clock_boss_preview.py. The preview canvas is
    // 960x540 px with the floor at y=464, and the open 30-unit arena maps onto it 1:1 at PPU 32, so
    // every pose below reuses the preview's pixel numbers through Px(). These are preview parts, not
    // approved boss motion clips. Combat timing and hit boxes stay in ClockExecutionerBoss.cs.
    public sealed partial class ClockExecutionerBoss
    {
        public const string PreviewResources="ClockExecutionerPreview";
        const float PreviewFloor=464,DialX=480,DialY=280,MixerScale=1.8f;
        // Behind the player (sorting order 10) like the preview, which draws the hero over the boss.
        const int OrderChain=4,OrderFist=5,OrderAnchor=6,OrderBody=7,OrderDial=8,OrderFront=9,OrderEffect=40;
        sealed class PreviewClip { public Sprite[] frames;public float fps,fadeAfter,fadeDuration; }
        [Serializable] sealed class PreviewSource { public PreviewEntry[] clips;public CollapsePiece[] pieces; }
        [Serializable] sealed class PreviewEntry { public string id;public float fps,fadeAfter,fadeDuration;public PreviewFrame[] frames; }
        [Serializable] sealed class PreviewFrame { public string resource; }
        [Serializable] sealed class CollapsePiece { public string id,kind;public float x,y,w,h,vx,spin,delay; }
        sealed class PreviewEffect { public string id;public float started,x,y,scale,angle;public bool flip,realtime; }
        readonly Dictionary<string,PreviewClip> preview=new Dictionary<string,PreviewClip>();
        readonly List<SpriteRenderer> previewParts=new List<SpriteRenderer>();
        readonly List<PreviewEffect> previewEffects=new List<PreviewEffect>();
        CollapsePiece[] collapsePieces=Array.Empty<CollapsePiece>();
        // Floor-line crops of the enlarged mixer body, keyed by sprite and visible rows.
        readonly Dictionary<(Sprite,int),Sprite> floorCrops=new Dictionary<(Sprite,int),Sprite>();
        int drawn;float previewAge;ClockBossAttack previewAttack;bool collapsePlume;
        public bool PreviewArtLoaded=>preview.Count>0;
        public int VisiblePreviewParts {get;private set;}
        public int ActivePreviewEffects=>previewEffects.Count;
        // Set by ClockExecutionerEncounter: seconds since the boss is revealed; infinity once settled.
        public float IntroDropAge {get;set;}=float.PositiveInfinity;
        float diedAt=-1;

        void LoadPreview()
        {
            preview.Clear();
            var json=Resources.Load<TextAsset>(PreviewResources+"/preview");if(!json)return;
            var source=JsonUtility.FromJson<PreviewSource>(json.text);
            foreach(var entry in source.clips)
            {
                var frames=new Sprite[entry.frames.Length];
                for(int i=0;i<frames.Length;i++)frames[i]=Resources.Load<Sprite>(entry.frames[i].resource);
                if(!Array.TrueForAll(frames,f=>f)){preview.Clear();return;}
                preview[entry.id]=new PreviewClip{frames=frames,fps=entry.fps,fadeAfter=entry.fadeAfter,fadeDuration=entry.fadeDuration};
            }
            collapsePieces=source.pieces??Array.Empty<CollapsePiece>();
            // The cropped test-room renderers stay as the fallback when the preview art is missing.
            foreach(var r in new[]{visual,beam,leftFist,rightFist,mixerFace,hourHand,minuteHand})r.forceRenderingOff=true;
            // Shared kill feedback copies visual.sprite into a white silhouette; the stale crop would cover the collapse.
            visual.sprite=null;
        }
        Vector2 Px(float x,float y)=>new Vector2(RoomLeft+x/32,Floor+(PreviewFloor-y)/32);
        float ToPxX(float worldX)=>(worldX-RoomLeft)*32;
        float ToPxY(float worldY)=>PreviewFloor-(worldY-Floor)*32;
        SpriteRenderer NextPreviewPart()
        {
            if(drawn==previewParts.Count)previewParts.Add(Art("Preview art",null,OrderBody));
            var r=previewParts[drawn++];r.enabled=true;return r;
        }
        // angle follows the preview canvas: radians, clockwise on screen.
        void Paint(string id,int frame,float x,float y,int order,float depth,float angle=0,float scale=1,bool flip=false,float alpha=1,Sprite crop=null)
        {
            var clip=preview[id];var r=NextPreviewPart();
            r.sprite=crop?crop:clip.frames[Mathf.Clamp(frame,0,clip.frames.Length-1)];
            var at=Px(x,y);r.transform.position=new Vector3(at.x,at.y,-depth*.001f);
            r.transform.rotation=Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);r.transform.localScale=new Vector3(scale,scale,1);
            r.flipX=flip;r.sortingOrder=order;r.color=new Color(1,1,1,alpha);
        }
        void PreviewLine(float x0,float y0,float x1,float y1,float width,Color color,int order,float depth)
        {
            var r=NextPreviewPart();r.sprite=arena.TechnicalSprite;
            Vector2 a=Px(x0,y0),b=Px(x1,y1);var mid=(a+b)*.5f;
            r.transform.position=new Vector3(mid.x,mid.y,-depth*.001f);
            r.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            r.transform.localScale=new Vector3(Vector2.Distance(a,b)+width/32,width/32,1);
            r.flipX=false;r.sortingOrder=order;r.color=color;
        }
        static Color Hex(string hex,float alpha=1){ColorUtility.TryParseHtmlString(hex,out var c);c.a=alpha;return c;}
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        // Part canvases share the dial centre as pivot; the preview draws them at (288+x,35+y).
        void Part(string id,float x,float y,int order,float depth,float angle=0,float alpha=1)
        {
            Paint("part_"+id,0,DialX+x,DialY+y,order,depth,angle,1,false,alpha);
        }
        // Effects run on the combat clock; the entry pauses that clock, so its effects use real time.
        void SpawnPreviewEffect(string id,float x,float y,float scale=1,bool flip=false,float angle=0)
        {
            if(!preview.ContainsKey("fx_"+id))return;bool realtime=!float.IsPositiveInfinity(IntroDropAge);
            previewEffects.Add(new PreviewEffect{id="fx_"+id,started=realtime?Time.unscaledTime:arena.Now,x=x,y=y,scale=scale,angle=angle,flip=flip,realtime=realtime});
        }

        // Long fist: both small fists leave through their sides (0.5 s), stay out for the three strikes,
        // and return over 0.45 s.
        Vector2 SideOffsetPx(int side,float handBob)
        {
            float x=0,y=handBob;
            if(CurrentAttack==ClockBossAttack.LongFist)
            {
                float a=Age,leave=a<.5f?Mathf.Clamp01(a/.5f):a<LongFistSequenceEnd?1:1-Mathf.Clamp01((a-LongFistSequenceEnd)/.45f);
                x=side*470*leave;y-=Mathf.Round(30*Mathf.Sin(leave*Mathf.PI));
            }
            return new Vector2(x,y);
        }

        void PresentPreview(float age,bool alive)
        {
            drawn=0;
            TriggerPreviewEvents(age);
            if(Dead)DrawCollapse();
            else if(PresentationVisible&&IntroDropAge>=0)DrawAssembly(age);
            DrawEffects();
            for(int i=drawn;i<previewParts.Count;i++)previewParts[i].enabled=false;
            VisiblePreviewParts=drawn;
        }
        void TriggerPreviewEvents(float age)
        {
            float before=previewAttack==CurrentAttack?previewAge:-1;
            bool Crossed(float at)=>before<at&&age>=at;
            if(CurrentAttack==ClockBossAttack.LongFist)
            {
                for(int i=0;i<AnnouncedFistStrikes;i++)
                {
                    // Dust where each fist breaks out, and where a downward fist slams the floor.
                    float at=LongFistExit+i*LongFistInterval;var strike=strikes[i];float col=ToPxX(strike.column);
                    if(Crossed(at+LongFistPrepare))
                    {
                        if(strike.origin==FistOrigin.Left)SpawnPreviewEffect("dust",110,PreviewFloor,2);
                        else if(strike.origin==FistOrigin.Right)SpawnPreviewEffect("dust",850,PreviewFloor,2,true);
                        else if(strike.origin==FistOrigin.Bottom)SpawnPreviewEffect("dust",col,PreviewFloor,2);
                    }
                    if(Crossed(at+LongFistExtendEnd))
                    {
                        // The fist slams the far edge: dust and debris burst away from that edge, and the view shakes.
                        float mid=PreviewFloor-LongFistHeight*16;
                        Vector3 hit=strike.origin==FistOrigin.Left?new Vector3(960,mid,-Mathf.PI/2):strike.origin==FistOrigin.Right?new Vector3(0,mid,Mathf.PI/2)
                            :strike.origin==FistOrigin.Top?new Vector3(col,PreviewFloor,0):new Vector3(col,0,Mathf.PI);
                        SpawnPreviewEffect("dust",hit.x,hit.y,2,false,hit.z);SpawnPreviewEffect("needleImpact",hit.x,hit.y,1.5f,false,hit.z);
                        Shake(.35f,.25f);
                    }
                }
            }
            else if(CurrentAttack==ClockBossAttack.ChainSlash)ChainImpacts(before,age);
            else if(CurrentAttack==ClockBossAttack.Mixer)
            {
                if(Crossed(MixerTravelEnd))SpawnPreviewEffect("dissolve",ToPxX(MixerCenter.x),ToPxY(MixerCenter.y),2);
            }
            previewAttack=CurrentAttack;previewAge=age;
            foreach(var h in hazards)
                if(!h.landedFx&&arena.Now>=h.impact)
                {h.landedFx=true;float x=ToPxX(h.box.center.x);SpawnPreviewEffect("needleImpact",x,PreviewFloor);SpawnPreviewEffect("dust",x,PreviewFloor);}
        }

        void DrawAssembly(float age)
        {
            // Each part bobs (idle) or shakes (roar) on its own slight delay so the mass moves together loosely.
            bool roar=IntroDropAge<float.PositiveInfinity;
            float Move(float lag,float amplitude){if(roar)return IntroShake(lag)*amplitude/3;float t=Mathf.Floor((arena.Now-lag)/.05f)*.05f;return -Mathf.Round(amplitude*(1-Mathf.Cos(t*Mathf.PI*2/1.2f))/2);}
            float bodyBob=Move(0,5),handBob=roar?-Move(.04f,3):Move(.15f,5),inkBob=Move(.08f,6),dialBob=Move(.03f,5),handsBob=Move(.05f,5);
            float chainBob=Move(.1f,3),centerBob=Move(.05f,3),beamBob=roar?Move(.1f,1):0;
            float rise=IntroRise();
            bool mixing=CurrentAttack==ClockBossAttack.Mixer&&age<MixerTravelEnd+MixerReturn;
            if(!mixing)Part("chain_center",0,rise+centerBob,OrderChain,0);
            foreach(int side in new[]{-1,1})
            {
                var o=SideOffsetPx(side,handBob);string name=side<0?"left":"right";
                if(Mathf.Abs(o.x)>.1f)Tether(side,o.x,o.y+rise);else Part("chain_"+name,0,rise+chainBob,OrderChain,1);
                Part("fist_"+name,o.x,o.y+rise,OrderFist,0);
            }
            Part("ceiling",0,rise+beamBob,OrderAnchor,0);
            DrawNeedles();DrawChainSlashArt(age);
            if(mixing){DrawMixer(age);return;}
            DrawGiant(age);
            float y=bodyBob+rise;
            Part("body",0,y,OrderBody,0);Part("ink_lower",0,inkBob+rise,OrderBody,1);Part("dial",0,dialBob+rise,OrderDial,0);
            ClockHands(DialX,DialY+handsBob+rise,1,CurrentAttack==ClockBossAttack.Needles?age*.7f:-1);
            if(FightElapsed>=120&&FightElapsed<122)Ring(DialX,DialY+y,65+(FightElapsed-120)*55,"#B66C4D",3,OrderDial,3);
        }
        void ClockHands(float x,float y,float scale,float hourOverride)
        {
            float minute=FightElapsed/240*Mathf.PI*2,hour=hourOverride>=0?hourOverride:minute*.15f;
            Paint("part_hand_minute",0,x,y,OrderDial,1,minute,scale);
            Paint("part_hand_hour",0,x,y,OrderDial,2,hour,scale);
            Paint("part_hand_hub",0,x,y,OrderDial,3,0,scale);
        }
        void Ring(float x,float y,float r,string color,float width,int order,float depth)
        {
            for(int i=0;i<60;i++)
            {
                float a=i*Mathf.PI/30,b=(i+1)*Mathf.PI/30;
                PreviewLine(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r,width,Hex(color),order,depth);
            }
        }
        // Rigid 13 px links from the fixed ceiling anchor; only their count and path change.
        void Tether(int side,float dx,float dy)
        {
            float ax=side<0?401:559,ay=111,bx=(side<0?376:584)+dx,by=298+dy;
            float distance=Mathf.Max(.01f,Mathf.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay)));int count=Mathf.Max(1,Mathf.CeilToInt(distance/13));
            float ux=(bx-ax)/distance,uy=(by-ay)/distance,angle=Mathf.Atan2(-ux,uy);
            for(int i=count;i>=0;i--)
            {
                float d=Mathf.Min(distance,i*13);
                Paint(i%2==0?"chainLink_wide":"chainLink_narrow",0,ax+ux*d,ay+uy*d,OrderChain,2+i*.01f,angle);
            }
        }
        // The preview giant fist is 176 px tall; it is drawn at the 4-unit hit size (x0.73) and turned to
        // travel from either side, the top edge or the floor.
        void DrawGiant(float age)
        {
            if(CurrentAttack!=ClockBossAttack.LongFist)return;
            float s=LongFistHeight*32/176;
            for(int i=0;i<AnnouncedFistStrikes;i++)
            {
                float ext=StrikeExtension(StrikeAge(i,age));if(ext<0)continue;
                var strike=strikes[i];bool across=Horizontal(strike.origin);
                float reach=Mathf.Clamp01(ext)*(across?LongFistLength:ArenaTop)*32;if(reach<1)continue;
                float col=ToPxX(strike.column),mid=PreviewFloor-LongFistHeight*16;
                // Travel direction on the preview canvas (y down), its normal, tip and art rotation.
                Vector2 d=strike.origin==FistOrigin.Left?Vector2.right:strike.origin==FistOrigin.Right?Vector2.left:strike.origin==FistOrigin.Top?Vector2.up:Vector2.down;
                Vector2 tip=strike.origin==FistOrigin.Left?new Vector2(reach,mid):strike.origin==FistOrigin.Right?new Vector2(960-reach,mid)
                    :strike.origin==FistOrigin.Top?new Vector2(col,reach):new Vector2(col,PreviewFloor-reach);
                bool flip=strike.origin==FistOrigin.Right;
                float angle=strike.origin==FistOrigin.Top?Mathf.PI/2:strike.origin==FistOrigin.Bottom?-Mathf.PI/2:0;
                Vector2 n=across?Vector2.up:Vector2.right;float rear=-292+38;
                // Tiles start exactly at the origin edge so a fist rising from the floor never shows arm below it.
                for(float x=-reach/s;x<rear;x+=72){var at=tip+d*(x*s);Paint("armTile",0,at.x,at.y,OrderAnchor,1,angle,s,flip);}
                Paint("giantFist",0,tip.x,tip.y,OrderAnchor,2,angle,s,flip);
                if(StrikeAge(i,age)<LongFistExtendEnd)
                    for(int k=0;k<5;k++)
                    {
                        Vector2 side=n*(-78+k*32)*s,a=tip-d*((95+k*17)*s)+side,b=tip-d*(32*s)+side;
                        PreviewLine(a.x,a.y,b.x,b.y,3,Hex("#D6D0C7"),OrderAnchor,3);
                    }
            }
        }
        // The needle art is 480 px tall (82 px wide at the guard). The tip follows the bottom of its 12-unit
        // hit column down to the floor, then at impact stabs NeedleEmbed px into it within NeedleStabSeconds
        // (fast, then slowing), drives a little deeper, springs back and quivers sideways, and stays stuck
        // while it fades. Rows below the floor are cut, so the lower blade disappears into the ground, and
        // short ink cracks mark where it went in.
        const float NeedleEmbed=160,NeedleStabSeconds=.06f,NeedleThrust=12,NeedleThrustSeconds=.08f,NeedleQuiver=2;
        void DrawNeedles()
        {
            foreach(var h in hazards)
            {
                float fall=.3f/h.speed;if(arena.Now<h.impact-fall)continue;
                float t=Mathf.Clamp01((arena.Now-h.impact+fall)/fall),tip=PreviewFloor-384*(1-t),entry=ToPxX(h.box.center.x),x=entry;
                float alpha=arena.Now<h.impact?1:Mathf.Clamp01(1-(arena.Now-h.impact)*h.speed/.5f);
                if(arena.Now>=h.impact)
                {
                    float a=(arena.Now-h.impact)*h.speed,s=Mathf.Clamp01(a/NeedleStabSeconds),after=a-NeedleStabSeconds;
                    tip+=Mathf.Round(NeedleEmbed*(1-(1-s)*(1-s)));
                    if(after>0&&after<NeedleThrustSeconds)tip+=Mathf.Round(NeedleThrust*Mathf.Sin(after/NeedleThrustSeconds*Mathf.PI));
                    if(after>0)x+=Mathf.Round(NeedleQuiver*Mathf.Sin(after*70)*Mathf.Exp(-after*14));
                }
                FloorClipped("needle",x,tip,1,1,alpha);
                if(arena.Now<h.impact){PreviewLine(x-50,tip-420,x-50,tip-320,3,Hex("#D6D0C7"),OrderFront,2);PreviewLine(x+51,tip-410,x+51,tip-315,2,Hex("#9C968D"),OrderFront,2);}
                else
                {
                    // Cracks open from the entry point along the floor top.
                    float open=Mathf.Clamp01((arena.Now-h.impact)*h.speed/NeedleThrustSeconds);var ink=Hex("#171716",alpha);
                    PreviewLine(entry-6,PreviewFloor,entry-6-14*open,PreviewFloor+3,2,ink,OrderFront,2);
                    PreviewLine(entry+6,PreviewFloor,entry+6+18*open,PreviewFloor+4,2,ink,OrderFront,2);
                    PreviewLine(entry-4,PreviewFloor+2,entry-4-6*open,PreviewFloor+10,2,ink,OrderFront,2);
                }
            }
        }
        // Mixer: body leaves upward (0.35 s), gap, enlarged clock rises from the floor at its start (0.5 s),
        // travels with spinning hands, sinks back (0.45 s), then the body drops back from above.
        void DrawMixer(float age)
        {
            // The clock sinks where its lunge ended.
            float sx=ToPxX(MixerPosition(0).x),ex=ToPxX(MixerCenter.x);
            float CentreY(float scale)=>PreviewFloor-6*32*(scale/MixerScale);
            if(age<.35f){float p=Mathf.Clamp01(age/.35f);NormalBody(DialY-560*p*p);return;}
            if(age<.5f)return;
            if(age<MixerPrepare){float p=Ease((age-.5f)/.5f),s=.65f+(MixerScale-.65f)*p;Foreground(sx,CentreY(s)+160*(1-p),s,age,false);return;}
            if(age<MixerTravelEnd){Foreground(ToPxX(MixerCenter.x),ToPxY(MixerCenter.y),MixerScale,age,true);return;}
            if(age<MixerTravelEnd+.45f){float p=Ease((age-MixerTravelEnd)/.45f),s=MixerScale-.5f*p;Foreground(ex,CentreY(s)+380*p,s,age,false);return;}
            if(age<MixerTravelEnd+.55f)return;
            NormalBody(DialY-560*(1-Ease((age-MixerTravelEnd-.55f)/.45f)));
        }
        void NormalBody(float y)
        {
            Paint("part_body_merged",0,DialX,y,OrderBody,0);Paint("part_dial",0,DialX,y,OrderDial,0);
            ClockHands(DialX,y,1,-1);
        }
        // The preview clips the enlarged clock at the floor line. A SpriteMask would fight the village
        // terrain's stencil, so the body sprite itself is cut to the rows above the floor.
        void FloorClipped(string id,float x,float y,float scale,float depth,float alpha)
        {
            var full=preview[id].frames[0];float h=full.rect.height,top=y-(h-full.pivot.y)*scale;
            int rows=Mathf.Clamp(Mathf.FloorToInt((PreviewFloor-top)/scale),0,(int)h);if(rows<=0)return;
            Sprite crop=null;
            if(rows<h&&!floorCrops.TryGetValue((full,rows),out crop))
            {
                var r=full.rect;crop=UnityEngine.Sprite.Create(full.texture,new Rect(r.x,r.y+h-rows,r.width,rows),
                    new Vector2(full.pivot.x/r.width,(full.pivot.y-(h-rows))/rows),full.pixelsPerUnit,0,SpriteMeshType.FullRect);
                floorCrops[(full,rows)]=crop;ownedSprites.Add(crop);
            }
            Paint(id,0,x,y,OrderFront,depth,0,scale,false,alpha,crop);
        }
        void Foreground(float x,float y,float scale,float age,bool spinning)
        {
            float k=scale/MixerScale;
            FloorClipped("foregroundBody",x,y,k,0,1);
            float accel=Mathf.Clamp01((age-.66f)/.34f),turn=spinning?(age-MixerPrepare)*44+3:accel*3;
            foreach(var (id,angle,length,sign) in new[]{("minute",turn,185f,1f),("hour",-turn*1.31f+2.1f,148f,-1f)})
            {
                if(spinning)for(int trail=10;trail>=1;trail--)
                {
                    float a=angle-trail*.14f*sign,b=a-.09f*sign,r=length*k;
                    PreviewLine(x-Mathf.Sin(a)*r,y+Mathf.Cos(a)*r,x-Mathf.Sin(b)*r,y+Mathf.Cos(b)*r,trail<3?3:2,
                        Hex(trail<3?"#D6D0C7":"#9C968D",(1-trail/12f)*.65f),OrderFront,1);
                }
                Paint("blade_"+id,0,x,y,OrderFront,2,angle,k);
            }
            Paint("bladeHub",0,x,y,OrderFront,3,0,k);
        }

        // Entry: the assembly drops from above for 0.5 s, bounces on impact and shakes.
        float IntroRise()
        {
            float a=IntroDropAge;if(float.IsPositiveInfinity(a)||RoarOnly&&a>=.5f)return 0;
            if(a<.5f){float d=Mathf.Clamp01(a/.5f);return -490*(1-d*d*d);}
            a-=.5f;return 16*Mathf.Sin(a*16)*Mathf.Exp(-a*5);
        }
        // Roar: body and fists shake against each other for 1.3 s after the impact.
        float IntroShake(float lag=0){float a=IntroDropAge-.5f-lag;return a<0||a>1.3f?0:Mathf.Round(Mathf.Sin(a*34)*3*(1-a/1.3f));}
        internal void AdvancePreviewIntro(float before,float after)
        {
            if(PreviewArtLoaded&&before<.5f&&after>=.5f){SpawnPreviewEffect("dust",400,PreviewFloor,2);SpawnPreviewEffect("dust",560,PreviewFloor,2,true);Shake(.4f,.35f);}
            if(before<.6f&&after>=.6f)Shake(.12f,1.2f); // the roar rumbles the view
        }

        // Death: the whole mass leans for 0.66 s, loses support and the pieces land; a plume hides the rest.
        void DrawCollapse()
        {
            if(diedAt<0)return;
            float age=arena.Now-diedAt,lean=Mathf.Pow(Mathf.Clamp01(age/.66f),2)*.17f;
            Part("ceiling",0,0,OrderAnchor,0,0,Mathf.Clamp01(1-age/3));
            if(age>=.96f&&!collapsePlume){collapsePlume=true;SpawnPreviewEffect("collapsePlume",490,PreviewFloor);}
            foreach(var p in collapsePieces)
            {
                float t=Mathf.Max(0,age-p.delay),x=p.vx*Mathf.Min(t,.7f),angle=p.spin*Mathf.Min(.65f,Mathf.Max(0,t-.18f));
                float cx=288+p.x+p.w/2+x,cy=35+p.y+p.h/2;
                float centerY=400+(cx-480)*Mathf.Sin(lean)+(cy-400)*Mathf.Cos(lean);
                float extent=Mathf.Abs(Mathf.Sin(lean+angle))*p.w/2+Mathf.Abs(Mathf.Cos(lean+angle))*p.h/2;
                float drop=Mathf.Max(0,(PreviewFloor-centerY-extent)/Mathf.Cos(lean));
                float descent=Mathf.Min(drop,1050*t*t),land=Mathf.Max(0,t-Mathf.Sqrt(drop/1050));
                float y=cy+descent-(land<.2f?Mathf.Sin(land/.2f*Mathf.PI)*3:0);
                // The preview rotates the whole group by lean around (480,400), then each piece around its centre.
                float rx=cx-480,ry=y-400,c=Mathf.Cos(lean),s=Mathf.Sin(lean);
                Paint(p.id,0,480+rx*c-ry*s,400+rx*s+ry*c,OrderBody,p.kind=="fist"?1:0,lean+angle);
            }
        }
        void DrawEffects()
        {
            for(int i=previewEffects.Count-1;i>=0;i--)
            {
                var e=previewEffects[i];var clip=preview[e.id];float t=(e.realtime?Time.unscaledTime:arena.Now)-e.started;int frame=Mathf.FloorToInt(t*clip.fps);
                if(frame>=clip.frames.Length){previewEffects.RemoveAt(i);continue;}
                float alpha=clip.fadeDuration>0?Mathf.Clamp01(1-(t-clip.fadeAfter)/clip.fadeDuration):1;
                Paint(e.id,frame,e.x,e.y,OrderEffect,i,e.angle,e.scale,e.flip,alpha);
            }
        }
    }
}
