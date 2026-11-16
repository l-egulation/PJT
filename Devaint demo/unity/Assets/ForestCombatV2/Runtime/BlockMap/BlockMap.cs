using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForestCombatV2.BlockMap
{
    // 원형 편집 원본은 data/map/block_rooms.json, Unity 사본은 Resources/BlockRooms.json이다.
    // 좌표: 1 = 1 Unity unit = 32px, y 위 방향. 원형 좌표는 방 안쪽(테두리 제외) 기준이다.
    [Serializable] public sealed class BlockRoomCatalog { public string schema; public BlockSpecies species; public BlockRoomDef[] rooms; }
    [Serializable] public sealed class BlockSpecies { public BlockSpeciesMap s1,s2; }
    [Serializable] public sealed class BlockSpeciesMap { public string T,K,P,G,C,W,A; public string Of(string code)=>code switch{"T"=>T,"K"=>K,"P"=>P,"G"=>G,"C"=>C,"W"=>W,"A"=>A,_=>null}; }
    [Serializable] public sealed class BlockRoomDef
    {
        public string id,kind,form,name,source,intent; public int stage,bw,bh,frameSide=2;
        // 기믹 방(봉인 석판·잠긴 상자)이 서는 자리. 방 안쪽 좌표이며 kind가 seal·chest일 때만 쓴다.
        public BlockMarker marker;
        public bool HasMarker=>kind=="seal"||kind=="chest";
        // 1챕터 시작방(S1-START)에서 플레이어가 서는 자리. 방 안쪽 좌표, y는 선 바닥·발판 높이. 판정 도구가 존재와 지지면을 검사한다.
        public BlockMarker spawn;
        public string[] slots; public BlockRect[] solids; public BlockPlat[] plats; public BlockEnemy[] enemies; public BlockProp[] props;
    }
    [Serializable] public sealed class BlockRect { public int x,y,w,h; public string[] need; }
    [Serializable] public sealed class BlockPlat { public int x,y,w; public string[] need; public bool vanishing; }
    [Serializable] public sealed class BlockMarker { public float x,y; }
    [Serializable] public sealed class BlockEnemy { public string t; public float x,y; }
    [Serializable] public sealed class BlockProp { public float x; public string label; }

    public static class BlockSpec
    {
        public const int BlockW=16,BlockH=12,Frame=2,DoorH=6,DoorW=6,SideApproach=4,TopLedgeDrop=3;
        public static char Opp(char side)=>side switch{'L'=>'R','R'=>'L','T'=>'B',_=>'T'};
        public static string MirrorId(string id,int bw)
        {
            char side=id[0];int n=SlotIndex(id);
            return side=='L'?"R"+n:side=='R'?"L"+n:side+(bw-1-n).ToString();
        }
        static BlockRoomCatalog catalog;
        public static BlockRoomCatalog Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var asset=Resources.Load<TextAsset>("BlockRooms");
                if(!asset)throw new InvalidOperationException("BlockRooms 원형 데이터가 Resources에 없습니다.");
                catalog=JsonUtility.FromJson<BlockRoomCatalog>(asset.text);
                if(catalog==null||catalog.schema!="block-rooms-v1"||catalog.rooms==null)throw new InvalidOperationException("BlockRooms 스키마가 다릅니다.");
                return catalog;
            }
        }
        static Dictionary<string,BlockRoomDef> byId;
        public static BlockRoomDef Room(string id)
        {
            byId??=Catalog.rooms.ToDictionary(r=>r.id);
            return byId.TryGetValue(id,out var r)?r:throw new InvalidOperationException("원형 없음: "+id);
        }
        // 슬롯 번호('L2' → 2) 해석 캐시
        static readonly Dictionary<string,int> slotIndex=new();
        public static int SlotIndex(string id){if(!slotIndex.TryGetValue(id,out int n))slotIndex[id]=n=int.Parse(id.Substring(1));return n;}
    }

    public sealed class BlockSlot { public string id; public char side; public int n,sill,y0,y1,x0,x1,y; public bool active; }
    public sealed class BlockPlatform { public int x,y,w; public string role,slot,owner; public bool vanishing; }
    public sealed class BlockActor { public string t,owner; public float x,y; public bool flip; }
    public sealed class BlockRoomBuild
    {
        public BlockRoomDef def; public bool mirror; public int fw,fh,fx,fy;
        public byte[] grid,own; public float markerX,markerY; public List<BlockPlatform> plats=new(); public List<BlockActor> enemies=new(); public List<BlockSlot> slots=new();
    }

    public static class BlockRooms
    {
        // active는 원형(반전 전) 슬롯 이름이다. 반전하면 이름도 함께 바꾼다.
        public static BlockRoomBuild Build(BlockRoomDef room,IEnumerable<string> active,bool mirror)
        {
            int fw=room.bw*BlockSpec.BlockW,fh=room.bh*BlockSpec.BlockH,fx=room.frameSide,fy=BlockSpec.Frame,W=fw-2*fx;
            var act=new HashSet<string>((active??room.slots).Select(n=>mirror?BlockSpec.MirrorId(n,room.bw):n));
            var b=new BlockRoomBuild{def=room,mirror=mirror,fw=fw,fh=fh,fx=fx,fy=fy,grid=new byte[fw*fh],own=new byte[fw*fh]};
            for(int i=0;i<b.grid.Length;i++)b.grid[i]=1;
            void Set(int x,int y,byte v,byte o){if(x<0||y<0||x>=fw||y>=fh)return;b.grid[y*fw+x]=v;if(o!=0)b.own[y*fw+x]=o;}
            for(int y=fy;y<fh-fy;y++)for(int x=fx;x<fw-fx;x++)Set(x,y,0,0);
            string[] Need(string[] need)=>need==null||need.Length==0?null:mirror?need.Select(n=>BlockSpec.MirrorId(n,room.bw)).ToArray():need;
            bool Keep(string[] need)=>need==null||need.Any(act.Contains);
            var slots=mirror?room.slots.Select(n=>BlockSpec.MirrorId(n,room.bw)).ToArray():room.slots;
            foreach(var r in room.solids??Array.Empty<BlockRect>())
            {
                if(!Keep(Need(r.need)))continue;int rx=mirror?W-r.x-r.w:r.x;
                for(int y=r.y;y<r.y+r.h;y++)for(int x=rx;x<rx+r.w;x++)Set(x+fx,y+fy,1,1);
            }
            foreach(var p in room.plats??Array.Empty<BlockPlat>())
            {
                var need=Need(p.need);if(!Keep(need))continue;
                b.plats.Add(new BlockPlatform{x=(mirror?W-p.x-p.w:p.x)+fx,y=p.y+fy,w=p.w,role=need==null?"core":"door",vanishing=p.vanishing});
            }
            foreach(string id in slots)
            {
                var g=Slot(room,id,fw,fh);g.active=act.Contains(id);b.slots.Add(g);
                if(!g.active)continue;
                if(g.side=='L'||g.side=='R')
                {
                    int a0=g.side=='L'?0:fw-fx,a1=g.side=='L'?fx:fw;
                    for(int y=g.y0;y<g.y1;y++)for(int x=a0;x<a1;x++)Set(x,y,0,2);
                    for(int y=g.y0;y<g.y1;y++)for(int x=0;x<BlockSpec.SideApproach;x++)Set(g.side=='L'?fx+x:fw-fx-1-x,y,0,2);
                    if(g.n>0)
                    {
                        int lx=g.side=='L'?fx:fw-fx-BlockSpec.SideApproach;
                        for(int y=g.sill-2;y<g.sill;y++)for(int x=lx;x<lx+BlockSpec.SideApproach;x++)Set(x,y,1,2);
                    }
                }
                else
                {
                    int a0=g.side=='B'?0:fh-fy,a1=g.side=='B'?fy:fh;
                    for(int y=a0;y<a1;y++)for(int x=g.x0;x<g.x1;x++)Set(x,y,0,2);
                    // 아래 출입구: 문 바닥(y=2)과 문턱 디딤(y=0). 아래 방 접근 발판에서 디딤까지 5u, 디딤에서 문 바닥까지 2u.
                    // 1단 점프 최고 높이가 정확히 3u라 3u 계단은 쓰지 않는다(2u는 1단, 4~5u는 2단).
                    if(g.side=='B'){b.plats.Add(new BlockPlatform{x=g.x0,y=fy,w=BlockSpec.DoorW,role="doorFloor",slot=id});b.plats.Add(new BlockPlatform{x=g.x0,y=0,w=BlockSpec.DoorW,role="rung",slot=id});}
                    else
                    {
                        b.plats.Add(new BlockPlatform{x=g.x0,y=fh-fy-BlockSpec.TopLedgeDrop,w=BlockSpec.DoorW,role="topLedge",slot=id});
                    }
                }
            }
            if(room.HasMarker&&room.marker!=null){b.markerX=(mirror?W-room.marker.x:room.marker.x)+fx;b.markerY=room.marker.y+fy;}
            foreach(var e in room.enemies??Array.Empty<BlockEnemy>())
                b.enemies.Add(new BlockActor{t=e.t,x=(mirror?W-e.x:e.x)+fx,y=e.y+fy,flip=mirror});
            return b;
        }
        public static BlockSlot Slot(BlockRoomDef room,string id,int fw,int fh)
        {
            char side=id[0];int n=int.Parse(id.Substring(1));
            if(side=='L'||side=='R'){int sill=n*BlockSpec.BlockH+BlockSpec.Frame;return new BlockSlot{id=id,side=side,n=n,sill=sill,y0=sill,y1=sill+BlockSpec.DoorH,x0=side=='L'?0:fw,y=sill};}
            int x0=n*BlockSpec.BlockW+(BlockSpec.BlockW-BlockSpec.DoorW)/2;
            return new BlockSlot{id=id,side=side,n=n,x0=x0,x1=x0+BlockSpec.DoorW,y=side=='B'?0:fh};
        }
        // 통로 블록: 16×12, 테두리 없음, 바닥 윗면 y=2. 위·아래만 열리면 벽타기 통로: 문 바닥에서 착지 발판까지 8u를
        // 벽 한 면으로 오른다(점프 3 + 벽 오르기 7.5 = 10.5, 여유 2.5). 착지 발판에서 위 방 바닥까지 4u는 2단 점프.
        // 일반 세로 통로 디딤은 y=4·8: 바닥 2→4(2u)→8(4u)→위 방 문턱 디딤 12(4u)→위 방 바닥 14(2u). 3u·6u 계단은 쓰지 않는다
        // (1단 점프 최고 3u, 2단 6u가 정확히 한계라 될 듯 말 듯함). 첫 디딤 위로 몸 2u + 여유 2u라 옆 대시가 벽 모서리에 걸리지 않는다.
        public const int ClimbRise=8;
        // bw: 가로 블록 수. 2 이상은 보스 앞 긴 가로 통로(좌우만 열림, BlockGenerator.Hall)
        public static (byte[] grid,List<BlockPlatform> plats,bool climb) Connector(IList<char> open,int bw=1)
        {
            int w=BlockSpec.BlockW*bw,h=BlockSpec.BlockH,sx0=(w-BlockSpec.DoorW)/2,sx1=sx0+BlockSpec.DoorW,floor=BlockSpec.Frame,top=floor+BlockSpec.DoorH;
            var grid=new byte[w*h];for(int i=0;i<grid.Length;i++)grid[i]=1;var plats=new List<BlockPlatform>();
            void Carve(int x0,int y0,int x1,int y1){for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)grid[y*w+x]=0;}
            bool L=open.Contains('L'),R=open.Contains('R'),T=open.Contains('T'),B=open.Contains('B');
            bool climb=open.Count==2&&T&&B;
            if(L)Carve(0,floor,R?w:sx1,top);
            if(R)Carve(L?0:sx0,floor,w,top);
            if(T){Carve(sx0,floor,sx1,h);if(climb)plats.Add(new BlockPlatform{x=sx0,y=floor+ClimbRise,w=BlockSpec.DoorW,role="landing"});else foreach(int y in new[]{floor+2,floor+6})plats.Add(new BlockPlatform{x=sx0,y=y,w=BlockSpec.DoorW,role="rung"});}
            if(B){Carve(sx0,0,sx1,top);plats.Add(new BlockPlatform{x=sx0,y=floor,w=BlockSpec.DoorW,role="doorFloor"});plats.Add(new BlockPlatform{x=sx0,y=0,w=BlockSpec.DoorW,role="rung"});}
            return (grid,plats,climb);
        }
    }

    // 배치 결과: 블록 좌표의 방·통로 블록과 링크. 방 링크 슬롯은 반전 후 이름, 통로 블록은 'L','R','T','B'.
    public sealed class BlockPlacedRoom { public string key,refId,role; public int bx,by; public bool mirror; }
    public sealed class BlockPlacedConnector { public string key; public int bx,by,bw=1; public char[] open; }
    public sealed class BlockLayout
    {
        public int seed,attempt; public string stage;
        public List<BlockPlacedRoom> rooms=new(); public List<BlockPlacedConnector> connectors=new(); public List<(string a,string na,string b,string nb)> links=new();
        public List<string> mainKeys=new(); public float horizontal,vertical;
        // 2스테이지: 잠긴 상자방 갈래가 갈라지는 주 경로 자리(mainKeys 순번). 없으면 -1.
        public int chestFrom=-1;
    }
    public sealed class BlockWorldRoom
    {
        public string key,refId,role,kind,name; public BlockRoomDef def; public BlockRoomBuild build; public int index;
        public int x,y,w,h; // 발자국(월드, y 위)
        public bool mirror; public bool IsConnector=>def==null; public bool climb; public char[] open;
    }
    public sealed class BlockWorld
    {
        public int W,H,originBX,originBY; public byte[] grid,own; public List<BlockPlatform> plats=new(); public List<BlockActor> enemies=new();
        public List<BlockWorldRoom> rooms=new(); public List<string> issues=new(); public BlockLayout layout;
        public bool Solid(int x,int y)=>x<0||y<0||x>=W||y>=H||grid[y*W+x]!=0;
    }

    public static class BlockAssembler
    {
        const int Margin=2;
        public static BlockWorld Assemble(BlockLayout layout)
        {
            var world=new BlockWorld{layout=layout};
            var act=new Dictionary<string,List<string>>();foreach(var r in layout.rooms)act[r.key]=new List<string>();
            foreach(var l in layout.links){if(act.ContainsKey(l.a))act[l.a].Add(l.na);if(act.ContainsKey(l.b))act[l.b].Add(l.nb);}
            var items=new List<(BlockWorldRoom room,int bx,int by,int bw,int bh,byte[] grid,int gw,int gh,List<BlockPlatform> plats)>();
            foreach(var r in layout.rooms)
            {
                var def=BlockSpec.Room(r.refId);
                var declared=new HashSet<string>(r.mirror?def.slots.Select(n=>BlockSpec.MirrorId(n,def.bw)):def.slots);
                foreach(string n in act[r.key])if(!declared.Contains(n))world.issues.Add($"{r.key}({r.refId})에 {n} 슬롯 없음");
                var pre=act[r.key].Select(n=>r.mirror?BlockSpec.MirrorId(n,def.bw):n);
                var b=BlockRooms.Build(def,pre,r.mirror);
                var wr=new BlockWorldRoom{key=r.key,refId=r.refId,role=r.role,kind=def.kind,name=def.name,def=def,build=b,mirror=r.mirror};
                items.Add((wr,r.bx,r.by,def.bw,def.bh,b.grid,b.fw,b.fh,b.plats));
            }
            foreach(var c in layout.connectors)
            {
                if(c.bw>1&&(c.open.Length!=2||!c.open.Contains('L')||!c.open.Contains('R')))world.issues.Add("긴 통로는 좌우만 열린다 "+c.key);
                var k=BlockRooms.Connector(c.open,c.bw);
                var wr=new BlockWorldRoom{key=c.key,refId="conn",kind="connector",name="",climb=k.climb,open=c.open};
                items.Add((wr,c.bx,c.by,c.bw,1,k.grid,BlockSpec.BlockW*c.bw,BlockSpec.BlockH,k.plats));
            }
            for(int i=0;i<items.Count;i++)for(int j=i+1;j<items.Count;j++)
            {
                var a=items[i];var c=items[j];
                if(a.bx<c.bx+c.bw&&c.bx<a.bx+a.bw&&a.by<c.by+c.bh&&c.by<a.by+a.bh)world.issues.Add($"겹침 {a.room.key}/{c.room.key}");
            }
            int x0=items.Min(i=>i.bx),y0=items.Min(i=>i.by),x1=items.Max(i=>i.bx+i.bw),y1=items.Max(i=>i.by+i.bh);
            world.originBX=x0-Margin;world.originBY=y0-Margin;
            world.W=(x1-x0+2*Margin)*BlockSpec.BlockW;world.H=(y1-y0+2*Margin)*BlockSpec.BlockH;
            world.grid=new byte[world.W*world.H];world.own=new byte[world.W*world.H];for(int i=0;i<world.grid.Length;i++)world.grid[i]=1;
            var owner=new int[world.grid.Length];for(int i=0;i<owner.Length;i++)owner[i]=-1;
            for(int idx=0;idx<items.Count;idx++)
            {
                var it=items[idx];int X=(it.bx-world.originBX)*BlockSpec.BlockW,Y=(it.by-world.originBY)*BlockSpec.BlockH;
                it.room.x=X;it.room.y=Y;it.room.w=it.gw;it.room.h=it.gh;it.room.index=idx;
                for(int y=0;y<it.gh;y++)for(int x=0;x<it.gw;x++)
                {
                    int k=(Y+y)*world.W+X+x;if(owner[k]!=-1)world.issues.Add("셀 이중 소유 "+it.room.key);
                    owner[k]=idx;world.grid[k]=it.grid[y*it.gw+x];if(!it.room.IsConnector)world.own[k]=it.room.build.own[y*it.gw+x];
                }
                foreach(var p in it.plats)world.plats.Add(new BlockPlatform{x=p.x+X,y=p.y+Y,w=p.w,role=p.role,slot=p.slot,owner=it.room.key,vanishing=p.vanishing});
                if(!it.room.IsConnector)foreach(var e in it.room.build.enemies)world.enemies.Add(new BlockActor{t=e.t,x=e.x+X,y=e.y+Y,flip=e.flip,owner=it.room.key});
                world.rooms.Add(it.room);
            }
            // 링크 정렬: 두 개구부가 같은 경계·행·열에서 맞닿는가
            foreach(var l in layout.links)
            {
                var A=world.rooms.Find(r=>r.key==l.a);var B=world.rooms.Find(r=>r.key==l.b);
                if(A==null||B==null){world.issues.Add("링크 대상 없음 "+l.a+"/"+l.b);continue;}
                var ea=Edge(A,l.na);var eb=Edge(B,l.nb);
                if(BlockSpec.Opp(ea.side)!=eb.side||ea.x!=eb.x||ea.y!=eb.y)world.issues.Add($"정렬 불일치 {l.a}.{l.na} ↔ {l.b}.{l.nb}");
            }
            return world;
        }
        // 개구부의 월드 위치. 좌우: (경계 x, 문턱 y), 상하: (개구부 왼쪽 x, 경계 y)
        public static (char side,int x,int y) Edge(BlockWorldRoom r,string n)
        {
            if(r.IsConnector)
            {
                int sx=r.x+(BlockSpec.BlockW-BlockSpec.DoorW)/2;char o=n[0];
                return o=='L'?('L',r.x,r.y+BlockSpec.Frame):o=='R'?('R',r.x+r.w,r.y+BlockSpec.Frame):o=='T'?('T',sx,r.y+BlockSpec.BlockH):('B',sx,r.y);
            }
            var g=BlockRooms.Slot(r.def,n,r.build.fw,r.build.fh);
            return g.side=='L'||g.side=='R'?(g.side,r.x+g.x0,r.y+g.sill):(g.side,r.x+g.x0,r.y+g.y);
        }
        // 셀 격자 → 겹치지 않는 고체 사각형(행 단위 병합 후 세로 병합). y 위 방향.
        public static List<RectInt> SolidRects(BlockWorld w)
        {
            var result=new List<RectInt>();var open=new Dictionary<long,int>();
            for(int y=0;y<w.H;y++)
            {
                var next=new Dictionary<long,int>();
                for(int x=0;x<w.W;)
                {
                    if(w.grid[y*w.W+x]==0){x++;continue;}
                    int s=x;while(x<w.W&&w.grid[y*w.W+x]!=0)x++;long key=(long)s*w.W+x;
                    if(open.TryGetValue(key,out int i)){var r=result[i];r.height++;result[i]=r;}
                    else{i=result.Count;result.Add(new RectInt(s,y,x-s,1));}
                    next[key]=i;
                }
                open=next;
            }
            return result;
        }
    }
}
