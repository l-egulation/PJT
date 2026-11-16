using System;
using Deviant.Combat;
using Deviant.Control.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    [DefaultExecutionOrder(800)]
    public sealed class ClockExecutionerEncounter : MonoBehaviour
    {
        // Health comes from the canonical combat data for both the stage and test room.
        public float provisionalHealth=>Boss?Boss.maxHealth:0;
        [Tooltip("Temporary intro timing: bars in, boss reveal hold, bars out.")]
        // Bars in -> the boss drops in (0.5 s) and roars (1.3 s) -> bars out -> the fight starts.
        public float barsInSeconds=.6f, revealHoldSeconds=1.8f, barsOutSeconds=.6f;
        [Tooltip("Health-driven enrage: bars in, the roar, bars out. Death: bars stay in while the boss collapses.")]
        public float phaseRoarSeconds=1.4f, deathHoldSeconds=2.4f;
        [Range(.05f,.25f)] public float barHeightFraction=.12f;
        // No fight clock: enrage is driven by health, with no time limit or time warnings.
        public ClockExecutionerBoss Boss { get; private set; }
        public bool MusicActive => isActiveAndEnabled && world && world.Ready && world.arena.Player && !world.arena.Player.Dead
            && Boss && !Boss.Dead && (walkingIn || introPlaying || Boss.Started);
        // The entry and the phase-two roar can be skipped (user decision 2026-10-29, Q7). The defeat outro cannot.
        public bool SkipAvailable=>walkingIn||introPlaying||Roaring;
        public ForestRoom Room { get; private set; }
        // Set by the play GUI when it draws the boss name and health bar itself (test scene included).
        public bool ExternalBossBar { get; set; }
        ForestCombatWorld world;
        BoxCollider2D gate;
        bool defeated,playerWasDead;
        bool introPlaying;
        float introAge;
        IControlScope introScope;
        IDisposable introScreenScope, sceneScreenScope;
        public bool IntroPlaying=>introPlaying;
        public float IntroDuration=>barsInSeconds+revealHoldSeconds+barsOutSeconds;
        public float LetterboxProgress => introPlaying?Letterbox(introAge,revealHoldSeconds):CutscenePlaying?Letterbox(sceneAge,SceneHold):0;
        float Letterbox(float age,float hold)=>age<barsInSeconds
            ?Mathf.Clamp01(age/Mathf.Max(.01f,barsInSeconds))
            :1-Mathf.Clamp01((age-barsInSeconds-hold)/Mathf.Max(.01f,barsOutSeconds));
        // Mid-fight cutscenes reuse the entry bars. The phase-two roar pauses combat; the death outro only blocks
        // input so the collapse keeps playing, and the player moves again once the bars are gone.
        // The final phase (HP 300) gets the same roar as phase two; one hit crossing both plays it once.
        enum Scene { None, PhaseTwo, FinalPhase, Death }
        bool Roaring=>scene==Scene.PhaseTwo||scene==Scene.FinalPhase;
        Scene scene;float sceneAge;IControlScope sceneScope;
        public bool CutscenePlaying=>scene!=Scene.None;
        public bool DeathCutscene=>scene==Scene.Death;
        float SceneHold=>Roaring?phaseRoarSeconds:deathHoldSeconds;
        public float CutsceneDuration=>barsInSeconds+SceneHold+barsOutSeconds;
        void BeginScene(Scene kind)
        {
            EndScene();EndWalkIn();ReleasePresentation();
            scene=kind;sceneAge=0;
            sceneScreenScope=world.arena.SuppressCombatScreenEffects();
            sceneScope=world.arena.Controls.OpenScope(kind==Scene.Death?"clock executioner defeat":kind==Scene.FinalPhase?"clock executioner final phase":"clock executioner phase two");
            sceneScope.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation);
            if(Roaring){sceneScope.PauseSimulation();Boss.RoarOnly=true;Boss.IntroDropAge=.5f;}
        }
        public void AdvanceCutscene(float dt)
        {
            if(scene==Scene.None)return;
            sceneAge+=Mathf.Max(0,dt);
            if(Roaring)
            {
                // The roar runs from the drop age 0.5 (landed) once the bars are in, like the entry's.
                float before=Boss.IntroDropAge;Boss.IntroDropAge=.5f+Mathf.Max(0,sceneAge-barsInSeconds);
                Boss.AdvancePreviewIntro(before,Boss.IntroDropAge);
            }
            if(sceneAge>=CutsceneDuration)EndScene();
        }
        void EndScene()
        {
            if(Roaring&&Boss){Boss.IntroDropAge=float.PositiveInfinity;Boss.RoarOnly=false;}
            scene=Scene.None;sceneAge=0;sceneScope?.Dispose();sceneScope=null;sceneScreenScope?.Dispose();sceneScreenScope=null;
        }
        public bool Defeated=>defeated;
        public void Initialize(ForestCombatWorld owner,Sprite sprite)
        {
            world=owner;Room=Array.Find(world.Data.rooms,r=>r.kind=="boss");
            if(Room==null)throw new InvalidOperationException("Clock boss requires a final boss room.");
            if(!sprite)sprite=Resources.Load<Sprite>("ClockExecutioner/Base");
            if(!sprite)throw new InvalidOperationException("Clock boss base sprite is missing.");
            float floor=-(Room.y+15);
            var go=new GameObject("시계탑 집행자");go.transform.SetParent(transform,false);
            Boss=go.AddComponent<ClockExecutionerBoss>();Boss.Initialize(world.arena,sprite,Room.x,floor);
            world.arena.enemies.Add(Boss);
            Boss.PresentationVisible=false;Boss.visual.enabled=false;Boss.hurtCollider.enabled=false;
            var door=new GameObject("Boss entry gate");door.transform.SetParent(transform,false);
            door.transform.position=new Vector3(Room.x-.125f,floor+7.5f);
            door.AddComponent<ArenaSurface>();gate=door.AddComponent<BoxCollider2D>();gate.size=new Vector2(.25f,15);gate.enabled=false;
            world.arena.ResetCompleted+=ResetEncounter;
            // Letterbox bars live in the world on the camera; IMGUI rectangles drifted with editor display scaling.
            topBar=Bar("Entry letterbox top");bottomBar=Bar("Entry letterbox bottom");
            Boss.CameraShake+=(amplitude,duration)=>world.ShakeCamera(amplitude,duration);
            Boss.PhaseTwoStarted+=()=>BeginScene(Scene.PhaseTwo);
            Boss.FinalPhaseStarted+=()=>{if(scene!=Scene.PhaseTwo)BeginScene(Scene.FinalPhase);};
            Boss.Defeated+=()=>BeginScene(Scene.Death);
        }
        void ResetEncounter()
        {
            if(!Boss)return;
            EndScene();EndWalkIn();ReleasePresentation();
            Boss.ResetEnemy();defeated=false;gate.enabled=false;playerWasDead=false;
            Boss.PresentationVisible=false;Boss.visual.enabled=false;Boss.hurtCollider.enabled=false;
        }
        // On entering, the camera glides onto the room while the player runs on its own to the centre of the
        // floor and stops; then the bars come in. Movement goes through the test-input axis because the
        // intro's input block would discard it.
        bool walkingIn,playerTestInput;float walkStarted;
        public float WalkInTargetX=>Room.x+Room.w/2f;
        public void BeginWalkIn()
        {
            if(walkingIn||introPlaying||Boss.Started||Boss.Dead)return;
            var player=world.arena.Player;walkingIn=true;walkStarted=Time.unscaledTime;
            introScreenScope??=world.arena.SuppressCombatScreenEffects();
            playerTestInput=player.UseTestInput;player.UseTestInput=true;
            world.FixedCameraCenter=new Vector2(Room.x+Room.w/2f,Boss.Floor+6);gate.enabled=true;Physics2D.SyncTransforms();
            Boss.PresentationVisible=false;Boss.visual.enabled=false;Boss.hurtCollider.enabled=false;
        }
        void StepWalkIn()
        {
            var player=world.arena.Player;
            float remaining=WalkInTargetX-player.Position.x;
            bool arrived=Mathf.Abs(remaining)<=.1f;
            player.axis=arrived?0:remaining>0?1:-1;
            if(arrived){player.Position=new Vector2(WalkInTargetX,player.Position.y);player.velocity=Vector2.zero;EndWalkIn();BeginIntro();}
        }
        void EndWalkIn()
        {
            if(!walkingIn)return;walkingIn=false;
            var player=world.arena.Player;if(player){player.axis=0;player.UseTestInput=playerTestInput;}
        }
        public void BeginIntro()
        {
            if(introPlaying||Boss.Started||Boss.Dead)return;
            EndWalkIn();
            world.FixedCameraCenter=new Vector2(Room.x+Room.w/2f,Boss.Floor+6);
            gate.enabled=true;Physics2D.SyncTransforms();
            Boss.PresentationVisible=false;Boss.visual.enabled=false;Boss.hurtCollider.enabled=false;
            introAge=0;introPlaying=true;Boss.IntroDropAge=-1;
            introScreenScope??=world.arena.SuppressCombatScreenEffects();
            introScope=world.arena.Controls.OpenScope("clock executioner introduction");
            introScope.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation);
            introScope.PauseSimulation();
        }
        public void AdvanceIntro(float dt)
        {
            if(!introPlaying)return;
            introAge+=Mathf.Max(0,dt);
            // Preview art drops in once the bars are in; settled (infinity) after the intro.
            float drop=Boss.IntroDropAge;Boss.IntroDropAge=introAge-barsInSeconds;Boss.AdvancePreviewIntro(drop,Boss.IntroDropAge);
            if(introAge>=barsInSeconds){Boss.PresentationVisible=true;Boss.visual.enabled=true;}
            if(introAge<IntroDuration)return;
            FinishIntro();
        }
        void FinishIntro()
        {
            introPlaying=false;introScope?.Dispose();introScope=null;introScreenScope?.Dispose();introScreenScope=null;Boss.IntroDropAge=float.PositiveInfinity;
            Boss.PresentationVisible=true;Boss.visual.enabled=true;
            Boss.hurtCollider.enabled=true;Boss.BeginFight();Boss.ResetCalibration();
        }
        // Skipping lands on each scene's end state: the settled boss and a started fight, or the phase-two
        // fight resuming. The roar's shakes are not replayed.
        public bool TrySkip()
        {
            if(!SkipAvailable||Boss.Dead||world.arena.Player.Dead)return false;
            if(Roaring){EndScene();return true;}
            if(walkingIn)EndWalkIn();
            if(!introPlaying)BeginIntro();
            FinishIntro();
            return true;
        }
        void HandleSkipKey()
        {
            if(SkipAvailable) PlayerControls.TryPerform(world.arena.Player, Deviant.Input.Contracts.GameAction.Interact, TrySkip);
        }
        void ReleasePresentation()
        {
            introPlaying=false;introAge=0;introScope?.Dispose();introScope=null;introScreenScope?.Dispose();introScreenScope=null;
            // The camera is not released here: Chapter1BossRoomEnclosure keeps the boss room view fixed
            // for as long as the player stays inside, including after the boss falls.
        }
        SpriteRenderer topBar,bottomBar;
        SpriteRenderer Bar(string label)
        {
            // A child of the camera, so the bars move with it in the same frame (placed from LateUpdate they were a
            // frame behind a moving camera and showed as a black strip partway down the screen).
            var go=new GameObject(label);go.transform.SetParent(world.View?world.View.transform:transform,false);
            var r=go.AddComponent<SpriteRenderer>();r.sprite=world.arena.TechnicalSprite;r.color=Color.black;r.sortingOrder=900;
            if(world.arena.spriteMaterial)r.sharedMaterial=world.arena.spriteMaterial;r.enabled=false;return r;
        }
        void LateUpdate()
        {
            if(!topBar||!world||!world.View)return;
            var view=world.View;float height=view.orthographicSize*2,width=height*960f/540,h=height*barHeightFraction*LetterboxProgress;
            topBar.enabled=bottomBar.enabled=(introPlaying||CutscenePlaying)&&h>0;if(!topBar.enabled)return;
            float depth=-view.transform.position.z;   // back onto the sprites' plane (z 0)
            topBar.transform.localPosition=new Vector3(0,height/2-h/2,depth);bottomBar.transform.localPosition=new Vector3(0,-height/2+h/2,depth);
            topBar.transform.localScale=bottomBar.transform.localScale=new Vector3(width+.5f,h,1);
        }
        void Update()
        {
            if(!world||!world.Ready||!Boss)return;
            var player=world.arena.Player;
            if(player.Dead)
            {
                if(!playerWasDead){EndScene();EndWalkIn();ReleasePresentation();Boss.StandDown();playerWasDead=true;} // the boss stays; only a reset clears it
                return;
            }
            playerWasDead=false;
            if(Boss.Dead)
            {
                // A death that skipped the hit path (a test setting health) must still release the entry.
                if(!defeated){defeated=true;gate.enabled=false;if(!CutscenePlaying){EndWalkIn();ReleasePresentation();}world.arena.Record("시계탑 집행자 처치 / 보스 전투 종료");}
                AdvanceCutscene(Time.unscaledDeltaTime);return;
            }
            HandleSkipKey();
            if(introPlaying){AdvanceIntro(Time.unscaledDeltaTime);return;}
            if(CutscenePlaying){AdvanceCutscene(Time.unscaledDeltaTime);return;}
            if(walkingIn){StepWalkIn();return;}
            var p=player.Position;
            bool inside=p.x>=Room.x+1&&p.x<Room.x+Room.w&&Mathf.Abs(p.y-Boss.Floor)<12;
            if(!Boss.Started&&inside)
            {
                BeginWalkIn();
            }
            // A checkpoint return or debug warp must not keep remote hazards or a sealed entry alive.
            if(Boss.Started&&!inside&&(p.x<Room.x-1||p.x>Room.x+Room.w+1||Mathf.Abs(p.y-Boss.Floor)>15))ResetEncounter();
        }
        public void DebugTestHealth(bool final) { if (Deviant.Combat.DevFeatures.Enabled && world.clockBossTestOnly && Boss) TestHealth(final ? Boss.FinalPhaseHealth : Boss.EnrageHealth); }
        public void Retry()
        {
            world.arena.ResetTest();
            var p=world.arena.Player;p.Position=new Vector2(Room.x+3,Boss.Floor+.02f);p.velocity=Vector2.zero;
            Physics2D.SyncTransforms();world.RefreshCamera(0,true);
        }
        void OnGUI()
        {
            if(!world||!Boss)return;
            if(SkipAvailable){DrawSkipPrompt();return;}
            bool inRoom=world.arena.Player.Position.x>=Room.x&&world.arena.Player.Position.x<=Room.x+Room.w;
            if(!inRoom&&!Boss.Started)return;
            var old=GUI.skin.font;if(world.diagnostics.uiFont)GUI.skin.font=world.diagnostics.uiFont;
            float x=(Screen.width-460)/2f;
            // The random stage draws nothing here; the name, health bar and tools are the test room's.
            if(!world.clockBossTestOnly){GUI.skin.font=old;return;}
            // With the play GUI's boss bar on top, the name and bar are its job; the test tools move below it.
            float y=ExternalBossBar?Screen.height*.2f:0;
            if(!ExternalBossBar)
            {
                GUI.Box(new Rect(x,12,460,28),defeated?"시계탑 집행자 / 처치 완료":"시계탑 집행자");
                var previousColor=GUI.color;
                GUI.color=new Color(.2f,.05f,.05f);GUI.DrawTexture(new Rect(x,42,460,10),Texture2D.whiteTexture);
                GUI.color=new Color(.9f,.25f,.2f);GUI.DrawTexture(new Rect(x,42,460*Mathf.Clamp01(Boss.health/Mathf.Max(.001f,Boss.maxHealth)),10),Texture2D.whiteTexture);
                GUI.color=previousColor;
            }
            if(GUI.Button(new Rect(x,y+70,140,26),"보스전 재시작"))Retry();
            world.arena.showBoxes=GUI.Toggle(new Rect(x+150,y+72,150,24),world.arena.showBoxes,"공격, 피격 판정 표시");
            GUI.Label(new Rect(x,y+100,460,24),$"HP {Boss.health:0} / {Boss.maxHealth:0} / {(Boss.FinalPhase?"최종 광폭":Boss.PhaseTwo?"광폭화":"일반")} / 속도 {Boss.PatternSpeed:0.##}배");
            bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled&&Boss.Started&&!Boss.Dead&&!CutscenePlaying;
            if(GUI.Button(new Rect(x,y+128,145,26),$"HP {Boss.EnrageHealth:0} / 광폭화"))TestHealth(Boss.EnrageHealth);
            if(GUI.Button(new Rect(x+155,y+128,145,26),$"HP {Boss.FinalPhaseHealth:0} / 최종 광폭"))TestHealth(Boss.FinalPhaseHealth);
            GUI.enabled=wasEnabled;
            GUI.skin.font=old;
        }
        void TestHealth(float target)
        {
            if(Boss.health<=target)return;
            Boss.Receive(Boss.health-target,world.arena.Player,1,false,null,true);
        }
        void OnDisable(){EndScene();EndWalkIn();ReleasePresentation();}
        void DrawSkipPrompt()
        {
            var old=GUI.skin.font;if(world.diagnostics.uiFont)GUI.skin.font=world.diagnostics.uiFont;
            var style=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleRight};style.normal.textColor=new Color(.95f,.92f,.85f);
            GUI.Label(new Rect(Screen.width-260,Screen.height-44,240,28),PlayerControls.Label(world.arena.Player, Deviant.Input.Contracts.GameAction.Interact)+" 건너뛰기",style);
            GUI.skin.font=old;
        }
        void OnDestroy(){EndScene();ReleasePresentation();if(world&&world.arena)world.arena.ResetCompleted-=ResetEncounter;if(topBar)Destroy(topBar.gameObject);if(bottomBar)Destroy(bottomBar.gameObject);}
    }
}
