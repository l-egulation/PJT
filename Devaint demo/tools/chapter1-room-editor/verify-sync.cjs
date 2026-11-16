const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'lost-words-room-sync-'));
const output = path.join(temporary, 'Chapter1Rooms.json');
const sync = path.join(__dirname, 'sync-unity.cjs');
const env = { ...process.env, CHAPTER1_ROOM_CATALOG_OUTPUT: output };
function run(...args) {
  return spawnSync(process.execPath, [sync, ...args], { env, encoding: 'utf8' });
}
function catalog() { return JSON.parse(fs.readFileSync(output, 'utf8')); }
function exported(room) {
  return { schema: 'chapter1-room-draft-v1', id: room.id, name: room.name,
    width: room.width, height: room.height, terrain: room.terrain, monsters: room.monsters };
}
try {
  assert.equal(run().status, 0);
  const first = exported(catalog().rooms[0]);
  first.monsters[0].species = '02_mole';
  const firstFile = path.join(temporary, 'first.json');
  fs.writeFileSync(firstFile, JSON.stringify(first));
  assert.equal(run(firstFile).status, 0);
  assert.equal(catalog().rooms[0].monsters[0].species, '02_mole');

  const second = exported(catalog().rooms[1]);
  second.monsters[0].species = '05_boar';
  const secondFile = path.join(temporary, 'second.json');
  fs.writeFileSync(secondFile, JSON.stringify(second));
  assert.equal(run(secondFile).status, 0);
  assert.equal(catalog().rooms[0].monsters[0].species, '02_mole');
  assert.equal(catalog().rooms[1].monsters[0].species, '05_boar');
  assert.equal(run('--check').status, 0);

  second.terrain[0].w -= 1;
  fs.writeFileSync(secondFile, JSON.stringify(second));
  assert.notEqual(run(secondFile).status, 0);
  assert.equal(catalog().rooms[1].terrain[0].w, second.terrain[0].w + 1);
  const d = catalog().rooms[9];
  const left = { ...exported(d), variant:'left', ports:d.ports.map(p=>({...p,side:'left'})),
    terrain:d.terrain.map(t=>({...t,x:d.width-t.x-t.w})),
    monsters:d.monsters.map(m=>({...m,x:d.width-m.x})) };
  left.monsters[0].species='02_mole';
  const leftFile=path.join(temporary,'left.json');
  fs.writeFileSync(leftFile,JSON.stringify(left));
  const mirroredSync=run(leftFile);
  assert.equal(mirroredSync.status,0,mirroredSync.stderr);
  assert.equal(catalog().rooms[9].monsters[0].x,d.monsters[0].x);
  assert.equal(catalog().rooms[9].monsters[0].species,'02_mole');
  assert.equal(run('--check').status,0);
  console.log('Room sync: sequential monster edits persist; changed terrain is rejected.');
} finally {
  if (temporary.startsWith(os.tmpdir() + path.sep)) fs.rmSync(temporary, { recursive: true, force: true });
}
