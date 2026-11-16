const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '../..');
const output = process.env.CHAPTER1_ROOM_CATALOG_OUTPUT
  ? path.resolve(process.env.CHAPTER1_ROOM_CATALOG_OUTPUT)
  : path.join(root, 'unity/Assets/ForestCombatV2/Resources/Chapter1Rooms.json');
const source = fs.readFileSync(path.join(__dirname, 'app.js'), 'utf8');
const definitions = source.slice(0, source.indexOf('  const $ ='));
assert.ok(definitions.length < source.length, 'Room definitions were not found');
const context = {};
vm.runInNewContext(`${definitions}\n globalThis.roomPresets = rooms; globalThis.forwardClearStatus = forwardClearStatus; globalThis.mirrorRoom = mirrorRoom; })();`, context);
const presets = context.roomPresets.map(entry => JSON.parse(JSON.stringify(entry.initial)));
const routes = new Map(context.roomPresets.map(entry => [entry.initial.id, entry]));
const sourceWorld = JSON.parse(fs.readFileSync(path.join(root, 'unity/Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json')));
const artwork = JSON.parse(fs.readFileSync(path.join(root, 'unity/Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json')));
const art = require('../../resources/tools/chapter1-rounded-terrain/tile-art.cjs');
const species = new Set(sourceWorld.monsters.map(monster => monster.id));
const overrides = new Map();
const args = process.argv.slice(2);
const check = args.includes('--check');
const defaults = args.includes('--defaults');
const existing = fs.existsSync(output) ? JSON.parse(fs.readFileSync(output, 'utf8')) : null;
for (const filename of args.filter(arg => arg !== '--check' && arg !== '--defaults')) {
  const draft = JSON.parse(fs.readFileSync(path.resolve(filename), 'utf8'));
  assert.equal(draft.schema, 'chapter1-room-draft-v1', `${filename}: invalid schema`);
  assert.ok(presets.some(room => room.id === draft.id), `${filename}: unknown room`);
  assert.ok(!overrides.has(draft.id), `${filename}: duplicate room`);
  overrides.set(draft.id, draft);
}

const sourceGround = (x, y) => x >= 0 && x < sourceWorld.cols && y >= 0 && y < sourceWorld.rows &&
  sourceWorld.ground[y * sourceWorld.cols + x] !== 0;
const mask = (occupied, x, y) => {
  let result = 0;
  for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++)
    if (occupied(x + dx, y + dy)) result |= 1 << ((dy + 1) * 3 + dx + 1);
  return result;
};
const samples = new Map();
for (const cell of artwork.ground) {
  const key = mask(sourceGround, cell.x, cell.y);
  if (!samples.has(key)) samples.set(key, []);
  const group = samples.get(key);
  if (group.length < 32) group.push({ id: cell.id, x: cell.x, y: cell.y });
}
const sampleKeys = [...samples.keys()];
function spriteFor(occupancy, x, y) {
  const target = mask(occupancy, x, y);
  let bestKey = target;
  if (!samples.has(target)) {
    let bestCost = Infinity;
    for (const candidate of sampleKeys) {
      if (Boolean(candidate & 16) !== Boolean(target & 16)) continue;
      let cost = 0;
      for (let bit = 0; bit < 9; bit++)
        if (((candidate ^ target) & (1 << bit)) !== 0) cost += bit === 1 || bit === 3 || bit === 5 || bit === 7 ? 3 : 1;
      if (cost < bestCost) { bestCost = cost; bestKey = candidate; }
    }
  }
  const group = samples.get(bestKey);
  assert.ok(group, `No terrain sprite for ${x},${y}`);
  const sample = group.find(entry => entry.x % 4 === x % 4 && entry.y % 4 === y % 4) ||
    group.find(entry => entry.x % 4 === x % 4) || group[0];
  return sample.id;
}

const platformArt = new Map(artwork.platforms.map(cell => [`${cell.x},${cell.y}`, cell.id]));
const woodSamples = sourceWorld.platforms.filter(p => !p.earthArtwork).map(platform => {
  const x = platform.x / 32, y = platform.y / 32, width = platform.w / 32;
  const ids = Array.from({ length: width }, (_, index) => platformArt.get(`${x + index},${y}`));
  return ids.every(Number.isInteger) ? ids : null;
}).filter(Boolean).sort((a, b) => a.length - b.length);

