// Authored forest relief. Coordinates and profiles are edited in data/relief-layout.json.
'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),box={window:{},console};vm.createContext(box);
for(const p of ['data/map.js','data/terrain-rules.js','editor/core.js','editor/terrain.js'])vm.runInContext(fs.readFileSync(path.join(root,p),'utf8'),box);
const data=box.window.EXPANDED_MAP,rules=box.window.TERRAIN_RULES;
const cfg=JSON.parse(fs.readFileSync(path.join(root,'data/relief-layout.json'))),W=cfg.cols,H=cfg.rows;
const grid=new Uint8Array(W*H).fill(1),markers=[],cliffs=[];
const solid=(x,y)=>x<0||y<0||x>=W||y>=H?1:grid[y*W+x];
const carve=(x,y,w,h)=>{for(let b=Math.max(2,Math.floor(y));b<Math.min(H-2,y+h);b++)for(let a=Math.max(2,Math.floor(x));a<Math.min(W-2,x+w);a++)grid[b*W+a]=0;};
function sample(points,x){for(let i=1;i<points.length;i++){const [a,u]=points[i-1],[b,v]=points[i];if(x<b)return u+(v-u)*(x-a)/(b-a);}return points.at(-1)[1];}
function add(kind,name,x,y,w,note='',extra={}){
 x=Math.round(x);y=Math.round(y);w=Math.round(w);
 if(x<2||y<2||x+w>W-2||y>H-3)return false;
 if(['platform','dropfloor'].includes(kind)){
  for(let a=x;a<x+w;a++)for(let b=y-3;b<=y;b++)if(solid(a,b))return false;
  if(markers.some(m=>['platform','dropfloor'].includes(m.kind)&&Math.abs(m.y/32-y)<2&&x<m.x/32+m.w/32&&x+w>m.x/32))return false;
 }
 markers.push({id:'relief-'+String(markers.length+1).padStart(3,'0'),kind,name,x:x*32,y:y*32,w:w*32,h:32,note,...(kind==='dropfloor'?{dropKey:'ArrowDown+KeyS'}:{}),...extra});return true;
}
const zones=structuredClone(cfg.zones);
for(const z of zones){
 const floor=z.floorProfile.map(([x,y])=>[Math.round(x*z.w),z.y+z.h+y]);
 const roof=z.roofProfile.map((v,i)=>[i*z.w/4,Math.max(3,z.y+v)]);
 z.shape={floor:floor.map(([x,y])=>[x+z.x,y]),ceiling:roof.map(([x,y])=>[x+z.x,y])};
 for(let u=0;u<z.w;u++){
  const bevel=Math.max(0,4-Math.min(u,z.w-1-u)),top=Math.round(sample(roof,u))+bevel;
  const bottom=Math.min(H-3,Math.round(sample(floor,u)));
  carve(z.x+u,top,1,bottom-top);
 }
 for(let i=1;i<floor.length;i++)if(floor[i][0]===floor[i-1][0]&&Math.abs(floor[i][1]-floor[i-1][1])>5)
  cliffs.push({zone:z.number,x:z.x+floor[i][0],upper:Math.min(floor[i][1],floor[i-1][1]),lower:Math.max(floor[i][1],floor[i-1][1]),side:floor[i][1]>floor[i-1][1]?1:-1});
}
// Preserve connectivity while sculpting the actual walking surface. Level links
// contain broad raised earth masses and hollows; steep links use broken terraces.
const shafts=[];
for(const c of cfg.connections){
 if(c.type==='walk'){
  for(let i=1;i<c.points.length;i++){
   const [ax,ay]=c.points[i-1],[bx,by]=c.points[i],span=bx-ax;
   const steep=Math.abs(by-ay)>12,profile=cfg.corridorRelief.routes[c.a+'-'+c.b]||cfg.corridorRelief[c.a%2?'flat':'valley'];
   const heights=[];for(let x=ax-3;x<=bx+3;x++){
    const t=Math.max(0,Math.min(1,(x-ax)/span));
    const y=steep?Math.round(ay+(by-ay)*cfg.corridorRelief.steepFractions[Math.min(5,Math.floor(t*6))]):Math.round(ay+(by-ay)*t+sample(profile,t));
    heights.push([x,y]);
   }
   for(let j=0;j<heights.length;j++){
    const [x,y]=heights[j],near=heights.slice(Math.max(0,j-7),j+8).map(v=>v[1]);
    const top=Math.min(...near)-cfg.corridorRelief.minimumHeadroom-(c.a%3);
    carve(x,top,1,y-top);
   }
  }
 }else{
  const [a,b]=c.points,top=Math.min(a[1],b[1]),bottom=Math.max(a[1],b[1]);
  const width=c.type==='wallkick'?4:(c.a%3===0?16:14),x=Math.round(a[0]-width/2);
  carve(x,top-7,width,bottom-top+7);shafts.push({x,width,top,bottom,c});
  // A side alcove breaks each long shaft silhouette without closing the route.
  if(c.type!=='wallkick')for(let y=top+10,k=0;y<bottom-4;y+=14,k++)carve(x+(k%2?width-3:-4),y-5,7,6);
 }
}
// Existing special-purpose side connections do not have separate graph edges.
carve(286,64,22,10);carve(294,118,14,12);carve(482,118,25,12);
// Several connected spaces open into a single tall forest hollow.
carve(60,25,12,46);carve(208,26,15,44);carve(385,48,14,42);
// Remove one-cell spikes at contour intersections. Keep useful ledges and walls.
for(let pass=0;pass<2;pass++){
 const trim=[];for(let y=3;y<H-3;y++)for(let x=3;x<W-3;x++)if(solid(x,y)&&
  [[x-1,y],[x+1,y],[x,y-1],[x,y+1]].filter(([a,b])=>solid(a,b)).length<=1)trim.push(y*W+x);
 for(const k of trim)grid[k]=0;
}
// Solid floating islands use the ground occupancy itself: sides and undersides
// collide and receive the same rounded grass/earth artwork as the terrain.
for(const z of zones.filter(z=>z.floatingIsland)){
 const a=z.floatingIsland,x=z.x+a.dx,y=z.y+z.h+2-a.rise;
 for(let xx=x;xx<x+a.width;xx++)for(let yy=y-4;yy<y+a.depth;yy++)
  if(solid(xx,yy))throw Error('Island requires open air in zone '+z.number);
 for(let row=0;row<a.depth;row++)for(let xx=x+(row===a.depth-1?1:0);xx<x+a.width-(row===a.depth-1?1:0);xx++)grid[(y+row)*W+xx]=1;
 z.island={x,y,w:a.width,h:a.depth};
}
// Authored broad combat decks flank each island, leaving the lower lane open.
for(const z of zones){
 const list=z.combatDecks||(z.longDeck?[z.longDeck]:[]);
 for(const a of list){
  const x=z.x+a.dx,y=(z.combatDecks?z.y+z.h+2:Math.round(sample(z.shape.floor,x+a.width/2)))-a.rise;
  if(!add('dropfloor',`${z.number} · 긴 가지 전투턱`,x,y,a.width,'넓은 전투/착지 공간. 흙섬 옆에서 2단 점프로 상층 진입.')&&z.combatDecks)
   throw Error('Combat island approach blocked in zone '+z.number);
 }
}
// Bridge shaft mouths at encounter height with broad drop-through decks.
// This keeps broad combat ground while retaining vertical return paths.
for(const z of zones.filter(z=>cfg.combatZones.includes(z.number))){
 const y=z.y+z.h+2;
 for(let x=z.x+3;x<z.x+z.w-3;){
  if(solid(x,y)){x++;continue;}
  const start=x;while(x<z.x+z.w-3&&!solid(x,y))x++;
  for(let a=start;a<x;a+=24)add('dropfloor',`${z.number} · 전투 공터 연결 데크`,a,y,Math.min(24,x-a),'수직 통로를 막지 않는 전투용 착지 데크.');
 }
}
for(const s of shafts){
 if(s.c.type==='wallkick'){
  add('wallkick','벽차기 선택 샛길',s.x,s.top,s.width,'기존 우회 경로가 있는 선택 통로. Unity 벽차기 검증 전.',{h:(s.bottom-s.top)*32});
  add('dropfloor','샛길 입구',s.x,s.top,s.width,'하강용 통과 바닥');continue;
 }
 let k=0;
 for(let y=s.bottom-2;y>=s.top;y-=cfg.platformLayout.shaftStepTiles,k++){
  const w=cfg.platformLayout.shaftWidths[k%2],x=s.x+1+(k%2?Math.max(0,s.width-w-2):0);
  add('dropfloor',`${s.c.a}↔${s.c.b} · 넓은 복귀 데크`,x,y,w,'4칸(128px) 높이 간격. 2단 점프로 오르는 넓은 복귀 데크.');
 }
}
for(const c of cliffs){
 for(let y=c.lower-2,k=0;y>c.upper;y-=cfg.platformLayout.cliffStepTiles,k++){
  const x=c.side>0?c.x+1+(k%2?3:0):c.x-5-(k%2?3:0);
  add('dropfloor',`${c.zone} · 절벽 측면 착지턱`,x,y,cfg.platformLayout.cliffWidthTiles,'큰 절벽을 단계적으로 오르고 내려오는 측면 경로.');
 }
}
// Approach the exposed cliff faces after all corridors have been carved. These
// are short, staggered routes around solid earth, not floating straight bridges.
for(let x=5;x<W-5;x++)for(let y=5;y<H-6;y++)if(solid(x,y)&&!solid(x,y-1))for(const side of [-1,1]){
 if(solid(x+side,y))continue;let bottom=y;
 while(bottom<H-3&&!solid(x+side,bottom)&&solid(x,bottom))bottom++;
 const height=bottom-y;if(height<6||height>30)continue;
 for(let yy=bottom-2,k=0;yy>y;yy-=cfg.platformLayout.cliffStepTiles,k++){
  const w=cfg.platformLayout.cliffWidthTiles,offset=k%2?2:0,px=side>0?x+1+offset:x-w-offset;
  add('dropfloor','솟은 지면 · 측면 진입 발판',px,yy,w,'높은 땅의 옆면을 돌아 정상 평지에 도착하는 착지점.');
 }
}
// Pick valid preview feet on ground, away from shaft mouths and overhangs.
function feet(z,targetX){const out=[];for(let x=z.x+3;x<z.x+z.w-3;x++)for(let y=Math.max(4,z.y-8);y<Math.min(H-3,z.y+z.h+14);y++){
 if([-1,0,1].every(dx=>solid(x+dx,y)&&[1,2,3].every(dy=>!solid(x+dx,y-dy))))out.push([x+.5,y]);
 }if(!out.length)throw Error('No safe footing in '+z.number);
 return out.sort((a,b)=>Math.abs(a[0]-targetX)-Math.abs(b[0]-targetX)||a[1]-b[1])[0];}
