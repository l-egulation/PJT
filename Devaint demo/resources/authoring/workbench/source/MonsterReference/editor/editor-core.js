(function(root){
'use strict';
const DEFAULT_PALETTE=['#025673','#02648C','#1B1616','#D2B396','#EBA516','#FDEEDA'];
const clone=value=>JSON.parse(JSON.stringify(value));
const same=(a,b)=>a.length===b.length&&a.every((v,i)=>v===b[i]);
const validImage=value=>typeof value==='string'&&/^data:image\/png;base64,/.test(value);
function normalize(raw){
 if(!raw||typeof raw!=='object')throw Error('편집 JSON 객체가 필요합니다.');
 const field=raw.bodyImages?'bodyImages':raw.images?'images':raw.image?'image':null;
 if(!field)throw Error('몸체·무기 분리본, 여러 프레임 images, 단일 image JSON을 지원합니다.');
 const sources=field==='image'?{[String(raw.sourceFrame??0)]:raw.image}:raw[field];
 const rawIds=Array.isArray(raw.sourceFrames)?raw.sourceFrames:field==='image'?[raw.sourceFrame??0]:Object.keys(sources);
 const ids=rawIds.map(String),width=raw.width??128,height=raw.height??128;
 if(!Number.isInteger(width)||!Number.isInteger(height)||width<1||height<1||width>512||height>512)throw Error('지원 이미지 크기는 1~512픽셀입니다.');
 if(!ids.length||ids.length>120||new Set(ids).size!==ids.length||width*height*ids.length>16000000)throw Error('프레임 수 또는 ID를 확인해 주세요.');
 if(ids.some(id=>!validImage(sources[id])))throw Error('각 프레임에 내장 PNG 이미지가 필요합니다.');
 if(raw.weaponImage&&!validImage(raw.weaponImage))throw Error('무기 이미지는 내장 PNG여야 합니다.');
 const ms=ids.map((id,i)=>Array.isArray(raw.ms)?raw.ms[i]:100);
 if(ms.some(v=>!Number.isFinite(v)||v<=0||v>30000))throw Error('프레임 재생 시간이 올바르지 않습니다.');
 const palette=Array.isArray(raw.palette)&&raw.palette.length?raw.palette:DEFAULT_PALETTE;
 if(palette.some(v=>!/^#[0-9a-fA-F]{6}$/.test(v)))throw Error('팔레트 색상을 확인해 주세요.');
 return {raw:clone(raw),field,sources,rawIds,ids,width,height,ms,palette};
}
function rgba(hex){return [...hex.slice(1).match(/../g).map(v=>parseInt(v,16)),255]}
function plane(width,height,data){return {width,height,data:new Uint8ClampedArray(data||width*height*4)}}
function allowedColor(palette,color){return color.length===4&&color.every(Number.isInteger)&&(color[3]===0&&color.slice(0,3).every(v=>v===0)||color[3]===255&&palette.some(hex=>same(rgba(hex),color)))}
function validatePlane(palette,p){for(let i=0;i<p.data.length;i+=4)if(!allowedColor(palette,Array.from(p.data.subarray(i,i+4))))throw Error('마스터 팔레트 밖의 색상 또는 반투명 픽셀은 사용할 수 없습니다.')}
function blend(dst,offset,src,source){
 const a=src[source+3]/255;if(a===0)return;if(a===1){for(let j=0;j<4;j++)dst[offset+j]=src[source+j];return}
 const b=dst[offset+3]/255,out=a+b*(1-a);for(let j=0;j<3;j++)dst[offset+j]=Math.round((src[source+j]*a+dst[offset+j]*b*(1-a))/out);dst[offset+3]=Math.round(out*255);
}
class Model{
 constructor(def,loaded,weapon){
  this.def=def;this.width=def.width;this.height=def.height;this.ids=def.ids;this.ms=def.ms;this.index=0;this.bodies=new Map();this.alias=new Map();this.placements=new Map();this.undoStack=[];this.redoStack=[];this.pending=null;this.notes=[];
  for(const id of this.ids){const v=loaded[id];if(!v||v.width!==this.width||v.height!==this.height)throw Error('프레임 크기가 JSON과 다릅니다: F'+id);this.bodies.set(id,plane(v.width,v.height,v.data));this.alias.set(id,id);const p=def.raw.placements?.[id]||{};const pos={x:p.x??this.width/2,y:p.y??this.height/2,angle:p.angle??0,visible:p.visible??!!weapon};if(![pos.x,pos.y,pos.angle].every(Number.isFinite))throw Error('무기 배치 값을 확인해 주세요.');this.placements.set(id,{...pos,visible:!!pos.visible})}
  this.weapon=weapon?plane(weapon.width,weapon.height,weapon.data):null;
  const groups=def.raw.sharedFrameGroups||(Array.isArray(def.raw.sharedIdleFrames)?[def.raw.sharedIdleFrames]:[]);
  for(const group of groups){const keys=group.map(String);if(keys.length<2||keys.some(k=>!this.bodies.has(k)))continue;const first=keys[0];if(keys.every(k=>same(this.bodies.get(first).data,this.bodies.get(k).data)&&JSON.stringify(this.placements.get(first))===JSON.stringify(this.placements.get(k))))for(const id of keys)this.alias.set(id,first);else this.notes.push('공유 프레임의 수정 내용이 달라 개별 상태로 유지했습니다.')}
  this.paletteLocked=def.raw.paletteLock===true;
  if(this.paletteLocked){for(const body of this.bodies.values())validatePlane(def.palette,body);if(this.weapon)validatePlane(def.palette,this.weapon)}
  this.originalPlacements=new Map([...this.placements].map(([k,p])=>[k,{...p}]));
 }
 key(id=this.ids[this.index]){return this.alias.get(String(id))||String(id)}
 body(id=this.ids[this.index]){return this.bodies.get(this.key(id))}
 placement(id=this.ids[this.index]){return this.placements.get(this.key(id))}
 target(layer,id=this.ids[this.index]){return layer==='weapon'?this.weapon:this.body(id)}
 select(i){this.index=(i+this.ids.length)%this.ids.length}
 begin(layer){this.commit();const id=this.key(),p=this.target(layer);this.pending={index:this.index,id,layer,before:layer==='placement'?{...this.placement()}:p?new Uint8ClampedArray(p.data):null}}
 commit(){const r=this.pending;this.pending=null;if(!r||r.before===null)return false;const p=r.layer==='placement'?this.placements.get(r.id):this.target(r.layer,r.id);r.after=r.layer==='placement'?{...p}:new Uint8ClampedArray(p.data);const equal=r.layer==='placement'?JSON.stringify(r.before)===JSON.stringify(r.after):same(r.before,r.after);if(equal)return false;this.undoStack.push(r);if(this.undoStack.length>100)this.undoStack.shift();this.redoStack=[];return true}
 travel(from,to,side){const r=from.pop();if(!r)return null;this.index=r.index;if(r.layer==='placement')this.placements.set(r.id,{...r[side]});else this.target(r.layer,r.id).data.set(r[side]);to.push(r);return r}
 undo(){this.commit();return this.travel(this.undoStack,this.redoStack,'before')}
 redo(){this.commit();return this.travel(this.redoStack,this.undoStack,'after')}
 paint(layer,x,y,color,size=1){const p=this.target(layer);if(!p)return;if(this.paletteLocked&&!allowedColor(this.def.palette,color))throw Error('마스터 팔레트의 색상만 사용할 수 있습니다.');const o=Math.floor((size-1)/2);for(let dy=0;dy<size;dy++)for(let dx=0;dx<size;dx++){const px=x-o+dx,py=y-o+dy;if(px<0||py<0||px>=p.width||py>=p.height)continue;const at=(py*p.width+px)*4;p.data.set(color,at)}}
 pick(layer,x,y){const p=this.target(layer);if(!p||x<0||y<0||x>=p.width||y>=p.height)return null;return Array.from(p.data.slice((y*p.width+x)*4,(y*p.width+x)*4+4))}
 fill(layer,x,y,color){const p=this.target(layer),from=this.pick(layer,x,y);if(!p||!from||same(from,color))return;if(this.paletteLocked&&!allowedColor(this.def.palette,color))throw Error('마스터 팔레트의 색상만 사용할 수 있습니다.');const q=[[x,y]];while(q.length){const [a,b]=q.pop();if(a<0||b<0||a>=p.width||b>=p.height)continue;const at=(b*p.width+a)*4;if(!from.every((v,i)=>v===p.data[at+i]))continue;p.data.set(color,at);q.push([a-1,b],[a+1,b],[a,b-1],[a,b+1])}}
 weaponPoint(x,y,id=this.ids[this.index]){if(!this.weapon)return null;const p=this.placement(id),a=p.angle*Math.PI/180,co=Math.cos(a),si=Math.sin(a),dx=x+.5-p.x,dy=y+.5-p.y;return [Math.floor(co*dx+si*dy+this.weapon.width/2),Math.floor(-si*dx+co*dy+this.weapon.height/2)]}
 weaponLayer(id=this.ids[this.index],force=false){const out=plane(this.width,this.height);if(!this.weapon||!force&&!this.placement(id).visible)return out;for(let y=0;y<this.height;y++)for(let x=0;x<this.width;x++){const [u,v]=this.weaponPoint(x,y,id);if(u<0||v<0||u>=this.weapon.width||v>=this.weapon.height)continue;const source=(v*this.weapon.width+u)*4,target=(y*this.width+x)*4;out.data.set(this.weapon.data.subarray(source,source+4),target)}return out}
 render(id=this.ids[this.index],view='composite',force=false){const out=view==='weapon'?plane(this.width,this.height):plane(this.width,this.height,this.body(id).data);if(view!=='body'){const top=this.weaponLayer(id,force);for(let at=0;at<out.data.length;at+=4)blend(out.data,at,top.data,at)}return out}
 export(encode){if(this.paletteLocked){for(const body of this.bodies.values())validatePlane(this.def.palette,body);if(this.weapon)validatePlane(this.def.palette,this.weapon)}const out=clone(this.def.raw),images=Object.fromEntries(this.ids.map(id=>[id,encode(this.body(id))]));if(this.def.field==='image')out.image=images[this.ids[0]];else out[this.def.field]=images;if(this.weapon){out.weaponImage=encode(this.weapon);out.placements=Object.fromEntries(this.ids.map(id=>[id,{...this.placement(id)}]))}return out}
}
const api={normalize,rgba,plane,same,Model,DEFAULT_PALETTE};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.PixelEditorCore=api;
})(typeof window!=='undefined'?window:globalThis);
