using System;
using System.Collections.Generic;
using System.Linq;

namespace ForestCombatV2.BlockMap
{
    // 시드 배치기. tools/map/generator.js와 같은 난수 순서·후보 순서를 쓴다(같은 시드 → 같은 배치).
    // 1스테이지: 왼쪽 출발 가로 진행. 2스테이지: 위에서 내려갔다가 올라오는 세로 중심.
    public static class BlockGenerator
    {
        sealed class Rng
        {
            uint a;public Rng(long seed){a=unchecked((uint)seed);}
            public double Next(){unchecked{a+=0x6D2B79F5;uint t=(a^(a>>15))*(1|a);t=(t+((t^(t>>7))*(61|t)))^t;return (t^(t>>14))/4294967296.0;}}
        }
        static List<T> Shuffle<T>(IEnumerable<T> src,Rng r){var a=src.ToList();for(int i=a.Count-1;i>0;i--){int j=(int)Math.Floor(r.Next()*(i+1));(a[i],a[j])=(a[j],a[i]);}return a;}
        static T Pick<T>(IList<T> a,Rng r)=>a[(int)Math.Floor(r.Next()*a.Count)];

        sealed class Item { public bool conn;public string key,refId,role;public BlockRoomDef room;public bool mirror;public int bx,by,bw,bh;public char[] open; }
        sealed class Plan
        {
            public List<Item> items=new();public List<(string a,string na,string b,string nb)> links=new();public Dictionary<string,List<string>> used=new();public int n;
            public bool Hit(int bx,int by,int bw,int bh){foreach(var p in items)if(bx<p.bx+p.bw&&p.bx<bx+bw&&by<p.by+p.bh&&p.by<by+bh)return true;return false;}
            public IEnumerable<string> SlotsOf(Item it)=>it.conn?it.open.Select(o=>o+"0"):it.mirror?it.room.slots.Select(s=>BlockSpec.MirrorId(s,it.room.bw)):it.room.slots;
            public List<string> Free(Item it){used.TryGetValue(it.key,out var u);var r=new List<string>();foreach(var s in it.conn?it.open.Select(o=>o+"0"):Slots(it.room,it.mirror))if(u==null||!u.Contains(s))r.Add(s);return r;}
            void Use(Item it,string s){if(!used.TryGetValue(it.key,out var u))used[it.key]=u=new List<string>();u.Add(s);}
            public void Link(Item a,string na,Item b,string nb){Use(a,na);Use(b,nb);links.Add((a.key,na,b.key,nb));}
            public (int i,int l,Dictionary<string,List<string>> u,int n) Snap()=>(items.Count,links.Count,used.ToDictionary(p=>p.Key,p=>p.Value.ToList()),n);
            public void Undo((int i,int l,Dictionary<string,List<string>> u,int n) s){items.RemoveRange(s.i,items.Count-s.i);links.RemoveRange(s.l,links.Count-s.l);used=s.u.ToDictionary(p=>p.Key,p=>p.Value.ToList());n=s.n;}
            public Item Room(string refId,bool mirror,int bx,int by,string role){var d=BlockSpec.Room(refId);var it=new Item{key=role+(++n),refId=refId,room=d,mirror=mirror,bx=bx,by=by,bw=d.bw,bh=d.bh,role=role};items.Add(it);return it;}
            public Item Conn(int bx,int by,char[] open,int bw=1){var it=new Item{conn=true,key="c"+(++n),open=open,bx=bx,by=by,bw=bw,bh=1};items.Add(it);return it;}
        }
        // 슬롯 위치(블록 좌표). 좌우: x=경계, y=행 / 상하: x=열, y=경계
        static (char side,int x,int y) Pos(Item it,string n)
        {
            char s=n[0];int k=BlockSpec.SlotIndex(n);
            return s=='L'?(s,it.bx,it.by+k):s=='R'?(s,it.bx+it.bw,it.by+k):s=='T'?(s,it.bx+k,it.by+it.bh):(s,it.bx+k,it.by);
        }
        static bool Match((char side,int x,int y) a,(char side,int x,int y) b)=>BlockSpec.Opp(a.side)==b.side&&a.x==b.x&&a.y==b.y;
        static (int bx,int by) PosFrom((char side,int x,int y) a,BlockRoomDef room,string t)
        {
            int k=BlockSpec.SlotIndex(t);
            return a.side=='R'?(a.x,a.y-k):a.side=='L'?(a.x-room.bw,a.y-k):a.side=='T'?(a.x-k,a.y):(a.x-k,a.y-room.bh);
        }
        static (int bx,int by) ConnAt((char side,int x,int y) a)=>a.side=='R'?(a.x,a.y):a.side=='L'?(a.x-1,a.y):a.side=='T'?(a.x,a.y):(a.x,a.y-1);
        sealed class Opt { public char[] sides;public bool conn,bend,climb;public Func<int,int,int,int,bool> bound;public double? wDirect,wConn,wClimb;public char next;public Func<List<Cand>,Cand> choose; }
        sealed class Cand { public string xs,t;public bool mirror;public int bx,by;public (int bx,int by,char[] open,bool climb)? via;public double w; }
        static readonly Dictionary<BlockRoomDef,string[]> mirrored=new();
        static string[] Slots(BlockRoomDef room,bool mirror)
        {
            if(!mirror)return room.slots;
            if(!mirrored.TryGetValue(room,out var s))mirrored[room]=s=room.slots.Select(n=>BlockSpec.MirrorId(n,room.bw)).ToArray();
            return s;
        }
        // 원형·반전별 슬롯을 방향(L·R·T·B)으로 나눠 둔다
        static readonly Dictionary<(BlockRoomDef,bool),Dictionary<char,string[]>> bySide=new();
        static string[] SlotsOn(BlockRoomDef room,bool mirror,char side)
        {
            if(!bySide.TryGetValue((room,mirror),out var map))
            {
                map=new Dictionary<char,string[]>();var all=Slots(room,mirror);
                foreach(char s in new[]{'L','R','T','B'})map[s]=all.Where(n=>n[0]==s).ToArray();
                bySide[(room,mirror)]=map;
            }
            return map[side];
        }
        static double Straight(BlockRoomDef room,bool mirror,string t,char next)
        {
            if(next=='\0')return 1;
            int entry=t[0]=='T'||t[0]=='B'?BlockSpec.SlotIndex(t):t[0]=='L'?0:room.bw-1;
            int d=int.MaxValue;foreach(var n in SlotsOn(room,mirror,next))if(n!=t)d=Math.Min(d,Math.Abs(BlockSpec.SlotIndex(n)-entry));
            if(d==int.MaxValue)return .05;return d==0?1:d==1?.3:.1;
        }
        static List<Cand> Candidates(Plan plan,Item X,string refId,Opt o)
        {
            var room=BlockSpec.Room(refId);var list=new List<Cand>();
            foreach(string xs in plan.Free(X))
            {
                var a=Pos(X,xs);if(o.sides!=null&&!o.sides.Contains(a.side))continue;
                foreach(bool mirror in new[]{false,true})
                {
                    foreach(string t in SlotsOn(room,mirror,BlockSpec.Opp(a.side)))
                    {
                        var p=PosFrom(a,room,t);
                        if(!plan.Hit(p.bx,p.by,room.bw,room.bh)&&(o.bound==null||o.bound(p.bx,p.by,room.bw,room.bh)))
                            list.Add(new Cand{xs=xs,mirror=mirror,t=t,bx=p.bx,by=p.by,w=(o.wDirect??1)*Straight(room,mirror,t,o.next)});
                    }
                    if(!o.conn)continue;
                    var cp=ConnAt(a);if(plan.Hit(cp.bx,cp.by,1,1))continue;
                    foreach(char so in new[]{'L','R','T','B'})
                    {
                        if(so==BlockSpec.Opp(a.side))continue;
                        char inner=BlockSpec.Opp(a.side);bool climb=(inner=='T'||inner=='B')&&(so=='T'||so=='B');
                        bool straightLR=(inner=='L'||inner=='R')&&(so=='L'||so=='R');
                        if(climb&&!o.climb)continue;if(!climb&&!o.bend&&!straightLR)continue;
                        var cs=Pos(new Item{conn=true,bx=cp.bx,by=cp.by,bw=1,bh=1},so+"0");
                        foreach(string t in SlotsOn(room,mirror,BlockSpec.Opp(so)))
                        {
                            var p=PosFrom(cs,room,t);
                            if(p.bx<cp.bx+1&&cp.bx<p.bx+room.bw&&p.by<cp.by+1&&cp.by<p.by+room.bh)continue;
                            if(!plan.Hit(p.bx,p.by,room.bw,room.bh)&&(o.bound==null||o.bound(p.bx,p.by,room.bw,room.bh)))
                                list.Add(new Cand{xs=xs,mirror=mirror,t=t,bx=p.bx,by=p.by,via=(cp.bx,cp.by,new[]{inner,so},climb),
                                    w=(climb?(o.wClimb??.3):(o.wConn??.15))*Straight(room,mirror,t,o.next)});
                        }
                    }
                }
            }
            return list;
        }
        static Cand Weighted(List<Cand> list,Rng r){double s=0;foreach(var c in list)s+=c.w;double x=r.Next()*s;foreach(var c in list){x-=c.w;if(x<=0)return c;}return list[list.Count-1];}
        static Item Attach(Plan plan,Item X,string refId,Opt o,Rng r,string role)
        {
            Cand c;
            if(o.choose!=null)c=o.choose(Candidates(plan,X,refId,o));
            else{var l=Candidates(plan,X,refId,o);c=l.Count>0?Weighted(l,r):null;}
            if(c==null)return null;
            var it=plan.Room(refId,c.mirror,c.bx,c.by,role);
            if(c.via.HasValue)
            {var v=c.via.Value;var k=plan.Conn(v.bx,v.by,v.open);plan.Link(X,c.xs,k,v.open[0]+"0");plan.Link(k,v.open[1]+"0",it,c.t);}
            else plan.Link(X,c.xs,it,c.t);
            return it;
        }
        static Item AttachTry(Plan plan,Item X,string refId,Opt o,Rng r,string role,Func<Item,bool> test)
        {
            foreach(var c in Shuffle(Candidates(plan,X,refId,o),r))
            {
                var s=plan.Snap();
                var o2=new Opt{sides=o.sides,conn=o.conn,bend=o.bend,climb=o.climb,bound=o.bound,wDirect=o.wDirect,wConn=o.wConn,wClimb=o.wClimb,next=o.next,choose=_=>c};
                var it=Attach(plan,X,refId,o2,r,role);
                if(it!=null&&test(it))return it;plan.Undo(s);
            }
            return null;
        }
        // 보스 앞 긴 통로: X의 좌우 슬롯에서 가로 통로 블록 Hall개(48u)를 곧게 놓고 그 끝에 방을 붙인다. 통로 최대 1블록 규칙의 유일한 예외.
        public const int Hall=3;
        static Item AttachHall(Plan plan,Item X,string refId,char[] sides,Func<int,int,int,int,bool> bound,Rng r,string role)
        {
            var room=BlockSpec.Room(refId);var list=new List<(string xs,bool mirror,string t,int bx,int by,int hx,int hy,char side)>();
            foreach(string xs in plan.Free(X))
            {
                var a=Pos(X,xs);if(a.side!='L'&&a.side!='R')continue;if(sides!=null&&!sides.Contains(a.side))continue;
                int hx=a.side=='R'?a.x:a.x-Hall,hy=a.y;
                if(plan.Hit(hx,hy,Hall,1)||(bound!=null&&!bound(hx,hy,Hall,1)))continue;
                var far=(a.side,a.side=='R'?a.x+Hall:a.x-Hall,a.y);
                foreach(bool mirror in new[]{false,true})foreach(string t in SlotsOn(room,mirror,BlockSpec.Opp(a.side)))
                {
                    var p=PosFrom(far,room,t);
                    if(!plan.Hit(p.bx,p.by,room.bw,room.bh)&&(bound==null||bound(p.bx,p.by,room.bw,room.bh)))list.Add((xs,mirror,t,p.bx,p.by,hx,hy,a.side));
                }
            }
            if(list.Count==0)return null;
            var c=Pick(list,r);
            var it=plan.Room(refId,c.mirror,c.bx,c.by,role);var k=plan.Conn(c.hx,c.hy,new[]{'L','R'},Hall);
            plan.Link(X,c.xs,k,BlockSpec.Opp(c.side)+"0");plan.Link(k,c.side+"0",it,c.t);
            return it;
        }
        static bool Join(Plan plan,Item A,Item B,bool conn,bool climb)
        {
            foreach(string na in plan.Free(A))foreach(string nb in plan.Free(B))if(Match(Pos(A,na),Pos(B,nb))){plan.Link(A,na,B,nb);return true;}
            if(!conn)return false;
            foreach(string na in plan.Free(A))
            {
                var a=Pos(A,na);var cp=ConnAt(a);if(plan.Hit(cp.bx,cp.by,1,1))continue;
                var cItem=new Item{conn=true,bx=cp.bx,by=cp.by,bw=1,bh=1};
                foreach(string nb in plan.Free(B))
                {
                    var b=Pos(B,nb);
                    foreach(char so in new[]{'L','R','T','B'})
                    {
                        if(so==BlockSpec.Opp(a.side))continue;var cs=Pos(cItem,so+"0");if(!Match(cs,b))continue;
                        bool isClimb=(so=='T'||so=='B')&&(a.side=='T'||a.side=='B');if(isClimb&&!climb)continue;
                        var k=plan.Conn(cp.bx,cp.by,new[]{BlockSpec.Opp(a.side),so});plan.Link(A,na,k,BlockSpec.Opp(a.side)+"0");plan.Link(k,so+"0",B,nb);return true;
                    }
                }
            }
            return false;
        }
        static int Climbs(Plan p)=>p.items.Count(i=>i.conn&&i.open.Contains('T')&&i.open.Contains('B'));

