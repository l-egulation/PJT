const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, 'app.js'), 'utf8');
const definitions = source.slice(0, source.indexOf('  const $ ='));
assert.ok(definitions.length < source.length, 'room definitions must precede the editor UI');
const context = {};
vm.runInNewContext(`${definitions}\n globalThis.rooms = rooms; globalThis.mirrorRoom=mirrorRoom; globalThis.migrateHills = migrateHills; globalThis.groundRoute = groundRoute; globalThis.forwardClearStatus = forwardClearStatus; })();`, context);

const rooms = context.rooms;
assert.equal(rooms.length, 12);
assert.equal(new Set(rooms.map(room => room.initial.id)).size, 12);
assert.equal(rooms[0].storage, 'lost-words-chapter1-room-forest-clearing-compact-v1');
assert.equal(new Set(rooms.map(room => room.storage)).size, 12);
assert.equal(new Set(rooms.map(room => JSON.stringify(room.initial.terrain))).size, 12);

for (const { initial } of rooms) {
  assert.equal(initial.width, 32, initial.name);
  assert.ok([18,27].includes(initial.height), initial.name);
  assert.equal(initial.monsters.length, 10, initial.name);
  assert.equal(initial.ports.length, 2, initial.name);
  for (let i = 0; i < initial.terrain.length; i++) {
    const a = initial.terrain[i];
    assert.ok(a.x >= 0 && a.y >= 0 && a.x + a.w <= initial.width && a.y + a.h <= initial.height, `${initial.name}: ${a.id} bounds`);
    for (const b of initial.terrain.slice(i + 1)) {
      const dx = Math.max(0, a.x - b.x - b.w, b.x - a.x - a.w);
      const dy = Math.max(0, a.y - b.y - b.h, b.y - a.y - a.h);
      const overlapX = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
      const overlapY = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
      assert.ok(overlapX <= 0 || overlapY <= 0, `${initial.name}: ${a.id} overlaps ${b.id}`);
      if (a.kind === 'wood' || b.kind === 'wood')
        assert.ok(Math.hypot(dx, dy) >= 1, `${initial.name}: ${a.id} too close to ${b.id}`);
    }
  }
  for (const monster of initial.monsters) {
    assert.ok(monster.x >= 2 && monster.x <= initial.width-2, `${initial.name}: monster near entrance`);
    assert.ok(initial.terrain.some(t => monster.x >= t.x + .4 && monster.x <= t.x + t.w - .4 && monster.y === t.y),
      `${initial.name}: unsupported ${monster.id}`);
  }
}
for (const entry of rooms) {
  assert.equal(context.groundRoute(entry.initial), true, `${entry.initial.name}: entry and exit ground`);
  assert.equal(context.forwardClearStatus(entry, entry.initial).ok, true, `${entry.initial.name}: forward clear route`);
  const brokenRoute = JSON.parse(JSON.stringify(entry.initial));
  brokenRoute.terrain = brokenRoute.terrain.filter(t => t.id !== entry.clearRoute.at(-1));
  assert.equal(context.forwardClearStatus(entry, brokenRoute).ok, false, `${entry.initial.name}: missing route is detected`);
  const saved = JSON.parse(JSON.stringify(entry.initial));
  delete saved.terrainRevision;
  saved.terrain = [{ id: 'old-floor', kind: 'ground', x: 0, y: 21, w: 48, h: 3 }];
  saved.monsters[0].species = '02_mole';
  const species = saved.monsters[0].species;
  const migrated = context.migrateHills(entry, saved);
  assert.equal(migrated.monsters[0].species, species, `${entry.initial.name}: edited monster species preserved`);
  assert.equal(context.groundRoute(migrated), true, `${entry.initial.name}: old geometry migrated to hills`);
  const edited = migrated.terrain[0];edited.w -= .5;
  context.migrateHills(entry, migrated);
  assert.equal(migrated.terrain[0].w, edited.w, `${entry.initial.name}: already migrated geometry is not reset`);
}
const brokenExit = JSON.parse(JSON.stringify(rooms[0].initial));
brokenExit.terrain.find(t => t.x+t.w===brokenExit.width).x -= 1;
assert.equal(context.groundRoute(brokenExit), false, 'disconnected exit ground must be flagged');
const digeut=rooms[9].initial, mirrored=context.mirrorRoom(digeut);
assert.equal(mirrored.ports[0].side,'left');
assert.equal(mirrored.ports[1].side,'left');
assert.equal(JSON.stringify(context.mirrorRoom(mirrored)),JSON.stringify(digeut));
assert.ok(context.forwardClearStatus(rooms[9],mirrored).ok);
const approved=JSON.parse(fs.readFileSync(path.join(__dirname,'../chapter1-compact-layout/approved-prototypes.json'))).templates;
for(const prototype of approved.filter(t=>t.id!=='straight')) {
 const room=rooms.find(r=>r.initial.id===prototype.id).initial;
 assert.equal(JSON.stringify(room.terrain),JSON.stringify(prototype.terrain),'approved terrain preserved');
 assert.equal(JSON.stringify(room.monsters),JSON.stringify(prototype.monsters),'approved monsters preserved');
}
console.log('12 compact rooms plus mirrored D: spacing, monster support, ordered routes, migration and approved geometry verified.');
