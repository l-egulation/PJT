/* Local map document editor: editable terrain and placement layers. */
'use strict';
const DATA=window.EXPANDED_MAP,{EditorModel,TYPES,clamp}=window.MapEditorCore;
const PLAN=DATA.plan,model=new window.TerrainEditor.WorkspaceModel(PLAN,DATA.markers,window.TERRAIN_RULES),$=s=>document.querySelector(s);
const map=$('#map'),mapCtx=map.getContext('2d'),scene=$('#scene'),sceneCtx=scene.getContext('2d'),scroll=$('#mapScroll');
const LEGACY_STORAGE='deviant-layout:A-expanded-v1-20261020';
const PREVIOUS_TERRAIN_STORAGE='deviant-map-editor-v2:A-24-spaces-v2-20261020';
const STORAGE='deviant-map-editor-v2:'+PLAN.id,images={};
const tileArt=new window.MapTileArt.TileArt(images,()=>document.createElement('canvas'));
let mini=null,hover=null,spaceHeld=false;
let ready=false,mode='edit',zoom=1,camera={x:0,y:0},drag=null,heroFoot=PLAN.spawn.slice(),lastFrame=0;

const groups={landmarks:['boss','midboss','elite','maintenance','shop','gold','structure'],movement:['trampoline','dropfloor','wallkick'],combat:['platform','enemy']};
const visible=m=>$('#filter').value==='all'||groups[$('#filter').value].includes(m.kind);
function status(text,error=false){$('#saveStatus').textContent=text;$('#saveStatus').classList.toggle('error',error);}
function persist(){try{localStorage.setItem(STORAGE,JSON.stringify(model.export()));status('이 브라우저에 자동 저장했어.');}catch(e){status('브라우저 자동 저장이 제한돼 있어. JSON 파일 저장을 사용해줘.',true);}}
function setCamera(x,y){camera.x=clamp(Math.round(x),0,PLAN.width-960);camera.y=clamp(Math.round(y),0,PLAN.height-540);$('#cameraX').value=camera.x;$('#cameraY').value=camera.y;if(ready)drawMap();}
function focus(x,y){setCamera(x-480,y-270);const s=map.width/PLAN.width;scroll.scrollLeft=Math.max(0,x*s-scroll.clientWidth/2);scroll.scrollTop=Math.max(0,y*s-scroll.clientHeight/2);}
function focusMarker(){const m=model.get();if(m){focus(m.x+m.w/2,m.y+m.h/2);heroFoot=null;}}
function list(){const target=$('#list');target.replaceChildren();for(const m of model.markers.filter(visible)){const b=document.createElement('button');b.textContent=m.name;b.style.borderLeftColor=TYPES[m.kind].color;b.classList.toggle('selected',model.selected===m.id);b.onclick=()=>{model.selected=m.id;refresh();focusMarker();};target.append(b);}$('#count').textContent=model.markers.filter(visible).length+' / '+model.markers.length+'개';}
function refresh(){
 $('#terrainStats').textContent='수정 지형 '+model.terrain.edits.size.toLocaleString()+'칸';
 const platforms=model.markers.filter(m=>m.kind==='platform');$('#platformStats').textContent='전투·이동 발판 '+platforms.length+'개 · 서로 다른 높이의 착지점';$('#movementStats').textContent='트램펄린 후보 '+model.markers.filter(m=>m.kind==='trampoline').length+'곳 · 통과 바닥 '+model.markers.filter(m=>m.kind==='dropfloor').length+'곳';
 const m=model.get();$('#properties').hidden=!m;$('#noSelection').hidden=!!m;$('#focus').disabled=!m;
 if(m){$('#name').value=m.name;$('#kind').value=m.kind;for(const[id,k]of [['posX','x'],['posY','y'],['sizeW','w'],['sizeH','h']])$('#'+id).value=m[k]/32;$('#note').value=m.note||'';$('#sizeH').disabled=['platform','dropfloor','trampoline'].includes(m.kind);$('#boostRow').hidden=m.kind!=='trampoline';$('#boost').value=m.boostTiles||24;}
 $('#undo').disabled=!model.undoStack.length;$('#redo').disabled=!model.redoStack.length;list();if(ready)drawMap();
}
function changed(){persist();refresh();}
function updateFields(){const m=model.get();if(!m)return;try{model.update(m.id,{name:$('#name').value,kind:$('#kind').value,x:Number($('#posX').value)*32,y:Number($('#posY').value)*32,w:Number($('#sizeW').value)*32,h:Number($('#sizeH').value)*32,note:$('#note').value,boostTiles:Number($('#boost').value)});changed();}catch(e){status(e.message,true);refresh();}}
for(const id of ['name','kind','posX','posY','sizeW','sizeH','note','boost'])$('#'+id).addEventListener('change',updateFields);
for(const select of [$('#kind'),$('#addKind')])for(const[k,v]of Object.entries(TYPES)){const option=document.createElement('option');option.value=k;option.textContent=v.label;select.append(option);}
$('#addKind').value='structure';
function setMode(value){mode=value;for(const [id,m] of [['editMode','edit'],['inspectMode','inspect'],['terrainMode','terrain']])$('#'+id).classList.toggle('active',mode===m);$('#terrainTools').hidden=mode!=='terrain';scene.style.cursor=mode==='inspect'?'grab':'crosshair';hover=null;drawMap();}
$('#editMode').onclick=()=>setMode('edit');$('#inspectMode').onclick=()=>setMode('inspect');$('#terrainMode').onclick=()=>setMode('terrain');
$('#add').onclick=()=>{model.add($('#addKind').value,camera.x+320,camera.y+192);setMode('edit');changed();};
$('#remove').onclick=()=>{model.remove();changed();};$('#undo').onclick=()=>{model.undo();changed();};$('#redo').onclick=()=>{model.redo();changed();};
$('#reset').onclick=()=>{if(!confirm('지형과 배치를 모두 기본 지형으로 복원할까요? 현재 수정본은 먼저 JSON으로 저장해 주세요.'))return;model.resetAll();changed();status('기본 지형의 지형과 배치로 복원했어. 되돌리기도 가능해.');};
$('#resetTerrain').onclick=()=>{if(!confirm('지형만 기본 지형으로 복원할까요? 배치는 유지됩니다.'))return;model.resetTerrain();changed();};
function save(){const blob=new Blob([JSON.stringify(model.export(),null,2)],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='chapter1-map-edited.json';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);persist();status('지형과 배치를 함께 저장했어. 다운로드된 JSON을 팀원에게 전달해줘.');}
$('#save').onclick=save;$('#load').onclick=()=>$('#file').click();
$('#file').onchange=async e=>{const f=e.target.files[0];if(!f)return;try{if(f.size>16*1024*1024)throw Error('16MB 이하의 맵 JSON을 선택해줘.');const result=model.import(JSON.parse(await f.text()));changed();status(result==='markers'?'이전 배치 JSON을 불러왔어. 현재 지형은 유지했어.':'지형과 배치를 함께 불러왔어. 되돌리기도 가능해.');}catch(err){status('불러오지 못했어: '+err.message,true);}finally{e.target.value='';}};
$('#focus').onclick=focusMarker;$('#filter').onchange=()=>{refresh();};$('#cameraX').max=PLAN.width-960;$('#cameraY').max=PLAN.height-540;
$('#cameraX').oninput=e=>{heroFoot=null;setCamera(+e.target.value,camera.y);};$('#cameraY').oninput=e=>{heroFoot=null;setCamera(camera.x,+e.target.value);};
function setZoom(z){const oldScale=map.width/PLAN.width,cx=(scroll.scrollLeft+scroll.clientWidth/2)/oldScale,cy=(scroll.scrollTop+scroll.clientHeight/2)/oldScale;zoom=clamp(z,1,4);map.width=Math.round(Math.min(1344,Math.max(320,scroll.clientWidth))*zoom);map.height=Math.round(map.width*PLAN.height/PLAN.width);map.style.width=map.width+'px';map.style.height=map.height+'px';$('#zoomLabel').textContent=Math.round(zoom*100)+'%';drawMap();const scale=map.width/PLAN.width;scroll.scrollLeft=Math.max(0,cx*scale-scroll.clientWidth/2);scroll.scrollTop=Math.max(0,cy*scale-scroll.clientHeight/2);}
$('#zoomOut').onclick=()=>setZoom(zoom/1.5);$('#zoomIn').onclick=()=>setZoom(zoom*1.5);$('#fit').onclick=()=>{setZoom(1);scroll.scrollLeft=scroll.scrollTop=0;};
document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();if(['name','kind','posX','posY','sizeW','sizeH','note','boost'].includes(document.activeElement?.id))updateFields();save();return;}if(/INPUT|TEXTAREA|SELECT/.test(document.activeElement?.tagName))return;if(e.ctrlKey||e.metaKey){if(e.key.toLowerCase()==='z'){e.preventDefault();e.shiftKey?model.redo():model.undo();changed();}else if(e.key.toLowerCase()==='y'){e.preventDefault();model.redo();changed();}}else if(e.key==='Escape'){pointerCancel();}else if(e.code==='Space'){e.preventDefault();spaceHeld=true;}else if(e.key==='1')setMode('edit');else if(e.key==='2')setMode('terrain');else if(e.key==='3')setMode('inspect');else if(e.key.toLowerCase()==='b'){setMode('terrain');$('#terrainTool').value='paint';}else if(e.key.toLowerCase()==='e'){setMode('terrain');$('#terrainTool').value='erase';}else if(e.key==='Delete'&&mode==='edit'&&model.get()){model.remove();changed();}});
for(const z of PLAN.zones){const b=document.createElement('button');b.textContent=z.number+'. '+z.name;b.onclick=()=>{heroFoot=z.foot.slice();setCamera(heroFoot[0]-432,heroFoot[1]-410);};$('#areas').append(b);}
for(const h of DATA.highlights){const b=document.createElement('button');b.textContent=h.name;b.onclick=()=>{heroFoot=h.hideHero?null:h.point.slice();setCamera(h.point[0]-432,h.point[1]-410);};$('#areas').append(b);}

