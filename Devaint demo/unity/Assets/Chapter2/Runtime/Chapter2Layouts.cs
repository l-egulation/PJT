using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class C2Point { public float x,y; public Vector2 Vector => new Vector2(x,y); }
    [Serializable] public sealed class C2Surface { public string id; public float x,y,w,h; public bool oneWay; public Rect Bounds=>new Rect(x,y,w,h); }
    [Serializable] public sealed class C2WallJumpSpace { public string id;public float x,y,w,h,previousWidth;public Rect Bounds=>new Rect(x,y,w,h); }
    [Serializable] public sealed class C2Bounds { public float x,y,w,h;public Rect Bounds=>new Rect(x,y,w,h); }
    [Serializable] public sealed class C2Room { public string id,kind,prototypeId; public float x,y,w,h; public bool main; public Rect Bounds=>new Rect(x,y,w,h); }
    [Serializable] public sealed class C2Port { public string roomId,id,side; public float x,y,opening; public bool enabled; }
    [Serializable] public sealed class C2Actor { public string id,roomId,species; public float x,y; public bool flipX; }
    [Serializable] public sealed class C2Connection { public string id,from,to,fromPortId,toPortId; public C2Point[] points; }
    // 봉인 도전방·잠긴 상자방(stage-gimmicks.md): 석판·상자 발 위치와 봉인 철창(쓰는 좌우 출입구마다 하나)
    [Serializable] public sealed class C2Gimmick { public string roomId; public float markerX,markerY; public C2Bounds[] gates; public C2Bounds drop; public Vector2 Marker=>new Vector2(markerX,markerY); }
    [Serializable] public sealed class C2Layout
    {
        public string id,name,sourceSha256; public int variant;
        public C2Room[] rooms; public C2Port[] ports; public C2Surface[] surfaces; public C2Actor[] actors; public C2Connection[] connections;
        public C2Point spawn,maintenance,boss,exit;
        public C2WallJumpSpace[] wallJumpSpaces;public C2Bounds sealedBounds;
        // 보스 앞 48u 가로 통로의 안쪽(x·바닥 y·폭 48·높이 6). bossHallToLeft: 보스방이 통로 왼쪽 끝에 있다
        public C2Bounds bossHall;public bool bossHallToLeft;
        public C2Gimmick seal,chest;
        // 열쇠 운반자를 고르는 방: 잠긴 상자방 갈래가 갈라진 자리 뒤의 주 경로 전투방
        public string[] carrierRooms;
        // 열쇠 운반자 경로용 타일 지도(1챕터 ForestData 형식, 0행이 맨 위). 월드 y = carrierMapOffset - 행.
        [NonSerialized] public ForestCombatV2.ForestData carrierMap;public float carrierMapOffset;
        public C2Room Room(string key)=>Array.Find(rooms,r=>r.id==key);
        public C2Room RoomAt(Vector2 feet)=>Array.Find(rooms,r=>r.Bounds.Contains(feet+Vector2.up*.25f));
    }
    [Serializable] public sealed class C2Bank
    {
        public string schema,sourceSha256; public float platformThickness; public C2Layout[] layouts;
        // 2챕터 맵은 런타임 조립(Chapter2BlockLayout)이다. 고정 배치 묶음은 더 이상 읽지 않는다.
        public static C2Bank Load()=>new C2Bank{schema="deviant-chapter2-runtime-v2",platformThickness=.5f,layouts=Array.Empty<C2Layout>()};
        // 같은 시드는 같은 배치를 만든다. family 인자는 쓰지 않는다(호출 호환용).
        public C2Layout Generate(int seed,string family=null)
        {
            var layout=Chapter2BlockLayout.Generate(seed);var errors=Validate(layout);
            if(errors.Count>0)throw new InvalidOperationException("Chapter2 runtime layout invalid: "+string.Join(" / ",errors));
            return layout;
        }
        // 주 경로 9곳 중 전투방 9와 순환 갈래 전투방 1. 전투방 2곳 자리를 봉인 도전방·잠긴 상자방이 쓴다(사용자 결정 2026-11-13).
        public const int CombatRooms=10;
        public static List<string> Validate(C2Layout l)
        {
            var e=new List<string>();if(l?.rooms==null||l.rooms.Length!=16||l.rooms.Select(r=>r.id).Distinct().Count()!=16)return new List<string>{"Expected sixteen rooms"};
            if(l.rooms.Count(r=>r.kind=="combat")!=CombatRooms||l.rooms.Where(r=>r.kind=="combat").Select(r=>r.prototypeId).Distinct().Count()!=CombatRooms)e.Add("Expected ten unique combat prototypes");
            foreach(var k in new[]{"start","maintenance","elite","boss","seal","chest"})if(l.rooms.Count(r=>r.kind==k)!=1)e.Add("Special room count "+k);
            if(l.seal==null||l.chest==null||l.seal.gates==null||l.seal.gates.Length==0)e.Add("Missing gimmick sites");
            if(l.surfaces==null||l.actors==null||l.connections==null||l.ports==null)return new List<string>{"Missing layout arrays"};
            bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
            foreach(var r in l.rooms)if(!Finite(r.x)||!Finite(r.y)||!Finite(r.w)||!Finite(r.h)||r.w<=0||r.h<=0)e.Add("Invalid room "+r.id);
            foreach(var s in l.surfaces)if(s.w<=0||s.h<=0||!Finite(s.x)||!Finite(s.y)||!Finite(s.w)||!Finite(s.h)||s.oneWay&&Mathf.Abs(s.h-.5f)>.0001f)e.Add("Invalid surface "+s.id);
            foreach(var a in l.actors)if(l.Room(a.roomId)==null||!Finite(a.x)||!Finite(a.y)||string.IsNullOrEmpty(a.species))e.Add("Invalid actor "+a.id);
            for(int i=0;i<l.rooms.Length;i++)for(int j=i+1;j<l.rooms.Length;j++)if(l.rooms[i].Bounds.Overlaps(l.rooms[j].Bounds))e.Add("Room overlap");
            foreach(var c in l.connections)if(l.Room(c.from)==null||l.Room(c.to)==null||!l.ports.Any(p=>p.roomId==c.from&&p.id==c.fromPortId&&p.enabled)||!l.ports.Any(p=>p.roomId==c.to&&p.id==c.toPortId&&p.enabled))e.Add("Bad connection "+c.id);
            foreach(var c in l.connections)
            {
                if(c.points==null||c.points.Length<2){e.Add("Missing corridor "+c.id);continue;}
                var a=l.ports.FirstOrDefault(p=>p.roomId==c.from&&p.id==c.fromPortId);var b=l.ports.FirstOrDefault(p=>p.roomId==c.to&&p.id==c.toPortId);
                if(a!=null&&Vector2.Distance(c.points[0].Vector,new Vector2(a.x,a.y))>.01f||b!=null&&Vector2.Distance(c.points[c.points.Length-1].Vector,new Vector2(b.x,b.y))>.01f)e.Add("Corridor does not meet port "+c.id);
                for(int i=0;i<c.points.Length;i++){var p=c.points[i];if(!Finite(p.x)||!Finite(p.y))e.Add("Non-finite corridor "+c.id);if(i>0&&Mathf.Abs(p.x-c.points[i-1].x)>.001f&&Mathf.Abs(p.y-c.points[i-1].y)>.001f)e.Add("Non-orthogonal corridor "+c.id);}
            }
            var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue("S");while(queue.Count>0){var id=queue.Dequeue();if(!seen.Add(id))continue;foreach(var c in l.connections){if(c.from==id)queue.Enqueue(c.to);else if(c.to==id)queue.Enqueue(c.from);}}
            if(seen.Count!=16)e.Add("Disconnected stage");return e;
        }
    }
}
