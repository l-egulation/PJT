using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Explicit command-line verification only. Normal game launches do not alter encounters.
    public sealed class Stage1VillageMonsterReview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BackgroundReview()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-stage1VillageCaptureRoot")>=0)
                Application.runInBackground=true;
        }
        [Serializable] sealed class Result
        {
            public string result,unity,error;
            public int monsters,elite,shots,hookClaws;
            public bool geometryPreserved,noBoss,allMotions,meleeHits,projectileHits,hookHit,hookMiss,interrupts,mirrored,reset;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-stage1VillageCaptureRoot");
            if(arg<0 || arg+1>=args.Length)yield break;
            Application.runInBackground=true;
            string root=Path.GetFullPath(args[arg+1]);Directory.CreateDirectory(root);
            var world=GetComponent<ForestCombatWorld>();var art=GetComponent<VillagePresentation>();
            float deadline=Time.realtimeSinceStartup+100;
            while((!world.Ready || !art.Ready) && Time.realtimeSinceStartup<deadline)yield return null;
            var result=new Result {unity=Application.unityVersion};
            try { Verify(world,result); }
            catch(Exception error) { result.error=error.ToString();Debug.LogException(error); }
            if(result.error==null)
            {
                // Real village camera/display, rather than a standalone sprite render.
                world.Warp(1);world.RefreshCamera(0,true);art.RefreshForCapture();
                foreach(var e in world.arena.enemies)e.aggressive=false;
                yield return null;yield return null;
                yield return new WaitForEndOfFrame();
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(root,"village-monsters.png"),image.EncodeToPNG());Destroy(image);
                var elite=world.arena.enemies.OfType<FieldMouseEnemy>().First(e=>e.Definition.id=="04_well-warden");
                var marker=world.Data.landmarks.First(l=>l.kind=="elite");
                world.Warp(marker.zone);world.RefreshCamera(0,true);art.RefreshForCapture();
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(root,"village-elite.png"),image.EncodeToPNG());Destroy(image);
            }
            result.result=result.error==null?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(root,"validation.json"),JsonUtility.ToJson(result,true));
            Debug.Log("STAGE1_VILLAGE_REVIEW_"+result.result+" "+JsonUtility.ToJson(result));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(result.error==null?0:1);
#else
            Application.Quit(result.error==null?0:1);