function worldPoint(e,canvas){const r=canvas.getBoundingClientRect();if(canvas===map)return{x:(e.clientX-r.left)/r.width*PLAN.width,y:(e.clientY-r.top)/r.height*PLAN.height};return{x:camera.x+(e.clientX-r.left)*960/r.width,y:camera.y+(e.clientY-r.top)*540/r.height};}
function tolerance(canvas){return canvas===map?10/(map.getBoundingClientRect().width/PLAN.width):10*960/scene.getBoundingClientRect().width;}
function hitAt(p,canvas){if(!$('#showFeatures').checked)return undefined;const pad=canvas===map?Math.min(tolerance(canvas)*.45,64):6;return [...model.markers].reverse().find(m=>visible(m)&&p.x>=m.x-pad&&p.x<=m.x+m.w+pad&&p.y>=m.y-pad&&p.y<=m.y+m.h+pad);}
function pointerDown(e){if(!ready||drag)return;e.preventDefault?.();const canvas=e.currentTarget,p=worldPoint(e,canvas);canvas.setPointerCapture(e.pointerId);
 if(e.button===1||spaceHeld){drag=canvas===scene?{canvas,action:'pan',client:[e.clientX,e.clientY],camera:{...camera}}:{canvas,action:'look'};return;}
 if(mode==='terrain'){const cell=gridPoint(p),tool=$('#terrainTool').value;if(tool==='pick'&&e.button!==2){const v=model.terrain.value(...cell);if(v)$('#terrainMaterial').value=v;else $('#terrainTool').value='erase';status(v?'이 칸의 재질을 선택했어.':'빈 공간이야. 지우개를 선택했어.');return;}const erase=e.button===2||tool==='erase'||tool==='rectErase',value=erase?0:Number($('#terrainMaterial').value);model.begin();drag={canvas,action:tool.startsWith('rect')?'terrainRect':'terrainPaint',start:cell,last:cell,end:cell,value,size:Number($('#brushSize').value)};if(drag.action==='terrainPaint')model.terrain.paint([cell],value,drag.size);heroFoot=null;drawMap();return;}
let m=model.get();const t=tolerance(canvas);const resize=mode==='edit'&&$('#showFeatures').checked&&m&&visible(m)&&Math.abs(p.x-m.x-m.w)<t&&Math.abs(p.y-m.y-m.h)<t;
 if(mode==='edit'&&(resize||(m=hitAt(p,canvas)))){model.selected=m.id;model.begin();drag={canvas,action:resize?'resize':'move',origin:p,marker:{...m}};refresh();}
 else if(canvas===scene){drag={canvas,action:'pan',client:[e.clientX,e.clientY],camera:{...camera}};heroFoot=null;}
 else{drag={canvas,action:'look'};heroFoot=null;setCamera(p.x-480,p.y-270);}}
