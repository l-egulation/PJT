(function(){
 'use strict';
 const C=window.DeviantEditorCore, $=id=>document.getElementById(id),W=1670,H=942;
 const canvas=$('stage'),ctx=canvas.getContext('2d'),overlay=$('selection'),oc=overlay.getContext('2d');
 const assets=Object.fromEntries(PREVIEW_DATA.sprites.map(s=>[s.id,s]));
 const known=new Set(Object.keys(assets)),storageKey=C.assetSet+'.layout-editor.v1';
 const groups={health:'체력',currencies:'골드 · 안료',inventory:'가방 버튼',pause:'일시정지',minimap:'미니맵','boss-health':'보스 체력','weapon-0':'무기 1 UI','weapon-1':'무기 2 UI','reveal-0':'출발 팔레트 1','reveal-1':'출발 팔레트 2','reveal-heading':'출발 팔레트 제목',custom:'추가한 에셋'};
 let bridge,baseScene={width:W,height:H,layers:[]},doc=C.empty(),resolved={items:[],groupBounds:{}},selection=new Set();
 let undo=[],redo=[],drag=null,background=null,backgroundUrl=null,saveTimer=null,restoring=false,presetPending=false;
 const masks=new Map();
 const measure=(text,size)=>{ctx.font=`700 ${size}px "Malgun Gothic",sans-serif`;return ctx.measureText(text).width;};
 const label=l=>l.type==='text'?'텍스트 · '+l.text:l.type==='path'?'지도 경로':l.type==='ellipse'?'쿨타임 가림':l.type==='rect'?(l.group==='minimap'?'플레이어 위치':'표시 바탕'):(assets[l.sprite]?.label??l.sprite);
 const notice=(text,error=false)=>{$('editor-notice').textContent=text;$('editor-notice').classList.toggle('error',error);};
 const round=n=>Math.round(n*10)/10;
 function snapshot(){return JSON.stringify({doc,previewState:bridge?.getState()});}
 function commit(before){if(before!==snapshot()){undo.push(before);if(undo.length>80)undo.shift();redo=[];}render(true);scheduleSave();}
 function transaction(fn){const before=snapshot();fn();commit(before);}
 function restoreSnapshot(s){const data=JSON.parse(s);doc=data.doc;restoring=true;bridge.setState(data.previewState);restoring=false;selection.clear();render(true);scheduleSave();}
 function savePayload(full=false){
  const p={...C.clone(doc),previewState:bridge.getState()};
  if(full){p.savedAt=new Date().toISOString();p.coordinates='top-left pixels at referenceResolution';p.resolvedLayers=resolved.items.map(l=>({id:l.id,group:l.group,type:l.type,sprite:l.sprite,rect:l.rect.map(round),rotation:l.rotation,opacity:l.opacity,hidden:l.hidden,locked:l.locked,text:l.type==='text'?l.text:undefined,fontSize:l.type==='text'?l.size:undefined,z:l.z}));}
  return p;
 }
 function scheduleSave(){if(!bridge||restoring)return;clearTimeout(saveTimer);$('save-state').textContent='저장 중…';saveTimer=setTimeout(()=>{
  try{localStorage.setItem(storageKey,JSON.stringify(savePayload()));$('save-state').textContent='브라우저에 자동 저장됨';}
  catch{$('save-state').textContent='자동 저장 불가 · JSON으로 저장하세요';}
 },250);}
 function selectedItems(){return resolved.items.filter(l=>selection.has('l:'+l.id)||selection.has('g:'+l.group));}
 function bounds(){return C.union(selectedItems().map(l=>l.rect));}
 function editable(){return selectedItems().filter(l=>!l.locked);}
 function resolve(){resolved=C.resolve(baseScene,doc,measure);}
 function paintLayer(c,l){
  if(l.hidden||l.opacity<=0)return;
  const [x,y,w,h]=l.rect,[bx,by,bw,bh]=l.baseRect;
  c.save();c.globalAlpha=l.opacity;c.translate(x+w/2,y+h/2);c.rotate(l.rotation*Math.PI/180);c.translate(-w/2,-h/2);c.scale(w/bw,h/bh);
  if(l.type==='sprite'){
   const im=bridge?.images[l.sprite];if(!im){c.restore();return;}
   if(l.fill!==undefined){c.beginPath();c.rect(0,0,bw*l.fill,bh);c.clip();}
   if(l.radial!==undefined){c.beginPath();c.moveTo(bw/2,bh/2);c.arc(bw/2,bh/2,Math.max(bw,bh),-Math.PI/2,-Math.PI/2+2*Math.PI*l.radial);c.closePath();c.clip();}
   let iw=bw,ih=bh;if(l.contain){const scale=Math.min(bw/im.width,bh/im.height);iw=im.width*scale;ih=im.height*scale;}
   if(l.flip){c.translate(bw,0);c.scale(-1,1);}c.drawImage(im,(bw-iw)/2,(bh-ih)/2,iw,ih);
  }else if(l.type==='text'){
   c.fillStyle=l.color;c.font=`700 ${l.size}px "Malgun Gothic",sans-serif`;c.textBaseline='top';c.textAlign='left';c.fillText(l.text,0,5);
  }else if(l.type==='path'){
   c.strokeStyle=l.color;c.lineWidth=l.width;c.beginPath();l.points.forEach(([px,py],i)=>i?c.lineTo(px-bx,py-by):c.moveTo(px-bx,py-by));c.stroke();
  }else{
   c.fillStyle=l.color;if(l.type==='ellipse'){c.beginPath();c.ellipse(bw/2,bh/2,bw/2,bh/2,0,0,Math.PI*2);c.fill();}else c.fillRect(0,0,bw,bh);
  }c.restore();
 }
 function paintBackground(c){
  c.fillStyle=canvas.classList.contains('dark')?'#161a19':'#f8eddc';c.fillRect(0,0,W,H);
  if(background){const s=Math.min(W/background.width,H/background.height);c.drawImage(background,(W-background.width*s)/2,(H-background.height*s)/2,background.width*s,background.height*s);}
 }
 function render(full=false){
  resolve();ctx.clearRect(0,0,W,H);if(background)paintBackground(ctx);
  for(const l of resolved.items)paintLayer(ctx,l);
  drawSelection();updateProperties();if(full)updateLayers();$('undo').disabled=!undo.length;$('redo').disabled=!redo.length;
 }
 function displayScale(){return overlay.getBoundingClientRect().width/W||1;}
 function handles(){const [x,y,w,h]=bounds();return [['nw',x,y],['n',x+w/2,y],['ne',x+w,y],['e',x+w,y+h/2],['se',x+w,y+h],['s',x+w/2,y+h],['sw',x,y+h],['w',x,y+h/2]];}
 function drawSelection(){
  oc.clearRect(0,0,W,H);if(!$('edit-mode').checked)return;
  const scale=displayScale();
  if($('grid').checked){oc.strokeStyle='#36654b22';oc.lineWidth=1/scale;oc.beginPath();for(let x=0;x<W;x+=32){oc.moveTo(x,0);oc.lineTo(x,H);}for(let y=0;y<H;y+=32){oc.moveTo(0,y);oc.lineTo(W,y);}oc.stroke();}
  const items=selectedItems();if(!items.length)return;
  oc.strokeStyle='#00dfbd';oc.lineWidth=1.5/scale;
  if(items.length>1){oc.setLineDash([4/scale,4/scale]);for(const l of items)oc.strokeRect(...l.rect);oc.setLineDash([]);}
  oc.strokeRect(...bounds());
  if(!editable().length)return;
  for(const [,x,y] of handles()){const side=7/scale;oc.fillStyle='#e8fff4';oc.fillRect(x-side/2,y-side/2,side,side);oc.strokeRect(x-side/2,y-side/2,side,side);}
 }
 function select(key,extend=false){if(!extend)selection.clear();if(extend&&selection.has(key))selection.delete(key);else selection.add(key);render(true);}
 function updateProperties(){
  const items=selectedItems(),any=!!items.length,[x,y,w,h]=bounds();const single=items.length===1?items[0]:null;
  const groupOnly=selection.size===1&&[...selection][0].startsWith('g:');
  $('selection-name').textContent=groupOnly?(groups[[...selection][0].slice(2)]??'묶음'):single?label(single):any?items.length+'개 선택':'에셋을 선택하세요';
  $('selection-info').textContent=any?(groupOnly?'묶음 이동·크기 조절':single?.type==='sprite'?single.sprite:'함께 이동·크기 조절'):'화면 또는 아래 목록에서 선택할 수 있습니다.';
  const values={x,y,w,h,rotation:single?.rotation??0,opacity:Math.round((single?.opacity??1)*100),font:single?.size??24};
  for(const [k,v] of Object.entries(values)){const e=$('prop-'+k);e.disabled=!any||(['rotation','opacity','font'].includes(k)&&!single);if(document.activeElement!==e)e.value=any?round(v):'';}
  $('prop-hidden').disabled=!any;$('prop-locked').disabled=!any;
  const ge=groupOnly?doc.groups[[...selection][0].slice(2)]??{}:null;
  $('prop-hidden').checked=!!(ge?ge.hidden:items.length&&items.every(l=>l.hidden));$('prop-locked').checked=!!(ge?ge.locked:items.length&&items.every(l=>l.locked));
  $('text-properties').hidden=single?.type!=='text';if(single?.type==='text'){
   if(document.activeElement!==$('prop-text'))$('prop-text').value=single.text;
   if(document.activeElement!==$('prop-color'))$('prop-color').value=single.color.slice(0,7);
  }
  for(const id of ['bring-front','send-back','duplicate','remove','reset-selected'])$(id).disabled=!any;
 }
 function rowButton(text,cls,fn,title){const b=document.createElement('button');b.type='button';b.textContent=text;b.className=cls;b.onclick=fn;if(title)b.title=title;return b;}
 function updateLayers(){
  const list=$('layer-list'),scroll=list.scrollTop,term=$('layer-search').value.toLowerCase();list.replaceChildren();
  const grouped=new Map();for(const l of [...resolved.items].reverse())(grouped.get(l.group)??(grouped.set(l.group,[]),grouped.get(l.group))).push(l);
  for(const [gid,items] of grouped){
   const matches=items.filter(l=>!term||label(l).toLowerCase().includes(term)||(groups[gid]??gid).includes(term));if(!matches.length)continue;
   const row=document.createElement('div');row.className='group-row';const key='g:'+gid;
   const b=rowButton(groups[gid]??gid,'row-select'+(selection.has(key)?' active':''),e=>select(key,e.shiftKey));b.dataset.group=gid;row.append(b);list.append(row);
   for(const l of matches){
    const row=document.createElement('div');row.className='layer-row';const key='l:'+l.id;
    const b=rowButton(label(l),'row-select'+(selection.has(key)?' active':'')+(l.hidden?' muted':''),e=>select(key,e.shiftKey),l.id);b.dataset.layer=l.id;
    const eye=rowButton(l.hidden?'○':'●','row-icon',()=>transaction(()=>{const e=doc.layers[l.id]??={};e.hidden=!e.hidden;doc.layers[l.id]=e;}),'숨기기 / 보이기');
    const lock=rowButton(l.locked?'▣':'□','row-icon'+(l.locked?' locked':''),()=>transaction(()=>{const e=doc.layers[l.id]??={};e.locked=!e.locked;doc.layers[l.id]=e;}),'잠금 / 해제');
    row.append(b,eye,lock);list.append(row);
   }
  }list.scrollTop=scroll;
 }
 function setTarget(target,startItems=selectedItems(),oldRect=bounds(),groupKey=null){
  if(groupKey){C.setGroupRect(doc,groupKey,oldRect,target,resolved.groupBounds[groupKey]);return;}
  const kx=target[2]/oldRect[2],ky=target[3]/oldRect[3];
  for(const l of startItems){if(l.locked)continue;C.setRect(doc,l,[target[0]+(l.rect[0]-oldRect[0])*kx,target[1]+(l.rect[1]-oldRect[1])*ky,l.rect[2]*kx,l.rect[3]*ky]);}
 }
 function singleGroup(){return selection.size===1&&[...selection][0].startsWith('g:')?[...selection][0].slice(2):null;}
 function applyRect(target){setTarget(target,selectedItems(),bounds(),singleGroup());}
 function point(e){const r=overlay.getBoundingClientRect();return [(e.clientX-r.left)*W/r.width,(e.clientY-r.top)*H/r.height];}
 function localPoint(l,p){const [x,y,w,h]=l.rect,angle=-l.rotation*Math.PI/180,dx=p[0]-x-w/2,dy=p[1]-y-h/2;return [(dx*Math.cos(angle)-dy*Math.sin(angle)+w/2)/w,(dx*Math.sin(angle)+dy*Math.cos(angle)+h/2)/h];}
 function hit(l,p){
  if(l.hidden||l.locked||l.opacity<=0)return false;const [u,v]=localPoint(l,p);if(u<0||v<0||u>1||v>1)return false;
  if(l.fill!==undefined&&u>l.fill)return false;
  if(l.radial!==undefined){const angle=(Math.atan2(u-.5,-(v-.5))+Math.PI*2)%(Math.PI*2);if(angle>l.radial*Math.PI*2)return false;}
  if(l.type==='sprite'){
   const im=bridge.images[l.sprite];if(!im)return false;const bw=l.baseRect[2],bh=l.baseRect[3];let x=u*bw,y=v*bh;
   if(l.flip)x=bw-x;let iw=bw,ih=bh;if(l.contain){const s=Math.min(bw/im.width,bh/im.height);iw=im.width*s;ih=im.height*s;}
   const a=(x-(bw-iw)/2)/iw,b=(y-(bh-ih)/2)/ih;if(a<0||b<0||a>=1||b>=1)return false;
   try{if(!masks.has(l.sprite)){const c=document.createElement('canvas');c.width=256;c.height=256;const cctx=c.getContext('2d',{willReadFrequently:true});cctx.drawImage(im,0,0,256,256);masks.set(l.sprite,cctx.getImageData(0,0,256,256).data);}return masks.get(l.sprite)[(Math.floor(b*256)*256+Math.floor(a*256))*4+3]>24;}catch{return true;}
  }
  if(l.type==='ellipse')return (u-.5)**2+(v-.5)**2<=.25;
  return true;
 }
 function hitHandle(p){if(!editable().length)return null;const radius=8/displayScale();return handles().find(h=>Math.hypot(h[1]-p[0],h[2]-p[1])<radius)?.[0]??null;}
 function resized(start,handle,dx,dy,keep){
  let [x,y,w,h]=start,rx=x+w,by=y+h;
  if(handle.includes('w'))x=Math.min(rx-4,x+dx);if(handle.includes('e'))rx=Math.max(x+4,rx+dx);
  if(handle.includes('n'))y=Math.min(by-4,y+dy);if(handle.includes('s'))by=Math.max(y+4,by+dy);
  let nw=rx-x,nh=by-y;
  if(keep){const ratio=w/h;if(handle==='n'||handle==='s')nw=nh*ratio;else nh=nw/ratio;if(handle.includes('w'))x=rx-nw;if(handle.includes('n'))y=by-nh;}
  return [x,y,nw,nh];
 }
 overlay.addEventListener('pointerdown',e=>{
  if(!$('edit-mode').checked||e.button!==0)return;e.preventDefault();overlay.focus();const p=point(e),handle=hitHandle(p);
  if(!handle){const hits=[...resolved.items].reverse().filter(l=>hit(l,p));let picked=hits[0];
   if(e.altKey&&hits.length){const current=hits.findIndex(l=>selection.has('l:'+l.id));picked=hits[(current+1)%hits.length];}
   const selected=selectedItems();const onSelected=selected.some(l=>hit(l,p));
   if(picked){if(e.shiftKey||!onSelected||e.altKey)select('l:'+picked.id,e.shiftKey);}else{selection.clear();render(true);return;}
  }
  if(!editable().length)return;
  drag={point:p,handle,before:snapshot(),document:C.clone(doc),items:C.clone(selectedItems()),bounds:bounds(),group:singleGroup()};overlay.setPointerCapture(e.pointerId);
 });
 overlay.addEventListener('pointermove',e=>{
  const p=point(e);if(!drag){const h=hitHandle(p);overlay.style.cursor=h?h+'-resize':resolved.items.some(l=>hit(l,p))?'move':'default';return;}
  let dx=p[0]-drag.point[0],dy=p[1]-drag.point[1];doc=C.clone(drag.document);
  let target=drag.handle?resized(drag.bounds,drag.handle,dx,dy,$('aspect-lock').checked):[drag.bounds[0]+dx,drag.bounds[1]+dy,drag.bounds[2],drag.bounds[3]];
  if($('snap').checked){target[0]=Math.round(target[0]/8)*8;target[1]=Math.round(target[1]/8)*8;if(drag.handle){target[2]=Math.max(8,Math.round(target[2]/8)*8);target[3]=Math.max(8,Math.round(target[3]/8)*8);}}
  setTarget(target,drag.items,drag.bounds,drag.group);render();
 });
 const endDrag=e=>{if(!drag)return;const before=drag.before;drag=null;if(overlay.hasPointerCapture(e.pointerId))overlay.releasePointerCapture(e.pointerId);commit(before);};
 overlay.addEventListener('pointerup',endDrag);overlay.addEventListener('pointercancel',e=>{if(drag){doc=drag.document;drag=null;render(true);}});
 for(const prop of ['x','y','w','h'])$('prop-'+prop).onchange=()=>{
  const val=Number($('prop-'+prop).value);if(!Number.isFinite(val)||!selectedItems().length)return;
  transaction(()=>{const r=bounds().slice(),i=['x','y','w','h'].indexOf(prop);r[i]=i<2?Math.max(-20000,Math.min(20000,val)):Math.max(4,Math.min(20000,val));
   if(i>=2&&$('aspect-lock').checked){const b=bounds();if(i===2)r[3]=r[2]*b[3]/b[2];else r[2]=r[3]*b[2]/b[3];}applyRect(r);
  });
 };
 for(const [prop,key,min,max,factor] of [['rotation','rotation',-3600,3600,1],['opacity','opacity',0,100,.01],['font','fontSize',4,500,1]])$('prop-'+prop).onchange=()=>{
  const value=Number($('prop-'+prop).value),l=selectedItems()[0];if(!l||!Number.isFinite(value))return;
  transaction(()=>{const e=doc.layers[l.id]??={};e[key]=Math.max(min,Math.min(max,value))*factor;if(prop==='font'){e.sx=1;e.sy=1;}doc.layers[l.id]=e;});
 };
 for(const prop of ['hidden','locked'])$('prop-'+prop).onchange=()=>transaction(()=>{
  const group=singleGroup(),value=$('prop-'+prop).checked;
  if(group){const e=doc.groups[group]??={};e[prop]=value;doc.groups[group]=e;}
  else for(const l of selectedItems()){const e=doc.layers[l.id]??={};e[prop]=value;doc.layers[l.id]=e;}
 });
 for(const [field,key] of [['prop-text','text'],['prop-color','color']])$(field).onchange=()=>transaction(()=>{const l=selectedItems()[0];if(l?.type==='text'){const e=doc.layers[l.id]??={};e[key]=$(field).value;doc.layers[l.id]=e;}});
 $('undo').onclick=()=>{if(undo.length){redo.push(snapshot());restoreSnapshot(undo.pop());}};
 $('redo').onclick=()=>{if(redo.length){undo.push(snapshot());restoreSnapshot(redo.pop());}};
 $('reset-selected').onclick=()=>transaction(()=>{for(const key of selection){if(key.startsWith('g:')){delete doc.groups[key.slice(2)];for(const l of selectedItems().filter(l=>l.group===key.slice(2)))delete doc.layers[l.id];}else delete doc.layers[key.slice(2)];}});
 $('reset-layout').onclick=()=>transaction(()=>{doc=C.empty();selection.clear();notice('기본 배치로 되돌렸습니다. 되돌리기로 복구할 수 있습니다.');});
 $('remove').onclick=()=>transaction(()=>{for(const l of editable()){if(l.id.startsWith('custom-')){doc.extras=doc.extras.filter(e=>e.id!==l.id);delete doc.layers[l.id];}else{const e=doc.layers[l.id]??={};e.hidden=true;doc.layers[l.id]=e;}}selection.clear();});
 for(const [id,direction] of [['bring-front',1],['send-back',-1]])$(id).onclick=()=>transaction(()=>{
  const z=(direction===1?Math.max:Math.min)(...resolved.items.map(l=>l.z));selectedItems().forEach((l,i)=>{const e=doc.layers[l.id]??={};e.z=z+direction*(i+1);doc.layers[l.id]=e;});
 });
 function addSprite(sid,rect){const id='custom-'+crypto.randomUUID();doc.extras.push({id,group:'custom',type:'sprite',sprite:sid,rect,contain:true});selection=new Set(['l:'+id]);return id;}
 $('duplicate').onclick=()=>transaction(()=>{const created=[];for(const l of selectedItems()){
  if(l.type==='sprite'){const id=addSprite(l.sprite,[l.rect[0]+16,l.rect[1]+16,l.rect[2],l.rect[3]]);doc.layers[id]={rotation:l.rotation,opacity:l.opacity,stretch:l.contain===false};created.push('l:'+id);}
  if(l.type==='text'){const id='custom-'+crypto.randomUUID();doc.extras.push({id,group:'custom',type:'text',text:l.text,x:l.rect[0]+16,y:l.rect[1]+16,size:l.size,color:l.color.slice(0,7),center:false});created.push('l:'+id);}
 }selection=new Set(created);});
 document.querySelectorAll('[data-align]').forEach(b=>b.onclick=()=>transaction(()=>{
  const items=editable(),all=bounds();if(singleGroup()||items.length===1){const r=all.slice();if(b.dataset.align==='left')r[0]=24;if(b.dataset.align==='top')r[1]=24;if(b.dataset.align==='center-x')r[0]=(W-r[2])/2;if(b.dataset.align==='center-y')r[1]=(H-r[3])/2;applyRect(r);}
  else for(const l of items){const r=l.rect.slice();if(b.dataset.align==='left')r[0]=all[0];if(b.dataset.align==='top')r[1]=all[1];if(b.dataset.align==='center-x')r[0]=all[0]+(all[2]-r[2])/2;if(b.dataset.align==='center-y')r[1]=all[1]+(all[3]-r[3])/2;C.setRect(doc,l,r);}
 }));
 function fillAssetList(){const term=$('asset-search').value.toLowerCase();$('asset-list').replaceChildren(...Object.values(assets).filter(s=>!term||(s.label+' '+s.id).toLowerCase().includes(term)).map(s=>new Option(s.label,s.id)));if($('asset-list').options.length)$('asset-list').selectedIndex=0;}
 $('asset-search').oninput=fillAssetList;fillAssetList();$('layer-search').oninput=updateLayers;
 $('add-asset').onclick=()=>{const s=assets[$('asset-list').value];if(!s)return;transaction(()=>{const k=140/Math.max(...s.size),w=s.size[0]*k,h=s.size[1]*k;addSprite(s.id,[(W-w)/2,(H-h)/2,w,h]);});};
 $('add-text').onclick=()=>transaction(()=>{const id='custom-'+crypto.randomUUID();doc.extras.push({id,group:'custom',type:'text',text:'새 텍스트',x:760,y:430,size:32,color:'#1b1c18',center:false});selection=new Set(['l:'+id]);});
 function download(blob,name){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 $('save-layout').onclick=()=>{download(new Blob([JSON.stringify(savePayload(true),null,2)],{type:'application/json'}),'deviant-ui-layout.json');notice('배치 JSON을 저장했습니다. 이 파일로 같은 배치를 다시 불러올 수 있습니다.');};
 $('load-layout').onchange=async()=>{const f=$('load-layout').files[0];if(!f)return;try{
  if(f.size>5*1024*1024)throw Error('5MB 이하의 배치 JSON을 선택해 주세요.');const raw=JSON.parse(await f.text()),valid=C.validate(raw,known),previewState=raw.previewState?bridge.validateState(raw.previewState):null;
  transaction(()=>{doc=valid;selection.clear();restoring=true;try{if(previewState)bridge.setState(previewState);}finally{restoring=false;}});notice('배치를 불러왔습니다.');
 }catch(e){notice('불러오기 실패: '+e.message,true);}finally{$('load-layout').value='';}};
 $('export-png').onclick=()=>{try{const c=document.createElement('canvas');c.width=W;c.height=H;const context=c.getContext('2d');if($('png-kind').value==='background')paintBackground(context);for(const l of resolved.items)paintLayer(context,l);c.toBlob(blob=>{if(blob){download(blob,'deviant-ui-layout.png');notice('선택 표시·격자를 제외한 PNG를 저장했습니다.');}},'image/png');}catch{notice('PNG 저장은 편집기_열기.cmd로 연 편집기에서 사용할 수 있습니다.',true);}};
 $('background-file').onchange=async()=>{const f=$('background-file').files[0];if(!f)return;if(f.size>20*1024*1024){notice('20MB 이하의 배경 이미지를 선택해 주세요.',true);return;}
  const url=URL.createObjectURL(f),im=new Image();im.onload=()=>{if(backgroundUrl)URL.revokeObjectURL(backgroundUrl);backgroundUrl=url;background=im;render();notice('게임 화면을 배경으로 넣었습니다. 배경 파일은 이 탭에서만 유지됩니다.');};im.onerror=()=>{URL.revokeObjectURL(url);notice('이미지를 열 수 없습니다.',true);};im.src=url;
 };
 $('clear-background').onclick=()=>{background=null;if(backgroundUrl)URL.revokeObjectURL(backgroundUrl);backgroundUrl=null;$('background-file').value='';render();notice('게임 배경을 지웠습니다. 배치는 그대로 유지됩니다.');};
 function zoom(){const available=$('viewport').clientWidth-32,value=$('zoom').value;$('stage-wrap').style.width=(value==='fit'?Math.max(200,available):W*Number(value))+'px';drawSelection();}
 $('zoom').onchange=zoom;new ResizeObserver(zoom).observe($('viewport'));
 $('edit-mode').onchange=drawSelection;$('grid').onchange=drawSelection;
 document.addEventListener('keydown',e=>{
  if(e.target.closest('input,textarea,select,[contenteditable=true]'))return;
  const command=e.ctrlKey||e.metaKey;
  if(command&&e.key.toLowerCase()==='s'){e.preventDefault();$('save-layout').click();return;}
  if(command&&e.key.toLowerCase()==='z'){e.preventDefault();$(e.shiftKey?'redo':'undo').click();return;}
  if(command&&e.key.toLowerCase()==='y'){e.preventDefault();$('redo').click();return;}
  if(command&&e.key.toLowerCase()==='d'){e.preventDefault();$('duplicate').click();return;}
  if(e.key==='Escape'){selection.clear();render(true);return;}
  if(!$('edit-mode').checked)return;
  if(['Delete','Backspace'].includes(e.key)&&selection.size){e.preventDefault();$('remove').click();return;}
  if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)&&editable().length){e.preventDefault();const step=e.shiftKey?10:1;transaction(()=>{const r=bounds();r[0]+=e.key==='ArrowLeft'?-step:e.key==='ArrowRight'?step:0;r[1]+=e.key==='ArrowUp'?-step:e.key==='ArrowDown'?step:0;applyRect(r);});}
 });
 window.DeviantLayoutEditor={
  attach(api){bridge=api;try{const raw=localStorage.getItem(storageKey);if(raw){const data=JSON.parse(raw);doc=C.validate(data,known);restoring=true;try{if(data.previewState)bridge.setState(data.previewState);}finally{restoring=false;}notice('이 브라우저에 저장된 배치를 복원했습니다.');}}
   catch(e){notice('자동 저장을 복원하지 못했습니다. 저장한 JSON을 불러올 수 있습니다.',true);}baseScene=bridge.getScene();render(true);zoom();scheduleSave();
   if(new URL(location.href).searchParams.get('layout')==='matched-weapons'){
    presetPending=true;fetch('../runtime/layout-edited.json').then(r=>{if(!r.ok)throw Error('배치 파일을 열 수 없습니다.');return r.json();}).then(raw=>{
     const valid=C.validate(raw,known),state=bridge.validateState(raw.previewState);
     transaction(()=>{doc=valid;selection.clear();restoring=true;try{bridge.setState(state);}finally{restoring=false;}});
     const url=new URL(location.href);url.searchParams.delete('layout');history.replaceState(null,'',url);
     notice('사용자 배치를 불러왔습니다. 무기 2의 간격과 크기를 무기 1에 맞췄습니다.');
    }).catch(e=>notice('배치 불러오기 실패: '+e.message,true)).finally(()=>{presetPending=false;});
   }
  },
  draw(scene){baseScene=scene;render(true);scheduleSave();},
  getDocument:()=>C.clone(doc),getResolved:()=>C.clone(resolved.items),getSelection:()=>[...selection],
  ready:()=>!!bridge&&!presetPending&&Object.keys(bridge.images).length===known.size
 };
})();
