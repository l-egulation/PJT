using System;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestCombatV2.Editor
{
    // Explicit, project-local request; never launches or changes scenes without this marker.
    [InitializeOnLoad]
    public static class ClockExecutionerVerification
    {
        const string Key="ClockExecutionerVerification.Pending",ModeKey="ClockExecutionerVerification.Mode";
        static double thirdShotAt;static bool bossTestAsked;
        static readonly string Request=Path.GetFullPath("../outputs/clock-boss/verify.request");
        static readonly string Output=Path.GetFullPath("../outputs/clock-boss");
        static readonly List<string> checks=new List<string>();
        static double started,introStart;
        static int introShot;static bool introLogged;
        // Real-time entry captures after the checks: bars in, drop, roar, bars out, fight.
        static readonly float[] IntroShots={.3f,.9f,1.6f,2.4f,3.1f,4f,4.7f};
        static ClockExecutionerVerification(){EditorApplication.update+=Tick;}
        [MenuItem("Tools/Deviant/Chapter1/Verify Boss Combat")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Request,"verify");
        }
        static void Require(bool pass,string label)
        {if(!pass)throw new InvalidOperationException(label);checks.Add(label);}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.Exists(Request)&&!SessionState.GetBool(Key,false))
            {
                if(EditorApplication.isPlaying)return;
                if(EditorSceneManager.GetActiveScene().isDirty){File.WriteAllText(Path.Combine(Output,"waiting.txt"),"Save current unsaved scene before verification.");return;}
                // "third" only takes the boss down to a third of its health and captures that fight with the GUI.
                // "third-play" does it in the main stage after the F10 boss room move, so the capture shows the play GUI.
                var mode=File.ReadAllText(Request).Trim();SessionState.SetString(ModeKey,mode);
                File.Delete(Request);checks.Clear();bossTestAsked=false;
                if(mode=="play"){ClockExecutionerBossTools.PlayTest();return;}
                if(mode=="third-play")EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
                else{ClockExecutionerBossTools.EnsureTestScene();EditorSceneManager.OpenScene(ClockExecutionerBossTools.TestScene);}
                SessionState.SetBool(Key,true);started=EditorApplication.timeSinceStartup;
                EditorApplication.EnterPlaymode();return;
            }
            if(introStart>0){CaptureIntro();return;}
            if(thirdShotAt>0){if(EditorApplication.timeSinceStartup>=thirdShotAt)CaptureThird();return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(!world||!world.Ready)return;
            bool inRun=SessionState.GetString(ModeKey,"")=="third-play";
            // F10 moves the current run's player to the boss room (once), then the fight goes on as in the test room.
            if(inRun&&!bossTestAsked)
            {
                bossTestAsked=true;started=0;var selector=UnityEngine.Object.FindAnyObjectByType<Chapter1MapSelector>();
                if(!selector||!selector.OpenBossTest()){Debug.LogError("F10 boss room move is unavailable in the main stage.");SessionState.SetBool(Key,false);Finish();}
                return;
            }
            // The test room spawns outside; bring the player in and let real frames settle the fixed camera and the
            // village parallax, which only follows the camera in LateUpdate (synchronous captures would show it stale).
            if(started==0){started=EditorApplication.timeSinceStartup;var entry=world.GetComponent<ClockExecutionerEncounter>();if(entry)entry.Retry();return;}
            if(EditorApplication.timeSinceStartup-started<5.5f)return; // walk-in (~1 s) and the 3 s entry finish first
            SessionState.SetBool(Key,false);
            try
            {
                var encounter=world.GetComponent<ClockExecutionerEncounter>();var boss=encounter.Boss;var arena=world.arena;var player=arena.Player;
                Require((world.clockBossTestOnly||inRun)&&encounter.Room.w==ClockExecutionerBoss.RoomWidth&&ClockExecutionerBoss.RoomWidth==30,"Dedicated open 30-unit boss room");
                Require(world.Data.spawn.x/32f<encounter.Room.x,"The test room starts outside, so the player walks in");
                encounter.Retry();encounter.BeginIntro();
                Require(encounter.IntroPlaying&&world.FixedCameraCenter.HasValue,"Entry locks camera");
                Require(!boss.PresentationVisible&&!boss.Started&&arena.Controls.Current.SimulationPaused,"Entry pauses combat");
                encounter.AdvanceIntro(encounter.barsInSeconds+.42f);boss.Present();world.RefreshCamera(0,true);
                Capture(world.View,Path.Combine(Output,"boss-preview-intro.png"));
                Require(!boss.PreviewArtLoaded||boss.IntroDropAge>0&&boss.IntroDropAge<.5f,"Entry drops the assembled boss in after the bars");
                encounter.AdvanceIntro(encounter.IntroDuration+.01f);
                Require(boss.Started&&!encounter.IntroPlaying,"Combat begins after bars disappear");
                player.UseTestInput=true;player.maxHealth=10000;player.health=10000;
                using(var manual=arena.AcquireManualSimulation())
                {
                    void Advance(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds*120);i++)manual.Step(1f/120);}
                    void Place(float x,float y=0){player.Position=new Vector2(x,boss.Floor+y+.02f);player.velocity=Vector2.zero;Physics2D.SyncTransforms();}
                    boss.phaseOneSpeed=1; // timing checks below read the base (phase two) speed
                    void Reset(){boss.ResetEnemy();boss.BeginFight();boss.ResetCalibration();Place(boss.RoomRight-1);}
                    if(SessionState.GetString(ModeKey,"").StartsWith("third"))
                    {
                        // Just under a third of health: phase two starts, its roar is skipped, and the fight runs on
                        // in real frames so the play GUI refreshes before CaptureThird.
                        Reset();Place(boss.RoomLeft+8);Advance(.5f);
                        boss.Receive(boss.health-boss.EnrageHealth+1,player,1,false,null,true);
                        // The roar pauses the simulation at once, so skip it before stepping on.
                        Require(boss.PhaseTwo,"A third of health left starts phase two");
                        Require(encounter.TrySkip(),"The phase-two roar can be skipped");Advance(1.2f);
                        thirdShotAt=EditorApplication.timeSinceStartup+1.5;return;
                    }
                    Reset();var origin=boss.Position;Advance(1);
                    Require(boss.Position==origin&&Mathf.Abs(origin.x-boss.RoomLeft-ClockExecutionerBoss.RoomWidth/2)<.001f,"Boss stays fixed at room center");
                    Require(!boss.bodyCollider.enabled,"Body has no solid collision");
                    Require(boss.LeftFistHurt.GetComponent<HurtRegion>().actor==boss&&boss.RightFistHurt.GetComponent<HurtRegion>().actor==boss,"Fists share boss identity for damage deduplication");
                    var lefts=new[]{FistOrigin.Left,FistOrigin.Left,FistOrigin.Left};
                    boss.ForcedFistOrigins=lefts;boss.BeginAttack(ClockBossAttack.LongFist);Advance(.55f);
                    Require(boss.FistBounds().size==new Vector3(30,4,1),"Long fist punches the full 30 units, 4 high");
                    var move=arena.Movement;float single=move.FirstJumpVelocity*move.FirstJumpVelocity/(2*move.RiseGravity);
                    float apex=single+move.SecondJumpVelocity*move.SecondJumpVelocity/(2*move.RiseGravity);
                    Require(single<ClockExecutionerBoss.LongFistHeight&&apex>ClockExecutionerBoss.LongFistHeight+1,"A double jump clears the 4-unit fist, a single jump does not");
                    Place(boss.RoomRight-1);float hp=player.health;Advance(1.2f);
                    Require(player.health<hp&&boss.AnnouncedFistStrikes==1,"The fist reaches the far edge 0.12 s after its 1 s warning");
                    Advance(5.3f);Require(boss.CurrentAttack==ClockBossAttack.None&&boss.AnnouncedFistStrikes==3,"Long fist sequence of three ends after 7 seconds");
                    Require(boss.NextPatternAt>arena.Now+1.8f,"Two-second gap after recovery");
                    Reset();boss.ForcedFistOrigins=lefts;boss.BeginAttack(ClockBossAttack.LongFist);
                    Place(boss.RoomLeft+2);hp=player.health;
                    Advance(1.45f);Require(player.health==hp,"Long fist waits for the 0.5 s exit and its 1 s warning");
                    Advance(.1f);Require(player.health==hp-25,"Long fist deals 25 once during its 0.12 s punch");
                    hp=player.health;Advance(.8f);Require(player.health==hp,"Hold and retraction are harmless");
                    Reset();boss.ForcedFistOrigins=new[]{FistOrigin.Top,FistOrigin.Bottom,FistOrigin.Right};
                    Place(boss.RoomLeft+10);boss.BeginAttack(ClockBossAttack.LongFist);Advance(.55f);
                    Require(Mathf.Abs(boss.FistStrikeColumn(0)-(boss.RoomLeft+10))<.05f,"Vertical strike locks onto the player");
                    hp=player.health;Advance(1.1f);Require(player.health==hp-25,"Top strike slams down the locked column");
                    Advance(.2f);Require(boss.AnnouncedFistStrikes==2&&boss.FistStrikeOrigin(1)==FistOrigin.Bottom,"Next strike warns 1.3 s after the previous one");
                    Reset();boss.ForcedFistOrigins=lefts;boss.BeginAttack(ClockBossAttack.LongFist);Place(boss.RoomLeft+15);
                    Advance(1.7f);Physics2D.SyncTransforms();float bossHp=boss.health;
                    BombProjectile.Spawn(arena,player,new Vector2(boss.RoomRight-7,boss.Floor+2),new Vector2(25,0),new BombData{radius=.25f,blastRadius=1.5f,lifetime=3},20);
                    Advance(.4f);Require(boss.health<bossHp,"A bomb is stopped by the giant fist and damages the boss");
                    boss.ForcedFistOrigins=null;
                    Reset();boss.BeginAttack(ClockBossAttack.Needles);Advance(.35f);
                    Require(boss.AnnouncedNeedles==2,"Needle stagger is 0.2 seconds");
                    boss.Receive(1,player,1,false,new HitData{stun=.8f});
                    Require(boss.CurrentAttack==ClockBossAttack.None&&boss.PendingHazards==2,"Stun cancels only unannounced needles");
                    // Chain slash: lines drawn one by one, a chain follows each 0.65 s after it is finished.
                    Reset();Place(boss.RoomLeft+15);boss.ForcedChainAnchor=new Vector2(boss.RoomLeft+15,boss.Floor+1);boss.BeginAttack(ClockBossAttack.ChainSlash);
                    Require(boss.ChainLineCount>=5&&boss.ChainLineCount<=7,"Chain slash draws five to seven lines");
                    Advance(.15f);Require(boss.AnnouncedChainLines==0,"Lines start after a 0.2 s lead");
                    Advance(.1f);Require(boss.AnnouncedChainLines==1,"First line is drawn");
                    hp=player.health;
                    Advance(.8f);Require(player.health==hp&&boss.AnnouncedChainLines==4,"No damage before a chain reaches the far edge; a new line every 0.25 s");
                    bool aimed=true;
                    for(int i=0;i<boss.AnnouncedChainLines;i++)
                    {
                        Vector2 a=boss.ChainLineFrom(i),b=boss.ChainLineTo(i),q=boss.ForcedChainAnchor.Value;
                        float off=Mathf.Abs((b.x-a.x)*(q.y-a.y)-(b.y-a.y)*(q.x-a.x))/Vector2.Distance(a,b);
                        bool edge=Mathf.Abs(a.x-boss.RoomLeft)<.01f||Mathf.Abs(a.x-boss.RoomRight)<.01f||Mathf.Abs(a.y-boss.Floor)<.01f||Mathf.Abs(a.y-boss.Floor-ClockExecutionerBoss.ArenaTop)<.01f;
                        aimed&=edge&&off<.01f&&Vector2.Distance(b,a)>Vector2.Distance(q,a);
                        aimed&=(Mathf.Abs(b.x-a.x)>Mathf.Abs(b.y-a.y))==(i%2==0);
                    }
                    Require(aimed,"Chain lines start on an edge, run through the aim point and alternate sideways/upright");
                    Shot("boss-chain-lines.png");
                    // Invulnerable at the first chain's impact: it must never retry after protection ends.
                    var protect=typeof(CombatPlayer).GetField("respawnProtect",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    protect.SetValue(player,arena.Now+.25f);
                    Advance(.22f);Shot("boss-chain-impact.png");
                    Require(player.health==hp,"No chain damage while invulnerable, as during a dash");
                    Advance(.07f);Require(player.health==hp,"The embedded first chain stays harmless after invulnerability ends");
                    Advance(.11f);
                    Shot("boss-chain-whip.png");boss.ForcedChainAnchor=null;Advance(.6f);Shot("boss-chain-later.png");
                    Advance(boss.ChainSlashDuration-2f);Require(boss.CurrentAttack==ClockBossAttack.None,"Chain slash ends after its recovery");
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);
                    // Wait in the destination safe strip so the first contact is the immediate hit under test.
                    Place(boss.AttackDirection>0?boss.RoomRight-1:boss.RoomLeft+1);
                    var end=boss.MixerPosition(1);float gap=boss.AttackDirection>0?boss.RoomRight-end.x-6:end.x-boss.RoomLeft-6;
                    Require(Mathf.Abs(gap-ClockExecutionerBoss.MixerSafeGap)<.001f&&ClockExecutionerBoss.MixerSafeGap==1,"Mixer leaves a one-unit destination gap");
                    Require(Mathf.Abs(end.y-boss.Floor-6)<.001f,"Mixer radius and center height are six");
                    boss.Receive(1,player,1,false,new HitData{stun=2});
                    Require(boss.CurrentAttack==ClockBossAttack.Mixer&&boss.stunUntil<=arena.Now,"Mixer is stun immune");
                    Advance(.5f);Require(!boss.hurtCollider.enabled,"Departing boss cannot be hit");
                    Advance(.55f);Require(boss.MixerActive&&boss.hurtCollider.enabled,"Mixer clock becomes targetable after one second");
                    Place(boss.MixerCenter.x);hp=player.health;Advance(.02f);
                    Require(hp-player.health>=7&&hp-player.health<=8,"Mixer deals immediate random 7 or 8 damage");
                    Require(player.moveLockUntil<=arena.Now&&player.knockUntil<=arena.Now,"Mixer adds no knockback or input lock");
                    hp=player.health;Advance(.26f);Require(hp-player.health>=7&&hp-player.health<=8,"Mixer repeats at 0.25 seconds");
                    // Lunge: cruise at 5 units/s, then dash at the player but never into the far 3-unit strip.
                    var enclosure=world.GetComponent<Chapter1BossRoomEnclosure>();Place(boss.RoomLeft+5);enclosure.Hold();Place(boss.RoomLeft+.3f);enclosure.Hold();
                    Require(enclosure.Holding&&world.FixedCameraCenter==enclosure.CameraCenter,"Standing against the closed entry keeps the boss room camera");
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);float side=boss.AttackDirection;
                    Place(side>0?boss.RoomRight-.5f:boss.RoomLeft+.5f);Advance(1+ClockExecutionerBoss.MixerCruise-.02f);float cruise=boss.MixerCenter.x;
                    Advance(.3f);Require(Mathf.Abs(boss.MixerCenter.x-boss.MixerPosition(1).x)<.01f&&Mathf.Abs(boss.MixerCenter.x-cruise)>3,"Mixer lunges to its travel end without entering the safe strip");
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);side=boss.AttackDirection;float target=boss.MixerPosition(0).x+side*13;
                    Place(target);Advance(1+ClockExecutionerBoss.MixerCruise+.3f);
                    Require(Mathf.Abs(boss.MixerCenter.x-target)<.1f,"Mixer lunges onto a player ahead of it");
                    Require(boss.InkTelegraphsLoaded,"Ink telegraph frames load from Resources");
                    void Shot(string name){boss.Present();world.RefreshCamera(0,true);Capture(world.View,Path.Combine(Output,name));}
                    Require(boss.PreviewArtLoaded,"Preview boss art loads from Resources");
                    Reset();Advance(.1f);Shot("boss-preview-idle.png");
                    Require(boss.VisiblePreviewParts>=12,"Idle boss is assembled from 12 preview parts");
                    Reset();boss.ForcedFistOrigins=new[]{FistOrigin.Top,FistOrigin.Right,FistOrigin.Bottom};Place(boss.RoomLeft+10);
                    boss.BeginAttack(ClockBossAttack.LongFist);Advance(.8f);Shot("boss-preview-fist-ink-top.png");
                    Advance(.9f);Shot("boss-preview-fist-top.png");Advance(1.3f);Shot("boss-preview-fist-right.png");
                    Advance(1.3f);Shot("boss-preview-fist-bottom.png");boss.ForcedFistOrigins=null;
                    Reset();boss.BeginAttack(ClockBossAttack.Needles);Advance(1.2f);Shot("boss-preview-needles.png");
                    // The first needle stabbing into the floor, 25 frames a second from just before impact.
                    Reset();boss.BeginAttack(ClockBossAttack.Needles);Advance(ClockExecutionerBoss.NeedleWarning/boss.PatternSpeed-.12f);
                    for(int f=0;f<18;f++){Shot("boss-needle-land-"+f.ToString("00")+".png");Advance(.04f);}
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);Place(boss.AttackDirection>0?boss.RoomRight-1:boss.RoomLeft+1);Advance(.85f);Shot("boss-preview-mixer-rise.png");
                    Reset();boss.Receive(boss.maxHealth+1,player,1,false,null,true);Advance(.5f);Shot("boss-preview-death-lean.png");
                    Advance(.5f);boss.Present();Advance(.4f);Shot("boss-preview-death-plume.png");Require(boss.Dead&&boss.ActivePreviewEffects>0,"Death collapses into the dust plume");
                    Require(encounter.DeathCutscene&&player.InputBlocked,"Death outro holds the bars and blocks input while the boss collapses");
                    encounter.AdvanceCutscene(encounter.CutsceneDuration+.01f);
                    Require(!encounter.CutscenePlaying&&!player.InputBlocked,"After the outro the bars leave and the player moves again");
                    enclosure.Hold();
                    Require(enclosure.Holding&&!enclosure.EntryShut,"Once the boss falls the entry reopens so the player can leave");
                    Require(enclosure.RoomWidth==ClockExecutionerBoss.RoomWidth&&enclosure.Holding&&world.FixedCameraCenter==enclosure.CameraCenter,"Camera stays fixed on the boss room after the boss falls");
                    Reset();boss.BeginAttack(ClockBossAttack.LongFist);Advance(.3f);boss.Present();
                    Require(boss.VisibleInkCount==0,"Wall ink waits for the small fist to leave");
                    Advance(.5f);Shot("boss-ink-wall.png");Require(boss.VisibleInkCount==1,"Wall ink boils before the giant fist");
                    Reset();boss.BeginAttack(ClockBossAttack.Needles);Advance(.65f);Shot("boss-ink-needles.png");
                    Require(boss.VisibleInkCount==boss.AnnouncedNeedles&&boss.AnnouncedNeedles>=2,"Ceiling ink marks each announced needle");
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);Advance(.4f);Shot("boss-ink-mixer-trace.png");
                    Require(boss.VisibleInkCount==1,"Ink clock face traces the mixer radius");
                    Reset();boss.BeginAttack(ClockBossAttack.Mixer);Advance(2);Shot("boss-stationary-mixer.png");
                    Require(boss.VisibleInkCount==1,"Ink clock face follows the active mixer");
                    // Only the player dies: the boss drops its attack but stays drawn in place until a reset.
                    Reset();boss.BeginAttack(ClockBossAttack.ChainSlash);Advance(1.2f);
                    player.Receive(player.maxHealth+player.shield+1,boss,1,false,null,true);Advance(.5f);boss.Present();
                    Require(player.Dead&&!boss.Dead&&boss.PresentationVisible&&boss.CurrentAttack==ClockBossAttack.None&&boss.AnnouncedChainLines==0&&boss.VisiblePreviewParts>=12,"Player death leaves the boss hanging idle, not gone");
                    player.ResetActor(new Vector2(boss.RoomLeft+10,boss.Floor));
                    // The boss room is exactly one view, so the 12% combat-feel zoom is held off inside it.
                    Place(boss.RoomLeft+10);enclosure.Hold();
                    Require(!arena.FeelFeedback||arena.FeelFeedback.CombatCamera.HoldBaseFraming,"Boss room holds the camera at its full 30-unit framing");
                    // Phase one is eased to 0.85x; a third of health left, the boss drops its attack, roars behind the
                    // bars, then attacks at the base speed.
                    boss.phaseOneSpeed=ClockExecutionerBoss.DefaultPhaseOneSpeed;
                    Reset();boss.BeginAttack(ClockBossAttack.Needles);
                    Require(Mathf.Approximately(boss.PatternSpeed,ClockExecutionerBoss.DefaultPhaseOneSpeed),"Phase one patterns run at 0.85x");
                    Advance(.5f);
                    boss.Receive(boss.health-boss.EnrageHealth+1,player,1,false,null,true);
                    Require(boss.PhaseTwo&&!boss.Dead&&boss.CurrentAttack==ClockBossAttack.None&&boss.PendingHazards==0,"At a third of health the boss enters phase two and drops its attack");
                    Require(encounter.CutscenePlaying&&!encounter.DeathCutscene&&arena.Controls.Current.SimulationPaused,"Phase two roars behind the bars with combat paused");
                    Require(boss.bossBarColor==ClockExecutionerBoss.PhaseTwoBar,"Phase two health bar is purple");
                    encounter.AdvanceCutscene(encounter.barsInSeconds+.7f);Shot("boss-phase-two-roar.png");
                    encounter.AdvanceCutscene(encounter.CutsceneDuration);
                    Require(!encounter.CutscenePlaying&&!arena.Controls.Current.SimulationPaused,"Phase two roar ends and combat resumes");
                    boss.BeginAttack(ClockBossAttack.LongFist);
                    Require(Mathf.Approximately(boss.PatternSpeed,ClockExecutionerBoss.PhaseTwoSpeed),"Phase two patterns run at the base speed");
                    Reset();Require(!boss.PhaseTwo&&boss.bossBarColor==ClockExecutionerBoss.PhaseOneBar,"A reset returns to phase one");
                    boss.phaseOneSpeed=1;
                    // No fight clock (user decision 2026-10-30): no enrage at 2 minutes and no time-limit fall at 4.
                    Reset();boss.aggressive=false;Advance(250);boss.aggressive=true;boss.BeginAttack(ClockBossAttack.LongFist);
                    Require(!player.Dead&&boss.PatternSpeed==1,"No time limit or enrage: after 250 seconds the player stands and patterns keep their speed");
                }
                File.WriteAllText(Path.Combine(Output,"report.txt"),"PASS\n"+string.Join("\n",checks));
                Debug.Log("CLOCK_BOSS_VERIFY_PASS checks="+checks.Count);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Output,"report.txt"),"FAIL\n"+string.Join("\n",checks)+"\n"+e);Debug.LogException(e);}
            finally
            {
                // Replay the entry in real time: Retry puts the player inside and the encounter starts it.
                var encounter=world.GetComponent<ClockExecutionerEncounter>();
                if(thirdShotAt>0){}
                else if(encounter){encounter.Retry();introShot=0;introLogged=false;introStart=EditorApplication.timeSinceStartup;}else Finish();
            }
        }
        static void CaptureThird()
        {
            thirdShotAt=0;SessionState.SetString(ModeKey,"");
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            if(world)
            {
                var boss=world.GetComponent<ClockExecutionerEncounter>().Boss;
                File.WriteAllText(Path.Combine(Output,"third-report.txt"),"boss health "+boss.health.ToString("0")+" / "+boss.maxHealth.ToString("0")+" phaseTwo="+boss.PhaseTwo);
                Capture(world.View,Path.Combine(Output,"boss-third-world.png"));CaptureWithGui(world.View,Path.Combine(Output,"boss-third-gui.png"));
            }
            Finish();
        }
        // The play GUI is an overlay canvas, which a camera render does not include: draw it into the same 960x540
        // texture through the camera, as TemporaryGuiVerification does. The native-world canvas (negative order) stays out.
        static void CaptureWithGui(Camera camera,string path)
        {
            var canvases=Array.FindAll(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay&&c.sortingOrder>=0&&c.gameObject.activeInHierarchy);
            var previous=camera.targetTexture;var active=RenderTexture.active;var viewport=camera.rect;camera.rect=new Rect(0,0,1,1);
            var target=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+1;c.SendMessage("FitToWorld",SendMessageOptions.DontRequireReceiver);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;c.SendMessage("FitToWorld",SendMessageOptions.DontRequireReceiver);}
                camera.targetTexture=previous;camera.rect=viewport;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);
            }
        }
        static void CaptureIntro()
        {
            if(!EditorApplication.isPlaying){introStart=0;return;}
            var world=UnityEngine.Object.FindAnyObjectByType<ForestCombatWorld>();
            double age=EditorApplication.timeSinceStartup-introStart;
            var encounter=world?world.GetComponent<ClockExecutionerEncounter>():null;
            if(encounter&&encounter.IntroPlaying&&!introLogged)
            {
                introLogged=true;float x=world.arena.Player.Position.x;
                File.AppendAllText(Path.Combine(Output,"report.txt"),"\nENTRY bars start at t="+age.ToString("0.00")+"s with the player at x="+(x-encounter.Room.x).ToString("0.00")+" (target "+(encounter.WalkInTargetX-encounter.Room.x).ToString("0.00")+")");
            }
            if(world&&introShot<IntroShots.Length&&age>=IntroShots[introShot])
                Capture(world.View,Path.Combine(Output,"boss-entry-"+(introShot++)+"-"+IntroShots[introShot-1].ToString("0.00")+"s.png"));
            if(introShot>=IntroShots.Length||age>10){introStart=0;Finish();}
        }
        static void Finish()
        {
            EditorApplication.ExitPlaymode();
            // Headless runs (-batchmode without -quit) close themselves once the report is written.
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
        static void Capture(Camera camera,string path)
        {
            // Batchmode reports a 4:3 screen, so the game's letterbox would widen the 16:9 capture; render the full 960x540 view.
            var previous=camera.targetTexture;var active=RenderTexture.active;var viewport=camera.rect;camera.rect=new Rect(0,0,1,1);
            var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=previous;camera.rect=viewport;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