function pointerMove(e){const p=worldPoint(e,e.currentTarget);hover={canvas:e.currentTarget,cell:gridPoint(p)};$('#tilePosition').textContent='칸 '+hover.cell[0]+', '+hover.cell[1];if(!drag||drag.canvas!==e.currentTarget)return;if(drag.action==='terrainPaint'){const cell=gridPoint(p);if(cell[0]!==drag.last[0]||cell[1]!==drag.last[1]){model.terrain.line(drag.last,cell,drag.value,drag.size);drag.last=cell;drawMap();}return;}if(drag.action==='terrainRect'){drag.end=gridPoint(p);drawMap();return;}if(drag.action==='look'){setCamera(p.x-480,p.y-270);return;}if(drag.action==='pan'){const r=scene.getBoundingClientRect();setCamera(drag.camera.x-(e.clientX-drag.client[0])*960/r.width,drag.camera.y-(e.clientY-drag.client[1])*540/r.height);return;}const m=drag.marker,dx=p.x-drag.origin.x,dy=p.y-drag.origin.y;model.update(m.id,drag.action==='resize'?{w:m.w+dx,h:m.h+dy}:{x:m.x+dx,y:m.y+dy});drawMap();const current=model.get();for(const[id,k]of [['posX','x'],['posY','y'],['sizeW','w'],['sizeH','h']])$('#'+id).value=current[k]/32;}
function pointerUp(e){if(!drag||e.currentTarget&&e.currentTarget!==drag.canvas)return;const action=drag.action;if(action==='terrainRect')model.terrain.rectangle(drag.start,drag.end,drag.value);if(['move','resize','terrainRect','terrainPaint'].includes(action)){model.end();drag=null;changed();}else drag=null;}
function pointerCancel(){if(model.transaction){model.restore(model.transaction);model.transaction=null;}drag=null;refresh();}
for(const c of [map,scene]){c.onpointerdown=pointerDown;c.onpointermove=pointerMove;c.onpointerup=pointerUp;c.onpointercancel=pointerCancel;c.oncontextmenu=e=>e.preventDefault();c.onpointerleave=()=>{hover=null;};c.onlostpointercapture=()=>{if(drag?.canvas===c)pointerCancel();};}
document.addEventListener('keyup',e=>{if(e.code==='Space')spaceHeld=false;});window.addEventListener('blur',()=>{spaceHeld=false;if(drag)pointerCancel();});

