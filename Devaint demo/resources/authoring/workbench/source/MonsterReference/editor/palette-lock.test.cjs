const test=require('node:test'),assert=require('node:assert/strict'),Core=require('./editor-core.js');
const palette=['#171716','#D6D0C7'],black=[23,23,22,255],cream=[214,208,199,255],red=[255,0,0,255];
function model(locked=true,pixels=[...black,...cream]){const raw={images:{0:'data:image/png;base64,AA=='},sourceFrames:[0],width:2,height:1,ms:[100],palette,paletteLock:locked};return new Core.Model(Core.normalize(raw),{0:Core.plane(2,1,pixels)})}
test('locked projects reject off-palette and partially transparent imported pixels',()=>{
 assert.throws(()=>model(true,[...red,...cream]),/마스터/);
 assert.throws(()=>model(true,[23,23,22,128,...cream]),/마스터/);
 assert.throws(()=>model(true,[23,23,22,0,...cream]),/마스터/);
});
test('pen and fill refuse forbidden colors without changing the frame',()=>{
 const m=model(),before=Array.from(m.body().data);
 assert.throws(()=>m.paint('body',0,0,red),/마스터/);
 assert.throws(()=>m.fill('body',0,0,red),/마스터/);
 assert.throws(()=>m.paint('body',0,0,[...black.slice(0,3),128]),/마스터/);
 assert.deepEqual(Array.from(m.body().data),before);
});
test('allowed paint, fill, erase, undo and redo retain a valid frame',()=>{
 const m=model();m.begin('body');m.paint('body',0,0,cream);m.commit();m.undo();assert.deepEqual(m.pick('body',0,0),black);m.redo();assert.deepEqual(m.pick('body',0,0),cream);
 m.fill('body',0,0,black);assert.deepEqual(m.pick('body',1,0),black);m.paint('body',0,0,[0,0,0,0]);assert.doesNotThrow(()=>m.export(()=> 'data:image/png;base64,AA=='));
});
test('export refuses a frame mutated outside paint/fill',()=>{
 const m=model();m.body().data.set(red,0);assert.throws(()=>m.export(()=> 'data:image/png;base64,AA=='),/마스터/);
});
test('legacy projects without a palette lock keep their color editing behavior',()=>{
 const m=model(false);m.paint('body',0,0,red);assert.deepEqual(m.pick('body',0,0),red);assert.doesNotThrow(()=>m.export(()=> 'data:image/png;base64,AA=='));
});