function validate(room) {
  assert.equal(room.width, 32, `${room.name}: width`);
  assert.ok([18,27].includes(room.height), `${room.name}: height`);
  assert.ok(room.monsters.length >= 10, `${room.name}: at least 10 monsters`);
  const occupied = Array.from({ length: room.width * room.height }, () => false);
  for (const terrain of room.terrain) {
    assert.ok(['ground', 'earth', 'wood'].includes(terrain.kind), `${room.name}: terrain type`);
    assert.ok(terrain.x >= 0 && terrain.y >= 0 && terrain.x + terrain.w <= room.width && terrain.y + terrain.h <= room.height,
      `${room.name}: terrain bounds`);
    assert.ok(Number.isInteger(terrain.x) && Number.isInteger(terrain.w) &&
      (terrain.kind === 'wood' ? terrain.y * 2 === Math.round(terrain.y * 2) :
        Number.isInteger(terrain.y) && Number.isInteger(terrain.h)), `${room.name}: tile alignment`);
    if (terrain.kind === 'wood') continue;
    for (let y = terrain.y; y < terrain.y + terrain.h; y++) for (let x = terrain.x; x < terrain.x + terrain.w; x++) {
      assert.ok(!occupied[y * room.width + x], `${room.name}: overlapping solid terrain at ${x},${y}`);
      occupied[y * room.width + x] = true;
    }
  }
  assert.ok(room.ports.every(p=>room.terrain.some(t=>t.y===p.y&&(p.side==='left'?t.x===0:t.x+t.w===room.width))), `${room.name}: unsupported port`);
  let previousX = -Infinity;
  for (const monster of [...room.monsters].sort((a, b) => a.x - b.x)) {
    assert.ok(species.has(monster.species), `${room.name}: unknown ${monster.species}`);
    assert.ok(monster.x >= 2 && monster.x <= room.width-2, `${room.name}: monster order or port`);
    assert.ok(room.terrain.some(t => monster.x >= t.x + .4 && monster.x <= t.x + t.w - .4 &&
      Math.abs(monster.y - t.y) < .26), `${room.name}: unsupported ${monster.id}`);
    previousX = monster.x;
  }
  const clear = context.forwardClearStatus(routes.get(room.id), room);
  assert.ok(clear.ok, `${room.name}: ${clear.reason}`);
  return occupied;
}

const catalog = {
  schema: 'chapter1-authored-rooms-v1',
  rooms: presets.map(room => {
    let override = overrides.get(room.id);
    if(override?.variant==='left') {
      assert.ok(room.mirrorable, `${room.name}: only the D room supports mirroring`);
      override=JSON.parse(JSON.stringify(context.mirrorRoom(override)));
    }
    if (override) {
      assert.equal(override.width, room.width);
      assert.equal(override.height, room.height);
      assert.deepEqual(override.terrain, room.terrain, `${room.name}: fixed terrain differs from the preset`);
      room.monsters = override.monsters;
    } else if (!defaults && existing && existing.schema === 'chapter1-authored-rooms-v1') {
      const saved = existing.rooms.find(entry => entry.id === room.id);
      if (saved && saved.width===room.width && saved.height===room.height && saved.terrainRevision===room.terrainRevision) room.monsters = saved.monsters;
    }
    const occupied = validate(room);
    const value = (x, y) => x >= 0 && x < room.width && y >= 0 && y < room.height && occupied[y * room.width + x] ? 1 : 0;
    const ground = [];
    for (let y = 0; y < room.height; y++) for (let x = 0; x < room.width; x++)
      if (art.groundSpec({ value }, x, y)) ground.push({ x, y, id: spriteFor((px, py) => value(px, py) !== 0, x, y) });
    const platforms = [];
    for (const terrain of room.terrain.filter(t => t.kind === 'wood')) {
      const sample = woodSamples.find(ids => ids.length === terrain.w) ||
        woodSamples.find(ids => ids.length > terrain.w) || woodSamples[woodSamples.length - 1];
      assert.ok(sample, 'Approved wood platform artwork is missing');
      for (let i = 0; i < terrain.w; i++) {
        const index = i === 0 ? 0 : i === terrain.w - 1 ? sample.length - 1 : 1 + (i - 1) % (sample.length - 2);
        platforms.push({ x: terrain.x + i, y: Math.floor(terrain.y), id: sample[index], offsetY: terrain.y % 1 });
      }
    }
    return { id: room.id, name: room.name, width: room.width, height: room.height, terrainRevision: room.terrainRevision,
      ports: room.ports.map((p,i)=>({...p,role:i?'exit':'entry'})), mirrorable: !!room.mirrorable, clearRoute: room.clearRoute || routes.get(room.id).clearRoute,
      terrain: room.terrain, monsters: room.monsters, renderGround: ground, renderPlatforms: platforms };
  })
};
const json = JSON.stringify(catalog) + '\n';
if (check) {
  assert.equal(fs.readFileSync(output, 'utf8'), json, 'Unity room catalog is stale; run sync-unity.cjs');
  console.log(`Unity room catalog current: ${catalog.rooms.length} templates`);
} else {
  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, json);
  console.log(`Wrote ${catalog.rooms.length} Unity room templates to ${output}`);
}