function features(ctx,s,ox,oy,mini){
 if(!$('#showFeatures').checked)return;
 for(const m of model.markers){const x=(m.x-ox)*s,y=(m.y-oy)*s,w=m.w*s;if(x+w<0||y+64*s<0||x>ctx.canvas.width||y-11*s>ctx.canvas.height)continue;
  if(m.kind==='platform'||m.kind==='dropfloor')tileArt.drawPlatform(ctx,m,s,ox,oy);
 }
 for(const m of model.markers.filter(visible)){const x=(m.x-ox)*s,y=(m.y-oy)*s,w=m.w*s,h=m.h*s;if(x+w<-10||y+h<-30||x>ctx.canvas.width||y>ctx.canvas.height)continue;const selected=m.id===model.selected,color=TYPES[m.kind].color;
  ctx.save();ctx.lineWidth=selected?2:1;ctx.strokeStyle=selected?'#ffffff':color;
  if(['platform','dropfloor'].includes(m.kind)){if(m.kind==='dropfloor'){ctx.setLineDash([6,4]);ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+w,y);ctx.stroke();}if(selected)ctx.strokeRect(x,y,w,Math.max(h,3));}
  else{ctx.fillStyle=color+'1c';ctx.fillRect(x,y,w,Math.max(h,3));ctx.strokeRect(x,y,w,Math.max(h,3));}
  ctx.setLineDash([]);
  if(m.kind==='trampoline'){ctx.fillStyle=color;ctx.fillRect(x,y+h-3,w,3);ctx.beginPath();ctx.moveTo(x,y+h);for(let k=0;k<=6;k++)ctx.lineTo(x+k*w/6,y+(k%2?h*.2:h*.8));ctx.stroke();const ax=x+w/2,ay=y-(mini?10:36);ctx.beginPath();ctx.moveTo(ax,y);ctx.lineTo(ax,ay);ctx.lineTo(ax-4,ay+6);ctx.moveTo(ax,ay);ctx.lineTo(ax+4,ay+6);ctx.stroke();}
  if(m.kind==='wallkick'){ctx.setLineDash([4,4]);ctx.beginPath();ctx.moveTo(x+w/2,y+h);ctx.lineTo(x+w/2,y);ctx.stroke();ctx.setLineDash([]);}
  const labels=$('#showLabels').checked&&(!mini||selected||['boss','midboss','elite','maintenance','shop','gold'].includes(m.kind)||zoom>2);
  if(labels){const label=m.kind==='dropfloor'?'↓ + S  통과 바닥':m.name;ctx.font=(mini?'12':'14')+'px "Malgun Gothic",sans-serif';ctx.textBaseline='bottom';const lx=Math.max(2,x+3),ly=Math.max(18,y-5),tw=ctx.measureText(label).width;ctx.fillStyle='#14231de8';ctx.fillRect(lx-3,ly-16,tw+7,20);ctx.fillStyle=color;ctx.fillText(label,lx,ly);}
  if(selected&&mode==='edit'){ctx.fillStyle='#fff5b6';ctx.fillRect(x+w-5,y+h-5,10,10);ctx.strokeStyle='#263827';ctx.strokeRect(x+w-5,y+h-5,10,10);}
  ctx.restore();
 }
}
function drawMap(){if(!ready)return;const s=map.width/PLAN.width;mapCtx.imageSmoothingEnabled=false;mapCtx.clearRect(0,0,map.width,map.height);updateMini();mapCtx.drawImage(mini,0,0,map.width,map.height);features(mapCtx,s,0,0,true);if($('#showZones').checked){mapCtx.textAlign='center';mapCtx.textBaseline='middle';mapCtx.font='12px sans-serif';for(const z of PLAN.zones){const x=(z.x+z.w/2)*32*s,y=(z.y+z.h/2)*32*s;mapCtx.fillStyle='#12261e';mapCtx.strokeStyle='#ecda9e';mapCtx.beginPath();mapCtx.arc(x,y,10,0,7);mapCtx.fill();mapCtx.stroke();mapCtx.fillStyle='#f1e6c5';mapCtx.fillText(z.number,x,y);}}mapCtx.strokeStyle='#81e6df';mapCtx.lineWidth=1.5;mapCtx.strokeRect(camera.x*s,camera.y*s,960*s,540*s);terrainOverlay(mapCtx,s,0,0,map);}
function backgrounds(t){const ctx=sceneCtx;ctx.fillStyle='#202b2c';ctx.fillRect(0,0,960,540);for(const l of DATA.layers){let top=-Math.round(clamp(l.baseCrop+camera.y*l.vertical,l.minVertical,l.maxVertical));if(l.coverBottom)top=Math.max(top,Math.ceil(540+l.bottomMargin-l.height*l.contentBottom));const offset=camera.x*l.parallax+l.phase;ctx.globalAlpha=l.opacity||1;if(l.single){ctx.drawImage(images[l.id],Math.round(l.phase-camera.x*l.parallax),top,l.width,l.height);continue;}for(let n=Math.floor(offset/l.width)-1;n*l.width-offset<960;n++){const flip=!l.repeatUnmirrored&&((n%2)+2)%2===1;ctx.save();ctx.translate(Math.round(n*l.width-offset)+(flip?l.width:0),top);if(flip)ctx.scale(-1,1);ctx.drawImage(images[l.id],0,0,l.width,l.height);ctx.restore();}}ctx.globalAlpha=1;}
function frame(now){requestAnimationFrame(frame);if(!ready||now-lastFrame<30)return;lastFrame=now;const ctx=sceneCtx;ctx.imageSmoothingEnabled=false;backgrounds(now/1000);for(let y=Math.max(0,Math.floor(camera.y/32));y<=Math.min(model.terrain.rows-1,Math.ceil((camera.y+540)/32));y++)for(let x=Math.max(0,Math.floor(camera.x/32));x<=Math.min(model.terrain.cols-1,Math.ceil((camera.x+960)/32));x++)tileArt.drawGround(ctx,model.terrain,x,y,x*32-camera.x,y*32-camera.y);features(ctx,1,camera.x,camera.y,false);
 if($('#showHero').checked&&heroFoot){let time=(now/1000)%DATA.hero.seconds.reduce((a,b)=>a+b,0),index=0;while(index<DATA.hero.seconds.length-1&&time>=DATA.hero.seconds[index])time-=DATA.hero.seconds[index++];ctx.drawImage(images['hero'+index],Math.round(heroFoot[0]-DATA.hero.pivot[0]-camera.x),Math.round(heroFoot[1]-DATA.hero.pivot[1]-camera.y));}
 if($('#showGrid').checked){ctx.strokeStyle='#ffffff25';ctx.beginPath();for(let x=-camera.x%32;x<960;x+=32){ctx.moveTo(x+.5,0);ctx.lineTo(x+.5,540);}for(let y=-camera.y%32;y<540;y+=32){ctx.moveTo(0,y+.5);ctx.lineTo(960,y+.5);}ctx.stroke();}terrainOverlay(ctx,1,camera.x,camera.y,scene);
}
async function loadImage(name,url){const i=new Image();i.src=window.INLINE_ASSETS?.[url]||url;await i.decode();images[name]=i;}
$('#showFeatures').onchange=()=>drawMap();$('#showLabels').onchange=()=>drawMap();$('#showZones').onchange=()=>drawMap();
(async()=>{await Promise.all([loadImage('atlas','assets/Terrain32.png'),loadImage('solidPlatform','assets/SolidPlatform.png'),loadImage('dropPlatform','assets/DropPlatform.png'),...DATA.layers.map(l=>loadImage(l.id,'assets/'+l.id+'.png')),...DATA.hero.seconds.map((_,i)=>loadImage('hero'+i,'assets/hero'+i+'.png'))]);ready=true;$('#loading').hidden=true;try{$('#previousTerrainDraft').hidden=!localStorage.getItem(PREVIOUS_TERRAIN_STORAGE);$('#previousDraft').hidden=!localStorage.getItem(LEGACY_STORAGE);const draft=localStorage.getItem(STORAGE);if(draft){model.import(JSON.parse(draft),false);status('이전에 편집한 지형과 배치를 복원했어.');}else status('기본 지형이 표시돼 있어. 지형 편집 버튼을 눌러 바닥을 칠하거나 지워봐.');}catch(e){status('저장된 편집을 복원하지 못했어. 추천 배치를 표시해. JSON 파일을 사용할 수 있어.',true);}refresh();setZoom(1);setCamera(DATA.initialView[0]-432,DATA.initialView[1]-410);heroFoot=DATA.initialView.slice();drawMap();requestAnimationFrame(frame);window.__layoutEditor={model,focusMarker,setCamera,refresh,setMode,drawMap,drawFrame:frame,get camera(){return{...camera};}};})().catch(e=>{$('#loading').textContent='에셋을 불러오지 못했어: '+e.message;$('#loading').classList.add('error');console.error(e);});

