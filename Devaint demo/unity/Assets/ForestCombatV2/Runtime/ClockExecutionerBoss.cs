using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    public enum ClockBossAttack { None, Basic, Needles, LongFist, Slam, Mixer, ChainSlash }
    public enum FistOrigin { Left, Right, Top, Bottom }

    // The arena clock owns all combat timing, including health-driven acceleration.
    [DefaultExecutionOrder(900)]
    public sealed partial class ClockExecutionerBoss : TestEnemy
    {
        // Impacts ask the encounter for a camera shake: amplitude in units, duration in seconds.
        public event Action<float,float> CameraShake;
        // Phase one is the base fight a little eased: every pattern runs at 0.85x (longer warnings, slower attacks,
        // longer gaps). After the configured damage the boss roars once (behind the bars) and
        // phase two runs at the base speed the patterns were tuned for; its health bar turns purple. The final phase
        // roars too (playtest 2026-11-04: it had no warning) and its bar turns ink black.
        // Death fires Defeated for the outro.
        public const float DefaultPhaseOneSpeed=.85f,PhaseTwoSpeed=1;
        public float EnrageHealth=>healthEnrage?maxHealth-combatTiming.enrageDamage:maxHealth/3f;
        public bool FinalPhase {get;private set;}
        public float FinalPhaseHealth=>combatTiming.finalPhaseHealth;
        public float RecoverySeconds=>FinalPhase?combatTiming.finalPhaseRecoverySeconds:SpecialRecovery;
        float PatternGap=>FinalPhase?combatTiming.finalPhasePatternGapSeconds:2;
        bool healthEnrage;
        // Verification sets this to 1 so the timing checks read the base (phase two) numbers.
        public float phaseOneSpeed=DefaultPhaseOneSpeed;
        public static readonly Color PhaseOneBar=new Color(.86f,.2f,.17f),PhaseTwoBar=new Color(.56f,.24f,.82f),FinalPhaseBar=new Color32(28,26,22,255);
        public bool PhaseTwo {get;private set;}
        public event Action PhaseTwoStarted,FinalPhaseStarted,Defeated;
        // The phase-two roar replays the entry roar without the landing bounce.
        public bool RoarOnly {get;set;}
        void Shake(float amplitude,float duration)=>CameraShake?.Invoke(amplitude,duration);
        // Open arena (2026-10-29): the fixed 30-unit camera edge is the room edge; no side walls.
        // Body hurt box covers the drawn clock (preview body x +-2.85, fallback crop 5.5x10 centred at Floor+5.3)
        // from just above the floor, so ground attacks and feet-level area ink both reach it.
        public const float RoomWidth=Chapter1ProceduralMap.BossRoomWidth,HurtWidth=5.7f,HurtHeight=10,HurtCenterY=5.3f;
        public const float LongFistLength=RoomWidth; // horizontal fists punch all the way to the far edge; jump them
        // 4 units: a single jump (3) cannot clear it, a double jump (6) can. Vertical fists are 4-unit columns.
        public const float LongFistHeight=4,LongFistColumn=4,ArenaTop=14.5f;
        public const int LongFistStrikes=3;
        public const int NeedleCells=(int)(RoomWidth/3);
        // Preview small-fist hurt box (86x114 px) at the preview fist centres.
        static readonly Vector2 FistHurtSize=new Vector2(86/32f,114/32f);
        // 18 needles, a new warning every 0.2 s, each landing 0.8 s after its warning (the last 0.3 s is the fall).
        public const int NeedleCount=18;
        public const float NeedleWarning=.8f,NeedleInterval=.2f,SpecialRecovery=2;
        // The small fists leave first (0.5 s); then every strike boils for 1 s and punches out in 0.12 s.
        // Strikes launch 1.3 s apart, so two giant fists are never in motion together.
        public const float LongFistExit=.5f,LongFistPrepare=1,LongFistExtend=.12f,LongFistHold=.3f,LongFistRetract=.5f,LongFistInterval=1.3f;
        public const float LongFistExtendEnd=LongFistPrepare+LongFistExtend;
        public const float LongFistHoldEnd=LongFistExtendEnd+LongFistHold;
        public const float LongFistMotionEnd=LongFistHoldEnd+LongFistRetract;
        public const float LongFistSequenceEnd=LongFistExit+LongFistInterval*(LongFistStrikes-1)+LongFistMotionEnd;
        // Time since strike i's warning began.
        public static float StrikeAge(int i,float age)=>age-LongFistExit-i*LongFistInterval;
        public const float MixerPrepare=1,MixerTravel=3,MixerReturn=1;
        public const float MixerTravelEnd=MixerPrepare+MixerTravel;
        // The clock cruises at 6.5 units/s for 1.4 s, then lunges at the player in 0.25 s. The lunge only goes
        // forward and never past the travel end, so the 1-unit strip at the far edge stays safe.
        public const float MixerCruiseSpeed=6.5f,MixerCruise=1.4f,MixerLunge=.25f,MixerSafeGap=1,MixerHurtSize=8.5f;
        public const float NeedleDuration=(NeedleCount-1)*NeedleInterval+NeedleWarning+SpecialRecovery,LongFistDuration=LongFistSequenceEnd+SpecialRecovery,MixerDuration=MixerTravelEnd+MixerReturn+SpecialRecovery;
        public float RoomLeft {get;private set;}
        public float Floor {get;private set;}
        public float RoomRight=>RoomLeft+RoomWidth;
        public ClockBossAttack CurrentAttack {get;private set;}
        public ClockBossAttack LastSpecial {get;private set;}
        public bool Started {get;private set;}
        public bool PresentationVisible {get;set;}=true;
        public float FightStarted {get;private set;}
        // Enrage depends on health, never elapsed time.
        public float FightElapsed=>Started?Mathf.Max(0,arena.Now-FightStarted):0;
        public float PatternSpeed {get;private set;}=1;
        public float NextPatternAt {get;private set;}
        public float DamageObserved {get;private set;}
        public float CalibrationHealth=>FightElapsed>1?DamageObserved/FightElapsed*105:0;
        public int PendingHazards=>hazards.Count;
        public int AnnouncedNeedles {get;private set;}
        public int AttackDirection=>direction;
        public IReadOnlyList<int> NeedlePlan=>cells;
        public IReadOnlyList<Bounds> ActiveBoxes=>activeBoxes;
        public Vector2 MixerCenter {get;private set;}
        public BoxCollider2D LeftFistHurt {get;private set;}
        public BoxCollider2D RightFistHurt {get;private set;}
        public bool MixerActive=>CurrentAttack==ClockBossAttack.Mixer&&Age>=MixerPrepare&&Age<MixerTravelEnd;
        public float Age=>(arena.Now-began)*PatternSpeed;
        public override void DelayRunInkMotion(float seconds)
        { if (CurrentAttack != ClockBossAttack.None) { began += seconds; nextNeedle += seconds; nextMixerTick += seconds; } }
        public float LastPatternSpeed {get;private set;}=1;
        readonly List<Hazard> hazards=new List<Hazard>();
        readonly List<Bounds> activeBoxes=new List<Bounds>();
        readonly HashSet<int> hitTargets=new HashSet<int>();
        readonly List<SpriteRenderer> parts=new List<SpriteRenderer>();
        readonly List<Sprite> ownedSprites=new List<Sprite>();
        readonly int[] cells=new int[NeedleCount];
        struct FistStrike { public FistOrigin origin;public float column; }
        readonly FistStrike[] strikes=new FistStrike[LongFistStrikes];
        readonly HashSet<int>[] strikeHits={new HashSet<int>(),new HashSet<int>(),new HashSet<int>()};
        public int AnnouncedFistStrikes {get;private set;}
        public FistOrigin FistStrikeOrigin(int i)=>strikes[i].origin;
        public float FistStrikeColumn(int i)=>strikes[i].column;
        // Verification only: fixes the origin of each strike instead of drawing it at random.
        public FistOrigin[] ForcedFistOrigins {get;set;}
        // Native ink warning frames exported by tools/combat/export_clock_boss_telegraphs.py.
        public const string TelegraphResources="ClockExecutionerTelegraph";
        readonly Dictionary<string,InkClip> ink=new Dictionary<string,InkClip>();
        readonly List<SpriteRenderer> inkParts=new List<SpriteRenderer>();
        public bool InkTelegraphsLoaded=>ink.Count==3;
        public int VisibleInkCount {get;private set;}
        System.Random random=new System.Random(31);
        float began,nextNeedle,nextMixerTick,lastHealth;
        bool mixerInside,resetting;
        int direction;
        SpriteRenderer leftFist,rightFist,beam,mixerFace,hourHand,minuteHand;
        Texture2D cleanTexture;
        Vector2 leftHome,rightHome;
        sealed class Hazard
        {
            public Bounds box;
            public float impact,expires,speed;
            public bool fired,landedFx;
            public readonly HashSet<int> hit=new HashSet<int>();
        }
        sealed class InkClip { public Sprite[] frames;public float fps,startAt;public int loopFrom; }
        [Serializable] sealed class InkSource { public InkEntry[] clips; }
        [Serializable] sealed class InkEntry { public string id;public float fps,startAt;public int loopFrom;public InkFrame[] frames; }
        [Serializable] sealed class InkFrame { public string path; }
        public void Initialize(CombatArena owner,Sprite fallback,float left,float floor,float hp=-1)
        {
            LoadChainTiming();
            healthEnrage=hp<0;
            RoomLeft=left;Floor=floor;
            float center=left+RoomWidth/2;
            Configure(owner,new Vector2(center,floor),new Vector2(1,1),new Vector2(0,3),false);
            bodyCollider.enabled=false; // No solid body or contact damage.
            ConfigureHurtbox(new Vector2(HurtWidth,HurtHeight),new Vector2(0,HurtCenterY));
            isBoss=true;spawn=Position;maxHealth=healthEnrage?combatTiming.maxHealth:hp;
            var bytes=Resources.Load<TextAsset>("ClockExecutioner/StationaryBytes");
            if(bytes)
            {
                cleanTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                Deviant.Media.CompressedTextureData.LoadImage(cleanTexture,bytes.bytes);cleanTexture.filterMode=FilterMode.Point;
                var colors=cleanTexture.GetPixels32();
                for(int i=0;i<colors.Length;i++)if(colors[i].r>220&&colors[i].g>220&&colors[i].b>220)colors[i].a=0;
                cleanTexture.SetPixels32(colors);cleanTexture.Apply();
            }
            visual=Art("Clock body",Crop(.29f,.16f,.43f,.83f,fallback),12);
            visual.transform.position=new Vector3(center,floor+5.3f);
            Fit(visual,new Vector2(5.5f,10));
            beam=Art("Ceiling anchor",Crop(.18f,0,.64f,.135f,fallback),11);
            beam.transform.position=new Vector3(center,floor+12.5f);Fit(beam,new Vector2(11,1.8f));
            leftFist=Art("Left chain fist",Crop(.085f,.655f,.205f,.285f,fallback),14);
            rightFist=Art("Right chain fist",Crop(.71f,.655f,.205f,.285f,fallback),14);
            leftHome=Px(363,350);rightHome=Px(597,350);
            Fit(leftFist,new Vector2(2.4f,3));Fit(rightFist,new Vector2(2.4f,3));
            LeftFistHurt=Hurt("Left fist hurtbox");RightFistHurt=Hurt("Right fist hurtbox");
            mixerFace=Art("Enlarged mixer clock",Crop(.30f,.405f,.39f,.345f,fallback),35);
            hourHand=Art("Mixer hour hand",owner.TechnicalSprite,37);
            minuteHand=Art("Mixer minute hand",owner.TechnicalSprite,38);
            LoadInk();LoadPreview();
            // A fresh seed per encounter so the pattern order, sides and aims differ every play.
            random=new System.Random(System.Environment.TickCount);
            ResetEnemy();
        }
        // Missing frames keep the original test-room shapes so the encounter stays playable.
        void LoadInk()
        {
            ink.Clear();
            var json=Resources.Load<TextAsset>(TelegraphResources+"/telegraphs");if(!json)return;
            foreach(var entry in JsonUtility.FromJson<InkSource>(json.text).clips)
            {
                var frames=new Sprite[entry.frames.Length];
                for(int i=0;i<frames.Length;i++)frames[i]=Resources.Load<Sprite>(TelegraphResources+"/"+entry.id+"/"+i.ToString("00"));
                if(Array.TrueForAll(frames,f=>f))ink[entry.id]=new InkClip{frames=frames,fps=entry.fps,startAt=entry.startAt,loopFrom=entry.loopFrom};
            }
            if(!InkTelegraphsLoaded)ink.Clear();
        }
        static Sprite InkSprite(InkClip clip,float age)
        {
            int n=clip.frames.Length,i=Mathf.Max(0,Mathf.FloorToInt(age*clip.fps));
            if(i>=n)i=clip.loopFrom<0?n-1:clip.loopFrom+(i-clip.loopFrom)%(n-clip.loopFrom);
            return clip.frames[i];
        }
        // Frames are drawn 1:1 at PPU 32 from their exported pivot; right-wall clips mirror in place.
        void Ink(ref int k,string id,float age,Vector2 at,bool flip,int order,float degrees=0,float scale=1)
        {
            while(inkParts.Count<=k)inkParts.Add(Art("Ink telegraph",null,order));
            var r=inkParts[k++];r.enabled=true;r.sprite=InkSprite(ink[id],age);r.flipX=flip;r.sortingOrder=order;
            r.color=Color.white;r.transform.position=at;r.transform.rotation=Quaternion.Euler(0,0,degrees);r.transform.localScale=new Vector3(scale,scale,1);
        }
        // The wall ink lens (192 px, its centre 96 px above the floor-side pivot) is scaled to the 4-unit fist
        // and turned so it boils where each strike comes out: either side edge, the top edge or the floor.
        void StrikeInk(ref int k,int i,float age)
        {
            var strike=strikes[i];float scale=LongFistHeight*32/192,offset=96*scale/32;
            if(strike.origin==FistOrigin.Left)Ink(ref k,"wallInk",age,new Vector2(RoomLeft,Floor),false,3,0,scale);
            else if(strike.origin==FistOrigin.Right)Ink(ref k,"wallInk",age,new Vector2(RoomRight,Floor),true,3,0,scale);
            else if(strike.origin==FistOrigin.Top)Ink(ref k,"wallInk",age,new Vector2(strike.column-offset,Floor+ArenaTop),false,3,-90,scale);
            else Ink(ref k,"wallInk",age,new Vector2(strike.column+offset,Floor),false,3,90,scale);
        }
        Sprite Crop(float x,float top,float w,float h,Sprite fallback)
        {
            if(!cleanTexture)return fallback;
            var s=Sprite.Create(cleanTexture,new Rect(x*cleanTexture.width,(1-top-h)*cleanTexture.height,w*cleanTexture.width,h*cleanTexture.height),new Vector2(.5f,.5f),100);
            ownedSprites.Add(s);return s;
        }
        SpriteRenderer Art(string label,Sprite sprite,int order)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=arena.spriteMaterial;r.sortingOrder=order;return r;
        }
        static void Fit(SpriteRenderer r,Vector2 size)
        {r.transform.localScale=new Vector3(size.x/r.sprite.bounds.size.x,size.y/r.sprite.bounds.size.y,1);}
        BoxCollider2D Hurt(string label)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var h=go.AddComponent<BoxCollider2D>();h.isTrigger=true;h.size=FistHurtSize;
            go.AddComponent<HurtRegion>().actor=this;return h;
        }
        public void BeginFight()
        {
            if(Started||Dead)return;
            Started=aggressive=true;FightStarted=arena.Now;NextPatternAt=arena.Now+2;DamageObserved=0;
        }
        public override void ResetEnemy()
        {
            resetting=true;base.ResetEnemy();resetting=false;
            if(bodyCollider)bodyCollider.enabled=false; // A shared actor reset must not restore a solid body.
            CurrentAttack=LastSpecial=ClockBossAttack.None;Started=aggressive=false;PhaseTwo=FinalPhase=RoarOnly=false;bossBarColor=PhaseOneBar;
            hazards.Clear();activeBoxes.Clear();AnnouncedNeedles=0;DamageObserved=0;
            PatternSpeed=LastPatternSpeed=1;lastHealth=health;mixerInside=false;
            diedAt=-1;collapsePlume=false;previewEffects.Clear();previewAttack=ClockBossAttack.None;IntroDropAge=float.PositiveInfinity;
            Position=spawn;velocity=Vector2.zero;Present();
        }
        // The player fell: drop the running attack and its hazards and hang idle, still drawn, until the run
        // resets (retry, F5 or a checkpoint return). Only the player dies; the boss never vanishes.
        public void StandDown()
        {
            if(Dead)return;
            DropAttack();aggressive=false;Present();
        }
        void DropAttack(){CurrentAttack=ClockBossAttack.None;hazards.Clear();activeBoxes.Clear();chainLines.Clear();hitTargets.Clear();mixerInside=false;}
        public override void CancelAttack()
        {
            if(!resetting&&!Dead&&CurrentAttack==ClockBossAttack.Mixer){stunUntil=0;return;}
            base.CancelAttack();
            CurrentAttack=ClockBossAttack.None;hitTargets.Clear();activeBoxes.Clear();
            if(arena)NextPatternAt=arena.Now+PatternGap/PatternSpeed;
            if(Dead){hazards.Clear();aggressive=false;SetHurtEnabled(false);}
        }
        protected override void OnAcceptedHit(CombatActor source,int dir)
        {
            DamageObserved+=Mathf.Max(0,lastHealth-health);lastHealth=health;
            if(Dead&&diedAt<0){diedAt=arena.Now;Defeated?.Invoke();}
            if(!Dead&&Started&&!PhaseTwo&&health<=EnrageHealth)
            {
                PhaseTwo=true;bossBarColor=PhaseTwoBar;DropAttack();PatternSpeed=PhaseTwoSpeed;NextPatternAt=arena.Now+1;
                PhaseTwoStarted?.Invoke();
            }
            if(!Dead&&Started&&healthEnrage&&!FinalPhase&&health<=FinalPhaseHealth)EnterFinalPhase();
            // No white flash on the boss (user decision): the hit spark alone shows the hit.
            if(source)SpawnPreviewEffect("hit",ToPxX(source.Position.x)+dir*45,ToPxY(source.Position.y)-44);
            if(Dead){hazards.Clear();activeBoxes.Clear();Present();}
        }
        void EnterFinalPhase()
        {
            FinalPhase=true;bossBarColor=FinalPhaseBar;
            float now=arena.Now,age=Age,oldSpeed=PatternSpeed,newSpeed=PhaseTwoSpeed*combatTiming.finalPhaseSpeedMultiplier;
            RetimeChainHolds(age,oldSpeed,newSpeed);
            PatternSpeed=newSpeed;began=now-age/newSpeed;
            float ratio=oldSpeed/newSpeed;
            nextNeedle=now+(nextNeedle-now)*ratio;
            nextMixerTick=now+(nextMixerTick-now)*ratio;
            foreach(var h in hazards)
            {
                h.impact=now+(h.impact-now)*ratio;h.expires=now+(h.expires-now)*ratio;h.speed=newSpeed;
            }
            NextPatternAt=now;
            FinalPhaseStarted?.Invoke();
        }
        public override void Step(float dt)
        {
            if(Dead||!Started||!aggressive)return;
            if(arena.Player.Dead){StandDown();return;}
            activeBoxes.Clear();velocity=Vector2.zero;Position=spawn;
            StepHazards(dt);
            if(CurrentAttack==ClockBossAttack.Mixer && arena.Sketch == null)stunUntil=0;
            if(arena.Now<stunUntil && arena.Sketch == null)return;
            if(CurrentAttack!=ClockBossAttack.None)StepAttack(dt);
            if(CurrentAttack==ClockBossAttack.None&&arena.Now>=NextPatternAt)BeginAttack(ChooseSpecial());
            Present();
        }
        ClockBossAttack ChooseSpecial()
        {
            var list=new List<ClockBossAttack>{ClockBossAttack.Needles,ClockBossAttack.LongFist,ClockBossAttack.Mixer,ClockBossAttack.ChainSlash};
            list.Remove(LastSpecial);return list[random.Next(list.Count)];
        }
        int soundedFists;
        bool soundedMixer;
        public void BeginAttack(ClockBossAttack kind)
        {
            if(Dead||(arena.Sketch == null && arena.Now<stunUntil)||kind==ClockBossAttack.Basic||kind==ClockBossAttack.Slam||kind==ClockBossAttack.None)return;
            CurrentAttack=LastSpecial=kind;began=arena.Now;PatternSpeed=FinalPhase?PhaseTwoSpeed*combatTiming.finalPhaseSpeedMultiplier:PhaseTwo?PhaseTwoSpeed:phaseOneSpeed;
            direction=random.Next(2)==0?1:-1;hitTargets.Clear();mixerInside=false;nextMixerTick=0;mixerLungeLocked=false;
            soundedFists=0; soundedMixer=false;
            if(kind==ClockBossAttack.ChainSlash)StartChainSlash();
            if(kind==ClockBossAttack.LongFist){AnnouncedFistStrikes=0;foreach(var hits in strikeHits)hits.Clear();}
            if(kind==ClockBossAttack.Needles)
            {
                int first=NeedleCell(arena.Player.Position.x,RoomLeft);cells[0]=first;
                int n=1;for(int i=0;i<NeedleCells;i++)if(i!=first)cells[n++]=i;
                for(int i=NeedleCells-1;i>1;i--){int j=random.Next(1,i+1);int v=cells[i];cells[i]=cells[j];cells[j]=v;}
                for(int i=NeedleCells;i<NeedleCount;i++)
                {
                    var options=new List<int>();
                    for(int cell=0;cell<NeedleCells;cell++){bool recent=false;for(int j=i-3;j<i;j++)if(cells[j]==cell)recent=true;if(!recent)options.Add(cell);}
                    cells[i]=options[random.Next(options.Count)];
                }
                AnnouncedNeedles=0;nextNeedle=began;AnnounceNeedle();
            }
            Present();
        }
        public static int NeedleCell(float x,float left)=>Mathf.Clamp(Mathf.FloorToInt((x-left)/3),0,NeedleCells-1);
        // Each strike picks any of four origins; vertical strikes lock onto the player's x at their warning.
        void AnnounceStrike()
        {
            int i=AnnouncedFistStrikes++;
            var origin=ForcedFistOrigins!=null&&ForcedFistOrigins.Length>i?ForcedFistOrigins[i]:(FistOrigin)random.Next(4);
            float half=LongFistColumn/2;
            strikes[i]=new FistStrike{origin=origin,column=Mathf.Clamp(arena.Player.Position.x,RoomLeft+half,RoomRight-half)};
            if(i==0&&origin<=FistOrigin.Right)direction=origin==FistOrigin.Left?1:-1;
        }
        public static bool Horizontal(FistOrigin origin)=>origin<=FistOrigin.Right;
        // Unit vector the fist travels along.
        public static Vector2 Travel(FistOrigin origin)=>origin==FistOrigin.Left?Vector2.right:origin==FistOrigin.Right?Vector2.left:origin==FistOrigin.Top?Vector2.down:Vector2.up;
        public static float StrikeExtension(float strikeAge)
        {
            if(strikeAge<LongFistPrepare||strikeAge>=LongFistMotionEnd)return -1;
            return strikeAge<LongFistExtendEnd?(strikeAge-LongFistPrepare)/LongFistExtend:strikeAge<LongFistHoldEnd?1:(LongFistMotionEnd-strikeAge)/LongFistRetract;
        }
        public Bounds StrikeBounds(int i,float extension=1)
        {
            var strike=strikes[i];float e=Mathf.Clamp01(extension);
            if(Horizontal(strike.origin))
            {
                float length=LongFistLength*e,start=strike.origin==FistOrigin.Left?RoomLeft+length/2:RoomRight-length/2;
                return new Bounds(new Vector3(start,Floor+LongFistHeight/2),new Vector3(length,LongFistHeight,1));
            }
            float reach=ArenaTop*e,y=strike.origin==FistOrigin.Top?Floor+ArenaTop-reach/2:Floor+reach/2;
            return new Bounds(new Vector3(strike.column,y),new Vector3(LongFistColumn,reach,1));
        }
        public Bounds FistBounds(float extension=1)=>StrikeBounds(0,extension);
        // Leading edge centre of a strike's box, where the fist itself is.
        Vector2 StrikeTip(int i,Bounds box){var d=Travel(strikes[i].origin);return (Vector2)box.center+Vector2.Scale(d,(Vector2)box.extents);}
        float mixerLungeFrom,mixerLungeTarget;bool mixerLungeLocked;
        // Mixer centre at travel time t (0..MixerTravel). The lunge target locks when the cruise ends.
        public Vector2 MixerAt(float t)
        {
            float start=MixerPosition(0).x,end=MixerPosition(1).x;t=Mathf.Clamp(t,0,MixerTravel);
            float cruise=start+direction*MixerCruiseSpeed*Mathf.Min(t,MixerCruise);
            if(t>=MixerCruise&&!mixerLungeLocked)
            {
                float player=arena.Player.Position.x;mixerLungeLocked=true;mixerLungeFrom=cruise;
                mixerLungeTarget=direction>0?Mathf.Clamp(player,cruise,end):Mathf.Clamp(player,end,cruise);
            }
            if(t<MixerCruise)return new Vector2(cruise,Floor+6);
            float p=Mathf.Clamp01((t-MixerCruise)/MixerLunge);
            return new Vector2(Mathf.Lerp(mixerLungeFrom,mixerLungeTarget,p*p),Floor+6);
        }
        public Vector2 MixerPosition(float progress)
        {
            float far=6+MixerSafeGap,x=direction>0?Mathf.Lerp(RoomLeft+6,RoomRight-far,progress):Mathf.Lerp(RoomRight-6,RoomLeft+far,progress);
            return new Vector2(x,Floor+6);
        }
        void AnnounceNeedle()
        {
            float impact=nextNeedle+NeedleWarning/PatternSpeed;
            hazards.Add(new Hazard{box=new Bounds(new Vector3(RoomLeft+cells[AnnouncedNeedles]*3+1.5f,Floor+6),new Vector3(3,12,1)),impact=impact,expires=impact+.5f/PatternSpeed,speed=PatternSpeed});
            AnnouncedNeedles++;nextNeedle+=NeedleInterval/PatternSpeed;
        }
        void StepAttack(float dt)
        {
            float age=Age;
            if(CurrentAttack==ClockBossAttack.Needles)
            {
                while(AnnouncedNeedles<NeedleCount&&arena.Now>=nextNeedle)AnnounceNeedle();
                if(age>=NeedleDuration-SpecialRecovery+RecoverySeconds)Finish();
            }
            else if(CurrentAttack==ClockBossAttack.LongFist)
            {
                while(AnnouncedFistStrikes<LongFistStrikes&&StrikeAge(AnnouncedFistStrikes,age)>=0)AnnounceStrike();
                for(int i=0;i<AnnouncedFistStrikes;i++)
                {
                    float a=StrikeAge(i,age);
                    if (a >= LongFistPrepare && (soundedFists & (1 << i)) == 0)
                    {
                        soundedFists |= 1 << i;
                        
                    }
                    if(a<LongFistPrepare||a>=LongFistExtendEnd)continue;
                    var box=StrikeBounds(i,(a-LongFistPrepare+dt*PatternSpeed)/LongFistExtend);activeBoxes.Add(box);
                    // Horizontal fists push 3 units along their travel; vertical ones only damage.
                    var strike=strikes[i];bool across=Horizontal(strike.origin);
                    int dir=across?(strike.origin==FistOrigin.Left?1:-1):(arena.Player.Position.x>=strike.column?1:-1);
                    // Parry: a side fist must be faced (toward the wall it comes from); a ceiling/floor fist from either facing.
                    var parryFrom=across?new Vector2(strike.origin==FistOrigin.Left?RoomLeft:RoomRight,arena.Player.Center.y):arena.Player.Center;
                    TryHit(box,25,across?3:0,strikeHits[i],dir,parryFrom,true);
                }
                if(age>=LongFistDuration-SpecialRecovery+RecoverySeconds)Finish();
            }
            else if(CurrentAttack==ClockBossAttack.ChainSlash)StepChainSlash(age);
            else if(CurrentAttack==ClockBossAttack.Mixer)
            {
                MixerCenter=MixerAt(age-MixerPrepare);
                if (age >= MixerPrepare && !soundedMixer)
                {
                    soundedMixer = true;
                    
                }
                if(age>=MixerPrepare&&age<MixerTravelEnd)
                {
                    Vector2 nearest=arena.Player.hurtCollider.ClosestPoint(MixerCenter);
                    bool inside=(nearest-MixerCenter).sqrMagnitude<=36;
                    if(inside&&(!mixerInside||arena.Now>=nextMixerTick))
                    {
                        nextMixerTick=arena.Now+.25f/PatternSpeed;
                        var p=arena.Player;float locked=p.moveLockUntil,knock=p.knockUntil,speed=p.knockSpeed;
                        if(!DashSafe(p))p.Receive(random.Next(7,9),this,direction,false,null);
                        // Mixer never adds hit stun or knockback, including on lethal ticks.
                        if(!p.Dead){p.moveLockUntil=locked;p.SetKnockMotion(knock,speed);}
                    }
                    mixerInside=inside;
                }
                if(age>=MixerDuration-SpecialRecovery+RecoverySeconds)Finish();
            }
        }
        void Finish()
        {
            LastPatternSpeed=PatternSpeed;NextPatternAt=arena.Now+PatternGap/PatternSpeed;
            CurrentAttack=ClockBossAttack.None;mixerInside=false;Present();
        }
        void StepHazards(float dt)
        {
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];float fall=.3f/h.speed;
                if(arena.Now>=h.impact-fall&&!h.fired)
                {
                    float t=Mathf.Clamp01((arena.Now-h.impact+fall)/fall),next=Mathf.Clamp01(t+dt/fall);
                    var b=h.box;b.SetMinMax(new Vector3(h.box.min.x,Floor+12*(1-next),-.5f),new Vector3(h.box.max.x,Floor+12+12*(1-t),.5f));
                    // Needles parry from either facing and deal no counter.
                    activeBoxes.Add(b);TryHit(b,25,0,h.hit,1,arena.Player.Center,false);
                    if(arena.Now>=h.impact)
                    {
                        h.fired=true;
                        
                    }
                }
                if(arena.Now>=h.expires)hazards.RemoveAt(i);
            }
        }
        // Boss attacks never hurt during the whole dash (not only its shorter i-frames); once it ends, an attack still
        // touching the player hits as usual (user decision).
        static bool DashSafe(CombatPlayer p)=>p.Invulnerable||p.Dashing;
        void TryHit(Bounds box,float damage,float push,HashSet<int> hits,int dir,Vector2 parryFrom,bool parryCounter)
        {
            var p=arena.Player;
            if(p.Dead||hits.Contains(p.ActorId)||!box.Intersects(p.hurtCollider.bounds))return;
            if(DashSafe(p))return;
            // A parried strike is consumed, so one strike is blocked (and countered) only once.
            if(!p.ReceiveWithParry(damage,this,dir,parryFrom,parryCounter,out bool parried)){if(parried)hits.Add(p.ActorId);return;}
            hits.Add(p.ActorId);if(push>0)p.SetKnockMotion(arena.Now+.2f,dir*push/.2f);
        }
        // Area ink reaches the nearest enabled hurt box: body (or the mixer square) and both fist boxes.
        // With every box off (before the fight, mixer departure/return, death) area ink cannot reach it either.
        public override Vector2 ClosestHurtPoint(Vector2 point)
        {
            Vector2 best=new Vector2(float.PositiveInfinity,float.PositiveInfinity);float nearest=float.PositiveInfinity;
            for(int i=0;i<3;i++)
            {
                var box=i==0?hurtCollider:i==1?LeftFistHurt:RightFistHurt;if(!HurtBoxActive(box))continue;
                var p=ClampToBox(HurtBox(box),point);float d=(p-point).sqrMagnitude;
                if(d<nearest){nearest=d;best=p;}
            }
            return best;
        }
        public override bool HurtOverlaps(Bounds area)
        {
            for(int i=0;i<3;i++)
            {
                var box=i==0?hurtCollider:i==1?LeftFistHurt:RightFistHurt;
                if(HurtBoxActive(box)&&Overlap2D(HurtBox(box),area))return true;
            }
            return false;
        }
        void SetHurtEnabled(bool enabled)
        {if(hurtCollider)hurtCollider.enabled=enabled;if(LeftFistHurt)LeftFistHurt.enabled=enabled;if(RightFistHurt)RightFistHurt.enabled=enabled;}
        SpriteRenderer Part(int index)
        {
            while(parts.Count<=index)parts.Add(Art("Temporary attack / telegraph",arena.TechnicalSprite,30));
            return parts[index];
        }
        void Draw(ref int n,Bounds b,Color c)
        {
            if(b.size.x<=0||b.size.y<=0)return;
            var r=Part(n++);r.enabled=true;r.transform.position=b.center;r.transform.rotation=Quaternion.identity;
            r.transform.localScale=b.size;r.color=c;
        }
        void Line(ref int n,Vector2 from,Vector2 to,float thickness,Color c)
        {
            var r=Part(n++);r.enabled=true;r.transform.position=(from+to)*.5f;
            r.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
            r.transform.localScale=new Vector3(Vector2.Distance(from,to),thickness,1);r.color=c;
        }
        void Outline(ref int n,Bounds b,Color c)
        {
            Line(ref n,new Vector2(b.min.x,b.min.y),new Vector2(b.max.x,b.min.y),.06f,c);
            Line(ref n,new Vector2(b.max.x,b.min.y),new Vector2(b.max.x,b.max.y),.06f,c);
            Line(ref n,new Vector2(b.max.x,b.max.y),new Vector2(b.min.x,b.max.y),.06f,c);
            Line(ref n,new Vector2(b.min.x,b.max.y),new Vector2(b.min.x,b.min.y),.06f,c);
        }
        void Warning(ref int n,Bounds b,float progress)
        {Draw(ref n,b,new Color(1,.25f,.05f,.12f+.12f*Mathf.Clamp01(progress)));Outline(ref n,b,new Color(1,.4f,.05f,.9f));}
        void Circle(ref int n,Vector2 center,float radius,Color c)
        {
            for(int j=0;j<64;j++)
            {float a=j*Mathf.PI/32,b=(j+1)*Mathf.PI/32;Line(ref n,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,.09f,c);}
        }
        void Chain(ref int n,Vector2 top,Vector2 end)
        {
            int count=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(top,end)/.4f),1,80);
            for(int j=0;j<count;j++)
            {
                Vector2 p=Vector2.Lerp(top,end,(j+.5f)/count);
                Draw(ref n,new Bounds(p,new Vector3(j%2==0?.18f:.3f,.36f,1)),new Color(.4f,.38f,.35f));
            }
        }
        public void Present()
        {
            if(!visual)return;
            int n=0,k=0;float age=Age;bool giant=false;bool alive=!Dead&&PresentationVisible;bool inked=InkTelegraphsLoaded,art=PreviewArtLoaded;
            bool mixing=CurrentAttack==ClockBossAttack.Mixer;
            bool normal=alive&&(!mixing||age<MixerPrepare||age>=MixerTravelEnd);
            float lift=mixing?(age<MixerPrepare?Mathf.Clamp01(age/.25f)*18:age>=MixerTravelEnd?(1-Mathf.Clamp01((age-MixerTravelEnd)/MixerReturn))*18:18):0;
            visual.enabled=normal;visual.transform.position=new Vector3(RoomLeft+RoomWidth/2,Floor+5.3f+lift);
            visual.color=Color.white;
            beam.enabled=alive;
            leftFist.enabled=rightFist.enabled=normal;
            Vector2 lp=leftHome+Vector2.up*lift,rp=rightHome+Vector2.up*lift;
            if(CurrentAttack==ClockBossAttack.LongFist)
            {
                // Both small fists leave for the three-strike sequence.
                float pull=Mathf.Clamp01(age/.35f)*10;lp+=Vector2.left*pull;rp+=Vector2.right*pull;
                if(age>=LongFistPrepare&&age<LongFistSequenceEnd)leftFist.enabled=rightFist.enabled=false;
            }
            // Preview art: hurt boxes follow the drawn small fists, including their sideways exit.
            if(art){var lo=SideOffsetPx(-1,0);var ro=SideOffsetPx(1,0);lp=Px(363+lo.x,350+lo.y);rp=Px(597+ro.x,350+ro.y);}
            leftFist.transform.position=lp;rightFist.transform.position=rp;
            LeftFistHurt.transform.position=lp;RightFistHurt.transform.position=rp;
            SetHurtEnabled(alive&&Started&&normal&&lift<.1f);
            if(normal&&!art)
            {
                float middle=RoomLeft+RoomWidth/2;
                Chain(ref n,new Vector2(middle-3.5f,Floor+12),lp+Vector2.up*1.5f);
                Chain(ref n,new Vector2(middle+3.5f,Floor+12),rp+Vector2.up*1.5f);
                Chain(ref n,new Vector2(middle,Floor+12),new Vector2(middle,Floor+10+lift));
            }
            if(alive&&Started)
            {
                if(CurrentAttack==ClockBossAttack.LongFist)
                {
                    for(int i=0;i<AnnouncedFistStrikes;i++)
                    {
                        float a=StrikeAge(i,age);
                        // Ink boils where the fist will come out, for the whole 0.7 s warning.
                        float boil=inked?a:-1;
                        if(boil>=0&&a<LongFistExtendEnd)StrikeInk(ref k,i,boil);
                        if(a<LongFistPrepare){if(!inked)Warning(ref n,StrikeBounds(i),a/LongFistPrepare);continue;}
                        float ext=StrikeExtension(a);if(ext<0)continue;
                        // Strikes never overlap in motion, so the left fist hurt box carries the giant fist.
                        var b=StrikeBounds(i,ext);if(!art)Draw(ref n,b,new Color(.13f,.12f,.12f,.95f));
                        var d=Travel(strikes[i].origin);var at=StrikeTip(i,b)-d*1.8f;giant=true;
                        var size=Horizontal(strikes[i].origin)?new Vector2(3.6f,LongFistHeight):new Vector2(LongFistColumn,3.6f);
                        leftFist.enabled=!art;leftFist.transform.position=at;Fit(leftFist,size);
                        LeftFistHurt.transform.position=at;LeftFistHurt.size=size;LeftFistHurt.enabled=true;
                    }
                }
                // The ink clock face traces the 6-unit mixer radius where the clock will appear.
                if(mixing&&age<MixerPrepare)
                {
                    if(inked)Ink(ref k,"clockRing",age,MixerPosition(0),false,3);
                    else Warning(ref n,new Bounds(new Vector3(direction>0?RoomLeft+.25f:RoomRight-.25f,Floor+6),new Vector3(.5f,12,1)),age);
                }
                foreach(var h in hazards)
                {
                    float warned=1-(h.impact-arena.Now)*h.speed/NeedleWarning;
                    if(arena.Now<h.impact)
                    {
                        // Ceiling ink drips over the locked 3-unit column; drops land on the floor line.
                        if(inked)Ink(ref k,"needleInk",warned,new Vector2(h.box.center.x,Floor),false,9);
                        else Warning(ref n,new Bounds(new Vector3(h.box.center.x,Floor+.175f),new Vector3(3,.35f,1)),warned);
                    }
                    if(!art&&arena.Now>=h.impact-.3f/h.speed)
                    {
                        var b=h.box;float t=Mathf.Clamp01((arena.Now-h.impact)*h.speed/.3f+1);
                        b.center=new Vector3(b.center.x,Floor+6+12*(1-t));
                        Draw(ref n,b,new Color(.75f,.72f,.64f,arena.Now<h.impact?1:Mathf.Clamp01((h.expires-arena.Now)*h.speed/.5f)));
                        Line(ref n,new Vector2(b.center.x,b.min.y),new Vector2(b.center.x,b.max.y),.12f,Color.black);
                    }
                }
            }
            if(alive&&Started&&CurrentAttack==ClockBossAttack.ChainSlash)
            {
                if(inked)ChainInk(ref k,age);
                if(!art)DrawChainSlashFallback(ref n,age);
            }
            bool mixerVisible=alive&&MixerActive;
            mixerFace.enabled=hourHand.enabled=minuteHand.enabled=mixerVisible;
            if(mixerVisible)
            {
                MixerCenter=MixerAt(age-MixerPrepare);
                mixerFace.transform.position=MixerCenter;Fit(mixerFace,new Vector2(12,12));
                if(inked)Ink(ref k,"clockRing",age,MixerCenter,false,art?3:36); // Preview: behind the clock body; fallback: above the square crop.
                else Circle(ref n,MixerCenter,6,new Color(1,.25f,.05f,.95f));
                RotateHand(hourHand,age*720,4,.35f);RotateHand(minuteHand,-age*1100,5.5f,.2f);
                // 8.5-unit wide box from the floor up to the top of the 6-unit mixer circle, so ground attacks reach it.
                float mixerTop=MixerCenter.y+6;
                hurtCollider.enabled=true;hurtCollider.size=new Vector2(MixerHurtSize,mixerTop-Floor);
                hurtCollider.offset=new Vector2(MixerCenter.x,(mixerTop+Floor)*.5f)-Position;
            }
            else{hurtCollider.size=new Vector2(HurtWidth,HurtHeight);hurtCollider.offset=new Vector2(0,HurtCenterY);}
            // Restore normal fist sizes once the enlarged attack has ended.
            if(!giant)
            {Fit(leftFist,new Vector2(2.4f,3));Fit(rightFist,new Vector2(2.4f,3));LeftFistHurt.size=RightFistHurt.size=FistHurtSize;}
            if(alive&&arena.showBoxes)
            {
                if(hurtCollider.enabled)Outline(ref n,hurtCollider.bounds,Color.red);
                if(LeftFistHurt.enabled)Outline(ref n,LeftFistHurt.bounds,Color.red);
                if(RightFistHurt.enabled)Outline(ref n,RightFistHurt.bounds,Color.red);
                foreach(var box in activeBoxes)Outline(ref n,box,Color.yellow);
            }
            if(art)PresentPreview(age,alive);
            for(int i=n;i<parts.Count;i++)parts[i].enabled=false;
            for(int i=k;i<inkParts.Count;i++)inkParts[i].enabled=false;
            VisibleInkCount=k;
        }
        void RotateHand(SpriteRenderer r,float angle,float length,float thickness)
        {
            float a=angle*Mathf.Deg2Rad;Vector2 v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            r.transform.position=MixerCenter+v*length*.5f;r.transform.rotation=Quaternion.Euler(0,0,angle);
            r.transform.localScale=new Vector3(length,thickness,1);r.color=new Color(.85f,.82f,.75f);
        }
        void LateUpdate()=>Present();
        void OnDisable(){foreach(var r in parts)if(r)r.enabled=false;foreach(var r in inkParts)if(r)r.enabled=false;foreach(var r in previewParts)if(r)r.enabled=false;SetHurtEnabled(false);hazards.Clear();}
        void OnDestroy(){foreach(var s in ownedSprites)if(s)Destroy(s);if(cleanTexture)Destroy(cleanTexture);}
        public void ResetCalibration(){DamageObserved=0;lastHealth=health;}
    }
}
