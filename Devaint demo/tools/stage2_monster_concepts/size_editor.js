(() => {
  'use strict';
  const data=JSON.parse(document.getElementById('editor-data').textContent);
  const $=id=>document.getElementById(id), canvas=$('stage'), ctx=canvas.getContext('2d');
  const ids=data.monsters.map(m=>m.id), byId=Object.fromEntries(data.monsters.map(m=>[m.id,m]));
  const key='stage2-waterway-monster-size-v1'+(data.storageNamespace?'-'+data.storageNamespace:''), schema='stage2-monster-size-study-v1';
  const clone=value=>JSON.parse(JSON.stringify(value));
  const defaults={scene:data.initial?.scene||'A',art:data.initial?.art||'after',selected:data.initial?.selected||ids[0],monsters:clone(data.defaults)};
  let state=clone(defaults), images={}, ready=false, drag=null, saveTimer;
  const status=text=>{$('status').textContent=text;};
  const clamp=(v,min,max)=>Math.min(max,Math.max(min,Math.round(v)));
  function validate(raw){
    if(!raw||raw.schema!==schema||raw.width!==960||raw.height!==540||!data.scenes[raw.scene]||!['after','before'].includes(raw.art)||!ids.includes(raw.selected)||!Array.isArray(raw.monsters)||raw.monsters.length!==ids.length)throw Error('이 도구에서 저장한 설정 JSON을 선택해주세요.');
    const seen=new Set(), monsters={};
    for(const m of raw.monsters){
      if(!m||!ids.includes(m.id)||seen.has(m.id)||typeof m.visible!=='boolean'||!Number.isFinite(m.height)||m.height<4||m.height>400||!Number.isFinite(m.x)||m.x< -960||m.x>1920||!Number.isFinite(m.foot)||m.foot< -540||m.foot>1080)throw Error('몬스터 크기·위치 값이 올바르지 않습니다.');
      seen.add(m.id);monsters[m.id]={height:Math.round(m.height),x:Math.round(m.x),foot:Math.round(m.foot),visible:m.visible};
    }
    return {scene:raw.scene,art:raw.art,selected:raw.selected,monsters};
  }
  function exportState(){return {schema,width:960,height:540,scene:state.scene,art:state.art,selected:state.selected,monsters:ids.map(id=>({id,...state.monsters[id]}))};}
  try{const saved=localStorage.getItem(key);if(saved)state=validate(JSON.parse(saved));}catch(_){status('기본 설정으로 시작합니다. JSON으로 설정을 저장할 수 있습니다.');}
  function save(){clearTimeout(saveTimer);saveTimer=setTimeout(()=>{try{localStorage.setItem(key,JSON.stringify(exportState()));status('이 브라우저에 자동 저장됨');}catch(_){status('브라우저 자동 저장 제한 · JSON으로 설정을 저장해주세요.');}},160);}
  function dimensions(id){const im=images[`${state.art}:${id}`];const h=state.monsters[id].height;return {w:Math.max(1,Math.round(h*im.width/im.height)),h};}
  function draw(guides=$('guides').checked){
    if(!ready)return;
    ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,960,540);ctx.drawImage(images[`scene:${state.scene}`],0,0);
    for(const id of ids){const m=state.monsters[id];if(m.visible){const {w,h}=dimensions(id);ctx.drawImage(images[`${state.art}:${id}`],m.x,m.foot-h,w,h);}}
    const fg=data.scenes[state.scene].foreground;ctx.drawImage(images.foreground,fg[0],fg[1]);
    if(guides&&state.monsters[state.selected].visible){
      const id=state.selected,m=state.monsters[id],{w,h}=dimensions(id);ctx.strokeStyle='#efc07b';ctx.lineWidth=1;ctx.setLineDash([4,3]);ctx.strokeRect(m.x-.5,m.foot-h-.5,w+1,h+1);ctx.setLineDash([]);ctx.beginPath();ctx.moveTo(m.x-7,m.foot+.5);ctx.lineTo(m.x+w+7,m.foot+.5);ctx.stroke();
    }
  }
  function resizeView(){
    const zoom=$('zoom').value, scale=zoom==='fit'?Math.min(1,$('viewport').clientWidth/960):Number(zoom);
    canvas.style.width=`${960*scale}px`;canvas.style.height=`${540*scale}px`;
    $('viewLabel').textContent=`논리 960×540 · 화면 ${Math.round(scale*100)}%`;
  }
  function sync(){
    const id=state.selected,m=state.monsters[id],info=byId[id];
    $('scene').value=state.scene;$('art').value=state.art;$('selectedName').textContent=`${id} ${info.name}`;
    $('height').value=m.height;$('heightNumber').value=m.height;$('scaleNumber').value=Math.round(m.height/data.defaults[id].height*100);$('posX').value=m.x;$('posFoot').value=m.foot;
    if(ready){const {w,h}=dimensions(id);$('dimensions').textContent=`너비 ${w}px × 높이 ${h}px · 플레이어 높이의 ${(h/64).toFixed(2)}배`;}
    for(const ident of ids){const row=document.querySelector(`[data-id="${ident}"]`);row.classList.toggle('active',ident===id);row.querySelector('input').checked=state.monsters[ident].visible;const h=state.monsters[ident].height;row.querySelector('small').textContent=`${byId[ident].role} · 높이 ${h}px`;}
    draw();
  }
  function change(){sync();save();}
  function select(id){state.selected=id;state.monsters[id].visible=true;change();}
  function setHeight(value){if(!Number.isFinite(Number(value))||value==='')return;state.monsters[state.selected].height=clamp(Number(value),4,400);change();}
  for(const info of data.monsters){
    const row=document.createElement('div');row.className='monster-row';row.dataset.id=info.id;
    const label=document.createElement('label'),check=document.createElement('input');check.type='checkbox';check.setAttribute('aria-label',`${info.id} ${info.name} 표시`);label.append(check);
    check.addEventListener('change',()=>{state.monsters[info.id].visible=check.checked;change();});
    const button=document.createElement('button');button.type='button';const title=document.createElement('span');title.textContent=`${info.id} ${info.name}`;button.append(title,document.createElement('small'));button.addEventListener('click',()=>select(info.id));row.append(label,button);$('monsters').append(row);
  }
  $('height').addEventListener('input',e=>setHeight(e.target.value));
  $('heightNumber').addEventListener('input',e=>{const n=Number(e.target.value);if(e.target.value!==''&&Number.isFinite(n)&&n>=4&&n<=400)setHeight(n);});
  $('heightNumber').addEventListener('change',e=>{if(e.target.value==='')sync();else setHeight(e.target.value);});
  $('scaleNumber').addEventListener('change',e=>{const n=Number(e.target.value);if(Number.isFinite(n)&&e.target.value!=='')setHeight(data.defaults[state.selected].height*n/100);});
  for(const [control,prop,min,max] of [['posX','x',-960,1920],['posFoot','foot',-540,1080]])$(control).addEventListener('input',e=>{if(e.target.value!==''&&Number.isFinite(Number(e.target.value))){state.monsters[state.selected][prop]=clamp(Number(e.target.value),min,max);change();}});
  $('scene').addEventListener('change',e=>{state.scene=e.target.value;change();});$('art').addEventListener('change',e=>{state.art=e.target.value;change();});$('zoom').addEventListener('change',resizeView);$('guides').addEventListener('change',()=>draw());
  $('presets').addEventListener('click',e=>{const group=e.target.dataset.preset;if(!group)return;const scene={melee:'A',ranged:'C',elite:'D',boss:'E'}[group];state.scene=scene;for(const id of ids)state.monsters[id].visible=byId[id].group===group;state.selected=ids.find(id=>byId[id].group===group);change();});
  $('playerHeight').addEventListener('click',()=>setHeight(64));$('resetSize').addEventListener('click',()=>setHeight(data.defaults[state.selected].height));
  $('resetPosition').addEventListener('click',()=>{const id=state.selected;state.monsters[id].x=data.defaults[id].x;state.monsters[id].foot=data.defaults[id].foot;change();});
  $('resetAll').addEventListener('click',()=>{state=clone(defaults);change();status('초기 크기·위치로 되돌렸습니다.');});
  function point(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*960/r.width,y:(e.clientY-r.top)*540/r.height};}
  canvas.addEventListener('pointerdown',e=>{if(!ready||e.button!==0)return;const p=point(e);const hit=ids.slice().reverse().find(id=>{const m=state.monsters[id],{w,h}=dimensions(id);return m.visible&&p.x>=m.x&&p.x<=m.x+w&&p.y>=m.foot-h&&p.y<=m.foot;});if(!hit)return;state.selected=hit;const m=state.monsters[hit];drag={id:hit,dx:p.x-m.x,dy:p.y-m.foot};canvas.setPointerCapture(e.pointerId);canvas.focus();sync();e.preventDefault();});
  canvas.addEventListener('pointermove',e=>{if(!drag)return;const p=point(e),m=state.monsters[drag.id];m.x=clamp(p.x-drag.dx,-960,1920);m.foot=clamp(p.y-drag.dy,-540,1080);sync();});
  function endDrag(){if(drag){drag=null;save();}}canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);
  canvas.addEventListener('keydown',e=>{const move={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[e.key];if(!move)return;e.preventDefault();const m=state.monsters[state.selected],n=e.shiftKey?8:1;m.x=clamp(m.x+move[0]*n,-960,1920);m.foot=clamp(m.foot+move[1]*n,-540,1080);change();});
  function download(blob,name){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),3000);}
  $('exportJson').addEventListener('click',()=>{download(new Blob([JSON.stringify(exportState(),null,2)],{type:'application/json'}),'stage2-monster-sizes.json');status('현재 크기·위치를 JSON으로 저장했습니다.');});
  $('importJson').addEventListener('click',()=>$('jsonFile').click());
  $('jsonFile').addEventListener('change',async e=>{const file=e.target.files[0];if(!file)return;try{if(file.size>1024*1024)throw Error('설정 파일은 1MB 이하 JSON이어야 합니다.');const next=validate(JSON.parse(await file.text()));state=next;change();status('설정 JSON을 불러왔습니다.');}catch(error){status(error.message||'설정 JSON을 읽을 수 없습니다.');}finally{e.target.value='';}});
  $('exportPng').addEventListener('click',()=>{if(!ready)return;try{draw(false);canvas.toBlob(blob=>{if(blob)download(blob,`stage2-${state.scene}-size-study.png`);draw();status(blob?'선택틀 없는 960×540 합성 PNG를 저장했습니다.':'PNG 저장에 실패했습니다.');},'image/png');}catch(error){draw();status('PNG 저장에 실패했습니다. JSON 설정은 저장할 수 있습니다.');}});
  function load(src){return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(Error('그림을 불러오지 못했습니다.'));im.src=src;});}
  const assets={foreground:data.foreground};for(const [id,s] of Object.entries(data.scenes))assets[`scene:${id}`]=s.image;for(const m of data.monsters)for(const art of ['before','after'])assets[`${art}:${m.id}`]=m[art];
  sync();status('배경과 몬스터를 불러오는 중…');
  Promise.all(Object.entries(assets).map(async([id,src])=>{images[id]=await load(src);})).then(()=>{ready=true;sync();resizeView();status('크기를 조절하세요. 변경은 이 브라우저에 자동 저장됩니다.');}).catch(error=>status(error.message));
  new ResizeObserver(resizeView).observe($('viewport'));
})();
