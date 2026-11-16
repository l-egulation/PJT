mergeInto(LibraryManager.library, {
  ProfilePersistenceProbePublish: function (json) {
    var state = JSON.parse(UTF8ToString(json));
    state.auto_persist = !!Module['autoSyncPersistentDataPath'];
    var root = Module.__unityIdbfsMount;
    var mount = root && (root.mount || root);
    state.persisting = !!(mount && mount.idbPersistState);
    window.__deviantProfilePersistenceProbe = state;
  }
});
