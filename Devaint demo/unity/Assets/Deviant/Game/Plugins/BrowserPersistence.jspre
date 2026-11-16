// Enable IDBFS automatic persistence before Unity mounts /idbfs in preRun.
// This applies to permanent profiles and PlayerPrefs even when replay collection is off.
(function () {
  var existing = Module['preInit'];
  if (typeof existing === 'function') existing = [existing];
  Module['preInit'] = existing || [];
  Module['preInit'].push(function () {
    Module['autoSyncPersistentDataPath'] = true;
  });
})();
