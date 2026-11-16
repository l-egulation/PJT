'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const C=require('../map-core.js');

const editor=fs.readFileSync(path.join(__dirname,'..','editor.js'),'utf8');
function actualFunction(start,end){
 const from=editor.indexOf(start),to=editor.indexOf(end,from);
 assert.ok(from>=0&&to>from,'The production editor function must be available to the regression harness.');
 return editor.slice(from,to);
}
const productionFunctions=actualFunction('function persist()','function sanitizeSelection()')+
 actualFunction('function finishDrag(','canvas.addEventListener(\'pointerup\'');

function harness(){
 const timers=new Map(),storage=new Map(),elements=new Map();
 let nextTimer=0;
 const context={
  C,model:C.seed(),saveTimer:0,storageAvailable:true,STORAGE:'test-layout',drag:null,
  clearTimeout:id=>timers.delete(id),
  setTimeout:callback=>{const id=++nextTimer;timers.set(id,callback);return id;},
  $:id=>{if(!elements.has(id))elements.set(id,{});return elements.get(id);},
  localStorage:{setItem:(key,value)=>storage.set(key,value)},
  canvas:{classList:{remove:()=>{}}},
  refresh:()=>{}
 };
 vm.createContext(context);
 vm.runInContext(productionFunctions,context);
 return {
  run:code=>vm.runInContext(code,context),
  flush:()=>{const callbacks=[...timers.values()];timers.clear();for(const callback of callbacks)callback();},
  saved:()=>JSON.parse(storage.get('test-layout')),
  live:()=>JSON.parse(JSON.stringify(context.model)),
  pending:()=>timers.size
 };
}

test('a pending automatic save excludes the next uncommitted drag position',()=>{
 const h=harness(),baseline=h.live().rooms[1].objects[0].x;
 h.run("model.note='이전에 확정한 편집';persist();model.rooms[1].objects[0].x+=4;");
 h.flush();
 assert.equal(h.saved().note,'이전에 확정한 편집');
 assert.equal(h.saved().rooms[1].objects[0].x,baseline);
 assert.equal(h.live().rooms[1].objects[0].x,baseline+4);
});

test('cancelling a drag after the previous save fires preserves the committed map on reload',()=>{
 const h=harness();
 h.run("model.rooms[1].objects[0].x+=1;persist();drag={kind:'object',before:C.clone(model)};model.rooms[1].objects[0].x+=4;");
 h.flush();
 h.run('finishDrag(true);');
 assert.deepEqual(h.saved(),h.live());
 assert.equal(h.saved().rooms[1].objects[0].x,C.seed().rooms[1].objects[0].x+1);
});

test('a later committed edit replaces the pending save with its own full snapshot',()=>{
 const h=harness();
 h.run("model.note='첫 편집';persist();model.note='두 번째 확정 편집';model.rooms[1].objects[0].x+=2;persist();");
 assert.equal(h.pending(),1);
 h.flush();
 assert.equal(h.saved().note,'두 번째 확정 편집');
 assert.deepEqual(h.saved(),h.live());
});
