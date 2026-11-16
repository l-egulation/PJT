(() => {
  'use strict';
  const cfg = JSON.parse(document.getElementById('village-config').textContent);
  const canvas = document.getElementById('game'), ctx = canvas.getContext('2d', {alpha:false});
  const stage = document.getElementById('stage'), mode = document.getElementById('mode');
  const assetSet = document.getElementById('asset-set');
  const backgroundVariant = document.getElementById('background-variant');
  const propSampling = document.getElementById('prop-sampling');
  const nativeProps = new Map();
  const buildings = new Set(['palette_atelier','supply_office','reclaim_workshop']);
  const filter = document.getElementById('filter'), display = document.getElementById('display');
  const panel = document.getElementById('station-panel'), locationLabel = document.getElementById('location');
  const uiControls=Array.from(document.querySelectorAll('button,select'));
  uiControls.forEach(c=>c.disabled=true);
  const W = 960, H = 540, WORLD = cfg.layout?.worldWidthPixels||2880, FLOOR = 444;
  const images = {}, caches = new Map(), keys = new Set();
  const player = {x:576, y:0, vx:0, vy:0, face:1, grounded:true};
  let camera = 144, last = 0, anim = 0, ready = false, closeLabel = '', playing = true;
  const names = cfg.names;
  const activeAssets = () => assetSet.value==='previous' ? (cfg.previousAssets||cfg.assets) : cfg.assets;
  const paintable = new Set(['palette_atelier','supply_office','reclaim_workshop','palette_bench','supply_counter','reclaim_bench']);
  const objects = [
    {key:'house', x:192, z:1}, {key:'house',x:2336,z:1,width:144},
    {key:'low_wall',x:2512,z:1},{key:'low_wall',x:2608,z:1},
    {key:'palette_atelier',x:864,z:2},{key:'supply_office',x:1408,z:2},{key:'reclaim_workshop',x:1984,z:2},
    {key:'record_desk',x:352,z:3},{key:'return_well',x:480,z:3},
    {key:'palette_bench',x:1056,z:3},{key:'supply_counter',x:1600,z:3},{key:'reclaim_bench',x:2176,z:3},
    {key:'departure_table',x:2464,z:3},{key:'handcart',x:2576,z:3},{key:'departure_sign',x:2720,z:3}
  ].sort((a,b)=>a.z-b.z);
  if(cfg.layout){
    const originalObjects=[...objects];objects.length=0;
    for(const row of cfg.layout.objects){const obj={...originalObjects[row.sourceId],x:row.x*32,y:row.y*32};objects.push(obj);}
  }
  const stations = [
    {key:'departure_sign',x:2720,description:'1스테이지로 출발하는 길입니다.'},
    {key:'record_desk',x:352,description:'기록과 이야기를 확인하는 자리입니다.'},
    {key:'return_well',x:480,description:'귀환 위치와 주변 시설을 안내하는 우물입니다.'},
    {key:'palette_bench',x:1056,description:'팔레트 작업대의 크기와 주변 공방과의 연결을 확인하세요.'},
    {key:'supply_counter',x:1600,description:'출발 보급을 받는 접수대입니다.'},
    {key:'reclaim_bench',x:2176,description:'잉크 회수 작업대입니다.'},
    {key:'departure_table',x:2464,description:'장비를 준비한 뒤 이정표가 가리키는 오른쪽 외곽길로 이어지는 구성입니다. 이 미리보기에서는 다음 맵으로 전환하지 않습니다.'}
  ];
  for(const station of stations)station.x=objects.find(o=>o.key===station.key).x;
  const coloredAt = x => mode.value==='color' || (mode.value==='partial' && (['palette_atelier','supply_office','reclaim_workshop'].some(k=>Math.abs(x-objects.find(o=>o.key===k).x)<208)));
  const stateFor = key => mode.value==='color' || (mode.value==='partial'&&paintable.has(key)) ? 0 : 1;
  const dimensions = (key,state=0) => {
    const a=activeAssets()[key],b=a.boxes[state];
    return {b,s:a.scale,w:Math.round((b[2]-b[0])*a.scale),h:Math.round((b[3]-b[1])*a.scale)};
  };
  function drawAsset(c,key,state,x){
    const a=activeAssets()[key],d=dimensions(key,state),b=d.b;
    if(assetSet.value!=='previous' && !buildings.has(key) && propSampling.value==='native'){
      const cacheKey=key+':'+state;
      if(!nativeProps.has(cacheKey)){
        const sprite=document.createElement('canvas');sprite.width=d.w;sprite.height=d.h;
        const sc=sprite.getContext('2d');
        sc.imageSmoothingEnabled=true;sc.imageSmoothingQuality='high';
        sc.drawImage(images[a.imageKeys?a.imageKeys[state]:a.imageKey],b[0],b[1],b[2]-b[0],b[3]-b[1],0,0,d.w,d.h);
        nativeProps.set(cacheKey,sprite);
      }
      c.save();c.imageSmoothingEnabled=false;
      c.drawImage(nativeProps.get(cacheKey),Math.round(x-d.w/2),FLOOR-d.h);
      c.restore();return;
    }
    c.drawImage(images[a.imageKeys?a.imageKeys[state]:a.imageKey],b[0],b[1],b[2]-b[0],b[3]-b[1],Math.round(x-d.w/2),FLOOR-d.h,d.w,d.h);
  }
  function tiled(c,img,left,top,right,bottom){
    c.save();c.beginPath();c.rect(left,top,right-left,bottom-top);c.clip();
    for(let y=top;y<bottom;y+=img.height)for(let x=left;x<right;x+=img.width)c.drawImage(img,x,y);
    c.restore();
  }
  function bake(){
    const cacheKey=assetSet.value+':'+mode.value+':'+filter.value+':'+propSampling.value+':'+backgroundVariant.value;
    if(caches.has(cacheKey))return caches.get(cacheKey);
    const layer=document.createElement('canvas');layer.width=WORLD;layer.height=H;
    const c=layer.getContext('2d',{alpha:true});c.imageSmoothingEnabled=filter.value==='smooth';
    if(backgroundVariant.value!=='layered'){
    c.fillStyle=mode.value==='color'?'#7fbee0':'#f8efdf';c.fillRect(0,0,WORLD,H);
    c.save();
    const variant=cfg.backgrounds[backgroundVariant.value];
    if(variant){
      const b=variant.boxes[mode.value==='color'?0:1],bg=images[variant.imageKey];
      const h=FLOOR,w=Math.round((b[2]-b[0])*h/(b[3]-b[1]));
      c.globalAlpha=mode.value==='color'?.68:.42;
      c.imageSmoothingEnabled=true;c.imageSmoothingQuality='high';
      for(let x=0,i=0;x<WORLD;x+=w,i++){
        c.save();c.translate(x+(i%2?w:0),0);if(i%2)c.scale(-1,1);
        c.drawImage(bg,b[0],b[1],b[2]-b[0],b[3]-b[1],0,0,w,h);c.restore();
      }
    }else{
      const bg=images[mode.value==='color'?'backgroundColor':'backgroundSketch'];
      c.globalAlpha=mode.value==='color'?.68:.35;
      for(let x=0;x<WORLD;x+=bg.width)c.drawImage(bg,x,FLOOR-bg.height);
    }
    c.restore();
    }
    tiled(c,images[mode.value==='color'?'soilColor':'soilSketch'],0,464,WORLD,H);
    if(mode.value==='partial')for(const [l,r] of objects.filter(o=>buildings.has(o.key)).map(o=>[o.x-160,o.x+256]))tiled(c,images.soilColor,l,464,r,H);
    for(let x=0;x<WORLD;x+=32)c.drawImage(images[coloredAt(x)?'capColor':'capSketch'],x-5,438);
    for(const obj of objects){
      c.save();c.translate(0,-(obj.y||0));
      if(obj.key==='house'){
        const im=images[mode.value==='color'?'houseColor':'houseSketch'],w=obj.width||256,h=Math.round(im.height*w/im.width);
        c.drawImage(im,Math.round(obj.x-w/2),FLOOR-h,w,h);
      }else drawAsset(c,obj.key,stateFor(obj.key),obj.x);
      c.restore();
    }
    if(caches.size>=4)caches.delete(caches.keys().next().value);
    caches.set(cacheKey,layer);return layer;
  }
  function closest(){
    return stations.reduce((a,b)=>Math.abs(a.x-player.x)<Math.abs(b.x-player.x)?a:b);
  }
  function jump(){if(flowBusy()||flow.scene!=='village')return;if(!ready||!panel.hidden)return;if(player.grounded){player.vy=570;player.grounded=false;}}
  function closePanel(){action.hidden=true;panel.hidden=true;keys.clear();canvas.focus({preventScroll:true});}
  function interact(){
    if(flowBusy())return;
    if(flow.scene==='boss'){bossInteract();return;}
    if(flow.scene!=='village')return;
    action.hidden=true;
    const sign=objects.find(o=>o.key==='departure_sign');
    if(Math.abs(player.x-sign.x)<75){openFlowPanel('마을 밖으로','오른쪽 길을 따라 1스테이지로 출발합니다.','1스테이지로 출발',beginDeparture);return;}
    if(!panel.hidden){closePanel();return;}
    const s=closest();if(Math.abs(s.x-player.x)>75)return;
    document.getElementById('station-title').textContent=names[s.key];
    document.getElementById('station-description').textContent=s.description;
    panel.hidden=false;keys.clear();document.getElementById('station-close').focus({preventScroll:true});
  }
  const opaqueLayers=new Map();
  function opaqueLayer(key,s){
    const id=key+':'+s;if(opaqueLayers.has(id))return opaqueLayers.get(id);
    const im=images[key],c=document.createElement('canvas');c.width=im.width;c.height=im.height;
    const x=c.getContext('2d');x.drawImage(im,0,0);const pixels=x.getImageData(0,0,c.width,c.height),d=pixels.data;
    const base=[244,240,229];
    for(let i=0;i<d.length;i+=4){
      d[i+3]=Math.max(0,Math.min(255,(d[i+3]-128)*8));
      if(s&&(key==='parallaxmarket'||key==='parallaxtrees')&&i/4<c.width*540)d[i+3]=0;
      if(s){const lum=.2126*d[i]+.7152*d[i+1]+.0722*d[i+2];const shade=Math.max(0,245-lum)*.53;for(let j=0;j<3;j++)d[i+j]=base[j]-shade;}
      else {
        const lum=.2126*d[i]+.7152*d[i+1]+.0722*d[i+2];
        for(let j=0;j<3;j++)d[i+j]=(lum+(d[i+j]-lum)*.48)*.65+base[j]*.35;
      }
    }
    x.putImageData(pixels,0,0);opaqueLayers.set(id,c);return c;
  }
  function drawParallax(cam){
    const color=mode.value==='color',s=color?0:1,t=performance.now()/1000;
    ctx.save();ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
    ctx.fillStyle=color?'#b8d4df':'#f4f0e5';ctx.fillRect(0,0,W,H);
    ctx.globalAlpha=color?.45:0;
    ctx.drawImage(images.parallaxsky,0,s*512,1536,512,0,0,W,H);
    ctx.globalAlpha=color?.7:.36;
    const drift=t*5,period=1100;
    for(let i=-1;i<3;i++){
      const x=((drift-cam*.08)%period)+i*period;
      ctx.drawImage(images.parallaxdistant,s*768,570,768,400,x,12,600,160);
    }
    ctx.globalAlpha=1;
    ctx.drawImage(opaqueLayer('parallaxdistant',s),s*768+170,40,450,500,295-cam*.16,35,280,311);
    for(let i=-1;i<3;i++){
      ctx.save();ctx.translate(i*1400-cam*.29+(i%2?1400:0),55);if(i%2)ctx.scale(-1,1);
      ctx.drawImage(opaqueLayer('parallaxtrees',s),0,s?478:0,1536,510,0,0,1400,389);ctx.restore();
    }
    const width=Math.round(1560*WORLD/2880),height=412,offset=cam*.48;
    for(let i=-1;i<3;i++){
      ctx.save();ctx.translate(i*width-offset+(i%2?width:0),444-height);if(i%2)ctx.scale(-1,1);
      ctx.drawImage(opaqueLayer('parallaxmarket',s),0,s?477:0,1536,510,0,0,width,height);ctx.restore();
    }
    ctx.restore();
    canvas.dataset.parallax=JSON.stringify({camera:cam,market:cam*.48,tower:cam*.16,cloud:cam*.08,drift});
  }
  function draw(){
    ctx.imageSmoothingEnabled=false;
    const source=bake(),cam=Math.round(camera);
    if(backgroundVariant.value==='layered')drawParallax(cam);
    ctx.drawImage(source,cam,0,W,H,0,0,W,H);
    const moving=Math.abs(player.vx)>8, frame=moving||!player.grounded?images['run'+(Math.floor(anim*14)%12+1)]:images.idle;
    ctx.save();ctx.translate(Math.round(player.x-cam),FLOOR-Math.round(player.y));ctx.scale(player.face,1);
    ctx.drawImage(frame,moving||!player.grounded?-96:-64,moving||!player.grounded?-147:-112);ctx.restore();
    const near=closest(),distance=Math.abs(near.x-player.x);
    if(distance<75&&player.grounded&&panel.hidden){
      const d=dimensions(near.key,stateFor(near.key));const x=Math.round(near.x-cam),y=FLOOR-d.h-35;
      ctx.fillStyle='#f8efdf';ctx.fillRect(x-10,y,20,22);ctx.strokeStyle='#685b47';ctx.lineWidth=1;ctx.strokeRect(x-10.5,y-.5,21,23);
      ctx.fillStyle='#423b2e';ctx.font='16px sans-serif';ctx.textAlign='center';ctx.fillText('F',x,y+17);
      ctx.font='13px "Malgun Gothic",sans-serif';ctx.fillStyle='#554b3d';ctx.fillText(names[near.key],x,y-8);
    }
    const loc=player.x<650?'우물 광장':player.x<1200?'염색공방':player.x<1760?'보급소':player.x<2260?'회수공방':player.x<2640?'출발 준비 구역':'마을 외곽길';
    if(closeLabel!==loc){locationLabel.textContent=loc;closeLabel=loc;}
    canvas.dataset.playerX=player.x.toFixed(2);canvas.dataset.playerY=player.y.toFixed(2);canvas.dataset.cameraX=camera.toFixed(2);
    drawFlow();
    canvas.dataset.artSet=assetSet.value;canvas.dataset.worldWidth=String(WORLD);canvas.dataset.objectCount=String(objects.length);
  }
  function tick(time){
    const dt=Math.min((time-(last||time))/1000,.04);last=time;
    if(ready)updateFlow(dt);
    if(ready&&playing&&panel.hidden&&!editing&&flow.scene==='village'&&!flowBusy()){
      const input=(keys.has('right')?1:0)-(keys.has('left')?1:0),target=input*224;
      const step=(input?1800:2200)*dt;
      player.vx+=Math.max(-step,Math.min(step,target-player.vx));
      if(input)player.face=input;
      player.x=Math.max(24,Math.min(WORLD-24,player.x+player.vx*dt));
      if(!player.grounded){player.vy-=1650*dt;player.y+=player.vy*dt;if(player.y<=0){player.y=0;player.vy=0;player.grounded=true;}}
      if(Math.abs(player.vx)>8)anim+=dt;
      const targetCam=Math.max(0,Math.min(WORLD-W,player.x-432));
      camera+=(targetCam-camera)*(1-Math.exp(-12.5*dt));
    }
    if(ready)draw();requestAnimationFrame(tick);
  }

  let editing=false,selected=null,drag=null;
  const flow={scene:'village',phase:'idle',elapsed:0,target:'stage',reward:false};
  const flowBar=document.createElement('div');flowBar.className='walk-controls';
  flowBar.innerHTML='<button id="go-sign">이정표로 이동</button><button id="boss-review">보스 처치 후 보기</button><button id="back-village">마을로 돌아오기</button><span id="flow-label">출발 연출: 이정표 앞 F → 출발 버튼</span>';
  stage.parentNode.insertBefore(flowBar,stage);
  const action=document.createElement('button');action.id='station-action';action.hidden=true;panel.insertBefore(action,document.getElementById('station-close'));
  function flowBusy(){return flow.phase!=='idle';}
  function flowLabel(t){document.getElementById('flow-label').textContent=t;}
  function openFlowPanel(title,text,button,fn){document.getElementById('station-title').textContent=title;document.getElementById('station-description').textContent=text;action.textContent=button;action.hidden=false;action.onclick=fn;panel.hidden=false;keys.clear();player.vx=0;action.focus();}
  function fadeTo(scene){closePanel();flow.target=scene;flow.phase='out';flow.elapsed=0;keys.clear();}
  function beginDeparture(){closePanel();editing=false;el('edit-layout').checked=false;el('edit-tools').hidden=true;flow.phase='walking';player.vx=224;player.face=1;player.y=0;player.grounded=true;keys.clear();flowLabel('오른쪽 길로 출발 중…');}
  function updateFlow(dt){
    if(!playing)return;
    if(flow.phase==='walking'){player.x+=224*dt;anim+=dt;player.vx=224;if(player.x-Math.round(camera)>W+128){canvas.dataset.departedFully='true';fadeTo('stage');}}
    else if(flow.phase==='out'){flow.elapsed+=dt;if(flow.elapsed>=.55){flow.scene=flow.target;flow.phase='black';flow.elapsed=0;player.vx=0;}}
    else if(flow.phase==='black'){flow.elapsed+=dt;if(flow.elapsed>=.15){flow.phase='in';flow.elapsed=0;}}
    else if(flow.phase==='in'){flow.elapsed+=dt;if(flow.elapsed>=.55){flow.phase='idle';flow.elapsed=0;flowLabel(flow.scene==='stage'?'1스테이지 입장 · Unity 캡처 기반 연출 미리보기':flow.scene==='boss'?'보스 처치 후 · 제단/출구를 클릭하거나 F로 확인':flow.scene==='next'?'다음 챕터 전환 연출 확인':'마을 · 이정표 앞에서 F');}}
    canvas.dataset.scene=flow.scene;canvas.dataset.flowPhase=flow.phase;
  }
  function drawFlow(){
    if(flow.scene!=='village'){
      ctx.fillStyle='#171713';ctx.fillRect(0,0,W,H);
      if(flow.scene==='stage'||flow.scene==='boss')ctx.drawImage(images[flow.scene==='stage'?'stageReview':'bossReview'],0,0,W,H);
      else{ctx.fillStyle='#f5efdf';ctx.font='24px sans-serif';ctx.textAlign='center';ctx.fillText('다음 챕터',W/2,H/2);}
      ctx.fillStyle='#151610d9';ctx.fillRect(12,12,560,44);ctx.fillStyle='#f8efdf';ctx.font='14px sans-serif';ctx.textAlign='left';ctx.fillText(flow.scene==='stage'?'1스테이지 입장 · 전투는 상단 보스 미리보기로 건너뛰기':flow.scene==='boss'?(flow.reward?'보상 선택 완료 · 오른쪽 출구 F / 클릭':'보스 처치 후 · 보상 제단 F / 클릭'):'다음 챕터 연결 연출 · 실제 맵 미연결',24,40);
      if(flow.scene==='boss'&&flow.reward){ctx.fillStyle='#f5efdf';ctx.font='14px sans-serif';ctx.fillText('보상 확인 완료',660,398);}
    }
    const alpha=flow.phase==='out'?Math.min(1,flow.elapsed/.55):flow.phase==='black'?1:flow.phase==='in'?1-Math.min(1,flow.elapsed/.55):0;
    if(alpha){ctx.save();ctx.globalAlpha=alpha;ctx.fillStyle='#000';ctx.fillRect(0,0,W,H);ctx.restore();}
  }
  function bossInteract(exit=false){if(exit||flow.reward){openFlowPanel('다음 챕터로','석조 출구를 통해 다음 챕터로 이동하는 연출입니다.','다음 챕터로 이동',()=>fadeTo('next'));}
    else openFlowPanel('잉크 보상 제단','보스 처치 후 잉크를 선택하는 자리입니다. 현재는 배치와 선택 완료 연출을 확인합니다.','보상 선택 완료 미리보기',()=>{flow.reward=true;closePanel();flowLabel('보상 확인 완료 · 오른쪽 출구를 확인하세요.');});}
  document.getElementById('go-sign').onclick=()=>{if(flowBusy()||flow.scene!=='village')return;teleport(objects.find(o=>o.key==='departure_sign').x);};
  function showBossReview(){
    if(!ready)return;
    closePanel();keys.clear();player.vx=0;editing=false;
    el('edit-layout').checked=false;el('edit-tools').hidden=true;
    Object.assign(flow,{scene:'boss',target:'boss',phase:'idle',elapsed:0,reward:false});
    canvas.dataset.scene='boss';canvas.dataset.flowPhase='idle';
    flowLabel('보스 처치 후 · 제단/출구를 클릭하거나 F로 확인');
    draw();stage.scrollIntoView({block:'center'});canvas.focus({preventScroll:true});
  }
  document.getElementById('boss-review').onclick=showBossReview;
  document.getElementById('back-village').onclick=()=>{if(flowBusy())return;player.x=576;camera=144;player.y=0;canvas.dataset.departedFully='false';fadeTo('village');};
  canvas.addEventListener('click',e=>{if(flow.scene!=='boss'||flowBusy())return;const p=point(e);const x=p.x-Math.round(camera);if(x>650)bossInteract(x>800);});

  const defaults=objects.map(o=>({...o}));
  const box=document.createElement('section');box.style.cssText='padding:12px;border:1px solid #777;margin:12px 0';
  box.innerHTML='<label><input id="edit-layout" type="checkbox"> 구조물 배치 모드</label> · 1유닛 = 32px · 원점: 맵 왼쪽 바닥 · 좌표: 구조물 밑면 중앙 <div id="edit-tools" hidden><p>구조물을 클릭·드래그하세요. 화면 위치: <input id="edit-camera" type="range" min="0" max="60" step="0.25" value="4.5"> <span id="edit-name"></span></p><label>X <input id="edit-x" type="number" step="0.25"></label> <label>Y <input id="edit-y" type="number" step="0.25"></label> <label>격자 <select id="edit-snap"><option value="0.25">0.25유닛</option><option value="0.5">0.5유닛</option><option value="1">1유닛</option><option value="0">자유</option></select></label> <button id="edit-save">배치 저장</button> <button id="edit-load">불러오기</button> <button id="edit-export">JSON 내보내기</button> <button id="edit-reset">초기 배치</button><p id="edit-status"></p></div>';
  stage.parentNode.insertBefore(box,stage);
  const el=id=>document.getElementById(id);
  el('edit-camera').max=(WORLD-W)/32;
  document.querySelectorAll('[data-go]').forEach(b=>{const map={480:'return_well',1056:'palette_bench',1600:'supply_counter',2176:'reclaim_bench',2464:'departure_table'};const obj=objects.find(o=>o.key===map[b.dataset.go]);if(obj)b.dataset.go=obj.x;});
  function refresh(){caches.clear();for(const station of stations){const obj=objects.find(o=>o.key===station.key);if(obj)station.x=obj.x;}if(selected){el('edit-name').textContent=names[selected.key]||'기존 집';el('edit-x').value=(selected.x/32).toFixed(3);el('edit-y').value=((selected.y||0)/32).toFixed(3);}if(ready)draw();}
  function snap(v){const step=Number(el('edit-snap').value)*32;return step?Math.round(v/step)*step:Math.round(v);}
  el('edit-layout').onchange=e=>{editing=e.target.checked;keys.clear();player.vx=0;el('edit-tools').hidden=!editing;canvas.style.cursor=editing?'grab':'';};
  el('edit-camera').oninput=e=>{camera=Number(e.target.value)*32;refresh();};
  for(const id of ['edit-x','edit-y'])el(id).onchange=()=>{if(!selected)return;const x=Number(el('edit-x').value),y=Number(el('edit-y').value);if(!Number.isFinite(x)||!Number.isFinite(y))return;selected.x=x*32;selected.y=y*32;refresh();};
  function point(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*960/r.width+Math.round(camera),y:(e.clientY-r.top)*540/r.height};}
  canvas.addEventListener('pointerdown',e=>{if(!editing)return;const p=point(e);selected=[...objects].reverse().find(o=>{const d=o.key==='house'?{w:o.width||256,h:Math.round((o.width||256)*292/256)}:dimensions(o.key,stateFor(o.key));return p.x>=o.x-d.w/2&&p.x<=o.x+d.w/2&&p.y<=444-(o.y||0)&&p.y>=444-(o.y||0)-d.h;});if(selected){drag={x:p.x-selected.x,y:p.y+(selected.y||0)};canvas.setPointerCapture(e.pointerId);refresh();}});
  canvas.addEventListener('pointermove',e=>{if(!editing||!drag||!selected)return;const p=point(e);selected.x=snap(p.x-drag.x);selected.y=snap(drag.y-p.y);refresh();});
  for(const name of ['pointerup','pointercancel'])canvas.addEventListener(name,()=>drag=null);
  function data(){return {version:1,ppu:32,worldWidthUnits:WORLD/32,origin:'map-left-floor',anchor:'bottom-center',objects:objects.map((o,i)=>({id:i,key:o.key,x:o.x/32,y:(o.y||0)/32}))};}
  function apply(d){if(d.version!==1||d.ppu!==32||!Array.isArray(d.objects)||d.objects.length!==objects.length)throw Error('배치 파일 형식이 다릅니다');for(let i=0;i<objects.length;i++){const r=d.objects[i];if(r.key!==objects[i].key||!Number.isFinite(r.x)||!Number.isFinite(r.y))throw Error('좌표 형식이 다릅니다');}d.objects.forEach((r,i)=>{objects[i].x=r.x*32;objects[i].y=r.y*32;});refresh();}
  el('edit-save').onclick=()=>{localStorage.setItem('village-layout-units-v2',JSON.stringify(data()));el('edit-status').textContent='이 브라우저에 배치를 저장했습니다.';};
  el('edit-load').onclick=()=>{try{apply(JSON.parse(localStorage.getItem('village-layout-units-v2')));el('edit-status').textContent='저장한 배치를 불러왔습니다.';}catch{el('edit-status').textContent='불러올 수 있는 저장 배치가 없습니다.';}};
  el('edit-reset').onclick=()=>{objects.forEach((o,i)=>Object.assign(o,defaults[i]));refresh();};
  el('edit-export').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(data(),null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='village-layout-units.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};

  function resize(){
    const dpr=window.devicePixelRatio||1,available=stage.clientWidth*dpr;
    let physicalScale=display.value==='integer'?Math.max(1,Math.floor(available/W)):available/W;
    if(physicalScale*W>available)physicalScale=available/W;
    if(document.fullscreenElement===stage)physicalScale=Math.min(physicalScale,display.value==='integer'?Math.max(1,Math.floor(window.innerHeight*dpr/H)):window.innerHeight*dpr/H);
    else physicalScale=Math.min(physicalScale,2);
    const cssW=W*physicalScale/dpr,cssH=H*physicalScale/dpr;
    canvas.style.width=cssW+'px';canvas.style.height=cssH+'px';
    document.getElementById('pixel-status').textContent='960 × 540 · '+physicalScale.toFixed(2).replace(/\.00$/,'')+'× '+(Number.isInteger(physicalScale)?'정수배':'축소/소수배')+' · '+(filter.value==='point'?'Point':'부드럽게 비교');
  }
  function teleport(x){if(flowBusy()||flow.scene!=='village')return;player.x=x;player.vx=0;player.y=0;player.vy=0;player.grounded=true;camera=Math.max(0,Math.min(WORLD-W,x-432));closePanel();draw();}
  window.addEventListener('keydown',e=>{
    if(flowBusy())return;
    if(flow.scene!=='village'&&e.code!=='KeyF'&&e.code!=='Escape')return;
    if(editing||['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;
    if(e.ctrlKey||e.metaKey||e.altKey||e.target.tagName==='SELECT'||!ready)return;
    if(['ArrowLeft','KeyA','ArrowRight','KeyD','Space','KeyF','Escape'].includes(e.code))e.preventDefault();
    if(e.code==='Escape'){closePanel();return;}
    if(!panel.hidden){if(e.code==='KeyF'&&!e.repeat)closePanel();return;}
    if(['ArrowLeft','KeyA'].includes(e.code))keys.add('left');
    if(['ArrowRight','KeyD'].includes(e.code))keys.add('right');
    if(e.code==='Space'&&!e.repeat)jump();if(e.code==='KeyF'&&!e.repeat)interact();
  });
  window.addEventListener('keyup',e=>{if(['ArrowLeft','KeyA'].includes(e.code))keys.delete('left');if(['ArrowRight','KeyD'].includes(e.code))keys.delete('right');});
  window.addEventListener('blur',()=>{keys.clear();});
  document.addEventListener('visibilitychange',()=>{playing=!document.hidden;keys.clear();last=0;});
  canvas.addEventListener('pointerdown',()=>canvas.focus({preventScroll:true}));
  for(const dir of ['left','right']){
    const b=document.getElementById(dir);
    b.addEventListener('pointerdown',e=>{e.preventDefault();b.setPointerCapture(e.pointerId);keys.add(dir);canvas.focus({preventScroll:true});});
    for(const evt of ['pointerup','pointercancel','lostpointercapture'])b.addEventListener(evt,()=>keys.delete(dir));
  }
  document.getElementById('jump').addEventListener('click',()=>{jump();canvas.focus({preventScroll:true});});
  document.getElementById('station-close').addEventListener('click',closePanel);
  document.querySelectorAll('[data-go]').forEach(b=>b.addEventListener('click',()=>teleport(Number(b.dataset.go))));
  for(const control of [mode,filter,propSampling,backgroundVariant])control.addEventListener('change',()=>{keys.clear();if(ready)draw();resize();});
  assetSet.addEventListener('change',()=>{keys.clear();refreshAudit();if(ready)draw();resize();});
  display.addEventListener('change',resize);window.addEventListener('resize',resize);document.addEventListener('fullscreenchange',resize);
  document.getElementById('fullscreen').addEventListener('click',async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await stage.requestFullscreen();}catch{document.getElementById('pixel-status').textContent='이 창에서 전체화면을 사용할 수 없습니다.';}});
  function refreshAudit(){
    document.getElementById('audit').replaceChildren();
    for(const [key,a] of Object.entries(activeAssets())){
      const b=a.boxes[0],d=dimensions(key,0),sk=dimensions(key,1),tr=document.createElement('tr');
      for(const t of [names[key]||key,(b[2]-b[0])+' × '+(b[3]-b[1])+'px',d.w+' × '+d.h+'px',sk.w+' × '+sk.h+'px',((b[2]-b[0])/d.w).toFixed(2)+'배']){const td=document.createElement('td');td.textContent=t;tr.append(td);}document.getElementById('audit').append(tr);
    }
  }
  refreshAudit();
  Promise.all(Object.entries(cfg.sources).map(([key,src])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[key]=im;resolve();};im.onerror=()=>reject(new Error(key+' 이미지를 불러오지 못했습니다.'));im.src=src;}))).then(()=>{
    ready=true;uiControls.forEach(c=>c.disabled=false);document.getElementById('loading').hidden=true;resize();draw();canvas.focus({preventScroll:true});document.documentElement.dataset.ready='true';
    if(new URLSearchParams(location.search).get('scene')==='boss')showBossReview();
  }).catch(error=>{document.getElementById('loading').querySelector('span').textContent=error.message;document.documentElement.dataset.error=error.message;});
  requestAnimationFrame(tick);
})();
