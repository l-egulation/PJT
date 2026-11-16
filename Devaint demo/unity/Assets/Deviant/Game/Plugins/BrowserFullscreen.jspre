// Keep short Escape presses available to Unity while its canvas is in DOM fullscreen.
// The browser retains its long-Escape exit gesture and permission controls.
(function () {
  var existing = Module['preInit'];
  if (typeof existing === 'function') existing = [existing];
  Module['preInit'] = existing || [];
  Module['preInit'].push(function () {
    var keyboard = navigator.keyboard;
    if (!keyboard || typeof keyboard.lock !== 'function' || typeof keyboard.unlock !== 'function') return;
    var requested = false;
    function gameFullscreen() {
      var canvas = Module['canvas'];
      var element = document.fullscreenElement;
      return !!(canvas && element && (element === canvas || element.contains(canvas)));
    }
    function unlock() {
      try { keyboard.unlock(); } catch (error) { /* The document may already be closing. */ }
    }
    function update() {
      var active = gameFullscreen();
      if (active === requested) return;
      requested = active;
      if (!active) { unlock(); return; }
      try {
        Promise.resolve(keyboard.lock(['Escape'])).then(function () {
          // Permission may resolve after the user has already left fullscreen.
          if (!gameFullscreen()) unlock();
        }, function () { /* Unsupported policies or denied permission keep normal browser behavior. */ });
      } catch (error) { /* Some browser implementations can reject synchronously. */ }
    }
    document.addEventListener('fullscreenchange', update);
    update();
  });
})();