for(const z of zones){z.foot=feet(z,z.x+z.focusTile).map(v=>v*32);delete z.floorProfile;delete z.roofProfile;delete z.focusTile;}
// Reserve continuous combat ground so terrain complements encounters instead
// of turning every landing into precision platforming.
for(const number of cfg.combatZones){const z=zones[number-1],runs=[];
 for(let y=Math.max(4,z.y-4);y<Math.min(H-3,z.y+z.h+14);y++){
  let start=null;for(let x=z.x+2;x<=z.x+z.w-2;x++){
   const valid=x<z.x+z.w-2&&solid(x,y)&&[1,2,3,4].every(dy=>!solid(x,y-dy));
   if(valid&&start===null)start=x;if(!valid&&start!==null){if(x-start>=8)runs.push({x:start,y,w:x-start});start=null;}
  }
 }
 runs.sort((a,b)=>b.w-a.w);if(!runs.length)throw Error('Combat floor too short in '+number);
 const r=runs[0];z.combatFloor={x:r.x,y:r.y,w:r.w};
 add('enemy',`${number} · 지면 전투 배치 후보`,r.x,r.y-4,r.w,'솟은 지형 사이의 연속 평지. 일반 전투 10마리 이상. 감지 전 제한 반경에서 무작위 배회.',{h:128});
}
// Retain landmark purposes, repositioning their feet onto the reshaped ground.
for(const old of cfg.landmarks){const m=structuredClone(old),number=Number(m.name.slice(0,2)),z=zones[number-1];if(!z)continue;
 if(['boss','midboss','elite'].includes(m.kind)){m.x=(z.x+6)*32;m.y=(z.y+z.h-16)*32;m.w=(z.w-12)*32;m.h=16*32;}
 else {const [x,y]=feet(z,old.x/32+old.w/64);m.w=Math.min(m.w,8*32);m.h=4*32;m.x=Math.round(x-m.w/64)*32;m.y=(y-4)*32;}
 m.id='landmark-'+old.id;markers.push(m);
}
// Air connectivity is a structural check, not a claim about jump physics.
const seen=new Uint8Array(W*H),components=[];for(let k=0;k<grid.length;k++)if(!grid[k]&&!seen[k]){const q=[k];seen[k]=1;for(let i=0;i<q.length;i++){const v=q[i],x=v%W,y=Math.floor(v/W);for(const [a,b]of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]])if(a>=0&&a<W&&b>=0&&b<H&&!solid(a,b)&&!seen[b*W+a]){seen[b*W+a]=1;q.push(b*W+a);}}components.push(q);}
// Remove sealed pinholes created where sloped corridor and cavern contours touch.
const main=components.sort((a,b)=>b.length-a.length)[0];for(const c of components.slice(1)){if(c.length>80)throw Error('Disconnected chamber: '+c.length);for(const k of c)grid[k]=1;}
rules.ground=Array.from(grid).join('');
const terrain=new box.TerrainEditor.TerrainModel({...data.plan,cells:[]},rules),cells=[];
for(let y=0;y<H;y++)for(let x=0;x<W;x++){const id=terrain.choose(x,y);if(id!==undefined)cells.push([x,y,id]);}
data.plan={...data.plan,id:cfg.id,name:'ver2 · 절벽과 깊은 숲',summary:'24개 연결 공간 · 넓은 전투 데크와 8개 공중 흙섬 · 지상/상층 분산 전투',zones,encounters:cfg.encounters,cells,sketch:[],connections:cfg.connections,spawn:zones[0].foot,
 checks:{airComponents:[main.length],tileIdsWithinCurrentAtlas:true,jumpSimulationRun:false,unityApplied:false}};
