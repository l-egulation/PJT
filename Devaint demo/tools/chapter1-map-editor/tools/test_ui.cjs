// Event wiring test in a small DOM harness. This is not a browser render test.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const ROOT=path.resolve(__dirname,'..');
const legacy='deviant-layout:A-expanded-v1-20261020',legacyText=JSON.stringify({mapId:'A-expanded-v1-20261020',markers:[{name:'이전 편집 보존'}]});const storage=new Map([[legacy,legacyText]]);
const previousTerrain='deviant-map-editor-v2:A-24-spaces-v2-20261020',previousText=JSON.stringify({mapId:'A-24-spaces-v2-20261020',terrain:{changes:[[1,0]]}});storage.set(previousTerrain,previousText);
async function boot(){
 const elements=new Map(),downloads=[],blobs=[];
 class Element{
  constructor(tag='div',id=''){this.tagName=tag.toUpperCase();this.id=id;this.value='';this.checked=false;this.hidden=false;this.style={};this.children=[];this.listeners={};this.files=[];this.width=id==='scene'?960:1344;this.height=id==='scene'?540:450;this.clientWidth=id==='mapScroll'?1200:960;this.clientHeight=450;this.scrollLeft=this.scrollTop=0;const classes=new Set();this.classList={toggle:(k,v)=>v===undefined?(classes.has(k)?classes.delete(k):classes.add(k)):v?classes.add(k):classes.delete(k),add:k=>classes.add(k),contains:k=>classes.has(k)};}
  append(...v){this.children.push(...v);}replaceChildren(...v){this.children=v;}toBlob(cb){cb(new Blob(['png-test']));}remove(){}setPointerCapture(){}click(){downloads.push(this.download||'file-picker');}
  addEventListener(n,f){(this.listeners[n]??=[]).push(f);}getBoundingClientRect(){return{left:0,top:0,width:this.id==='map'?this.width:960,height:this.id==='map'?this.height:540};}
  getContext(){if(this.ctx)return this.ctx;const self=this,raster=(w,h)=>({data:new Uint8ClampedArray(w*h*4),width:w,height:h});this.ctx=new Proxy({canvas:self,measureText:t=>({width:String(t).length*7}),createImageData:raster,getImageData:(_x,_y,w,h)=>raster(w,h)},{get(o,k){if(k in o)return o[k];return(...args)=>{if(k==='drawImage')for(const n of args.slice(1))assert.ok(Number.isFinite(n),'non-finite canvas coordinate');};},set(o,k,v){o[k]=v;return true;}});return this.ctx;}
 }
  const standalone=!process.argv.includes('--source'),htmlArg=process.argv.indexOf('--html');const html=fs.readFileSync(htmlArg>=0?process.argv[htmlArg+1]:ROOT+(standalone?'/맵 편집기.html':'/index.html'),'utf8');for(const match of html.matchAll(/<([a-z]+)\b([^>]*\bid="([^"]+)"[^>]*)>/g)){const e=new Element(match[1],match[3]);e.checked=/\bchecked\b/.test(match[2]);e.hidden=/\bhidden\b/.test(match[2]);elements.set(e.id,e);}
 elements.get('filter').value='all';elements.get('terrainTool').value='paint';elements.get('terrainMaterial').value='1';elements.get('brushSize').value='1';
 const document={body:new Element('body'),activeElement:null,listeners:{},querySelector:s=>{assert.ok(s.startsWith('#'));assert.ok(elements.has(s.slice(1)),s);return elements.get(s.slice(1));},createElement:t=>new Element(t),addEventListener(n,f){this.listeners[n]=f;}};
 class FakeImage{set src(v){if(v.startsWith('data:image/png;base64,')){const b=Buffer.from(v.split(',')[1],'base64');assert.equal(b.subarray(1,4).toString(),'PNG');}else assert.ok(fs.existsSync(path.join(ROOT,v)),v);this.url=v;}async decode(){}}
 const window={};const context=vm.createContext({window,document,Image:FakeImage,console,Blob,confirm:()=>true,URL:{createObjectURL:b=>(blobs.push(b),'blob:test'),revokeObjectURL(){}},setTimeout(){},requestAnimationFrame(){},localStorage:{getItem:k=>storage.get(k),setItem:(k,v)=>storage.set(k,v)},performance:{now:()=>0}});
 window.addEventListener=()=>{};if(standalone){assert.ok(!html.includes('<script src='));for(const m of html.matchAll(/<script>([\s\S]*?)<\/script>/g)){vm.runInContext(m[1],context);if(context.MapEditorCore)window.MapEditorCore=context.MapEditorCore;if(context.TerrainEditor)window.TerrainEditor=context.TerrainEditor;if(context.MapTileArt)window.MapTileArt=context.MapTileArt;}}else{const bundle=fs.readFileSync(ROOT+'/맵 편집기.html','utf8');window.INLINE_ASSETS=JSON.parse(bundle.match(/window\.INLINE_ASSETS=(\{[^<]+\});<\/script>/)[1]);for(const file of ['data/map.js','editor/core.js','data/terrain-rules.js','editor/terrain.js','editor/tile-art.js'])vm.runInContext(fs.readFileSync(ROOT+'/'+file,'utf8'),context,{filename:file});window.MapEditorCore=context.MapEditorCore;window.TerrainEditor=context.TerrainEditor;window.MapTileArt=context.MapTileArt;vm.runInContext(fs.readFileSync(ROOT+'/editor/app.js','utf8'),context,{filename:'app-editor.js'});}await new Promise(setImmediate);await new Promise(setImmediate);
 assert.ok(window.__layoutEditor,'startup failed: '+elements.get('loading').textContent);assert.equal(elements.get('loading').hidden,true);
 return{elements,editor:window.__layoutEditor,downloads,blobs,document};
}
(async()=>{const app=await boot(),{elements:E,editor}=app,model=editor.model,map=E.get('map');
 if(process.argv.includes('--compat')){
  const baseline=JSON.parse(JSON.stringify(model.export()));
  E.get('addKind').value='trampoline';E.get('add').onclick();assert.equal(model.get().kind,'trampoline');
  E.get('save').onclick();const saved=JSON.parse(await app.blobs.at(-1).text());assert.equal(saved.markers.length,baseline.markers.length+1);
  E.get('remove').onclick();assert.equal(model.markers.length,baseline.markers.length);
  const file=E.get('file');file.files=[{size:100,text:async()=>JSON.stringify(saved)}];await file.onchange({target:file});
  assert.deepEqual(JSON.parse(JSON.stringify(model.export())),saved,'compatibility editor JSON save/load round trip');
  const stable=JSON.stringify(model.export());file.files=[{size:10,text:async()=>'{"schema":"invalid"}'}];await file.onchange({target:file});assert.equal(JSON.stringify(model.export()),stable);
  console.log(JSON.stringify({passed:true,environment:'Node DOM harness; no browser renderer',checks:['compatibility bundle startup','embedded PNG decode','add/remove marker','JSON download contents','JSON load round trip','invalid import preserves edits']}));return;
 }
 assert.equal(map.width,1200,'whole map fits the available panel');const item=model.markers.find(m=>m.kind==='elite'),before={...item};model.selected=item.id;editor.refresh();assert.equal(E.get('properties').hidden,false);
 const event=(x,y)=>({currentTarget:map,pointerId:1,clientX:x*map.width/model.plan.width,clientY:y*map.height/model.plan.height});map.onpointerdown(event(item.x+32,item.y+32));map.onpointermove(event(item.x+128,item.y+96));map.onpointerup({});assert.equal(model.get(item.id).x,before.x+96);assert.equal(model.get(item.id).y,before.y+64);E.get('undo').onclick();assert.equal(model.get(item.id).x,before.x);E.get('redo').onclick();assert.equal(model.get(item.id).x,before.x+96);
 const r={...model.get(item.id)};map.onpointerdown(event(r.x+r.w,r.y+r.h));map.onpointermove(event(r.x+r.w+64,r.y+r.h+32));map.onpointerup({});assert.equal(model.get(item.id).w,r.w+64);
 E.get('addKind').value='trampoline';E.get('add').onclick();const added=model.get();assert.equal(added.kind,'trampoline');assert.equal(E.get('boostRow').hidden,false);E.get('name').value='사용자 트램펄린';E.get('name').listeners.change[0]();assert.equal(model.get().name,'사용자 트램펄린');E.get('remove').onclick();assert.equal(model.get(added.id),undefined);E.get('undo').onclick();assert.ok(model.get(added.id));E.get('save').onclick();assert.equal(app.downloads.at(-1),'chapter1-map-edited.json');assert.equal(JSON.parse(await app.blobs.at(-1).text()).markers.length,model.markers.length);
 model.selected=added.id;editor.refresh();app.document.activeElement=E.get('name');E.get('name').value='입력 중 저장한 이름';let prevented=false;app.document.listeners.keydown({ctrlKey:true,key:'s',preventDefault(){prevented=true;}});assert.ok(prevented);assert.equal(model.get(added.id).name,'입력 중 저장한 이름');assert.ok(E.get('movementStats').textContent.includes(model.markers.filter(m=>m.kind==='trampoline').length+'곳'));
 assert.equal(E.get('previousDraft').hidden,false);E.get('previousDraft').onclick();assert.equal(storage.get(legacy),legacyText);assert.equal(app.downloads.at(-1),'deviant-previous-8-spaces-placements.json');
 assert.equal(E.get('previousTerrainDraft').hidden,false);E.get('previousTerrainDraft').onclick();assert.equal(app.downloads.at(-1),'chapter1-previous-terrain-draft.json');assert.equal(storage.get(previousTerrain),previousText);assert.notEqual(model.plan.id,JSON.parse(previousText).mapId);assert.equal(model.terrain.edits.size,0,'old terrain changes must not apply to the new base');
 const expected=model.export();const restored=await boot();assert.deepEqual(JSON.parse(JSON.stringify(restored.editor.model.export())),JSON.parse(JSON.stringify(expected)));
 const file=E.get('file');file.files=[{size:10,text:async()=>'{"schema":"wrong"}'}];await file.onchange({target:file});assert.deepEqual(model.export(),expected);assert.ok(E.get('saveStatus').textContent.includes('불러오지 못했어'));

 // Terrain paint: continuous strokes, right-click erasure, rectangles, cancel,
 // history and export/import all share the same workspace document.
 const terrain=model.terrain;
 editor.setMode('terrain');assert.equal(E.get('terrainTools').hidden,false);
 const point=(x,y,button=0)=>({...event((x+.5)*32,(y+.5)*32),button,preventDefault(){}});
 const tx=18,ty=60;assert.equal(terrain.value(tx,ty),0);
 map.onpointerdown(point(tx,ty));map.onpointermove(point(tx+6,ty));map.onpointerup({});
 for(let x=tx;x<=tx+6;x++)assert.equal(terrain.value(x,ty),1,'stroke must not skip cells');
 assert.equal(terrain.value(tx-1,ty),0);assert.ok(terrain.tile(tx,ty)!==undefined);
 const painted=model.export();E.get('undo').onclick();assert.equal(terrain.value(tx,ty),0);E.get('redo').onclick();assert.equal(terrain.value(tx,ty),1);
 map.onpointerdown(point(tx,ty,2));map.onpointerup({});assert.equal(terrain.value(tx,ty),0,'right-click erases');
 E.get('terrainTool').value='rectPaint';map.onpointerdown(point(tx,ty+1));map.onpointermove(point(tx+3,ty+3));map.onpointerup({});
 for(let y=ty+1;y<=ty+3;y++)for(let x=tx;x<=tx+3;x++)assert.equal(terrain.value(x,y),1);
 const beforeCancel=JSON.stringify(model.export());E.get('terrainTool').value='paint';map.onpointerdown(point(tx+10,ty));map.onpointercancel();assert.equal(JSON.stringify(model.export()),beforeCancel,'cancelled stroke restores terrain');
 E.get('terrainTool').value='rectErase';map.onpointerdown(point(tx,ty+1));map.onpointermove(point(tx+3,ty+3));map.onpointerup({});assert.equal(terrain.value(tx+2,ty+2),0);
 E.get('terrainTool').value='paint';E.get('terrainMaterial').value='2';map.onpointerdown(point(tx,ty));map.onpointerup({});assert.equal(terrain.value(tx,ty),2);
 editor.drawFrame(1000);editor.drawMap();E.get('exportPng').onclick();assert.equal(app.downloads.at(-1),'chapter1-map-overview.png');E.get('save').onclick();assert.equal(JSON.parse(await app.blobs.at(-1).text()).schema,'forest-map-document-v2');
 const documentBefore=model.export();const reopened=await boot();assert.deepEqual(JSON.parse(JSON.stringify(reopened.editor.model.export())),JSON.parse(JSON.stringify(documentBefore)),'terrain autosave reload');
 file.files=[{size:100,text:async()=>JSON.stringify(painted)}];await file.onchange({target:file});assert.equal(terrain.value(tx,ty),1,'full map file loads terrain');
 const legacyMarkers={...painted,schema:'forest-layout-markers-v1'};delete legacyMarkers.terrain;
 const geoBefore=JSON.stringify(terrain.snapshot());model.import(legacyMarkers);assert.equal(JSON.stringify(terrain.snapshot()),geoBefore,'old placement file preserves terrain');
 const stable=JSON.stringify(model.export()),broken={...painted,terrain:{...painted.terrain,changes:[[0,1],[0,2]]}};
 file.files=[{size:100,text:async()=>JSON.stringify(broken)}];await file.onchange({target:file});assert.equal(JSON.stringify(model.export()),stable,'invalid terrain import is atomic');
 // Editing the detailed view also works at CSS-scaled coordinates.
 editor.setCamera(0,1700);E.get('terrainTool').value='paint';E.get('terrainMaterial').value='1';const sc=E.get('scene'),cp=editor.camera;
 const se={currentTarget:sc,pointerId:2,button:0,clientX:tx*32+16-cp.x,clientY:(ty+4)*32+16-cp.y,preventDefault(){}};
 sc.onpointerdown(se);sc.onpointerup({});assert.equal(terrain.value(tx,ty+4),1);
 console.log(JSON.stringify({passed:true,environment:'Node DOM harness; no browser renderer',checks:['asset startup','fit width','pointer drag','resize handle','undo/redo buttons','add/rename/delete','download JSON contents','Ctrl+S commits focused input','live feature counts','autosave reload','invalid file preserves edits','previous-map edits retained and exportable','continuous tile strokes','right-click eraser','rectangle fill/delete','sketch material','terrain undo/redo','cancelled stroke rollback','terrain JSON round trip','terrain autosave reload','legacy marker import','invalid terrain atomicity','detailed viewport painting']}));
})().catch(e=>{console.error(e);process.exitCode=1;});
