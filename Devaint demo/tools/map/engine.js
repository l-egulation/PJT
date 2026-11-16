// 공통 방·통로 연결 규격(제안) — 셀 격자 조립과 규칙 판정. 검토용, 게임과 연결하지 않는다.
// 좌표: 1칸 = 1u = 32px, y 위 방향. 방 발자국(footprint) 좌하단이 원점.
(function(root){
 'use strict';
 const SPEC={
  blockW:16, blockH:12,      // 블록: 방·통로 배치의 최소 단위
  frame:2,                   // 방 테두리(벽·천장·바닥) 두께
  doorH:6,                   // 좌우 출입구 높이(1스테이지 PassageHeadroom 6과 같음)
  doorW:6,                   // 상하 출입구 폭(수직 통로 안쪽 폭)
  sideApproach:4,            // 좌우 출입구 안쪽 접근 영역 폭
  topLedgeDrop:3,            // 위 출입구 접근 발판: 천장 아래 3칸
  keepSide:8,                // 좌우 출입구 안쪽 면에서 적 금지 거리(같은 높이 ±4)
  keepSideDy:4,
  keepVertical:4,            // 상하 출입구 발판 주변 적 금지 거리
  riseRec:4, riseCap:5,      // 디딤면 사이 상승: 권장 4, 상한 5(2단 점프 6.0에서 여유 1)
  gapRec:4, gapCap:6,        // 같은 높이 간격: 권장 4, 상한 6(대시 없이)
  platGapV:3,                // 겹치는 일방향 발판 사이 세로 간격 최소
 };
 // 종별 최소 바닥 폭(적이 서서 이동·공격할 수 있는 연속 바닥)
 const ENEMY={
  T:{name:'가시뿌리',s1:'05_boar → 01_thorn-root',s2:'M3 찌르기',run:4,h:1.3,w:1.0,melee:true},
  K:{name:'먹빗자루',s1:'07_scarecrow → 02_broom-stalker',s2:'K4 분사',run:5,h:2.6,w:0.85,melee:true},
  P:{name:'배수관 포수',s1:'10_water_bucket → 03_gutter-spitter',s2:'R 곡사',run:4,h:1.7,w:1.5,ranged:true},
  G:{name:'술통 포격수',s1:'draft_06_stationary_turret',s2:'H3 곡사·웅덩이',run:3,h:2.8,w:2.1,ranged:true,fixed:true},
  C:{name:'돌진꾼',s1:'draft_07_heavy_charger(12u 돌진)',s2:'B3(6u 돌진)',run:10,h:3.6,w:4.0,melee:true,runS2:8},
  W:{name:'우물 도르래 수문장',s1:'04_well-warden(엘리트)',run:8,h:2.6,w:1.5,melee:true,elite:true},
  A:{name:'A3 엘리트',s2:'Chapter2EliteEnemy',run:8,h:3,w:2.4,melee:true,elite:true},
 };
 const clone=o=>JSON.parse(JSON.stringify(o));
 // ── 슬롯 ──
 // id: L0,L1,R0,R1(블록 행) / T0,T1,B0,B1(블록 열)
 function slotGeom(room,id,fw,fh){
  const s=SPEC,side=id[0],n=+id.slice(1);
  if(side==='L'||side==='R'){const sill=n*s.blockH+s.frame;return {side,n,sill,x:side==='L'?0:fw,y0:sill,y1:sill+s.doorH};}
  const x0=n*s.blockW+(s.blockW-s.doorW)/2;return {side,n,x0,x1:x0+s.doorW,y:side==='B'?0:fh};
 }
 function mirrorId(id,bw){const side=id[0],n=+id.slice(1);if(side==='L')return 'R'+n;if(side==='R')return 'L'+n;return side+(bw-1-n);}
 // ── 방 조립 ──
 // room: {id,bw,bh,slots:[...],solids:[{x,y,w,h,need?}],plats:[{x,y,w,need?}],enemies:[{t,x,y}],frameSide?}
 // 템플릿 좌표는 방 안쪽(interior) 기준. need가 있으면 해당 슬롯 중 하나라도 활성일 때만 둔다.
 function buildRoom(room,active,mirror){
  const s=SPEC,fw=room.bw*s.blockW,fh=room.bh*s.blockH,fx=room.frameSide??s.frame,fy=s.frame;
  const W=fw-2*fx,H=fh-2*fy;
  // active는 원형(반전 전) 슬롯 이름. 반전하면 슬롯 이름도 함께 바꾼다.
  const act=new Set((active||room.slots).map(n=>mirror?mirrorId(n,room.bw):n));
  // 반전은 템플릿을 먼저 뒤집고 슬롯 이름을 바꾼다(슬롯은 블록 가운데라 반전해도 격자 위에 남는다).
  let tpl=clone(room);
  if(mirror){
   tpl.solids=tpl.solids.map(r=>({...r,x:W-r.x-r.w,need:r.need&&r.need.map(n=>mirrorId(n,room.bw))}));
   tpl.plats=tpl.plats.map(p=>({...p,x:W-p.x-p.w,need:p.need&&p.need.map(n=>mirrorId(n,room.bw))}));
   tpl.enemies=(tpl.enemies||[]).map(e=>({...e,x:W-e.x,flip:!e.flip}));
   tpl.slots=tpl.slots.map(n=>mirrorId(n,room.bw));
   if(tpl.marker)tpl.marker={...tpl.marker,x:W-tpl.marker.x};
   if(tpl.spawn)tpl.spawn={...tpl.spawn,x:W-tpl.spawn.x};
  }
  const on=n=>act.has(n);
  const keep=e=>!e.need||e.need.some(on);
  const grid=new Uint8Array(fw*fh).fill(1),own=new Uint8Array(fw*fh);// own: 1 템플릿 2 출입구 키트
  const set=(x,y,v,o)=>{if(x>=0&&y>=0&&x<fw&&y<fh){grid[y*fw+x]=v;if(o)own[y*fw+x]=o;}};
  for(let y=fy;y<fh-fy;y++)for(let x=fx;x<fw-fx;x++)set(x,y,0);
  const solids=[],plats=[],kit=[],notes=[];
  for(const r of tpl.solids.filter(keep)){for(let y=r.y;y<r.y+r.h;y++)for(let x=r.x;x<r.x+r.w;x++)set(x+fx,y+fy,1,1);solids.push({...r,x:r.x+fx,y:r.y+fy});}
  for(const p of tpl.plats.filter(keep))plats.push({x:p.x+fx,y:p.y+fy,w:p.w,role:p.role||(p.need?'door':'core'),need:p.need,vanishing:!!p.vanishing});
  const slots=[];
  for(const id of tpl.slots){
   const g=slotGeom(room,id,fw,fh);g.id=id;g.active=on(id);slots.push(g);
   if(!g.active)continue;
   if(g.side==='L'||g.side==='R'){
    const xs=g.side==='L'?[0,fx]:[fw-fx,fw];
    for(let y=g.y0;y<g.y1;y++)for(let x=xs[0];x<xs[1];x++)set(x,y,0,2);
    for(let y=g.y0;y<g.y1;y++)for(let x=0;x<s.sideApproach;x++)set(g.side==='L'?fx+x:fw-fx-1-x,y,0,2);// 접근 영역은 비운다
    if(g.n>0){// 위층 좌우 출입구: 벽에 붙은 2칸 두께 선반
     const lx=g.side==='L'?fx:fw-fx-s.sideApproach;
     for(let y=g.sill-2;y<g.sill;y++)for(let x=lx;x<lx+s.sideApproach;x++)set(x,y,1,2);
     kit.push({kind:'ledge',x:lx,y:g.sill-2,w:s.sideApproach,h:2,slot:id});
    }
   }else{
    const ys=g.side==='B'?[0,fy]:[fh-fy,fh];
    for(let y=ys[0];y<ys[1];y++)for(let x=g.x0;x<g.x1;x++)set(x,y,0,2);
    // 아래 출입구: 문 바닥(y=2)과 문턱 디딤(y=0). 아래 방 접근 발판(천장 3칸 아래)에서 디딤까지 5u, 디딤에서 문 바닥까지 2u.
    // 1단 점프 최고 높이가 정확히 3u라 3u 계단은 될 듯 말 듯 안 되므로 2u(1단)·4~5u(2단)만 쓴다(사용자 요청 2026-11-06).
    if(g.side==='B'){plats.push({x:g.x0,y:fy,w:s.doorW,role:'doorFloor',slot:id});plats.push({x:g.x0,y:0,w:s.doorW,role:'rung',slot:id});}
    else{
     plats.push({x:g.x0,y:fh-fy-s.topLedgeDrop,w:s.doorW,role:'topLedge',slot:id});
    }
   }
  }
  const enemies=(tpl.enemies||[]).map(e=>({...e,x:e.x+fx,y:e.y+fy}));
  // 기믹 표식(봉인 석판·잠긴 상자): 고체 또는 발판 위에 서고 머리 위 3칸이 비어야 한다
  const marker=tpl.marker?{x:tpl.marker.x+fx,y:tpl.marker.y+fy}:null;
  // 1챕터 시작방의 플레이어 시작 자리(방 안쪽 좌표, y는 선 바닥 높이)
  const spawn=tpl.spawn?{x:tpl.spawn.x+fx,y:tpl.spawn.y+fy}:null;
  return {room,marker,spawn,mirror:!!mirror,active:[...act].filter(n=>tpl.slots.includes(n)),fw,fh,fx,fy,W,H,grid,own,solids,plats,kit,slots,enemies,notes};
 }
 // 셀 격자 → 겹치지 않는 고체 사각형(행 단위 병합 후 세로 병합)
 function toRects(grid,w,h,ox,oy){
  const out=[],open=new Map();
  for(let y=0;y<h;y++){
   const runs=[];let x=0;
   while(x<w){if(!grid[y*w+x]){x++;continue;}let e=x;while(e<w&&grid[y*w+e])e++;runs.push([x,e]);x=e;}
   const next=new Map();
   for(const [a,b] of runs){const k=a+':'+b;let r=open.get(k);if(r){r.h++;}else{r={x:a+ox,y:y+oy,w:b-a,h:1,oneWay:false};out.push(r);}next.set(k,r);}
   open.clear();for(const [k,v] of next)open.set(k,v);
  }
  return out;
 }
 // 단일 방 검증용: 활성 출입구마다 이웃 쪽 최소 지형(통로·아래 방 접근 발판·위 방 바닥)을 붙인다.
 function withStubs(b){
  const s=SPEC,M=14,w=b.fw+2*M,h=b.fh+2*M,grid=new Uint8Array(w*h).fill(1);
  for(let y=0;y<b.fh;y++)for(let x=0;x<b.fw;x++)grid[(y+M)*w+x+M]=b.grid[y*b.fw+x];
  const carve=(x0,y0,x1,y1)=>{for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++)if(x>=0&&y>=0&&x<w&&y<h)grid[y*w+x]=0;};
  const plats=b.plats.map(p=>({...p,x:p.x+M,y:p.y+M}));const targets=[];
  for(const g of b.slots.filter(g=>g.active)){
   if(g.side==='L'||g.side==='R'){
    const x0=g.side==='L'?M-10:M+b.fw,x1=g.side==='L'?M:M+b.fw+10;carve(x0,g.y0+M,x1,g.y1+M);
    targets.push({id:g.id,x:(g.side==='L'?M-7:M+b.fw+7),y:g.sill+M,box:{x:g.side==='L'?M-10:M+b.fw+4,y:g.sill+M-.1,w:6,h:.6}});
   }else if(g.side==='B'){
    // 아래 방: 테두리 2칸 + 천장 밑 3칸 접근 발판, 테두리 안 디딤
    carve(g.x0+M,M-2,g.x1+M,M);carve(g.x0+M-6,M-11,g.x1+M+6,M-2);
    plats.push({x:g.x0+M,y:M-2-s.topLedgeDrop,w:s.doorW,role:'topLedge'});
    targets.push({id:g.id,box:{x:g.x0+M,y:M-5.1,w:s.doorW,h:.6}});
   }else{
    carve(g.x0+M,M+b.fh,g.x1+M,M+b.fh+2);carve(g.x0+M-8,M+b.fh+2,g.x1+M+8,M+b.fh+9);
    plats.push({x:g.x0+M,y:M+b.fh,w:s.doorW,role:'rung'});plats.push({x:g.x0+M,y:M+b.fh+2,w:s.doorW,role:'doorFloor'});
    targets.push({id:g.id,box:{x:g.x0+M-8,y:M+b.fh+1.9,w:6,h:.6}});
   }
  }
  const surfaces=toRects(grid,w,h,0,0).concat(plats.map(p=>({x:p.x,y:p.y-.5,w:p.w,h:.5,oneWay:true})));
  return {surfaces,targets,offset:M};
 }
 // ── 규칙 판정 ──
 function checkRoom(room,active,mirror,MoveCheck,stage){
  const b=buildRoom(room,active,mirror),issues=[],ok=[];
  const s=SPEC,{fw,fh,grid}=b;const S=(x,y)=>x<0||y<0||x>=fw||y>=fh||grid[y*fw+x]===1;
  // G1 고체 최소 두께 2(템플릿 사각형 단위)
  for(const r of b.solids)if(r.w<2||r.h<2)issues.push({rule:'G1',msg:`1칸 두께 고체 (${r.x},${r.y},${r.w}×${r.h})`});
  // G2 일방향 발판: 고체와 겹치지 않음, 끝은 벽에 붙이거나 1칸 이상 띄움, 위 여유 3칸
  for(const p of b.plats){
   for(let x=p.x;x<p.x+p.w;x++){if(S(x,p.y-1)&&p.role!=='rung'&&p.role!=='doorFloor'){issues.push({rule:'G2',msg:`발판이 고체에 묻힘 (${p.x},${p.y})`});break;}}
   let head=99;for(let x=p.x;x<p.x+p.w;x++){for(let y=p.y;y<fh;y++)if(S(x,y)){head=Math.min(head,y-p.y);break;}}
   if(head<s.platGapV&&p.role!=='rung')issues.push({rule:'G2',msg:`발판 위 여유 ${head}칸 (${p.x},${p.y})`});
  }
  for(let i=0;i<b.plats.length;i++)for(let j=i+1;j<b.plats.length;j++){const a=b.plats[i],c=b.plats[j];if(a.x<c.x+c.w&&c.x<a.x+a.w){const d=Math.abs(a.y-c.y);if(d===0)issues.push({rule:'G2',msg:`발판 겹침 (${a.x},${a.y})`});else if(d<s.platGapV&&a.role!=='rung'&&c.role!=='rung')issues.push({rule:'G2',msg:`발판 세로 간격 ${d} (${a.x},${a.y})/(${c.x},${c.y})`});}}
  // E 적 배치
  for(const e of b.enemies){
   const spec=ENEMY[e.t];const fx=Math.floor(e.x);
   const onSolid=S(fx,e.y-1),onPlat=b.plats.find(p=>p.y===e.y&&e.x>=p.x&&e.x<=p.x+p.w);
   if(!onSolid&&!onPlat){issues.push({rule:'E1',msg:`${e.t}@(${e.x},${e.y}) 발밑 지면 없음`});continue;}
   if(onPlat&&onPlat.role!=='core')issues.push({rule:'E1',msg:`${e.t}@(${e.x},${e.y}) 출입구 키트 발판 위`});
   let clearH=99;for(let y=e.y;y<fh;y++)if(S(fx,y)){clearH=y-e.y;break;}
   if(clearH<Math.ceil(spec.h))issues.push({rule:'E1',msg:`${e.t}@(${e.x},${e.y}) 머리 위 여유 ${clearH}`});
   // 연속 바닥 폭
   let run=0;if(onPlat)run=onPlat.w;else{let a=fx,c=fx;while(S(a-1,e.y-1)&&!S(a-1,e.y))a--;while(S(c+1,e.y-1)&&!S(c+1,e.y))c++;run=c-a+1;}
   const need=stage==='s2'&&spec.runS2?spec.runS2:spec.run;
   if(run<need)issues.push({rule:'E2',msg:`${e.t}@(${e.x},${e.y}) 바닥 폭 ${run} < ${need}`});
   // 출입구 금지 구역(활성 여부와 무관하게 선언된 모든 슬롯 기준 → 어떤 조합에서도 유지)
   for(const g of b.slots){
    if(g.side==='L'||g.side==='R'){const face=g.side==='L'?b.fx:fw-b.fx;if(Math.abs(e.x-face)<s.keepSide&&Math.abs(e.y-g.sill)<=s.keepSideDy)issues.push({rule:'E3',msg:`${e.t}@(${e.x},${e.y}) ${g.id} 출입구 ${Math.abs(e.x-face).toFixed(1)}u`});
     if(spec.ranged&&Math.abs(e.y-g.sill)<1.5&&Math.abs(e.x-face)<12)issues.push({rule:'E3',msg:`${e.t}@(${e.x},${e.y}) ${g.id} 같은 높이 원거리 12u 미만`});}
    else{const ly=g.side==='B'?b.fy:fh-b.fy-s.topLedgeDrop;if(e.x>g.x0-s.keepVertical&&e.x<g.x1+s.keepVertical&&Math.abs(e.y-ly)<=2)issues.push({rule:'E4',msg:`${e.t}@(${e.x},${e.y}) ${g.id} 상하 출입구 발판 근처`});}
   }
  }
  if(b.marker){const m=b.marker,mx=Math.floor(m.x);
   if(!S(mx,m.y-1)&&!b.plats.some(p=>p.y===m.y&&m.x>=p.x&&m.x<=p.x+p.w))issues.push({rule:'K1',msg:`표식(${m.x},${m.y}) 발밑 지면 없음`});
   for(let y=m.y;y<m.y+3;y++)if(S(mx,y)){issues.push({rule:'K1',msg:`표식(${m.x},${m.y}) 머리 위 여유 부족`});break;}}
  // P1 플레이어 시작 자리: 1챕터 시작방은 필수. 방 안쪽, 고체 바닥 또는 출입구 조건 없는 발판 위, 머리 위 2칸(몸 높이) 비움
  if(room.kind==='start'&&stage==='s1'&&!b.spawn)issues.push({rule:'P1',msg:'시작 자리(spawn) 없음'});
  if(b.spawn){const p=b.spawn,px=Math.floor(p.x);
   if(p.x<b.fx||p.x>fw-b.fx||p.y<b.fy||p.y>fh-b.fy)issues.push({rule:'P1',msg:`시작 자리(${p.x},${p.y}) 방 밖`});
   else{
    const plat=b.plats.find(q=>q.y===p.y&&p.x>=q.x&&p.x<=q.x+q.w);
    if(!S(px,p.y-1)&&!plat)issues.push({rule:'P1',msg:`시작 자리(${p.x},${p.y}) 발밑 지면 없음`});
    else if(!S(px,p.y-1)&&plat.role!=='core')issues.push({rule:'P1',msg:`시작 자리(${p.x},${p.y}) 출입구 조건 발판 위`});
    for(let y=p.y;y<p.y+2;y++)if(S(px,y)){issues.push({rule:'P1',msg:`시작 자리(${p.x},${p.y}) 머리 위 여유 부족`});break;}
   }}
  // M 이동: 활성 출입구 사이 양방향(점프만), 적 도달
  let move=null;
  if(MoveCheck){
   const st=withStubs(b),opt={pad:1,wallClimb:false,strictRise:true};
   const a=MoveCheck.analyze(st.surfaces,opt);
   const t0=st.targets[0];
   if(t0){
    const bx=t0.box,start=a.from(bx.x+bx.w/2,bx.y+.1);a.run(start);
    const fw2=a.reach(start),bw2=a.back(start);move={pairs:[],enemies:[]};
    for(const t of st.targets.slice(1)){const f=a.inBox(fw2,t.box),k=a.inBox(bw2,t.box);move.pairs.push({from:t0.id,to:t.id,go:f,back:k});if(!f||!k)issues.push({rule:'M1',msg:`${t0.id}↔${t.id} ${f?'':'가는 길 없음 '}${k?'':'돌아오는 길 없음'}`});}
    if(b.marker){const box={x:b.marker.x+st.offset-1.5,y:b.marker.y+st.offset-.3,w:3,h:.6};const f=a.inBox(fw2,box),k=a.inBox(bw2,box);
     if(!f||!k)issues.push({rule:'M3',msg:'표식 '+(f?'':'가는 길 없음 ')+(k?'':'돌아오는 길 없음')});}
    for(const e of b.enemies){const box={x:e.x+st.offset-1.5,y:e.y+st.offset-.3,w:3,h:.6};const f=a.inBox(fw2,box);move.enemies.push(f);if(!f)issues.push({rule:'M2',msg:`${e.t}@(${e.x},${e.y}) 도달 불가`});}
   }
   // M4 시작 자리에서 열린 모든 출입구로 나갈 수 있어야 한다
   if(b.spawn&&st.targets.length){
    const a2=MoveCheck.analyze(st.surfaces,opt),s0=a2.from(b.spawn.x+st.offset,b.spawn.y+st.offset+.1);a2.run(s0);const r0=s0?a2.reach(s0):new Set();
    for(const t of st.targets)if(!a2.inBox(r0,t.box))issues.push({rule:'M4',msg:`시작 자리에서 ${t.id} 출입구로 가는 길 없음`});
   }
  }
  return {b,issues,move};
 }
 function combos(slots){const out=[];for(let m=1;m<1<<slots.length;m++)out.push(slots.filter((_,i)=>m>>i&1));return out;}
 // ── 통로 블록: 16×12, 테두리 없음. 바닥 윗면 y=2(방 출입구 문턱과 같은 높이), 좌우 통로 높이 6 ──
 // bw: 가로 블록 수. 2 이상은 보스 앞 긴 가로 통로(좌우만 열림)
 function buildConnector(openings,bw=1){
  const s=SPEC,w=s.blockW*bw,h=s.blockH,grid=new Uint8Array(w*h).fill(1),plats=[];
  const carve=(x0,y0,x1,y1)=>{for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++)grid[y*w+x]=0;};
  const o=new Set(openings),sx0=(w-s.doorW)/2,sx1=sx0+s.doorW,floor=s.frame,top=floor+s.doorH;
  if(o.has('L'))carve(0,floor,o.has('R')?w:sx1,top);
  if(o.has('R'))carve(o.has('L')?0:sx0,floor,w,top);
  // 위·아래만 열린 블록은 벽타기 통로: 문 바닥(y=2)에서 착지 발판(y=10)까지 8u를 벽 한 면으로 오른다(점프 3u + ↑ 7.5u = 10.5u, 여유 2.5u). 착지 발판에서 위 방 바닥까지 4u는 2단 점프.
  const climb=o.size===2&&o.has('T')&&o.has('B');
  if(o.has('T')){carve(sx0,floor,sx1,h);if(climb)plats.push({x:sx0,y:floor+8,w:s.doorW,role:'landing'});else for(const y of [floor+2,floor+6])plats.push({x:sx0,y,w:s.doorW,role:'rung'});}// 디딤 y=4·8: 바닥 2→4(2u)→8(4u)→위 방 문턱 디딤 12(4u)→위 방 바닥 14(2u). 첫 디딤은 가로 통로 천장(8) 아래 4u라 몸 2u 위로 2u 여유가 있어 옆 대시가 벽 모서리에 걸리지 않는다
  if(o.has('B')){carve(sx0,0,sx1,top);plats.push({x:sx0,y:floor,w:s.doorW,role:'doorFloor'});plats.push({x:sx0,y:0,w:s.doorW,role:'rung'});}
  // 위 이웃 방 바닥(y=h+2)은 그 방의 아래 출입구 발판이 맡는다.
  return {grid,w,h,plats,openings:[...o],climb};
 }
 // ── 스테이지 조립: 블록 좌표에 방·통로 블록을 놓고 링크된 슬롯만 연다 ──
 // layout: {rooms:[{key,ref,bx,by,mirror}], connectors:[{key,open:[..],bx,by}], links:[[a,slotOrSide,b,slotOrSide]]}
 function assemble(layout,lookup){
  const s=SPEC,issues=[],placed=new Map();
  const act=new Map();for(const r of layout.rooms)act.set(r.key,[]);
  for(const [a,sa,b,sb] of layout.links){if(act.has(a))act.get(a).push(sa);if(act.has(b))act.get(b).push(sb);}
  let x0=Infinity,y0=Infinity,x1=-Infinity,y1=-Infinity;
  const items=[];
  for(const r of layout.rooms){
   const room=lookup(r.ref);if(!room){issues.push('원형 없음 '+r.ref);continue;}
   const declared=new Set(r.mirror?room.slots.map(n=>mirrorId(n,room.bw)):room.slots);
   for(const n of act.get(r.key))if(!declared.has(n))issues.push(`${r.key}(${r.ref}${r.mirror?' 반전':''})에 ${n} 슬롯 없음`);
   // buildRoom은 반전 전 이름으로 활성 목록을 받는다
   const pre=r.mirror?act.get(r.key).map(n=>mirrorId(n,room.bw)):act.get(r.key);
   const b=buildRoom(room,pre,r.mirror);
   items.push({kind:'room',key:r.key,ref:r.ref,room,bx:r.bx,by:r.by,bw:room.bw,bh:room.bh,b});
  }
  for(const c of layout.connectors||[]){const bw=c.bw||1;if(bw>1&&(c.open.length!==2||!c.open.includes('L')||!c.open.includes('R')))issues.push('긴 통로는 좌우만 열린다 '+c.key);const k=buildConnector(c.open,bw);items.push({kind:'conn',key:c.key,bx:c.bx,by:c.by,bw,bh:1,k,open:c.open});}
  // 겹침
  for(let i=0;i<items.length;i++)for(let j=i+1;j<items.length;j++){const a=items[i],c=items[j];if(a.bx<c.bx+c.bw&&c.bx<a.bx+a.bw&&a.by<c.by+c.bh&&c.by<a.by+a.bh)issues.push(`겹침 ${a.key}/${c.key}`);}
  for(const it of items){x0=Math.min(x0,it.bx);y0=Math.min(y0,it.by);x1=Math.max(x1,it.bx+it.bw);y1=Math.max(y1,it.by+it.bh);}
  const M=2,W=(x1-x0+2*M)*s.blockW,H=(y1-y0+2*M)*s.blockH,grid=new Uint8Array(W*H).fill(1),owner=new Int16Array(W*H).fill(-1),own=new Uint8Array(W*H);
  const plats=[],enemies=[],labels=[];
  const ox=it=>(it.bx-x0+M)*s.blockW,oy=it=>(it.by-y0+M)*s.blockH;
  items.forEach((it,idx)=>{
   const X=ox(it),Y=oy(it),g=it.kind==='room'?it.b.grid:it.k.grid,gw=it.kind==='room'?it.b.fw:it.k.w,gh=it.kind==='room'?it.b.fh:it.k.h;
   for(let y=0;y<gh;y++)for(let x=0;x<gw;x++){const k=(Y+y)*W+X+x;if(owner[k]!==-1)issues.push('셀 이중 소유 '+it.key);owner[k]=idx;grid[k]=g[y*gw+x];if(it.kind==='room')own[k]=it.b.own[y*gw+x];}
   for(const p of (it.kind==='room'?it.b.plats:it.k.plats))plats.push({...p,x:p.x+X,y:p.y+Y,owner:it.key});
   if(it.kind==='room'){for(const e of it.b.enemies)enemies.push({...e,x:e.x+X,y:e.y+Y,owner:it.key});labels.push({key:it.key,ref:it.ref,name:it.room.name,kind:it.room.kind||'combat',x:X,y:Y,w:gw,h:gh,mirror:it.b.mirror,active:it.b.active});}
   else labels.push({key:it.key,ref:(it.k.climb?'벽타기 ':'통로 ')+it.open.join(''),name:'',kind:'conn',climb:it.k.climb,x:X,y:Y,w:gw,h:gh});
  });
  // 링크 정렬: 두 쪽 개구부가 같은 행·열에서 맞닿는지
  const find=k=>items.find(i=>i.key===k);
  const edge=(it,n)=>{// 개구부의 월드 위치(블록 경계 위)
   const X=ox(it),Y=oy(it),side=n[0];
   if(it.kind==='conn'){const o=n;const sx=X+(s.blockW-s.doorW)/2;if(o==='L')return {side:'L',x:X,y:Y+s.frame};if(o==='R')return {side:'R',x:X+it.k.w,y:Y+s.frame};if(o==='T')return {side:'T',x:sx,y:Y+s.blockH};return {side:'B',x:sx,y:Y};}
   const g=slotGeom(it.room,n,it.b.fw,it.b.fh);
   if(side==='L'||side==='R')return {side,x:X+g.x,y:Y+g.sill};return {side,x:X+g.x0,y:Y+g.y};
  };
  const opp={L:'R',R:'L',T:'B',B:'T'};
  for(const [a,sa,b,sb] of layout.links){
   const A=find(a),B=find(b);if(!A||!B){issues.push('링크 대상 없음 '+a+'/'+b);continue;}
   const ea=edge(A,sa),eb=edge(B,sb);
   if(opp[ea.side]!==eb.side||ea.x!==eb.x||ea.y!==eb.y)issues.push(`정렬 불일치 ${a}.${sa}(${ea.x},${ea.y}) ↔ ${b}.${sb}(${eb.x},${eb.y})`);
  }
  return {grid,own,W,H,plats,enemies,labels,issues,items,origin:{x0,y0,M}};
 }
 function checkLayout(L,MoveCheck,startKey,opts){
  const surfaces=toRects(L.grid,L.W,L.H,0,0).concat(L.plats.map(p=>({x:p.x,y:p.y-.5,w:p.w,h:.5,oneWay:true})));
  const a=MoveCheck.analyze(surfaces,{pad:1,wallClimb:!!(opts&&opts.wallClimb),strictRise:true});
  const st=L.labels.find(l=>l.key===startKey);const start=a.from(st.x+st.w/2,st.y+SPEC.frame+.1);a.run(start);
  const fw=a.reach(start),bw=a.back(start),rooms=[];
  for(const l of L.labels){if(l.kind==='conn')continue;const box={x:l.x,y:l.y,w:l.w,h:l.h};rooms.push({key:l.key,ref:l.ref,go:a.inBox(fw,box),back:a.inBox(bw,box)});}
  const enemies=L.enemies.map(e=>a.inBox(fw,{x:e.x-1.5,y:e.y-.3,w:3,h:.6}));
  return {rooms,enemiesReached:enemies.filter(Boolean).length,enemies:enemies.length,nodes:a.nodes.size,a,fw,bw};
 }
 const api={SPEC,ENEMY,slotGeom,mirrorId,buildRoom,toRects,withStubs,checkRoom,combos,buildConnector,assemble,checkLayout};
 if(typeof module!=='undefined')module.exports=api;else root.RoomSpec=api;
})(typeof window!=='undefined'?window:globalThis);
