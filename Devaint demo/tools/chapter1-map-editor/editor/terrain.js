(function(root){
'use strict';
const {EditorModel,clamp}=root.MapEditorCore,copy=v=>JSON.parse(JSON.stringify(v));
const bits=n=>{let c=0;for(;n;n>>>=1)c+=n&1;return c;};
class TerrainModel{
 constructor(plan,rules){
  this.cols=rules.cols;this.rows=rules.rows;this.rules=rules;this.total=this.cols*this.rows;
  this.base=Uint8Array.from(rules.ground,Number);this.edits=new Map();this.visual=new Map();this.baseVisual=new Map();this.dirty=new Set();this.cache=new Map();
  for(const [x,y,id] of plan.cells)if(this.inside(x,y))this.baseVisual.set(y*this.cols+x,id);
  this.visual=new Map(this.baseVisual);this.revision=0;
 }
 inside(x,y){return Number.isInteger(x)&&Number.isInteger(y)&&x>=0&&y>=0&&x<this.cols&&y<this.rows;}
 value(x,y){if(!this.inside(x,y))return 0;const k=y*this.cols+x;return this.edits.has(k)?this.edits.get(k):this.base[k];}
 mask(x,y,r=1){let mask=0;for(let dy=-r;dy<=r;dy++)for(let dx=-r;dx<=r;dx++)if(this.value(x+dx,y+dy))mask|=1<<((dy+r)*(2*r+1)+dx+r);return mask;}
 tile(x,y){return this.visual.get(y*this.cols+x);}
 snapshot(){return [...this.edits].sort((a,b)=>a[0]-b[0]);}
 validate(entries){
  if(!Array.isArray(entries)||entries.length>this.total)throw Error('지형 편집 데이터가 너무 크거나 잘못됐어.');
  const seen=new Set();return entries.map(entry=>{
   if(!Array.isArray(entry)||entry.length!==2)throw Error('지형 칸 형식을 확인해줘.');
   const [k,v]=entry;if(!Number.isInteger(k)||k<0||k>=this.total||![0,1,2].includes(v)||seen.has(k))throw Error('맵 밖 또는 중복된 지형 칸이 있어.');
   seen.add(k);return [k,v];
  });
 }
 restore(entries){
  const next=this.validate(entries),affected=new Set([...this.edits.keys(),...next.map(e=>e[0])]);
  this.edits=new Map(next.filter(([k,v])=>v!==this.base[k]));
  this.visual=new Map(this.baseVisual);this.cache.clear();
  this.retile(new Set(this.edits.keys()));
  for(const k of affected){const x=k%this.cols,y=Math.floor(k/this.cols);for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++)if(this.inside(x+dx,y+dy))this.dirty.add((y+dy)*this.cols+x+dx);}
  this.revision++;
 }
 choose(x,y){
  const m=this.mask(x,y);if(!m)return undefined;
  let mat=this.value(x,y);if(!mat){mat=1;for(let dx=-1;dx<=1;dx++)if(this.value(x+dx,y+1)===2)mat=2;}
  const m5=this.mask(x,y,2),cacheKey=mat+':'+m+':'+m5+':'+(x%3)+':'+(y%3);
  if(this.cache.has(cacheKey))return this.cache.get(cacheKey);
  let cat=this.rules.catalogs[mat];
  // Sketch source art supports flat surfaces only: keep actual wall/corner roles
  // from the normal library instead of substituting the wrong geometry.
  if(mat===2&&!cat[m])cat=this.rules.catalogs[1];
  let choices=cat[m];
  if(!choices){let best=Infinity,mask=0;for(const key of Object.keys(cat)){
   const k=Number(key),cost=(((k^m)&16)!==0?1000:0)+bits((k^m)&186)*50+bits(k^m);
   if(cost<best){best=cost;mask=k;}
  }choices=cat[mask];}
  let score=Infinity,id;
  for(const [tile,n5,px,py] of choices){const cost=bits(n5^m5)*8+(px!==x%3?2:0)+(py!==y%3?1:0);if(cost<score||(cost===score&&tile<id)){score=cost;id=tile;}}
  // The interior must remain solid even for narrow or newly carved shapes.
  this.cache.set(cacheKey,id);return id;
 }
 retile(keys){
  const around=new Set();for(const k of keys){const x=k%this.cols,y=Math.floor(k/this.cols);for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++)if(this.inside(x+dx,y+dy))around.add((y+dy)*this.cols+x+dx);}
  for(const k of around){const x=k%this.cols,y=Math.floor(k/this.cols);let affected=false;
   for(let dy=-2;dy<=2&&!affected;dy++)for(let dx=-2;dx<=2;dx++)if(this.inside(x+dx,y+dy)&&this.edits.has((y+dy)*this.cols+x+dx)){affected=true;break;}
   const id=affected?this.choose(x,y):this.baseVisual.get(k);if(id===undefined)this.visual.delete(k);else this.visual.set(k,id);this.dirty.add(k);
  }
  if(around.size)this.revision++;
 }
 paint(points,value,size=1){
  if(![0,1,2].includes(value))throw Error('올바른 지형 재질을 선택해줘.');
  const changed=new Set(),radius=Math.floor(size/2);
  for(const [x,y] of points)for(let dy=-radius;dy<=radius;dy++)for(let dx=-radius;dx<=radius;dx++){
   const a=x+dx,b=y+dy;if(!this.inside(a,b)||this.value(a,b)===value)continue;
   const k=b*this.cols+a;if(this.base[k]===value)this.edits.delete(k);else this.edits.set(k,value);changed.add(k);
  }
  this.retile(changed);return changed.size;
 }
 rectangle(a,b,value){const points=[];for(let y=Math.max(0,Math.min(a[1],b[1]));y<=Math.min(this.rows-1,Math.max(a[1],b[1]));y++)for(let x=Math.max(0,Math.min(a[0],b[0]));x<=Math.min(this.cols-1,Math.max(a[0],b[0]));x++)points.push([x,y]);return this.paint(points,value);}
 line(a,b,value,size){
  let [x,y]=a;const dx=Math.abs(b[0]-x),sx=x<b[0]?1:-1,dy=-Math.abs(b[1]-y),sy=y<b[1]?1:-1;let err=dx+dy;const points=[];
  for(;;){points.push([x,y]);if(x===b[0]&&y===b[1])break;const e=err*2;if(e>=dy){err+=dy;x+=sx;}if(e<=dx){err+=dx;y+=sy;}}
  return this.paint(points,value,size);
 }
}
class WorkspaceModel extends EditorModel{
 constructor(plan,markers,rules){super(plan,markers);this.terrain=new TerrainModel(plan,rules);}
 snapshot(){return {markers:copy(this.markers),selected:this.selected,terrain:this.terrain.snapshot()};}
 restore(s){this.markers=copy(s.markers);this.selected=s.selected;this.terrain.restore(s.terrain||[]);}
 record(before){super.record(before);while(this.undoStack.length>1&&this.undoStack.reduce((n,s)=>n+s.terrain.length,0)>350000)this.undoStack.shift();}
 resetTerrain(){const before=this.snapshot();this.terrain.restore([]);this.record(before);}
 resetAll(){const before=this.snapshot();this.markers=copy(this.defaults);this.selected=null;this.terrain.restore([]);this.record(before);}
 export(){return {...super.export(),schema:'forest-map-document-v2',tileSize:32,terrain:{cols:this.terrain.cols,rows:this.terrain.rows,changes:this.terrain.snapshot()}};}
 import(value,record=true){
  if(value?.schema==='forest-layout-markers-v1'){super.import(value,record);return 'markers';}
  if(value?.schema!=='forest-map-document-v2'||value.tileSize!==32||value.terrain?.cols!==this.terrain.cols||value.terrain?.rows!==this.terrain.rows)throw Error('이 편집기에서 저장한 전체 맵 JSON을 선택해줘.');
  const terrain=this.terrain.validate(value.terrain.changes),before=this.snapshot();
  // Marker validation is atomic in the parent model; terrain validation happens first.
  super.import({...value,schema:'forest-layout-markers-v1'},false);this.terrain.restore(terrain);if(record)this.record(before);return 'map';
 }
}
root.TerrainEditor={TerrainModel,WorkspaceModel};if(typeof module!=='undefined'&&module.exports)module.exports=root.TerrainEditor;
})(typeof globalThis!=='undefined'?globalThis:this);