#endif
        }
        static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        public static string VerifyLogic(ForestCombatWorld world)
        {
            var result=new Result {unity=Application.unityVersion};
            Verify(world,result);result.result="PASS";return JsonUtility.ToJson(result,true);
        }
        static void Verify(ForestCombatWorld world,Result report)
        {
            Check(world && world.Ready,"Village game did not initialize.");
            var arena=world.arena;var content=world.GetComponent<Stage1VillageMonsterContent>();
            var original=Chapter1ProceduralMap.Generate(JsonUtility.FromJson<ForestData>(world.worldJson.text),
                JsonUtility.FromJson<RoundedTerrainLayout>(world.roundedTerrainJson.text),world.Data.generationSeed);
            report.geometryPreserved=original.ground.SequenceEqual(world.Data.ground) && original.platforms.Length==world.Data.platforms.Length;
            Check(report.geometryPreserved,"Monster content changed map geometry.");
            var enemies=arena.enemies.OfType<FieldMouseEnemy>().Where(e=>!(e is Chapter1PrototypeEnemy)).ToArray();
            report.monsters=enemies.Length;report.elite=enemies.Count(e=>e.Definition.id=="04_well-warden");
            report.noBoss=enemies.All(e=>!e.isBoss && e.Definition.id!="05_clock-executioner") && world.Data.monsters.Length==content.Data.monsters.Length;
            Check(report.noBoss && report.elite>0,"The village species and an elite, with no boss, are required.");
            foreach(var e in enemies) { e.aggressive=false;e.CancelAttack(); }
            arena.Player.UseTestInput=true;
            var originalSpawns=enemies.Select(e=>e.spawn).ToArray();
            var floor=new GameObject("Temporary monster attack verification floor");
            floor.transform.position=new Vector3(-500,-.5f,0);floor.AddComponent<ArenaSurface>();
            floor.AddComponent<BoxCollider2D>().size=new Vector2(100,1);Physics2D.SyncTransforms();
            foreach(var index in enemies.Select(e=>e.SurfaceIndex).Distinct())index.Rebuild(UnityEngine.Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
            using(var simulation=arena.AcquireManualSimulation())
            {
                void Advance(float seconds)
                {
                    for(int i=0;i<Mathf.CeilToInt(seconds/.01f);i++)
                    { simulation.Step(.01f);foreach(var p in world.GetComponentsInChildren<Stage1VillageMonsterPresentation>())p.Draw(); }
                }
                FieldMouseEnemy Place(string id,int facing,float distance)
                {
                    foreach(var other in enemies){other.aggressive=false;other.CancelAttack();}
                    arena.Player.ResetPlayer();
                    var e=enemies.First(a=>a.Definition.id==id);e.spawn=new Vector2(-500,.003f);e.ResetEnemy();e.SyncFacing(facing);
                    arena.Player.Position=new Vector2(-500+facing*distance,.003f);arena.Player.velocity=Vector2.zero;
                    Physics2D.SyncTransforms();Advance(.12f);return e;
                }
                report.allMotions=true;
                foreach(var spec in content.Data.monsters.Where(s=>!s.attackKind.StartsWith("prototype-")))
                {
                    var e=Place(spec.id,1,10);var p=e.GetComponent<Stage1VillageMonsterPresentation>().Player;
                    foreach(string motion in new[]{"idle","walk","attack","hit","death"})
                    { report.allMotions&=p.Play(motion,true);p.SampleAt(p.Clip.DurationMs/2000f);report.allMotions&=e.visual.sprite==p.Clip.frames[p.Frame]; }
                    p.Play("idle");
                }
                Check(report.allMotions,"A native game motion is missing.");
                report.meleeHits=true;report.mirrored=true;
                foreach(int facing in new[]{-1,1})foreach(string id in new[]{"01_thorn-root","02_broom-stalker"})
                {
                    var e=Place(id,facing,id=="01_thorn-root"?2.0f:1.2f);
                    float health=arena.Player.health;e.aggressive=true;Advance(1.1f+EnemyPursuitSettings.Load().reactionDelayMax); // + the "!" reaction pause
                    bool hit=arena.Player.health<health;
                    report.meleeHits&=hit;report.mirrored&=e.facing==facing && e.visual.flipX==(facing>0);
                    Check(hit,"Prototype melee did not hit: "+id+" direction "+facing);
                }
                var spitter=Place("03_gutter-spitter",1,4);
                float hp=arena.Player.health;spitter.aggressive=true;Advance(1.65f+EnemyPursuitSettings.Load().reactionDelayMax); // + the "!" reaction pause
                report.shots=spitter.GetComponent<Stage1VillageMonsterAttack>().ShotsFired;
                report.projectileHits=report.shots>0 && arena.Player.health<hp;
                Check(report.projectileHits,"Spitter projectile did not damage the target.");
                var warden=Place("04_well-warden",1,3.5f);
                var hook=warden.GetComponent<Stage1VillageMonsterAttack>();
                hp=arena.Player.health;warden.aggressive=true;Advance(hook.Spec.damageStart/hook.Spec.hookSpeed-.1f);
                bool pulledClose=Mathf.Abs(arena.Player.Position.x-warden.Position.x)<2.2f;
                Advance(.5f);
                report.hookClaws=warden.GetComponent<Stage1VillageMonsterAttack>().HitsDelivered;
                report.hookHit=report.hookClaws>0 && arena.Player.health<hp && pulledClose;
                Check(report.hookHit,"Hook did not pull and then claw.");
                warden=Place("04_well-warden",-1,4);
                warden.aggressive=true;Advance(.3f);arena.Player.Position=new Vector2(-510,.003f);hp=arena.Player.health;Advance(2.1f);
                report.hookMiss=arena.Player.health==hp;
                Check(report.hookMiss,"A missed hook caused a follow-up hit.");
                var interrupted=Place("03_gutter-spitter",1,4);
                int shotsBefore=interrupted.GetComponent<Stage1VillageMonsterAttack>().ShotsFired;
                interrupted.aggressive=true;Advance(.5f);
                Check(interrupted.Receive(1,arena.Player,-1,false,null),"Fixture hit was not accepted.");
                Advance(.01f);
                Check(interrupted.GetComponent<Stage1VillageMonsterPresentation>().Player.Motion=="hit","Accepted hit did not select the hit pose.");
                interrupted.aggressive=false;Advance(1.3f);
                report.interrupts=!interrupted.SpeciesAttackVisible && interrupted.GetComponent<Stage1VillageMonsterAttack>().ShotsFired==shotsBefore;
                Check(report.interrupts,"Interrupted attack emitted a late projectile.");
                Check(interrupted.Receive(9999,arena.Player,-1,false,null),"Death fixture hit was not accepted.");
                Advance(.1f);
                Check(interrupted.GetComponent<Stage1VillageMonsterPresentation>().Player.Motion=="death" && interrupted.visual.enabled,"Death motion did not begin.");
                Advance(interrupted.Definition.deathDuration);
                Check(!interrupted.visual.enabled,"Death motion was not hidden on completion.");
                for(int i=0;i<enemies.Length;i++){enemies[i].spawn=originalSpawns[i];enemies[i].ResetEnemy();enemies[i].aggressive=false;}
                arena.Player.ResetPlayer();
                Advance(.01f);
                report.reset=enemies.All(e=>!e.Dead && !e.SpeciesAttackVisible && e.Position==e.spawn);
                Check(report.reset,"Monster reset failed.");
            }
            Destroy(floor);
        }
    }
}
