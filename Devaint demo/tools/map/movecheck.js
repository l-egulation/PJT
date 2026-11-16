// 보수적 이동 판정기 — 검토용. 실제 Unity 물리를 재현하지 않는다.
// 확인한 수치(data/combat/locomotion/player.json): 1단 점프 3.0u, 2단 6.0u, 달리기 7.5u/s,
// 벽 붙기 ↑ 7.5u/면 + 발돋움 1u, 몸 0.9375×2u. 대시는 필수 경로에서 쓰지 않는다고 가정한다.
// 판정: 위로 올라간 뒤(수직) 옆으로 이동하고(수평) 떨어지는 경로만 인정한다. 실제 포물선보다 보수적이다.
(function(root){
 'use strict';
 const RES=0.25;
 const DEFAULT={
  bodyW:1,          // 0.9375u → 0.25 격자에서 4칸
  bodyH:2,
  jumpRise:6,       // 2단 점프 최대 상승
  reachLow:6,       // 상승 3u 이하일 때 허용 수평 이동
  reachHigh:4.5,    // 상승 3u 초과일 때 허용 수평 이동(2단 정점 시간 0.6s × 7.5u/s)
  wallRise:11,      // 벽 붙기: 점프 3 + 등반 7.5 + 발돋움 1 → 11.5에서 여유 0.5
  wallClimb:false,
  // 엄격 모드: 점프 착지 높이 차가 2u 초과 4u 미만(1단 최고 3u 근처라 될 듯 말 듯함)이거나 5u 초과(2단 최고 6u 근처)면 인정하지 않는다
  strictRise:false
 };
 function rasterize(surfaces,pad){
  pad=pad||4;
  let x0=Infinity,y0=Infinity,x1=-Infinity,y1=-Infinity;
  for(const s of surfaces){x0=Math.min(x0,s.x);y0=Math.min(y0,s.y);x1=Math.max(x1,s.x+s.w);y1=Math.max(y1,s.y+s.h);}
  x0-=pad;y0-=pad;x1+=pad;y1+=pad;
  const W=Math.ceil((x1-x0)/RES),H=Math.ceil((y1-y0)/RES);
  const solid=new Uint8Array(W*H),plat=new Uint8Array(W*H);
  const ci=v=>Math.round((v-x0)/RES),cj=v=>Math.round((v-y0)/RES);
  for(const s of surfaces){
   const a=ci(s.x),b=ci(s.x+s.w);
   if(s.oneWay){const top=cj(s.y+s.h);for(let i=Math.max(0,a);i<Math.min(W,b);i++)plat[top*W+i]=1;continue;}
   const c=cj(s.y),d=cj(s.y+s.h);
   for(let j=Math.max(0,c);j<Math.min(H,d);j++)for(let i=Math.max(0,a);i<Math.min(W,b);i++)solid[j*W+i]=1;
  }
  // 바깥 여백은 고체로 막는다(방 밖으로 새는 경로 방지).
  return {W,H,x0,y0,solid,plat,toX:i=>x0+i*RES,toY:j=>y0+j*RES,ci,cj};
 }
 function analyze(surfaces,opts){
  opts=Object.assign({},DEFAULT,opts||{});
  const g=rasterize(surfaces,opts.pad);
  const {W,H,solid,plat}=g,B=Math.round(opts.bodyW/RES),T=Math.round(opts.bodyH/RES);
  const S=(i,j)=>i<0||j<0||i>=W||j>=H||solid[j*W+i]===1;
  const P=(i,j)=>i>=0&&j>=0&&i<W&&j<H&&plat[j*W+i]===1;
  // 몸 칸이 비었는지(i..i+B-1, j..j+T-1)·지지면 여부·낙하 결과를 한 번만 계산한다.
  const colClear=new Uint8Array(W*H);
  for(let i=0;i<W;i++){let run=0;for(let j=H-1;j>=0;j--){run=solid[j*W+i]?0:run+1;colClear[j*W+i]=run>=T?1:0;}}
  const clr=new Uint8Array(W*H),sup=new Uint8Array(W*H);
  for(let j=0;j<H;j++)for(let i=0;i+B<=W;i++){let ok=1,s=0;for(let c=0;c<B;c++){const k=j*W+i+c;if(!colClear[k]){ok=0;break;}if((j>0&&solid[k-W])||plat[k])s=1;}clr[j*W+i]=ok;sup[j*W+i]=ok&&s;}
  const clear=(i,j)=>i>=0&&j>=0&&i+B<=W&&j<H&&clr[j*W+i]===1;
  const support=(i,j)=>clear(i,j)&&sup[j*W+i]===1;
  const memo=new Int32Array(W*H).fill(-2);
  function land(i,j){// (i,j)에서 자유 낙하해 처음 닿는 행, 없으면 -1
   if(!clear(i,j))return -1;const k=j*W+i;if(memo[k]!==-2)return memo[k];
   let r=j,stack=[];while(r>=0&&clear(i,r)&&!sup[r*W+i]&&memo[r*W+i]===-2){stack.push(r);r--;}
   let res;if(r<0||!clear(i,r))res=-1;else if(sup[r*W+i])res=r;else res=memo[r*W+i];
   for(const s of stack)memo[s*W+i]=res;if(sup[k])memo[k]=j;return memo[k];
  }
  function fall(i,j,skip){
   if(!skip)return land(i,j);
   for(let c=0;c<B;c++)if(S(i+c,j-1))return -1; // 고체 위에서는 통과 불가
   let any=false;for(let c=0;c<B;c++)if(P(i+c,j))any=true;if(!any)return -1;
   return land(i,j-1);
  }
  const id=(i,j)=>j*W+i;
  const nodes=new Map();// key → {i,j,out:[]}
  const queue=[];
  // 서 있는 상태와 벽에 붙은 상태(cling)를 따로 둔다. 붙은 상태는 공중이므로 목표 판정에서 제외한다.
  function node(i,j,cling){const k=id(i,j)*2+(cling?1:0);let n=nodes.get(k);if(!n){n={i,j,cling:!!cling,out:[],set:new Set()};nodes.set(k,n);queue.push(n);}return n;}
  const RISE=Math.round(opts.jumpRise/RES),LOWR=Math.round(3/RES);
  const DLOW=Math.round(opts.reachLow/RES),DHIGH=Math.round(opts.reachHigh/RES);
  const HOP=Math.round(1/RES),CLIMB=Math.round(7.5/RES),KICK_RISE=Math.round(3/RES),KICK_REACH=Math.round(5/RES);
  function wallSide(i,j){// 몸 옆 벽: -1 왼쪽, 1 오른쪽, 0 없음. 벽 꼭대기가 발 아래 1u 이내면 발돋움으로 인정한다.
   for(let k=-HOP;k<T;k++){if(S(i-1,j+k))return -1;if(S(i+B,j+k))return 1;}return 0;
  }
  function expand(n){
   const {i,j}=n,add=(a,b,c)=>{if(b<0)return;const m=node(a,b,c);if(!n.set.has(m)){n.set.add(m);n.out.push(m);}};
   const sideMoves=(h,reach)=>{for(const d of [-1,1])for(let s=1;s<=reach;s++){const a=i+d*s;if(!clear(a,h))break;add(a,land(a,h));}};
   const SAFE1=Math.round(2/RES),SAFE2LO=Math.round(4/RES),SAFE2HI=Math.round(5/RES);
   const riseOk=r=>!opts.strictRise||r<0||r-j<=SAFE1||(r-j>=SAFE2LO&&r-j<=SAFE2HI);
   const jumpMoves=(h,reach)=>{for(const d of [-1,1])for(let s=1;s<=reach;s++){const a=i+d*s;if(!clear(a,h))break;const r=land(a,h);if(riseOk(r))add(a,r);}};
   if(n.cling){
    // 벽 붙기: ↑로 최대 7.5u + 발돋움, 그 높이에서 옆으로 이동하거나 반대쪽으로 벽차기(3u 상승, 5u 이내 반대 벽)
    const side=wallSide(i,j);
    for(let u=0;u<=CLIMB+HOP;u++){
     const h=j+u;if(!clear(i,h))break;
     if(u>0&&wallSide(i,h)!==side)break;
     sideMoves(h,DLOW);
     // 벽에서 공중 점프(벽차기는 공중 점프를 쓰지 않는다: CombatPlayer.WallMovement.cs) — 3u 더 올라 옆으로
     for(let r=1;r<=KICK_RISE&&clear(i,h+r);r++)if(r===KICK_RISE||!clear(i,h+r+1)){const hh=h+r;for(const d of [-1,1])for(let s=1;s<=DHIGH;s++){const a=i+d*s;if(!clear(a,hh))break;add(a,land(a,hh));}}
     let top=h;for(let r=1;r<=KICK_RISE&&clear(i,h+r);r++)top=h+r;
     for(let s=1;s<=KICK_REACH;s++){const a=i-side*s;if(!clear(a,top))break;add(a,land(a,top));
      {let t2=top;for(let r=1;r<=KICK_RISE&&clear(a,top+r);r++)t2=top+r;for(const d of [-1,1])for(let q=1;q<=DHIGH;q++){const e=a+d*q;if(!clear(e,t2))break;add(e,land(e,t2));}}
      if(!clear(a-side,top)&&wallSide(a,top)===-side)add(a,top,true);}
    }
    add(i,land(i,j));
    return;
   }
   // 걷기
   for(const d of [-1,1]){const a=i+d;if(!clear(a,j))continue;add(a,land(a,j));}
   // 내려가기(일방향 발판 통과)
   add(i,fall(i,j,true));
   // 점프: 위로 u칸 오른 뒤 좌우 이동, 낙하. 벽 붙기 허용 시 오르는 중 닿은 벽에 붙는다.
   for(let u=1;u<=RISE;u++){
    if(!clear(i,j+u))break;
    jumpMoves(j+u,u<=LOWR?DLOW:DHIGH);
    if(opts.wallClimb&&wallSide(i,j+u)!==0)add(i,j+u,true);
   }
   // 벽 쪽으로 점프해 붙기: 옆으로 이동한 지점에 벽이 있으면 붙는다.
   if(opts.wallClimb)for(let u=1;u<=LOWR;u++){if(!clear(i,j+u))break;for(const d of [-1,1])for(let s=1;s<=DLOW;s++){const a=i+d*s;if(!clear(a,j+u))break;if(!clear(a+d,j+u))add(a,j+u,true);}}
  }
  return {g,B,T,clear,support,fall,
   from(x,y){// (x,y)=발 중심 좌표
    const i=g.ci(x-opts.bodyW/2),j0=g.cj(y);let j=j0;
    // 시작점이 고체 안이면 위로 빈 곳을 찾는다(방 가운데 둔덕 등)
    for(let k=0;k<g.H-j0&&!clear(i,j0+k);k++)j=j0+k+1;
    const f=fall(i,j,false);return f<0?null:node(i,f);
   },
   run(start){
    if(!start)return;let h=0;while(h<queue.length)expand(queue[h++]);for(const n of nodes.values())n.set=null;
   },
   nodes,
   reach(start){// 정방향 도달 집합
    const seen=new Set([start]),q=[start];while(q.length){const n=q.pop();for(const m of n.out)if(!seen.has(m)){seen.add(m);q.push(m);}}return seen;
   },
   back(start){// start로 돌아올 수 있는 집합
    const rev=new Map();for(const n of nodes.values())for(const m of n.out){let a=rev.get(m);if(!a)rev.set(m,a=[]);a.push(n);}
    const seen=new Set([start]),q=[start];while(q.length){const n=q.pop();for(const m of rev.get(n)||[])if(!seen.has(m)){seen.add(m);q.push(m);}}return seen;
   },
   pos(n){return {x:g.toX(n.i)+opts.bodyW/2,y:g.toY(n.j)};},
   inBox(set,box){for(const n of set){if(n.cling)continue;const p=this.pos(n);if(p.x>=box.x&&p.x<=box.x+box.w&&p.y>=box.y&&p.y<=box.y+box.h)return true;}return false;}
  };
 }
 const api={RES,DEFAULT,rasterize,analyze};
 if(typeof module!=='undefined')module.exports=api;else root.MoveCheck=api;
})(typeof window!=='undefined'?window:globalThis);
