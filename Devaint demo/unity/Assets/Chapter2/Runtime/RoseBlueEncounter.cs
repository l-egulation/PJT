using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Chapter2.RoseBlue;
using Deviant.Control.Contracts;
using UnityEngine;
using RTarget=Deviant.Chapter2.RoseBlue.Target;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter : MonoBehaviour, ICustomPlayerHitReactionSource
    {
        float machineMoveElapsed;
        bool machineExtraPatternStarted;
        public Encounter Model {get;private set;}
        public RoseBlueStage Stage {get;private set;}
        public RoseBlueActor Machine {get;private set;}
        public RoseBlueActor Rose {get;private set;}
        public RoseBlueActor Blue {get;private set;}
        public bool Started=>Model!=null&&Model.Phase!=Phase.Dormant;
        public bool WalkingIn {get;private set;}
        public bool EnteredRoom=>WalkingIn||Started;
        bool walkWasTestInput, entryNotified;
        void NotifyEntered(){if(entryNotified)return;entryNotified=true;Entered?.Invoke();}
        public float WalkInTargetX=>Stage.CameraCenter.x;
        public bool MusicActive => Started && !Model.Terminal;
        public bool Dead=>Model!=null&&Model.Phase==Phase.Complete;
        /// <summary>The entrance cutscene is playing.</summary>
        public bool InIntroduction=>Model!=null&&Model.Phase==Phase.Introduction;
        /// <summary>A boss cutscene with dialogue is playing (entrance, phase 1 to 2): the game GUI hides the HUD and boss bar and draws the bubbles.</summary>
        public bool InBossCutscene=>Model!=null&&(Model.Phase==Phase.Introduction||Model.Phase==Phase.FloorTransition);
        public bool OverridesPlayerHitReaction=>true;
        public bool ProtectsPlayer=>WalkingIn||Started&&(Model.Cutscene||Model.Phase==Phase.AbsorptionPush);
        public bool BlocksTraversal=>ProtectsPlayer||arena.Now<bossRootUntil||arena.Now<pushUntil;
        public float health=>(float)(Model.Phase==Phase.Machine?Model.MachineHp:Model.BlueHp);
        public float maxHealth=>(float)(Model.Phase==Phase.Machine?Tuning.MachineHp:Model.Phase==Phase.FusedBlue?Tuning.FusedBlueHp:Tuning.BlueHp);
        public RoseBlueActor HudActor=>Model.Phase==Phase.Machine||Model.Phase==Phase.Introduction?Machine:Blue;
        // 화면 표시 이름: Rose=베르소, Blue=렉토(코드 이름 Rose·Blue는 그대로). docs/design/enemies/chapter2/rose-blue/chapter2-rose-blue-boss.md 「표시 이름과 보스 체력바」
        public const string RoseDisplayName="베르소",BlueDisplayName="렉토";
        // 2페이즈는 두 보스 체력을 합친 한 줄, 3페이즈는 이름과 체력만(수식어 없음).
        public string HudText=>Model.Phase==Phase.Duo||Model.Phase==Phase.RevivingBlue
            ?$"{RoseDisplayName} {BlueDisplayName} {Mathf.CeilToInt(Rose.health+Blue.health)} / {Tuning.RoseHp+Tuning.BlueHp}"+(Model.Phase==Phase.RevivingBlue?$" · 보호막 {Mathf.CeilToInt(revivalShield?revivalShield.health:0)} / {Tuning.RevivalShieldHp}":"")
            :Model.Phase==Phase.FusedBlue?$"{BlueDisplayName} {Mathf.CeilToInt(Blue.health)} / {Tuning.FusedBlueHp}":$"기계장치 {Mathf.CeilToInt(Machine.health)} / {Tuning.MachineHp}";
        public float HudFraction=>Model.Phase==Phase.Duo||Model.Phase==Phase.RevivingBlue?(Rose.health+Blue.health)/(float)(Tuning.RoseHp+Tuning.BlueHp):HudActor.health/HudActor.maxHealth;
        public event Action Finished,Entered;
        CombatArena arena;System.Random random;AttackRotation rotation;RestMovement movement;DuoPositions positions;
        IControlScope cutsceneScope;IDisposable screenPresentationScope;float cutAge,cutLength,pushUntil,pushStarted,bossRootUntil,stunAfterPush;
        Vector2 cutFrom,cutTo,pushFrom;bool floorChanged,wasResting,pushing;int cutToken;long caster;
        float previousRoseStun,previousBlueStun;
        // Boss counter-mechanics have a real stun animation; player ink statuses only keep their overlay.
        float nativeRoseStunUntil,nativeBlueStunUntil;
        float RoseMotionStunUntil => arena.Sketch == null ? Rose.stunUntil : nativeRoseStunUntil;
        float BlueMotionStunUntil => arena.Sketch == null ? Blue.stunUntil : nativeBlueStunUntil;
        readonly List<Texture2D> ownedTextures=new List<Texture2D>();readonly List<Sprite> ownedSprites=new List<Sprite>();
        Material roseErase,blueErase,machineMaterial;
        public IEnumerable<RoseBlueActor> Actors
        {get{yield return Machine;yield return Rose;yield return Blue;foreach(var p in exchangeActors)if(p)yield return p;if(revivalShield)yield return revivalShield;}}
        public void RegisterTargets(List<TestEnemy> targets)
        {
            foreach(var a in new[]{Machine,Rose,Blue})targets.Remove(a);
            RefreshActors();
            foreach(var a in Actors)if(a&&a.hurtCollider&&a.hurtCollider.enabled&&!a.Dead&&!targets.Contains(a))targets.Add(a);
        }
        public void Initialize(CombatArena owner,Vector2 origin,int seed)
        {
            arena=owner;random=new System.Random(seed);rotation=new AttackRotation(seed);movement=new RestMovement();Model=Encounter.CreateApproved();
            Stage=new GameObject("Rose Blue approved battlefield").AddComponent<RoseBlueStage>();Stage.transform.SetParent(transform,false);Stage.Build(arena,origin,seed);
            var m=Stage.Data.devices.Single();Machine=Actor("Machine",Stage.World(new Vector2(m.x+m.w/2,m.y)),new Vector2(m.w,m.h),null,new Color(.48f,.64f,.65f),(float)Tuning.MachineHp,RTarget.Machine);
            Rose=Character("rose",RTarget.Rose,(float)Tuning.RoseHp);Blue=Character("blue",RTarget.Blue,(float)Tuning.BlueHp);
            var erase=Resources.Load<Shader>("RoseBlueErase");if(erase){roseErase=new Material(erase);blueErase=new Material(erase);machineMaterial=new Material(erase){renderQueue=arena.Player.visual.sharedMaterial.renderQueue};Machine.visual.sharedMaterial=machineMaterial;}
            // Keep the interactable machine behind the player, including during its erase transition.
            Machine.visual.sortingLayerID=arena.Player.visual.sortingLayerID;
            Machine.visual.sortingOrder=arena.Player.visual.sortingOrder-1;
            InitializePhase1Art();Machine.PresentationVisible=false;RefreshActors();
            arena.Player.EncounterProtection=()=>ProtectsPlayer;
            arena.Player.JumpAdmission=()=>!BlocksTraversal;
            arena.Player.FallDestination=()=>Started&&Model.Phase==Phase.Machine?(Vector2?)Stage.PlatformCenter(Stage.Field.PickFallRespawn()):null;
        }
        RoseBlueActor Character(string name,RTarget role,float hp)
        {
            var p=Stage.Data.Marker(name+"-encounter");var sprite=ReferenceSprite(name);
            var actor=Actor(name,Stage.World(p.Feet),new Vector2(p.w,p.h),sprite,Color.white,hp,role);
            actor.ConfigureHurtbox(new Vector2((float)Tuning.BossHurtWidth,(float)Tuning.BossHurtHeight),new Vector2(0,(float)Tuning.BossHurtHeight/2));actor.visual.flipX=p.flipX;return actor;
        }
        RoseBlueActor Actor(string name,Vector2 at,Vector2 size,Sprite sprite,Color color,float hp,RTarget role)
        {
            var a=new GameObject("Rose Blue / "+name).AddComponent<RoseBlueActor>();a.transform.SetParent(transform,false);a.Initialize(arena,at,size,sprite,color,hp);
            a.DamageAllowed=()=>Model.CanDamage(role)&&!(role==RTarget.Rose&&ShieldUp);
            a.HitAdmission=origin=>role!=RTarget.Rose||Model.Phase!=Phase.RevivingBlue||!origin.IsSpecified||!shieldExecutions.Contains(origin.ExecutionId);a.ControlAllowed=()=>role!=RTarget.Machine&&Model.Phase!=Phase.RevivingBlue&&Model.CanDamage(role);
            a.HealthFloor=()=>role==RTarget.Rose&&Model.Phase==Phase.RevivingBlue?1:0;
            a.Interrupted=()=>{Model.Stun(role);DrainSignals();};return a;
        }
        Sprite ReferenceSprite(string name)
        {
            var bytes=Resources.Load<TextAsset>("RoseBlue/"+name);if(!bytes)throw new InvalidOperationException("Rose/Blue reference bytes missing from local Unity deployment");
            var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);Deviant.Media.CompressedTextureData.LoadImage(tex,bytes.bytes);tex.filterMode=FilterMode.Point;
            // Display-only background removal. Source PNG bytes stay intact in resources.
            var pixels=tex.GetPixels32();var seen=new bool[pixels.Length];var queue=new Queue<int>();
            void Add(int i){if(seen[i])return;seen[i]=true;var c=pixels[i];if(c.r>245&&c.g>245&&c.b>245)queue.Enqueue(i);}
            for(int x=0;x<tex.width;x++){Add(x);Add((tex.height-1)*tex.width+x);}for(int y=0;y<tex.height;y++){Add(y*tex.width);Add(y*tex.width+tex.width-1);}
            while(queue.Count>0){int i=queue.Dequeue();pixels[i].a=0;if(i%tex.width>0)Add(i-1);if(i%tex.width<tex.width-1)Add(i+1);if(i>=tex.width)Add(i-tex.width);if(i<pixels.Length-tex.width)Add(i+tex.width);}
            tex.SetPixels32(pixels);tex.Apply();Rect crop=name=="rose"?new Rect(150,tex.height-180-408,468,408):new Rect(220,tex.height-130-508,328,508);
            var sprite=Sprite.Create(tex,crop,new Vector2(.5f,.5f),32);ownedTextures.Add(tex);ownedSprites.Add(sprite);return sprite;
        }
        public void BeginWalkIn()
        {
            if(EnteredRoom||Dead)return;
            var player=arena.Player;WalkingIn=true;walkWasTestInput=player.UseTestInput;
            player.CancelAction();player.ClearInputState();player.UseTestInput=true;
            Stage.ShowPlatforms(false);Stage.Enclose();NotifyEntered();
        }
        void EndWalkIn()
        {
            if(!WalkingIn)return;
            WalkingIn=false;
            if(arena&&arena.Player){arena.Player.axis=0;arena.Player.UseTestInput=walkWasTestInput;arena.Player.velocity=Vector2.zero;}
        }
        public void BeginFight(){if(Started)return;Model.Begin();arena.Player.Position=new Vector2(arena.Player.Position.x,Mathf.Max(arena.Player.Position.y,Stage.Floor));arena.Player.velocity=Vector2.zero;Stage.ShowPlatforms(false);Stage.Enclose();NotifyEntered();DrainSignals();}
        public void BeforePlayer(float dt)
        {
            if(WalkingIn)
            {
                var player=arena.Player;float distance=WalkInTargetX-player.Position.x;
                player.axis=Mathf.Abs(distance)<=.1f?0:distance>0?1:-1;return;
            }
            if(!Started||Model.Terminal)return;
            if(ProtectsPlayer||arena.Now<pushUntil){arena.Player.moveLockUntil=Mathf.Max(arena.Player.moveLockUntil,arena.Now+dt*2);arena.Player.velocity=Vector2.zero;}
            if(arena.Now<bossRootUntil)arena.Player.rootUntil=Mathf.Max(arena.Player.rootUntil,bossRootUntil);
        }
        public void Step(float dt)
        {
            if(WalkingIn)
            {
                var player=arena.Player;
                if(player.Dead){EndWalkIn();return;}
                if(Mathf.Abs(player.Position.x-WalkInTargetX)<=.15f)
                {
                    player.Position=new Vector2(WalkInTargetX,Stage.Floor);EndWalkIn();BeginFight();
                }
                return;
            }
            if(Started&&Model.Phase==Phase.Complete){StepDefeatPose();return;}
            if(!Started||Model.Terminal)return;
            if(arena.Player.Dead){Model.EndRun();DrainSignals();return;}
            Model.DamageBatch(Math.Max(0,Model.MachineHp-Machine.health),Math.Max(0,Model.RoseHp-Rose.health),Math.Max(0,Model.BlueHp-Blue.health));
            DrainSignals();SyncHealth();
            if(Model.Cutscene){StepCutscene(dt);RefreshActors();StepPhase1Presentation(dt);if(Model.Phase==Phase.Introduction)StepIntro();else if(Model.Phase==Phase.FloorTransition)StepPhaseShift();return;}
            if(Model.Phase==Phase.AbsorptionPush){StepPush();Model.Advance(dt);DrainSignals();RefreshActors();return;}
            Model.Advance(dt);DrainSignals();SyncHealth();
            if(pushing)StepPush();
            if(Model.Phase==Phase.Machine&&arena.Player.Position.y<Stage.Origin.y+.25f)arena.Player.Fall();
            if(Model.Phase==Phase.Machine)machineMoveElapsed+=dt;
            StepHazards(dt);StepExchange(dt);StepCasts(dt);
            if(arena.Sketch == null && previousBlueStun<Blue.stunUntil&&arena.Now<Blue.stunUntil)Interrupt(RTarget.Blue);
            if(arena.Sketch == null && previousRoseStun<Rose.stunUntil&&arena.Now<Rose.stunUntil)Interrupt(RTarget.Rose);
            previousBlueStun=Blue.stunUntil;previousRoseStun=Rose.stunUntil;
            if(Model.Phase==Phase.Machine||Model.Phase==Phase.Duo||Model.Phase==Phase.FusedBlue)
            {
                rotation.Advance(dt);
                bool stunned=arena.Sketch == null && (arena.Now<Blue.stunUntil||Model.Phase==Phase.Duo&&arena.Now<Rose.stunUntil);
                bool resting=rotation.CanMove&&!stunned&&(arena.Sketch != null || arena.Now>=Blue.rootUntil);
                if(resting){if(!wasResting)movement.Begin(Blue.Position.x,arena.Player.Center.x);Blue.Position=new Vector2((float)movement.Step(Blue.Position.x,dt*(1-arena.ReadRunInkMotionSlow(Blue)),Stage.Area.xMin+Blue.size.x/2,Stage.Area.xMax-Blue.size.x/2),Stage.Floor);}
                wasResting=resting;
                if(!stunned){var next=rotation.PickNext();if(next.HasValue){
                    if(Model.Phase==Phase.Machine&&machineMoveElapsed>=Tuning.MachineMoveInterval){
                        if(machineExtraPatternStarted){
                            Stage.Field.MoveMachine();Machine.Position=Stage.PlatformCenter(Stage.Field.MachinePlatform);machineMoveElapsed=0;
                            machineExtraPatternStarted=false;
                        }
                        else machineExtraPatternStarted=true;
                    }
                    BeginPattern(next.Value);
                }}
            }
            RefreshActors();StepPhase1Presentation(dt);
        }
        void DrainSignals()
        {
            while(Model.TryRead(out var n))switch(n.Kind)
            {
                case Signal.CancelBossEffects:ClearBossEffects();break;
                case Signal.BeginIntroduction:BeginCutscene(n.Generation,IntroSeconds,Stage.World(Stage.Data.Marker("player-phase1").Feet));BeginIntroPresentation();break;
                case Signal.BeginFloorCutscene:BreakMachineArt();Stage.ShowPlatforms(false);Machine.PresentationVisible=false;BeginCutscene(n.Generation,PhaseShiftSeconds,new Vector2(arena.Player.Position.x,Stage.Floor));BeginPhaseShiftPresentation();break;
                case Signal.BeginAbsorptionPush:screenPresentationScope??=arena.SuppressCombatScreenEffects();Blue.Position=Rose.Position;PushPlayer(0);break;
                case Signal.BeginAbsorptionCutscene:BeginCutscene(n.Generation,(float)TemporaryPresentation.AbsorptionSeconds,new Vector2(Stage.CameraCenter.x,Stage.Floor));break;
                case Signal.BeginMachine:Stage.ShowPlatforms(true);Machine.PresentationVisible=true;rotation.SetPhase(Phase.Machine);break;
                case Signal.BeginDuo:
                    Rose.Position=Stage.World(Stage.Data.Marker("rose-phase2").Feet);Blue.Position=Stage.World(Stage.Data.Marker("blue-phase2").Feet);
                    positions=new DuoPositions(new Point(Rose.Position.x,Rose.Position.y),new Point(Blue.Position.x,Blue.Position.y));rotation.SetPhase(Phase.Duo);break;
                case Signal.BeginFusedBlue:Blue.maxHealth=(float)Tuning.FusedBlueHp;rotation.SetPhase(Phase.FusedBlue);break;
                case Signal.BeginRevival:BeginRevivalShield();break;
                case Signal.ReviveBlue:ClearRevivalShield();Blue.visual.color=Color.white;break;
                case Signal.ReturnRose:positions.FinishRevival();Rose.Position=new Vector2((float)positions.Rose.X,(float)positions.Rose.Y);break;
                case Signal.CancelUnspawnedAttacks:Interrupt(n.Actor);break;
                case Signal.RoseStunned:nativeRoseStunUntil=arena.Now+(float)Tuning.ExchangeStun;Rose.stunUntil=Mathf.Max(Rose.stunUntil,nativeRoseStunUntil);break;
                case Signal.BlueStunned:nativeBlueStunUntil=arena.Now+(float)Tuning.ExchangeStun;Blue.stunUntil=Mathf.Max(Blue.stunUntil,nativeBlueStunUntil);break;
                case Signal.ExchangeExplosion:
                    Hit(Centered(exchangeMeeting,(float)Tuning.ExchangeBlastWidth,(float)Tuning.ExchangeBlastHeight),(float)Tuning.ExchangeDamage);
                    StartSheetFx("exchange-flash",exchangeMeeting,1.5f,false,AbovePlayer(6));break;
                case Signal.Finished:Stage.ReleaseEnclosure();Finished?.Invoke();ReleaseControl();break;
                case Signal.RunEnded:ReleaseControl();break;
            }
        }
        void SyncHealth(){Machine.health=(float)Model.MachineHp;Rose.health=(float)Model.RoseHp;Blue.health=(float)Model.BlueHp;}
        void RefreshActors()
        {
            if(Model.Phase==Phase.Dormant)
            {foreach(var actor in new[]{Machine,Rose,Blue}){actor.PresentationVisible=false;actor.SetHittable(false);}return;}
            foreach(var a in new[]{Machine,Rose,Blue})a.SetHittable((a.DamageAllowed?.Invoke()??false));
            if(revivalShield){revivalShield.SetHittable(ShieldUp);revivalShield.PresentationVisible=ShieldUp;}
            if(Model.Phase==Phase.Machine){Rose.PresentationVisible=casts.Any(c=>c.Pattern==Pattern.LayerShots&&c.Clock.WillSpawn("rose-arrow"));Blue.PresentationVisible=casts.Any(c=>c.Pattern==Pattern.LayerShots&&c.Clock.WillSpawn("blue-orb")||c.Pattern==Pattern.MachineAndPlatforms&&c.Clock.Age<MachineBurstPoseSeconds);}
            else{Rose.PresentationVisible=Model.Phase!=Phase.FusedBlue&&Model.Phase!=Phase.Complete;Blue.PresentationVisible=Model.Phase!=Phase.RevivingBlue&&Model.Phase!=Phase.Complete;}
            if(Model.Phase==Phase.Duo){var dive=casts.LastOrDefault(c=>c.Pattern==Pattern.RootAndShots&&!c.BlueReturned&&!c.BlueCancelled);if(dive!=null)Blue.PresentationVisible=dive.Clock.Age>=Tuning.RootTell-Tuning.BlueDropSeconds;}
            Machine.PresentationVisible=Model.Phase==Phase.Machine;
        }
        void BeginCutscene(int token,float duration,Vector2 destination)
        {
            screenPresentationScope??=arena.SuppressCombatScreenEffects();
            cutToken=token;cutAge=0;cutLength=duration;cutFrom=arena.Player.Position;cutTo=destination;floorChanged=false;fallVelocity=0;landedAge=-1;
            if(cutsceneScope==null){cutsceneScope=arena.Controls.OpenScope("Rose Blue cutscene");cutsceneScope.BlockInput(InputChannels.Combat|InputChannels.Navigation|InputChannels.Interaction);}
            arena.Player.CancelAction();arena.Player.ClearInputState();
        }
        void StepCutscene(float dt)
        {
            cutAge+=dt;
            if(Model.Phase==Phase.FloorTransition){StepNaturalFall(dt);return;}
            // The entrance cutscene moves the player and draws its bubble itself (StepIntro, RoseBlueEncounter.Intro.cs).
            if(Model.Phase!=Phase.Introduction)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(cutAge/cutLength));arena.Player.Position=Vector2.Lerp(cutFrom,cutTo,t);arena.Player.velocity=Vector2.zero;
                DrawLiftBubble();
            }
            if(cutAge>=cutLength)CompleteCutscene(cutToken);
        }
        public void CompleteCutscene(int token)
        {
            bool wasIntro=Model.Phase==Phase.Introduction,wasShift=Model.Phase==Phase.FloorTransition;
            if(!Model.CompleteCutscene(token))return;
            if(wasIntro)EndIntroPresentation();if(wasShift)EndPhaseShiftPresentation();
            arena.Player.Position=cutTo;arena.Player.velocity=Vector2.zero;arena.Player.jumps.Reset();ReleaseControl();
            Rose.visual.sharedMaterial=Blue.visual.sharedMaterial=arena.spriteMaterial;Rose.visual.color=Blue.visual.color=Color.white;DrainSignals();SyncHealth();RefreshActors();Physics2D.SyncTransforms();
        }
        // Only the dedicated scene calls this, immediately after a fresh scene load.
        public void StartTestPhase(int phase)
        {
            if (phase < 1 || phase > 3)
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (Model.Phase != Phase.Introduction)
                throw new InvalidOperationException("Phase selection requires a fresh boss scene.");
            CompleteCutscene(Model.Generation);
            if (phase >= 2)
            {
                Model.DamageBatch(Tuning.MachineHp, 0, 0);
                DrainSignals();
                cutTo = new Vector2(Stage.CameraCenter.x, Stage.Floor);
                CompleteCutscene(Model.Generation);
            }
            if (phase == 3)
            {
                Model.DamageBatch(0, Tuning.RoseHp, 0);
                DrainSignals();
                Model.Advance(Tuning.PushSeconds);
                DrainSignals();
                CompleteCutscene(Model.Generation);
            }
            pushing = false;
            pushUntil = bossRootUntil = arena.Now;
            arena.Player.moveLockUntil = arena.Player.stunUntil = arena.Player.rootUntil = arena.Now;
            arena.Player.velocity = Vector2.zero;
            SyncHealth();
            RefreshActors();
            Physics2D.SyncTransforms();
        }
        void ReleaseControl(){EndWalkIn();HideLiftBubble();cutsceneScope?.Dispose();cutsceneScope=null;screenPresentationScope?.Dispose();screenPresentationScope=null;}
        void OnEnable()
        {
            if(arena&&Model!=null&&(Model.Cutscene||Model.Phase==Phase.AbsorptionPush))
                screenPresentationScope??=arena.SuppressCombatScreenEffects();
        }
        void OnDisable(){EndWalkIn();screenPresentationScope?.Dispose();screenPresentationScope=null;}
        void PushPlayer(float afterStun)
        {pushing=true;pushStarted=arena.Now;pushUntil=arena.Now+(float)Tuning.PushSeconds;pushFrom=arena.Player.Position;stunAfterPush=afterStun;arena.Player.CancelAction();arena.Player.moveLockUntil=pushUntil+afterStun;arena.Player.stunUntil=Mathf.Max(arena.Player.stunUntil,pushUntil+afterStun);}
        void StepPush()
        {float t=Mathf.Clamp01((arena.Now-pushStarted)/(float)Tuning.PushSeconds);arena.Player.Position=Vector2.Lerp(pushFrom,new Vector2(Stage.CameraCenter.x,Stage.Floor),t);arena.Player.velocity=Vector2.zero;if(t>=1){pushing=false;if(stunAfterPush>0){arena.Player.stunUntil=Mathf.Max(arena.Player.stunUntil,pushUntil+stunAfterPush);stunAfterPush=0;}}}
        void OnDestroy(){ClearRevivalShield();ClearBossEffects();ReleaseControl();if(roseErase)Destroy(roseErase);if(blueErase)Destroy(blueErase);if(machineMaterial)Destroy(machineMaterial);foreach(var s in ownedSprites)if(s)Destroy(s);foreach(var t in ownedTextures)if(t)Destroy(t);if(arena&&arena.Player){arena.Player.EncounterProtection=null;arena.Player.JumpAdmission=null;arena.Player.FallDestination=null;}}
    }
}
