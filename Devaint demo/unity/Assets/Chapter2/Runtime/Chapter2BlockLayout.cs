using System;
using System.Collections.Generic;
using System.Linq;
using ForestCombatV2.BlockMap;
using UnityEngine;

namespace Deviant.Chapter2
{
    // 2챕터 맵을 런타임에 조립한다. 원형은 data/map/block_rooms.json, 배치는 BlockGenerator.Stage2(시드).
    // 기존 C2Layout 계약(방 id S·F01..·M·E·B, 엘리트 E:elite, 보스 전투장 원점 boss-(0,3))을 지킨다.
    // 봉인 도전방은 SEAL, 잠긴 상자방은 CHEST다(1스테이지 원형 사용, stage-gimmicks.md).
    public static class Chapter2BlockLayout
    {
        public const string RuntimeId="runtime";
        public static C2Layout Generate(int seed)
        {
            BlockLayout layout=null;
            for(int i=0;i<8&&layout==null;i++)layout=BlockGenerator.Stage2(seed+i*977);
            if(layout==null)throw new InvalidOperationException("No chapter 2 block layout for seed "+seed);
            var world=BlockAssembler.Assemble(layout);
            if(world.issues.Count>0)throw new InvalidOperationException("Chapter 2 block layout: "+string.Join(" / ",world.issues));
            return FromBlocks(world,seed);
        }
        static string SideName(char c)=>c=='L'?"left":c=='R'?"right":c=='T'?"top":"bottom";
        public static C2Layout FromBlocks(BlockWorld world,int seed)
        {
            var layout=world.layout;var roomsW=world.rooms.Where(r=>!r.IsConnector).ToList();
            // 방 id: 주 경로 전투방 F01.. 다음 순환 갈래 전투방
            var ids=new Dictionary<string,string>();int n=0;
            foreach(var k in layout.mainKeys.Concat(roomsW.Select(r=>r.key)))
            {
                if(ids.ContainsKey(k))continue;var r=roomsW.First(x=>x.key==k);
                ids[k]=r.role=="S"?"S":r.role=="SHOP"?"M":r.role=="ELITE"?"E":r.role=="BOSS"?"B":r.role=="SEAL"?"SEAL":r.role=="CHEST"?"CHEST":"F"+(++n).ToString("00");
            }
            var rooms=roomsW.OrderBy(r=>ids[r.key]=="S"?0:ids[r.key].StartsWith("F")?1:2).ThenBy(r=>ids[r.key])
                // 방 범위는 테두리를 뺀 안쪽이다. 출입구 터널(테두리)은 어느 방에도 속하지 않는 통로다.
                .Select(r=>new C2Room{id=ids[r.key],kind=r.kind,prototypeId=r.kind=="combat"||r.kind=="seal"||r.kind=="chest"?r.def.id:"",x=r.x+r.def.frameSide,y=r.y+BlockSpec.Frame,w=r.w-2*r.def.frameSide,h=r.h-2*BlockSpec.Frame,main=layout.mainKeys.Contains(r.key)}).ToArray();
            // 출입구: 선언한 모든 슬롯, 열린 것만 enabled. 좌우는 개구부 가운데(문턱+3), 상하는 개구부 가운데 x
            var ports=new List<C2Port>();
            (float x,float y) Center(BlockWorldRoom r,string slot)
            {
                var e=BlockAssembler.Edge(r,slot);
                return e.side=='L'||e.side=='R'?(e.x,e.y+BlockSpec.DoorH/2f):(e.x+BlockSpec.DoorW/2f,e.y);
            }
            foreach(var r in roomsW)foreach(var g in r.build.slots)
            {
                var c=Center(r,g.id);
                ports.Add(new C2Port{roomId=ids[r.key],id=g.id,side=SideName(g.side),x=c.x,y=c.y,opening=g.side=='L'||g.side=='R'?BlockSpec.DoorH:BlockSpec.DoorW,enabled=g.active});
            }
            // 연결: 통로 블록을 사이에 둔 두 링크는 방-방 연결 하나로 합친다
            var connections=new List<C2Connection>();
            foreach(var l in layout.links)
            {
                bool ac=l.a.StartsWith("c"),bc=l.b.StartsWith("c");if(ac)continue;
                string bKey=l.b,bSlot=l.nb;
                if(bc){var o=layout.links.First(m=>!m.Equals(l)&&(m.a==l.b||m.b==l.b));bKey=o.a==l.b?o.b:o.a;bSlot=o.a==l.b?o.nb:o.na;}
                var A=roomsW.First(x=>x.key==l.a);var B=roomsW.First(x=>x.key==bKey);
                var pa=Center(A,l.na);var pb=Center(B,bSlot);
                var pts=new List<C2Point>{new C2Point{x=pa.x,y=pa.y}};
                if(pa.x!=pb.x&&pa.y!=pb.y)
                {
                    bool aSide=l.na[0]=='L'||l.na[0]=='R';
                    pts.Add(aSide?new C2Point{x=pb.x,y=pa.y}:new C2Point{x=pa.x,y=pb.y});
                }
                pts.Add(new C2Point{x=pb.x,y=pb.y});
                connections.Add(new C2Connection{id="link-"+connections.Count,from=ids[l.a],to=ids[bKey],fromPortId=l.na,toPortId=bSlot,points=pts.ToArray()});
            }
            var surfaces=BlockAssembler.SolidRects(world).Select((r,i)=>new C2Surface{id="solid-"+i,x=r.x,y=r.y,w=r.width,h=r.height}).ToList();
            foreach(var p in world.plats)surfaces.Add(new C2Surface{id="platform-"+surfaces.Count,x=p.x,y=p.y-.5f,w=p.w,h=.5f,oneWay=true});
            var species=BlockSpec.Catalog.species.s2;var actors=new List<C2Actor>();var counter=new Dictionary<string,int>();
            foreach(var e in world.enemies)
            {
                string room=ids[e.owner];
                if(e.t=="A"){actors.Add(new C2Actor{id="E:elite",roomId=room,species="04_well-warden",x=e.x,y=e.y,flipX=e.flip});continue;}
                counter.TryGetValue(room,out int c);counter[room]=c+1;
                actors.Add(new C2Actor{id=room+":"+e.t.ToLowerInvariant()+(c+1),roomId=room,species=species.Of(e.t),x=e.x,y=e.y,flipX=e.flip});
            }
            BlockWorldRoom Role(string role)=>roomsW.First(r=>r.role==role);
            var S=Role("S");var M=Role("SHOP");var Bw=Role("BOSS");int f=BlockSpec.Frame;
            var boss=new C2Point{x=Bw.x+Bw.def.frameSide,y=Bw.y+f};
            var hall=world.rooms.First(r=>r.IsConnector&&r.w>BlockSpec.BlockW);
            var bossHall=new C2Bounds{x=hall.x,y=hall.y+f,w=hall.w,h=BlockSpec.DoorH};
            var walls=world.rooms.Where(r=>r.IsConnector&&r.climb).Select((r,i)=>new C2WallJumpSpace{id="climb-"+i,x=r.x+(BlockSpec.BlockW-BlockSpec.DoorW)/2,y=r.y+f,w=BlockSpec.DoorW,h=BlockRooms.ClimbRise,previousWidth=BlockSpec.DoorW}).ToArray();
            var result=new C2Layout{id=RuntimeId,variant=seed,name="지하수로 · 시드 "+seed,sourceSha256="",rooms=rooms,ports=ports.ToArray(),surfaces=surfaces.ToArray(),
                actors=actors.ToArray(),connections=connections.ToArray(),
                spawn=new C2Point{x=S.x+S.w/2f,y=S.y+f},maintenance=new C2Point{x=M.x+M.w/2f,y=M.y+f},boss=boss,exit=new C2Point{x=boss.x+15,y=boss.y},
                wallJumpSpaces=walls,sealedBounds=new C2Bounds{x=0,y=0,w=world.W,h=world.H},
                bossHall=bossHall,bossHallToLeft=Bw.x<hall.x};
            AddGimmicks(result,world,ids);
            return result;
        }
        // 봉인 도전방·잠긴 상자방 자리, 열쇠 운반자 방, 운반자 경로 지도(stage-gimmicks.md)
        static void AddGimmicks(C2Layout result,BlockWorld world,Dictionary<string,string> ids)
        {
            var layout=world.layout;int f=BlockSpec.Frame;
            C2Gimmick Site(string role)
            {
                var r=world.rooms.FirstOrDefault(x=>!x.IsConnector&&x.role==role);if(r==null)return null;
                var site=new C2Gimmick{roomId=ids[r.key],markerX=r.x+r.build.markerX,markerY=r.y+r.build.markerY};
                // 철창: 쓰는 좌우 출입구마다 테두리 바깥 끝에서 방 꼭대기까지(1챕터와 같은 모양). 바닥은 문턱 높이다.
                var gates=new List<C2Bounds>();float width=ForestCombatV2.Chapter1SealChallenge.GateWidth,top=r.y+r.h;
                foreach(var g in r.build.slots)
                {
                    if(!g.active||g.side!='L'&&g.side!='R')continue;var e=BlockAssembler.Edge(r,g.id);
                    gates.Add(new C2Bounds{x=e.side=='L'?e.x:e.x-width,y=e.y,w=width,h=top-e.y});
                }
                site.gates=gates.ToArray();
                // 떨어지는 몹: 방 안쪽 폭 전체, 천장 테두리 바로 아래에서 바닥을 찾는다
                site.drop=new C2Bounds{x=r.x+r.def.frameSide,y=r.y,w=r.w-2*r.def.frameSide,h=r.h-f-1};
                return site;
            }
            result.seal=Site("SEAL");result.chest=Site("CHEST");
            // 열쇠 운반자: 상자방 갈래가 갈라진 주 경로 자리 뒤의 주 경로 전투방(1챕터처럼 상자를 먼저 본다)
            var carriers=new List<string>();
            for(int k=Math.Max(1,layout.chestFrom+1);k<layout.mainKeys.Count;k++)
            {
                var r=world.rooms.First(x=>x.key==layout.mainKeys[k]);
                if(r.kind=="combat")carriers.Add(ids[r.key]);
            }
            result.carrierRooms=carriers.ToArray();
            result.carrierMap=CarrierMap(world);result.carrierMapOffset=world.H;
        }
        // 1챕터 ForestData 형식의 타일 지도: 0행이 맨 위, 방·통로 블록 범위, 하향 통과 발판(2챕터 발판은 모두 통과 가능)
        static ForestCombatV2.ForestData CarrierMap(BlockWorld world)
        {
            int cols=world.W,rows=world.H;var ground=new int[cols*rows];
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)ground[(rows-1-y)*cols+x]=world.grid[y*cols+x];
            var rooms=new List<ForestCombatV2.ForestRoom>();var passages=new List<ForestCombatV2.ForestPassage>();
            foreach(var r in world.rooms)
            {
                if(r.IsConnector){passages.Add(new ForestCombatV2.ForestPassage{x=r.x,y=rows-(r.y+r.h),w=r.w,h=r.h});continue;}
                rooms.Add(new ForestCombatV2.ForestRoom{id=rooms.Count,x=r.x,y=rows-(r.y+r.h),w=r.w,h=r.h,kind=r.kind,name=r.name});
            }
            var platforms=world.plats.Select(p=>new ForestCombatV2.FoliagePlatform{x=p.x*32,y=(rows-p.y)*32,w=p.w*32}).ToArray();
            return new ForestCombatV2.ForestData{cols=cols,rows=rows,width=cols*32,height=rows*32,ground=ground,platforms=platforms,rooms=rooms.ToArray(),passages=passages.ToArray()};
        }
        // 출입구 안쪽 접근 구역(좌우: 방 좌우 벽 + 접근 4, 위아래: 테두리 2 기준). 적 이동 영역에서 뺀다(Chapter2NormalEnemy.BindEncounter).
        public static Rect[] ApproachZones(C2Layout layout,string roomId)
        {
            var list=new List<Rect>();float f=BlockSpec.Frame,a=BlockSpec.SideApproach;
            var room=layout.rooms.First(r=>r.id==roomId);
            foreach(var p in layout.ports.Where(p=>p.enabled&&p.roomId==roomId))
            {
                float floor=p.y-p.opening/2f;
                // 좌우 벽 두께는 원형마다 다르다(보통 3, 보스 1): 경계에서 방 안쪽 면까지 + 접근 4
                if(p.side=="left")list.Add(new Rect(p.x,floor,room.x-p.x+a,p.opening));
                else if(p.side=="right")list.Add(new Rect(room.x+room.w-a,floor,p.x-(room.x+room.w)+a,p.opening));
                // 아래: 방 바닥 높이, 위: 천장 3칸 아래 접근 발판 높이
                else{float y=p.side=="bottom"?p.y+f:p.y-f-BlockSpec.TopLedgeDrop;list.Add(new Rect(p.x-p.opening/2f-1,y,p.opening+2,3));}
            }
            return list.ToArray();
        }
    }
}
