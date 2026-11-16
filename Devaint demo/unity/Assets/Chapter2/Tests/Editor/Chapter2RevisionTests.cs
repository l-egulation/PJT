using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Chapter2.Editor;
using Deviant.Combat.Contracts;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Deviant.Chapter2.Tests
{
    public sealed class Chapter2RevisionTests
    {
        [UnityTearDown] public IEnumerator LeavePlayModeAfterEachTest(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [SetUp] public void Prepare()
        {
            if(Application.isPlaying)return;
            EditorSceneManager.playModeStartScene=null;
            Chapter2Tools.Create();
            var world=Object.FindAnyObjectByType<Chapter2World>();world.seed=17;world.family="current";
        }
        [UnityTest] public IEnumerator EncountersAndComparisonViews()
        {
            yield return new EnterPlayMode();
            float deadline=Time.realtimeSinceStartup+45;
            Chapter2World world=null;
            do {yield return null;world=Object.FindAnyObjectByType<Chapter2World>();}while((!world||!world.Ready)&&Time.realtimeSinceStartup<deadline);
            Assert.That(world&&world.Ready,Is.True);
            yield return null;yield return null;
            Inspect(world);
            yield return new ExitPlayMode();
        }
        static void Inspect(Chapter2World world)
        {
            bool baseline=Environment.GetEnvironmentVariable("C2_REVISION_PHASE")=="before";
            string dir=Environment.GetEnvironmentVariable("C2_REVISION_OUTPUT");
            using(var simulation=world.arena.AcquireManualSimulation())
            {
                // Store camera coordinates once, then use exactly the same 30x16.875-unit view after editing.
                if(!string.IsNullOrEmpty(dir))
                {
                var dataPath=Path.Combine(dir,"views.json");
                ViewList views;
                if(baseline){views=Views(world);Directory.CreateDirectory(dir);File.WriteAllText(dataPath,JsonUtility.ToJson(views,true));}
                else
                {
                    views=JsonUtility.FromJson<ViewList>(File.ReadAllText(dataPath));
                    var old=JsonUtility.FromJson<C2Bank>(File.ReadAllText(Path.Combine(dir,"layouts-before.json"))).layouts.First(l=>l.id==world.Layout.id&&l.variant==world.Layout.variant);
                    foreach(var view in views.items)
                    {
                        if(view.id=="wall-jump")
                        {
                            var connection=old.connections.First(c=>c.points.Skip(1).Select((p,i)=>new[]{c.points[i],p}).Any(p=>p[0].x==p[1].x&&Mathf.Abs(p[0].x-view.x)<.1f&&view.y>Mathf.Min(p[0].y,p[1].y)&&view.y<Mathf.Max(p[0].y,p[1].y)));
                            var currentConnection=world.Layout.connections.First(c=>c.id==connection.id);
                            var segment=currentConnection.points.Skip(1).Select((p,i)=>new[]{currentConnection.points[i],p}).Where(p=>p[0].x==p[1].x).OrderByDescending(p=>Mathf.Abs(p[1].y-p[0].y)).First();
                            float centerY=(segment[0].y+segment[1].y)/2;view.x=segment[0].x;view.y=centerY;view.playerX=view.x;view.playerY=centerY-6;continue;
                        }
                        var room=old.rooms.FirstOrDefault(r=>r.Bounds.Contains(new Vector2(view.playerX,view.playerY+.5f)));
                        if(room==null)room=old.rooms.OrderBy(r=>Vector2.Distance(r.Bounds.center,new Vector2(view.x,view.y))).First();
                        var current=world.Layout.Room(room.id);float dx=current.x-room.x,dy=current.y-room.y;
                        view.x+=dx;view.y+=dy;view.playerX+=dx;view.playerY+=dy;
                    }
                }
                foreach(var view in views.items)
                {
                    world.arena.Player.ResetPlayer();world.arena.Player.Position=new Vector2(view.playerX,view.playerY);
                    Physics2D.SyncTransforms();simulation.Step(1f/120);world.CameraStep(0,true);
                    world.sceneCamera.transform.position=new Vector3(view.x,view.y,-10);
                    Capture(world.sceneCamera,Path.Combine(dir,(baseline?"before-":"after-")+view.id+".png"));
                }
                }
                foreach(var enemy in world.Actors.Values.OfType<Chapter2NormalEnemy>())
                {
                    var source=world.Layout.actors.First(a=>a.id==world.Actors.First(p=>p.Value==enemy).Key);
                    var room=world.Layout.Room(source.roomId);
                    world.WarpToRoom(room.id);world.arena.Player.ResetPlayer();enemy.ResetEnemy();
                    world.arena.Player.maxHealth=10000;world.arena.Player.health=10000;
                    // 같은 방의 다른 적 몸이 플레이어를 막지 않도록 확인하는 적 외에는 잠시 끈다
                    var others=world.Actors.Values.Where(a=>a&&a!=enemy&&a.gameObject.activeSelf).ToList();foreach(var o in others)o.gameObject.SetActive(false);
                    // 적의 오른쪽 3u가 낭떠러지나 벽이면 왼쪽 3u에 선다(같은 바닥에서 공격을 확인)
                    foreach(float side in new[]{3f,-3f})
                    {
                        world.arena.Player.Position=new Vector2(enemy.Position.x+side,enemy.Position.y+.05f);world.arena.Player.velocity=Vector2.zero;Physics2D.SyncTransforms();
                        simulation.Step(1f/120);
                        if(Mathf.Abs(world.arena.Player.Position.y-enemy.Position.y)<.2f&&world.Layout.RoomAt(world.arena.Player.Position)?.id==room.id)break;
                    }
                    // 카메라를 플레이어 위치로 옮긴다(공격 시작은 전조가 화면에 보일 때만 허용된다)
                    for(int c=0;c<3;c++)world.CameraStep(0,true);
                    bool eligible=enemy.TargetEligible(world.arena.Player);
                    Debug.Log("C2_ENCOUNTER "+source.species+" room="+room.id+" eligible="+eligible);
                    if(baseline)Assert.That(eligible,Is.False,"Reproduce Chapter1-only room lookup failure");
                    else
                    {
                        Assert.That(eligible,Is.True,"Chapter2 same-room target "+source.id);
                        for(int n=0;n<600&&enemy.AttacksStarted==0;n++)simulation.Step(1f/120);
                        Assert.That(enemy.AttacksStarted,Is.GreaterThan(0),source.species+" must telegraph");
                        bool active=false;float hp=world.arena.Player.health;
                        for(int n=0;n<360;n++){simulation.Step(1f/120);enemy.Presentation.Sample();active|=enemy.HitsDelivered>0||enemy.Presentation.ActiveEffects>0;}
                        Assert.That(active,Is.True,source.species+" must explode/charge after telegraph");
                        Debug.Log("C2_ATTACK_VERIFIED "+source.species+" attacks="+enemy.AttacksStarted+" hits="+enemy.HitsDelivered);
                        world.WarpToRoom("S");simulation.Step(1f/120);Assert.That(enemy.aggressive,Is.True,"leaving the owner room keeps the enemy engaged");Assert.That(enemy.hurtCollider.enabled,Is.True);
                    }
                    foreach(var o in others)o.gameObject.SetActive(true);
                    // One representative of each prototype species is sufficient here.
                    if(source.species.Contains("heavy"))break;
                }
            }
        }
        static void AssertExpandedWallTransfers(Chapter2World world,Deviant.Combat.CombatArena.ManualSimulation simulation)
        {
            var player=world.arena.Player;int verified=0;
            foreach(var shaft in world.Layout.wallJumpSpaces.Where(s=>s.h>9))
            {
                float y=shaft.y+2;
                bool solidLeft=world.Layout.surfaces.Any(s=>!s.oneWay&&s.Bounds.Contains(new Vector2(shaft.x-.02f,y+1)));
                bool solidRight=world.Layout.surfaces.Any(s=>!s.oneWay&&s.Bounds.Contains(new Vector2(shaft.x+shaft.w+.02f,y+1)));
                if(!solidLeft||!solidRight)continue;
                foreach(int side in new[]{-1,1})
                {
                    player.ResetPlayer();player.SyncFacing(side);player.velocity=Vector2.zero;player.grounded=false;
                    float face=side<0?shaft.x:shaft.x+shaft.w;
                    player.Position=new Vector2(face-side*(player.size.x/2+player.offset.x+.01f),y);
                    Physics2D.SyncTransforms();simulation.Step(1f/120,new CombatInput(side));
                    Assert.That(player.WallClimbing,Is.True,shaft.id+" start wall "+side);
                    float start=player.Position.y;
                    simulation.Step(1f/120,new CombatInput(-side,CombatButtons.Jump));
                    for(int tick=0;tick<90&&!player.WallClimbing;tick++)simulation.Step(1f/120,new CombatInput(-side));
                    Assert.That(player.WallClimbing,Is.True,shaft.id+" reach opposite wall across five to six units");
                    Assert.That(player.Position.y,Is.GreaterThan(start),shaft.id+" gains height");
                    Assert.That(Mathf.Abs(player.Position.x-face),Is.GreaterThan(3.5f),shaft.id+" actually crosses");
                    verified++;
                }
            }
            Assert.That(verified,Is.GreaterThanOrEqualTo(4));Debug.Log("C2_WALL_TRANSFERS_VERIFIED "+verified);
        }
        [Serializable] sealed class View { public string id;public float x,y,playerX,playerY; }
        [Serializable] sealed class ViewList { public View[] items; }
        static ViewList Views(Chapter2World world)
        {
            var rooms=world.Layout.rooms.Where(r=>r.kind=="combat").Take(3).ToArray();
            var list=rooms.Select(r=>new View{id=r.id+"-room",x=r.x+r.w/2,y=r.y+r.h*.6f,playerX=r.x+r.w/2,playerY=r.y+1}).ToList();
            var pairs=(from a in world.Layout.surfaces from b in world.Layout.surfaces
                let gap=b.x-a.x-a.w let low=Mathf.Max(a.y,b.y) let high=Mathf.Min(a.y+a.h,b.y+b.h)
                where !a.oneWay&&!b.oneWay&&gap>=2.74f&&gap<=3.01f&&high-low>6
                select new View{id="wall-jump",x=(a.x+a.w+b.x)/2,y=low+7,playerX=(a.x+a.w+b.x)/2,playerY=low+1}).First();
            list.Add(pairs);
            var f=rooms[0];list.Add(new View{id="sealed-boundary",x=f.x+2,y=f.y+f.h-2,playerX=f.x+4,playerY=f.y+f.h-7});
            return new ViewList{items=list.ToArray()};
        }
        static void Capture(Camera camera,string path)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(960,540,24);
            var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }
    }
}
