const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),box={console,window:{}};vm.createContext(box);
for(const f of ['data/map.js','data/terrain-rules.js','editor/core.js','editor/terrain.js'])vm.runInContext(fs.readFileSync(path.join(root,f),'utf8'),box);
const DATA=box.window.EXPANDED_MAP,RULES=box.window.TERRAIN_RULES,{WorkspaceModel}=box.TerrainEditor;
const make=()=>new WorkspaceModel(DATA.plan,DATA.markers,RULES),model=make(),t=model.terrain;
const baseline=JSON.stringify([...t.visual].sort((a,b)=>a[0]-b[0]));
assert.equal(t.edits.size,0);assert.equal(t.total,576*152);
// Carve out a genuine chamber inside solid terrain and reconnect its perimeter.
const boundary=[t.tile(18,5),t.tile(19,5)];assert.equal(t.value(20,6),1);
model.begin();t.rectangle([20,6],[29,12],0);model.end();assert.equal(t.value(20,6),0);assert.equal(t.value(19,6),1);
assert.notDeepEqual([t.tile(18,5),t.tile(19,5)],boundary,'new cavity changes nearby edge art');
assert.equal(t.tile(25,9),undefined,'empty interior has no stale ground pixels');
const cavern=model.export();const fresh=make();fresh.import(JSON.parse(JSON.stringify(cavern)));
assert.equal(JSON.stringify([...fresh.terrain.visual].sort((a,b)=>a[0]-b[0])),JSON.stringify([...t.visual].sort((a,b)=>a[0]-b[0])),'reload produces identical visible tiles');
model.undo();assert.equal(JSON.stringify([...t.visual].sort((a,b)=>a[0]-b[0])),baseline,'undo restores the exact baseline tile IDs');
model.redo();assert.equal(t.value(25,9),0);
// Filling an erased region back to the original material must restore its exact baseline IDs.
t.rectangle([20,6],[29,12],1);assert.equal(t.edits.size,0);assert.equal(JSON.stringify([...t.visual].sort((a,b)=>a[0]-b[0])),baseline);
// World boundaries, untrusted imports and history must all preserve valid state.
t.paint([[-1,-1]],1);assert.equal(t.edits.size,0);
const stable=JSON.stringify(model.export());for(const entries of [[[t.total,1]],[[0,7]],[[0,1],[0,2]],[[1.2,1]],[[0,'2']]]){
 const invalid=JSON.parse(stable);invalid.terrain.changes=entries;assert.throws(()=>model.import(invalid));assert.equal(JSON.stringify(model.export()),stable);
}
const badMarker=JSON.parse(stable);badMarker.markers[0].x=-32;badMarker.terrain.changes=[[0,0]];assert.throws(()=>model.import(badMarker));assert.equal(JSON.stringify(model.export()),stable);
// Every cardinal/diagonal neighbourhood must choose a valid current-atlas tile,
// including isolated blocks and very narrow walls.
for(let mask=0;mask<512;mask++){
 const points=[];for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)points.push([46+dx,58+dy]);
 t.paint(points,0);for(let i=0;i<9;i++)if(mask&(1<<i))t.paint([points[i]],1);
 const id=t.tile(46,58);assert.ok(id===undefined||Number.isInteger(id)&&id>=0&&id<DATA.uniqueTiles,mask);
}
t.restore([]);
// A long drag changes every crossed cell once and is one undoable transaction.
const history=model.undoStack.length;model.begin();t.line([16,60],[35,60],1,3);model.end();assert.equal(model.undoStack.length,history+1);
for(let x=16;x<=35;x++)assert.equal(t.value(x,60),1);
model.undo();assert.equal(JSON.stringify([...t.visual].sort((a,b)=>a[0]-b[0])),baseline);
// Generate an actual tile-composition example for visual inspection.
model.begin();t.rectangle([20,68],[27,73],0);t.rectangle([20,73],[27,73],1);t.rectangle([29,71],[35,73],1);model.end();
fs.mkdirSync(path.join(root,'checks'),{recursive:true});fs.writeFileSync(path.join(root,'checks/edited-example.json'),JSON.stringify({document:model.export(),tiles:[...t.visual].map(([k,id])=>[k%t.cols,Math.floor(k/t.cols),id])}));
console.log(JSON.stringify({passed:true,checks:['unchanged initial tile IDs','cavity edge refresh','no ghost terrain','deterministic reload','exact undo','paint-back exact restoration','world bounds','invalid import atomicity','512 neighbourhoods valid atlas IDs','long stroke continuity','single-transaction history'],example:'checks/edited-example.json'}));
