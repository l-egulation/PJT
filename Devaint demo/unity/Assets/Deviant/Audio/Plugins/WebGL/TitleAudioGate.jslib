mergeInto(LibraryManager.library, {
    $DeviantTitleAudioButton: {
        element: null,

        hide: function () {
            if (this.element) this.element.remove();
            this.element = null;
        },

        show: function (context) {
            if (this.element) return;
            var button = document.createElement('button');
            button.type = 'button';
            button.textContent = '소리 켜기';
            button.style.cssText = 'position:fixed;right:24px;bottom:24px;z-index:1000;padding:10px 16px;cursor:pointer';
            button.addEventListener('click', function () {
                context.resume().catch(function () {
                    // Leave the button available for another user gesture.
                });
            });
            var root = document.getElementById('unity-fullscreen-container') || document.body;
            root.appendChild(button);
            this.element = button;
        }
    },

    DeviantTitleAudioReady__deps: ['$WEBAudio', '$DeviantTitleAudioButton'],
    DeviantTitleAudioReady: function () {
        var context = WEBAudio.audioContext;
        if (!context) return 0;
        if (context.state === 'running' && WEBAudio.contextIsRunning) {
            DeviantTitleAudioButton.hide();
            return 1;
        }
        if (context.state !== 'running' && context.state !== 'closed')
            DeviantTitleAudioButton.show(context);
        return 0;
    },

    DeviantHideTitleAudioButton__deps: ['$DeviantTitleAudioButton'],
    DeviantHideTitleAudioButton: function () {
        DeviantTitleAudioButton.hide();
    }
});