        public static BlockLayout Stage1(int seed)
        {
            var ids=BlockSpec.Catalog.rooms.Where(r=>r.stage==1&&r.kind=="combat").Select(r=>r.id).ToList();
            var seals=BlockSpec.Catalog.rooms.Where(r=>r.stage==1&&r.kind=="seal").Select(r=>r.id).ToList();
            var chests=BlockSpec.Catalog.rooms.Where(r=>r.stage==1&&r.kind=="chest").Select(r=>r.id).ToList();
            for(int attempt=0;attempt<300;attempt++)
            {
                var r=new Rng(seed*7919L+attempt);var plan=new Plan();
                var pool=Shuffle(ids,r);
                Func<int,int,int,int,bool> band=(bx,by,bw,bh)=>by>=-4&&by+bh<=8;
                var S=plan.Room("S1-START",false,0,(int)Math.Floor(r.Next()*3),"S");
                var main=new List<Item>{S};int shopAt=r.Next()<.5?4:5;bool ok=true;
                // 봉인 도전방은 정비소 뒤 주 경로 자리(보스 앞까지), 잠긴 상자방 갈래(주 경로 a → 상자방 → a+2)는 정비소에서 또는 그 뒤에서
                // 갈라진다. 봉인방은 좌우 출입구만 있어 그 갈래의 출발·건너뜀·합류 자리가 될 수 없으므로 함께 맞는 (a, 봉인 자리)에서 고른다.
                var pairs=new List<(int a,int s)>();for(int a=shopAt;a<=6;a++)for(int s=shopAt+1;s<=8;s++)if(s<a||s>a+2)pairs.Add((a,s));
                int sealAt=Pick(pairs,r).s;string sealRef=Pick(seals,r);var chestFrom=pairs.Where(p=>p.s==sealAt).Select(p=>p.a).ToList();
                for(int i=1;i<=9&&ok;i++)
                {
                    string refId=i==shopAt?"S1-SHOP":i==9?"S1-BOSS":i==sealAt?sealRef:Pop(pool),role=i==shopAt?"SHOP":i==9?"BOSS":i==sealAt?"SEAL":"M";
                    var it=i==9?AttachHall(plan,main[i-1],refId,new[]{'R'},band,r,role):Attach(plan,main[i-1],refId,new Opt{sides=new[]{'R'},conn=true,bound=band,wConn=.08},r,role);
                    if(it==null){ok=false;break;}main.Add(it);
                }
                if(!ok)continue;
                // 우회: 주 경로 a → U → a+2 (a+1은 전투방, 정비소·봉인방·보스 건너뛰지 않음). 잠긴 상자방 갈래를 먼저 위·아래 중 되는 쪽에
                // 놓고(그보다 오른쪽 주 경로 전투방이 열쇠를 든다), 전투 우회 갈래는 반대쪽에 놓는다.
                string Bypass(char dir,IEnumerable<int> froms,Func<IEnumerable<string>> refs,string role,Func<Item,bool> test)
                {
                    foreach(int a in froms)
                    {
                        if(main[a+1].role!="M"||main[a+2].role=="BOSS")continue;
                        foreach(string refId in refs())
                            if(AttachTry(plan,main[a],refId,new Opt{sides=new[]{dir},conn=true,bend=true,climb=true},r,role,u=>Join(plan,u,main[a+2],true,true)&&test(u))!=null)return refId;
                    }
                    return null;
                }
                char chestDir=' ';
                foreach(int a in Shuffle(chestFrom,r))
                {
                    foreach(char dir in Shuffle(new[]{'T','B'},r))
                        if(Bypass(dir,new[]{a},()=>Shuffle(chests,r),"CHEST",u=>main.Any(m=>m.role=="M"&&m.bx>u.bx))!=null){chestDir=dir;break;}
                    if(chestDir!=' ')break;
                }
                if(chestDir==' ')continue;
                char other=chestDir=='T'?'B':'T';
                string used=Bypass(other,Shuffle(new[]{1,2,3,4,5,6},r),()=>Shuffle(pool,r),other=='T'?"UP":"DN",_=>true);
                if(used==null)continue;
                pool.Remove(used);
                bool elite=false;
                foreach(int h in Shuffle(new[]{5,6,7,8},r))
                {
                    if(main[h].role!="M")continue;var s=plan.Snap();
                    var E1=AttachTry(plan,main[h],pool[pool.Count-1],new Opt{sides=new[]{'T','B'},conn=true,bend=true,climb=true},r,"EB",
                        e=>Attach(plan,e,"S1-ELITE",new Opt{conn=true,bend=true},r,"ELITE")!=null);
                    if(E1!=null){Pop(pool);elite=true;break;}plan.Undo(s);
                }
                if(!elite)continue;
                if(Climbs(plan)>1)continue;
                return Finish(plan,seed,attempt,"s1",main);
            }
            return null;
        }
        public static BlockLayout Stage2(int seed)
        {
            var ids=BlockSpec.Catalog.rooms.Where(r=>r.stage==2&&r.kind=="combat").Select(r=>r.id).ToList();
            // 봉인 도전방·잠긴 상자방은 1스테이지 원형을 그대로 쓴다(2챕터 적용, 사용자 결정 2026-11-13).
            var seals=BlockSpec.Catalog.rooms.Where(r=>r.stage==1&&r.kind=="seal").Select(r=>r.id).ToList();
            var chests=BlockSpec.Catalog.rooms.Where(r=>r.stage==1&&r.kind=="chest").Select(r=>r.id).ToList();
            for(int attempt=0;attempt<600;attempt++)
            {
                var r=new Rng(seed*104729L+attempt);var plan=new Plan();
                // 주 경로(뒤에서 꺼냄)에 세로로 긴 방이 먼저 오도록 키 2블록 이하 방을 앞쪽에 둔다(안정 정렬)
                var pool=Shuffle(ids,r).OrderBy(id=>BlockSpec.Room(id).bh>2?1:0).ToList();
                var S=plan.Room("S2-START",false,0,0,"S");
                int depth=-(10+(int)Math.Floor(r.Next()*3));int dirH=r.Next()<.5?1:-1;char side=dirH>0?'R':'L',back=dirH>0?'L':'R';
                var main=new List<Item>{S};string phase="down";int across=0;bool ok=true;
                Func<int,int,int,int,bool> top=(bx,by,bw,bh)=>by+bh<=2;
                for(int i=1;i<=9;i++)
                {
                    var X=main[i-1];Item it;
                    if(phase=="down")
                    {
                        bool vert=r.Next()<.9;
                        it=Attach(plan,X,Pop(pool),new Opt{sides=vert?new[]{'B'}:new[]{side,'B'},conn=true,climb=true,bound=top,wClimb=.4,next='B'},r,"M");
                        if(it!=null&&it.by<=depth){phase="across";across=1;}
                    }
                    else if(phase=="across")
                    {
                        it=Attach(plan,X,Pop(pool),new Opt{sides=new[]{side},conn=true,bound=top},r,"M");across--;if(across<=0)phase="up";
                    }
                    else it=Attach(plan,X,Pop(pool),new Opt{sides=r.Next()<.9?new[]{'T'}:new[]{'T',side},conn=true,climb=true,bound=top,wClimb=.5,next='T'},r,"M");
                    if(it==null){ok=false;break;}main.Add(it);
                }
                if(!ok||phase!="up")continue;
                var last=main[9];if(last.by+last.bh<-1)continue;
                var B=AttachHall(plan,last,"S2-BOSS",new[]{side,back},top,r,"BOSS");if(B==null)continue;
                if(B.by+B.bh<0)continue;
                if(plan.items.Any(p=>p!=B&&p.by>=B.by+B.bh&&p.bx<B.bx+B.bw&&B.bx<p.bx+p.bw))continue;
                int mainClimb=Climbs(plan);if(mainClimb<1||mainClimb>3)continue;
                // 세로 중심: 주 경로 문→문 이동의 세로/가로 비가 2 미만이면 다시 뽑는다(주 경로만으로 정해지므로 갈래 전에 검사)
                var (th,tv)=Travel(plan,main);if(tv<2*th)continue;
                // 순환 갈래: 내려가는 쪽 방 i에서 올라오는 쪽 방 j로 전투방 1개와 잠긴 상자방
                int[] downIdx={1,2,3,4,5},upIdx={6,7,8,9};bool loop=false;int loopFrom=-1;
                for(int t=0;t<40&&!loop;t++)
                {
                    int i=Pick(downIdx,r),j=Pick(upIdx,r);if(j-i<3)continue;var s=plan.Snap();string refId=pool[pool.Count-1],chestRef=Pick(chests,r);
                    var O1=Attach(plan,main[i],refId,new Opt{conn=true,bend=true,bound=top},r,"O");
                    var O2=O1==null?null:AttachTry(plan,O1,chestRef,new Opt{conn=true,bend=true,bound=top},r,"CHEST",o=>Join(plan,o,main[j],true,false));
                    if(O2!=null){Pop(pool);loop=true;loopFrom=i;}else plan.Undo(s);
                }
                if(!loop)continue;
                // 정비소(막다른 갈래). 1스테이지처럼 상자방 갈래는 정비소 자리에서 또는 그 뒤에서 갈라진다.
                Item shop=null;int shopAt=-1;
                foreach(int h in Shuffle(new[]{2,3,4,5,6,7}.Where(h=>h<=loopFrom),r)){shop=Attach(plan,main[h],"S2-SHOP",new Opt{conn=true,bend=true,bound=top},r,"SHOP");if(shop!=null){shopAt=h;break;}}
                if(shop==null)continue;
                // 봉인 도전방: 정비소 자리 뒤 주 경로 방에서 갈라지는 막다른 방. 좌우 출입구만 있어 세로로 이어지는 주 경로 안에는
                // 넣지 않는다(넣으면 세로 진행 조건 때문에 배치가 거의 나오지 않는다).
                string sealRef=Pick(seals,r);Item seal=null;
                foreach(int h in Shuffle(new[]{3,4,5,6,7,8,9}.Where(h=>h>shopAt),r)){seal=Attach(plan,main[h],sealRef,new Opt{conn=true,bend=true,bound=top},r,"SEAL");if(seal!=null)break;}
                if(seal==null)continue;
                Item elite=null;foreach(int h in Shuffle(new[]{4,5,6,7,8,9},r)){elite=Attach(plan,main[h],"S2-ELITE",new Opt{conn=true,bend=true,bound=top},r,"ELITE");if(elite!=null)break;}
                if(elite==null)continue;
                if(Climbs(plan)>3)continue;
                var done=Finish(plan,seed,attempt,"s2",main);done.chestFrom=loopFrom;return done;
            }
            return null;
        }
        static string Pop(List<string> pool){var v=pool[pool.Count-1];pool.RemoveAt(pool.Count-1);return v;}
        // 주 경로 문→문 맨해튼 이동량(가로·세로). 통로 블록은 꺾이는 점을 지난다.
        static (float h,float v) Travel(Plan plan,List<Item> main)
        {
            (double x,double y) U(Item it,string n){var p=Pos(it,n);return p.side=='L'||p.side=='R'?(p.x*16,p.y*12+2):(p.x*16+8,p.y*12);}
            Item Find(string k)=>plan.items.First(x=>x.key==k);
            var pts=new List<(double x,double y)>{(main[0].bx*16+main[0].bw*8,main[0].by*12+2)};
            for(int i=1;i<main.Count;i++)
            {
                string a=main[i-1].key,b=main[i].key;
                var d=plan.links.FirstOrDefault(l=>(l.a==a&&l.b==b)||(l.a==b&&l.b==a));
                if(d.a!=null){var (x,nx)=d.a==a?(d.a,d.na):(d.b,d.nb);pts.Add(U(Find(x),nx));continue;}
                var l1=plan.links.First(l=>{string o=l.a==a?l.b:l.b==a?l.a:null;return o!=null&&Find(o).conn&&plan.links.Any(m=>!m.Equals(l)&&(m.a==o||m.b==o)&&(m.a==b||m.b==b));});
                var c=Find(l1.a==a?l1.b:l1.a);string na=l1.a==a?l1.na:l1.nb;
                var l2=plan.links.First(m=>!m.Equals(l1)&&(m.a==c.key||m.b==c.key)&&(m.a==b||m.b==b));string nb=l2.a==b?l2.na:l2.nb;
                pts.Add(U(Find(a),na));pts.Add((c.bx*16+8,c.by*12+2));pts.Add(U(Find(b),nb));
            }
            var last=main[main.Count-1];pts.Add((last.bx*16+last.bw*8,last.by*12+2));
            float h=0,v=0;for(int i=1;i<pts.Count;i++){h+=(float)Math.Abs(pts[i].x-pts[i-1].x);v+=(float)Math.Abs(pts[i].y-pts[i-1].y);}
            return (h,v);
        }
        static BlockLayout Finish(Plan plan,int seed,int attempt,string stage,List<Item> main)
        {
            var L=new BlockLayout{seed=seed,attempt=attempt,stage=stage};
            foreach(var i in plan.items)
            {
                if(i.conn)L.connectors.Add(new BlockPlacedConnector{key=i.key,bx=i.bx,by=i.by,bw=i.bw,open=i.open});
                else L.rooms.Add(new BlockPlacedRoom{key=i.key,refId=i.refId,role=i.role,bx=i.bx,by=i.by,mirror=i.mirror});
            }
            bool IsConn(string k)=>plan.items.First(x=>x.key==k).conn;
            foreach(var l in plan.links)L.links.Add((l.a,IsConn(l.a)?l.na.Substring(0,1):l.na,l.b,IsConn(l.b)?l.nb.Substring(0,1):l.nb));
            L.mainKeys=main.Select(m=>m.key).ToList();
            (L.horizontal,L.vertical)=Travel(plan,main);
            return L;
        }
    }
}
