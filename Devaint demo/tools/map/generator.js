// 런타임 조립 배치기 시제품(검토용). 블록 격자 위에 원형을 직결·통로 블록 1개로 붙인다.
// 출력은 engine.assemble이 받는 {rooms,connectors,links} 형식. 게임과 연결하지 않는다.
(function(root){
 'use strict';
 const RS=root.RoomSpec||require('./engine.js'),D=root.RoomData||require('./rooms.js');
 const ALL=[...D.s1,...D.s1special,...D.s2,...D.s2special],byId=id=>ALL.find(r=>r.id===id);
 const opp={L:'R',R:'L',T:'B',B:'T'};
 function rng(seed){let a=seed>>>0;return()=>{a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
 const shuffle=(a,r)=>{a=a.slice();for(let i=a.length-1;i>0;i--){const j=Math.floor(r()*(i+1));[a[i],a[j]]=[a[j],a[i]];}return a;};
 const pick=(a,r)=>a[Math.floor(r()*a.length)];
 // 슬롯 위치(블록 좌표). 좌우: x=경계, y=행 / 상하: x=열, y=경계
 function slotPos(it,n){const side=n[0],k=+n.slice(1);if(side==='L')return{side,x:it.bx,y:it.by+k};if(side==='R')return{side,x:it.bx+it.bw,y:it.by+k};if(side==='T')return{side,x:it.bx+k,y:it.by+it.bh};return{side,x:it.bx+k,y:it.by};}
 const slotsOf=it=>it.kind==='conn'?it.open.map(o=>o+'0'):(it.mirror?it.room.slots.map(n=>RS.mirrorId(n,it.room.bw)):it.room.slots);
 const match=(a,b)=>opp[a.side]===b.side&&a.x===b.x&&a.y===b.y;
 function posFrom(a,room,t){const k=+t.slice(1);
  if(a.side==='R')return{bx:a.x,by:a.y-k};if(a.side==='L')return{bx:a.x-room.bw,by:a.y-k};if(a.side==='T')return{bx:a.x-k,by:a.y};return{bx:a.x-k,by:a.y-room.bh};}
 function connAt(a){if(a.side==='R')return{bx:a.x,by:a.y};if(a.side==='L')return{bx:a.x-1,by:a.y};if(a.side==='T')return{bx:a.x,by:a.y};return{bx:a.x,by:a.y-1};}
 class Plan{
  constructor(){this.items=[];this.links=[];this.used={};this.n=0;}
  hit(r){return this.items.some(p=>r.bx<p.bx+p.bw&&p.bx<r.bx+r.bw&&r.by<p.by+p.bh&&p.by<r.by+r.bh);}
  free(it){const u=this.used[it.key]||[];return slotsOf(it).filter(n=>!u.includes(n));}
  use(it,n){(this.used[it.key]=this.used[it.key]||[]).push(n);}
  link(a,na,b,nb){this.use(a,na);this.use(b,nb);this.links.push([a.key,na,b.key,nb]);}
  snap(){return {i:this.items.length,l:this.links.length,u:JSON.stringify(this.used),n:this.n};}
  undo(s){this.items.length=s.i;this.links.length=s.l;this.used=JSON.parse(s.u);this.n=s.n;}
  room(ref,mirror,pos,role){const room=byId(ref);const it={kind:'room',key:role+(++this.n),ref,room,mirror,bx:pos.bx,by:pos.by,bw:room.bw,bh:room.bh,role};this.items.push(it);return it;}
  conn(pos,open,bw=1){const it={kind:'conn',key:'c'+(++this.n),open,bx:pos.bx,by:pos.by,bw,bh:1};this.items.push(it);return it;}
  out(){return {rooms:this.items.filter(i=>i.kind==='room').map(i=>({key:i.key,ref:i.ref,bx:i.bx,by:i.by,mirror:i.mirror,role:i.role})),connectors:this.items.filter(i=>i.kind==='conn').map(i=>({key:i.key,open:i.open,bx:i.bx,by:i.by,bw:i.bw})),links:this.links.map(([a,na,b,nb])=>{const c=k=>this.items.find(i=>i.key===k).kind==='conn';return [a,c(a)?na[0]:na,b,c(b)?nb[0]:nb];})};}
 }
 // 세로 진행 가중치: 들어온 슬롯 t와, 다음에 나갈 방향(next='B'|'T')의 슬롯 열 차이가 작을수록 높다
 function straight(room,mirror,t,next){
  if(!next)return 1;const ss=mirror?room.slots.map(n=>RS.mirrorId(n,room.bw)):room.slots;
  const entryCol=t[0]==='T'||t[0]==='B'?+t.slice(1):(t[0]==='L'?0:room.bw-1);
  const outs=ss.filter(n=>n[0]===next&&n!==t);if(!outs.length)return .05;
  const d=Math.min(...outs.map(n=>Math.abs(+n.slice(1)-entryCol)));return d===0?1:d===1?.3:.1;
 }
 // X의 빈 슬롯에 새 방을 붙이는 후보를 모두 만든다(직결 또는 통로 블록 1개)
 function candidates(plan,X,ref,o){
  const room=byId(ref),out=[];
  for(const xs of plan.free(X)){
   const a=slotPos(X,xs);if(o.sides&&!o.sides.includes(a.side))continue;
   for(const mirror of [false,true]){
    const ts=(mirror?room.slots.map(n=>RS.mirrorId(n,room.bw)):room.slots).filter(t=>t[0]===opp[a.side]);
    for(const t of ts){
     const p=posFrom(a,room,t),r={bx:p.bx,by:p.by,bw:room.bw,bh:room.bh};
     if(!plan.hit(r)&&(!o.bound||o.bound(r)))out.push({xs,mirror,t,pos:p,via:null,w:(o.wDirect??1)*straight(room,mirror,t,o.next)});
    }
    if(!o.conn)continue;
    const cp=connAt(a),cr={...cp,bw:1,bh:1};if(plan.hit(cr))continue;
    for(const so of ['L','R','T','B']){
     if(so===opp[a.side])continue;const kind=new Set([opp[a.side],so]);const climb=kind.has('T')&&kind.has('B');
     if(climb&&!o.climb)continue;if(!climb&&!o.bend&&!(kind.has('L')&&kind.has('R')))continue;
     const cs=slotPos({...cp,bw:1,bh:1},so+'0');
     for(const t of (mirror?room.slots.map(n=>RS.mirrorId(n,room.bw)):room.slots).filter(t=>t[0]===opp[so])){
      const p=posFrom(cs,room,t),r={bx:p.bx,by:p.by,bw:room.bw,bh:room.bh};
      if(r.bx<cp.bx+1&&cp.bx<r.bx+r.bw&&r.by<cp.by+1&&cp.by<r.by+r.bh)continue;
      if(!plan.hit(r)&&(!o.bound||o.bound(r)))out.push({xs,mirror,t,pos:p,via:{pos:cp,open:[opp[a.side],so],climb},w:(climb?(o.wClimb??.3):(o.wConn??.15))*straight(room,mirror,t,o.next)});
     }
    }
   }
  }
  return out;
 }
 function weighted(list,r){const s=list.reduce((n,c)=>n+c.w,0);let x=r()*s;for(const c of list){x-=c.w;if(x<=0)return c;}return list[list.length-1];}
 function attach(plan,X,ref,o,r,role){
  const c=o.choose?o.choose(candidates(plan,X,ref,o)):(()=>{const l=candidates(plan,X,ref,o);return l.length?weighted(l,r):null;})();
  if(!c)return null;
  const it=plan.room(ref,c.mirror,c.pos,role);
  if(c.via){const k=plan.conn(c.via.pos,c.via.open);plan.link(X,c.xs,k,c.via.open[0]+'0');plan.link(k,c.via.open[1]+'0',it,c.t);}
  else plan.link(X,c.xs,it,c.t);
  return it;
 }
 // 후보를 무작위 순서로 모두 시험해 test가 성공하는 첫 배치를 남긴다
 function attachTry(plan,X,ref,o,r,role,test){
  for(const c of shuffle(candidates(plan,X,ref,o),r)){
   const s=plan.snap();const it=attach(plan,X,ref,{...o,choose:()=>c},r,role);
   if(it&&test(it))return it;plan.undo(s);
  }
  return null;
 }
 // 이미 놓인 두 원형을 직결 또는 통로 블록 1개로 잇는다
 // 보스 앞 긴 통로: X의 좌우 슬롯에서 가로 통로 블록 HALL개(48u)를 곧게 놓고 그 끝에 방을 붙인다. 통로 최대 1블록 규칙의 유일한 예외.
 const HALL=3;
 function attachHall(plan,X,ref,o,r,role){
  const room=byId(ref),out=[];
  for(const xs of plan.free(X)){
   const a=slotPos(X,xs);if(a.side!=='L'&&a.side!=='R')continue;if(o.sides&&!o.sides.includes(a.side))continue;
   const hp={bx:a.side==='R'?a.x:a.x-HALL,by:a.y},hr={...hp,bw:HALL,bh:1};
   if(plan.hit(hr)||(o.bound&&!o.bound(hr)))continue;
   const far={side:a.side,x:a.side==='R'?a.x+HALL:a.x-HALL,y:a.y};
   for(const mirror of [false,true])for(const t of (mirror?room.slots.map(n=>RS.mirrorId(n,room.bw)):room.slots).filter(t=>t[0]===opp[a.side])){
    const p=posFrom(far,room,t),rr={bx:p.bx,by:p.by,bw:room.bw,bh:room.bh};
    if(!plan.hit(rr)&&(!o.bound||o.bound(rr)))out.push({xs,mirror,t,pos:p,hall:hp,side:a.side});
   }
  }
  if(!out.length)return null;
  const c=out[Math.floor(r()*out.length)];
  const it=plan.room(ref,c.mirror,c.pos,role),k=plan.conn(c.hall,['L','R'],HALL);
  plan.link(X,c.xs,k,opp[c.side]+'0');plan.link(k,c.side+'0',it,c.t);
  return it;
 }
 function join(plan,A,B,o){
  for(const na of plan.free(A))for(const nb of plan.free(B)){const a=slotPos(A,na),b=slotPos(B,nb);if(match(a,b)){plan.link(A,na,B,nb);return 'direct';}}
  if(!o.conn)return null;
  for(const na of plan.free(A)){
   const a=slotPos(A,na),cp=connAt(a),cr={...cp,bw:1,bh:1};if(plan.hit(cr))continue;
   for(const nb of plan.free(B)){const b=slotPos(B,nb);
    for(const so of ['L','R','T','B']){if(so===opp[a.side])continue;const cs=slotPos(cr,so+'0');if(!match(cs,b))continue;
     const climb=(so==='T'||so==='B')&&(a.side==='T'||a.side==='B');if(climb&&!o.climb)continue;
     const k=plan.conn(cp,[opp[a.side],so]);plan.link(A,na,k,opp[a.side]+'0');plan.link(k,so+'0',B,nb);return climb?'climb':'conn';}}
  }
  return null;
 }
 // ── 1스테이지: 가로 진행. 중단 주 경로 6전투 + 정비소(3·4방 뒤) + 봉인 도전방(정비소 뒤) + 위·아래 우회(하나는 잠긴 상자방) + 엘리트 갈래 ──
 const SEALS=D.s1special.filter(x=>x.kind==='seal').map(x=>x.id),CHESTS=D.s1special.filter(x=>x.kind==='chest').map(x=>x.id);
 function stage1(seed,F){F=F||{};const bump=k=>F[k]=(F[k]||0)+1;
  for(let attempt=0;attempt<300;attempt++){
   const r=rng(seed*7919+attempt),plan=new Plan();
   const pool=shuffle(D.s1.map(x=>x.id),r);
   const band=b=>b.by>=-4&&b.by+b.bh<=8;
   const S=plan.room('S1-START',false,{bx:0,by:Math.floor(r()*3)},'S');
   const main=[S],shopAt=r()<.5?4:5;let ok=true;
   // 봉인 도전방은 정비소 뒤 주 경로 자리(보스 앞까지), 잠긴 상자방 갈래(주 경로 a → 상자방 → a+2)는 정비소에서 또는 그 뒤에서
   // 갈라진다. 봉인방은 좌우 출입구만 있어 그 갈래의 출발·건너뜀·합류 자리가 될 수 없으므로 함께 맞는 (a, 봉인 자리)에서 고른다.
   const pairs=[];for(let a=shopAt;a<=6;a++)for(let s=shopAt+1;s<=8;s++)if(s<a||s>a+2)pairs.push([a,s]);
   const sealAt=pick(pairs,r)[1],sealRef=pick(SEALS,r),chestFrom=pairs.filter(p=>p[1]===sealAt).map(p=>p[0]);
   for(let i=1;i<=9&&ok;i++){
    const ref=i===shopAt?'S1-SHOP':i===9?'S1-BOSS':i===sealAt?sealRef:pool.pop(),role=i===shopAt?'SHOP':i===9?'BOSS':i===sealAt?'SEAL':'M';
    const it=i===9?attachHall(plan,main[i-1],ref,{sides:['R'],bound:band},r,role):attach(plan,main[i-1],ref,{sides:['R'],conn:true,bound:band,wConn:.08},r,role);
    if(!it){ok=false;break;}main.push(it);
   }
   if(!ok){bump('main');continue;}
   // 우회: 주 경로 a → U → a+2 (a+1은 전투방, 정비소·봉인방·보스 건너뛰지 않음). 잠긴 상자방 갈래를 먼저 위·아래 중 되는 쪽에
   // 놓고(그보다 오른쪽 주 경로 전투방이 열쇠를 든다), 전투 우회 갈래는 반대쪽에 놓는다.
   const bypass=(dir,froms,refs,role,test)=>{
    for(const a of froms){
     if(main[a+1].role!=='M'||main[a+2].role==='BOSS')continue;
     for(const ref of refs()){
      const U=attachTry(plan,main[a],ref,{sides:[dir],conn:true,bend:true,climb:true},r,role,u=>!!join(plan,u,main[a+2],{conn:true,climb:true})&&test(u));
      if(U)return ref;
     }
    }
    return null;
   };
   let chestDir=null;
   for(const a of shuffle(chestFrom,r)){
    for(const dir of shuffle(['T','B'],r))if(bypass(dir,[a],()=>shuffle(CHESTS,r),'CHEST',u=>main.some(m=>m.role==='M'&&m.bx>u.bx))){chestDir=dir;break;}
    if(chestDir)break;
   }
   if(!chestDir){bump('chest');continue;}
   const dir=chestDir==='T'?'B':'T',used=bypass(dir,shuffle([1,2,3,4,5,6],r),()=>shuffle(pool,r),dir==='T'?'UP':'DN',()=>true);
   if(!used){bump('bypass'+dir);continue;}
   pool.splice(pool.indexOf(used),1);
   // 엘리트 갈래: 주 경로 5~8번째 전투방에서 위·아래로 전투방 1 + 엘리트방
   let elite=false;
   for(const h of shuffle([5,6,7,8],r)){
    if(main[h].role!=='M')continue;const s=plan.snap();
    const E1=attachTry(plan,main[h],pool[pool.length-1],{sides:['T','B'],conn:true,bend:true,climb:true},r,'EB',e=>!!attach(plan,e,'S1-ELITE',{conn:true,bend:true},r,'ELITE'));
    if(E1){pool.pop();elite=true;break;}plan.undo(s);
   }
   if(!elite){bump('elite');continue;}
   const climbs=plan.items.filter(i=>i.kind==='conn'&&i.open.includes('T')&&i.open.includes('B')).length;
   if(climbs>1){bump('climb');continue;}
   // 진행 방향은 왼쪽 출발로 고정(사용자 결정 2026-11-05)
   return finish(plan,seed,attempt,'s1',main);
  }
  return null;
 }
 // ── 2스테이지: 세로 진행. 위에서 깊게 내려갔다가 다시 위로 올라와 보스(출구 위) ──
 function stage2(seed){
  for(let attempt=0;attempt<600;attempt++){
   const r=rng(seed*104729+attempt),plan=new Plan();
   // 주 경로(뒤에서 꺼냄)에 세로로 긴 방이 먼저 오도록 키 2블록 이하 방을 앞쪽에 둔다
   const pool=shuffle(D.s2.map(x=>x.id),r).sort((a,b)=>(byId(a).bh>2)-(byId(b).bh>2)||0);
   const S=plan.room('S2-START',false,{bx:0,by:0},'S');
   const depth=-(10+Math.floor(r()*3)),dirH=r()<.5?1:-1,side=dirH>0?'R':'L',back=dirH>0?'L':'R';
   const main=[S];let phase='down',across=0,ok=true,mainClimb=0;
   const top=b=>b.by+b.bh<=2;
   for(let i=1;i<=9;i++){
    const X=main[i-1];let it=null;
    if(phase==='down'){
     const vert=r()<.9;
     it=attach(plan,X,pool.pop(),{sides:vert?['B']:[side,'B'],conn:true,climb:true,bound:top,wClimb:.4,next:'B'},r,'M');
     if(it&&it.by<=depth){phase='across';across=1;}
    }else if(phase==='across'){
     it=attach(plan,X,pool.pop(),{sides:[side],conn:true,bound:top},r,'M');across--;if(across<=0)phase='up';
    }else{
     it=attach(plan,X,pool.pop(),{sides:r()<.9?['T']:['T',side],conn:true,climb:true,bound:top,wClimb:.5,next:'T'},r,'M');
    }
    if(!it){ok=false;break;}main.push(it);
   }
   if(!ok||phase!=='up')continue;
   const last=main[9];if(last.by+last.bh<-1)continue;// 마지막 전투방이 꼭대기 근처까지 올라와야 한다
   const B=attachHall(plan,last,'S2-BOSS',{sides:[side,back],bound:top},r,'BOSS');if(!B)continue;
   if(B.by+B.bh<0)continue;
   // 보스 출구(위) 위는 비어 있어야 한다
   if(plan.items.some(p=>p!==B&&p.by>=B.by+B.bh&&p.bx<B.bx+B.bw&&B.bx<p.bx+p.bw))continue;
   const mainLinks=plan.links.length;
   mainClimb=plan.items.filter(i=>i.kind==='conn'&&i.open.includes('T')&&i.open.includes('B')).length;
   if(mainClimb<1||mainClimb>3)continue;
   // 세로 중심: 주 경로 문→문 이동의 세로/가로 비가 2 미만이면 다시 뽑는다(주 경로만으로 정해지므로 갈래 전에 검사)
   {const tv=travel(plan,main);if(tv.v<2*tv.h)continue;}
   // 순환 갈래: 내려가는 쪽 방 i에서 올라오는 쪽 방 j로 전투방 1개와 잠긴 상자방(1스테이지 원형 S1-CHEST-A~C)
   const downIdx=main.map((m,k)=>k).filter(k=>k>=1&&k<=5),upIdx=main.map((m,k)=>k).filter(k=>k>=6&&k<=9);
   let loop=false,loopFrom=-1;
   for(let t=0;t<40&&!loop;t++){
    const i=pick(downIdx,r),j=pick(upIdx,r);if(j-i<3)continue;const s=plan.snap();const ref=pool[pool.length-1],chestRef=pick(CHESTS,r);
    const O1=attach(plan,main[i],ref,{conn:true,bend:true,bound:top},r,'O');
    const O2=O1&&attachTry(plan,O1,chestRef,{conn:true,bend:true,bound:top},r,'CHEST',o=>!!join(plan,o,main[j],{conn:true}));
    if(O2){pool.pop();loop=true;loopFrom=i;}else plan.undo(s);
   }
   if(!loop)continue;
   // 정비소(막다른 갈래). 1스테이지처럼 상자방 갈래는 정비소 자리에서 또는 그 뒤에서 갈라진다.
   let shop=null,shopAt=-1;for(const h of shuffle([2,3,4,5,6,7].filter(h=>h<=loopFrom),r)){shop=attach(plan,main[h],'S2-SHOP',{conn:true,bend:true,bound:top},r,'SHOP');if(shop){shopAt=h;break;}}
   if(!shop)continue;
   // 봉인 도전방(1스테이지 원형 S1-SEAL-A~C): 정비소 자리 뒤 주 경로 방에서 갈라지는 막다른 방. 좌우 출입구만 있어
   // 세로로 이어지는 주 경로 안에는 넣지 않는다.
   const sealRef=pick(SEALS,r);
   let seal=null;for(const h of shuffle([3,4,5,6,7,8,9].filter(h=>h>shopAt),r)){seal=attach(plan,main[h],sealRef,{conn:true,bend:true,bound:top},r,'SEAL');if(seal)break;}
   if(!seal)continue;
   let elite=null;for(const h of shuffle([4,5,6,7,8,9],r)){elite=attach(plan,main[h],'S2-ELITE',{conn:true,bend:true,bound:top},r,'ELITE');if(elite)break;}
   if(!elite)continue;
   const climbs=plan.items.filter(i=>i.kind==='conn'&&i.open.includes('T')&&i.open.includes('B')).length;
   if(climbs>3)continue;
   const L=finish(plan,seed,attempt,'s2',main);L.chestFrom=loopFrom;return L;
  }
  return null;
 }
 // 주 경로 문→문 맨해튼 이동량(가로 h·세로 v). 통로 블록은 꺾이는 점을 지난다.
 function travel(plan,main){
  const U=(it,n)=>{const p=slotPos(it,n);return p.side==='L'||p.side==='R'?[p.x*16,p.y*12+2]:[p.x*16+8,p.y*12];};
  const find=k=>plan.items.find(i=>i.key===k),pts=[[main[0].bx*16+main[0].bw*8,main[0].by*12+2]];
  for(let i=1;i<main.length;i++){const a=main[i-1].key,b=main[i].key;
   const d=plan.links.find(l=>(l[0]===a&&l[2]===b)||(l[0]===b&&l[2]===a));
   if(d){const [x,nx]=d[0]===a?[d[0],d[1]]:[d[2],d[3]];pts.push(U(find(x),nx));continue;}
   const l1=plan.links.find(l=>(l[0]===a||l[2]===a)&&find(l[0]===a?l[2]:l[0]).kind==='conn'&&plan.links.some(m=>m!==l&&(m[0]===(l[0]===a?l[2]:l[0])||m[2]===(l[0]===a?l[2]:l[0]))&&(m[0]===b||m[2]===b)));
   const c=find(l1[0]===a?l1[2]:l1[0]),na=l1[0]===a?l1[1]:l1[3];
   const l2=plan.links.find(m=>m!==l1&&(m[0]===c.key||m[2]===c.key)&&(m[0]===b||m[2]===b)),nb=l2[0]===b?l2[1]:l2[3];
   pts.push(U(find(a),na),[c.bx*16+8,c.by*12+2],U(find(b),nb));}
  const last=main[main.length-1];pts.push([last.bx*16+last.bw*8,last.by*12+2]);
  let h=0,v=0;for(let i=1;i<pts.length;i++){h+=Math.abs(pts[i][0]-pts[i-1][0]);v+=Math.abs(pts[i][1]-pts[i-1][1]);}
  return {h,v};
 }
 function finish(plan,seed,attempt,stage,main){
  const L=plan.out();L.start='S1';L.seed=seed;L.attempt=attempt;L.stage=stage;
  L.mainKeys=main.map(m=>m.key);
  // 지표: 주 경로 블록 이동량(가로·세로), 특수방 위치
  const {h,v}=travel(plan,main);
  const its=plan.items;const x0=Math.min(...its.map(i=>i.bx)),x1=Math.max(...its.map(i=>i.bx+i.bw)),y0=Math.min(...its.map(i=>i.by)),y1=Math.max(...its.map(i=>i.by+i.bh));
  const rel=it=>({x:((it.bx+it.bw/2)-x0)/(x1-x0),y:((it.by+it.bh/2)-y0)/(y1-y0)});
  const hostOf=key=>{const l=plan.links.find(l=>l[2]===key||l[0]===key);return l?(l[0]===key?l[2]:l[0]):null;};
  const special={};for(const role of ['SHOP','ELITE','BOSS','SEAL','CHEST']){const it=its.find(i=>i.role===role);if(!it)continue;
   const mainIdx=main.indexOf(it);special[role]={rel:rel(it),mainIdx,host:hostOf(it.key)};}
  L.metrics={horizontal:h,vertical:v,climbs:its.filter(i=>i.kind==='conn'&&i.open.includes('T')&&i.open.includes('B')).length,connectors:its.filter(i=>i.kind==='conn').length,size:{w:(x1-x0)*16,h:(y1-y0)*12},special};
  return L;
 }
 const api={stage1,stage2,rng};
 if(typeof module!=='undefined')module.exports=api;else root.Generator=api;
})(typeof window!=='undefined'?window:globalThis);
