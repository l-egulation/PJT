using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class Chapter1RoomCatalog
    {
        public string schema;
        public Chapter1RoomTemplate[] rooms;
    }
    [Serializable] public sealed class Chapter1RoomTemplate
    {
        public string id,name;
        public int width,height;
        public bool mirrorable;
        public Chapter1ConnectorPort[] ports;
        public string[] clearRoute;
        // Authored feet positions for traversal verification, not an enemy clear order.
        public Point[] traversalRoute;
        public Chapter1RoomTerrain[] terrain;
        public Chapter1RoomMonster[] monsters;
        public Chapter1RoomDecoration[] decorations;
        public VisualCell[] renderGround,renderPlatforms;
    }
    [Serializable] public sealed class Chapter1RoomTerrain
    {
        public string id,kind;
        public float x,y,w,h;
    }
    [Serializable] public sealed class Chapter1RoomMonster
    {
        public string id,species;
        public float x,y;
    }
    [Serializable] public sealed class Chapter1ConnectorCatalog
    {
        public string schema;
        public Chapter1ConnectorTemplate[] connectors;
    }
    [Serializable] public sealed class Chapter1ConnectorTemplate
    {
        public string id;
        public int width,height;
        public Chapter1ConnectorPort[] ports;
        public Chapter1RoomTerrain[] terrain;
    }
    [Serializable] public sealed class Chapter1ConnectorPort
    {
        public string side,role;
        public int y;
    }

    // 블록 격자 원형(data/map/block_rooms.json)과 시드 배치기(BlockMap.BlockGenerator)로 1챕터 맵을 만든다.
    // 결과는 기존 ForestData 계약(타일 y 아래 방향, 발판·액터·시점은 픽셀)을 그대로 따른다.
    public static class Chapter1ProceduralMap
    {
        // 주 경로 전투방 6, 전투 우회 1, 엘리트 갈래 전투방 1. 정비소 뒤 주 경로 자리 1곳은 봉인 도전방, 우회 갈래 하나는 잠긴 상자방이다.
        public const int CombatRooms=8;
        // 주 경로 전투방 2곳의 템플릿 나무 발판 40%(반올림, 최소 1)를 지워지는 발판으로 바꾼다(시험값, 사용자 결정 2026-11-05).
        public static int NextSeed()=>BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0)&int.MaxValue;
        // 보스방 안쪽은 정확히 카메라 한 화면(30u)이다. 시계탑 집행자·보스 출입 장치가 이 폭과 바닥 15행을 쓴다.
        public const int BossRoomWidth=30,BossFootRow=15;
        public static ForestData Generate(ForestData source,RoundedTerrainLayout artwork,int seed)
        {
            if(source==null||source.monsters==null)throw new InvalidOperationException("Chapter1 procedural rooms require the authored ver2 source.");
            if(artwork==null||artwork.schema!="chapter1-rounded-runtime-v2"||artwork.geometrySha256!=Chapter1RoundedTerrain.GeometryHash(source))
                throw new InvalidOperationException("The approved room artwork does not match its source map.");
            Chapter1NewMonsterCatalog.ExtendSource(source);
            BlockMap.BlockLayout layout=null;
            for(int i=0;i<8&&layout==null;i++)layout=BlockMap.BlockGenerator.Stage1(seed+i*977);
            if(layout==null)throw new InvalidOperationException("No chapter 1 block layout for seed "+seed);
            var world=BlockMap.BlockAssembler.Assemble(layout);
            if(world.issues.Count>0)throw new InvalidOperationException("Chapter 1 block layout: "+string.Join(" / ",world.issues));
            var data=FromBlocks(source,artwork,world,seed);
            Validate(data);
            return data;
        }
        static string SideName(char c)=>c=='L'?"left":c=='R'?"right":c=='T'?"top":"bottom";
        static ForestData FromBlocks(ForestData source,RoundedTerrainLayout artwork,BlockMap.BlockWorld world,int seed)
        {
            int cols=world.W,rows=world.H;
            int Down(float yUp)=>rows-Mathf.RoundToInt(yUp);
            var ground=new int[cols*rows];
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)ground[(rows-1-y)*cols+x]=world.grid[y*cols+x];
            var layout=world.layout;
            // 방 순서: 주 경로(시작 0번 … 보스 9번) 다음 갈래 방. 통로 블록은 방이 아니다.
            var order=layout.mainKeys.Concat(layout.rooms.Select(r=>r.key).Where(k=>!layout.mainKeys.Contains(k))).ToList();
            var placed=order.Select(k=>world.rooms.First(r=>r.key==k)).ToList();
            int IdOf(string key)=>placed.FindIndex(r=>r.key==key);
            // 연결: 통로 블록을 사이에 둔 두 링크는 방-방 통로 하나로 합친다
            var links=layout.links;
            var passages=new List<(string a,string sa,string b,string sb,List<BlockMap.BlockWorldRoom> conns)>();
            foreach(var l in links)
            {
                bool ac=l.a.StartsWith("c"),bc=l.b.StartsWith("c");
                if(!ac&&!bc){passages.Add((l.a,l.na,l.b,l.nb,new List<BlockMap.BlockWorldRoom>()));continue;}
                if(!ac&&bc)
                {
                    var other=links.First(m=>!m.Equals(l)&&(m.a==l.b||m.b==l.b));
                    string bKey=other.a==l.b?other.b:other.a,bSlot=other.a==l.b?other.nb:other.na;
                    passages.Add((l.a,l.na,bKey,bSlot,new List<BlockMap.BlockWorldRoom>{world.rooms.First(r=>r.key==l.b)}));
                }
            }
            (string side,int row) Door(string key,string slot)
            {
                var r=world.rooms.First(x=>x.key==key);var e=BlockMap.BlockAssembler.Edge(r,slot);
                int floor=r.y+BlockMap.BlockSpec.Frame;
                return (SideName(e.side),e.side=='L'||e.side=='R'?Down(e.y):Down(floor));
            }
            var rooms=new List<ForestRoom>();var landmarks=new List<ForestLandmark>();var actors=new List<MonsterPlacement>();
            var species=BlockMap.BlockSpec.Catalog.species.s1;
            for(int id=0;id<placed.Count;id++)
            {
                var r=placed[id];var def=r.def;
                string kind=def.kind,name=def.name;
                int floorDown=Down(r.y+BlockMap.BlockSpec.Frame);
                var room=new ForestRoom{id=id,x=r.x,y=rows-(r.y+r.h),w=r.w,h=r.h,kind=kind,name=name,mirrored=r.mirror,
                    mainPath=layout.mainKeys.Contains(r.key),
                    sourceZone=kind=="start"?1:kind=="maintenance"?5:kind=="elite"?12:kind=="boss"?24:100+Array.IndexOf(BlockMap.BlockSpec.Catalog.rooms,def)};
                // 들어오는 쪽·나가는 쪽: 주 경로 앞뒤 방, 갈래 방은 붙은 방
                var inP=passages.FirstOrDefault(p=>p.b==r.key);var outP=passages.FirstOrDefault(p=>p.a==r.key);
                (string side,int row) entry=inP.a!=null?Door(r.key,inP.sb):("left",floorDown);
                (string side,int row) exit=outP.a!=null?Door(r.key,outP.sa):(entry.side=="left"?"right":"left",floorDown);
                room.entrySide=entry.side;room.exitSide=exit.side;room.entryY=entry.row-room.y;room.exitY=exit.row-room.y;
                if(kind=="maintenance")room.entryY=room.exitY=floorDown-room.y;
                if(kind=="boss")
                {
                    // 보스 장치 계약: 안쪽 30u, 바닥 = room.y+15, 입구 왼쪽 벽
                    room.x=r.x+def.frameSide;room.w=BossRoomWidth;room.y=floorDown-BossFootRow;room.h=BossFootRow+3;
                    room.entrySide="left";room.exitSide="right";room.entryY=room.exitY=BossFootRow;
                }
                rooms.Add(room);
                if(kind=="seal"||kind=="chest")
                    landmarks.Add(new ForestLandmark{kind=kind,name=kind=="seal"?"봉인 석판":"잠긴 상자",
                        x=(r.x+r.build.markerX)*32,y=Down(r.y+r.build.markerY)*32,zone=id});
                if(kind=="maintenance"||kind=="elite"||kind=="boss")
                    landmarks.Add(new ForestLandmark{kind=kind,name=kind=="maintenance"?"정비소":kind=="elite"?"엘리트 몬스터방":"최종보스",
                        x=(room.x+room.w/2f)*32,y=floorDown*32,zone=id});
                foreach(var e in world.enemies.Where(e=>e.owner==r.key))
                {
                    if(e.t=="W")continue;// 엘리트는 엘리트 표식에서 생성한다(Stage1VillageMonsterContent)
                    string sp=species.Of(e.t);int k=Array.FindIndex(source.monsters,m=>m.id==sp);
                    if(k<0)throw new InvalidOperationException("Unknown room monster: "+sp);
                    actors.Add(new MonsterPlacement{kind=k,zone=id,x=e.x*32,y=Down(e.y)*32,roaming=true,roamSeed=seed^((id+1)*1000+actors.Count)});
                }
            }
            var forestPassages=new List<ForestPassage>();
            foreach(var p in passages)
            {
                var spaces=new List<GroundRect>();
                void AddTunnel(string key,string slot)
                {
                    var r=world.rooms.First(x=>x.key==key);var e=BlockMap.BlockAssembler.Edge(r,slot);int f=BlockMap.BlockSpec.Frame;
                    // 좌우 출입구: 이 방의 좌우 벽 두께(원형마다 다름)만큼. 맞은편 방·통로 블록은 자기 쪽을 더한다.
                    if(e.side=='L'||e.side=='R'){int fs=r.IsConnector?0:r.def.frameSide;spaces.Add(new GroundRect{x=e.side=='L'?e.x:e.x-fs,y=Down(e.y+BlockMap.BlockSpec.DoorH),w=fs,h=BlockMap.BlockSpec.DoorH});}
                    else spaces.Add(new GroundRect{x=e.x,y=Down(e.y+f),w=BlockMap.BlockSpec.DoorW,h=2*f});
                }
                AddTunnel(p.a,p.sa);AddTunnel(p.b,p.sb);
                foreach(var c in p.conns)spaces.Add(new GroundRect{x=c.x,y=rows-(c.y+c.h),w=c.w,h=c.h});
                int x0=spaces.Min(s=>s.x),x1=spaces.Max(s=>s.x+s.w);
                var da=Door(p.a,p.sa);var db=Door(p.b,p.sb);
                int top=Math.Min(da.row,db.row),bottom=Math.Max(da.row,db.row);
                forestPassages.Add(new ForestPassage{from=IdOf(p.a),to=IdOf(p.b),x=x0,y=top,w=x1-x0,h=bottom-top+1,spaces=spaces.ToArray()});
            }
            // No vanishing decks in the combat map (playtest 2026-11-06): only the sky parkour's crumbling stones drop away.
            // Template flags are ignored, the chest room's included.
            var platforms=world.plats.Select(p=>new FoliagePlatform{x=p.x*32,y=Down(p.y)*32,w=p.w*32}).ToList();
            var start=placed[0];
            // 시작 위치는 시작방 원형의 spawn(방 안쪽 좌표)이다. 판정 도구가 S1-START의 spawn 존재·지지면을 보장한다.
            var spawnAt=start.def.spawn??throw new InvalidOperationException("The start room prototype has no spawn.");
            int innerW=start.w-2*start.def.frameSide;
            float spawnX=start.x+start.def.frameSide+(start.mirror?innerW-spawnAt.x:spawnAt.x),spawnY=start.y+BlockMap.BlockSpec.Frame+spawnAt.y;
            var data=new ForestData{width=cols*32,height=rows*32,cols=cols,rows=rows,ground=ground,rects=BuildRects(ground,cols,rows),
                platforms=platforms.ToArray(),renderGround=BuildGroundArtwork(source,artwork,ground,cols,rows),renderPlatforms=WoodArtwork(platforms),
                layers=source.layers,monsters=source.monsters,actors=actors.ToArray(),landmarks=landmarks.ToArray(),encounters=source.encounters,
                rooms=rooms.ToArray(),passages=forestPassages.ToArray(),generationSeed=seed,nodes=Array.Empty<RouteNode>(),route=Array.Empty<RouteEdge>(),
                passageSpaces=forestPassages.Select(p=>new GroundRect{x=p.x,y=p.y-9,w=p.w,h=p.h+11}).ToArray(),
                spawn=new Point{x=spawnX*32,y=(rows-spawnY)*32}};
            data.views=rooms.Select(room=>new ForestView{name=(room.id+1).ToString("00")+" "+room.name,
                x=(room.x+room.w/2f)*32,y=(room.y+Math.Max(room.entryY,1))*32,a=room.x*32,b=(room.x+room.w)*32}).ToArray();
            return data;
        }
        // 둥근 지형 표시(팀 스케치를 끈 경우)용 나무 발판 그림. 승인 원형 카탈로그의 나무 타일 id를 쓴다.
        static VisualCell[] WoodArtwork(List<FoliagePlatform> platforms)
        {
            var asset=Resources.Load<TextAsset>("Chapter1Rooms");
            var catalog=asset?JsonUtility.FromJson<Chapter1RoomCatalog>(asset.text):null;
            var reference=catalog?.rooms?[0];var part=reference?.terrain==null?null:Array.Find(reference.terrain,t=>t.kind=="wood"&&t.w>=3);
            if(part==null||reference.renderPlatforms==null)return Array.Empty<VisualCell>();
            int Tile(int offset)=>Array.Find(reference.renderPlatforms,c=>c.x==Mathf.RoundToInt(part.x)+offset&&c.y==Mathf.FloorToInt(part.y))?.id??0;
            int left=Tile(0),middle=Tile(1),right=Tile(Mathf.RoundToInt(part.w)-1);
            var cells=new List<VisualCell>();
            foreach(var p in platforms){int w=p.w/32;for(int i=0;i<w;i++)cells.Add(new VisualCell{x=p.x/32+i,y=p.y/32,id=i==0?left:i==w-1?right:middle});}
            return cells.ToArray();
        }

        static VisualCell[] BuildGroundArtwork(ForestData source,RoundedTerrainLayout artwork,int[] ground,int Columns,int Rows)
        {
            // Select existing native-size atlas tiles by their complete 5x5 context.
            // Reusing a floor strip on tall walls leaves grass seams and transparent
            // holes; the final occupancy must own both inner fill and exposed edges.
            int Mask(int[] cells,int cols,int rows,int x,int y)
            {
                int mask=0;
                for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
                    if(x+dx>=0&&x+dx<cols&&y+dy>=0&&y+dy<rows&&cells[(y+dy)*cols+x+dx]!=0)
                        mask|=1<<((dy+2)*5+dx+2);
                return mask;
            }
            var samples=new Dictionary<long,int>();
            foreach(var cell in artwork.ground)
            {
                int mask=Mask(source.ground,source.cols,source.rows,cell.x,cell.y);
                long key=((long)mask<<4)|(cell.x&3)|((cell.y&3)<<2);
                if(!samples.ContainsKey(key))samples.Add(key,cell.id);
            }
            var chosen=new Dictionary<long,int>();
            var result=new List<VisualCell>();
            const int center=1<<12,cardinal=(1<<7)|(1<<11)|(1<<13)|(1<<17);
            int Bits(int value){int count=0;while(value!=0){value&=value-1;count++;}return count;}
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
            {
                int mask=Mask(ground,Columns,Rows,x,y);
                bool draw=(mask&center)!=0;
                if(!draw)
                    foreach(int dx in new[]{-1,1})foreach(int dy in new[]{-1,1})
                    {
                        int corner=(1<<(12+dx))|(1<<(12+dy*5))|(1<<(12+dx+dy*5));
                        if((mask&corner)==corner)draw=true;
                    }
                if(!draw)continue;
                int phase=(x&3)|((y&3)<<2);
                long key=((long)mask<<4)|phase;
                if(!chosen.TryGetValue(key,out int id))
                {
                    if(!samples.TryGetValue(key,out id))
                    {
                        int best=int.MaxValue;
                        foreach(var sample in samples)
                        {
                            int difference=mask^(int)(sample.Key>>4);
                            if((difference&center)!=0)continue;
                            int cost=Bits(difference)*8+Bits(difference&cardinal)*64+
                                (((sample.Key&3)!=(phase&3))?2:0)+(((sample.Key&12)!=(phase&12))?1:0);
                            if(cost<best){best=cost;id=sample.Value;}
                        }
                        if(best==int.MaxValue)throw new InvalidOperationException("No terrain artwork for stage earth.");
                    }
                    chosen.Add(key,id);
                }
                result.Add(new VisualCell{x=x,y=y,id=id});
            }
            return result.ToArray();
        }

        static GroundRect[] BuildRects(int[] ground,int Columns,int Rows)
        {
            var result=new List<GroundRect>();
            var previous=new Dictionary<long,GroundRect>();
            for(int y=0;y<Rows;y++)
            {
                var current=new Dictionary<long,GroundRect>();
                for(int x=0;x<Columns;)
                {
                    if(ground[y*Columns+x]==0){x++;continue;}
                    int start=x;while(x<Columns&&ground[y*Columns+x]!=0)x++;
                    long key=(long)start*Columns+x;
                    if(previous.TryGetValue(key,out var rect))rect.h++;
                    else{rect=new GroundRect{x=start,y=y,w=x-start,h=1};result.Add(rect);}
                    current.Add(key,rect);
                }
                previous=current;
            }
            return result.ToArray();
        }

        public static void Validate(ForestData data)
        {
            int combat=0,maintenance=0,boss=0,seal=0,chest=0;
            var seen=new HashSet<int>();
            foreach(var room in data.rooms)
            {
                if(!seen.Add(room.sourceZone))throw new InvalidOperationException("A room template was repeated in one run.");
                if(room.kind=="combat")combat++;
                if(room.kind=="maintenance"&&room.mainPath)maintenance++;
                if(room.kind=="boss")boss++;
                if(room.kind=="seal"&&room.mainPath)seal++;
                if(room.kind=="chest"&&!room.mainPath)chest++;
            }
            if(data.rooms.Length==0||data.rooms[0].kind!="start"||data.rooms[0].exitSide!="right")
                throw new InvalidOperationException("The start room must be first and exit to the right.");
            if(combat!=CombatRooms||maintenance!=1||boss!=1||seal!=1||chest!=1||
               Array.FindAll(data.rooms,r=>r.kind=="seal"||r.kind=="chest").Length!=2||Array.FindIndex(data.rooms,r=>r.kind=="boss")!=9||
               Array.Exists(data.rooms,r=>r.kind=="shop"||r.kind=="maintenance"&&!r.mainPath))
                throw new InvalidOperationException("Procedural room composition is incomplete.");
            var reached=new HashSet<int>{0};
            bool changed;
            do
            {
                changed=false;
                foreach(var edge in data.passages)
                    if(reached.Contains(edge.from))changed|=reached.Add(edge.to);
                    else if(reached.Contains(edge.to))changed|=reached.Add(edge.from);
            }while(changed);
            if(reached.Count!=data.rooms.Length)throw new InvalidOperationException("A procedural room cannot be reached.");
            foreach(var room in data.rooms)if(room.kind=="combat")
            {
                int count=0;foreach(var actor in data.actors)if(actor.zone==room.id)count++;
                if(count<15)throw new InvalidOperationException("A combat room has fewer than fifteen monsters.");
            }
        }
    }
}
