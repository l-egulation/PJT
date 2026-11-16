const W=960,H=540,TILE=32,COLS=640,ROWS=64,WORLD=COLS*TILE,WORLD_HEIGHT=ROWS*TILE,SURFACE=0;
const FOOT_SCREEN=H*(236/360),BACKGROUND_SCALE=1;
const canvas=document.querySelector('#game'),ctx=canvas.getContext('2d',{alpha:false});
canvas.width=W;canvas.height=H;
ctx.imageSmoothingEnabled=false;
const state={camera:0,cameraY:288-FOOT_SCREEN,cameraVX:0,cameraVY:0,forestOffset:40,dropUntil:0,keys:new Set(),layers:{far:true,mid:true,ground:true,hero:true,grid:false,collisions:false},loaded:false,time:0};
const images=new Map(),manifestURL=new URL('assets/manifest.json',location.href);
let manifest={},assetRoot=new URL('.',manifestURL),heroImg;
const hero={x:220,y:288,vx:0,vy:0,jumps:0,grounded:true,facing:1};
// 8 authored areas, each 80 cells wide. Long flats alternate with deliberate
// separated terraces. Tall cliffs have reversible double-jump shelf routes.
const level=window.FOREST_LEVEL;
const {profile,mask,platforms,terrainRuns,cliffRoutes,zones}=level;
const occupied=(c,r)=>r>=0&&r<ROWS&&c>=0&&c<COLS&&mask[r][c];
const imageURL=src=>new URL(src,assetRoot).href;
const loadImage=async src=>{const u=imageURL(src);if(images.has(u))return images.get(u);const im=new Image();im.src=u;await im.decode();images.set(u,im);return im;};
const asDesc=v=>typeof v==='string'?{src:v}:v;
const arrayOf=v=>!v?[]:Array.isArray(v)?v:Object.entries(v).map(([name,obj])=>({name,...asDesc(obj)}));

function getAsset(name){return manifest.assets?.[name]??manifest[name];}
async function loadDescriptor(raw){const d=asDesc(raw);if(!d?.src)return null;return {...d,img:await loadImage(d.src)};}
let art={far:[],mid:[],midVariants:[],midSeams:[]},modules={};
let terrainAtlas=null,terrainColumns=new Map(),backgroundLayers=[];
let animationData=null,clips=new Map(),heroFrames=new Map();

let monsterVariants=[],monsterMode='placed',monsterLabels=false,monsterPalette='dusk';
async function loadMonsters(){
  if(!window.CHAPTER_MONSTERS)return;
  const root=new URL(window.CHAPTER_MONSTERS.root,location.href);
  const response=await fetch(new URL('manifest.json',root));
  if(!response.ok)throw new Error('몬스터 목록을 읽지 못했습니다.');
  const data=await response.json();
  monsterVariants=await Promise.all(data.variants.map(async v=>{
    const img=new Image();img.src=new URL(v.file,root);await img.decode();
    const originalImg=new Image();originalImg.src=new URL(v.originalFile||v.file,root);await originalImg.decode();return {...v,img,originalImg,baseline:v.baselineFromTop??data.baselineFromTop};
  }));
  const shelf=document.querySelector('#monsterShelf');
  if(shelf)for(const v of monsterVariants){
    const card=document.createElement('button');card.title=v.name+' 배치 위치로 이동';
    const img=document.createElement('img');img.src=v.img.src;img.width=v.img.naturalWidth;img.height=v.img.naturalHeight;
    const label=document.createElement('span');label.textContent=v.name;
    card.append(img,label);shelf.append(card);
    card.onclick=()=>{monsterMode='placed';document.querySelector('#monsterMode').value=monsterMode;
      const placement=window.CHAPTER_MONSTERS.placements.find(p=>monsterVariants[p[0]].id===v.id);
      setCamera(placement[1]-W*.6,true);};
  }
  window.__monsters={variants:monsterVariants,placements:window.CHAPTER_MONSTERS.placements,get mode(){return monsterMode;}};
}
function drawMonsters(){
  if(monsterMode==='hidden'||!monsterVariants.length)return;
  const placements=monsterMode==='lineup'?monsterVariants.map((v,i)=>[i,600+i*108]):window.CHAPTER_MONSTERS.placements;
  for(const [index,x] of placements){
    const v=monsterVariants[index],native=monsterPalette!=='original',img=native?v.img:v.originalImg;
    const scale=native?1:(v.comparisonScale||1),width=Math.round(img.naturalWidth*scale),height=Math.round(img.naturalHeight*scale);
    const baseline=native?v.baseline:Math.round(120*scale),half=width/2;
    if(x+half<state.camera||x-half>state.camera+W)continue;
    const y=surfaceAt(x),sx=Math.round(x-state.camera);
    if(native)ctx.drawImage(img,Math.round(sx-half),y-baseline);
    else ctx.drawImage(img,Math.round(sx-half),y-baseline,width,height);
    if(monsterLabels){ctx.save();ctx.font='11px sans-serif';ctx.textAlign='center';
      const label=v.name,w=ctx.measureText(label).width+12,ly=y-baseline+(native?v.bounds.top:Math.round(v.bounds.top*scale))-20;
      ctx.fillStyle='#17151ee8';ctx.fillRect(sx-w/2,ly,w,17);ctx.fillStyle='#f7e4be';ctx.fillText(label,sx,ly+12);ctx.restore();}
  }
}

