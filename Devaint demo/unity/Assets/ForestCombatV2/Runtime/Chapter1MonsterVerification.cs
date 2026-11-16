using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ForestCombatV2
{
    // Opt-in verification uses the actual actor/controller and restores the play state.
    public sealed class Chapter1MonsterVerification : MonoBehaviour
    {
        [Serializable] public class Check { public string name,evidence;public bool pass; }
        [Serializable] public class Report { public bool complete,pass;public List<Check> checks=new List<Check>();public List<string> errors=new List<string>(); }
        Report report;string output;ForestCombatWorld world;
        bool restoreInput;
        readonly Dictionary<TestEnemy,bool> originalAggression=new Dictionary<TestEnemy,bool>();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-verifyChapter1Monsters");
            if(i<0 || i+1>=args.Length)return;
            output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(output);report=new Report();
            output=Path.Combine(output,"hurt-heavy-ui");Directory.CreateDirectory(output);
            Application.logMessageReceived+=OnLog;StartCoroutine(Guarded());
        }
        void OnLog(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception) { report.errors.Add(message+"\n"+stack);Save(); } }
        void OnDestroy() { Application.logMessageReceived-=OnLog; }
        void Save() { File.WriteAllText(Path.Combine(output,"monster-runtime.json"),JsonUtility.ToJson(report,true)); }
        void CheckThat(bool pass,string name,string evidence="") { report.checks.Add(new Check{name=name,pass=pass,evidence=evidence});Save();if(!pass)throw new InvalidOperationException(name+": "+evidence); }
        IEnumerator Guarded()
        {
            var routine=Run();
            try { while(true) { bool more;object current;try { more=routine.MoveNext();current=more?routine.Current:null; }catch(Exception e) { report.errors.Add(e.ToString());Save();yield break; }if(!more)break;yield return current; } }
            finally { RestoreControls(); }
        }
        void RestoreControls()
        {
            if(!restoreInput || !world || !world.arena || !world.arena.Player)return;
            var arena=world.arena;
            arena.Player.ClearInputState();arena.Player.UseTestInput=false;
            foreach(var pair in originalAggression)if(pair.Key)pair.Key.aggressive=pair.Value;
            restoreInput=false;
        }
        void OnDisable() { RestoreControls(); }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var camera=world.View;var old=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24);camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
            camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
            if(name.EndsWith("_idle")) { ScreenCapture.CaptureScreenshot(Path.Combine(output,name+"_hud.png"));yield return null; }
        }
        IEnumerator Run()
        {
            world=GetComponent<ForestCombatWorld>();while(!world.Ready)yield return null;
            var arena=world.arena;var player=arena.Player;
            foreach(var enemy in arena.enemies)originalAggression[enemy]=enemy.aggressive;
            restoreInput=true;player.UseTestInput=true;
            player.axis=1;player.specialHeld=true;player.ResetPlayer();
            CheckThat(player.axis==0 && !player.specialHeld,"Reset clears held movement and special input");
            var animations=world.GetComponentsInChildren<Chapter1MonsterPresentation>();
            CheckThat(world.monsterAnimations.clips.Length==25 && world.monsterAnimations.clips.Sum(c=>c.frames.Length)==130,"25 clips and 130 original frames imported");
            CheckThat(animations.Select(a=>a.species).Distinct().Count()==6,"All six supplied species have live map actors");
            CheckThat(animations.Length==arena.enemies.Count && world.Data.actors.Length==arena.enemies.Count && world.Data.monsters.All(k=>world.monsterAnimations.Find(k.id,"move")!=null),"Only approved animated monsters remain in the map",arena.enemies.Count+" actors; no rejected static fallback species");
            foreach(var enemy in arena.enemies)enemy.aggressive=false;
            var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editorInput=settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard=null;
            try
            {
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard=InputSystem.AddDevice<Keyboard>();player.UseTestInput=false;
                var adapter=player.GetComponent<CombatKeyboardInput>();float x=player.Position.x;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));InputSystem.Update();keyboard.MakeCurrent();adapter.SendMessage("Update");
                yield return new WaitForSeconds(.12f);
                CheckThat(player.axis==1 && player.facing==1 && player.Position.x>x+.1f && !player.currentCast,"D moves right without triggering an attack");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();adapter.SendMessage("Update");x=player.Position.x;
                yield return new WaitForSeconds(.12f);
                CheckThat(player.axis==0 && Mathf.Abs(player.Position.x-x)<.01f,"Releasing right immediately stops horizontal movement");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow));InputSystem.Update();adapter.SendMessage("Update");x=player.Position.x;
                yield return new WaitForSeconds(.12f);
                CheckThat(player.axis==-1 && player.facing==-1 && player.Position.x<x-.1f,"A moves and faces left");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow,Key.RightArrow));InputSystem.Update();adapter.SendMessage("Update");
                CheckThat(player.axis==0,"A and D held together cancel each other");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));InputSystem.Update();adapter.SendMessage("Update");CheckThat(player.axis==1,"Right arrow remains a movement alias");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow));InputSystem.Update();adapter.SendMessage("Update");CheckThat(player.axis==-1,"Left arrow remains a movement alias");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.Update();adapter.SendMessage("Update");yield return new WaitForSeconds(.04f);
                CheckThat(player.currentCast && player.currentCast.isBasic,"Q triggers weapon 1 basic attack");player.ResetPlayer();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));InputSystem.Update();adapter.SendMessage("Update");yield return new WaitForSeconds(.04f);
                CheckThat(player.currentCast && !player.currentCast.isBasic && player.specialHeld,"E triggers and holds weapon 1 skill");player.ResetPlayer();
                player.axis=1;player.specialHeld=true;adapter.SendMessage("OnApplicationFocus",false);
                CheckThat(player.axis==0 && !player.specialHeld,"Focus loss clears held controls");
                player.axis=-1;adapter.SendMessage("OnApplicationPause",true);CheckThat(player.axis==0,"Pause clears held controls");
                player.axis=1;adapter.enabled=false;CheckThat(player.axis==0,"Disabling keyboard adapter clears held controls");adapter.enabled=true;
            }
            finally
            {
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editorInput;
                player.ClearInputState();player.UseTestInput=true;player.ResetPlayer();
            }
            CheckThat(Mathf.Approximately(arena.EnemyHitReaction.BasicStaggerSeconds,.3f),"Approved enemy stagger is compiled from the current combat definition");
            foreach(string species in animations.Select(a=>a.species).Distinct())
            {
                var animation=animations.First(a=>a.species==species);var actor=animation.actor;
                player.ResetPlayer();actor.ResetEnemy();player.Position=actor.Position+Vector2.left*7;
                player.velocity=Vector2.zero;actor.velocity=Vector2.zero;Physics2D.SyncTransforms();world.RefreshCamera(0,true);
                actor.aggressive=true;float start=arena.Now;var frames=new HashSet<int>();float x=actor.Position.x;
                while(arena.Now<start+.42f) { yield return new WaitForEndOfFrame();if(animation.State=="move")frames.Add(animation.Frame); }
                CheckThat(frames.Count>=2 && Mathf.Abs(actor.Position.x-x)>.2f,species+" actual movement advances sprites",frames.Count+" frames, delta="+(actor.Position.x-x));
                yield return Capture(species+"_move");
                player.Position=actor.Position+Vector2.left*(actor.size.x*.5f+.8f);player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                start=arena.Now;while(actor.Telegraph<0 && arena.Now<start+2)yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();
                CheckThat(actor.Telegraph>=0 && animation.State=="attack",species+" actual combat enters attack clip");
                yield return Capture(species+"_attack");
                start=arena.Now;frames.Clear();while(actor.Telegraph>=0 && arena.Now<start+2) { yield return new WaitForEndOfFrame();if(animation.State=="attack")frames.Add(animation.Frame); }
                CheckThat(frames.Count>=3,species+" attack animation advances",frames.Count+" frames");
                actor.aggressive=false;actor.Receive(1,player,1,true,null);yield return new WaitForEndOfFrame();
                CheckThat(animation.State=="hurt",species+" accepted hit shows hurt sprite");
                actor.aggressive=true;float hitAt=arena.Now;
                player.Position=actor.Position+Vector2.left*7;Physics2D.SyncTransforms();
                yield return new WaitForSeconds(.14f);float restX=actor.Position.x;
                yield return new WaitForSeconds(.07f);
                CheckThat(animation.State=="hurt" && Mathf.Abs(actor.Position.x-restX)<.01f,species+" hit pose and movement pause persist after knockback",(arena.Now-hitAt)+"s / delta="+(actor.Position.x-restX));
                yield return Capture(species+"_hurt_hold");
                actor.Receive(1,player,1,true,null);float again=arena.Now;
                yield return new WaitForSeconds(.2f);
                CheckThat(animation.State=="hurt" && arena.Now<actor.staggerUntil,species+" repeated hit refreshes pause");
                yield return new WaitForSeconds(.2f);
                CheckThat(animation.State!="hurt" && arena.Now>=actor.staggerUntil,species+" movement resumes after recovery");
                actor.aggressive=false;actor.Receive(10000,player,1,false,null);
                yield return new WaitForEndOfFrame();CheckThat(actor.Dead && animation.State=="death",species+" actual death starts nonlooping clip");
                yield return new WaitForSeconds(.22f);yield return Capture(species+"_death");
                yield return new WaitForSeconds(.65f);CheckThat(!actor.visual.enabled,species+" death disappears after final transparent frame");
                actor.ResetEnemy();yield return new WaitForEndOfFrame();CheckThat(actor.visual.enabled && animation.State!="death",species+" reset restores renderer");
            }
            foreach(string form in new[]{"hammer","shield","greatsword"})
            {
                foreach(var actor in arena.enemies){actor.ResetEnemy();actor.aggressive=false;}
                player.ResetPlayer();int index=Array.FindIndex(arena.catalog.forms,f=>f.id==form);
                player.Reconfigure(0,index,0);yield return new WaitForEndOfFrame();
                CheckThat(arena.Presentation.CurrentForm==form && world.diagnostics.GetWeaponIcon(form)!=null,form+" approved appearance and UI icon loaded");
                CheckThat(player.visual.sprite.pixelsPerUnit==32 && player.visual.transform.localPosition==Vector3.zero,form+" native pixel scale and foot pivot preserved");
                yield return Capture(form+"_idle");
                player.axis=1;yield return new WaitForSeconds(.12f);player.axis=0;yield return Capture(form+"_run");
                player.QueueTestInput(basic:true);yield return new WaitForSeconds(.18f);
                CheckThat(arena.Presentation.CurrentForm==form && arena.Presentation.CurrentClip.StartsWith("attack"),form+" actual S attack uses its own sprite set");
                yield return Capture(form+"_attack");yield return new WaitForSeconds(1.4f);
                var set=arena.characterAnimations.sets.First(s=>s.form==form);
                for(int direction=-1;direction<=1;direction+=2)
                {
                    player.ResetPlayer();player.SyncFacing(direction);yield return new WaitForFixedUpdate();
                    world.RefreshCamera(0,true);player.QueueTestInput(basic:true);
                    for(int hit=0;hit<player.Form.basics.Length;hit++)
                    {
                        float limit=arena.Now+1;
                        while(!player.currentCast && arena.Now<limit)yield return new WaitForFixedUpdate();
                        var cast=player.currentCast;var clip=set.Find("attack"+(hit+1));var effect=set.Effect(clip.name);
                        CheckThat(cast && player.BasicIndex==hit,form+" queued combo enters hit "+(hit+1)+" facing "+direction);
                        CheckThat(effect!=null && effect.sprites.All(s=>s) && clip.fxStart>=0 && clip.sourceImpact>0,form+" hit "+(hit+1)+" approved effect and contact loaded");
                        CheckThat(Mathf.Abs(cast.Definition.End-clip.Duration)<.001f && Mathf.Abs(cast.ImpactTime-clip.sourceImpact)<.001f,form+" hit "+(hit+1)+" runs at approved native timing",cast.Definition.End+"s / contact="+cast.ImpactTime);
                        bool emitted=false,queued=false,captured=false;
                        var poses=new HashSet<Sprite>();float began=arena.Now;
                        while(player.currentCast==cast && arena.Now<began+1.5f)
                        {
                            yield return new WaitForEndOfFrame();
                            if(player.visual.sprite && cast.Age<cast.ImpactTime)poses.Add(player.visual.sprite);
                            if(arena.Presentation.LastEffectHandle!=null && arena.Presentation.LastEffectHandle.Occurrence.ExecutionId==cast.ExecutionId)emitted=true;
                            if(!queued && hit+1<player.Form.basics.Length && cast.Age>.08f) { queued=true;player.QueueTestInput(basic:true); }
                            if(!captured && cast.Age>=cast.ImpactTime+.04f) { captured=true;yield return Capture(form+"_hit"+(hit+1)+"_"+(direction<0?"left":"right")); }
                        }
                        CheckThat(emitted && arena.Presentation.LastEffectError==null,form+" hit "+(hit+1)+" emits a live effect facing "+direction,arena.Presentation.LastEffectError);
                        if(form=="greatsword" && hit==2)CheckThat(poses.Count>=2,"Greatsword third hit advances during anticipation facing "+direction,poses.Count+" poses before impact");
                    }
                    yield return new WaitForSeconds(.35f);
                }
            }
            player.Reconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="base_sword"),0);
            player.ResetPlayer();player.axis=0;float y=player.Position.y;player.QueueTestInput(jump:true);yield return new WaitForSeconds(.25f);
            CheckThat(player.Position.y>y+2,"Existing player jump still works");
            player.QueueTestInput(jump:true);yield return new WaitForSeconds(.25f);CheckThat(player.Position.y>y+5,"Existing second jump still works");
            yield return Capture("player_double_jump");
            player.ResetPlayer();player.UseTestInput=false;
            foreach(var actor in arena.enemies) { actor.ResetEnemy();actor.aggressive=true; }
            world.RefreshCamera(0,true);report.complete=true;report.pass=report.errors.Count==0&&report.checks.All(c=>c.pass);Save();
            Debug.Log("CHAPTER1_MONSTERS_VERIFIED "+report.checks.Count+" checks; pass="+report.pass);
        }
    }
}
