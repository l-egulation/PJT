(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.Stage3Core=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
'use strict';
const SCHEMA='deviant-stage3-layout',VERSION=1;
const TYPES={start:'시작방',combat:'전투방',maintenance:'정비소',elite:'엘리트방',boss:'보스방'};
const ZONES={garden:{name:'외곽 정원',color:'#8eae98'},guard:{name:'경비 구역',color:'#9daac5'},service:{name:'작업동 접근',color:'#c5a57d'}};
const OBJECTS={platform:'하향 통과 발판',solid:'고체 지형',enemy:'적 배치 후보',spawn:'시작 위치',heal:'잉크 충전소',station:'정비 시설',reward:'보상 후보',boss:'보스 위치',exit:'챕터 출구'};
const clone=v=>JSON.parse(JSON.stringify(v)),clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),snap=(v,s=1)=>Math.round(v/s)*s;
const copyName=name=>name.slice(0,97)+' 복사';
function seed(){
 let serial=0;const p=(x,y,w)=>({id:'p'+(++serial),kind:'platform',x,y,w,h:.5,name:'돌 디딤 발판',note:''});
 const s=(x,y,w,h)=>({id:'s'+(++serial),kind:'solid',x,y,w,h,name:'돌 단상',note:''});
 const m=(kind,x,y,name,note='')=>({id:'m'+(++serial),kind,x,y,w:kind==='enemy'?.9375:1.5,h:kind==='enemy'?2:2,name,note});
 const rooms=[
 {id:'S',name:'수로 출구',zone:'garden',type:'start',x:0,y:48,w:32,h:24,note:'낮은 수로 출구에서 성 외곽의 덮인 회랑으로 진입한다. 시작과 진행 방향을 먼저 확인하는 비전투 공간.',objects:[p(10,18,8),p(20,14,6),m('spawn',7,22,'플레이어 시작')]},
 {id:'F01',name:'덮인 정원 회랑',zone:'garden',type:'combat',x:32,y:48,w:48,h:24,note:'넓은 바닥에서 새 적을 소개한다. 식물과 아치는 배경에 두고 착지선을 선명하게 유지한다.',objects:[s(12,20,8,2),s(29,18,8,4),p(21,14,7),p(11,10,7),m('enemy',11,22,'근접 경비 후보'),m('enemy',24,22,'방패 경비 후보'),m('enemy',34,18,'원거리 경비 후보'),m('enemy',27,22,'근접 경비 후보')]},
 {id:'F02',name:'외곽 경비실',zone:'garden',type:'combat',x:80,y:48,w:48,h:24,note:'가로 전투면을 중심으로 낮은 우회 발판을 배치한다. 위층으로 향하는 경비 회랑이 다음 방에 보인다.',objects:[p(7,18,8),p(19,14,8),p(31,10,8),p(19,6,7),m('enemy',13,22,'근접 경비 후보'),m('enemy',24,14,'원거리 경비 후보'),m('enemy',30,22,'방패 경비 후보'),m('enemy',35,22,'근접 경비 후보')]},
 {id:'F03',name:'성벽 계단실',zone:'guard',type:'combat',x:128,y:36,w:32,h:36,note:'왼쪽 아래로 들어와 오른쪽 중층으로 나간다. 4u 간격의 디딤으로 올라가며 전투한다.',objects:[p(8,30,9),p(18,26,9),p(9,22,10),p(4,18,9),p(14,14,9),p(20,10,8),p(10,6,9),m('enemy',13,34,'방패 경비 후보'),m('enemy',22,26,'근접 경비 후보'),m('enemy',13,22,'원거리 경비 후보'),m('enemy',18,14,'근접 경비 후보')]},
 {id:'M',name:'경비 휴게실',zone:'guard',type:'maintenance',x:160,y:36,w:32,h:24,note:'전투방 세 곳 뒤의 주 경로 정비소. 위쪽 경비탑 갈래의 출발점이기도 하다. 일반 적은 배치하지 않는다.',objects:[p(7,18,8),p(17,14,8),p(9,10,8),m('station',13,22,'정비 시설'),m('station',21,22,'잉크 판매대')]},
 {id:'F04',name:'복층 경비 회랑',zone:'guard',type:'combat',x:192,y:24,w:48,h:36,note:'왼쪽 아래에서 오른쪽 위로 전투층을 순서대로 연결한다. 정비소에서 경비탑을 거친 갈래도 위쪽에서 합류한다.',objects:[p(7,30,8),p(20,26,8),p(31,22,8),p(20,18,8),p(9,14,8),p(21,10,8),p(34,10,7),p(5,10,7),m('enemy',14,34,'방패 경비 후보'),m('enemy',24,26,'근접 경비 후보'),m('enemy',35,22,'원거리 경비 후보'),m('enemy',24,18,'근접 경비 후보'),m('enemy',25,10,'방패 경비 후보')]},
 {id:'F05',name:'안쪽 운반 계단',zone:'service',type:'combat',x:240,y:24,w:32,h:36,note:'위층 경비 회랑에서 작업동의 낮은 운반 길로 내려간다. 문서 상자와 운반 표식이 등장한다.',objects:[p(9,10,8),p(19,14,8),p(10,18,8),p(3,22,8),p(14,26,8),p(14,30,8),m('enemy',13,10,'근접 경비 후보'),m('enemy',23,14,'원거리 경비 후보'),m('enemy',18,26,'방패 경비 후보'),m('enemy',19,34,'근접 경비 후보')]},
 {id:'F06',name:'기록 운반 통로',zone:'service',type:'combat',x:272,y:48,w:48,h:24,note:'왼쪽 위 입구에서 낮은 운반 통로로 내려온다. 보스 앞의 넓은 전투면과 회복 시설 자리를 확보한다.',objects:[p(9,10,8),p(20,14,8),p(31,18,8),m('enemy',13,10,'근접 경비 후보'),m('enemy',24,14,'원거리 경비 후보'),m('enemy',26,22,'방패 경비 후보'),m('enemy',31,22,'근접 경비 후보'),m('heal',38,22,'잉크 충전소','시험값: 100G / 150G, 최대 체력 25%, 챕터당 2회. 위치 후보를 편집한다.')]},
 {id:'B',name:'기록 반입장 관문',zone:'service',type:'boss',x:320,y:48,w:32,h:24,note:'성의 작업동으로 진입하는 관문. 안쪽 폭 30u의 평평한 보스 무대. 출구 뒤는 4스테이지 기록 반입실로 이어진다.',objects:[m('boss',19,22,'보스 배치 후보','외형과 전투 패턴은 미정'),m('reward',24,22,'보스 잉크 제단'),m('exit',28,22,'정제시설로','보스 처치 뒤 활성화하는 출구 후보')]},
 {id:'F07',name:'위층 경비탑',zone:'guard',type:'combat',x:160,y:0,w:48,h:24,note:'정비소 위에서 갈라져 복층 경비 회랑 위쪽으로 합류하는 선택 경로. 오른쪽 엘리트방은 막다른 도전 공간.',objects:[p(12,18,8),p(17,14,8),p(30,10,8),p(39,6,6),m('enemy',17,22,'방패 경비 후보'),m('enemy',21,14,'원거리 경비 후보'),m('enemy',34,10,'근접 경비 후보'),m('reward',31,22,'탐험 보상 후보')]},
 {id:'E',name:'경비대장 대기실',zone:'guard',type:'elite',x:208,y:0,w:32,h:24,note:'선택 경비탑 끝의 엘리트방. 챕터 전체 추격은 이 초안에 포함하지 않는다.',objects:[p(11,16,9),p(20,12,7),m('enemy',19,22,'엘리트 배치 후보'),m('reward',25,22,'엘리트 보상 후보')]},
 {id:'F08',name:'작업동 보관실',zone:'service',type:'combat',x:240,y:72,w:48,h:24,note:'운반 계단에서 아래로 갈라져 기록 운반 통로로 올라오는 선택 순환 경로. 기록 상자와 낮은 작업 발판을 배치한다.',objects:[p(6,18,8),p(17,14,8),p(29,10,8),p(5,10,8),m('enemy',13,22,'근접 경비 후보'),m('enemy',22,14,'원거리 경비 후보'),m('enemy',30,22,'방패 경비 후보'),m('reward',38,22,'보관실 보상 후보')]}
 ];
 const c=(id,a,sa,ia,b,sb,ib,kind='main')=>({id,from:{room:a,side:sa,slot:ia},to:{room:b,side:sb,slot:ib},kind});
 return {schema:SCHEMA,version:VERSION,title:'3챕터 · 성채 진입과 작업동 접근',note:'설계 초안. 일반 전투방 8 + 특수방 4. 적과 보상 표시는 위치 후보이며 전투 규칙은 미확정.',units:{pixelsPerUnit:32,blockWidth:16,blockHeight:12,yAxis:'down'},movement:{speed:7.5,firstJump:20,secondJump:20,riseGravity:66.6666667,fallGravity:104.1666667,bodyWidth:.9375,bodyHeight:2},rooms,connections:[c('C01','S','R',1,'F01','L',1),c('C02','F01','R',1,'F02','L',1),c('C03','F02','R',1,'F03','L',2),c('C04','F03','R',1,'M','L',1),c('C05','M','R',1,'F04','L',2),c('C06','F04','R',0,'F05','L',0),c('C07','F05','R',2,'F06','L',0),c('C08','F06','R',1,'B','L',1),c('C09','M','T',0,'F07','B',0,'branch'),c('C10','F07','B',2,'F04','T',0,'branch'),c('C11','F07','R',1,'E','L',1,'elite'),c('C12','F05','B',0,'F08','T',0,'branch'),c('C13','F08','T',2,'F06','B',0,'branch')]};
}
function roomById(map,id){return map.rooms.find(r=>r.id===id);}
function maxSlot(r,side){return ((side==='L'||side==='R')?r.h/12:r.w/16)-1;}
function port(map,end){const r=roomById(map,end.room);if(!r)return null;const v=end.side==='L'||end.side==='R';return {x:r.x+(v?(end.side==='L'?0:r.w):end.slot*16+8),y:r.y+(v?end.slot*12+7:(end.side==='T'?0:r.h)),floor:r.y+end.slot*12+10,room:r};}
function connectionGeometry(map,c){
 const a=port(map,c.from),b=port(map,c.to);if(!a||!b)return {ok:false,reason:'연결할 방이 없습니다.'};
 if(c.from.room===c.to.room)return {ok:false,a,b,reason:'같은 방끼리 연결되어 있습니다.'};
 if(c.from.slot>maxSlot(a.room,c.from.side)||c.to.slot>maxSlot(b.room,c.to.side))return {ok:false,a,b,reason:'출입구 슬롯이 방 범위를 벗어났습니다.'};
 let low,high,horizontal=false;
 if(c.from.side==='R'&&c.to.side==='L'){low=a;high=b;horizontal=true;}else if(c.from.side==='L'&&c.to.side==='R'){low=b;high=a;horizontal=true;}
 else if(c.from.side==='B'&&c.to.side==='T'){low=a;high=b;}else if(c.from.side==='T'&&c.to.side==='B'){low=b;high=a;}else return {ok:false,a,b,reason:'출입구 방향이 마주보지 않습니다.'};
 const gap=horizontal?high.x-low.x:high.y-low.y,aligned=horizontal?low.y===high.y:low.x===high.x,allowed=horizontal?16:12;
 if(!aligned||!(gap===0||gap===allowed))return {ok:false,a,b,reason:!aligned?'출입구 높이·열이 어긋났습니다.':'직결 또는 통로 1블록으로 맞춰 주세요.'};
 return {ok:true,a,b,horizontal,gap,rect:gap?horizontal?{x:low.x,y:low.y-7,w:16,h:12}:{x:low.x-8,y:low.y,w:16,h:12}:null};
}
function portsFor(map,r){const result=[];for(const c of map.connections){if(!connectionGeometry(map,c).ok)continue;for(const e of [c.from,c.to])if(e.room===r.id)result.push(e);}return result;}
function strips(start,end,cuts,make){let at=start;const out=[];for(const [a,b] of cuts.sort((a,b)=>a[0]-b[0])){if(a>at)out.push(make(at,a-at));at=Math.max(at,b);}if(at<end)out.push(make(at,end-at));return out;}
function fixtures(map,r){
 const ports=portsFor(map,r),wall=r.type==='boss'?1:3,solids=[],platforms=[];
 const cuts=side=>ports.filter(e=>e.side===side).map(e=>side==='L'||side==='R'?[e.slot*12+4,e.slot*12+10]:[e.slot*16+5,e.slot*16+11]);
 solids.push(...strips(0,r.w,cuts('T'),(x,w)=>({x,y:0,w,h:2})),...strips(0,r.w,cuts('B'),(x,w)=>({x,y:r.h-2,w,h:2})),...strips(2,r.h-2,cuts('L'),(y,h)=>({x:0,y,w:wall,h})),...strips(2,r.h-2,cuts('R'),(y,h)=>({x:r.w-wall,y,w:wall,h})));
 for(const e of ports){if(e.side==='B'){platforms.push({x:e.slot*16+5,y:r.h-2,w:6,h:.5,fixture:true},{x:e.slot*16+5,y:r.h,w:6,h:.5,fixture:true});}else if(e.side==='T'){platforms.push({x:e.slot*16+5,y:5,w:6,h:.5,fixture:true});}else {const y=e.slot*12+10;if(y<r.h-2)platforms.push({x:e.side==='L'?wall:r.w-wall-4,y,w:4,h:.5,fixture:true});}}
 return {solids,platforms};
}
function collisionGeometry(map){
 const solids=[],platforms=[];for(const r of map.rooms){const f=fixtures(map,r);for(const v of [...f.solids,...r.objects.filter(o=>o.kind==='solid')])solids.push({...v,x:v.x+r.x,y:v.y+r.y,room:r.id});for(const v of [...f.platforms,...r.objects.filter(o=>o.kind==='platform')])platforms.push({...v,x:v.x+r.x,y:v.y+r.y,room:r.id});}
 for(const c of map.connections){const g=connectionGeometry(map,c);if(!g.ok||!g.rect)continue;const a=g.rect;if(g.horizontal){solids.push({x:a.x,y:a.y,w:a.w,h:4},{x:a.x,y:a.y+10,w:a.w,h:2});}else{solids.push({x:a.x,y:a.y,w:5,h:12},{x:a.x+11,y:a.y,w:5,h:12});for(const y of [0,4,8,12])platforms.push({x:a.x+5,y:a.y+y,w:6,h:.5,fixture:true});}}
 const seen=new Set();return {solids,platforms:platforms.filter(b=>{const key=[b.x,b.y,b.w,b.h].join(',');if(seen.has(key))return false;seen.add(key);return true;})};
}
const overlap=(a,b)=>a.x<b.x+b.w-.001&&a.x+a.w>b.x+.001&&a.y<b.y+b.h-.001&&a.y+a.h>b.y+.001;
function validate(map){
 const issues=[],add=(room,text,level='warn',object)=>issues.push({room,text,level,object});
 for(let i=0;i<map.rooms.length;i++){const r=map.rooms[i],wall=r.type==='boss'?1:3;
  if(r.x%16||r.y%12||r.w%16||r.h%12)add(r.id,'방 위치·크기는 16×12u 블록에 맞춰야 합니다.');
  for(const b of map.rooms.slice(i+1))if(overlap(r,b))add(r.id,b.id+' 방과 겹칩니다.','error');
  const terrain=r.objects.filter(o=>o.kind==='platform'||o.kind==='solid'),fixed=fixtures(map,r);
  for(const o of r.objects){if(o.x<wall||o.x+o.w>r.w-wall||o.y<2||o.y+(terrain.includes(o)?o.h:0)>r.h-2+.001)add(r.id,o.name+' 배치가 방 내부를 벗어납니다.','warn',o.id);
   if(o.kind==='solid'&&(o.w<2||o.h<2))add(r.id,'고체 지형은 두께 2u 이상이 필요합니다.','warn',o.id);
   if(terrain.includes(o)){for(const e of portsFor(map,r)){let area;if(e.side==='L'||e.side==='R')area={x:e.side==='L'?wall:r.w-wall-4,y:e.slot*12+4,w:4,h:6};else area={x:e.slot*16+5,y:e.side==='T'?2:r.h-8,w:6,h:6};if(overlap(o,area))add(r.id,o.name+' 배치가 출입구 접근 공간에 겹칩니다.','warn',o.id);}
    for(const b of terrain)if(b.id>o.id&&overlap(o,b))add(r.id,'발판·고체 지형이 서로 겹칩니다.','warn',o.id);
    if(o.kind==='platform')for(const b of terrain)if(b.id>o.id&&b.kind==='platform'&&o.x<b.x+b.w&&o.x+o.w>b.x&&Math.abs(o.y-b.y)<3)add(r.id,'겹쳐 놓인 발판의 높이 간격이 3u 미만입니다.','warn',o.id);
   }
   if(['enemy','boss','heal','station','spawn','exit','reward'].includes(o.kind)){
    const surfaces=[...fixed.solids,...fixed.platforms,...terrain];if(!surfaces.some(v=>Math.abs(v.y-o.y)<.01&&o.x>=v.x-.01&&o.x+o.w<=v.x+v.w+.01))add(r.id,o.name+'의 발 아래 지지면을 확인하세요.','warn',o.id);
    if([...fixed.solids,...terrain.filter(t=>t.kind==='solid')].some(b=>overlap({x:o.x,y:o.y-o.h,w:o.w,h:o.h},b)))add(r.id,o.name+' 배치가 고체 지형에 묻힙니다.','warn',o.id);
    if(o.kind==='enemy'){for(const e of portsFor(map,r)){const x=e.side==='L'?0:e.side==='R'?r.w:e.slot*16+8,y=(e.side==='L'||e.side==='R')?e.slot*12+10:e.side==='T'?5:r.h-2;if(Math.abs(o.x+o.w/2-x)<(e.side==='L'||e.side==='R'?11:4)&&Math.abs(o.y-y)<4)add(r.id,o.name+' 배치가 출입구에 가깝습니다.','warn',o.id);}}
   }
  }
 }
 const used=new Set();for(const c of map.connections){const g=connectionGeometry(map,c);if(!g.ok)add(c.from.room,c.id+': '+g.reason,'error');for(const e of [c.from,c.to]){const k=e.room+':'+e.side+e.slot;if(used.has(k))add(e.room,'출입구 '+e.side+e.slot+'에 연결이 중복됩니다.','error');used.add(k);}if(g.ok&&g.rect)for(const r of map.rooms)if(overlap(g.rect,r))add(r.id,c.id+' 통로와 방이 겹칩니다.','error');}
 const start=map.rooms.find(r=>r.type==='start');if(!start)add(null,'시작방이 없습니다.','error');if(!map.rooms.some(r=>r.type==='boss'))add(null,'보스방이 없습니다.','error');
 if(start){const seen=new Set([start.id]);let changed=true;while(changed){changed=false;for(const c of map.connections)if(connectionGeometry(map,c).ok){if(seen.has(c.from.room)&&!seen.has(c.to.room)){seen.add(c.to.room);changed=true;}if(seen.has(c.to.room)&&!seen.has(c.from.room)){seen.add(c.from.room);changed=true;}}}for(const r of map.rooms)if(!seen.has(r.id))add(r.id,'시작방과 유효한 연결이 이어지지 않습니다.','error');}
 return issues;
}
function parse(input){
 const d=typeof input==='string'?JSON.parse(input):clone(input);if(!d||d.schema!==SCHEMA||d.version!==VERSION)throw Error('이 편집기의 버전 1 맵 JSON을 선택해 주세요.');
 if(!Array.isArray(d.rooms)||d.rooms.length<1||d.rooms.length>150||!Array.isArray(d.connections)||d.connections.length>500)throw Error('방 또는 연결 개수가 허용 범위를 벗어납니다.');
 const ids=new Set(),objectIds=new Set();let count=0;const str=(v,max)=>typeof v==='string'&&v.length<=max;const num=(v,min,max)=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
 for(const r of d.rooms){if(!str(r.id,60)||!r.id||ids.has(r.id)||!str(r.name,100)||!TYPES[r.type]||!ZONES[r.zone]||!num(r.x,-5000,5000)||!num(r.y,-5000,5000)||r.x%16||r.y%12||!num(r.w,32,160)||!num(r.h,12,120)||r.w%16||r.h%12||!Array.isArray(r.objects))throw Error('방 ID·종류·격자 좌표가 올바르지 않습니다.');ids.add(r.id);if(r.note!==undefined&&!str(r.note,5000))throw Error('방 메모가 너무 깁니다.');
  for(const o of r.objects){if(!str(o.id,80)||!o.id||objectIds.has(o.id)||!OBJECTS[o.kind]||!str(o.name,100)||!num(o.x,-160,320)||!num(o.y,-120,240)||!num(o.w,.125,160)||!num(o.h,.125,120)||o.note!==undefined&&!str(o.note,5000))throw Error('발판·배치 데이터가 올바르지 않습니다.');objectIds.add(o.id);if(++count>10000)throw Error('배치 개수가 너무 많습니다.');}
 }
 const cids=new Set();for(const c of d.connections){if(!str(c.id,80)||!c.id||cids.has(c.id)||!['main','branch','elite'].includes(c.kind))throw Error('연결 데이터가 올바르지 않습니다.');cids.add(c.id);for(const e of [c.from,c.to])if(!e||!ids.has(e.room)||!['L','R','T','B'].includes(e.side)||!Number.isInteger(e.slot)||e.slot<0||e.slot>12)throw Error('연결 출입구를 확인하세요.');}
 if(!str(d.title,200)||d.note!==undefined&&!str(d.note,5000))throw Error('맵 제목·메모를 확인하세요.');
 const movement=d.movement||seed().movement;for(const [k,a,b] of [['speed',1,20],['firstJump',1,50],['secondJump',1,50],['riseGravity',10,300],['fallGravity',10,300],['bodyWidth',.25,3],['bodyHeight',.5,4]])if(!num(movement[k],a,b))throw Error('이동 미리보기 수치가 올바르지 않습니다.');
 return {schema:SCHEMA,version:VERSION,title:d.title,note:d.note||'',units:{pixelsPerUnit:32,blockWidth:16,blockHeight:12,yAxis:'down'},movement:clone(movement),rooms:d.rooms.map(r=>({...r,note:r.note||'',objects:r.objects.map(o=>({...o,note:o.note||''}))})),connections:d.connections};
}
function bounds(map){return {x:Math.min(...map.rooms.map(r=>r.x)),y:Math.min(...map.rooms.map(r=>r.y)),right:Math.max(...map.rooms.map(r=>r.x+r.w)),bottom:Math.max(...map.rooms.map(r=>r.y+r.h))};}
function autoConnect(map,ra,rb){
 const free=(r,side,slot)=>!map.connections.some(c=>[c.from,c.to].some(e=>e.room===r.id&&e.side===side&&e.slot===slot));let best=null;
 for(const [sa,sb] of [['R','L'],['L','R'],['T','B'],['B','T']])for(let ia=0;ia<=maxSlot(ra,sa);ia++)for(let ib=0;ib<=maxSlot(rb,sb);ib++){if(!free(ra,sa,ia)||!free(rb,sb,ib))continue;const c={id:'c-'+Date.now().toString(36),from:{room:ra.id,side:sa,slot:ia},to:{room:rb.id,side:sb,slot:ib},kind:'branch'},g=connectionGeometry(map,c),a=port(map,c.from),b=port(map,c.to),score=(g.ok?0:10000)+Math.hypot(a.x-b.x,a.y-b.y);if(!best||score<best.score)best={c,score};}
 return best?.c;
}
function makeRoom(map){const b=bounds(map),used=new Set(map.rooms.map(r=>r.id));let n=9;while(used.has('F'+String(n).padStart(2,'0')))n++;return {id:'F'+String(n).padStart(2,'0'),name:'새 전투방',type:'combat',zone:'service',x:snap(b.right+16,16),y:snap(b.bottom-24,12),w:48,h:24,note:'방을 배치한 다음 연결 도구로 출입구를 연결하세요.',objects:[]};}
class History{constructor(limit=80){this.past=[];this.future=[];this.limit=limit;}push(before,after){if(JSON.stringify(before)===JSON.stringify(after))return false;this.past.push(clone(before));if(this.past.length>this.limit)this.past.shift();this.future=[];return true;}undo(now){if(!this.past.length)return null;this.future.push(clone(now));return this.past.pop();}redo(now){if(!this.future.length)return null;this.past.push(clone(now));return this.future.pop();}}
return {SCHEMA,VERSION,TYPES,ZONES,OBJECTS,clone,clamp,snap,copyName,seed,parse,roomById,maxSlot,port,portsFor,fixtures,collisionGeometry,connectionGeometry,validate,bounds,overlap,autoConnect,makeRoom,History};
});