const motion={ready:false,id:'idle',elapsedMs:0,clock:0,action:null,dead:false,queued:false,comboUntil:0,dashUntil:0,dashFacing:1,airDashed:false};
const motionLabels={idle:'대기',walk:'걷기',run:'달리기',jump_start:'점프 시작',jump_rise:'상승',jump_apex:'정점',fall_start:'낙하 시작',fall_loop:'낙하',land:'착지',dash_start:'대시 시작',dash_active:'대시',dash_end:'대시 마무리',death_start:'사망 시작',death_fall:'쓰러짐',death_hold:'사망 유지',attack1:'공격 1타',attack2:'공격 2타'};

// Frame durations are sampled directly in milliseconds, including the final
// recovery frame. Neither image bounds nor a uniform FPS alter the approved art.
function sampleModule(clip,elapsedMs){
  if(!clip?.steps?.length)return null;
  const total=clip.steps.reduce((sum,s)=>sum+Number(s.ms),0);
  const elapsed=Math.max(0,elapsedMs),local=clip.loop?elapsed%total:Math.min(elapsed,total);
  let end=0,index=clip.steps.length-1;
  for(let i=0;i<clip.steps.length;i++){end+=Number(clip.steps[i].ms);if(local<end-1e-7){index=i;break;}}
  return {step:clip.steps[index],index,total,localMs:local,done:!clip.loop&&elapsed>=total-1e-7};
}
function currentSample(){return sampleModule(clips.get(motion.id),motion.elapsedMs);}
function setMotion(id,restart=false,elapsedMs=0){
  if(!clips.has(id))return false;
  if(restart||motion.id!==id){motion.id=id;motion.elapsedMs=Math.max(0,elapsedMs);}return true;
}
function configureAnimation(data){
  animationData=data;clips=new Map(data.modules.map(clip=>[clip.id,clip]));
  for(const clip of clips.values())if(!clip.steps?.length||clip.steps.some(s=>!(Number(s.ms)>0)||!data.frames[s.frame]))throw new Error(`캐릭터 모션 데이터 오류: ${clip.id}`);
  Object.assign(motion,{ready:true,id:'idle',elapsedMs:0,clock:0,action:null,dead:false,queued:false,comboUntil:0,dashUntil:0,dashFacing:1,airDashed:false});
  document.querySelectorAll('[data-action]').forEach(button=>button.disabled=false);
}
function locomotion(){return hero.grounded?(Math.abs(hero.vx)>.1?(Math.abs(hero.vx)>140?'run':'walk'):'idle'):(hero.vy<-45?'jump_rise':hero.vy<=45?'jump_apex':'fall_start');}
function finishAction(overflow=0){
  motion.action=null;motion.queued=false;
  const dir=(state.keys.has('ArrowRight')||state.keys.has('KeyD')?1:0)-(state.keys.has('ArrowLeft')||state.keys.has('KeyA')?1:0);
  hero.vx=dir*(state.keys.has('ShiftLeft')||state.keys.has('ShiftRight')?170:115);
  setMotion(locomotion(),true,overflow);
}
function requestJump(){
  if(motion.dead||motion.action||hero.jumps>=2)return false;
  if(hero.grounded&&(state.keys.has('ArrowDown')||state.keys.has('KeyS'))&&platforms.some(p=>hero.x+9>p.x&&hero.x-9<p.x+p.w&&Math.abs(hero.y-p.y)<.1)){
    state.dropUntil=state.time+.25;hero.y+=2;hero.vy=35;hero.grounded=false;hero.jumps=1;setMotion('fall_start',true);return true;
  }
  hero.vy=-345;hero.grounded=false;hero.jumps++;setMotion('jump_start',true);return true;
}
function requestAttack(){
  if(!motion.ready||motion.dead||motion.action==='dash')return false;
  if(motion.action==='attack1'){
    const chain=animationData.attack_events?.['1']?.chainAt??currentSample().total;
    if(motion.elapsedMs>=chain-1e-7){motion.action='attack2';motion.queued=false;setMotion('attack2',true);}
    else motion.queued=true;
    return true;
  }
  if(motion.action)return false;
  const id=motion.comboUntil>motion.clock?'attack2':'attack1';motion.action=id;motion.queued=false;motion.comboUntil=0;setMotion(id,true);return true;
}
function requestDash(){
  if(!motion.ready||motion.dead||motion.action||motion.clock<motion.dashUntil||(!hero.grounded&&motion.airDashed))return false;
  motion.action='dash';motion.queued=false;motion.comboUntil=0;motion.dashFacing=hero.facing;motion.dashUntil=motion.clock+450;
  if(!hero.grounded)motion.airDashed=true;hero.vy=0;setMotion('dash_start',true);return true;
}
function requestDeath(){
  if(!motion.ready||motion.dead)return false;
  motion.dead=true;motion.action='death';motion.queued=false;motion.comboUntil=0;hero.vx=0;state.keys.clear();setMotion('death_start',true);return true;
}
function revive(){
  Object.assign(motion,{action:null,dead:false,queued:false,comboUntil:0,dashUntil:0,airDashed:false});
  hero.vx=hero.vy=0;hero.jumps=0;state.keys.clear();
  const floor=surfaceAt(hero.x),shelves=platforms.filter(p=>hero.x>=p.x&&hero.x<p.x+p.w&&p.y>=hero.y-.01).map(p=>p.y);
  hero.y=Math.min(floor,...shelves);hero.grounded=true;setMotion('idle',true);return true;
}
function advanceMotion(dt,wasGrounded){
  if(!motion.ready)return;const ms=dt*1000;motion.clock+=ms;
  if(hero.grounded)motion.airDashed=false;
  if(!motion.action){
    const sample=currentSample();
    if(!wasGrounded&&hero.grounded)setMotion('land',true);
    else if(hero.grounded){if(motion.id!=='land'||sample?.done)setMotion(locomotion());}
    else if(motion.id==='jump_start'&&!sample?.done){}
    else if(hero.vy<-45)setMotion('jump_rise');
    else if(hero.vy<=45)setMotion('jump_apex');
    else if(motion.id==='fall_start'){if(sample?.done)setMotion('fall_loop');}
    else if(motion.id!=='fall_loop')setMotion('fall_start');
  }
  motion.elapsedMs+=ms;
  // Consume overshoot when crossing phase boundaries, retaining exact timing
  // at low frame rates and for tests that step by large time intervals.
  for(let guard=0;guard<8;guard++){
    const sample=currentSample();if(!sample)break;
    if(motion.action==='attack1'&&motion.queued){
      const chain=animationData.attack_events?.['1']?.chainAt??sample.total;
      if(motion.elapsedMs>=chain-1e-7){const extra=motion.elapsedMs-chain;motion.action='attack2';motion.queued=false;setMotion('attack2',true,extra);continue;}
    }
    if(!sample.done)break;const extra=Math.max(0,motion.elapsedMs-sample.total);
    const next={death_start:'death_fall',death_fall:'death_hold',dash_start:'dash_active',dash_active:'dash_end'}[motion.id];
    if(next){setMotion(next,true,extra);continue;}
    if(motion.id==='death_hold')break;
    if(motion.action==='attack1'){motion.comboUntil=motion.clock-extra+(animationData.combo_window_ms??220);finishAction(extra);continue;}
    if(motion.action==='attack2'||motion.id==='dash_end'){finishAction(extra);continue;}
    if(motion.id==='jump_start'){setMotion(hero.vy<-45?'jump_rise':hero.vy<=45?'jump_apex':'fall_start',true,extra);continue;}
    if(motion.id==='fall_start'){setMotion('fall_loop',true,extra);continue;}
    if(motion.id==='land'){setMotion(locomotion(),true,extra);continue;}
    break;
  }
}
function motionSnapshot(){
  const s=currentSample();return {id:motion.id,frame:s?.step.frame??null,index:s?.index??0,elapsedMs:motion.elapsedMs,durationMs:s?.total??0,done:s?.done??false,dead:motion.dead,action:motion.action,queued:motion.queued,clock:motion.clock,comboUntil:motion.comboUntil,hero:{...hero}};
}
async function init(){
  try{
    const response=await fetch(manifestURL,{cache:'no-store'});if(!response.ok)throw new Error('에셋 manifest.json 파일을 찾지 못했습니다.');manifest=await response.json();
    for(const type of ['mid']){
      let raw=getAsset(type);if(raw?.src||typeof raw==='string')raw=[raw];
      art[type]=(await Promise.all(arrayOf(raw).map(loadDescriptor))).filter(Boolean);
    }
    try{heroImg=await loadImage(manifest.hero?.src||'hero.png');}catch(e){console.warn('Hero not loaded',e);}
    if(manifest.hero?.animationManifest){
      const animationResponse=await fetch(imageURL(manifest.hero.animationManifest),{cache:'no-store'});if(!animationResponse.ok)throw new Error('캐릭터 재생 설정을 읽지 못했습니다.');
      const data=await animationResponse.json();
      heroFrames=new Map(await Promise.all(Object.entries(data.frames).map(async([id,frame])=>[id,await loadImage(frame.src)])));
      configureAnimation(data);
    }
    for(const [name,raw] of Object.entries(manifest.assets?.modules||{}).filter(([name])=>name.startsWith('rim_')&&!name.startsWith('rim_side_'))){const d=await loadDescriptor(raw);if(d)modules[name]=d;}
    if(!modules.rim_top_01||!modules.rim_platform_left)throw new Error('v2 광폭 지표/흙 모듈이 아직 준비되지 않았습니다.');
    terrainAtlas=new Image();terrainAtlas.src='terrain-atlas.png';await terrainAtlas.decode();
    terrainAtlas=window.FOREST_PRESENTATION.gradeImage(terrainAtlas,window.FOREST_BACKGROUNDS.terrainGrade);
    for(const cell of window.FOREST_TERRAIN.cells){if(!terrainColumns.has(cell[0]))terrainColumns.set(cell[0],[]);terrainColumns.get(cell[0]).push(cell);}
    backgroundLayers=await Promise.all(window.FOREST_BACKGROUNDS.layers.map(async layer=>{const img=new Image();img.src=layer.src;await img.decode();return {...layer,img:window.FOREST_PRESENTATION.gradeImage(img,layer.grade)};}));
    await loadMonsters();
    state.loaded=true;document.querySelector('#loading').hidden=true;
    document.querySelector('#assetCount').textContent='90도 회전 벽 · 뒤집힌 잔디 천장 · 3개 굴 · 나무 '+level.trees.length+'그루';
    buildInspector();draw();
    window.__assembly={state,hero,profile,platforms,zones,terrainRuns,cliffRoutes,art,modules,manifest,render:draw,setCamera:setCamera,motion:window.__motion,dimensions:{W,H,TILE,WORLD,WORLD_HEIGHT}};
  }catch(error){document.querySelector('#loading').textContent=`${error.message}\n에셋 준비 후 새로고침해 주세요.`;console.error(error);}
}
function sprite(d,x,y,flip=false){if(!d)return;const w=d.width||d.img.naturalWidth,h=d.height||d.img.naturalHeight;ctx.save();ctx.translate(Math.round(x)+(flip?w:0),Math.round(y));if(flip)ctx.scale(-1,1);ctx.drawImage(d.img,d.sx||0,d.sy||0,w,h,0,0,w,h);ctx.restore();}
function strip(d,parallax,defaultY){
  const w=d.width||d.img.naturalWidth,y=d.y??defaultY,offset=state.camera/BACKGROUND_SCALE*(d.parallax??parallax),first=Math.floor(offset/w)-1;
  for(let i=first;i*w-offset<W;i++)sprite(d,i*w-offset,y,!!d.alternateFlip&&i%2!==0);
}
function surfaceAt(x){return profile[Math.max(0,Math.min(COLS-1,Math.floor(x/TILE)))]*TILE+SURFACE;}
const groveAnchors=new Map();
function groveGround(x,width){
  const key=x+':'+width;if(groveAnchors.has(key))return groveAnchors.get(key);
  // A grove may span a cliff. Use its lowest ground so higher ground buries
  // the trunks rather than exposing roots over the neighboring valley.
  const left=Math.max(0,Math.floor(x/TILE)),right=Math.min(COLS-1,Math.floor((x+width-1)/TILE));
  let floor=0;for(let c=left;c<=right;c++)floor=Math.max(floor,profile[c]*TILE);
  const anchor=floor+24;groveAnchors.set(key,anchor);return anchor;
}
function getTreePlacements(){return level.trees.map(t=>({...t,d:art.mid.find(d=>d.name===t.name)}));}
function forestGroundReference(cameraX,cameraY){
 const col=Math.max(0,Math.min(COLS-1,Math.floor((cameraX+W*.45)/TILE))),probe=cameraY+FOOT_SCREEN;let last=profile[col]*TILE;
 for(let row=0;row<ROWS;row++)if(mask[row][col]&&!mask[row-1]?.[col]){last=row*TILE;if(last>=probe-16)return last;}
 return last;
}
function forestLayerTop(layer,cameraX,cameraY){
 if(layer.anchorToTerrain){const rise=Math.max(0,forestGroundReference(cameraX,cameraY)-cameraY-FOOT_SCREEN);return -Math.round(Math.max(0,layer.baseCrop-rise));}
 return -Math.round(Math.max(layer.minVertical,Math.min(layer.maxVertical,layer.baseCrop+cameraY*layer.vertical)));
}
function drawBackdrops(){
 ctx.fillStyle='#554b3a';ctx.fillRect(0,0,W,H);
 if(state.layers.far)for(const layer of backgroundLayers){
  const offset=state.camera*layer.parallax+layer.phase,first=Math.floor(offset/layer.width)-1,y=forestLayerTop(layer,state.camera,state.cameraY);
  for(let n=first;n*layer.width-offset<W;n++){
   const flip=((n%2)+2)%2===1;ctx.save();ctx.translate(Math.round(n*layer.width-offset)+(flip?layer.width:0),y);if(flip)ctx.scale(-1,1);ctx.drawImage(layer.img,0,0);
   if(layer.repeatBottom&&y+layer.height<H){ctx.translate(0,2*layer.height);ctx.scale(1,-1);ctx.drawImage(layer.img,0,0);}ctx.restore();
  }
 }
 if(state.layers.mid)for(const tree of getTreePlacements()){if(tree.x+tree.d.width<state.camera||tree.x>state.camera+W)continue;sprite(tree.d,tree.x-state.camera,tree.y-state.cameraY);}
}
function drawTerrain(){
 const minY=Math.floor(state.cameraY/32)-2,maxY=Math.ceil((state.cameraY+H)/32)+2,columns=window.FOREST_TERRAIN.columns;
 for(let col=Math.floor(state.camera/32)-2;col<Math.ceil((state.camera+W)/32)+2;col++)for(const [x,y,id] of terrainColumns.get(col)||[]){
  if(y<minY||y>maxY)continue;ctx.drawImage(terrainAtlas,id%columns*32,Math.floor(id/columns)*32,32,32,x*32-Math.round(state.camera),y*32,32,32);
 }
}
function drawHero(){
  const step=currentSample()?.step,img=heroFrames.get(step?.frame)||heroImg;if(!img)return;
  const pivot=motion.ready?(animationData.pivot||[64,115]):[64,121];
  ctx.save();ctx.translate(Math.round(hero.x-state.camera),Math.round(hero.y));if(hero.facing<0)ctx.scale(-1,1);
  ctx.drawImage(img,-pivot[0]+(step?.dx||0),-pivot[1]+(step?.dy||0));ctx.restore();
}
function draw(){
  if(!state.loaded)return;if(monsterMode==='lineup'){state.camera=480;state.cameraY=256-FOOT_SCREEN;state.cameraVX=state.cameraVY=0;}ctx.imageSmoothingEnabled=false;drawBackdrops();
  ctx.save();ctx.translate(0,-Math.round(state.cameraY));
  if(state.layers.ground)drawTerrain();drawMonsters();if(state.layers.hero)drawHero();
  if(state.layers.grid){ctx.strokeStyle='#ffffff38';ctx.lineWidth=1;ctx.beginPath();for(let x=-(state.camera%32);x<W;x+=32){ctx.moveTo(x+.5,state.cameraY);ctx.lineTo(x+.5,state.cameraY+H);}for(let y=Math.floor(state.cameraY/32)*32;y<state.cameraY+H;y+=32){ctx.moveTo(0,y+.5);ctx.lineTo(W,y+.5);}ctx.stroke();}
  if(state.layers.collisions){ctx.strokeStyle='#6ff5e5';ctx.lineWidth=1;ctx.beginPath();profile.forEach((r,c)=>{const x=c*TILE-state.camera;if(c===0)ctx.moveTo(x,r*TILE+SURFACE);else ctx.lineTo(x,r*TILE+SURFACE);ctx.lineTo(x+TILE,r*TILE+SURFACE);});platforms.forEach(p=>{ctx.moveTo(p.x-state.camera,p.y+SURFACE);ctx.lineTo(p.x+p.w-state.camera,p.y+SURFACE);});ctx.stroke();ctx.strokeRect(Math.round(hero.x-state.camera-9)+.5,Math.round(hero.y-62)+.5,18,62);}
  ctx.restore();
  let zone=zones[0];zones.forEach(z=>{if(hero.x>=z.x)zone=z;});document.querySelector('#zoneTitle').textContent=zone.name;
  document.querySelector('#zoneHint').textContent=zone.hint;
  document.querySelector('#position').textContent=`${Math.round(hero.x).toLocaleString()} / ${WORLD.toLocaleString()} px`;
  document.querySelector('#camera').value=Math.round(state.camera);
  const sample=currentSample();
  document.querySelector('#motionStatus').textContent=sample?`${motionLabels[motion.id]||motion.id} ${sample.index+1}/${clips.get(motion.id).steps.length} · ${sample.step.frame}${motion.id==='death_hold'?' · 마지막 자세 유지':''}${motion.queued?' · 2타 예약':''}`:'캐릭터 모션 준비 중';
  drawOverview();
}
function setCamera(x,moveHero=false,redraw=true){state.camera=Math.max(0,Math.min(WORLD-W,Math.round(x)));state.cameraVX=state.cameraVY=0;if(moveHero){hero.x=state.camera+W*.45;hero.y=surfaceAt(hero.x);hero.vx=hero.vy=0;hero.grounded=true;hero.jumps=0;state.keys.clear();state.dropUntil=0;state.cameraY=Math.max(-384,Math.min(WORLD_HEIGHT-H,hero.y-FOOT_SCREEN));state.forestOffset=40;Object.assign(motion,{action:null,dead:false,queued:false,comboUntil:0,airDashed:false});setMotion('idle',true);}if(redraw)draw();}
let mapCache=null;
function drawOverview(){
 const map=document.querySelector('#overview');if(!map)return;const c=map.getContext('2d'),sx=map.width/WORLD,sy=map.height/WORLD_HEIGHT;
 if(!mapCache){mapCache=document.createElement('canvas');mapCache.width=map.width;mapCache.height=map.height;const b=mapCache.getContext('2d');b.fillStyle='#3c383d';b.fillRect(0,0,map.width,map.height);b.fillStyle='#211b24';
 for(let row=0;row<ROWS;row++)for(let col=0;col<COLS;col++)if(mask[row][col])b.fillRect(col*32*sx,row*32*sy,32*sx+.5,32*sy+.5);
 b.fillStyle='#d8bd59';for(let row=0;row<ROWS;row++)for(let col=0;col<COLS;col++)if(mask[row][col]&&!mask[row-1]?.[col])b.fillRect(col*32*sx,row*32*sy,32*sx+.5,1);
 b.fillStyle='#95bd9e';platforms.forEach(p=>b.fillRect(p.x*sx,p.y*sy,p.w*sx,1.5));}
 c.drawImage(mapCache,0,0);c.strokeStyle='#fff0ce';c.lineWidth=1;c.strokeRect(state.camera*sx,Math.max(0,state.cameraY)*sy,W*sx,H*sy);c.fillStyle='#80e5ee';c.fillRect(hero.x*sx-2,hero.y*sy-4,4,4);
}
function colliding(x,y){
  const l=Math.floor((x-8.99)/32),r=Math.floor((x+8.99)/32),top=Math.floor((y-61.99-SURFACE)/32),bottom=Math.floor((y-.01-SURFACE)/32);
  for(let yy=top;yy<=bottom;yy++)for(let xx=l;xx<=r;xx++)if(occupied(xx,yy))return true;return false;
}
function followCamera(dt){
  window.FOREST_PRESENTATION.followCamera(state,hero,dt,{width:W,height:H,worldWidth:WORLD,worldHeight:WORLD_HEIGHT,foot:FOOT_SCREEN},window.FOREST_BACKGROUNDS?.camera);
}
function advance(dt){
  const wasGrounded=hero.grounded;
  const dir=(state.keys.has('ArrowRight')||state.keys.has('KeyD')?1:0)-(state.keys.has('ArrowLeft')||state.keys.has('KeyA')?1:0);
  const dashing=motion.ready&&motion.action==='dash';
  if(motion.dead||motion.action?.startsWith('attack'))hero.vx=0;
  else if(dashing){const fraction=motion.elapsedMs/(currentSample()?.total||1);hero.vx=motion.dashFacing*(motion.id==='dash_active'?420:motion.id==='dash_end'?170*Math.max(0,1-fraction):0);}
  else hero.vx=dir*(state.keys.has('ShiftLeft')||state.keys.has('ShiftRight')?170:115);
  if(dir&&!motion.action)hero.facing=dir;
  const dx=hero.vx*dt,steps=Math.max(1,Math.ceil(Math.abs(dx)/3));for(let i=0;i<steps;i++){const next=hero.x+dx/steps;if(!colliding(next,hero.y))hero.x=Math.max(12,Math.min(WORLD-12,next));}
  const hoverDash=dashing&&motion.id!=='dash_end'&&!wasGrounded;
  hero.vy=hoverDash?0:Math.min(500,hero.vy+800*dt);const dy=hero.vy*dt,ysteps=Math.max(1,Math.ceil(Math.abs(dy)/3));hero.grounded=false;
  for(let i=0;i<ysteps;i++){
    const next=hero.y+dy/ysteps;
    const shelf=hero.vy>=0&&state.time>=state.dropUntil?platforms.find(p=>hero.x+9>p.x&&hero.x-9<p.x+p.w&&hero.y<=p.y+SURFACE+.01&&next>=p.y+SURFACE):null;
    if(shelf){hero.y=shelf.y+SURFACE;hero.vy=0;hero.grounded=true;hero.jumps=0;break;}
    if(colliding(hero.x,next)){if(hero.vy>0){hero.y=Math.round((hero.y-SURFACE)/32)*32+SURFACE;hero.grounded=true;hero.jumps=0;}hero.vy=0;break;}hero.y=next;
  }
  // A very short final substep can fall less than the collision epsilon.
  // Probe a fraction of a pixel below the feet so an idle actor stays grounded.
  if(!hero.grounded&&hero.vy>=0&&colliding(hero.x,hero.y+.05)){
    hero.y=Math.round((hero.y-SURFACE)/32)*32+SURFACE;hero.vy=0;hero.grounded=true;hero.jumps=0;
  }
  if(hero.y>WORLD_HEIGHT+128){hero.y=surfaceAt(hero.x);hero.vy=0;hero.jumps=0;}
  advanceMotion(dt,wasGrounded);
  followCamera(dt);
  // The backdrop cover scale fills the new view without exposing the panorama cut.
  state.forestOffset=40;
}
function stepSimulation(ms){let remaining=Math.max(0,ms)/1000;while(remaining>1e-9){const dt=Math.min(1/120,remaining);advance(dt);remaining-=dt;}state.time+=Math.max(0,ms)/1000;}
let last=null;function loop(now){if(document.hidden){last=null;requestAnimationFrame(loop);return;}const elapsed=last===null?0:Math.max(0,now-last);last=now;if(state.loaded){stepSimulation(elapsed);draw();}requestAnimationFrame(loop);}requestAnimationFrame(loop);
if(document.addEventListener)document.addEventListener('visibilitychange',()=>{last=null;state.keys.clear();});
canvas.addEventListener('keydown',e=>{
  const codes=['ArrowLeft','ArrowRight','ArrowDown','KeyS','Space','ArrowUp','KeyW','KeyA','KeyD','ShiftLeft','ShiftRight','ControlLeft','ControlRight','KeyJ','KeyK','KeyR'];
  if(codes.includes(e.code)){e.preventDefault();state.keys.add(e.code);}if(e.repeat)return;
  if(['Space','ArrowUp','KeyW'].includes(e.code))requestJump();
  if(e.code==='KeyJ')requestAttack();if(e.code==='KeyK')requestDeath();if(e.code==='KeyR')revive();
  if(['ControlLeft','ControlRight'].includes(e.code))requestDash();
});
window.addEventListener('keyup',e=>state.keys.delete(e.code));canvas.addEventListener('blur',()=>state.keys.clear());
document.querySelector('#camera').addEventListener('input',e=>setCamera(Number(e.target.value),true));
document.querySelector('#camera').max=WORLD-W;
const overview=document.querySelector('#overview');if(overview)overview.addEventListener('click',e=>{const rect=overview.getBoundingClientRect();setCamera((e.clientX-rect.left)/rect.width*WORLD-W*.45,true);canvas.focus({preventScroll:true});});
document.querySelectorAll('[data-x]').forEach(b=>b.onclick=()=>{setCamera(Number(b.dataset.x),true);canvas.focus({preventScroll:true});});
document.querySelectorAll('[data-layer]').forEach(box=>box.onchange=()=>{state.layers[box.dataset.layer]=box.checked;draw();});
document.querySelector('#reset').onclick=()=>setCamera(0,true);
// Enlarge the full 960x540 viewport uniformly; keep the simulation unchanged.
let nativeFullscreen=false;
async function enterFullscreen(){
  document.body.classList.add('play-fullscreen');state.keys.clear();
  const viewport=document.querySelector('.scene-viewport');
  try{if(viewport.requestFullscreen)await viewport.requestFullscreen();}catch(error){/* Embedded browser: use full-tab layout. */}
  canvas.focus({preventScroll:true});
}
async function exitFullscreen(){
  document.body.classList.remove('play-fullscreen');state.keys.clear();
  if(document.fullscreenElement&&document.exitFullscreen)await document.exitFullscreen();
  canvas.focus({preventScroll:true});
}
document.querySelector('#fullscreen').onclick=enterFullscreen;
document.querySelector('#exitFullscreen').onclick=exitFullscreen;
if(document.addEventListener)document.addEventListener('fullscreenchange',()=>{
  if(document.fullscreenElement)nativeFullscreen=true;
  else if(nativeFullscreen){nativeFullscreen=false;document.body.classList.remove('play-fullscreen');state.keys.clear();canvas.focus({preventScroll:true});}
});
window.addEventListener('keydown',e=>{
  if(e.code==='Escape'&&document.body.classList.contains('play-fullscreen')){e.preventDefault();exitFullscreen();}
  if(e.code==='KeyF'&&!e.repeat&&document.activeElement===canvas){e.preventDefault();document.body.classList.contains('play-fullscreen')?exitFullscreen():enterFullscreen();}
});
document.querySelector('#download').onclick=()=>{const a=document.createElement('a');a.href=canvas.toDataURL('image/png');a.download=`시골황혼_실제조립_${Math.round(state.camera)}px.png`;a.click();};
document.querySelector('#sceneMode').onclick=()=>mode('scene');document.querySelector('#tilesMode').onclick=()=>mode('tiles');
const actions={jump:requestJump,attack:requestAttack,dash:requestDash,die:requestDeath,revive};
document.querySelectorAll('[data-action]').forEach(button=>button.onclick=()=>{actions[button.dataset.action]?.();canvas.focus({preventScroll:true});draw();});
window.__motion={
  configure:configureAnimation,
  sampleModule:(id,elapsedMs)=>{const clip=typeof id==='string'?clips.get(id):id,s=sampleModule(clip,elapsedMs);return s?{...s,id:clip.id,frame:s.step.frame,elapsedMs:s.localMs,durationMs:s.total,complete:s.done}:null;},
  snapshot:motionSnapshot,actions,
  step:(ms,keys=[])=>{state.keys.clear();keys.forEach(key=>state.keys.add(key));stepSimulation(ms);return motionSnapshot();},
  teleport:(x,y,grounded=true)=>{Object.assign(hero,{x,y,vx:0,vy:0,jumps:grounded?0:1,grounded});return motionSnapshot();},
  reset:()=>{Object.assign(hero,{x:220,y:288,vx:0,vy:0,jumps:0,grounded:true,facing:1});Object.assign(motion,{clock:0,action:null,dead:false,queued:false,comboUntil:0,dashUntil:0,airDashed:false});state.keys.clear();Object.assign(state,{camera:0,cameraY:288-FOOT_SCREEN,cameraVX:0,cameraVY:0,forestOffset:40,dropUntil:0});setMotion('idle',true);return motionSnapshot();}
};
function mode(which){document.querySelector('#scenePanel').hidden=which!=='scene';document.querySelector('#tilesPanel').hidden=which!=='tiles';document.querySelector('#sceneMode').classList.toggle('active',which==='scene');document.querySelector('#tilesMode').classList.toggle('active',which==='tiles');state.keys.clear();}
document.querySelector('#assetZoom').onchange=buildInspector;
document.querySelector('#sceneZoom').onchange=e=>{
  document.querySelector('.scene-frame').dataset.zoom=e.target.value;
};
if(Number.isFinite(window.innerWidth)&&window.innerWidth<680){
  document.querySelector('#sceneZoom').value='fit';
  document.querySelector('.scene-frame').dataset.zoom='fit';
}
function buildInspector(){
 const target=document.querySelector('#assetGroups');target.replaceChildren();
 for(const layer of window.FOREST_BACKGROUNDS.layers){const title=document.createElement('h3');title.className='asset-heading';title.textContent=layer.label+' · 원본 배율 1:1 · '+layer.width+'×'+layer.height;target.append(title);const link=document.createElement('a');link.href=layer.src;link.target='_blank';const photo=document.createElement('img');photo.src=link.href;photo.style.maxWidth='100%';photo.style.imageRendering='pixelated';link.append(photo);target.append(link);}
 const h=document.createElement('h3');h.className='asset-heading';h.textContent='현재 맵에 사용한 32×32 타일 '+(window.FOREST_TERRAIN?.uniqueTiles||0)+'종';target.append(h);
 const a=document.createElement('a');a.href='terrain-atlas.png';a.target='_blank';const img=document.createElement('img');img.src='terrain-atlas.png';img.style.maxWidth='100%';img.style.imageRendering='pixelated';a.append(img);target.append(a);
 const p=document.createElement('p');p.textContent='Unity에서 조립한 실제 타일시트입니다. 캐릭터와 원본 재질은 승인본을 그대로 사용했습니다.';target.append(p);
}
document.querySelectorAll('[data-cave]').forEach(b=>b.onclick=()=>{const cave=level.caves[Number(b.dataset.cave)];window.__motion.actions.revive();window.__motion.teleport((cave.left+8)*32,cave.floor*32,true);state.camera=Math.max(0,(cave.left+8)*32-W*.45);state.cameraY=cave.floor*32-FOOT_SCREEN;state.cameraVX=state.cameraVY=0;draw();canvas.focus({preventScroll:true});});
document.querySelector('#monsterMode')?.addEventListener('change',e=>{monsterMode=e.target.value;if(monsterMode==='lineup'){setCamera(480,true);hero.x=1400;hero.y=surfaceAt(hero.x);}draw();});
document.querySelector('#monsterLabels')?.addEventListener('change',e=>{monsterLabels=e.target.checked;draw();});
document.querySelector('#monsterPalette')?.addEventListener('change',e=>{monsterPalette=e.target.value;document.querySelectorAll('#monsterShelf img').forEach((img,i)=>img.src=(monsterPalette==='original'?monsterVariants[i].originalImg:monsterVariants[i].img).src);draw();});
init();
