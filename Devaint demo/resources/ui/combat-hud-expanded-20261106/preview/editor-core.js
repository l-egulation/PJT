/* Layout math and file contract. No DOM, storage, or gameplay mutation. */
(function(root){
 'use strict';
 const schema='deviant-ui-layout-edit-v1',assetSet='ui.combat-hud-expanded-20261106';
 const clone=v=>JSON.parse(JSON.stringify(v));
 const empty=()=>({schema,assetSet,referenceResolution:[1670,942],layers:{},groups:{},extras:[]});
 const union=rects=>{
  if(!rects.length)return [0,0,1,1];
  const x=Math.min(...rects.map(r=>r[0])),y=Math.min(...rects.map(r=>r[1]));
  return [x,y,Math.max(...rects.map(r=>r[0]+r[2]))-x,Math.max(...rects.map(r=>r[1]+r[3]))-y];
 };
 function canonical(layer,edit,measure){
  const l={...clone(layer),...Object.fromEntries(['text','fontSize','color','sprite'].filter(k=>edit[k]!==undefined).map(k=>[k,edit[k]]))};
  if(l.type==='text'){
   l.size=l.fontSize??l.size;const w=Math.max(1,measure(l.text,l.size));
   l.baseRect=[l.x-(l.center?w/2:0),l.y,w,l.size*1.35];
  }else if(l.type==='path'){
   const xs=l.points.map(p=>p[0]),ys=l.points.map(p=>p[1]);
   l.baseRect=[Math.min(...xs),Math.min(...ys),Math.max(1,Math.max(...xs)-Math.min(...xs)),Math.max(1,Math.max(...ys)-Math.min(...ys))];
  }else l.baseRect=l.rect.slice();
  return l;
 }
 function resolve(scene,doc,measure){
  const base=[...scene.layers,...doc.extras].map((l,index)=>({...canonical(l,doc.layers[l.id]??{},measure),index}));
  const groupBounds={};for(const l of base)(groupBounds[l.group]??=[]).push(l.baseRect);
  for(const k in groupBounds)groupBounds[k]=union(groupBounds[k]);
  const items=base.map(l=>{
   const e=doc.layers[l.id]??{},g=doc.groups[l.group]??{},b=groupBounds[l.group];
   const gsx=g.sx??1,gsy=g.sy??1,[x,y,w,h]=l.baseRect;
   return {...l,rect:[b[0]+(x+(e.dx??0)-b[0])*gsx+(g.dx??0),b[1]+(y+(e.dy??0)-b[1])*gsy+(g.dy??0),w*(e.sx??1)*gsx,h*(e.sy??1)*gsy],
    hidden:!!(e.hidden||g.hidden),locked:!!(e.locked||g.locked),rotation:e.rotation??0,opacity:e.opacity??1,z:e.z??l.index,
    contain:e.stretch?false:l.contain,groupBounds:b,groupTransform:{dx:g.dx??0,dy:g.dy??0,sx:gsx,sy:gsy}};
  }).sort((a,b)=>a.z-b.z||a.index-b.index);
  return {items,groupBounds};
 }
 function setRect(doc,item,target){
  const e=doc.layers[item.id]??={},g=item.groupTransform,b=item.groupBounds,[x,y,w,h]=item.baseRect;
  Object.assign(e,{dx:(target[0]-b[0]-g.dx)/g.sx+b[0]-x,dy:(target[1]-b[1]-g.dy)/g.sy+b[1]-y,
   sx:Math.max(.01,target[2]/(w*g.sx)),sy:Math.max(.01,target[3]/(h*g.sy))});
  doc.layers[item.id]=e;
 }
 function setGroupRect(doc,id,oldRect,target,baseBounds){
  const g=doc.groups[id]??={},kx=target[2]/oldRect[2],ky=target[3]/oldRect[3];
  Object.assign(g,{dx:((g.dx??0)+baseBounds[0]-oldRect[0])*kx+target[0]-baseBounds[0],
   dy:((g.dy??0)+baseBounds[1]-oldRect[1])*ky+target[1]-baseBounds[1],sx:(g.sx??1)*kx,sy:(g.sy??1)*ky});
  doc.groups[id]=g;
 }
 function validate(raw,knownSprites){
  if(!raw||raw.schema!==schema||raw.assetSet!==assetSet)throw Error('이 에셋 세트의 배치 JSON을 선택해 주세요.');
  if(JSON.stringify(raw.referenceResolution)!=='[1670,942]')throw Error('기준 해상도가 맞지 않습니다.');
  const doc=empty();
  const finite=(v,min,max)=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
  for(const bucket of ['layers','groups']){
   if(!raw[bucket]||Array.isArray(raw[bucket])||typeof raw[bucket]!=='object'||Object.keys(raw[bucket]).length>2000)throw Error('배치 항목이 올바르지 않습니다.');
   for(const [id,e] of Object.entries(raw[bucket])){
    if(!id||id.length>200||['__proto__','constructor','prototype'].includes(id)||!e||typeof e!=='object')throw Error('잘못된 에셋 ID입니다.');
    const clean={};
    for(const [k,range] of Object.entries({dx:[-20000,20000],dy:[-20000,20000],sx:[.01,100],sy:[.01,100],rotation:[-3600,3600],opacity:[0,1],z:[-100000,100000],fontSize:[4,500]})){
     if(e[k]!==undefined){if(!finite(e[k],...range))throw Error('위치·크기 값이 범위를 벗어났습니다.');clean[k]=e[k];}
    }
    for(const k of ['hidden','locked','stretch'])if(e[k]!==undefined)clean[k]=!!e[k];
    if(e.text!==undefined){if(typeof e.text!=='string'||e.text.length>500)throw Error('텍스트가 너무 깁니다.');clean.text=e.text;}
    if(e.color!==undefined){if(!/^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(e.color))throw Error('색상 값이 올바르지 않습니다.');clean.color=e.color;}
    if(e.sprite!==undefined){if(!knownSprites.has(e.sprite))throw Error('알 수 없는 에셋입니다.');clean.sprite=e.sprite;}
    doc[bucket][id]=clean;
   }
  }
  if(!Array.isArray(raw.extras)||raw.extras.length>500)throw Error('추가 에셋이 너무 많습니다.');
  const ids=new Set();
  for(const l of raw.extras){
   if(!l||typeof l.id!=='string'||!/^custom-[a-z0-9-]+$/i.test(l.id)||ids.has(l.id))throw Error('추가 에셋 ID가 올바르지 않습니다.');ids.add(l.id);
   if(l.type==='sprite'){
    if(!knownSprites.has(l.sprite)||!Array.isArray(l.rect)||l.rect.length!==4||!l.rect.every((v,i)=>finite(v,i<2?-20000:1,20000)))throw Error('추가 그림이 올바르지 않습니다.');
    doc.extras.push({id:l.id,group:'custom',type:'sprite',sprite:l.sprite,rect:l.rect.slice(),contain:l.contain!==false});
   }else if(l.type==='text'){
    if(typeof l.text!=='string'||l.text.length>500||!finite(l.x,-20000,20000)||!finite(l.y,-20000,20000)||!finite(l.size,4,500)||!/^#[0-9a-f]{6}$/i.test(l.color))throw Error('추가 텍스트가 올바르지 않습니다.');
    doc.extras.push({id:l.id,group:'custom',type:'text',text:l.text,x:l.x,y:l.y,size:l.size,color:l.color,center:false});
   }else throw Error('지원하지 않는 추가 에셋입니다.');
  }
  return doc;
 }
 const api={schema,assetSet,clone,empty,union,resolve,setRect,setGroupRect,validate};
 if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.DeviantEditorCore=api;
})(typeof globalThis!=='undefined'?globalThis:this);
