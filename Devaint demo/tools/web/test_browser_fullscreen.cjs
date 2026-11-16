// Exercises the shipped preInit hook; browser permission/fullscreen input still needs a Web Player.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const hook = fs.readFileSync(path.join(__dirname, '../../unity/Assets/Deviant/Game/Plugins/BrowserFullscreen.jspre'), 'utf8');
const flushPromises = () => new Promise(setImmediate);

function fixture(keyboard) {
  const listeners = new Map();
  const canvas = {};
  const document = { fullscreenElement: null, addEventListener: (name, fn) => listeners.set(name, fn) };
  let previousCalls = 0;
  const context = { Module: { canvas, preInit: () => previousCalls++ }, navigator: { keyboard }, document };
  vm.runInNewContext(hook, context);
  for (const init of context.Module.preInit) init();
  assert.equal(previousCalls, 1);
  return {
    setFullscreen(element) { document.fullscreenElement = element; listeners.get('fullscreenchange')?.(); },
    container: { contains: value => value === canvas }, canvas, listeners,
  };
}

(async () => {
  const locks = [];
  let unlocks = 0, resolveLock;
  const f = fixture({ lock: keys => { locks.push([...keys]); return new Promise(resolve => { resolveLock = resolve; }); }, unlock: () => unlocks++ });
  f.setFullscreen({ contains: () => false });
  assert.equal(locks.length, 0, 'Other page elements must not lock Escape.');
  f.setFullscreen(f.container);
  assert.deepEqual(locks, [['Escape']]);
  f.setFullscreen(f.container);
  assert.equal(locks.length, 1, 'Repeated fullscreen events must not repeat permission requests.');
  f.setFullscreen(null);
  assert.equal(unlocks, 1);
  resolveLock(); await flushPromises();
  assert.equal(unlocks, 2, 'A delayed permission result must not leave the keyboard locked after exit.');
  f.setFullscreen(f.canvas);
  assert.equal(locks.length, 2);
  resolveLock(); await flushPromises();
  assert.equal(unlocks, 2);
  f.setFullscreen(null);
  assert.equal(unlocks, 3);
  assert.deepEqual([...f.listeners.keys()], ['fullscreenchange'], 'Unity keyboard events must remain untouched.');

  for (const keyboard of [undefined, {}, { lock() {} }]) {
    const unsupported = fixture(keyboard);
    unsupported.setFullscreen(unsupported.canvas);
    assert.equal(unsupported.listeners.size, 0);
  }
  for (const fail of [() => Promise.reject(new Error('permission denied')), () => { throw new Error('not supported'); }]) {
    const denied = fixture({ lock: fail, unlock() {} });
    denied.setFullscreen(denied.canvas);
    await flushPromises();
    denied.setFullscreen(null);
  }
  console.log('Browser fullscreen keyboard lock: passed (Escape only, canvas scope, exit/race cleanup, denied/unsupported fallback).');
})().catch(error => { console.error(error); process.exitCode = 1; });
