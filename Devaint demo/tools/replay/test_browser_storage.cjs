// Stub test of our JS bridge only; real IndexedDB persistence needs a Web Player.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const callbacks = [];
const context = {
  LibraryManager: { library: {} },
  mergeInto: Object.assign,
  FS: { syncfs(populate, callback) { assert.equal(populate, false); callbacks.push(callback); } }
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(__dirname, '../../unity/Assets/Deviant/Replay/Unity/Plugins/ReplayStorage.jslib'), 'utf8'), context);
const library = context.LibraryManager.library;
context.DeviantReplayFS = library.$DeviantReplayFS;
assert.equal(library.DeviantReplaySyncStatus(), 0);
library.DeviantReplaySyncFS();
assert.equal(library.DeviantReplaySyncStatus(), 1);
library.DeviantReplaySyncFS();
library.DeviantReplaySyncFS();
assert.equal(callbacks.length, 1);
callbacks.shift()(null);
assert.equal(callbacks.length, 1); // Concurrent requests coalesce into one subsequent flush.
callbacks.shift()(new Error('test-only quota failure'));
assert.equal(library.DeviantReplaySyncStatus(), -1);
library.DeviantReplaySyncFS();
callbacks.shift()(null);
assert.equal(library.DeviantReplaySyncStatus(), 0);
context.FS.syncfs = () => { throw new Error('test-only synchronous failure'); };
library.DeviantReplaySyncFS();
assert.equal(library.DeviantReplaySyncStatus(), -1);
console.log('Replay browser storage bridge: passed');

// New players enable Unity autoPersist at preInit; preserve prior initialization hooks.
const bootstrap = fs.readFileSync(path.join(__dirname, '../../unity/Assets/Deviant/Game/Plugins/BrowserPersistence.jspre'), 'utf8');
let existingCalled = false;
context.Module = { preInit: () => { existingCalled = true; } };
vm.runInContext(bootstrap, context);
assert.equal(context.Module.autoSyncPersistentDataPath, undefined);
for (const init of context.Module.preInit) init();
assert.equal(existingCalled, true);
assert.equal(context.Module.autoSyncPersistentDataPath, true);
context.Module.__unityIdbfsMount = { idbPersistState: 'idb' };
context.FS.syncfs = () => { throw new Error('manual sync must not run alongside autoPersist'); };
library.DeviantReplaySyncFS();
assert.equal(library.DeviantReplaySyncStatus(), 1);
context.Module.__unityIdbfsMount.idbPersistState = 0;
assert.equal(library.DeviantReplaySyncStatus(), 0);
console.log('Automatic profile persistence initialization and replay compatibility: passed');

context.Module.__unityIdbfsMount = { mount: { idbPersistState: 'idb' } };
assert.equal(library.DeviantReplaySyncStatus(), 1);
context.Module.__unityIdbfsMount.mount.idbPersistState = 0;
assert.equal(library.DeviantReplaySyncStatus(), 0);
console.log('Unity IDBFS root-node/mount status: passed');