$('#previousDraft').onclick=()=>{try{const raw=localStorage.getItem(LEGACY_STORAGE);if(!raw)throw Error('이전 저장본이 없어.');const old=JSON.parse(raw);if(old.mapId!=='A-expanded-v1-20261020')throw Error('이전 저장본 형식을 확인해줘.');const url=URL.createObjectURL(new Blob([raw],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='deviant-previous-8-spaces-placements.json';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);status('이전 8공간안의 편집을 백업했어. 새 지도에는 새 배치를 사용해.');}catch(e){status(e.message,true);}};


function gridPoint(p){return [clamp(Math.floor(p.x/32),0,model.terrain.cols-1),clamp(Math.floor(p.y/32),0,model.terrain.rows-1)];}
function updateMini(){
 const terrain=model.terrain;
 if(!mini){mini=document.createElement('canvas');mini.width=terrain.cols*4;mini.height=terrain.rows*4;for(let k=0;k<terrain.total;k++)terrain.dirty.add(k);}
 const ctx=mini.getContext('2d');ctx.imageSmoothingEnabled=false;
 // Include foliage above dirty cells, then redraw in the same top-to-bottom
 // order as the detailed view. Erasing a curved edge must not leave a fringe.
 const redraw=new Set(terrain.dirty);for(const k of terrain.dirty){if(k>=terrain.cols)redraw.add(k-terrain.cols);if(k+terrain.cols<terrain.total)redraw.add(k+terrain.cols);}
 for(const k of redraw){const x=k%terrain.cols,y=Math.floor(k/terrain.cols);ctx.fillStyle='#48554b';ctx.fillRect(x*4,y*4,4,4);}
 for(const k of [...redraw].sort((a,b)=>a-b)){const x=k%terrain.cols,y=Math.floor(k/terrain.cols);tileArt.drawGround(ctx,terrain,x,y,x*4,y*4,1/8);}
 terrain.dirty.clear();
}
function terrainOverlay(ctx,s,ox,oy,canvas){
 if(mode!=='terrain')return;let a,b,erase;
 if(drag?.action==='terrainRect'){a=drag.start;b=drag.end;erase=drag.value===0;}
 else if(hover?.canvas===canvas){const r=Math.floor(Number($('#brushSize').value)/2);a=[hover.cell[0]-r,hover.cell[1]-r];b=[hover.cell[0]+r,hover.cell[1]+r];erase=['erase','rectErase'].includes($('#terrainTool').value);}
 else return;
 const x=(Math.min(a[0],b[0])*32-ox)*s,y=(Math.min(a[1],b[1])*32-oy)*s,w=(Math.abs(b[0]-a[0])+1)*32*s,h=(Math.abs(b[1]-a[1])+1)*32*s;
 ctx.save();ctx.fillStyle=erase?'#ff90733a':'#c9e49b38';ctx.strokeStyle=erase?'#ffad96':'#e3f4b6';ctx.lineWidth=1.5;ctx.fillRect(x,y,w,h);ctx.strokeRect(x,y,w,h);ctx.restore();
}
$('#terrainTool').onchange=()=>{hover=null;};
$('#exportPng').onclick=()=>{drawMap();map.toBlob(blob=>{if(!blob){status('이미지를 저장하지 못했어. JSON 저장을 사용해줘.',true);return;}const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='chapter1-map-overview.png';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);status('현재 지도 PNG 저장을 요청했어. 편집 데이터는 JSON으로 따로 저장해줘.');});};

$('#previousTerrainDraft').onclick=()=>{const raw=localStorage.getItem(PREVIOUS_TERRAIN_STORAGE);if(!raw)return;const url=URL.createObjectURL(new Blob([raw],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='chapter1-previous-terrain-draft.json';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);status('이전 지형의 편집본을 저장했어. 이전 지형용 편집기에서 열 수 있어. 새 지형 저장본과는 분리돼.');};
