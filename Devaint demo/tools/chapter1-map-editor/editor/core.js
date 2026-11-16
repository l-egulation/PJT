(function(root){
'use strict';
const TYPES={maintenance:{label:'정비소',color:'#76daca',w:256,h:128},elite:{label:'엘리트 몬스터방',color:'#f29462',w:1280,h:640},midboss:{label:'중간보스',color:'#f29462',w:1280,h:640},boss:{label:'최종보스',color:'#ee7395',w:1536,h:768},shop:{label:'상점',color:'#efcf65',w:448,h:192},gold:{label:'황금방',color:'#efcf65',w:640,h:256},structure:{label:'구조물',color:'#b6c8bd',w:384,h:224},wallkick:{label:'벽차기 통로',color:'#bd99ee',w:96,h:768},platform:{label:'전투·보조 발판',color:'#a5d280',w:1024,h:32},dropfloor:{label:'통과 바닥 ↓+S',color:'#9fdbea',w:640,h:32},trampoline:{label:'트램펄린',color:'#ffad64',w:160,h:32},enemy:{label:'몬스터 배치',color:'#ed8880',w:640,h:160}};
const clone=v=>JSON.parse(JSON.stringify(v)),snap=v=>Math.round(v/32)*32,clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
class EditorModel{
 constructor(plan,markers){this.plan={id:plan.id,width:plan.width,height:plan.height};this.defaults=clone(markers);this.markers=clone(markers);this.selected=null;this.undoStack=[];this.redoStack=[];this.transaction=null;this.sequence=1000;}
 snapshot(){return {markers:clone(this.markers),selected:this.selected};}
 restore(s){this.markers=clone(s.markers);this.selected=s.selected;}
 record(before){if(JSON.stringify(before)===JSON.stringify(this.snapshot()))return;this.undoStack.push(before);if(this.undoStack.length>80)this.undoStack.shift();this.redoStack=[];}
 begin(){if(!this.transaction)this.transaction=this.snapshot();}
 end(){if(this.transaction){const s=this.transaction;this.transaction=null;this.record(s);}}
 get(id=this.selected){return this.markers.find(m=>m.id===id);}
 normalize(m){
  if(!Object.hasOwn(TYPES,m.kind))throw Error('알 수 없는 배치 종류야.');
  const v={...m};for(const k of ['x','y','w','h'])if(!Number.isFinite(Number(v[k])))throw Error('좌표와 크기는 숫자로 입력해줘.');
  v.w=clamp(snap(Number(v.w)),32,this.plan.width);v.h=clamp(snap(Number(v.h)),32,this.plan.height);
  if(['platform','dropfloor','trampoline'].includes(v.kind))v.h=32;
  v.x=clamp(snap(Number(v.x)),0,this.plan.width-v.w);v.y=clamp(snap(Number(v.y)),0,this.plan.height-v.h);
  v.name=String(v.name||TYPES[v.kind].label).slice(0,100);v.note=String(v.note||'').slice(0,1000);
  if(v.kind==='trampoline')v.boostTiles=clamp(Math.round(Number(v.boostTiles)||24),1,100);
  if(v.kind==='dropfloor')v.dropKey='ArrowDown+KeyS';return v;
 }
 update(id,patch){const i=this.markers.findIndex(m=>m.id===id);if(i<0)return;const before=this.snapshot();const safe={};for(const k of ['name','note','kind','x','y','w','h','boostTiles'])if(Object.hasOwn(patch,k))safe[k]=patch[k];this.markers[i]=this.normalize({...this.markers[i],...safe});if(!this.transaction)this.record(before);return this.markers[i];}
 add(kind,x,y){if(!Object.hasOwn(TYPES,kind))throw Error('배치 종류를 선택해줘.');const before=this.snapshot();let id;do{id='user'+(++this.sequence);}while(this.get(id));const t=TYPES[kind];const m=this.normalize({id,kind,name:t.label,x,y,w:t.w,h:t.h,note:'사용자 배치'});this.markers.push(m);this.selected=id;this.record(before);return m;}
 remove(id=this.selected){if(!this.get(id))return;const before=this.snapshot();this.markers=this.markers.filter(m=>m.id!==id);this.selected=null;this.record(before);}
 undo(){this.end();if(!this.undoStack.length)return false;this.redoStack.push(this.snapshot());this.restore(this.undoStack.pop());return true;}
 redo(){if(!this.redoStack.length)return false;this.undoStack.push(this.snapshot());this.restore(this.redoStack.pop());return true;}
 reset(){const before=this.snapshot();this.markers=clone(this.defaults);this.selected=null;this.record(before);}
 export(){return {schema:'forest-layout-markers-v1',mapId:this.plan.id,width:this.plan.width,height:this.plan.height,markers:clone(this.markers)};}
 import(value,record=true){
  if(value?.schema!=='forest-layout-markers-v1'||value.mapId!==this.plan.id||value.width!==this.plan.width||value.height!==this.plan.height)throw Error('현재 지형 버전에서 저장한 JSON을 선택해줘. 이전 지형 JSON은 함께 보관한 이전 지형용 편집기에서 열어줘.');
  if(!Array.isArray(value.markers)||value.markers.length>1024)throw Error('배치 목록이 올바르지 않아.');const ids=new Set(),next=[];
  for(const m of value.markers){
   if(!m||typeof m.id!=='string'||!m.id||m.id.length>100||ids.has(m.id)||!Object.hasOwn(TYPES,m.kind))throw Error('중복되거나 잘못된 배치 항목이 있어.');
   ids.add(m.id);for(const k of ['x','y','w','h'])if(typeof m[k]!=='number'||!Number.isFinite(m[k])||m[k]%32!==0)throw Error('좌표는 32px 격자에 맞는 숫자여야 해.');
   if(m.w<32||m.h<32||m.x<0||m.y<0||m.x+m.w>this.plan.width||m.y+m.h>this.plan.height)throw Error('맵 밖으로 나가는 배치 영역이 있어.');
   if(['platform','dropfloor','trampoline'].includes(m.kind)&&m.h!==32)throw Error('발판과 장치의 표시 높이는 1칸이어야 해.');
   next.push(this.normalize({id:m.id,kind:m.kind,name:m.name,note:m.note,x:m.x,y:m.y,w:m.w,h:m.h,boostTiles:m.boostTiles}));
  }
  const before=this.snapshot();this.markers=next;this.selected=null;if(record)this.record(before);
 }
 hit(x,y,pad=0){return [...this.markers].reverse().find(m=>x>=m.x-pad&&x<=m.x+m.w+pad&&y>=m.y-pad&&y<=m.y+m.h+pad);}
}
const api={EditorModel,TYPES,snap,clamp};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.MapEditorCore=api;
})(typeof globalThis!=='undefined'?globalThis:this);