data.markers=markers;data.highlights=[5,6,8,20].map(n=>({name:zones[n-1].name,point:zones[n-1].foot}));data.initialView=zones[5].foot;
data.measurements={...data.measurements,currentPlatforms:markers.filter(m=>m.kind==='platform').length,dropFloors:markers.filter(m=>m.kind==='dropfloor').length,trampolines:0,narrowPassages:3,authoredCliffs:cliffs.length};
data.validation={airConnected:true,unityApplied:false,physicsSimulation:false,sourceArtworkUnscaled:true};
fs.writeFileSync(path.join(root,'data/map.js'),'window.EXPANDED_MAP='+JSON.stringify(data)+';\n');
fs.writeFileSync(path.join(root,'data/terrain-rules.js'),'window.TERRAIN_RULES='+JSON.stringify(rules)+';\n');
fs.writeFileSync(path.join(root,'data/placements.json'),JSON.stringify({schema:'forest-layout-markers-v1',mapId:cfg.id,width:W*32,height:H*32,markers},null,2)+'\n');
console.log(JSON.stringify({zones:zones.length,ground:grid.reduce((a,b)=>a+b,0),connectedAir:main.length,cliffs,markers:markers.length,platforms:data.measurements.currentPlatforms,dropFloors:data.measurements.dropFloors}));
