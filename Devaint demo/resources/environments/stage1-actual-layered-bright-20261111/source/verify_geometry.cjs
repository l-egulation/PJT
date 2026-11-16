const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const main=process.argv[2]||path.resolve(__dirname,'../../../..');
const E=require(path.join(main,'tools/map/engine.js')),D=require(path.join(main,'tools/map/rooms.js'));
const local=JSON.parse(fs.readFileSync(path.join(__dirname,'rooms.json'),'utf8'));
for(const r of local.rooms){const s=D.s1.find(s=>s.id===r.id),actual=E.buildRoom(s,s.slots,false);assert.deepEqual(r.grid,Array.from(actual.grid));assert.deepEqual(r.plats,JSON.parse(JSON.stringify(actual.plats)));assert.equal(r.width,actual.fw);assert.equal(r.height,actual.fh);assert.deepEqual(r.active,actual.active);}
console.log(JSON.stringify({sourceCommit:local.sourceCommit,exactRoomGeometry:'PASS',rooms:local.rooms.length}));
