using System;
using System.Linq;
using ForestCombatV2;
using Stage1VillageExperiment;
using UnityEngine;

namespace Deviant.Chapter2
{
    // Share the current game's pixel viewport and discovered-room map. Physics keeps authored coordinates.
    public static class Chapter2CommonComposition
    {
        public static Chapter1FullMap Attach(Chapter2World world)
        {
            var viewport=world.gameObject.AddComponent<VillagePixelViewport>();viewport.sourceCamera=world.sceneCamera;
            world.arena.Player.gameObject.AddComponent<Chapter1PlayerHitFeedback>().player=world.arena.Player;
            float left=Mathf.Floor(world.Layout.surfaces.Min(s=>s.x))-2,top=Mathf.Ceil(world.Layout.surfaces.Max(s=>s.y+s.h))+2;
            int cols=Mathf.CeilToInt(world.Layout.surfaces.Max(s=>s.x+s.w)-left)+2,rows=Mathf.CeilToInt(top-world.Layout.surfaces.Min(s=>s.y))+2;
            var data=new ForestData{width=cols*32,height=rows*32,cols=cols,rows=rows,ground=new int[cols*rows],platforms=Array.Empty<FoliagePlatform>(),views=Array.Empty<ForestView>(),landmarks=Array.Empty<ForestLandmark>()};
            Vector2 Project(Vector2 p)=>new Vector2((p.x-left)*32,(top-p.y)*32);
            foreach(var surface in world.Layout.surfaces)
            {
                int x=Mathf.FloorToInt(surface.x-left),y=Mathf.FloorToInt(top-surface.y-surface.h),right=Mathf.CeilToInt(surface.x+surface.w-left),bottom=Mathf.CeilToInt(top-surface.y);
                for(int yy=Mathf.Max(0,y);yy<Mathf.Min(rows,bottom);yy++)for(int xx=Mathf.Max(0,x);xx<Mathf.Min(cols,right);xx++)data.ground[yy*cols+xx]=1;
            }
            data.rooms=world.Layout.rooms.Select((r,i)=>new ForestRoom{id=i,name=r.id+" / "+r.kind,kind=r.kind,x=Mathf.FloorToInt(r.x-left),y=Mathf.FloorToInt(top-r.y-r.h),w=Mathf.CeilToInt(r.w),h=Mathf.CeilToInt(r.h),mainPath=r.main}).ToArray();
            data.passages=world.Layout.connections.Select(c=>new ForestPassage{from=Array.FindIndex(world.Layout.rooms,r=>r.id==c.from),to=Array.FindIndex(world.Layout.rooms,r=>r.id==c.to),spaces=c.points.Skip(1).Select((p,i)=>
            {
                var a=Project(c.points[i].Vector)/32;var b=Project(p.Vector)/32;
                return new GroundRect{x=Mathf.FloorToInt(Mathf.Min(a.x,b.x))-1,y=Mathf.FloorToInt(Mathf.Min(a.y,b.y))-1,w=Mathf.CeilToInt(Mathf.Abs(a.x-b.x))+3,h=Mathf.CeilToInt(Mathf.Abs(a.y-b.y))+3};
            }).ToArray()}).ToArray();
            var map=world.gameObject.AddComponent<Chapter1FullMap>();map.Initialize(new Chapter1MapSource{Data=data,Arena=world.arena,Diagnostics=world.diagnostics,Ready=()=>world&&world.Ready,Visible=()=>world&&world.Ready,BossDefeated=()=>world&&world.Boss&&world.Boss.Dead,CameraPosition=()=>Project((Vector2)world.sceneCamera.transform.position+new Vector2(-15,8.4375f)),WorldToMap=Project,Title="2챕터 · 전체 지도"});return map;
        }
    }
}
