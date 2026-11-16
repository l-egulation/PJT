mergeInto(LibraryManager.library, {
  $DeviantReplayFS: { running: false, dirty: false, error: false },
  DeviantReplaySyncFS__deps: ['$DeviantReplayFS', '$FS'],
  DeviantReplaySyncFS: function () {
    // New Web players use Unity's serialized autoPersist queue for the entire persistent mount.
    // Avoid racing that queue with a second manual FS.syncfs operation.
    if (typeof Module !== 'undefined' && Module['autoSyncPersistentDataPath']) return;
    if (DeviantReplayFS.running) { DeviantReplayFS.dirty = true; return; }
    var pump = function () {
      DeviantReplayFS.running = true;
      DeviantReplayFS.dirty = false;
      try {
        FS.syncfs(false, function (err) {
          DeviantReplayFS.running = false;
          DeviantReplayFS.error = !!err;
          if (DeviantReplayFS.dirty) pump();
        });
      } catch (err) {
        DeviantReplayFS.running = false;
        DeviantReplayFS.error = true;
      }
    };
    pump();
  },
  DeviantReplaySyncStatus__deps: ['$DeviantReplayFS'],
  DeviantReplaySyncStatus: function () {
    if (typeof Module !== 'undefined' && Module['autoSyncPersistentDataPath']) {
      var root = Module.__unityIdbfsMount;
      var mount = root && (root.mount || root);
      return mount && mount.idbPersistState ? 1 : 0;
    }
    return DeviantReplayFS.error ? -1 : DeviantReplayFS.running ? 1 : 0;
  }
});
