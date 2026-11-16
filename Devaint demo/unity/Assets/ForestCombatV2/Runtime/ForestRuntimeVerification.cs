using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace ForestCombatV2
{
    // Runs only when this locally built player receives --verify-forest DIR.
    // All movement/hits below use the actual merged controller and colliders.
    public sealed class ForestRuntimeVerification : MonoBehaviour
    {
        [Serializable] public class Check { public string name,evidence; public bool pass; }
        [Serializable] public class Report { public bool pass; public string unityVersion,scene; public List<Check> checks=new List<Check>();public List<string> errors=new List<string>(); }
        [Serializable] public class JumpPlan { public int from,to; public float start,target,second; public bool drop,walk; }
        [Serializable] class Navigation { public bool pass;public float exitX,exitY;public List<JumpPlan> jumps; }
        Report report;ForestCombatWorld world;CombatArena arena;string folder;
        CombatArena.ManualSimulation navigation;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--verify-forest");if(i<0||i+1>=args.Length)return;
            folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(folder);report=new Report{unityVersion=Application.unityVersion,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name};
            Application.logMessageReceived+=OnLog;StartCoroutine(Guarded());
        }
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception) {report.errors.Add(message+"\n"+stack);Save();}}
        void Save(){File.WriteAllText(Path.Combine(folder,"runtime-verification.json"),JsonUtility.ToJson(report,true));}
        void CheckThat(bool pass,string name,string evidence="")
        {
            report.checks.Add(new Check{name=name,pass=pass,evidence=evidence});Save();
            if(!pass)throw new InvalidOperationException(name+": "+evidence);
        }
        IEnumerator Guarded()
        {
            var routine=Run();while(true){object current=null;bool more=false;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception e){report.errors.Add(e.ToString());Save();Application.Quit(1);yield break;}if(!more)break;yield return current;}
        }
        IEnumerator Advance(float seconds){float end=arena.Now+seconds;while(arena.Now<end)yield return new WaitForFixedUpdate();}
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var camera=world.View;var old=camera.targetTexture;var rect=camera.rect;var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);var active=RenderTexture.active;
            camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);
        }
        IEnumerator Run()
        {
            world=GetComponent<ForestCombatWorld>();while(!world.Ready)yield return null;arena=world.arena;var p=arena.Player;p.UseTestInput=true;
            foreach(var e in arena.enemies)e.aggressive=false;
            CheckThat(world.Data.width==20480&&world.Data.platforms.Length==38&&world.SurfaceCount==73,"Applied forest geometry installed",world.SurfaceCount+" collider rectangles; 38 foliage platforms");
            CheckThat(arena.catalog.forms.Length==10&&arena.characterAnimations.sets.Length>=4,"Merged R16 catalog and approved character resources loaded");
            if(Array.Exists(world.Data.layers,l=>l.id=="clouds"))
            {
                CheckThat(world.Data.layers.Length==5,"Sky, sun, clouds, fields and trees are separate layers");
                var sun=Array.Find(world.Data.layers,l=>l.id=="sun");
                CheckThat(sun!=null&&sun.single,"Only one independent sun is rendered");
                yield return Advance(.5f);world.RefreshCamera(0,true);
                var cloud=world.transform.Find("clouds");float cloudX=cloud.position.x*32-world.CameraX;
                yield return Capture("00_clouds_start");yield return Advance(1);
                float drift=cloud.position.x*32-world.CameraX-cloudX;
                CheckThat(drift>1&&drift<6,"Clouds drift slowly independently of player input",drift+" screen pixels per second");
                CheckThat(Array.TrueForAll(world.Data.layers,l=>l.maxVertical-l.minVertical<=10),"Background vertical movement remains at most ten pixels");
                yield return Capture("00_clouds_moved");
                if(Array.Exists(world.Data.layers,l=>l.coverBottom))
                {
                    int highest=0;for(int index=0;index<world.Data.platforms.Length;index++)if(world.Data.platforms[index].y<world.Data.platforms[highest].y)highest=index;
                    for(int index=0;index<world.Data.platforms.Length;index++)
                    {
                        var ledge=world.Data.platforms[index];Place((ledge.x+ledge.w*.5f)/32,-ledge.y/32f);world.RefreshCamera(0,true);
                        CheckThat(world.BackgroundCoversBottom(out string detail),"Painted background covers screen at platform "+(index+1),detail);
                        if(index==0||index==highest)yield return Capture(index==0?"12_first_platform_edges":"13_highest_platform");
                    }
                    var high=world.Data.platforms[highest];Place((high.x+high.w*.5f)/32,-high.y/32f);world.RefreshCamera(0,true);float highestFloor=p.Position.y;
                    p.QueueTestInput(jump:true);yield return Advance(.28f);p.QueueTestInput(jump:true);yield return Advance(.3f);
                    bool covered=world.BackgroundCoversBottom(out string apexDetail);
                    CheckThat(p.Position.y>highestFloor+5.5f&&covered,"Highest platform double-jump exposes no background cutoff",apexDetail+" jump="+(p.Position.y-highestFloor));
                    yield return Capture("14_highest_double_jump");
                    p.ResetPlayer();p.axis=0;world.RefreshCamera(0,true);yield return Advance(.1f);
                }
            }
            yield return Advance(.1f);float sx=p.Position.x;p.axis=1;yield return Advance(.4f);p.axis=0;
            CheckThat(Mathf.Abs(p.Position.x-sx-arena.Movement.RunSpeed*.4f)<.15f,"Actual controller walks at merged movement profile speed",p.Position.ToString());yield return Capture("01_run");
            p.ResetPlayer();p.axis=0;float floor=p.Position.y,top=floor;p.QueueTestInput(jump:true);
            float start=arena.Now;while(arena.Now<start+.32f){yield return new WaitForFixedUpdate();top=Mathf.Max(top,p.Position.y);}
            CheckThat(Mathf.Abs(top-floor-3)<.12f,"First jump retains R16 height3",(top-floor).ToString());yield return Capture("02_first_jump");
            p.QueueTestInput(jump:true);start=arena.Now;while(arena.Now<start+.34f){yield return new WaitForFixedUpdate();top=Mathf.Max(top,p.Position.y);}
            CheckThat(top-floor>5.7f&&top-floor<6.15f,"Second jump works in the forest",(top-floor).ToString());yield return Capture("03_double_jump");yield return Advance(.8f);
            CheckThat(p.grounded&&Mathf.Abs(p.Position.y-floor)<.08f,"Landing returns feet to tile baseline",p.Position.ToString());
            p.ResetPlayer();p.axis=1;sx=p.Position.x;p.QueueTestInput(dash:true);yield return Advance(.09f);yield return Capture("04_dash");yield return Advance(.13f);p.axis=0;
            CheckThat(p.Position.x-sx>=2.9f&&p.Position.x-sx<3.3f,"Merged dash moves through the new map",(p.Position.x-sx).ToString());
            p.ResetPlayer();p.axis=0;var target=arena.enemies[0];target.Position=p.Position+Vector2.right*1.65f;target.velocity=Vector2.zero;Physics2D.SyncTransforms();float hp=target.health;
            p.QueueTestInput(basic:true);yield return Advance(.26f);CheckThat(target.health<hp,"S basic attack applies actual combat damage",hp+" -> "+target.health);yield return Capture("05_basic_hit");
            float after=target.health;yield return Advance(.10f);CheckThat(target.health==after,"Basic hit does not repeat across active frames");yield return Advance(.55f);
            target.health=target.maxHealth;target.Position=p.Position+Vector2.right*1.65f;Physics2D.SyncTransforms();hp=target.health;p.QueueTestInput(special:true);yield return Advance(.8f);
            CheckThat(target.health<hp,"W special uses merged skill/hit logic",hp+" -> "+target.health);yield return Capture("06_special");yield return Advance(.5f);
            int whip=Array.FindIndex(arena.catalog.forms,f=>f.id=="whip");p.Reconfigure(1,whip,0);p.QueueTestInput(swap:true);yield return Advance(.08f);CheckThat(p.Form.id=="whip","Q swaps to approved whip character");
            p.QueueTestInput(basic:true);yield return Advance(.25f);yield return Capture("07_whip");yield return Advance(1);
            p.CancelAction();p.health=p.maxHealth;target.health=target.maxHealth;target.profile=0;target.Position=p.Position+Vector2.right*1.7f;target.velocity=Vector2.zero;target.aggressive=true;Physics2D.SyncTransforms();yield return Advance(1.2f);
            CheckThat(p.health<p.maxHealth,"Enemy AI attacks and damages player",p.health.ToString());target.aggressive=false;
            p.ResetPlayer();var platform=world.Data.platforms[0];p.Position=new Vector2((platform.x+platform.w/2f)/32,-platform.y/32f);p.velocity=Vector2.zero;Physics2D.SyncTransforms();p.grounded=p.ProbeGround();floor=p.Position.y;
            CheckThat(p.grounded&&p.DropThrough(),"Down+Space drop-through enters foliage platform");yield return Advance(.22f);CheckThat(p.Position.y<floor-.5f,"Drop-through clears the platform without sticking");yield return Capture("08_drop_through");
            foreach(var e in arena.enemies)e.aggressive=false;p.CancelAction();
            for(int i=0;i<world.Data.views.Length;i++){world.Warp(i);yield return Advance(.18f);CheckThat(p.grounded,"Area "+(i+1)+" has a safe landing",p.Position.ToString());if(i==2||i==6)yield return Capture(i==2?"09_branch_cave":"10_climbing_ridge");}
            using (navigation=arena.AcquireManualSimulation())
            {
                try { RunNavigation(); }
                finally { navigation=null; }
            }
            arena.ResetTest();p.UseTestInput=false;p.axis=0;foreach(var e in arena.enemies)e.aggressive=true;world.RefreshCamera(0,true);yield return Advance(.15f);yield return Capture("11_play_ready");
            report.pass=report.errors.Count==0&&report.checks.TrueForAll(c=>c.pass);Save();Debug.Log("FOREST_RUNTIME_VERIFIED "+report.checks.Count+" checks");Application.Quit(report.pass?0:1);
        }
        void Tick(float dt=1f/120)
        {navigation.StepPending(dt);world.RefreshCamera(dt);}
        void Place(float x,float y)
        {var p=arena.Player;p.ResetPlayer();p.Position=new Vector2(x,y);p.velocity=Vector2.zero;p.axis=0;Physics2D.SyncTransforms();p.grounded=p.ProbeGround();}
        bool TryArc(RouteEdge edge,float launch,float targetX,float second,out JumpPlan result)
        {
            result=null;var from=world.Data.nodes[edge.from];var to=world.Data.nodes[edge.to];var p=arena.Player;Place(launch,-from.y/32);
            if(p.Blocked(p.Position,p.facing))return false;
            bool drop=edge.second==-2,walk=second==-3;if(drop){if(!p.DropThrough())return false;}else if(!walk)p.QueueTestInput(jump:true);
            bool secondUsed=false;
            for(int i=0;i<250;i++)
            {
                p.axis=Mathf.Abs(p.Position.x-targetX)<.045f?0:p.Position.x<targetX?1:-1;
                if(!drop&&second>=0&&!secondUsed&&i/120f>=second){p.QueueTestInput(jump:true);secondUsed=true;}
                Tick();
                if(p.grounded){if(Mathf.Abs(p.Position.y+to.y/32)<.08f&&p.Position.x>=to.a/32-.2f&&p.Position.x<=to.b/32+.2f){result=new JumpPlan{from=edge.from,to=edge.to,start=launch,target=targetX,second=second,drop=drop,walk=walk};return true;}if(!walk)return false;}
                if(p.Position.y<-70)return false;
            }
            return false;
        }
        JumpPlan FindArc(RouteEdge edge)
        {
            var from=world.Data.nodes[edge.from];var to=world.Data.nodes[edge.to];
            float target=Mathf.Clamp(edge.start/32,to.a/32+.7f,to.b/32-.7f),near=Mathf.Clamp(target,from.a/32+.7f,from.b/32-.7f);
            if(to.y>=from.y && (Mathf.Abs(to.a-from.b)<.1f||Mathf.Abs(to.b-from.a)<.1f))
                if(TryArc(edge,near,target,-3,out JumpPlan walking))return walking;
            foreach(float launch in new[]{edge.start/32,near,from.a/32+.8f,from.b/32-.8f,(from.a+from.b)/64})
            foreach(float aim in new[]{target,(to.a+to.b)/64,to.a/32+.8f,to.b/32-.8f})
            foreach(float second in new[]{-1f,.12f,.20f,.28f,.36f})
                if(TryArc(edge,launch,aim,second,out JumpPlan result))return result;
            return null;
        }
        void WalkTo(float x)
        {
            var p=arena.Player;for(int i=0;i<16000&&Mathf.Abs(p.Position.x-x)>.0001f;i++)
            {p.axis=p.Position.x<x?1:-1;float dt=Mathf.Min(1f/120,Mathf.Abs(p.Position.x-x)/arena.Movement.RunSpeed);Tick(dt);}
            p.axis=0;CheckThat(Mathf.Abs(p.Position.x-x)<.005f,"Continuous route reaches its launch point",p.Position+" -> "+x);
        }
        void RunNavigation()
        {
            // Use the actual MonoBehaviour physics against the loaded forest's
            // collider rectangles, not a replacement JavaScript approximation.
            var saved=new List<TestEnemy>(arena.enemies);foreach(var e in saved)e.gameObject.SetActive(false);arena.enemies.Clear();arena.ResetTest();arena.Player.UseTestInput=true;
            var plans=new List<JumpPlan>();
            foreach(var edge in world.Data.route)
            {
                var plan=FindArc(edge);
                if(plan!=null){plans.Add(plan);CheckThat(true,"R16 traverses route link "+edge.from+" -> "+edge.to);continue;}
                // The faster R16 falling arc may touch an intermediate terrace
                // that the old web-preview trajectory jumped over. Walk each
                // real descending terrace instead of forcing that old shortcut.
                var current=world.Data.nodes[edge.from];var goal=world.Data.nodes[edge.to];var chain=new List<JumpPlan>();
                for(int limit=0;limit<16&&current.id!=goal.id&&goal.y>=current.y;limit++)
                {
                    RouteNode next=null;bool right=goal.a>=current.b;
                    foreach(var n in world.Data.nodes)
                        if(n.id!=current.id&&n.y>=current.y&&n.y<=goal.y&&(right?Mathf.Abs(n.a-current.b)<.1f&&n.a<=goal.a:Mathf.Abs(n.b-current.a)<.1f&&n.b>=goal.b))
                            if(next==null||n.y<next.y)next=n;
                    if(next==null)break;
                    var link=FindArc(new RouteEdge{from=current.id,to=next.id,start=(right?current.b-24:current.a+24),speed=right?170:-170,second=-1});
                    if(link==null)break;chain.Add(link);current=next;
                }
                CheckThat(current.id==goal.id,"R16 follows intermediate terraces "+edge.from+" -> "+edge.to);plans.AddRange(chain);
            }
            Place(world.Data.spawn.x/32,-world.Data.spawn.y/32);
            foreach(var plan in plans)
            {
                WalkTo(plan.start);var p=arena.Player;var target=world.Data.nodes[plan.to];
                if(plan.drop)CheckThat(p.DropThrough(),"Continuous route drops through shaft cap");else if(!plan.walk)p.QueueTestInput(jump:true);
                bool second=false,landed=false;
                for(int i=0;i<250;i++){p.axis=Mathf.Abs(p.Position.x-plan.target)<.045f?0:p.Position.x<plan.target?1:-1;if(!plan.drop&&plan.second>=0&&!second&&i/120f>=plan.second){p.QueueTestInput(jump:true);second=true;}Tick();if(p.grounded&&(!plan.walk||Mathf.Abs(p.Position.y+target.y/32)<.08f&&p.Position.x>=target.a/32-.2f&&p.Position.x<=target.b/32+.2f)){landed=true;break;}}
                CheckThat(landed&&Mathf.Abs(p.Position.y+target.y/32)<.08f&&p.Position.x>=target.a/32-.2f&&p.Position.x<=target.b/32+.2f,"Continuous R16 route lands on link "+plan.to,p.Position.ToString());
            }
            WalkTo(638);var player=arena.Player;CheckThat(player.Position.x>=638-.01f,"Continuous start-to-exit traversal completed without wall climbing",player.Position.ToString());
            File.WriteAllText(Path.Combine(folder,"unity-navigation.json"),JsonUtility.ToJson(new Navigation{pass=true,exitX=player.Position.x,exitY=player.Position.y,jumps=plans},true));
            foreach(var e in saved){e.gameObject.SetActive(true);arena.enemies.Add(e);}arena.Player.axis=0;
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
